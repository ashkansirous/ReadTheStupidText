// Crash and error reports from the phone apps (Slice 227). Dino Reader and Read The Stupid Text post one report per crash:
//   POST /api/crash   { "schemaVersion": 1, "app", "platform", "version", "channel", "kind", "exceptionType", "message", "stack", "logTail", "minute", "device"?, "session"? }
// Each valid report is kept whole in R2 (`crashes/<app>/<yyyy-mm-dd>/<random id>.json` in the downloads bucket) and counted as one point in the
// Analytics Engine dataset `app_crashes` (binding APP_CRASHES). Nothing identifies the sender: the object is named with a random id made here, the IP is
// never stored (the rate limiter sees it as its key and keeps no record for us), and the only things taken from the connection are the country code
// and the closed device buckets, checked by the same helpers as the usage counts (events.ts). When the remote config has `crashReports.enabled`
// off or missing the request is answered like a success and nothing is written; likewise an unstamped version and an Intel android/ios device (a robot
// or emulator). Like events.ts this file is importable by node's type stripping (worker/crashes.test.mjs): keep it free of enums and parameter properties.

import { JSON_CONTENT_TYPE, NO_CACHE } from "./download-keys.mjs";
import {
  VERSION_PATTERN,
  coreBound,
  countryOf,
  isRecord,
  isRobotCount,
  isUnstampedVersion,
  matches,
  oneOf,
  parseDevice,
  ramBound,
  readCappedText,
  reply,
  requestCountry,
  SESSION_PATTERN,
} from "./events.ts";
import type { DeviceContext } from "./events.ts";

export const CRASH_PATH = "/api/crash";

/** The request format; the Worker reads the fields it knows and ignores the rest. */
export const CRASH_SCHEMA_VERSION = 1;

/** The R2 folder every report is kept under. */
export const CRASH_PREFIX = "crashes/";

/** The body is at most this many bytes (the app trims its log tail to fit). */
export const MAX_CRASH_BODY_BYTES = 64 * 1024;

export const MESSAGE_BYTES = 1024;
export const STACK_BYTES = 16 * 1024;
export const LOG_TAIL_BYTES = 32 * 1024;
export const EXCEPTION_TYPE_BYTES = 200;

