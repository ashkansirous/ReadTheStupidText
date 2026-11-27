// Slice 458: a store names a book by its product id (`uk.sirous.dinoreader.book.<stem>`), the row by its book id. The link is the `productId` in each store book's meta.json (Slice 346).
// Read from the downloads bucket and kept in the isolate for ten minutes; a product with no match is not counted (no row is better than a row for the wrong book).
import { STORY_ID_PATTERN } from "../download-keys.mjs";
import type { BookResolver } from "./types.ts";

const STORIES_PREFIX = "dinoreader/stories/";
const META_SUFFIX = "/meta.json";
const REUSE_MS = 10 * 60_000;
const LIST_LIMIT = 1000;
const MAX_PAGES = 5;

const cache = new WeakMap<R2Bucket, { readonly at: number; readonly byProduct: ReadonlyMap<string, string> }>();

/** The book id in a meta.json key (`dinoreader/stories/<id>/meta.json`), or null. */
export function bookIdOfMetaKey(key: string): string | null {
  if (!key.startsWith(STORIES_PREFIX) || !key.endsWith(META_SUFFIX)) return null;
  const id = key.slice(STORIES_PREFIX.length, key.length - META_SUFFIX.length);
  return STORY_ID_PATTERN.test(id) ? id : null;
}

async function metaKeys(bucket: R2Bucket): Promise<string[]> {
  const keys: string[] = [];
  let cursor: string | undefined;
  for (let page = 0; page < MAX_PAGES; page += 1) {
    const result = await bucket.list({ prefix: STORIES_PREFIX, cursor, limit: LIST_LIMIT });
    keys.push(...result.objects.map((object) => object.key).filter((key) => bookIdOfMetaKey(key) !== null));
    if (!result.truncated) break;
    cursor = result.cursor;
  }
  return keys;
}

async function productOf(bucket: R2Bucket, key: string): Promise<[string, string] | null> {
  const object = await bucket.get(key);
  const meta = object === null ? null : ((await object.json()) as { productId?: unknown });
  const id = bookIdOfMetaKey(key);
  return id !== null && meta !== null && typeof meta.productId === "string" && meta.productId !== "" ? [meta.productId, id] : null;
}

async function loadProducts(bucket: R2Bucket): Promise<ReadonlyMap<string, string>> {
  const pairs = await Promise.all((await metaKeys(bucket)).map((key) => productOf(bucket, key)));
  return new Map(pairs.filter((pair): pair is [string, string] => pair !== null));
}

/** The resolver over the downloads bucket. A bucket that cannot be read answers null (the row is dropped, the delivery is not retried). */
export function bookResolverFor(bucket: R2Bucket): BookResolver {
  return async (productId) => {
    try {
      const held = cache.get(bucket);
      if (held !== undefined && Date.now() - held.at < REUSE_MS) return held.byProduct.get(productId) ?? null;
      const byProduct = await loadProducts(bucket);
      cache.set(bucket, { at: Date.now(), byProduct });
      return byProduct.get(productId) ?? null;
    } catch {
      return null; // no-fallback: the catalogue could not be read, so the book cannot be named and no row is written
    }
  };
}
