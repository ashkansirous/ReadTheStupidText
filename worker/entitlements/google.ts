// Google Play: purchases.products.get (androidpublisher v3) with a service account. A purchase counts only in state PURCHASED (0).
// A subscription proof (Slice 384) is read with purchases.subscriptionsv2.get instead, which carries the state and each line item's expiryTime.
import { APP_PACKAGE_ID, isSubscriptionProduct } from "../products.ts";
import { signJwt } from "./jwt.ts";
import { CANNOT_VERIFY, CANNOT_VERIFY_SUBSCRIPTION, NO_SUBSCRIPTION, SubscriptionState, checked, checkedSubscription } from "./types.ts";
import type { CheckResult, EntitlementEnv, Fetcher, Proof, StoreChecker, SubscriptionCheck, SubscriptionStatus } from "./types.ts";

const TOKEN_URL = "https://oauth2.googleapis.com/token";
const PRODUCTS_URL = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${APP_PACKAGE_ID}/purchases/products`;
const SUBSCRIPTIONS_URL = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${APP_PACKAGE_ID}/purchases/subscriptionsv2/tokens`;
const SCOPE = "https://www.googleapis.com/auth/androidpublisher";
const JWT_BEARER = "urn:ietf:params:oauth:grant-type:jwt-bearer";
const PURCHASED = 0;
const GONE = 410;
const ASSERTION_LIFETIME_S = 3600;
const TOKEN_REUSE_MS = 50 * 60_000;

interface ServiceAccount {
  readonly client_email: string;
  readonly private_key: string;
}

let cachedToken: { readonly value: string; readonly at: number; readonly account: string } | null = null;

function serviceAccountOf(env: EntitlementEnv): ServiceAccount | null {
  if (env.PLAY_SERVICE_ACCOUNT_JSON === undefined || env.PLAY_SERVICE_ACCOUNT_JSON === "") return null;
  try {
    const parsed = JSON.parse(env.PLAY_SERVICE_ACCOUNT_JSON) as Partial<ServiceAccount>;
    return typeof parsed.client_email === "string" && typeof parsed.private_key === "string" ? { client_email: parsed.client_email, private_key: parsed.private_key } : null;
  } catch {
    return null;
  }
}

async function accessToken(account: ServiceAccount, fetcher: Fetcher): Promise<string | null> {
  if (cachedToken !== null && cachedToken.account === account.client_email && Date.now() - cachedToken.at < TOKEN_REUSE_MS) return cachedToken.value;
  const now = Math.floor(Date.now() / 1000);
  const assertion = await signJwt("RS256", {}, { iss: account.client_email, scope: SCOPE, aud: TOKEN_URL, iat: now, exp: now + ASSERTION_LIFETIME_S }, account.private_key);
  const body = new URLSearchParams({ grant_type: JWT_BEARER, assertion }).toString();
  const response = await fetcher(TOKEN_URL, { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body });
  if (!response.ok) return null;
  const json = (await response.json()) as { access_token?: unknown };
  if (typeof json.access_token !== "string") return null;
  cachedToken = { value: json.access_token, at: Date.now(), account: account.client_email };
  return json.access_token;
}

// 400/404/410 is Google saying this token is not a purchase of this product; anything else unexpected means we could not ask.
function resultOf(status: number, body: { purchaseState?: unknown }, productId: string): CheckResult {
  if (status === 200) return checked(body.purchaseState === PURCHASED ? [productId] : []);
  return status === 400 || status === 404 || status === 410 ? checked([]) : CANNOT_VERIFY;
}

/** Slice 384: a subscription counts while Play says it is active, cancelled-but-not-expired or in its grace period, and its line item for our product has not expired. */
const SUBSCRIPTION_ENTITLED_STATES: readonly string[] = ["SUBSCRIPTION_STATE_ACTIVE", "SUBSCRIPTION_STATE_CANCELED", "SUBSCRIPTION_STATE_IN_GRACE_PERIOD"];

interface SubscriptionBody {
  readonly subscriptionState?: unknown;
  readonly lineItems?: unknown;
}

const GRACE_STATE = "SUBSCRIPTION_STATE_IN_GRACE_PERIOD";

// Google (purchases.subscriptionsv2, SubscriptionState; checked with context7 /websites/developers_google_android-publisher): IN_GRACE_PERIOD means the renewal payment was declined but the user keeps access, and
// lineItems.expiryTime is still the missed renewal date, so a grace subscription is decided by its state, not by that date. ON_HOLD (suspended) and PAUSED do not keep access; ACTIVE and CANCELED are
// entitled only while the line item has not expired.
function subscriptionOwns(body: SubscriptionBody, productId: string, now: number): boolean {
  if (typeof body.subscriptionState !== "string" || !SUBSCRIPTION_ENTITLED_STATES.includes(body.subscriptionState)) return false;
  const items = Array.isArray(body.lineItems) ? (body.lineItems as { productId?: unknown; expiryTime?: unknown }[]) : [];
  const ours = items.filter((item) => item.productId === productId);
  if (body.subscriptionState === GRACE_STATE) return ours.length > 0;
  return ours.some((item) => typeof item.expiryTime === "string" && Date.parse(item.expiryTime) > now);
}

