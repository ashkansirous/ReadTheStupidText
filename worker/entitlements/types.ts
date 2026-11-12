// Purchase checks per store (Slice 346). Each store has one checker behind the same shape: a proof the app sent in, the products
// the store says are owned out. A checker that cannot ask the store (missing secrets, store down, a bad reply) answers
// `cannot-verify`, never `owned`. Nothing about the buyer is stored or logged. Keep this folder free of enums and parameter
// properties so worker/entitlements.test.mjs can run it under node's type stripping.

export const Store = { Android: "android", Ios: "ios", Windows: "windows" } as const;
export type StoreName = (typeof Store)[keyof typeof Store];

export const Verdict = { Checked: "checked", CannotVerify: "cannot-verify" } as const;
export type VerdictName = (typeof Verdict)[keyof typeof Verdict];

/** What the app sends in the `X-Purchase-Proof` header: `<store>:<productId>:<token>` (the token is the rest of the line). */
export interface Proof {
  readonly store: StoreName;
  /** The product the token was issued for (Android purchase token, iOS transaction id); empty for Windows, whose key covers every product. */
  readonly productId: string;
  readonly token: string;
}

/** `checked` carries the owned product ids (possibly none: a valid answer that the buyer owns nothing); `cannot-verify` carries none. */
export interface CheckResult {
  readonly verdict: VerdictName;
  readonly owned: readonly string[];
}

/** The fetch the checkers use; tests pass a recorded one. */
export type Fetcher = (url: string, init?: RequestInit) => Promise<Response>;

/** Store keys, all Worker secrets (`wrangler secret put`, Part D); a missing one makes that store `cannot-verify`. */
export interface EntitlementEnv {
  /** The Play service account's JSON key (client_email, private_key). */
  readonly PLAY_SERVICE_ACCOUNT_JSON?: string;
  /** App Store Server API key: the .p8 PEM, its key id and the issuer id. */
  readonly APPLE_IAP_KEY_P8?: string;
  readonly APPLE_IAP_KEY_ID?: string;
  readonly APPLE_IAP_ISSUER_ID?: string;
  /** "true" only on a Worker that should honour sandbox (TestFlight, App Review) purchases; unset in production. */
  readonly APPLE_IAP_ALLOW_SANDBOX?: string;
  /** Microsoft Store collections API: an Entra ID web app (tenant, client id and secret). */
  readonly MS_STORE_TENANT_ID?: string;
  readonly MS_STORE_CLIENT_ID?: string;
  readonly MS_STORE_CLIENT_SECRET?: string;
}

export interface StoreChecker {
  check(proof: Proof, env: EntitlementEnv, fetcher: Fetcher): Promise<CheckResult>;
}

export const CANNOT_VERIFY: CheckResult = { verdict: Verdict.CannotVerify, owned: [] };

export function checked(owned: readonly string[]): CheckResult {
  return { verdict: Verdict.Checked, owned };
}
