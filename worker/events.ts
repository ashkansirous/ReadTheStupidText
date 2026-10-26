// Dino Reader's anonymous usage counts (Slice 133, Decisions 140-142). The app batches a session's counts into one
//   POST /api/events    { "schemaVersion": 1, "events": [ { name, book, language, source, hasRecording, version, platform, day, sampleRate, detail? } ] }
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
const NO_BOOK = "none";
const PLATFORMS = ["android", "ios", "windows"] as const;
const BOOK_SOURCES = [SHELF_SOURCE, OWN_BOOK] as const;
const NO_DETAIL = "";
const STORE_FILTERS = ["none", "age", "author", "age+author"] as const;
const OWN_BOOK_KINDS = ["photo", "pdf", "scan", "typed"] as const;
const PAGE_BUCKETS = ["1", "2-5", "6-10", "11+"] as const;
// The app's supported reading languages (UiLanguage.Supported in DinoReader.Core); UsageEventTests fails when this list and the app's drift apart.
const READING_LANGUAGES = ["en", "fa", "fr", "es", "pt", "zh", "ar", "ru", "ja", "da", "fi", "sv", "nb", "nl"] as const;
const VOICE_CHANGES = ["character_voice", "flavour", "narrator"] as const;

/** What an event name allows: the sources it may come from and the one extra value (`detail`) it carries. */
interface EventRule {
  readonly sources: readonly string[];
  readonly detail: (value: unknown) => boolean;
}

const emptyDetail = (value: unknown): boolean => value === undefined || value === NO_DETAIL;
const detailIn = (list: readonly string[]) => (value: unknown): boolean => oneOf(list, value);

// Slice 133 events carry no detail; Slice 134 added the rest. Anything not named here is dropped.
const EVENT_RULES: Readonly<Record<string, EventRule>> = {
  book_opened: { sources: BOOK_SOURCES, detail: emptyDetail },
  book_finished: { sources: BOOK_SOURCES, detail: emptyDetail },
  store_download: { sources: [SHELF_SOURCE], detail: detailIn(STORE_FILTERS) },
  own_book_added: { sources: [OWN_BOOK], detail: detailIn(OWN_BOOK_KINDS) },
  recording_made: { sources: BOOK_SOURCES, detail: detailIn(PAGE_BUCKETS) },
  voice_changed: { sources: [NO_BOOK], detail: detailIn(VOICE_CHANGES) },
  reading_language_changed: { sources: [NO_BOOK], detail: detailIn(READING_LANGUAGES) },
  app_opened: { sources: [NO_BOOK], detail: emptyDetail },
};
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
  /** The event's one extra closed value (see EVENT_RULES); empty for the reader events. */
  readonly detail: string;
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

// A shelf book is named by its package id; an own book never carries one, and a count about no book names none, so the source and the book must agree.
function bookMatchesSource(book: unknown, source: unknown): book is string {
  if (source === OWN_BOOK) return book === OWN_BOOK;
  if (source === NO_BOOK) return book === NO_BOOK;
  return matches(STORY_ID_PATTERN, book);
}

function ruleFor(name: unknown): EventRule | null {
  return typeof name === "string" && Object.hasOwn(EVENT_RULES, name) ? EVENT_RULES[name]! : null;
}

/** The checked count in `raw`, or null when any field is missing, of the wrong type or outside its list or pattern. */
export function parseEvent(raw: unknown): UsageEvent | null {
  if (!isRecord(raw)) return null;
  const { name, book, language, source, hasRecording, version, platform, day, sampleRate } = raw;
  const rule = ruleFor(name);
  if (rule === null || !rule.detail(raw["detail"])) return null;
  const detail = typeof raw["detail"] === "string" ? raw["detail"] : NO_DETAIL;
  const valid =
    typeof name === "string" &&
    oneOf(rule.sources, source) &&
    bookMatchesSource(book, source) &&
    matches(LANGUAGE_PATTERN, language) &&
    typeof hasRecording === "boolean" &&
    matches(VERSION_PATTERN, version) &&
    oneOf(PLATFORMS, platform) &&
    isRealDay(day) &&
    typeof sampleRate === "number" &&
    sampleRate >= MIN_SAMPLE_RATE &&
    sampleRate <= 1;
  return valid ? { name, book, language, source, hasRecording, version, platform, day, sampleRate, detail } : null;
}

