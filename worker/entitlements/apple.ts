// Apple: App Store Server API, Get Transaction Info (ES256 token from the .p8 key). The transaction counts when it is for this app and
// the proof's product, is in the Production environment and has not been revoked. Only when the Worker is told to (`APPLE_IAP_ALLOW_SANDBOX`, a staging Worker),
// a transaction the production host does not know is tried on the sandbox host and a Sandbox one counts; in production a sandbox purchase never unlocks a book.
import { APP_PACKAGE_ID, isSubscriptionProduct } from "../products.ts";
import { jwtPayload, signJwt } from "./jwt.ts";
import { CANNOT_VERIFY, CANNOT_VERIFY_SUBSCRIPTION, NO_SUBSCRIPTION, SubscriptionState, checked, checkedSubscription } from "./types.ts";
import type { CheckResult, EntitlementEnv, Fetcher, Proof, StoreChecker, SubscriptionCheck, SubscriptionStatus } from "./types.ts";

const PRODUCTION_HOST = "https://api.storekit.apple.com";
const SANDBOX_HOST = "https://api.storekit-sandbox.apple.com";
const AUDIENCE = "appstoreconnect-v1";
const TOKEN_LIFETIME_S = 1800;
const TOKEN_REUSE_MS = 20 * 60_000;
const NOT_FOUND = 404;
const ENVIRONMENT_PRODUCTION = "Production";
const ENVIRONMENT_SANDBOX = "Sandbox";

let cachedToken: { readonly value: string; readonly at: number; readonly keyId: string } | null = null;

interface AppleKey {
  readonly pem: string;
  readonly keyId: string;
  readonly issuerId: string;
}

function keyOf(env: EntitlementEnv): AppleKey | null {
  const { APPLE_IAP_KEY_P8: pem, APPLE_IAP_KEY_ID: keyId, APPLE_IAP_ISSUER_ID: issuerId } = env;
  return pem && keyId && issuerId ? { pem, keyId, issuerId } : null;
}

async function bearer(key: AppleKey): Promise<string> {
  if (cachedToken !== null && cachedToken.keyId === key.keyId && Date.now() - cachedToken.at < TOKEN_REUSE_MS) return cachedToken.value;
  const now = Math.floor(Date.now() / 1000);
  const value = await signJwt("ES256", { kid: key.keyId }, { iss: key.issuerId, iat: now, exp: now + TOKEN_LIFETIME_S, aud: AUDIENCE, bid: APP_PACKAGE_ID }, key.pem);
  cachedToken = { value, at: Date.now(), keyId: key.keyId };
  return value;
}

/** A sandbox transaction counts only when the Worker is told to accept them (`APPLE_IAP_ALLOW_SANDBOX` = "true", for TestFlight and App Review); in production the payload's `environment` must be Production. */
function sandboxAllowed(env: EntitlementEnv): boolean {
  return env.APPLE_IAP_ALLOW_SANDBOX === "true";
}

function transactionOwns(signed: unknown, productId: string, allowSandbox: boolean): boolean {
  const payload = typeof signed === "string" ? jwtPayload(signed) : null;
  if (payload === null) return false;
  const environment = payload["environment"];
  const rightEnvironment = environment === ENVIRONMENT_PRODUCTION || (allowSandbox && environment === ENVIRONMENT_SANDBOX);
  return rightEnvironment && payload["bundleId"] === APP_PACKAGE_ID && payload["productId"] === productId && payload["revocationDate"] === undefined;
}

async function askHost(host: string, proof: Proof, token: string, fetcher: Fetcher): Promise<Response> {
  return fetcher(`${host}/inApps/v1/transactions/${encodeURIComponent(proof.token)}`, { headers: { Authorization: `Bearer ${token}`, Accept: "application/json" } });
}

async function resultOf(response: Response, productId: string, allowSandbox: boolean): Promise<CheckResult> {
  if (response.status === NOT_FOUND || response.status === 400) return checked([]);
  if (response.status !== 200) return CANNOT_VERIFY;
  const body = (await response.json()) as { signedTransactionInfo?: unknown };
  return checked(transactionOwns(body.signedTransactionInfo, productId, allowSandbox) ? [productId] : []);
}

