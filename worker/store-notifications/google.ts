// Slice 458: Play Real-time Developer Notifications through a Pub/Sub push (POST /api/store/google).
// 1. The push is believed only with its Google-signed OIDC token: RS256 against Google's published keys, issuer accounts.google.com, our audience, the push service account's email, not expired.
// 2. The notification names a purchase token; the purchase is then read (purchases.subscriptionsv2.get, purchases.products.get) to learn the offer (trial or paid), the region and whether it is a test.
// The token never leaves this file: it is not stored, logged or put in a row.
import { base64UrlDecode, signJwt } from "../entitlements/jwt.ts";
import type { Fetcher } from "../entitlements/types.ts";
import { APP_PACKAGE_ID, isSubscriptionProduct, SUBSCRIPTION_MONTHLY_PRODUCT, SUBSCRIPTION_YEARLY_PRODUCT } from "../products.ts";
import { PLAN_MONTHLY_SOURCE, PLAN_YEARLY_SOURCE } from "../events.ts";
import { IGNORE, NO_PLAN, REFUSE, RETRY } from "./types.ts";
import type { BookResolver, Outcome, StoreCount } from "./types.ts";

const PLATFORM = "android";
const NO_BOOK = "none";
const NO_COUNTRY = "none";
const JWKS_URL = "https://www.googleapis.com/oauth2/v3/certs";
const TOKEN_URL = "https://oauth2.googleapis.com/token";
const SCOPE = "https://www.googleapis.com/auth/androidpublisher";
const API = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${APP_PACKAGE_ID}/purchases`;
const ISSUERS = ["accounts.google.com", "https://accounts.google.com"];
const JWT_BEARER = "urn:ietf:params:oauth:grant-type:jwt-bearer";
const CLOCK_SKEW_S = 60;
const KEYS_REUSE_MS = 60 * 60_000;
const KEYS_REFETCH_COOLDOWN_MS = 60_000;
const TOKEN_REUSE_MS = 50 * 60_000;
const REGION_PATTERN = /^[A-Z]{2}$/;
const FIRST_RENEWAL_SUFFIX = "..0";
const FREE_TRIAL_PHASE = "freeTrial";
const INTRO_PRICE_PHASE = "introductoryPrice";

/** What the Worker needs to verify the push and ask Play: the push's audience and service account (wrangler vars, set by item 4) and the Play service account that reads purchases (a secret, already on the Worker). */
export interface GoogleStoreEnv {
  readonly STORE_GOOGLE_AUDIENCE?: string;
  readonly STORE_GOOGLE_PUSH_EMAIL?: string;
  readonly PLAY_SERVICE_ACCOUNT_JSON?: string;
}

// ---- 1. the push's OIDC token ----------------------------------------------------------------------------------------------

let cachedKeys: { readonly keys: readonly JsonWebKey[]; readonly at: number; readonly reuseMs: number } | null = null;
let lastKeyFetchAt = 0; // every attempt, failed or not: the cooldown that bounds outbound fetches to Google

/** The cache lifetime the JWKS response asks for (`Cache-Control: max-age`), clamped; the default when it names none. */
function reuseMsOf(response: Response): number {
  const match = /max-age=(\d+)/.exec(response.headers.get("Cache-Control") ?? "");
  const asked = match === null ? KEYS_REUSE_MS : Number(match[1]) * 1000;
  return Math.min(Math.max(asked, KEYS_REFETCH_COOLDOWN_MS), KEYS_REUSE_MS);
}

async function fetchKeys(fetcher: Fetcher): Promise<readonly JsonWebKey[]> {
  lastKeyFetchAt = Date.now();
  const response = await fetcher(JWKS_URL);
  if (!response.ok) return cachedKeys?.keys ?? [];
  const body = (await response.json()) as { keys?: JsonWebKey[] };
  const keys = Array.isArray(body.keys) ? body.keys : [];
  cachedKeys = { keys, at: Date.now(), reuseMs: reuseMsOf(response) };
  return keys;
}

const keyWithId = (keys: readonly JsonWebKey[], kid: unknown): JsonWebKey | undefined => keys.find((candidate) => (candidate as { kid?: string }).kid === kid);

/** The signing key named by `kid`: from the cache while it is fresh; on a miss (Google rotated its keys) one refetch, at most once per cooldown. */
async function googleKey(fetcher: Fetcher, kid: unknown): Promise<JsonWebKey | undefined> {
  const fresh = cachedKeys !== null && Date.now() - cachedKeys.at < cachedKeys.reuseMs;
  const held = fresh ? keyWithId(cachedKeys!.keys, kid) : undefined;
  if (held !== undefined) return held;
  if (Date.now() - lastKeyFetchAt < KEYS_REFETCH_COOLDOWN_MS) return undefined;
  return keyWithId(await fetchKeys(fetcher), kid);
}

/** Test seam: forgets the cached Google keys and access token. */
export function resetGoogleStoreCaches(): void {
  cachedKeys = null;
  cachedAccess = null;
  lastKeyFetchAt = 0;
}

function decodeObject(part: string): Record<string, unknown> | null {
  const parsed: unknown = JSON.parse(new TextDecoder().decode(base64UrlDecode(part)));
  return typeof parsed === "object" && parsed !== null && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : null;
}

async function signatureHolds(parts: string[], key: JsonWebKey): Promise<boolean> {
  const imported = await crypto.subtle.importKey("jwk", key, { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" }, false, ["verify"]);
  return crypto.subtle.verify("RSASSA-PKCS1-v1_5", imported, base64UrlDecode(parts[2]!), new TextEncoder().encode(`${parts[0]}.${parts[1]}`));
}

function claimsHold(claims: Record<string, unknown>, env: GoogleStoreEnv, nowS: number): boolean {
  const exp = claims["exp"];
  return (
    typeof claims["iss"] === "string" && ISSUERS.includes(claims["iss"]) &&
    env.STORE_GOOGLE_AUDIENCE !== undefined && env.STORE_GOOGLE_AUDIENCE !== "" && claims["aud"] === env.STORE_GOOGLE_AUDIENCE &&
    env.STORE_GOOGLE_PUSH_EMAIL !== undefined && env.STORE_GOOGLE_PUSH_EMAIL !== "" && claims["email"] === env.STORE_GOOGLE_PUSH_EMAIL && claims["email_verified"] === true &&
    typeof exp === "number" && exp + CLOCK_SKEW_S >= nowS
  );
}

/** True when the Authorization header carries a Google-signed OIDC token for this Worker's audience and the configured push service account. Fails closed: an unset audience or email refuses everything. */
export async function verifyPushToken(authorization: string | null, env: GoogleStoreEnv, fetcher: Fetcher, nowS: number = Math.floor(Date.now() / 1000)): Promise<boolean> {
  const token = authorization !== null && authorization.startsWith("Bearer ") ? authorization.slice("Bearer ".length) : "";
  const parts = token.split(".");
  if (parts.length !== 3) return false;
  try {
    const header = decodeObject(parts[0]!);
    const claims = decodeObject(parts[1]!);
    if (header === null || claims === null || header["alg"] !== "RS256" || !claimsHold(claims, env, nowS)) return false;
    const key = await googleKey(fetcher, header["kid"]);
    return key !== undefined && (await signatureHolds(parts, key));
  } catch {
    return false; // no-fallback: a token we cannot read or verify is refused, never believed
  }
}

// ---- 2. the purchase, read from Play ---------------------------------------------------------------------------------------

let cachedAccess: { readonly value: string; readonly at: number } | null = null;

async function accessToken(env: GoogleStoreEnv, fetcher: Fetcher): Promise<string | null> {
  if (cachedAccess !== null && Date.now() - cachedAccess.at < TOKEN_REUSE_MS) return cachedAccess.value;
  const account = JSON.parse(env.PLAY_SERVICE_ACCOUNT_JSON ?? "{}") as { client_email?: string; private_key?: string };
  if (typeof account.client_email !== "string" || typeof account.private_key !== "string") return null;
  const now = Math.floor(Date.now() / 1000);
  const assertion = await signJwt("RS256", {}, { iss: account.client_email, scope: SCOPE, aud: TOKEN_URL, iat: now, exp: now + 3600 }, account.private_key);
  const response = await fetcher(TOKEN_URL, { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body: new URLSearchParams({ grant_type: JWT_BEARER, assertion }).toString() });
  const body = response.ok ? ((await response.json()) as { access_token?: unknown }) : {};
  if (typeof body.access_token !== "string") return null;
  cachedAccess = { value: body.access_token, at: Date.now() };
  return body.access_token;
}

type PlayRead = { readonly status: "ok"; readonly body: Record<string, unknown> } | { readonly status: "gone" } | { readonly status: "retry" };

const PLAY_NOT_ALLOWED = [401, 403];
const PLAY_GONE = [400, 404, 410];

/** A 401 or 403 from Play is a configuration fault (the service account has no access to the app): retrying cannot fix it, so it is logged and the delivery is dropped. 5xx and 429 stay retryable. */
function playRefused(status: number): PlayRead {
  console.error(`store-notifications: Play answered ${status}; the Play service account needs access to the app in the Play Console`);
  return { status: "gone" };
}

async function playGet(url: string, env: GoogleStoreEnv, fetcher: Fetcher): Promise<PlayRead> {
  try {
    const token = await accessToken(env, fetcher);
    if (token === null) return { status: "retry" };
    const response = await fetcher(url, { headers: { Authorization: `Bearer ${token}`, Accept: "application/json" } });
    if (response.status === 200) return { status: "ok", body: (await response.json()) as Record<string, unknown> };
    if (PLAY_NOT_ALLOWED.includes(response.status)) return playRefused(response.status);
    return PLAY_GONE.includes(response.status) ? { status: "gone" } : { status: "retry" };
  } catch {
    return { status: "retry" }; // no-fallback: Play could not be asked; Pub/Sub redelivers
  }
}

const asText = (value: unknown): string => (typeof value === "string" ? value : "");
const asRecord = (value: unknown): Record<string, unknown> | null => (typeof value === "object" && value !== null && !Array.isArray(value) ? (value as Record<string, unknown>) : null);

/** Which plan a Play subscription line item is: by its base plan id (`monthly`, `yearly`), else by the product id, else `none`. */
export function planOfLineItem(item: Record<string, unknown> | null): string {
  const basePlan = asText(asRecord(item?.["offerDetails"])?.["basePlanId"]).toLowerCase();
  const productId = asText(item?.["productId"]);
  if (basePlan.includes("month")) return PLAN_MONTHLY_SOURCE;
  if (basePlan.includes("year") || basePlan.includes("annual")) return PLAN_YEARLY_SOURCE;
  if (productId === SUBSCRIPTION_MONTHLY_PRODUCT) return PLAN_MONTHLY_SOURCE;
  return productId === SUBSCRIPTION_YEARLY_PRODUCT ? PLAN_YEARLY_SOURCE : NO_PLAN;
}

/** The subscription notification types (Play's SubscriptionNotificationType numbers) we count, mapped to a kind; PURCHASED and RENEWED are decided by the purchase (trial or paid, first renewal). */
const SUBSCRIPTION_KINDS: Readonly<Record<number, string>> = { 3: "cancelled", 5: "billing_retry", 6: "grace", 12: "refunded", 13: "expired" };
const PURCHASED = 4;
const RENEWED = 2;
const RECOVERED = 1;
const RESTARTED = 7;

function inPhase(item: Record<string, unknown> | null, phase: string): boolean {
  return asRecord(item?.["offerPhase"])?.[phase] !== undefined;
}

const hasOffer = (item: Record<string, unknown> | null): boolean => asText(asRecord(item?.["offerDetails"])?.["offerId"]) !== "";
const orderNumberedRenewal = (body: Record<string, unknown>): boolean => /\.\.\d+$/.test(asText(body["latestOrderId"]));

/** Only a free trial is not paid. A paid introductory-price offer also has an offer id, so it is told apart by its own phase. */
function inFreeTrial(item: Record<string, unknown> | null): boolean {
  return inPhase(item, FREE_TRIAL_PHASE);
}

/**
 * Whether the notified purchase began as a free trial. A push that arrives (or is retried) after the trial ended reads a line item that is already in its paid phase,
 * so a subscription on an offer (not an introductory-price one) that has already been renewed began as a trial.
 */
export function beganAsFreeTrial(body: Record<string, unknown>, item: Record<string, unknown> | null): boolean {
  return inFreeTrial(item) || (orderNumberedRenewal(body) && hasOffer(item) && !inPhase(item, INTRO_PRICE_PHASE));
}

/** A renewal that is the first paid order after a free-trial offer: Play numbers renewal orders `GPA.xxxx..0`, `..1`, and a paid introductory-price offer is not a trial to convert. */
export function isFirstPaidOrderAfterTrial(body: Record<string, unknown>, item: Record<string, unknown> | null): boolean {
  return asText(body["latestOrderId"]).endsWith(FIRST_RENEWAL_SUFFIX) && hasOffer(item) && !inFreeTrial(item) && !inPhase(item, INTRO_PRICE_PHASE);
}

function subscriptionKind(type: number, body: Record<string, unknown>, item: Record<string, unknown> | null): { kind: string; offer: string } | null {
  if (type === PURCHASED) return beganAsFreeTrial(body, item) ? { kind: "trial_started", offer: "trial" } : { kind: "subscribed", offer: "paid" };
  if (type === RENEWED) return { kind: isFirstPaidOrderAfterTrial(body, item) ? "converted" : "renewed", offer: "paid" };
  if (type === RECOVERED || type === RESTARTED) return null; // not a billing renewal (the RENEWED push that follows is the one counted), and the closed list has no kind for them
  const kind = SUBSCRIPTION_KINDS[type];
  return kind === undefined ? null : { kind, offer: inFreeTrial(item) ? "trial" : "paid" };
}

function regionOf(value: unknown): string {
  return typeof value === "string" && REGION_PATTERN.test(value) ? value : NO_COUNTRY;
}

function lineItemFor(body: Record<string, unknown>, subscriptionId: string): Record<string, unknown> | null {
  const items = Array.isArray(body["lineItems"]) ? (body["lineItems"] as unknown[]).map(asRecord) : [];
  return items.find((item) => item?.["productId"] === subscriptionId) ?? items[0] ?? null;
}

async function subscriptionOutcome(type: number, token: string, subscriptionId: string, dedupKey: string, env: GoogleStoreEnv, fetcher: Fetcher): Promise<Outcome> {
  if (!isSubscriptionProduct(subscriptionId)) return IGNORE;
  const read = await playGet(`${API}/subscriptionsv2/tokens/${encodeURIComponent(token)}`, env, fetcher);
  if (read.status !== "ok") return read.status === "gone" ? IGNORE : RETRY;
  const item = lineItemFor(read.body, subscriptionId);
  const decided = subscriptionKind(type, read.body, item);
  if (decided === null) return IGNORE;
  return { type: "count", dedupKey, count: { platform: PLATFORM, ...decided, source: planOfLineItem(item), book: NO_BOOK, country: regionOf(read.body["regionCode"]), test: read.body["testPurchase"] !== undefined } };
}

const ONE_TIME_PURCHASED = 1;
const ONE_TIME_CANCELED = 2;
const TEST_PURCHASE_TYPE = 0;

async function bookOutcome(type: number, token: string, sku: string, dedupKey: string, env: GoogleStoreEnv, fetcher: Fetcher, books: BookResolver): Promise<Outcome> {
  const kind = type === ONE_TIME_PURCHASED ? "book_bought" : type === ONE_TIME_CANCELED ? "book_refunded" : null;
  const book = kind === null ? null : await books(sku);
  if (kind === null || book === null) return IGNORE;
  const read = await playGet(`${API}/products/${encodeURIComponent(sku)}/tokens/${encodeURIComponent(token)}`, env, fetcher);
  if (read.status !== "ok") return read.status === "gone" ? IGNORE : RETRY;
  const count: StoreCount = { platform: PLATFORM, kind, offer: "paid", source: "shelf", book, country: regionOf(read.body["regionCode"]), test: read.body["purchaseType"] === TEST_PURCHASE_TYPE };
  return { type: "count", dedupKey, count };
}

/** A voided subscription is a refund; a voided one-time purchase names no sku, so it cannot be tied to a book and is dropped. */
async function voidedOutcome(voided: Record<string, unknown>, dedupKey: string, env: GoogleStoreEnv, fetcher: Fetcher): Promise<Outcome> {
  const SUBSCRIPTION_PRODUCT_TYPE = 1;
  if (voided["productType"] !== SUBSCRIPTION_PRODUCT_TYPE) return IGNORE;
  const read = await playGet(`${API}/subscriptionsv2/tokens/${encodeURIComponent(asText(voided["purchaseToken"]))}`, env, fetcher);
  if (read.status !== "ok") return read.status === "gone" ? IGNORE : RETRY;
  const item = lineItemFor(read.body, "");
  if (!isSubscriptionProduct(asText(item?.["productId"]))) return IGNORE;
  return { type: "count", dedupKey, count: { platform: PLATFORM, kind: "refunded", offer: "paid", source: planOfLineItem(item), book: NO_BOOK, country: regionOf(read.body["regionCode"]), test: read.body["testPurchase"] !== undefined } };
}

function developerNotification(body: unknown): Record<string, unknown> | null {
  const data = asText(asRecord(asRecord(body)?.["message"])?.["data"]);
  if (data === "") return null;
  try {
    const parsed = asRecord(JSON.parse(new TextDecoder().decode(base64UrlDecode(data))));
    return parsed !== null && parsed["packageName"] === APP_PACKAGE_ID ? parsed : null;
  } catch {
    return null;
  }
}

/** Decides what one Pub/Sub push is worth. The caller has already verified the push's token. A body that is not a notification for our package is refused; a test ping and kinds we do not count are dropped. */
export async function decideGoogle(body: unknown, env: GoogleStoreEnv, fetcher: Fetcher, books: BookResolver): Promise<Outcome> {
  const notification = developerNotification(body);
  if (notification === null) return REFUSE;
  const dedupKey = `google:${asText(asRecord(asRecord(body)?.["message"])?.["messageId"])}`;
  const sub = asRecord(notification["subscriptionNotification"]);
  if (sub !== null) return subscriptionOutcome(Number(sub["notificationType"]), asText(sub["purchaseToken"]), asText(sub["subscriptionId"]), dedupKey, env, fetcher);
  const one = asRecord(notification["oneTimeProductNotification"]);
  if (one !== null) return bookOutcome(Number(one["notificationType"]), asText(one["purchaseToken"]), asText(one["sku"]), dedupKey, env, fetcher, books);
  const voided = asRecord(notification["voidedPurchaseNotification"]);
  return voided !== null ? voidedOutcome(voided, dedupKey, env, fetcher) : IGNORE;
}
