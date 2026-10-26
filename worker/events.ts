// Dino Reader's anonymous usage counts (Slice 133, Decisions 140-142). The app batches a session's counts into one
//   POST /api/events    { "schemaVersion": 1, "events": [ { name, book, language, source, hasRecording, version, platform, day, sampleRate } ] }
// and this writes each valid one to the Workers Analytics Engine dataset bound as USAGE (`dino_usage`). Nothing about the
// caller is read, stored or written: no IP, no user agent, no id; the only fields kept are the ones named below, each checked
// against a closed list or a strict pattern, so a changed or hostile client can add no free text. When the remote config
// (Slice 132) has telemetry off or missing, the request is answered like a success and nothing is written. Only Dino Reader's
// host and its /dino/ prefix on workers.dev route here (index.ts), which also supplies the telemetry check (it reads the
// config object). Like appconfig.ts this file imports values only from download-keys.mjs so worker/events.test.mjs can run it
// under node's type stripping: keep it free of enums and parameter properties.

import { JSON_CONTENT_TYPE, NO_CACHE, STORY_ID_PATTERN } from "./download-keys.mjs";

export const EVENTS_PATH = "/api/events";

/** The request format; the Worker reads `events` and ignores fields it does not know. */
export const EVENTS_SCHEMA_VERSION = 1;

/** One request carries at most this many counts. */
export const MAX_EVENTS_PER_REQUEST = 50;

const MAX_BODY_BYTES = 32 * 1024;
const ALLOWED_METHODS = "POST";
const OWN_BOOK = "own";
const SHELF_SOURCE = "shelf";
const PLATFORMS = ["android", "ios", "windows"] as const;
const EVENT_NAMES = ["book_opened", "book_finished"] as const;
const SOURCES = [SHELF_SOURCE, OWN_BOOK] as const;
const LANGUAGE_PATTERN = /^[a-z]{2,3}(-[A-Za-z]{2,4})?$/;
const VERSION_PATTERN = /^\d{1,6}(\.\d{1,6}){1,3}$/;
const MIN_SAMPLE_RATE = 0.001;
const DAY_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;
const ErrorCode = {
  BadRequest: "bad-request",
  TooMany: "too-many-events",
  TooLarge: "body-too-large",
  Unavailable: "events-unavailable",
} as const;

/** A count that passed every check; these are exactly the fields that reach the dataset. */
export interface UsageEvent {
  readonly name: string;
  readonly book: string;
  readonly language: string;
  readonly source: string;
  readonly hasRecording: boolean;
  readonly version: string;
  readonly platform: string;
  readonly day: string;
  readonly sampleRate: number;
}

/** What this module needs from the Worker's bindings (`USAGE` is declared in wrangler.jsonc). */
export interface EventsEnv {
  readonly USAGE?: AnalyticsEngineDataset;
}

/** True while the remote config has telemetry on; false when it is off or missing. Rejects when the config cannot be read (answered 503, so the app retries). */
export type TelemetryCheck = () => Promise<boolean>;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function oneOf(list: readonly string[], value: unknown): value is string {
  return typeof value === "string" && list.includes(value);
}

function matches(pattern: RegExp, value: unknown): value is string {
  return typeof value === "string" && pattern.test(value);
}