// Slice 384: a subscription is read with Get All Subscription Statuses (GET /inApps/v1/subscriptions/{transactionId}, any transaction of the customer), which answers with the
// latest transaction of each subscription: it counts while its status is Active (1) or in the billing grace period (4), its expiresDate is ahead and it has not been revoked.
const STATUS_ACTIVE = 1;
const STATUS_GRACE = 4;
const ENTITLED_STATUSES: readonly number[] = [STATUS_ACTIVE, STATUS_GRACE];

interface StatusLast {
  readonly status?: unknown;
  readonly signedTransactionInfo?: unknown;
  readonly signedRenewalInfo?: unknown;
}

function subscriptionProductOf(last: StatusLast, allowSandbox: boolean, now: number): string | null {
  if (typeof last.status !== "number" || !ENTITLED_STATUSES.includes(last.status)) return null;
  const payload = typeof last.signedTransactionInfo === "string" ? jwtPayload(last.signedTransactionInfo) : null;
  if (payload === null) return null;
  const productId = payload["productId"];
  const environment = payload["environment"];
  const rightEnvironment = environment === ENVIRONMENT_PRODUCTION || (allowSandbox && environment === ENVIRONMENT_SANDBOX);
  const expires = payload["expiresDate"];
  const inGrace = last.status === STATUS_GRACE; // in billing grace Apple's own status says "still paid" while the transaction's expiresDate is already past
  const live = (inGrace || (typeof expires === "number" && expires > now)) && payload["revocationDate"] === undefined;
  return rightEnvironment && live && payload["bundleId"] === APP_PACKAGE_ID && typeof productId === "string" && isSubscriptionProduct(productId) ? productId : null;
}

async function subscriptionResultOf(response: Response, allowSandbox: boolean): Promise<CheckResult> {
  if (response.status === NOT_FOUND || response.status === 400) return checked([]);
  if (response.status !== 200) return CANNOT_VERIFY;
  const body = (await response.json()) as { data?: { lastTransactions?: StatusLast[] }[] };
  const lasts = (Array.isArray(body.data) ? body.data : []).flatMap((group) => (Array.isArray(group.lastTransactions) ? group.lastTransactions : []));
  const owned = lasts.map((last) => subscriptionProductOf(last, allowSandbox, Date.now())).filter((productId): productId is string => productId !== null);
  return checked([...new Set(owned)]);
}

async function askSubscriptionHost(host: string, proof: Proof, token: string, fetcher: Fetcher): Promise<Response> {
  return fetcher(`${host}/inApps/v1/subscriptions/${encodeURIComponent(proof.token)}`, { headers: { Authorization: `Bearer ${token}`, Accept: "application/json" } });
}

async function checkSubscription(proof: Proof, token: string, allowSandbox: boolean, fetcher: Fetcher): Promise<CheckResult> {
  const production = await askSubscriptionHost(PRODUCTION_HOST, proof, token, fetcher);
  const response = production.status === NOT_FOUND && allowSandbox ? await askSubscriptionHost(SANDBOX_HOST, proof, token, fetcher) : production;
  return subscriptionResultOf(response, allowSandbox);
}

// Slice 384 (finding 2): the state and end date the app shows, from the same statuses answer. Active (1) and grace (4) rank above everything else; among equals the later end wins. In grace the end is the
// renewal info's gracePeriodExpiresDate (the transaction's own expiresDate is already past); a revoked one (status 5 or a revocationDate) is revoked.
const STATUS_REVOKED = 5;
const PAID_RANK = 1_000_000_000_000_000;

function iso(ms: unknown): string | null {
  return typeof ms === "number" ? new Date(ms).toISOString() : null;
}

function graceEnd(last: StatusLast, now: number): string | null {
  const renewal = typeof last.signedRenewalInfo === "string" ? jwtPayload(last.signedRenewalInfo) : null;
  const grace = renewal?.["gracePeriodExpiresDate"];
  return iso(typeof grace === "number" && Number.isFinite(grace) ? Math.max(grace, now) : now); // no usable grace date: the transaction's expiresDate is already past, so grace counts from the read time; a grace date already past reads as still in grace now (as google.ts does), since the gate still serves files
}

