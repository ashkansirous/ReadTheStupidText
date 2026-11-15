// Apple: App Store Server API, Get Transaction Info (ES256 token from the .p8 key). The transaction counts when it is for this app and
// the proof's product, is in the Production environment and has not been revoked. Only when the Worker is told to (`APPLE_IAP_ALLOW_SANDBOX`, a staging Worker),
// a transaction the production host does not know is tried on the sandbox host and a Sandbox one counts; in production a plain transaction id that is a sandbox purchase never unlocks a book,
// but a signed transaction (JWS) that verifies against Apple's root and names the Sandbox environment is asked on the sandbox host on every Worker (TestFlight buys are Sandbox).
import { APP_PACKAGE_ID, BOOK_PRODUCT_PREFIX, isSubscriptionProduct } from "../products.ts";
import { isJws, verifyAppleSignedPayload } from "./apple-jws.ts";
import { jwtPayload, signJwt } from "./jwt.ts";
import { CANNOT_VERIFY, CANNOT_VERIFY_SUBSCRIPTION, NO_SUBSCRIPTION, SubscriptionState, Verdict, checked, checkedSubscription } from "./types.ts";
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

type AskHost = (host: string, proof: Proof, token: string, fetcher: Fetcher) => Promise<Response>;

/** Asks the first host; a production host that does not know the transaction (404) is followed by the sandbox host when sandbox is allowed (Apple's own advice). A Sandbox JWS starts at the sandbox host. */
async function askWithFallback(ask: AskHost, firstHost: string, allowSandbox: boolean, proof: Proof, token: string, fetcher: Fetcher): Promise<Response> {
  const first = await ask(firstHost, proof, token, fetcher);
  return firstHost === PRODUCTION_HOST && first.status === NOT_FOUND && allowSandbox ? ask(SANDBOX_HOST, proof, token, fetcher) : first;
}

