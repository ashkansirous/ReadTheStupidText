// Slice 458: a store may deliver one notification twice. The notification's own UUID (Apple) or message id (Pub/Sub) is kept as a short-lived KV key and nothing else:
// the value is a single character, the key expires after seven days, and it is never logged. Without the binding every delivery counts (a double count beats a lost one).
/** How long a notification id is remembered, and the oldest `signedDate` Apple's payloads may carry (apple.ts): Apple retries for about 157 hours, so both are 7 days. */
export const DEDUP_WINDOW_DAYS = 7;
const SECONDS_PER_DAY = 24 * 60 * 60;
export const DEDUP_WINDOW_MS = DEDUP_WINDOW_DAYS * SECONDS_PER_DAY * 1000;
const DEDUP_TTL_S = DEDUP_WINDOW_DAYS * SECONDS_PER_DAY;
const SEEN = "1";

export interface DedupStore {
  get(key: string): Promise<string | null>;
  put(key: string, value: string, options: { expirationTtl: number }): Promise<void>;
}

const hasNoId = (key: string): boolean => key.endsWith(":");

/** True when this key was already counted. A key with no id part (`apple:`) is never seen. Reading does not remember: call `markSeen` once the row is written. */
export async function alreadySeen(store: DedupStore | undefined, key: string): Promise<boolean> {
  if (store === undefined || hasNoId(key)) return false;
  return (await store.get(key)) !== null;
}

/** Remembers a counted key for the dedup window, after its row was written (a failed write must stay retryable). A key with no id part is never remembered. */
export async function markSeen(store: DedupStore | undefined, key: string): Promise<void> {
  if (store === undefined || hasNoId(key)) return;
  await store.put(key, SEEN, { expirationTtl: DEDUP_TTL_S });
}
