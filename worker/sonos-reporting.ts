// Slice 436 A2: Sonos playback reports. A player POSTs `/timePlayed` to the integration's Reporting Endpoint (Sonos Dev Portal) with the
// played time of a track; one `final` report of 30 s or more is one counted play of one book (the apps' own bar, "played for 30 s or more").
// getMediaURI is not a play count (sonos.ts). Nothing about the home is read or stored: no household id, IP or account, only book, language, seconds.
// Payload fields follow https://docs.sonos.com/docs/add-reporting and https://docs.sonos.com/reference/timeplayed: an `items` array whose entries carry
// the SMAPI `objectId`, a report `type` (`final` or `update`) and `durationPlayedMillis`. The parser also accepts `id` and `durationMillis`/`duration`
// (older examples name them so); see the slice's *Not yet verified* for what could not be read from the live pages.

import { loadTracks, findTrack } from "./sonos.ts";
import type { Track } from "./sonos.ts";
import type { CatalogueRuntime, ObjectServer } from "./stories.ts";
import { DETAIL_REPORTED, SPEAKER_PLATFORM_SONOS, recordSpeakerFault, recordSpeakerPlay } from "./speaker-usage.ts";
import type { SpeakerUsage } from "./speaker-usage.ts";

export const SONOS_REPORTING_PATH = "/sonos/timePlayed";
export const MIN_PLAY_SECONDS = 30;
const MAX_BODY_BYTES = 64 * 1024;
const MAX_ITEMS = 50;
const REPORT_FINAL = "final";
const ALLOWED_METHODS = "POST";
const STATUS_OK = 200;
const STATUS_BAD = 400;
const STATUS_NOT_ALLOWED = 405;
const MILLIS = 1000;

/** One final report: the Sonos item id and the seconds played. */
export interface FinalReport {
  readonly id: string;
  readonly seconds: number;
}

type Parsed = { readonly ok: true; readonly reports: readonly FinalReport[] } | { readonly ok: false };

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function reportItems(body: unknown): unknown[] | null {
  if (Array.isArray(body)) return body;
  if (!isRecord(body)) return null;
  const list = body["items"] ?? body["reports"];
  return Array.isArray(list) ? list : null;
}

function itemId(item: Record<string, unknown>): string | null {
  const id = item["objectId"] ?? item["id"];
  return typeof id === "string" && id !== "" ? id : null;
}

function playedSeconds(item: Record<string, unknown>): number | null {
  const millis = item["durationPlayedMillis"] ?? item["durationMillis"] ?? item["duration"];
  return typeof millis === "number" && Number.isFinite(millis) && millis >= 0 ? millis / MILLIS : null;
}

/** The `final` reports of a payload; `update` reports are skipped. A payload that is not an item list, or a final report with no id or time, is not ok. */
export function parseReports(body: unknown): Parsed {
  const items = reportItems(body);
  if (items === null || items.length > MAX_ITEMS) return { ok: false };
  const reports: FinalReport[] = [];
  for (const item of items) {
    if (!isRecord(item)) return { ok: false };
    const type = item["type"] ?? item["reportType"];
    if (type !== REPORT_FINAL) continue;
    const id = itemId(item);
    const seconds = playedSeconds(item);
    if (id === null || seconds === null) return { ok: false };
    reports.push({ id, seconds });
  }
  return { ok: true, reports };
}

/** The whole seconds a report counts: the played time capped at the book's length, or null under the 30 s bar. */
export function countedSeconds(report: FinalReport, track: Track): number | null {
  return report.seconds >= MIN_PLAY_SECONDS ? Math.round(Math.min(report.seconds, track.seconds)) : null;
}

export function isSonosReportingPath(pathname: string): boolean {
  return pathname === SONOS_REPORTING_PATH;
}

async function readJson(request: Request): Promise<unknown> {
  const text = await request.text();
  if (text.length > MAX_BODY_BYTES) return null;
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

async function countAll(counted: readonly { readonly track: Track; readonly seconds: number }[], usage: SpeakerUsage | undefined, request: Request): Promise<void> {
  for (const { track, seconds } of counted) {
    await recordSpeakerPlay(usage, { platform: SPEAKER_PLATFORM_SONOS, book: track.storyId, language: track.language, seconds, detail: DETAIL_REPORTED }, request).catch(() => false);
  }
}

/** `POST /sonos/timePlayed`: 200 with an empty body once the final reports are read (counted or too short), 400 for a bad payload or an unknown item (a fault row), 405 for any other method. */
export async function handleSonosReporting(request: Request, env: { DOWNLOADS?: R2Bucket }, pathPrefix: string, serve: ObjectServer, usage?: SpeakerUsage, catalogue?: CatalogueRuntime): Promise<Response> {
  if (request.method !== "POST") return new Response(null, { status: STATUS_NOT_ALLOWED, headers: { Allow: ALLOWED_METHODS } });
  const parsed = parseReports(await readJson(request));
  if (!parsed.ok) return bad();
  if (parsed.reports.length === 0) return new Response(null, { status: STATUS_OK });
  const tracks = await loadTracks(request, env, pathPrefix, serve, catalogue).catch(() => null);
  if (tracks === null) return new Response(null, { status: 503 });
  const counted: { track: Track; seconds: number }[] = [];
  for (const report of parsed.reports) {
    const track = findTrack(tracks, report.id);
    if (track === null) {
      await defer(usage, recordSpeakerFault(usage, SPEAKER_PLATFORM_SONOS, request).catch(() => false));
      return bad();
    }
    const seconds = countedSeconds(report, track);
    if (seconds !== null) counted.push({ track, seconds });
  }
  await defer(usage, countAll(counted, usage, request));
  return new Response(null, { status: STATUS_OK });
}
