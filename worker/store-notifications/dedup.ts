// Slice 458: a store may deliver one notification twice. The notification's own UUID (Apple) or message id (Pub/Sub) is kept as a short-lived KV key and nothing else:
// the value is a single character, the key expires after a day, and it is never logged. Without the binding every delivery counts (a double count beats a lost one).
const DEDUP_TTL_S = 24 * 60 * 60;
const SEEN = "1";

export interface DedupStore {
  get(key: string): Promise<string | null>;
  put(key: string, value: string, options: { expirationTtl: number }): Promise<void>;
}

/** True when this key was already counted; otherwise remembers it and returns false. A key with no id part (`apple:`) is never remembered. */
export async function alreadySeen(store: DedupStore | undefined, key: string): Promise<boolean> {
  if (store === undefined || key.endsWith(":")) return false;
  if ((await store.get(key)) !== null) return true;
  await store.put(key, SEEN, { expirationTtl: DEDUP_TTL_S });
  return false;
}
