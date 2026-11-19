// Slice 419: every Dino count written to `dino_usage` is also posted to Ninjalog (an EU-hosted log search service) as one named JSON
// document, so a funnel or a session timeline is a query instead of a pile of `blobN` columns. The Worker forwards; the app makes no
// new request. OFF unless the `NINJALOG_ENABLED` var is "true" AND the `NINJALOG_TOKEN` Worker secret is set (the terms check of the slice's
// item 1 found no DPA yet; the owner chose on 2026-11-19 to forward anyway, so both wrangler configs set the var to "true"). Fire-and-forget after the answer is sent: a Ninjalog failure never changes the app's 204
// or the Analytics Engine write. Never carries the IP, the raw body or a row the usage rules dropped (the callers hand over only checked counts).
import type { WrittenCount, WrittenTee } from "./events.ts";

export const NINJALOG_INGEST_URL = "https://app.ninjalog.dev/v1/ingest/json";
/** The Ninjalog index the production counts go to (`dino_usage_beta` is never forwarded). */
export const NINJALOG_INDEX = "dino_usage";
export const NINJALOG_ENABLED_VALUE = "true";
/** Ninjalog gets a short wait: the post runs after the answer, and an unanswered one is dropped rather than held. */
export const NINJALOG_TIMEOUT_MS = 3000;
const NO_NUMBER = -1;
const NDJSON_TYPE = "application/x-ndjson";

/** The two Worker settings that switch forwarding on (`NINJALOG_ENABLED` is a wrangler var, `NINJALOG_TOKEN` a secret). */
export interface NinjalogEnv {
  readonly NINJALOG_ENABLED?: string;
  readonly NINJALOG_TOKEN?: string;
}

/** A count that was written to `dino_usage`, with its batch's context. */
export type SentCount = WrittenCount;

/** Called by the events and speaker handlers with the counts they just wrote; never throws, never awaited by the request. */
export type NinjalogTee = WrittenTee;

export type NinjalogFetch = (input: string, init: RequestInit) => Promise<Response>;

/** The token when forwarding is on, else null (off: flag not exactly "true", or no token). */
export function ninjalogToken(env: NinjalogEnv): string | null {
  const token = env.NINJALOG_TOKEN;
  return env.NINJALOG_ENABLED === NINJALOG_ENABLED_VALUE && typeof token === "string" && token !== "" ? token : null;
}

function present<T>(value: T, none: T): T | undefined {
  return value === none ? undefined : value;
}

/**
 * One count as a named document: the field names are the ones in `events.ts` (never `blobN`). `timestamp` is ISO-8601 (the batch's
 * receive time; which field Ninjalog reads as the event time is checked at the
 * first live send). Fields a count did not carry (empty session, -1 numbers) are left out so a query can test for them.
 */
export function ninjalogDocument(sent: SentCount, receivedAt: Date): Record<string, unknown> {
  const { event, context } = sent;
  return {
    "#index": NINJALOG_INDEX,
    timestamp: receivedAt.toISOString(),
    name: event.name,
    detail: present(event.detail, ""),
    book: event.book,
    language: event.language,
    source: event.source,
    hasRecording: event.hasRecording,
    version: event.version,
    platform: event.platform,
    day: event.day,
    sampleRate: event.sampleRate,
    sampleInterval: 1 / event.sampleRate,
    session: present(event.session, ""),
    seq: present(event.seq, NO_NUMBER),
    sinceLaunch: present(event.sinceLaunch, NO_NUMBER),
    count: present(event.count, NO_NUMBER),
    deviceCount: present(event.deviceCount, NO_NUMBER),
    seconds: present(event.seconds, NO_NUMBER),
    uiLang: event.uiLang,
    installAge: event.installAge,
    launchNo: event.launchNo,
    screen: event.screen,
    country: context.country,
    os: context.device.osMajor,
    form: context.device.formFactor,
    arch: context.device.arch,
    cores: context.device.cores,
    ram: context.device.ram,
  };
}

/** One newline-delimited JSON body for a batch (JSON.stringify leaves out the undefined fields). */
export function ninjalogBody(documents: readonly Record<string, unknown>[]): string {
  return documents.map((document) => JSON.stringify(document)).join("\n");
}

/** Posts one batch. Resolves whether Ninjalog took it; never rejects (a timeout, a network error or a non-2xx is just false). */
export async function postToNinjalog(token: string, documents: readonly Record<string, unknown>[], send: NinjalogFetch = fetch): Promise<boolean> {
  if (documents.length === 0) return false;
  try {
    const response = await send(NINJALOG_INGEST_URL, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}`, "Content-Type": NDJSON_TYPE },
      body: ninjalogBody(documents),
      signal: AbortSignal.timeout(NINJALOG_TIMEOUT_MS),
    });
    if (!response.ok) console.warn(`ninjalog: ingest answered ${response.status}`);
    await response.body?.cancel();
    return response.ok;
  } catch {
    // no-fallback: a Ninjalog failure never reaches the request path; the Analytics Engine row is the record
    console.warn("ninjalog: ingest failed");
    return false;
  }
}

/**
 * The tee for a request, or undefined when forwarding is off. `defer` is the Worker's `ctx.waitUntil` (called as a method by the caller);
 * `send` is a test seam for `fetch`.
 */
export function ninjalogTee(env: NinjalogEnv, defer: (work: Promise<unknown>) => void, send: NinjalogFetch = fetch, now: () => Date = () => new Date()): NinjalogTee | undefined {
  const token = ninjalogToken(env);
  if (token === null) return undefined;
  return (sent) => {
    if (sent.length === 0) return;
    try {
      const receivedAt = now();
      defer(postToNinjalog(token, sent.map((count) => ninjalogDocument(count, receivedAt)), send));
    } catch {
      // no-fallback: building or deferring the post must never fail the request
    }
  };
}
