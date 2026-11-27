// Slice 436 A2: Sonos playback reports. A player POSTs `/timePlayed` to the integration's Reporting Endpoint (Sonos Dev Portal) with the
// played time of a track; one `final` report of 30 s or more is one counted play of one book (the apps' own bar, "played for 30 s or more").
// getMediaURI is not a play count (sonos.ts). Nothing about the home is read or stored: no household id, IP or account, only book, language, seconds.
// Payload shape, read from https://docs.sonos.com/docs/add-reporting (final report examples: SMAPI items carry `objectId`) and
// https://docs.sonos.com/reference/timeplayed (OpenAPI: body `items[]` with `reportId`, `id`, `mediaUrl`, `type`, `positionMillis`,
// `durationPlayedMillis`, ...): an `items` array; each entry names its item (`objectId`, or `id` on a cloud queue), a `type` (`final` or
// `update`) and `durationPlayedMillis`, the only played time counted (`positionMillis` and any duration are the playhead or the track's
// length, never time listened). A final report without them is skipped, never counted. The endpoint is anonymous (Sonos sends an access
// token that a service with no accounts cannot check), so it only counts, caps the rows one request can write, and writes no fault row.

import { loadTracks, findTrack } from "./sonos.ts";
import type { Track } from "./sonos.ts";
import { readBoundedText } from "./sonos-soap.ts";
import type { CatalogueRuntime, ObjectServer } from "./stories.ts";
import { DETAIL_REPORTED, SPEAKER_PLATFORM_SONOS, recordSpeakerPlay } from "./speaker-usage.ts";
import type { SpeakerUsage } from "./speaker-usage.ts";

/** Sonos requires the version number in the Reporting Endpoint URL; the owner sets `<host>/sonos/v2.0` and players POST `/timePlayed` under it. */
export const SONOS_REPORTING_PATH = "/sonos/v2.0/timePlayed";
/** Slice 467: the owner's Reporting Endpoint is `<host>/sonos/v2` (no ".0"), so both bases serve `/timePlayed` and answer the portal's reachability GET. */
export const SONOS_REPORTING_BASES: readonly string[] = ["/sonos/v2", "/sonos/v2.0"];
const TIME_PLAYED_SUFFIX = "/timePlayed";
const BASE_METHODS = "GET, HEAD";
export const MIN_PLAY_SECONDS = 30;
/** The most rows one request can write: a player reports one track at a time, so more final reports in a request are left uncounted. */
export const MAX_COUNTED_PER_REQUEST = 5;
const MAX_ITEMS = 50;
const REPORT_FINAL = "final";
const ALLOWED_METHODS = "POST";
const STATUS_OK = 200;
const STATUS_BAD = 400;
const STATUS_NOT_ALLOWED = 405;
const STATUS_UNAVAILABLE = 503;
const MILLIS = 1000;
/** Slice 436 (review): a counted `reportId` is remembered this long, so a Sonos retry of the same final report counts once. */
export const REPORT_MEMORY_SECONDS = 2 * 24 * 60 * 60;
const REPORT_MEMORY_PATH = "/__sonos-report-id/";

/** One final report: the Sonos item id, the seconds played and, when Sonos sent one, its `reportId`. */
export interface FinalReport {
  readonly id: string;
  readonly seconds: number;
  readonly reportId?: string;
}

/** The counted report ids: `has` before counting, `add` after. */
export interface ReportMemory {
  has(reportId: string): Promise<boolean>;
  add(reportId: string): Promise<void>;
}

function memoryKey(request: Request, reportId: string): Request {
  return new Request(new URL(`${REPORT_MEMORY_PATH}${encodeURIComponent(reportId)}`, request.url).toString());
}

/** The Cache API as the memory (no binding, no secret): per data centre, so a retry that lands elsewhere can count again; absent outside the Workers runtime. */
export function cacheReportMemory(request: Request): ReportMemory | null {
  const store = (globalThis as { caches?: { default?: Cache } }).caches?.default;
  if (store === undefined) return null;
  return {
    has: async (reportId) => (await store.match(memoryKey(request, reportId))) !== undefined,
    add: async (reportId) => store.put(memoryKey(request, reportId), new Response(null, { headers: { "Cache-Control": `max-age=${REPORT_MEMORY_SECONDS}` } })),
  };
}

type Parsed = { readonly ok: true; readonly reports: readonly FinalReport[] } | { readonly ok: false };

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function reportItems(body: unknown): unknown[] | null {
  if (!isRecord(body)) return null;
  const list = body["items"];
  return Array.isArray(list) ? list : null;
}

function itemId(item: Record<string, unknown>): string | null {
  const id = item["objectId"] ?? item["id"];
  return typeof id === "string" && id !== "" ? id : null;
}

function playedSeconds(item: Record<string, unknown>): number | null {
  const millis = item["durationPlayedMillis"];
  return typeof millis === "number" && Number.isFinite(millis) && millis >= 0 ? millis / MILLIS : null;
}

function finalReport(item: unknown): FinalReport | null {
  if (!isRecord(item) || item["type"] !== REPORT_FINAL) return null;
  const id = itemId(item);
  const seconds = playedSeconds(item);
  if (id === null || seconds === null) return null;
  const reportId = item["reportId"];
  return typeof reportId === "string" && reportId !== "" ? { id, seconds, reportId } : { id, seconds };
}

