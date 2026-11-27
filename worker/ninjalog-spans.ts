// Slice 421: the counts the Ninjalog tee already forwards are also sent as OpenTelemetry spans, one trace per app session, so a journey, the
// event-to-event flow and a funnel are queries (`spantree`). No app change: every count carries `session` (the trace) and `seq` (1, 2, 3 ...; the
// span, its parent being seq - 1). OTLP/HTTP JSON per the OpenTelemetry proto spec (context7 /open-telemetry/opentelemetry-proto): ids are hex
// strings, 64-bit times are decimal strings, enums are integers. Same switch, token, timeout and never-throw rule as `ninjalog.ts`.
import { ninjalogDocument, NINJALOG_TIMEOUT_MS, type NinjalogFetch, type SentCount } from "./ninjalog.ts";

export const NINJALOG_TRACES_URL = "https://app.ninjalog.dev/v1/traces";
/** The Ninjalog index the spans go to (a resource attribute `#index`). */
export const NINJALOG_SPANS_INDEX = "dino_spans";
export const SPAN_SERVICE_NAME = "dino";
const JSON_TYPE = "application/json";
const SPAN_KIND_INTERNAL = 1;
const SESSION_PATTERN = /^[0-9a-f]{32}$/;
const SESSION_ZERO = "0".repeat(32);
const SPAN_ID_LENGTH = 16;
const NO_NUMBER = -1;
const EVENT_SCREEN = "screen";
const EVENT_TAP = "tap";
const DETAIL_SEPARATOR = ":";
/** The `UiScreenActions.Shown` value (src/Dino/DinoReader.Core/UiUsage.cs): the plain screen span; any other action is named. */
const SCREEN_ACTION_SHOWN = "shown";
const KEY_INDEX = "#index";
const KEY_SERVICE_NAME = "service.name";
const MS_PER_SECOND = 1000;
const NS_PER_MS = 1_000_000n;
/** Fields of the log document that are not span attributes: the index lives on the resource, the time on the span. */
const DOCUMENT_ONLY_KEYS: readonly string[] = [KEY_INDEX, "timestamp"];
/** Numeric attributes that are always `doubleValue`, so one key never mixes OTLP types (rate 1 and rate 0.5 alike). */
const DOUBLE_KEYS: readonly string[] = ["sampleRate", "sampleInterval"];

/** One OTLP attribute value in its JSON shape (`intValue` is a decimal string, as for any 64-bit integer). */
type OtlpValue = { stringValue: string } | { intValue: string } | { doubleValue: number } | { boolValue: boolean };
interface OtlpAttribute { readonly key: string; readonly value: OtlpValue }
export interface OtlpSpan {
  readonly traceId: string;
  readonly spanId: string;
  readonly parentSpanId?: string;
  readonly name: string;
  readonly kind: number;
  readonly startTimeUnixNano: string;
  readonly endTimeUnixNano: string;
  readonly attributes: readonly OtlpAttribute[];
}
export interface OtlpTraces {
  readonly resourceSpans: readonly { readonly resource: { readonly attributes: readonly OtlpAttribute[] }; readonly scopeSpans: readonly { readonly spans: readonly OtlpSpan[] }[] }[];
}

/** The seq as a span id (16 hex characters, zero-padded), or null for a seq that is not a whole number >= 1. */
export function spanIdOf(seq: number): string | null {
  if (!Number.isSafeInteger(seq) || seq < 1) return null;
  return seq.toString(16).padStart(SPAN_ID_LENGTH, "0");
}

function validSession(session: string): boolean {
  return SESSION_PATTERN.test(session) && session !== SESSION_ZERO;
}

/** The span name: a `screen` count is its screen (`<screen> <action>` unless the action is `shown`), a tap is `tap <detail>` (just `tap` with no detail), else the event name. */
export function spanName(sent: SentCount): string {
  const { name, detail, screen } = sent.event;
  if (name === EVENT_TAP) return detail === "" ? EVENT_TAP : `${EVENT_TAP} ${detail}`;
  if (name !== EVENT_SCREEN) return name;
  const [first = "", action = ""] = detail.split(DETAIL_SEPARATOR);
  const shown = first === "" ? screen : first;
  return action === "" || action === SCREEN_ACTION_SHOWN ? shown : `${shown} ${action}`;
}

function attributeOf(key: string, value: unknown): OtlpAttribute | null {
  if (typeof value === "string") return { key, value: { stringValue: value } };
  if (typeof value === "boolean") return { key, value: { boolValue: value } };
  if (typeof value !== "number") return null;
  if (DOUBLE_KEYS.includes(key) || !Number.isInteger(value)) return { key, value: { doubleValue: value } };
  return { key, value: { intValue: String(value) } };
}

