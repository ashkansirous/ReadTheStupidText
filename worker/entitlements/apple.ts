// Apple: App Store Server API, Get Transaction Info (ES256 token from the .p8 key). The transaction counts when it is for this app and
// the proof's product, is in the Production environment and has not been revoked. Only when the Worker is told to (`APPLE_IAP_ALLOW_SANDBOX`, a staging Worker),
// a transaction the production host does not know is tried on the sandbox host and a Sandbox one counts; in production a sandbox purchase never unlocks a book.
import { APP_PACKAGE_ID } from "../products.ts";
import { jwtPayload, signJwt } from "./jwt.ts";
import { CANNOT_VERIFY, checked } from "./types.ts";
import type { CheckResult, EntitlementEnv, Fetcher, Proof, StoreChecker } from "./types.ts";

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

export const appleChecker: StoreChecker = {
  async check(proof, env, fetcher) {
    const key = keyOf(env);
    if (key === null) return CANNOT_VERIFY;
    try {
      const token = await bearer(key);
      const production = await askHost(PRODUCTION_HOST, proof, token, fetcher);
      const allowSandbox = sandboxAllowed(env);
      const response = production.status === NOT_FOUND && allowSandbox ? await askHost(SANDBOX_HOST, proof, token, fetcher) : production;
      return await resultOf(response, proof.productId, allowSandbox);
    } catch {
      return CANNOT_VERIFY; // network or signing failure: we could not ask, which is never "owned"
    }
  },
};

/** Test seam: forgets the cached token. */
export function resetAppleTokenCache(): void {
  cachedToken = null;
}