/** Slice 384 (finding 2): the state and end date the app shows. A running (active or cancelled) or grace subscription whose line item has not expired keeps its end date; anything else is expired. */
function subscriptionStatusOf(body: SubscriptionBody, productId: string, now: number): SubscriptionStatus {
  const items = Array.isArray(body.lineItems) ? (body.lineItems as { productId?: unknown; expiryTime?: unknown }[]) : [];
  const expiry = items.find((item) => item.productId === productId && typeof item.expiryTime === "string")?.expiryTime as string | undefined;
  if (subscriptionOwns(body, productId, now) && body.subscriptionState === GRACE_STATE && (expiry === undefined || Number.isNaN(Date.parse(expiry)))) {
    return { state: SubscriptionState.Grace, expiresAt: new Date(now).toISOString() }; // the gate serves a grace line item with no usable expiryTime, so the status route agrees: still in grace now
  }
  if (expiry === undefined || Number.isNaN(Date.parse(expiry))) return NO_SUBSCRIPTION;
  if (!subscriptionOwns(body, productId, now)) return { state: SubscriptionState.Expired, expiresAt: expiry };
  if (body.subscriptionState === GRACE_STATE) return { state: SubscriptionState.Grace, expiresAt: Date.parse(expiry) > now ? expiry : new Date(now).toISOString() }; // Play gives no grace end (InGracePeriodStateContext has only the reason), so a past expiry reads as "still in grace now"
  return { state: SubscriptionState.Active, expiresAt: expiry };
}

async function askSubscriptionStatus(proof: Proof, token: string, fetcher: Fetcher): Promise<SubscriptionCheck> {
  const url = `${SUBSCRIPTIONS_URL}/${encodeURIComponent(proof.token)}`;
  const response = await fetcher(url, { headers: { Authorization: `Bearer ${token}`, Accept: "application/json" } });
  if (response.status !== 200) return response.status === GONE ? checkedSubscription(NO_SUBSCRIPTION) : CANNOT_VERIFY_SUBSCRIPTION; // only 410 is "expired"; a 400 or 404 (not yet propagated, a bad token) keeps the app's record
  return checkedSubscription(subscriptionStatusOf((await response.json()) as SubscriptionBody, proof.productId, Date.now()));
}

async function askSubscription(proof: Proof, token: string, fetcher: Fetcher): Promise<CheckResult> {
  const url = `${SUBSCRIPTIONS_URL}/${encodeURIComponent(proof.token)}`;
  const response = await fetcher(url, { headers: { Authorization: `Bearer ${token}`, Accept: "application/json" } });
  if (response.status !== 200) return response.status === 400 || response.status === 404 || response.status === 410 ? checked([]) : CANNOT_VERIFY;
  const body = (await response.json()) as SubscriptionBody;
  return checked(subscriptionOwns(body, proof.productId, Date.now()) ? [proof.productId] : []);
}

async function ask(proof: Proof, token: string, fetcher: Fetcher): Promise<CheckResult> {
  if (isSubscriptionProduct(proof.productId)) return askSubscription(proof, token, fetcher);
  const url = `${PRODUCTS_URL}/${encodeURIComponent(proof.productId)}/tokens/${encodeURIComponent(proof.token)}`;
  const response = await fetcher(url, { headers: { Authorization: `Bearer ${token}`, Accept: "application/json" } });
  const body = response.status === 200 ? ((await response.json()) as { purchaseState?: unknown }) : {};
  return resultOf(response.status, body, proof.productId);
}

export const googleChecker: StoreChecker = {
  async check(proof, env, fetcher) {
    const account = serviceAccountOf(env);
    if (account === null) return CANNOT_VERIFY;
    try {
      const token = await accessToken(account, fetcher);
      return token === null ? CANNOT_VERIFY : await ask(proof, token, fetcher);
    } catch {
      return CANNOT_VERIFY; // network or signing failure: we could not ask, which is never "owned"
    }
  },
  async subscription(proof, env, fetcher) {
    const account = serviceAccountOf(env);
    if (account === null) return CANNOT_VERIFY_SUBSCRIPTION;
    try {
      const token = await accessToken(account, fetcher);
      return token === null ? CANNOT_VERIFY_SUBSCRIPTION : await askSubscriptionStatus(proof, token, fetcher);
    } catch {
      return CANNOT_VERIFY_SUBSCRIPTION; // no-fallback: we could not ask, which the app reads as "keep what you hold", never as expired
    }
  },
};

/** Test seam: forgets the cached access token. */
export function resetGoogleTokenCache(): void {
  cachedToken = null;
}