/** The Analytics Engine data point for a count: the name is the sampling index, every field a blob, and the double is the count's weight (1 / the sample rate the app counted at), so SUM(double) estimates the true total. */
export function toDataPoint(event: UsageEvent): AnalyticsEngineDataPoint {
  return {
    indexes: [event.name],
    blobs: [event.name, event.book, event.language, event.source, event.hasRecording ? "yes" : "no", event.version, event.platform, event.day, event.detail],
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

/** Turns one raw count into a data point; null when the count is invalid. */
type PointMaker = (raw: unknown) => AnalyticsEngineDataPoint | null;

// The shared request handling (method, telemetry switch, binding, size caps, batch limit); the two products differ only in the dataset and in what a valid count is.
async function handleBatch(request: Request, dataset: AnalyticsEngineDataset | undefined, telemetryEnabled: TelemetryCheck, makePoint: PointMaker): Promise<Response> {
  if (request.method !== "POST") {
    return new Response(null, { status: 405, headers: { Allow: ALLOWED_METHODS } });
  }
  // The cheap checks come first: a switched-off or unconfigured endpoint never reads the body.
  const enabled = await telemetryEnabled().catch(() => null);
  if (enabled === null) return reply(503, ErrorCode.Unavailable);
  if (!enabled) return reply(204);
  if (!dataset) return reply(503, ErrorCode.Unavailable);
  const body = await readBody(request);
  if (body === "too-large") return reply(413, ErrorCode.TooLarge);
  const events = isRecord(body) ? body["events"] : undefined;
  if (body === "invalid" || !Array.isArray(events)) return reply(400, ErrorCode.BadRequest);
  if (events.length > MAX_EVENTS_PER_REQUEST) return reply(413, ErrorCode.TooMany);
  for (const raw of events) {
    const point = makePoint(raw);
    if (point !== null) dataset.writeDataPoint(point);
  }
  return reply(204);
}

/** Answers POST /api/events: 204 when taken or deliberately dropped, 400/413 for a body that is not a batch of counts. */
export function handleEvents(request: Request, env: EventsEnv, telemetryEnabled: TelemetryCheck): Promise<Response> {
  return handleBatch(request, env.USAGE, telemetryEnabled, (raw) => {
    const event = parseEvent(raw);
    return event === null ? null : toDataPoint(event);
  });
}

// ---- Read The Stupid Text's phone counts (Slice 137, Decisions 143-145) -------------------------------------------------
// The same pipe as Dino's, its own dataset (`rtst_usage`, binding RTST_USAGE) and its own closed event set. A count is a name
// plus closed-list fields; no free text, no id, nothing finer than the day. Each event names the fields it requires and the
// ones it may carry; any other field, or a value outside its list or pattern, drops the whole count.

/** The Analytics Engine binding for the phone's counts; not in the wrangler configs until Analytics Engine is enabled on the account. */
export interface RtstEventsEnv {
  readonly RTST_USAGE?: AnalyticsEngineDataset;
}

const YES = "yes";
const NO = "no";
const RTST_MODES = ["original", "translation"] as const;
const RTST_ENGINES = ["phone", "supertonic", "piper", "melo", "kokoro"] as const;
const RTST_LENGTH_BUCKETS = ["<200", "200-1000", "1000-5000", ">5000"] as const;
const RTST_STAGES = ["model_download", "translate", "first_audio"] as const;
const RTST_OUTCOMES = ["done", "cancelled", "failed"] as const;
const RTST_DURATION_BUCKETS = ["<0.5", "0.5-1", "1-2", "2-5", "5-10", "10-20", ">20"] as const;
const MIN_OS_MAJOR = 1;
const MAX_OS_MAJOR = 99;

/** The blob order of a phone count after its envelope; a field a count does not carry is an empty blob. */
export const RTST_FIELDS = ["language", "sourceLanguage", "mode", "engine", "voiceDefault", "lengthBucket", "osMajor", "stage", "outcome", "durationBucket"] as const;
type RtstField = (typeof RTST_FIELDS)[number];
type FieldValue = string | number | boolean;

const isOsMajor = (value: unknown): boolean => Number.isInteger(value) && (value as number) >= MIN_OS_MAJOR && (value as number) <= MAX_OS_MAJOR;

const RTST_VALIDATORS: Readonly<Record<RtstField, (value: unknown) => boolean>> = {
  language: (value) => matches(LANGUAGE_PATTERN, value),
  sourceLanguage: (value) => matches(LANGUAGE_PATTERN, value),
  mode: (value) => oneOf(RTST_MODES, value),
  engine: (value) => oneOf(RTST_ENGINES, value),
  voiceDefault: (value) => typeof value === "boolean",
  lengthBucket: (value) => oneOf(RTST_LENGTH_BUCKETS, value),
  osMajor: isOsMajor,
  stage: (value) => oneOf(RTST_STAGES, value),
  outcome: (value) => oneOf(RTST_OUTCOMES, value),
  durationBucket: (value) => oneOf(RTST_DURATION_BUCKETS, value),
};

interface RtstRule {
  readonly required: readonly RtstField[];
  readonly optional: readonly RtstField[];
}

const RTST_EVENT_RULES: Readonly<Record<string, RtstRule>> = {
  app_opened: { required: ["language", "osMajor"], optional: [] },
  read_started: { required: ["language", "mode", "engine", "voiceDefault", "lengthBucket"], optional: ["sourceLanguage"] },
  translate_offered: { required: ["sourceLanguage", "language"], optional: [] },
  translate_chosen: { required: ["sourceLanguage", "language"], optional: [] },
  voice_changed: { required: ["language", "engine"], optional: [] },
  timing: { required: ["stage", "outcome", "durationBucket"], optional: ["sourceLanguage", "language"] },
};

/** A phone count that passed every check: the envelope plus the closed fields its event allows. */
export interface RtstEvent {
  readonly name: string;
  readonly version: string;
  readonly platform: string;
  readonly day: string;
  readonly sampleRate: number;
  readonly fields: Readonly<Partial<Record<RtstField, FieldValue>>>;
}

function rtstRuleFor(name: unknown): RtstRule | null {
  return typeof name === "string" && Object.hasOwn(RTST_EVENT_RULES, name) ? RTST_EVENT_RULES[name]! : null;
}

function hasValidEnvelope(raw: Record<string, unknown>): boolean {
  const { version, platform, day, sampleRate } = raw;
  return (
    matches(VERSION_PATTERN, version) &&
    oneOf(PLATFORMS, platform) &&
    isRealDay(day) &&
    typeof sampleRate === "number" &&
    sampleRate >= MIN_SAMPLE_RATE &&
    sampleRate <= 1
  );
}

// The event's fields, or null when one is missing, invalid or not allowed for this event.
function rtstFieldsOf(raw: Record<string, unknown>, rule: RtstRule): RtstEvent["fields"] | null {
  const allowed: readonly RtstField[] = [...rule.required, ...rule.optional];
  const present = RTST_FIELDS.filter((field) => raw[field] !== undefined);
  if (present.some((field) => !allowed.includes(field))) return null;
  if (rule.required.some((field) => raw[field] === undefined)) return null;
  if (present.some((field) => !RTST_VALIDATORS[field](raw[field]))) return null;
  return Object.fromEntries(present.map((field) => [field, raw[field] as FieldValue]));
}

/** The checked phone count in `raw`, or null when anything about it is outside the closed lists. */
export function parseRtstEvent(raw: unknown): RtstEvent | null {
  if (!isRecord(raw)) return null;
  const rule = rtstRuleFor(raw["name"]);
  if (rule === null || !hasValidEnvelope(raw)) return null;
  const fields = rtstFieldsOf(raw, rule);
  if (fields === null) return null;
  return { name: raw["name"] as string, version: raw["version"] as string, platform: raw["platform"] as string, day: raw["day"] as string, sampleRate: raw["sampleRate"] as number, fields };
}

function rtstBlob(value: FieldValue | undefined): string {
  if (value === undefined) return "";
  if (typeof value === "boolean") return value ? YES : NO;
  return String(value);
}

/** The data point of a phone count: the name is the sampling index, the envelope and fields are blobs, the double is the count's weight (1 / sampleRate). */
export function toRtstDataPoint(event: RtstEvent): AnalyticsEngineDataPoint {
  return {
    indexes: [event.name],
    blobs: [event.name, event.version, event.platform, event.day, ...RTST_FIELDS.map((field) => rtstBlob(event.fields[field]))],
    doubles: [1 / event.sampleRate],
  };
}

/** Answers POST /api/events on Read The Stupid Text's host: the phone's closed event set into the RTST_USAGE dataset. */
export function handleRtstEvents(request: Request, env: RtstEventsEnv, telemetryEnabled: TelemetryCheck): Promise<Response> {
  return handleBatch(request, env.RTST_USAGE, telemetryEnabled, (raw) => {
    const event = parseRtstEvent(raw);
    return event === null ? null : toRtstDataPoint(event);
  });
}
