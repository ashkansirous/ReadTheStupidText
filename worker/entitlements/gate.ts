// The paid-book gate (Slice 346): decides whether a request may have a paid book's files. Off (`paidBooks.enabled` not a literal true)
// every book is free and no proof is looked at. On, a paid book needs a proof header whose store says the buyer owns a product that
// unlocks the book. An answer is kept in memory per proof (keyed by a hash of it, never written anywhere; minutes for "owns", seconds for
// "owns nothing", at most MAX_CACHED of them), so a download's many requests (Range, audio packs) and a flood of bad proofs ask the store once.
// Nothing about the buyer is stored or logged.
import { unlocksBook } from "../products.ts";
import { appleChecker } from "./apple.ts";
import { googleChecker } from "./google.ts";
import { microsoftChecker } from "./microsoft.ts";
import { parseProof } from "./proof.ts";
import { Store, Verdict } from "./types.ts";
import type { EntitlementEnv, Fetcher, Proof, StoreChecker, StoreName } from "./types.ts";

export const GateAnswer = { Off: "off", Allowed: "allowed", NoProof: "no-proof", BadProof: "bad-proof", CannotVerify: "cannot-verify", RateLimited: "rate-limited" } as const;
export type GateAnswerName = (typeof GateAnswer)[keyof typeof GateAnswer];

export interface PaidBook {
  readonly id: string;
  readonly productId: string | null;
}

/** Asked per request for a paid book: the header value (or null), the book and the client key (only the rate limiter's key, never stored). */
export type PaidGate = (proofHeader: string | null, book: PaidBook, clientKey: string) => Promise<GateAnswerName>;

/** True when this client may make one more call to a store (the per-client limit in front of the store checkers). */
export type StoreCallLimit = (clientKey: string) => Promise<boolean>;

export const OWNED_CACHE_MS = 5 * 60_000;
export const NOT_OWNED_CACHE_MS = 30_000;
export const MAX_CACHED = 500;

const CHECKERS: Readonly<Record<StoreName, StoreChecker>> = {
  [Store.Android]: googleChecker,
  [Store.Ios]: appleChecker,
  [Store.Windows]: microsoftChecker,
};

interface Cached {
  readonly at: number;
  readonly owned: readonly string[];
}

const ownedCache = new Map<string, Cached>();

async function proofKey(proof: Proof): Promise<string> {
  const bytes = new TextEncoder().encode(`${proof.store}\n${proof.productId}\n${proof.token}`);
  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", bytes));
  return Array.from(digest, (byte) => byte.toString(16).padStart(2, "0")).join("");
}

/** A "owns something" answer is kept OWNED_CACHE_MS; a "owns nothing" one only NOT_OWNED_CACHE_MS, so a purchase just made is seen soon. */
function lifetimeOf(owned: readonly string[]): number {
  return owned.length > 0 ? OWNED_CACHE_MS : NOT_OWNED_CACHE_MS;
}

function remember(key: string, owned: readonly string[]): void {
  ownedCache.delete(key); // re-inserted last, so the oldest key is always the first
  if (ownedCache.size >= MAX_CACHED) ownedCache.delete(ownedCache.keys().next().value as string);
  ownedCache.set(key, { at: Date.now(), owned });
}

const OVER_LIMIT = "over-limit";

/**
 * The products the proof's store says are owned, null when the store could not be asked, or OVER_LIMIT when this client has used up its store calls.
 * Only a call that would reach a store counts against the limit: a cached answer is free, so a download's many requests are never limited.
 */
async function ownedProducts(
  proof: Proof,
  clientKey: string,
  options: { readonly env: EntitlementEnv; readonly fetcher: Fetcher; readonly checkers: Readonly<Record<StoreName, StoreChecker>>; readonly limit?: StoreCallLimit },
): Promise<readonly string[] | null | typeof OVER_LIMIT> {
  const key = await proofKey(proof);
  const cached = ownedCache.get(key);
  if (cached !== undefined && Date.now() - cached.at < lifetimeOf(cached.owned)) return cached.owned;
  if (options.limit !== undefined && !(await options.limit(clientKey))) return OVER_LIMIT;
  const result = await options.checkers[proof.store].check(proof, options.env, options.fetcher);
  if (result.verdict !== Verdict.Checked) return null;
  remember(key, result.owned);
  return result.owned;
}

export interface GateOptions {
  /** The `paidBooks.enabled` switch, read when a paid book is asked for. */
  readonly enabled: () => Promise<boolean>;
  readonly env: EntitlementEnv;
  readonly fetcher: Fetcher;
  /** Test seam: replaces the three store checkers. */
  readonly checkers?: Readonly<Record<StoreName, StoreChecker>>;
  /** The per-client limit on calls that reach a store checker; a client over it gets RateLimited, never Allowed. Absent = no limit (tests). */
  readonly limit?: StoreCallLimit;
}

/** A switch that cannot be read counts as off, so with it unreadable every route behaves as it did before paid books. */
async function isOn(enabled: () => Promise<boolean>): Promise<boolean> {
  try {
    return await enabled();
  } catch {
    return false; // no-fallback: unreadable switch = off, by design (Slice 346)
  }
}

export function createPaidGate(options: GateOptions): PaidGate {
  const checkers = options.checkers ?? CHECKERS;
  return async (proofHeader, book, clientKey) => {
    if (!(await isOn(options.enabled))) return GateAnswer.Off;
    if (proofHeader === null || proofHeader.trim() === "") return GateAnswer.NoProof;
    const proof = parseProof(proofHeader.trim());
    if (proof === null) return GateAnswer.BadProof;
    const owned = await ownedProducts(proof, clientKey, { env: options.env, fetcher: options.fetcher, checkers, limit: options.limit });
    if (owned === OVER_LIMIT) return GateAnswer.RateLimited;
    if (owned === null) return GateAnswer.CannotVerify;
    return unlocksBook(owned, book.id, book.productId) ? GateAnswer.Allowed : GateAnswer.BadProof;
  };
}

/** Test seam: forgets every cached answer. */
export function resetOwnedCache(): void {
  ownedCache.clear();
}
