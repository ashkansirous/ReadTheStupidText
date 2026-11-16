// Slice 390: checks an App Store signed payload (the JWS of a StoreKit 2 transaction the app sends as its purchase proof) without asking Apple: the JWS header's x5c chain must end in Apple's
// Root CA - G3 (pinned by the SHA-256 of its certificate), every certificate must be signed by the next one and valid at the payload's `signedDate`, the leaf and the intermediate must carry
// Apple's marker extensions, and the JWS signature must verify under the leaf's key. Only then is the payload believed. This is Apple's own documented verification
// (https://developer.apple.com/documentation/appstoreserverapi/jwstransaction) done with WebCrypto and a small DER reader, no dependency (as worker/alexa-verify.ts does for Amazon's chain).
// A bad or forged JWS is just `null`; nothing here logs. Kept free of enums and parameter properties so node's type stripping can run it in tests.
import { base64UrlDecode } from "./jwt.ts";

/** SHA-256 of Apple Root CA - G3 (https://www.apple.com/certificateauthority/AppleRootCA-G3.cer, valid to 2039-04-30), lower-case hex. */
export const APPLE_ROOT_CA_G3_SHA256 = "63343abfb89a6a03ebb57e9b3f5fa7be7c4f5c756f3017b3a8c488c3653e9179";

/** Apple's signing chain is exactly leaf, intermediate, root (as Apple's app-store-server-library requires). */
const CHAIN_LENGTH = 3;
/** Clock skew allowed on a certificate's validity dates, in milliseconds (the same 60 s the library allows). */
const CLOCK_SKEW_MS = 60_000;
const JWS_PARTS = 3;
const JWS_ALGORITHM = "ES256";

const Tag = { Integer: 0x02, BitString: 0x03, Oid: 0x06, Sequence: 0x30, UtcTime: 0x17, GeneralizedTime: 0x18, Version: 0xa0, Extensions: 0xa3 } as const;

/** DER bytes of the OIDs read, as hex. */
const Oid = {
  EcdsaWithSha256: "2a8648ce3d040302",
  EcdsaWithSha384: "2a8648ce3d040303",
  CurveP256: "2a8648ce3d030107",
  CurveP384: "2b81040022",
  /** Apple's marker extension on the leaf (receipt signing) and on the intermediate (Worldwide Developer Relations). */
  AppleLeaf: "2a864886f76364060b01",
  AppleIntermediate: "2a864886f76364060201",
} as const;

const HASH_OF_SIGNATURE: Readonly<Record<string, string>> = { [Oid.EcdsaWithSha256]: "SHA-256", [Oid.EcdsaWithSha384]: "SHA-384" };
const CURVE_OF_OID: Readonly<Record<string, string>> = { [Oid.CurveP256]: "P-256", [Oid.CurveP384]: "P-384" };
const SIGNATURE_BYTES: Readonly<Record<string, number>> = { "P-256": 32, "P-384": 48 };

interface Tlv {
  readonly tag: number;
  readonly header: number;
  readonly start: number;
  readonly end: number;
}

interface Certificate {
  readonly tbs: Uint8Array;
  /** The extnID of every extension, as OID hex. */
  readonly extensions: readonly string[];
  readonly hash: string;
  readonly signature: Uint8Array;
  readonly spki: Uint8Array;
  readonly curve: string;
  readonly notBefore: number;
  readonly notAfter: number;
}

export interface AppleJwsOptions {
  /** Trusted root fingerprints (lower-case hex SHA-256 of the DER); tests pass their own, production uses Apple Root CA - G3 only. */
  readonly roots?: readonly string[];
  /** The clock for a payload with no `signedDate`. */
  readonly now?: number;
}

function readTlv(bytes: Uint8Array, at: number, limit: number): Tlv {
  if (at + 2 > limit) throw new Error("der");
  const tag = bytes[at]!;
  let length = bytes[at + 1]!;
  let header = 2;
  if (length >= 0x80) {
    const count = length & 0x7f;
    if (count < 1 || count > 3 || at + 2 + count > limit) throw new Error("der");
    length = 0;
    for (let index = 0; index < count; index += 1) length = length * 256 + bytes[at + 2 + index]!;
    header += count;
  }
  const start = at + header;
  const end = start + length;
  if (end > limit) throw new Error("der");
  return { tag, header, start, end };
}

function children(bytes: Uint8Array, parent: Tlv): Tlv[] {
  const items: Tlv[] = [];
  for (let at = parent.start; at < parent.end; ) {
    const item = readTlv(bytes, at, parent.end);
    items.push(item);
    at = item.end;
  }
  return items;
}

