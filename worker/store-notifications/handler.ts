// Slice 458: the two store-notification endpoints, `POST /api/store/apple` and `POST /api/store/google` (and their `/dino` twins on workers.dev).
// Each delivery is verified, decided (apple.ts, google.ts), deduplicated and written as one `store` row. Answers carry no body: 204 taken or deliberately dropped, 400 not a
// store's notification, 401 a Pub/Sub push with no valid token, 405 not a POST, 503 for a notification we could not decide (the store redelivers) or without its dataset.
import { NO_CONTEXT, NO_EXTRA_FIELDS, isStoreCount, readCappedText, reply, toDataPoint } from "../events.ts";
import type { UsageEvent } from "../events.ts";
import type { Fetcher } from "../entitlements/types.ts";
import { decideApple } from "./apple.ts";
import { alreadySeen } from "./dedup.ts";
import type { DedupStore } from "./dedup.ts";
import { decideGoogle, verifyPushToken } from "./google.ts";
import type { GoogleStoreEnv } from "./google.ts";
import { bookResolverFor } from "./books.ts";
import type { AppleJwsOptions } from "../entitlements/apple-jws.ts";
import type { BookResolver, Outcome, StoreCount } from "./types.ts";

export const STORE_APPLE_PATH = "/api/store/apple";
export const STORE_GOOGLE_PATH = "/api/store/google";
const STORE_EVENT = "store";
const NO_LANGUAGE = "und";
const NO_VERSION = "";
const FULL_WEIGHT = 1;
const ISO_DAY_LENGTH = 10;
const MAX_BODY_BYTES = 64 * 1024;
const NO_BODY_STATUS = 204;

export interface StoreNotificationsEnv extends GoogleStoreEnv {
  readonly DINO_USAGE?: AnalyticsEngineDataset;
  readonly DINO_USAGE_BETA?: AnalyticsEngineDataset;
  readonly DOWNLOADS?: R2Bucket;
  /** The KV namespace that holds notification ids for a day (item 4 creates it). */
  readonly STORE_DEDUP?: DedupStore;
}

/** What a test can swap: the fetch to Google/Play, the Apple chain's trusted roots, the book lookup and the clock. */
export interface StoreNotificationsDeps {
  readonly fetcher?: Fetcher;
  readonly apple?: AppleJwsOptions;
  readonly books?: BookResolver;
  readonly now?: Date;
}

/** True for the two endpoints' paths. */
export function isStorePath(pathname: string): boolean {
  return pathname === STORE_APPLE_PATH || pathname === STORE_GOOGLE_PATH;
}

/** The `store` row of a decided count. */
export function storeEvent(count: StoreCount, now: Date): UsageEvent {
  return {
    name: STORE_EVENT, book: count.book, language: NO_LANGUAGE, source: count.source, hasRecording: false, version: NO_VERSION, platform: count.platform,
    day: now.toISOString().slice(0, ISO_DAY_LENGTH), sampleRate: FULL_WEIGHT, ...NO_EXTRA_FIELDS, detail: `${count.platform}.${count.kind}.${count.offer}`,
  };
}

function writeCount(count: StoreCount, env: StoreNotificationsEnv, now: Date): boolean {
  const dataset = count.test ? env.DINO_USAGE_BETA : env.DINO_USAGE; // production-only analytics: a sandbox or test purchase never reaches dino_usage
  const event = storeEvent(count, now);
  if (dataset === undefined || !isStoreCount(event)) return false;
  dataset.writeDataPoint(toDataPoint(event, { ...NO_CONTEXT, country: count.country }));
  return true;
}

async function finish(outcome: Outcome, env: StoreNotificationsEnv, now: Date): Promise<Response> {
  if (outcome.type === "refuse") return reply(400);
  if (outcome.type === "retry") return reply(503);
  if (outcome.type === "ignore") return new Response(null, { status: NO_BODY_STATUS });
  if (await alreadySeen(env.STORE_DEDUP, outcome.dedupKey)) return new Response(null, { status: NO_BODY_STATUS });
  return writeCount(outcome.count, env, now) ? new Response(null, { status: NO_BODY_STATUS }) : reply(503);
}

async function jsonBody(request: Request): Promise<unknown> {
  const text = await readCappedText(request, MAX_BODY_BYTES);
  if (text === "too-large") return undefined;
  try {
    return JSON.parse(text);
  } catch {
    return undefined; // no-fallback: a body that is not JSON is a refused delivery
  }
}

/** Answers one store notification (path already matched by `isStorePath`). */
export async function handleStoreNotification(request: Request, env: StoreNotificationsEnv, deps: StoreNotificationsDeps = {}): Promise<Response> {
  if (request.method !== "POST") return reply(405);
  const pathname = new URL(request.url).pathname;
  const google = pathname.endsWith(STORE_GOOGLE_PATH);
  const fetcher = deps.fetcher ?? ((url, init) => fetch(url, init));
  const now = deps.now ?? new Date();
  if (google && !(await verifyPushToken(request.headers.get("Authorization"), env, fetcher, Math.floor(now.getTime() / 1000)))) return reply(401);
  const body = await jsonBody(request);
  if (body === undefined) return reply(400);
  const books = deps.books ?? (env.DOWNLOADS === undefined ? async () => null : bookResolverFor(env.DOWNLOADS));
  const outcome = google ? await decideGoogle(body, env, fetcher, books) : await decideApple(body, books, deps.apple);
  return finish(outcome, env, now);
}