async function checkSubscription(proof: Proof, token: string, allowSandbox: boolean, firstHost: string, fetcher: Fetcher): Promise<CheckResult> {
  return subscriptionResultOf(await askWithFallback(askSubscriptionHost, firstHost, allowSandbox, proof, token, fetcher), allowSandbox);
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

async function checkSubscriptionStatus(proof: Proof, token: string, allowSandbox: boolean, firstHost: string, fetcher: Fetcher): Promise<SubscriptionCheck> {
  const response = await askWithFallback(askSubscriptionHost, firstHost, allowSandbox, proof, token, fetcher);
  if (response.status !== 200) return CANNOT_VERIFY_SUBSCRIPTION; // a 404 or 400 is not "expired" (the transaction may not have propagated yet): the app keeps its record; only the status values say expired
  return checkedSubscription(bestStatus((await response.json()) as { data?: { lastTransactions?: StatusLast[] }[] }, allowSandbox, Date.now()));
}

// Slice 390: a build with StoreKit 2 sends the transaction's signed JWS as the token; an older build sends the transaction id. A JWS is verified against Apple's root first
// (apple-jws.ts); the checks below then run on the transaction id inside it (the original one for a subscription, whose statuses answer covers every renewal), so Apple's
// answer about revocation and the plan stays the last word. A token that is not a JWS is a transaction id from a build before StoreKit 2 and goes through unchanged.
// A verified Sandbox JWS (TestFlight and App Review buys are Sandbox) is accepted only when the Worker allows sandbox (APPLE_IAP_ALLOW_SANDBOX), the same rule as a plain transaction id: a production Worker refuses it, so a sandbox tester never buys free.
const JWS_PREFIX = "eyJ";
const NOT_A_PROOF = "not-a-proof";
const UNVERIFIED = "unverified";

interface Resolved {
  readonly proof: Proof;
  /** The environment the verified JWS names; null for a plain transaction id. */
  readonly environment: string | null;
}

function isSignedTransaction(token: string): boolean {
  return token.startsWith(JWS_PREFIX) && isJws(token);
}

/** The caller's own checks on a verified payload: this app, one of its products, and the product the proof names. */
function isOurs(payload: Record<string, unknown>, productId: string): boolean {
  const own = productId.startsWith(BOOK_PRODUCT_PREFIX) || isSubscriptionProduct(productId);
  return payload["bundleId"] === APP_PACKAGE_ID && own && payload["productId"] === productId;
}

function idOf(payload: Record<string, unknown>, name: string): string | null {
  const value = payload[name];
  return typeof value === "string" && /^\d+$/.test(value) ? value : null;
}

async function transactionIdOf(proof: Proof, roots: readonly string[] | undefined): Promise<Resolved | null | typeof UNVERIFIED> {
  const payload = await verifyAppleSignedPayload(proof.token, roots === undefined ? {} : { roots });
  if (payload === null) return UNVERIFIED; // clock skew, a chain change or a malformed header is not proof of a forgery: never cached as "not owned"
  if (!isOurs(payload, proof.productId)) return null;
  const environment = payload["environment"];
  if (environment !== ENVIRONMENT_PRODUCTION && environment !== ENVIRONMENT_SANDBOX) return null;
  const id = idOf(payload, isSubscriptionProduct(proof.productId) ? "originalTransactionId" : "transactionId");
  return id === null ? null : { proof: { ...proof, token: id }, environment };
}

/** The proof to check with Apple: the same one for a transaction id, one carrying the verified transaction id and environment for a JWS, or NOT_A_PROOF for a JWS that does not verify. */
async function resolved(proof: Proof, roots: readonly string[] | undefined, allowSandbox: boolean): Promise<Resolved | typeof NOT_A_PROOF | typeof UNVERIFIED> {
  if (!isSignedTransaction(proof.token)) return { proof, environment: null };
  const found = await transactionIdOf(proof, roots);
  if (found === null) return NOT_A_PROOF;
  if (found === UNVERIFIED) return UNVERIFIED;
  return found.environment === ENVIRONMENT_SANDBOX && !allowSandbox ? UNVERIFIED : found; // a verified Sandbox JWS on a Worker without the switch is not "owns nothing": it is not cached, so App Review and TestFlight buys unlock once the Worker allows Sandbox
}

async function checkOwned(found: Resolved, env: EntitlementEnv, key: AppleKey, fetcher: Fetcher): Promise<CheckResult> {
  const { proof, environment } = found;
  const sandboxJws = environment === ENVIRONMENT_SANDBOX;
  const allowSandbox = sandboxAllowed(env);
  const firstHost = sandboxJws ? SANDBOX_HOST : PRODUCTION_HOST;
  const token = await bearer(key);
  if (isSubscriptionProduct(proof.productId)) return withEnvironment(await checkSubscription(proof, token, allowSandbox, firstHost, fetcher), environment);
  const response = await askWithFallback(askHost, firstHost, allowSandbox, proof, token, fetcher);
  return withEnvironment(await resultOf(response, proof.productId, allowSandbox), environment);
}

function withEnvironment(result: CheckResult, environment: string | null): CheckResult {
  return environment === null || result.verdict !== Verdict.Checked ? result : { ...result, environment };
}

/** The Apple checker; `roots` replaces Apple Root CA - G3 as the trusted root fingerprints, for tests only (production code calls it with nothing). */
export function createAppleChecker(roots?: readonly string[]): StoreChecker {
  return {
    async check(sent, env, fetcher) {
      const key = keyOf(env);
      if (key === null) return CANNOT_VERIFY;
      try {
        const found = await resolved(sent, roots, sandboxAllowed(env));
        if (found === UNVERIFIED) return CANNOT_VERIFY; // a JWS that fails verification is not cached as "not owned" (same as the subscription path)
        if (found === NOT_A_PROOF) return checked([]); // a verified transaction that is not ours, not for this product or Sandbox on a production Worker owns nothing
        return await checkOwned(found, env, key, fetcher);
      } catch {
        return CANNOT_VERIFY; // no-fallback: network or signing failure: we could not ask, which is never "owned"
      }
    },
    async subscription(sent, env, fetcher) {
      const key = keyOf(env);
      if (key === null) return CANNOT_VERIFY_SUBSCRIPTION;
      try {
        const found = await resolved(sent, roots, sandboxAllowed(env));
        if (found === NOT_A_PROOF || found === UNVERIFIED) return CANNOT_VERIFY_SUBSCRIPTION; // a JWS that does not verify says nothing about the plan: the app keeps its record, never reads "expired"
        const firstHost = found.environment === ENVIRONMENT_SANDBOX ? SANDBOX_HOST : PRODUCTION_HOST;
        return await checkSubscriptionStatus(found.proof, await bearer(key), sandboxAllowed(env), firstHost, fetcher);
      } catch {
        return CANNOT_VERIFY_SUBSCRIPTION; // no-fallback: we could not ask, which the app reads as "keep what you hold", never as expired
      }
    },
  };
}

export const appleChecker: StoreChecker = createAppleChecker();

/** Test seam: forgets the cached token. */
export function resetAppleTokenCache(): void {
  cachedToken = null;
}