function statusOfLast(last: StatusLast, allowSandbox: boolean, now: number): SubscriptionStatus | null {
  const payload = typeof last.signedTransactionInfo === "string" ? jwtPayload(last.signedTransactionInfo) : null;
  if (payload === null || typeof last.status !== "number") return null;
  const environment = payload["environment"];
  const productId = payload["productId"];
  const rightEnvironment = environment === ENVIRONMENT_PRODUCTION || (allowSandbox && environment === ENVIRONMENT_SANDBOX);
  if (!rightEnvironment || payload["bundleId"] !== APP_PACKAGE_ID || typeof productId !== "string" || !isSubscriptionProduct(productId)) return null;
  const expires = payload["expiresDate"];
  if (last.status === STATUS_REVOKED || payload["revocationDate"] !== undefined) return { state: SubscriptionState.Revoked, expiresAt: iso(expires) };
  if (last.status === STATUS_GRACE) return { state: SubscriptionState.Grace, expiresAt: graceEnd(last, now) };
  const running = last.status === STATUS_ACTIVE && typeof expires === "number" && expires > now;
  return { state: running ? SubscriptionState.Active : SubscriptionState.Expired, expiresAt: iso(expires) };
}

function rankOf(status: SubscriptionStatus): number {
  const paid = status.state === SubscriptionState.Active || status.state === SubscriptionState.Grace ? PAID_RANK : 0;
  return paid + (status.expiresAt === null ? 0 : Date.parse(status.expiresAt));
}

function bestStatus(body: { data?: { lastTransactions?: StatusLast[] }[] }, allowSandbox: boolean, now: number): SubscriptionStatus {
  const lasts = (Array.isArray(body.data) ? body.data : []).flatMap((group) => (Array.isArray(group.lastTransactions) ? group.lastTransactions : []));
  const found = lasts.map((last) => statusOfLast(last, allowSandbox, now)).filter((status): status is SubscriptionStatus => status !== null);
  return found.reduce((best, status) => (rankOf(status) > rankOf(best) ? status : best), NO_SUBSCRIPTION);
}

async function checkSubscriptionStatus(proof: Proof, token: string, allowSandbox: boolean, fetcher: Fetcher): Promise<SubscriptionCheck> {
  const production = await askSubscriptionHost(PRODUCTION_HOST, proof, token, fetcher);
  const response = production.status === NOT_FOUND && allowSandbox ? await askSubscriptionHost(SANDBOX_HOST, proof, token, fetcher) : production;
  if (response.status !== 200) return CANNOT_VERIFY_SUBSCRIPTION; // a 404 or 400 is not "expired" (the transaction may not have propagated yet): the app keeps its record; only the status values say expired
  return checkedSubscription(bestStatus((await response.json()) as { data?: { lastTransactions?: StatusLast[] }[] }, allowSandbox, Date.now()));
}

export const appleChecker: StoreChecker = {
  async check(proof, env, fetcher) {
    const key = keyOf(env);
    if (key === null) return CANNOT_VERIFY;
    try {
      const token = await bearer(key);
      if (isSubscriptionProduct(proof.productId)) return await checkSubscription(proof, token, sandboxAllowed(env), fetcher);
      const production = await askHost(PRODUCTION_HOST, proof, token, fetcher);
      const allowSandbox = sandboxAllowed(env);
      const response = production.status === NOT_FOUND && allowSandbox ? await askHost(SANDBOX_HOST, proof, token, fetcher) : production;
      return await resultOf(response, proof.productId, allowSandbox);
    } catch {
      return CANNOT_VERIFY; // network or signing failure: we could not ask, which is never "owned"
    }
  },
  async subscription(proof, env, fetcher) {
    const key = keyOf(env);
    if (key === null) return CANNOT_VERIFY_SUBSCRIPTION;
    try {
      return await checkSubscriptionStatus(proof, await bearer(key), sandboxAllowed(env), fetcher);
    } catch {
      return CANNOT_VERIFY_SUBSCRIPTION; // no-fallback: we could not ask, which the app reads as "keep what you hold", never as expired
    }
  },
};

/** Test seam: forgets the cached token. */
export function resetAppleTokenCache(): void {
  cachedToken = null;
}
