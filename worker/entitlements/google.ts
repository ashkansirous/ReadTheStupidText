// Google Play: purchases.products.get (androidpublisher v3) with a service account. A purchase counts only in state PURCHASED (0).
import { APP_PACKAGE_ID } from "../products.ts";
import { signJwt } from "./jwt.ts";
import { CANNOT_VERIFY, checked } from "./types.ts";
import type { CheckResult, EntitlementEnv, Fetcher, Proof, StoreChecker } from "./types.ts";

const TOKEN_URL = "https://oauth2.googleapis.com/token";
const PRODUCTS_URL = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${APP_PACKAGE_ID}/purchases/products`;
const SCOPE = "https://www.googleapis.com/auth/androidpublisher";
const JWT_BEARER = "urn:ietf:params:oauth:grant-type:jwt-bearer";
const PURCHASED = 0;
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

async function ask(proof: Proof, token: string, fetcher: Fetcher): Promise<CheckResult> {
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
};

/** Test seam: forgets the cached access token. */
export function resetGoogleTokenCache(): void {
  cachedToken = null;
}
