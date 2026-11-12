// Microsoft Store: the collections API (POST /v6.0/collections/query) with an Entra ID token for audience https://onestore.microsoft.com.
// The proof token is the Store ID key the app made with StoreContext.GetCustomerCollectionsIdAsync. Each Active durable add-on's
// `inAppOfferToken` is the product id the developer gave it in Partner Center, which is our product id, so the answer is the whole
// list the customer owns. (learn.microsoft.com/windows/uwp/monetize/query-for-products)
import { CANNOT_VERIFY, checked } from "./types.ts";
import type { CheckResult, EntitlementEnv, Fetcher, Proof, StoreChecker } from "./types.ts";

const COLLECTIONS_URL = "https://collections.mp.microsoft.com/v6.0/collections/query";
const AUDIENCE = "https://onestore.microsoft.com";
const TICKET_REFERENCE = "dino";
const PRODUCT_TYPE_DURABLE = "Durable";
const STATUS_ACTIVE = "Active";
const TOKEN_REUSE_MS = 40 * 60_000;
const MAX_PAGE_SIZE = 100;

let cachedToken: { readonly value: string; readonly at: number; readonly clientId: string } | null = null;

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

async function accessToken(app: MicrosoftApp, fetcher: Fetcher): Promise<string | null> {
  if (cachedToken !== null && cachedToken.clientId === app.clientId && Date.now() - cachedToken.at < TOKEN_REUSE_MS) return cachedToken.value;
  const body = new URLSearchParams({ grant_type: "client_credentials", client_id: app.clientId, client_secret: app.secret, resource: AUDIENCE }).toString();
  const response = await fetcher(`https://login.microsoftonline.com/${encodeURIComponent(app.tenant)}/oauth2/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body,
  });
  if (!response.ok) return null;
  const json = (await response.json()) as { access_token?: unknown };
  if (typeof json.access_token !== "string") return null;
  cachedToken = { value: json.access_token, at: Date.now(), clientId: app.clientId };
  return json.access_token;
}

/**
 * The Entra ID access token (audience https://onestore.microsoft.com) the Windows app passes to `StoreContext.GetCustomerCollectionsIdAsync` to make a Store ID key (Microsoft's documented flow:
 * the service issues the ticket, the client turns it into a key, the service reads the key). Null while a Microsoft key is missing or Microsoft would not answer.
 */
export async function serviceTicket(env: EntitlementEnv, fetcher: Fetcher): Promise<string | null> {
  const app = appOf(env);
  if (app === null) return null;
  try {
    return await accessToken(app, fetcher);
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
      const token = await accessToken(app, fetcher);
      return token === null ? CANNOT_VERIFY : await query(proof, token, fetcher);
    } catch {
      return CANNOT_VERIFY; // network failure: we could not ask, which is never "owned"
    }
  },
};

/** Test seam: forgets the cached token. */
export function resetMicrosoftTokenCache(): void {
  cachedToken = null;
}