const ALLOWED_METHODS = "POST";
const APPS = ["dino", "rtst"] as const;
const PLATFORMS = ["android", "ios"] as const;
export const USER_REPORT_KIND = "user_report";
const KINDS = ["managed", "native", "unclean_exit", "anr", "low_memory", "hang", USER_REPORT_KIND] as const;
// A user report (Slice 249, "Report a problem") names itself with a code the person can quote: 8 letters and digits without 0, O, 1, I and L, carried as the message
// `user report <code>`. Nothing else about it is free text. The app's ReportCode.Alphabet must match.
const USER_REPORT_MESSAGE = /^user report ([A-HJ-KM-NP-Z2-9]{8})$/;
const USER_REPORT_PREFIX = "report-";
const CHANNELS = ["store", "testflight"] as const;
const TESTFLIGHT = "testflight";
const IOS = "ios";
const OTHER_TYPE = "other";
const UNKNOWN_CLIENT = "unknown";
// The exception type or the signal text: letters, digits and the punctuation of a .NET type name or a "key=value" native description.
const TYPE_PATTERN = /^[A-Za-z0-9_.`+<>[\],=:$ -]{1,200}$/;
const MINUTE_PATTERN = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}Z$/;
const REPLACEMENT_CHARACTER = "�";
const ErrorCode = {
  BadRequest: "bad-request",
  TooLarge: "body-too-large",
  Unavailable: "crashes-unavailable",
  NotKept: "report-not-kept",
  RateLimited: "rate-limited",
} as const;

/** What this module needs from the Worker's bindings (declared in both wrangler configs). */
export interface CrashesEnv {
  readonly DOWNLOADS?: R2Bucket;
  /** The Analytics Engine dataset `app_crashes`. */
  readonly APP_CRASHES?: AnalyticsEngineDataset;
  /** A rate limit per connection; required: without it every report is answered 503 and nothing is written. */
  readonly CRASH_LIMITER?: RateLimit;
}

/** True while the remote config has crash reports on; false when off or missing. Rejects when the config cannot be read (answered 503, so the app retries). */
export type CrashFlagCheck = () => Promise<boolean>;

/** A report that passed every check; these are exactly the fields that reach R2 and the dataset. */
export interface CrashReport {
  readonly app: string;
  readonly platform: string;
  readonly version: string;
  readonly channel: string;
  readonly kind: string;
  readonly exceptionType: string;
  readonly message: string;
  readonly stack: string;
  readonly logTail: string;
  readonly minute: string;
  readonly device: DeviceContext;
  /** Slice 250: the usage session id of the launch the report was made in (32 hex digits), so its usage rows can be found; absent when the build sent none or a malformed one. */
  readonly session?: string;
}

/** The code of a user report (`user_report` kind) from its message, or null for any other kind or a message that is not `user report <code>`. */
export function userReportCode(report: Pick<CrashReport, "kind" | "message">): string | null {
  if (report.kind !== USER_REPORT_KIND) return null;
  return USER_REPORT_MESSAGE.exec(report.message)?.[1] ?? null;
}

const encoder = new TextEncoder();
const decoder = new TextDecoder();

/** The start of `text` within `maxBytes` of UTF-8, cut on a whole character. */
export function capHead(text: string, maxBytes: number): string {
  const bytes = encoder.encode(text);
  if (bytes.length <= maxBytes) return text;
  return decoder.decode(bytes.slice(0, maxBytes)).replaceAll(REPLACEMENT_CHARACTER, "");
}

/** The end of `text` within `maxBytes` of UTF-8 (the newest part of a log), cut on a whole character. */
export function capTail(text: string, maxBytes: number): string {
  const bytes = encoder.encode(text);
  if (bytes.length <= maxBytes) return text;
  return decoder.decode(bytes.slice(bytes.length - maxBytes)).replaceAll(REPLACEMENT_CHARACTER, "");
}

function textOrEmpty(value: unknown): string {
  return typeof value === "string" ? value : "";
}

function minuteOrNow(value: unknown, now: Date): string {
  return matches(MINUTE_PATTERN, value) ? value : now.toISOString().slice(0, 16) + "Z";
}

function typeOrOther(value: unknown): string {
  const capped = capHead(textOrEmpty(value), EXCEPTION_TYPE_BYTES);
  return TYPE_PATTERN.test(capped) ? capped : OTHER_TYPE;
}

/**
 * The checked report in `raw` for the app whose host it arrived on, or null when a field is missing, of the wrong type or outside its list or pattern
 * (an unstamped version is also null: it is dropped, not an error; see `isDroppedReport`).
 */
export function parseCrash(raw: unknown, app: string, now: Date = new Date()): CrashReport | null {
  if (!isRecord(raw) || raw["schemaVersion"] !== CRASH_SCHEMA_VERSION) return null;
  const { platform, version, channel, kind } = raw;
  const valid =
    raw["app"] === app &&
    oneOf(APPS, app) &&
    oneOf(PLATFORMS, platform) &&
    matches(VERSION_PATTERN, version) &&
    oneOf(CHANNELS, channel) &&
    oneOf(KINDS, kind) &&
    (channel !== TESTFLIGHT || platform === IOS); // TestFlight is an iOS channel: nothing authenticates the sender, so an Android "testflight" report is refused
  if (!valid) return null;
  const checked = {
    app,
    platform,
    version,
    channel,
    kind,
    exceptionType: typeOrOther(raw["exceptionType"]),
    message: capHead(textOrEmpty(raw["message"]), MESSAGE_BYTES),
    stack: capHead(textOrEmpty(raw["stack"]), STACK_BYTES),
    logTail: capTail(textOrEmpty(raw["logTail"]), LOG_TAIL_BYTES),
    minute: minuteOrNow(raw["minute"], now),
    device: parseDevice(raw["device"]),
    ...(matches(SESSION_PATTERN, raw["session"]) ? { session: raw["session"] } : {}),
  };
  // A user report without a valid code cannot be found again, so it is refused rather than kept under a made-up name.
  if (kind === USER_REPORT_KIND && userReportCode(checked) === null) return null;
  return checked;
}

/** True for a well-formed report that is deliberately not kept: an unstamped (dev) version, or an android/ios report from an Intel device (a robot or emulator). */
export function isDroppedReport(raw: unknown, report: CrashReport): boolean {
  return isUnstampedVersion(report.version) || isRobotCount(raw, report.device);
}

/** The R2 key of a report received on `day` (yyyy-mm-dd) with the random id `id`. */
export function crashKey(app: string, day: string, id: string): string {
  return `${CRASH_PREFIX}${app}/${day}/${id}.json`;
}

/** The R2 key of a user report: `crashes/<app>/<day>/report-<code>.json`, so `pull-crashes.mjs --code` finds it by name. */
export function userReportKey(app: string, day: string, code: string): string {
  return `${CRASH_PREFIX}${app}/${day}/${USER_REPORT_PREFIX}${code}.json`;
}

/** The Analytics Engine point for a report: the kind is the sampling index, every field a blob, the doubles the report's weight and the core and RAM lower bounds. */
export function toCrashDataPoint(report: CrashReport, country: string): AnalyticsEngineDataPoint {
  const { device } = report;
  return {
    indexes: [report.kind],
    blobs: [
      report.app, report.platform, report.version, report.channel, report.kind, report.exceptionType,
      country, device.osMajor, device.formFactor, device.arch, device.cores, device.ram,
    ],
    doubles: [1, coreBound(device.cores), ramBound(device.ram)],
  };
}

/** True for the one path this module answers (after the `/dino` prefix is stripped). */
export function isCrashPath(pathname: string): boolean {
  return pathname === CRASH_PATH;
}

async function readBody(request: Request): Promise<unknown | "too-large" | "invalid"> {
  const declared = Number(request.headers.get("Content-Length") ?? 0);
  if (declared > MAX_CRASH_BODY_BYTES) return "too-large";
  const text = await readCappedText(request, MAX_CRASH_BODY_BYTES);
  if (text === "too-large") return text;
  try {
    return JSON.parse(text);
  } catch {
    return "invalid";
  }
}

// One report per connection at a time is plenty; the IP is only the limiter's key and is never written anywhere by this Worker.
async function withinRateLimit(request: Request, limiter: RateLimit): Promise<boolean> {
  const key = request.headers.get("CF-Connecting-IP") ?? UNKNOWN_CLIENT;
  return (await limiter.limit({ key })).success;
}

// One rule for a user report (Slice 249): it shows its sender a code, so it is answered 2xx only by the single success line at the end of handleCrash, after its R2 put
// succeeded. Every other path for it (off, rate limited, unreadable body, dropped device, put error) is non-2xx, so the app never says "Sent" for something nobody stored.
// While crash reports are off, only a body PROVEN to be a crash report (a record whose kind is not a user report) is answered like a success and dropped; the body is read
// (capped) before the rate limit decides, because the kind is needed to choose the answer. Anything else is refused.
async function answerWhileOff(request: Request, env: CrashesEnv): Promise<Response> {
  const body = await readBody(request);
  if (body === "too-large") return reply(413, ErrorCode.TooLarge);
  if (!isRecord(body) || body["kind"] === USER_REPORT_KIND) return answerUnkept(request, env, body);
  return reply(204);
}

// A user report or an unreadable body while off: 429 when over the rate limit, 503 when it is a user report (unavailable), 400 when it is not even a record.
async function answerUnkept(request: Request, env: CrashesEnv, body: unknown): Promise<Response> {
  if (env.CRASH_LIMITER && !(await withinRateLimit(request, env.CRASH_LIMITER))) return reply(429, ErrorCode.RateLimited);
  return isRecord(body) ? reply(503, ErrorCode.Unavailable) : reply(400, ErrorCode.BadRequest);
}

const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * The `yyyy-mm-dd` a user report is filed under: the day it was made (its `minute`), so a retry of the same report after midnight lands on the same object; a time more than
 * a day from `now` (a wrong clock, a made-up value) is not trusted and `now`'s day is used.
 */
export function userReportDay(report: Pick<CrashReport, "minute">, now: Date): string {
  const made = Date.parse(report.minute);
  const trusted = Number.isFinite(made) && Math.abs(now.getTime() - made) <= DAY_MS;
  return (trusted ? report.minute : now.toISOString()).slice(0, 10);
}

async function keepReport(env: CrashesEnv & { readonly DOWNLOADS: R2Bucket }, report: CrashReport, country: string, now: Date): Promise<void> {
  const code = userReportCode(report);
  const day = code === null ? now.toISOString().slice(0, 10) : userReportDay(report, now);
  const key = code === null ? crashKey(report.app, day, crypto.randomUUID()) : userReportKey(report.app, day, code);
  await env.DOWNLOADS.put(key, JSON.stringify({ schemaVersion: CRASH_SCHEMA_VERSION, ...report, country }), { httpMetadata: { contentType: JSON_CONTENT_TYPE } });
  env.APP_CRASHES?.writeDataPoint(toCrashDataPoint(report, country));
}

/**
 * Answers POST /api/crash for `app` (the product whose host it arrived on): 204 when kept or when a crash report is deliberately dropped (off, unstamped, a robot). A user report
 * gets 204 only from the line after its R2 put succeeded; every other path for it is non-2xx (422 dropped, 503 off or put error, 429 limited, 400 unreadable). 400 for a body that
 * is not a report, 413 for one over 64 KB, 429 over the rate limit, 503 when the config or the bucket cannot be reached (the app keeps the report and retries).
 */
export async function handleCrash(request: Request, env: CrashesEnv, crashReportsEnabled: CrashFlagCheck, app: string): Promise<Response> {
  if (request.method !== "POST") {
    return new Response(null, { status: 405, headers: { Allow: ALLOWED_METHODS, "Cache-Control": NO_CACHE } });
  }
  // The cheap checks come first: an unconfigured endpoint never reads the body, and a switched-off one reads it only (capped, rate-limited) to tell a user report from a crash report.
  const enabled = await crashReportsEnabled().catch(() => null);
  if (enabled === null) return reply(503, ErrorCode.Unavailable);
  if (!enabled) return answerWhileOff(request, env);
  // The rate limit is required: without its binding nothing is read or written (503), so a misconfigured deploy never takes unlimited reports.
  if (!env.DOWNLOADS || !env.CRASH_LIMITER) return reply(503, ErrorCode.Unavailable);
  if (!(await withinRateLimit(request, env.CRASH_LIMITER))) return reply(429, ErrorCode.RateLimited);
  const body = await readBody(request);
  if (body === "too-large") return reply(413, ErrorCode.TooLarge);
  const now = new Date();
  const report = body === "invalid" ? null : parseCrash(body, app, now);
  if (report === null) return reply(400, ErrorCode.BadRequest);
  if (isDroppedReport(body, report)) {
    // A crash report is dropped silently; a user report shows its sender a code, so it is answered 422 (the app takes any non-2xx as failed and does not queue a 422 for retry).
    return report.kind === USER_REPORT_KIND ? reply(422, ErrorCode.NotKept) : reply(204);
  }
  try {
    await keepReport({ ...env, DOWNLOADS: env.DOWNLOADS }, report, countryOf(requestCountry(request)), now);
  } catch {
    return reply(503, ErrorCode.Unavailable);
  }
  return reply(204);
}