function content(bytes: Uint8Array, item: Tlv): Uint8Array {
  return bytes.slice(item.start, item.end);
}

function whole(bytes: Uint8Array, item: Tlv): Uint8Array {
  return bytes.slice(item.start - item.header, item.end);
}

function hex(bytes: Uint8Array): string {
  return [...bytes].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}

function parseTime(bytes: Uint8Array, item: Tlv): number {
  const text = new TextDecoder().decode(content(bytes, item));
  const long = item.tag === Tag.GeneralizedTime;
  const match = (long ? /^(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})Z$/ : /^(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})Z$/).exec(text);
  if (match === null) throw new Error("der");
  const [year, month, day, hour, minute, second] = match.slice(1).map(Number) as [number, number, number, number, number, number];
  const fullYear = long ? year : year >= 50 ? 1900 + year : 2000 + year;
  return Date.UTC(fullYear, month - 1, day, hour, minute, second);
}

function oidHex(bytes: Uint8Array, algorithm: Tlv): string {
  const oid = children(bytes, algorithm)[0];
  if (oid === undefined || oid.tag !== Tag.Oid) throw new Error("der");
  return hex(content(bytes, oid));
}

/** The curve of an EC public key (SubjectPublicKeyInfo) as WebCrypto names it. */
function curveOf(bytes: Uint8Array, spki: Tlv): string {
  const algorithm = children(bytes, spki)[0];
  const curve = algorithm === undefined ? undefined : children(bytes, algorithm)[1];
  const name = curve === undefined ? undefined : CURVE_OF_OID[hex(content(bytes, curve))];
  if (name === undefined) throw new Error("curve");
  return name;
}

/** The OID of each extension in the TBS fields' `[3]` extensions block (an Extension is SEQUENCE { extnID OID, critical?, extnValue }); empty when there is none. */
function extensionOids(bytes: Uint8Array, fields: readonly Tlv[]): string[] {
  const block = fields.find((field) => field.tag === Tag.Extensions);
  const list = block === undefined ? undefined : children(bytes, block)[0];
  if (list === undefined || list.tag !== Tag.Sequence) return [];
  return children(bytes, list).map((extension) => {
    const id = children(bytes, extension)[0];
    if (id === undefined || id.tag !== Tag.Oid) throw new Error("der");
    return hex(content(bytes, id));
  });
}

function parseCertificate(der: Uint8Array): Certificate {
  const certificate = readTlv(der, 0, der.length);
  if (certificate.tag !== Tag.Sequence || certificate.end !== der.length) throw new Error("der");
  const [tbsItem, algorithm, signatureItem] = children(der, certificate);
  if (tbsItem === undefined || algorithm === undefined || signatureItem === undefined || signatureItem.tag !== Tag.BitString) throw new Error("der");
  const hash = HASH_OF_SIGNATURE[oidHex(der, algorithm)];
  if (hash === undefined) throw new Error("algorithm");
  const signature = content(der, signatureItem);
  if (signature[0] !== 0) throw new Error("der");
  const fields = children(der, tbsItem);
  const first = fields[0]?.tag === Tag.Version ? 1 : 0; // serial number follows the optional version; then the signature algorithm, issuer, validity, subject and key
  const validity = fields[first + 3];
  const spki = fields[first + 5];
  if (validity === undefined || spki === undefined) throw new Error("der");
  const [from, until] = children(der, validity);
  if (from === undefined || until === undefined) throw new Error("der");
  const tbs = whole(der, tbsItem);
  return { tbs, extensions: extensionOids(der, fields), hash, signature: signature.slice(1), spki: whole(der, spki), curve: curveOf(der, spki), notBefore: parseTime(der, from), notAfter: parseTime(der, until) };
}

/** An ECDSA signature in DER (SEQUENCE of two INTEGERs) as the fixed-width r||s WebCrypto verifies. */
function rawSignature(der: Uint8Array, size: number): Uint8Array {
  const pair = readTlv(der, 0, der.length);
  const [r, s] = children(der, pair);
  if (pair.tag !== Tag.Sequence || r === undefined || s === undefined || r.tag !== Tag.Integer || s.tag !== Tag.Integer) throw new Error("der");
  const raw = new Uint8Array(size * 2);
  for (const [item, offset] of [[r, 0], [s, size]] as const) {
    let value = content(der, item);
    while (value.length > size && value[0] === 0) value = value.slice(1);
    if (value.length > size) throw new Error("der");
    raw.set(value, offset + size - value.length);
  }
  return raw;
}

