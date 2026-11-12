// Small JWT helpers for the store checkers (WebCrypto, no dependency). Used to sign the Google service-account assertion (RS256)
// and Apple's App Store Server API token (ES256), and to read the payload of Apple's signed transaction.

const ES256_ALGORITHM = { name: "ECDSA", namedCurve: "P-256" } as const;
const RS256_ALGORITHM = { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" } as const;
const PEM_BODY = /-----BEGIN [A-Z ]+-----|-----END [A-Z ]+-----|\s+/g;

export function base64UrlEncode(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

export function base64UrlDecode(text: string): Uint8Array {
  const padded = text.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(text.length / 4) * 4, "=");
  const binary = atob(padded);
  return Uint8Array.from(binary, (char) => char.charCodeAt(0));
}

function textBytes(text: string): Uint8Array {
  return new TextEncoder().encode(text);
}

function pemBytes(pem: string): Uint8Array {
  return Uint8Array.from(atob(pem.replace(PEM_BODY, "")), (char) => char.charCodeAt(0));
}

/** A compact JWS over `header.payload`, signed with a PKCS8 PEM key. `alg` is `ES256` (Apple) or `RS256` (Google). */
export async function signJwt(alg: "ES256" | "RS256", header: Record<string, unknown>, payload: Record<string, unknown>, pem: string): Promise<string> {
  const algorithm = alg === "ES256" ? ES256_ALGORITHM : RS256_ALGORITHM;
  const key = await crypto.subtle.importKey("pkcs8", pemBytes(pem), algorithm, false, ["sign"]);
  const input = `${base64UrlEncode(textBytes(JSON.stringify({ alg, typ: "JWT", ...header })))}.${base64UrlEncode(textBytes(JSON.stringify(payload)))}`;
  const signature = await crypto.subtle.sign(alg === "ES256" ? { name: "ECDSA", hash: "SHA-256" } : RS256_ALGORITHM.name, key, textBytes(input));
  return `${input}.${base64UrlEncode(new Uint8Array(signature))}`;
}

/** The payload object of a compact JWS, or null when it is not one. The signature is not checked: the caller fetched it from the issuer over TLS. */
export function jwtPayload(jws: string): Record<string, unknown> | null {
  const part = jws.split(".")[1];
  if (part === undefined || part === "") return null;
  try {
    const parsed: unknown = JSON.parse(new TextDecoder().decode(base64UrlDecode(part)));
    return typeof parsed === "object" && parsed !== null && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}
