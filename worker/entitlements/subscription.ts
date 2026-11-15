// The subscription's state and end date for the app (Slice 384, finding 2): iOS and Android have no end date on the device, so the app asks here after each store read, with the same
// `X-Purchase-Proof` header as a paid book's files (a subscription product's proof). Same switch (`paidBooks.enabled`), same per-client store-call limit and same error codes as the paid gate:
// 404 switch off, 402 no proof, 403 a proof that is not a subscription's (or Windows, which reads its end locally), 503 a store that cannot be asked, 429 over the limit. An expired or revoked
// subscription is a normal 200 answer with its state. An answer is kept in memory per proof for SUBSCRIPTION_CACHE_MS (a hash key, never written anywhere). Nothing about the buyer is stored or logged.
import { isSubscriptionProduct } from "../products.ts";
import { CHECKERS, proofKey } from "./gate.ts";
import type { GateOptions } from "./gate.ts";
import { parseProof } from "./proof.ts";
import { Store, Verdict } from "./types.ts";
import type { SubscriptionStatus } from "./types.ts";

export const SubscriptionAnswerKind = { Off: "off", NoProof: "no-proof", BadProof: "bad-proof", CannotVerify: "cannot-verify", RateLimited: "rate-limited", Status: "status" } as const;
export type SubscriptionAnswerKindName = (typeof SubscriptionAnswerKind)[keyof typeof SubscriptionAnswerKind];

export interface SubscriptionAnswer {
  readonly kind: SubscriptionAnswerKindName;
  readonly status?: SubscriptionStatus;
}

/** Asked per request: the header value (or null) and the client key (only the rate limiter's key, never stored). */
export type SubscriptionSource = (proofHeader: string | null, clientKey: string) => Promise<SubscriptionAnswer>;

export const SUBSCRIPTION_CACHE_MS = 60_000;
export const SUBSCRIPTION_REFUSAL_CACHE_MS = 10_000;
export const MAX_CACHED_SUBSCRIPTIONS = 500;

const cache = new Map<string, { readonly at: number; readonly status: SubscriptionStatus }>();

function remember(key: string, status: SubscriptionStatus): void {
  cache.delete(key);
  if (cache.size >= MAX_CACHED_SUBSCRIPTIONS) cache.delete(cache.keys().next().value as string);
  cache.set(key, { at: Date.now(), status });
}

// Review finding 5: a "cannot verify" (store error) answer is remembered for a short time too, so a store outage does not spend a limiter slot (shared with the paid-book gate) on every call. A 429 is never cached: the limit is per client, the key per proof.
const refusals = new Map<string, { readonly at: number; readonly kind: SubscriptionAnswerKindName }>();

function recentRefusal(key: string): SubscriptionAnswerKindName | null {
  const held = refusals.get(key);
  return held !== undefined && Date.now() - held.at < SUBSCRIPTION_REFUSAL_CACHE_MS ? held.kind : null;
}

function refuse(key: string, kind: SubscriptionAnswerKindName): SubscriptionAnswer {
  refusals.delete(key);
  if (refusals.size >= MAX_CACHED_SUBSCRIPTIONS) refusals.delete(refusals.keys().next().value as string);
  refusals.set(key, { at: Date.now(), kind });
  return { kind };
}

async function isOn(enabled: () => Promise<boolean>): Promise<boolean> {
  try {
    return await enabled();
  } catch {
    return false; // no-fallback: unreadable switch = off, as in the paid gate
  }
}

export function createSubscriptionSource(options: GateOptions): SubscriptionSource {
  const checkers = options.checkers ?? CHECKERS;
  return async (proofHeader, clientKey) => {
    if (!(await isOn(options.enabled))) return { kind: SubscriptionAnswerKind.Off };
    if (proofHeader === null || proofHeader.trim() === "") return { kind: SubscriptionAnswerKind.NoProof };
    const proof = parseProof(proofHeader.trim());
    const checker = proof === null ? undefined : checkers[proof.store];
    if (proof === null || proof.store === Store.Windows || !isSubscriptionProduct(proof.productId) || checker?.subscription === undefined) return { kind: SubscriptionAnswerKind.BadProof };
    const key = await proofKey(proof);
    const cached = cache.get(key);
    if (cached !== undefined && Date.now() - cached.at < SUBSCRIPTION_CACHE_MS) return { kind: SubscriptionAnswerKind.Status, status: cached.status };
    const refused = recentRefusal(key);
    if (refused !== null) return { kind: refused };
    if (options.limit !== undefined && !(await options.limit(clientKey))) return { kind: SubscriptionAnswerKind.RateLimited }; // never cached: the limit is per client, the cache key per proof
    const result = await checker.subscription(proof, options.env, options.fetcher);
    if (result.verdict !== Verdict.Checked || result.status === null) return refuse(key, SubscriptionAnswerKind.CannotVerify);
    remember(key, result.status);
    return { kind: SubscriptionAnswerKind.Status, status: result.status };
  };
}

/** Test seam: forgets every cached answer. */
export function resetSubscriptionCache(): void {
  cache.clear();
  refusals.clear();
}
