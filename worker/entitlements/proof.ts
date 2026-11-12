import { Store } from "./types.ts";
import type { Proof, StoreName } from "./types.ts";
import { isProductId } from "../products.ts";

export const PROOF_HEADER = "X-Purchase-Proof";
const MAX_TOKEN_LENGTH = 8192;
const STORES: readonly string[] = Object.values(Store);

/** Parses `<store>:<productId>:<token>`; null for anything malformed (the caller answers 403, never asks a store). */
export function parseProof(header: string): Proof | null {
  const first = header.indexOf(":");
  const second = header.indexOf(":", first + 1);
  if (first < 1 || second < 0) return null;
  const store = header.slice(0, first);
  const productId = header.slice(first + 1, second);
  const token = header.slice(second + 1).trim();
  const storeOk = STORES.includes(store);
  const productOk = productId === "" ? store === Store.Windows : isProductId(productId);
  if (!storeOk || !productOk || token === "" || token.length > MAX_TOKEN_LENGTH) return null;
  return { store: store as StoreName, productId, token };
}