function isRealDay(value: unknown): value is string {
  const parts = typeof value === "string" ? DAY_PATTERN.exec(value) : null;
  if (parts === null) return false;
  const [year, month, day] = [Number(parts[1]), Number(parts[2]), Number(parts[3])];
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

// A shelf book is named by its package id; an own book never carries one, so the source and the book must agree.
function bookMatchesSource(book: unknown, source: unknown): book is string {
  return source === OWN_BOOK ? book === OWN_BOOK : matches(STORY_ID_PATTERN, book);
}

/** The checked count in `raw`, or null when any field is missing, of the wrong type or outside its list or pattern. */
export function parseEvent(raw: unknown): UsageEvent | null {
  if (!isRecord(raw)) return null;
  const { name, book, language, source, hasRecording, version, platform, day, sampleRate } = raw;
  const valid =
    oneOf(EVENT_NAMES, name) &&
    oneOf(SOURCES, source) &&
    bookMatchesSource(book, source) &&
    matches(LANGUAGE_PATTERN, language) &&
    typeof hasRecording === "boolean" &&
    matches(VERSION_PATTERN, version) &&
    oneOf(PLATFORMS, platform) &&
    isRealDay(day) &&
    typeof sampleRate === "number" &&
    sampleRate >= MIN_SAMPLE_RATE &&
    sampleRate <= 1;
  return valid ? { name, book, language, source, hasRecording, version, platform, day, sampleRate } : null;
}

/** Every valid count in `events`, in order; invalid ones are dropped one by one. */
export function validEvents(events: readonly unknown[]): UsageEvent[] {
  return events.map(parseEvent).filter((event): event is UsageEvent => event !== null);
}

/** The Analytics Engine data point for a count: the name is the sampling index, every field a blob, and the double is the count's weight (1 / the sample rate the app counted at), so SUM(double) estimates the true total. */
export function toDataPoint(event: UsageEvent): AnalyticsEngineDataPoint {
  return {
    indexes: [event.name],
    blobs: [event.name, event.book, event.language, event.source, event.hasRecording ? "yes" : "no", event.version, event.platform, event.day],
    doubles: [1 / event.sampleRate],
  };
}

function reply(status: number, code?: string): Response {
  const headers: Record<string, string> = { "Cache-Control": NO_CACHE };
  if (code === undefined) return new Response(null, { status, headers });
  headers["Content-Type"] = JSON_CONTENT_TYPE;
  return new Response(JSON.stringify({ code }), { status, headers });
}

// Reads at most MAX_BODY_BYTES: the stream is cut off as soon as it passes the cap, so a chunked or lying request is never buffered whole.
async function readCappedText(request: Request): Promise<string | "too-large"> {
  if (request.body === null) return "";
  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    total += value.byteLength;
    if (total > MAX_BODY_BYTES) {
      await reader.cancel();
      return "too-large";
    }
    chunks.push(value);
  }
  return new TextDecoder().decode(await new Blob(chunks).arrayBuffer());
}

async function readBody(request: Request): Promise<unknown | "too-large" | "invalid"> {
  const declared = Number(request.headers.get("Content-Length") ?? 0);
  if (declared > MAX_BODY_BYTES) return "too-large";
  const text = await readCappedText(request);
  if (text === "too-large") return text;
  try {
    return JSON.parse(text);
  } catch {
    return "invalid";
  }
}

/** True for the one path this module answers (after the `/dino` prefix is stripped). */
export function isEventsPath(pathname: string): boolean {
  return pathname === EVENTS_PATH;
}

/** Answers POST /api/events: 204 when taken or deliberately dropped, 400/413 for a body that is not a batch of counts. */
export async function handleEvents(request: Request, env: EventsEnv, telemetryEnabled: TelemetryCheck): Promise<Response> {
  if (request.method !== "POST") {
    return new Response(null, { status: 405, headers: { Allow: ALLOWED_METHODS } });
  }
  // The cheap checks come first: a switched-off or unconfigured endpoint never reads the body.
  const enabled = await telemetryEnabled().catch(() => null);
  if (enabled === null) return reply(503, ErrorCode.Unavailable);
  if (!enabled) return reply(204);
  if (!env.USAGE) return reply(503, ErrorCode.Unavailable);
  const body = await readBody(request);
  if (body === "too-large") return reply(413, ErrorCode.TooLarge);
  const events = isRecord(body) ? body["events"] : undefined;
  if (body === "invalid" || !Array.isArray(events)) return reply(400, ErrorCode.BadRequest);
  if (events.length > MAX_EVENTS_PER_REQUEST) return reply(413, ErrorCode.TooMany);
  for (const event of validEvents(events)) env.USAGE.writeDataPoint(toDataPoint(event));
  return reply(204);
}