/** The `final` reports of a payload; `update` reports and a final one with no item id or no `durationPlayedMillis` are skipped. A payload that is not an `items` list (or has too many) is not ok. */
export function parseReports(body: unknown): Parsed {
  const items = reportItems(body);
  if (items === null || items.length > MAX_ITEMS) return { ok: false };
  const reports: FinalReport[] = [];
  for (const item of items) {
    const report = finalReport(item);
    if (report !== null) reports.push(report);
  }
  return { ok: true, reports };
}

/** The whole seconds a report counts: the played time capped at the book's length, or null under the 30 s bar. */
export function countedSeconds(report: FinalReport, track: Track): number | null {
  return report.seconds >= MIN_PLAY_SECONDS ? Math.round(Math.min(report.seconds, track.seconds)) : null;
}

/** True for `<base>/timePlayed` (the reports) on either base. */
export function isTimePlayedPath(pathname: string): boolean {
  return SONOS_REPORTING_BASES.some((base) => pathname === `${base}${TIME_PLAYED_SUFFIX}`);
}

/** True for a base itself, with or without a trailing slash (the portal's reachability check). */
export function isSonosReportingBase(pathname: string): boolean {
  return SONOS_REPORTING_BASES.some((base) => pathname === base || pathname === `${base}/`);
}

export function isSonosReportingPath(pathname: string): boolean {
  return isTimePlayedPath(pathname) || isSonosReportingBase(pathname);
}

/** `GET`/`HEAD` on a base: 200 with an empty body; any other method 405. */
function answerBase(request: Request): Response {
  if (request.method === "GET" || request.method === "HEAD") return new Response(null, { status: STATUS_OK });
  return new Response(null, { status: STATUS_NOT_ALLOWED, headers: { Allow: BASE_METHODS } });
}

async function readJson(request: Request): Promise<unknown> {
  const text = await readBoundedText(request);
  if (text === null) return null;
  try {
    return JSON.parse(text);
  } catch {
    // no-fallback: an unreadable body is the sender's 400, nothing to count
    return null;
  }
}

function bad(): Response {
  return new Response(null, { status: STATUS_BAD });
}

function defer(usage: SpeakerUsage | undefined, work: Promise<unknown>): Promise<unknown> {
  if (usage?.defer === undefined) return work;
  usage.defer(work);
  return Promise.resolve();
}

interface CountedReport {
  readonly track: Track;
  readonly seconds: number;
  readonly reportId: string | undefined;
}

/** Writes each row; a reportId is remembered only after its row was written, so a failed write is counted when Sonos retries. */
async function countAll(counted: readonly CountedReport[], memory: ReportMemory | null, usage: SpeakerUsage | undefined, request: Request): Promise<void> {
  for (const { track, seconds, reportId } of counted) {
    const written = await recordSpeakerPlay(usage, { platform: SPEAKER_PLATFORM_SONOS, book: track.storyId, language: track.language, seconds, detail: DETAIL_REPORTED }, request).catch(() => false);
    if (written && reportId !== undefined) await memory?.add(reportId).catch(() => undefined);
  }
}

/** `POST /sonos/v2.0/timePlayed` and `POST /sonos/v2/timePlayed`: 200 with an empty body once the reports are read (counted, too short, unknown or malformed ones skipped; at most `MAX_COUNTED_PER_REQUEST` rows), 400 for a body that is not an `items` list, 405 for any other method. `GET`/`HEAD` on the two bases (`/sonos/v2`, `/sonos/v2.0`) answer 200 with an empty body (the portal's reachability check; Slice 467), so the base check must stay. No fault row: the endpoint is anonymous. */
export async function handleSonosReporting(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer, usage?: SpeakerUsage, catalogue?: CatalogueRuntime): Promise<Response> {
  if (isSonosReportingBase(new URL(request.url).pathname)) return answerBase(request);
  if (request.method !== "POST") return new Response(null, { status: STATUS_NOT_ALLOWED, headers: { Allow: ALLOWED_METHODS } });
  const parsed = parseReports(await readJson(request));
  if (!parsed.ok) return bad();
  if (parsed.reports.length === 0) return new Response(null, { status: STATUS_OK });
  const tracks = await loadTracks(request, env, pathPrefix, serve, catalogue).catch(() => null);
  if (tracks === null) return new Response(null, { status: STATUS_UNAVAILABLE });
  const counted: CountedReport[] = [];
  const ids = new Set<string>();
  const memory = cacheReportMemory(request);
  for (const report of parsed.reports) {
    if (counted.length >= MAX_COUNTED_PER_REQUEST) break;
    const track = findTrack(tracks, report.id);
    const seconds = track === null ? null : countedSeconds(report, track);
    if (track === null || seconds === null || (await alreadyCounted(report.reportId, ids, memory))) continue;
    counted.push({ track, seconds, reportId: report.reportId });
  }
  await defer(usage, countAll(counted, memory, usage, request));
  return new Response(null, { status: STATUS_OK });
}

/** True for a `reportId` already counted in this request or remembered from an earlier one; a report without an id is never a repeat. */
async function alreadyCounted(reportId: string | undefined, inRequest: Set<string>, memory: ReportMemory | null): Promise<boolean> {
  if (reportId === undefined) return false;
  if (inRequest.has(reportId)) return true;
  inRequest.add(reportId);
  return memory !== null && (await memory.has(reportId).catch(() => false));
}

