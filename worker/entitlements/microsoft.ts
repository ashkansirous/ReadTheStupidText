// Microsoft Store: the collections API (POST /v6.0/collections/query) with an Entra ID token for audience https://onestore.microsoft.com.
// The proof token is the Store ID key the app made with StoreContext.GetCustomerCollectionsIdAsync. Each Active durable add-on's
// `inAppOfferToken` is the product id the developer gave it in Partner Center, which is our product id, so the answer is the whole
// list the customer owns. (learn.microsoft.com/windows/uwp/monetize/query-for-products)
import { CANNOT_VERIFY, checked } from "./types.ts";
import type { CheckResult, EntitlementEnv, Fetcher, Proof, StoreChecker } from "./types.ts";

const COLLECTIONS_URL = "https://collections.mp.microsoft.com/v6.0/collections/query";
// Two audiences (learn.microsoft.com/windows/uwp/monetize/view-and-grant-products-from-a-service, step 3): the first token never leaves the Worker
// (it authorises the collections query); the second is the one the docs say to hand to the client to make a Store ID key.
const QUERY_AUDIENCE = "https://onestore.microsoft.com";
const TICKET_AUDIENCE = "https://onestore.microsoft.com/b2b/keys/create/collections";
const TICKET_REFERENCE = "dino";
const PRODUCT_TYPE_DURABLE = "Durable";
const STATUS_ACTIVE = "Active";
const TOKEN_REUSE_MS = 40 * 60_000;
const MAX_PAGE_SIZE = 100;

const cachedTokens = new Map<string, { readonly value: string; readonly at: number; readonly clientId: string }>();

interface MicrosoftApp {
  readonly tenant: string;
  readonly clientId: string;
  readonly secret: string;
}

interface CollectionItem {
  readonly inAppOfferToken?: unknown;
  readonly productType?: unknown;
  readonly status?: unknown;
}

function appOf(env: EntitlementEnv): MicrosoftApp | null {
  const { MS_STORE_TENANT_ID: tenant, MS_STORE_CLIENT_ID: clientId, MS_STORE_CLIENT_SECRET: secret } = env;
  return tenant && clientId && secret ? { tenant, clientId, secret } : null;
}

/** A short non-reversible fingerprint of the client secret (the secret itself is never kept), so a rotated secret under the same client id does not reuse the old token. */
async function secretFingerprint(secret: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(secret));
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, "0")).join("");
}

async function accessToken(app: MicrosoftApp, fetcher: Fetcher, audience: string): Promise<string | null> {
  const credential = `${app.clientId}:${await secretFingerprint(app.secret)}`;
  const cached = cachedTokens.get(audience);
  if (cached !== undefined && cached.clientId === credential && Date.now() - cached.at < TOKEN_REUSE_MS) return cached.value;
  const body = new URLSearchParams({ grant_type: "client_credentials", client_id: app.clientId, client_secret: app.secret, resource: audience }).toString();
  const response = await fetcher(`https://login.microsoftonline.com/${encodeURIComponent(app.tenant)}/oauth2/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body,
  });
  if (!response.ok) return null;
  const json = (await response.json()) as { access_token?: unknown };
  if (typeof json.access_token !== "string") return null;
  cachedTokens.set(audience, { value: json.access_token, at: Date.now(), clientId: credential });
  return json.access_token;
}

/**
 * The Entra ID access token (audience https://onestore.microsoft.com/b2b/keys/create/collections, never the query audience, which stays in the Worker) the Windows app passes to `StoreContext.GetCustomerCollectionsIdAsync` to make a Store ID key (Microsoft's documented flow:
 * the service issues the ticket, the client turns it into a key, the service reads the key). Null while a Microsoft key is missing or Microsoft would not answer.
 */
export async function serviceTicket(env: EntitlementEnv, fetcher: Fetcher): Promise<string | null> {
  const app = appOf(env);
  if (app === null) return null;
  try {
    return await accessToken(app, fetcher, TICKET_AUDIENCE);
  } catch {
    return null;
  }
}

export function ownedFromItems(items: readonly CollectionItem[]): string[] {
  const active = items.filter((item) => item.productType === PRODUCT_TYPE_DURABLE && item.status === STATUS_ACTIVE);
  return active.map((item) => item.inAppOfferToken).filter((token): token is string => typeof token === "string" && token !== "");
}

async function query(proof: Proof, token: string, fetcher: Fetcher): Promise<CheckResult> {
  const body = {
    maxPageSize: MAX_PAGE_SIZE,
    beneficiaries: [{ localTicketReference: TICKET_REFERENCE, identityValue: proof.token, identityType: "b2b" }],
    productTypes: [PRODUCT_TYPE_DURABLE],
    validityType: "Valid",
  };
  const response = await fetcher(COLLECTIONS_URL, { method: "POST", headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" }, body: JSON.stringify(body) });
  if (response.status === 400) return checked([]); // a Store ID key the service cannot read is not a purchase
  if (response.status !== 200) return CANNOT_VERIFY;
  const json = (await response.json()) as { items?: unknown };
  return checked(Array.isArray(json.items) ? ownedFromItems(json.items as CollectionItem[]) : []);
}

export const microsoftChecker: StoreChecker = {
  async check(proof, env, fetcher) {
    const app = appOf(env);
    if (app === null) return CANNOT_VERIFY;
    try {
      const token = await accessToken(app, fetcher, QUERY_AUDIENCE);
      return token === null ? CANNOT_VERIFY : await query(proof, token, fetcher);
    } catch {
      return CANNOT_VERIFY; // network failure: we could not ask, which is never "owned"
    }
  },
};

/** Test seam: forgets the cached token. */
export function resetMicrosoftTokenCache(): void {
  cachedTokens.clear();
}
