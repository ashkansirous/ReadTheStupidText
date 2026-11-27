// Slice 458: the two store-notification endpoints, `POST /api/store/apple` and `POST /api/store/google` (and their `/dino` twins on workers.dev).
// Each delivery is verified, decided (apple.ts, google.ts), deduplicated and written as one `store` row. Answers carry no body: 204 taken or deliberately dropped, 400 not a
// store's notification, 401 a Pub/Sub push with no valid token, 405 not a POST, 503 for a notification we could not decide (the store redelivers). A row that cannot be written (dataset unbound, invalid row) is a logged 204: a redelivery cannot fix it.
import { NO_CONTEXT, NO_EXTRA_FIELDS, isStoreCount, readCappedText, reply, toDataPoint } from "../events.ts";
import type { UsageEvent } from "../events.ts";
import type { Fetcher } from "../entitlements/types.ts";
import { decideApple } from "./apple.ts";
import { alreadySeen, markSeen } from "./dedup.ts";
import { RETRY } from "./types.ts";
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
  /** The KV namespace that holds notification ids for seven days (item 4 creates it). */
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

/** Why a count was not written: no dataset is bound for it (a config fault) or the row is not a valid `store` row. Neither is fixed by a redelivery. */
function writeCount(count: StoreCount, env: StoreNotificationsEnv, now: Date): "written" | "unbound" | "invalid" {
  const dataset = count.test ? env.DINO_USAGE_BETA : env.DINO_USAGE; // production-only analytics: a sandbox or test purchase never reaches dino_usage
  const event = storeEvent(count, now);
  if (dataset === undefined) return "unbound";
  if (!isStoreCount(event)) return "invalid";
  dataset.writeDataPoint(toDataPoint(event, { ...NO_CONTEXT, country: count.country }));
  return "written";
}

const noBody = (): Response => new Response(null, { status: NO_BODY_STATUS });

/** The dedup read: a KV that throws counts as unseen (a double count beats a lost one). */
async function seenBefore(env: StoreNotificationsEnv, key: string): Promise<boolean> {
  try {
    return await alreadySeen(env.STORE_DEDUP, key);
  } catch {
    console.warn("store-notifications dedup read failed"); // no-fallback: unseen, so the row is counted
    return false;
  }
}

/** The dedup write after the row exists: a failure is logged and the delivery still succeeds, so the store does not redeliver and count the row again. */
async function remember(env: StoreNotificationsEnv, key: string): Promise<void> {
  try {
    await markSeen(env.STORE_DEDUP, key);
  } catch {
    console.warn("store-notifications dedup write failed"); // no-fallback: the row exists; answering an error would make the store redeliver it
  }
}

async function finish(outcome: Outcome, env: StoreNotificationsEnv, now: Date): Promise<Response> {
  if (outcome.type === "refuse") return reply(400);
  if (outcome.type === "retry") return reply(503);
  if (outcome.type === "ignore") return noBody();
  if (await seenBefore(env, outcome.dedupKey)) return noBody();
  const written = writeCount(outcome.count, env, now);
  if (written !== "written") {
    console.warn(`store-notifications row dropped reason=${written} test=${outcome.count.test}`); // a config fault: 204, since the store's redelivery would fail the same way for days
    return noBody();
  }
  await remember(env, outcome.dedupKey);
  return noBody();
}

async function decide(google: boolean, body: unknown, env: StoreNotificationsEnv, fetcher: Fetcher, books: BookResolver, deps: StoreNotificationsDeps, now: Date): Promise<Outcome> {
  try {
    return google ? await decideGoogle(body, env, fetcher, books) : await decideApple(body, books, deps.apple, now);
  } catch {
    return RETRY; // no-fallback: something we could not read (the catalogue bucket) is not a reason to lose the count; the store redelivers
  }
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
  const outcome = await decide(google, body, env, fetcher, books, deps, now);
  return finish(outcome, env, now);
}