/** The log document's named fields as flat span attributes (no `timestamp`, no `#index`). */
export function spanAttributes(sent: SentCount, receivedAt: Date): OtlpAttribute[] {
  const document = ninjalogDocument(sent, receivedAt);
  const attributes: OtlpAttribute[] = [];
  for (const [key, value] of Object.entries(document)) {
    const attribute = DOCUMENT_ONLY_KEYS.includes(key) ? null : attributeOf(key, value);
    if (attribute !== null) attributes.push(attribute);
  }
  return attributes;
}

/** Start in whole ms: the receive time minus the seconds this count was made before the session's latest count of the batch;
 * a count with no sinceLaunch takes the previous span's start (the receive time when it is the first). */
function startMs(sent: SentCount, latestSince: number, receivedAt: Date, previousStart: number | null): number {
  const since = sent.event.sinceLaunch;
  if (since === NO_NUMBER || latestSince === NO_NUMBER) return previousStart ?? receivedAt.getTime();
  return receivedAt.getTime() - Math.max(0, latestSince - since) * MS_PER_SECOND;
}

function startsOf(counts: readonly SentCount[], latestSince: number, receivedAt: Date): number[] {
  const starts: number[] = [];
  for (const count of counts) starts.push(startMs(count, latestSince, receivedAt, starts[starts.length - 1] ?? null));
  return starts;
}

function nanos(ms: number): string {
  return (BigInt(ms) * NS_PER_MS).toString();
}

/** The counts that can be spans (a usable session and seq), grouped by session and ordered by seq. */
function sessionsOf(sent: readonly SentCount[]): Map<string, SentCount[]> {
  const sessions = new Map<string, SentCount[]>();
  for (const count of sent) {
    if (!validSession(count.event.session) || spanIdOf(count.event.seq) === null) continue;
    const list = sessions.get(count.event.session) ?? [];
    list.push(count);
    sessions.set(count.event.session, list);
  }
  for (const list of sessions.values()) list.sort((a, b) => a.event.seq - b.event.seq);
  return sessions;
}

function spansOfSession(session: string, counts: readonly SentCount[], receivedAt: Date): OtlpSpan[] {
  const latestSince = Math.max(...counts.map((count) => count.event.sinceLaunch));
  const starts = startsOf(counts, latestSince, receivedAt);
  return counts.map((count, index) => {
    const parent = spanIdOf(count.event.seq - 1);
    const start = starts[index] ?? receivedAt.getTime();
    return {
      traceId: session,
      spanId: spanIdOf(count.event.seq) as string,
      ...(parent === null ? {} : { parentSpanId: parent }),
      name: spanName(count),
      kind: SPAN_KIND_INTERNAL,
      startTimeUnixNano: nanos(start),
      endTimeUnixNano: nanos(Math.max(start, starts[index + 1] ?? start)),
      attributes: spanAttributes(count, receivedAt),
    };
  });
}

/** One OTLP traces request for a batch, or null when no count can be a span. */
export function ninjalogTraces(sent: readonly SentCount[], receivedAt: Date): OtlpTraces | null {
  if (Number.isNaN(receivedAt.getTime())) return null;
  const spans = [...sessionsOf(sent)].flatMap(([session, counts]) => spansOfSession(session, counts, receivedAt));
  if (spans.length === 0) return null;
  const resource = { attributes: [{ key: KEY_SERVICE_NAME, value: { stringValue: SPAN_SERVICE_NAME } }, { key: KEY_INDEX, value: { stringValue: NINJALOG_SPANS_INDEX } }] };
  return { resourceSpans: [{ resource, scopeSpans: [{ spans }] }] };
}

/** Posts one traces request. Resolves whether Ninjalog took it; never rejects. */
export async function postSpansToNinjalog(token: string, traces: OtlpTraces | null, send: NinjalogFetch = fetch): Promise<boolean> {
  if (traces === null) return false;
  try {
    const response = await send(NINJALOG_TRACES_URL, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}`, "Content-Type": JSON_TYPE },
      body: JSON.stringify(traces),
      signal: AbortSignal.timeout(NINJALOG_TIMEOUT_MS),
    });
    if (!response.ok) console.warn(`ninjalog: traces answered ${response.status}`);
    await response.body?.cancel();
    return response.ok;
  } catch {
    // no-fallback: a Ninjalog failure never reaches the request path; the Analytics Engine row is the record
    console.warn("ninjalog: traces failed");
    return false;
  }
}