async function signedBy(certificate: Certificate, issuer: Certificate): Promise<boolean> {
  const key = await crypto.subtle.importKey("spki", issuer.spki, { name: "ECDSA", namedCurve: issuer.curve }, false, ["verify"]);
  const signature = rawSignature(certificate.signature, SIGNATURE_BYTES[issuer.curve]!);
  return crypto.subtle.verify({ name: "ECDSA", hash: certificate.hash }, key, signature, certificate.tbs);
}

async function fingerprint(der: Uint8Array): Promise<string> {
  return hex(new Uint8Array(await crypto.subtle.digest("SHA-256", der)));
}

function base64Bytes(text: string): Uint8Array {
  return Uint8Array.from(atob(text), (char) => char.charCodeAt(0));
}

/** The extension OIDs (hex) a DER certificate lists in its extensions block; exported for the tests. */
export function certificateExtensions(der: Uint8Array): readonly string[] {
  return parseCertificate(der).extensions;
}

/** True for a string shaped like a compact JWS (three dot-separated parts); says nothing about whether it verifies. */
export function isJws(token: string): boolean {
  return token.split(".").length === JWS_PARTS;
}

/** True when the certificate's own extensions list carries this OID (not merely the bytes somewhere in the certificate). */
function hasOid(certificate: Certificate, oid: string): boolean {
  return certificate.extensions.includes(oid);
}

function decodeJson(part: string): Record<string, unknown> | null {
  const parsed: unknown = JSON.parse(new TextDecoder().decode(base64UrlDecode(part)));
  return typeof parsed === "object" && parsed !== null && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : null;
}

async function trustedChain(x5c: unknown, roots: readonly string[]): Promise<Certificate[] | null> {
  if (!Array.isArray(x5c) || x5c.length !== CHAIN_LENGTH || x5c.some((item) => typeof item !== "string")) return null;
  const ders = (x5c as string[]).map(base64Bytes);
  if (!roots.includes(await fingerprint(ders[ders.length - 1]!))) return null;
  const chain = ders.map(parseCertificate);
  for (let index = 0; index < chain.length - 1; index += 1) {
    if (!(await signedBy(chain[index]!, chain[index + 1]!))) return null;
  }
  const [leaf, intermediate] = chain;
  return leaf !== undefined && intermediate !== undefined && hasOid(leaf, Oid.AppleLeaf) && hasOid(intermediate, Oid.AppleIntermediate) ? chain : null;
}

async function signatureHolds(jws: string[], leaf: Certificate): Promise<boolean> {
  if (leaf.curve !== "P-256") return false;
  const key = await crypto.subtle.importKey("spki", leaf.spki, { name: "ECDSA", namedCurve: "P-256" }, false, ["verify"]);
  return crypto.subtle.verify({ name: "ECDSA", hash: "SHA-256" }, key, base64UrlDecode(jws[2]!), new TextEncoder().encode(`${jws[0]}.${jws[1]}`));
}

/**
 * The payload of an App Store signed payload when the whole chain checks out, else null (malformed, not Apple's, tampered, or a certificate that was not valid when Apple signed it).
 * A payload's `signedDate` (milliseconds) is the instant the certificates must be valid at; without one, the clock is used.
 */
export async function verifyAppleSignedPayload(jws: string, options: AppleJwsOptions = {}): Promise<Record<string, unknown> | null> {
  const parts = jws.split(".");
  if (!isJws(jws)) return null;
  try {
    const header = decodeJson(parts[0]!);
    const payload = decodeJson(parts[1]!);
    if (header === null || payload === null || header["alg"] !== JWS_ALGORITHM) return null;
    const chain = await trustedChain(header["x5c"], options.roots ?? [APPLE_ROOT_CA_G3_SHA256]);
    if (chain === null) return null;
    const signed = payload["signedDate"];
    const at = typeof signed === "number" && Number.isFinite(signed) ? signed : (options.now ?? Date.now());
    if (chain.some((certificate) => at < certificate.notBefore - CLOCK_SKEW_MS || at > certificate.notAfter + CLOCK_SKEW_MS)) return null;
    return (await signatureHolds(parts, chain[0]!)) ? payload : null;
  } catch {
    return null; // no-fallback: anything unreadable or a crypto failure means the proof is not believed; the checker answers "owns nothing", never "owned"
  }
}
