// Slice 282: the checks every Alexa request must pass before the Worker believes it
// (https://developer.amazon.com/en-US/docs/alexa/custom-skills/host-a-custom-skill-as-a-web-service.html): the certificate URL rules,
// the signing chain, the Signature-256 over the raw body, the 150 s timestamp and the skill's own application id (no id set = every request refused).
// No dependency: the few X.509 fields the steps need are read by a minimal DER reader below (a certificate library was tried:
// @peculiar/x509 needs a reflect polyfill under node and a second one under wrangler; see the slice file). Signatures go through
// crypto.subtle, which the Worker and node both have. Every failure is just `null` (the caller answers HTTP 400 without detail);
// nothing here logs. Kept free of enums and parameter properties so node's type stripping can run it in tests.

import { TRUSTED_ROOTS } from "./alexa-roots.ts";

export const SIGNATURE_HEADER = "Signature-256";
export const CERT_URL_HEADER = "SignatureCertChainUrl";
export const CERT_HOST = "s3.amazonaws.com";
export const CERT_PATH_PREFIX = "/echo.api/";
export const SIGNING_DOMAIN = "echo-api.amazon.com";
export const MAX_REQUEST_AGE_MS = 150_000;
const MAX_CHAIN_BYTES = 65_536;
const MAX_CHAIN_CERTS = 6;
const CHAIN_CACHE_MS = 3_600_000;
export const MAX_CACHED_CHAINS = 16;
const HTTPS = "https:";
const SPKI_ALGORITHM = "RSASSA-PKCS1-v1_5";
const FETCH_TIMEOUT_MS = 5_000;

const Tag = { Boolean: 0x01, BitString: 0x03, OctetString: 0x04, Oid: 0x06, Sequence: 0x30, UtcTime: 0x17, GeneralizedTime: 0x18, Version: 0xa0, Extensions: 0xa3, DnsName: 0x82 } as const;

/** DER bytes of the OIDs read, as hex. */
const Oid = {
  Sha256WithRsa: "2a864886f70d01010b",
  SubjectAltName: "551d11",
  BasicConstraints: "551d13",
} as const;

/** Only RSA with SHA-256 is accepted (Amazon's chain uses nothing else). */
const HASH_OF_SIGNATURE: Readonly<Record<string, string>> = {
  [Oid.Sha256WithRsa]: "SHA-256",
};

interface Tlv {
  readonly tag: number;
  readonly header: number;
  readonly start: number;
  readonly end: number;
}

export interface ParsedCertificate {
  readonly tbs: Uint8Array;
  readonly hash: string;
  readonly signature: Uint8Array;
  readonly issuer: Uint8Array;
  readonly subject: Uint8Array;
  readonly spki: Uint8Array;
  readonly notBefore: number;
  readonly notAfter: number;
  readonly dnsNames: readonly string[];
  readonly isCa: boolean;
}

/** What `verifyAlexaRequest` needs besides the request; every field is replaceable so tests can run offline with their own roots and clock. */
export interface VerifyOptions {
  readonly now?: Date;
  readonly roots?: readonly string[];
  readonly fetchChain?: (url: string) => Promise<string | null>;
  /** The skill's id; empty or missing rejects every request (the signature only proves the sender is Alexa, not that it is our skill). */
  readonly skillId?: string;
  readonly cache?: Map<string, ChainCacheEntry>;
}

/** A verified leaf key and when to stop trusting it (an hour, or the leaf's notAfter if sooner). Failures are never stored. */
export interface ChainCacheEntry {
  readonly spki: Uint8Array;
  readonly expires: number;
}

/** A parsed request body (only the fields this Worker reads are typed, the rest stay unknown). */
export type AlexaEnvelope = Record<string, unknown>;

const defaultCache = new Map<string, ChainCacheEntry>();

// ---- DER ----

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

function whole(bytes: Uint8Array, item: Tlv): Uint8Array {
  return bytes.slice(item.start - item.header, item.end);
}

function content(bytes: Uint8Array, item: Tlv): Uint8Array {
  return bytes.slice(item.start, item.end);
}

function hex(bytes: Uint8Array): string {
  return [...bytes].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}

export function sameBytes(left: Uint8Array, right: Uint8Array): boolean {
  return left.length === right.length && left.every((byte, index) => byte === right[index]);
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

function dnsNamesOf(bytes: Uint8Array, value: Tlv): string[] {
  const names = readTlv(bytes, value.start, value.end);
  return children(bytes, names)
    .filter((name) => name.tag === Tag.DnsName)
    .map((name) => new TextDecoder().decode(content(bytes, name)).toLowerCase());
}

function isCaOf(bytes: Uint8Array, value: Tlv): boolean {
  const constraints = readTlv(bytes, value.start, value.end);
  const first = children(bytes, constraints)[0];
  return first !== undefined && first.tag === Tag.Boolean && bytes[first.start] !== 0;
}

interface ExtensionFacts {
  readonly dnsNames: string[];
  readonly isCa: boolean;
}

function readExtensions(bytes: Uint8Array, wrapper: Tlv): ExtensionFacts {
  const facts = { dnsNames: [] as string[], isCa: false };
  const list = readTlv(bytes, wrapper.start, wrapper.end);
  for (const extension of children(bytes, list)) {
    const parts = children(bytes, extension);
    const value = parts[parts.length - 1];
    const kind = parts[0] === undefined ? "" : hex(content(bytes, parts[0]));
    if (value === undefined || value.tag !== Tag.OctetString) continue;
    if (kind === Oid.SubjectAltName) facts.dnsNames.push(...dnsNamesOf(bytes, value));
    if (kind === Oid.BasicConstraints) facts.isCa = isCaOf(bytes, value);
  }
  return facts;
}

export function parseCertificate(der: Uint8Array): ParsedCertificate {
  const certificate = readTlv(der, 0, der.length);
  if (certificate.tag !== Tag.Sequence || certificate.end !== der.length) throw new Error("der");
  const [tbsItem, algorithm, signatureItem] = children(der, certificate);
  if (tbsItem === undefined || algorithm === undefined || signatureItem === undefined || signatureItem.tag !== Tag.BitString) throw new Error("der");
  const oid = children(der, algorithm)[0];
  const hash = oid === undefined ? undefined : HASH_OF_SIGNATURE[hex(content(der, oid))];
  if (hash === undefined) throw new Error("algorithm");
  const signature = content(der, signatureItem);
  if (signature[0] !== 0) throw new Error("der");
  const fields = children(der, tbsItem);
  let at = fields[0]?.tag === Tag.Version ? 1 : 0;
  at += 2; // serial number and the TBS copy of the signature algorithm
  const inner = fields[at - 1];
  if (inner === undefined || !sameBytes(whole(der, inner), whole(der, algorithm))) throw new Error("algorithm"); // RFC 5280 4.1.1.2: both must be identical
  const [issuer, validity, subject, spki] = fields.slice(at, at + 4);
  if (issuer === undefined || validity === undefined || subject === undefined || spki === undefined) throw new Error("der");
  const [from, until] = children(der, validity);
  if (from === undefined || until === undefined) throw new Error("der");
  const wrapper = fields.slice(at + 4).find((field) => field.tag === Tag.Extensions);
  const facts = wrapper === undefined ? { dnsNames: [], isCa: false } : readExtensions(der, wrapper);
  return {
    tbs: whole(der, tbsItem),
    hash,
    signature: signature.slice(1),
    issuer: whole(der, issuer),
    subject: whole(der, subject),
    spki: whole(der, spki),
    notBefore: parseTime(der, from),
    notAfter: parseTime(der, until),
    dnsNames: facts.dnsNames,
    isCa: facts.isCa,
  };
}

const PEM_BLOCK = /-----BEGIN CERTIFICATE-----([A-Za-z0-9+/=\s]+?)-----END CERTIFICATE-----/g;

function bytesOfBase64(text: string): Uint8Array {
  const binary = atob(text);
  return Uint8Array.from(binary, (char) => char.charCodeAt(0));
}

export function parsePemChain(pem: string): ParsedCertificate[] {
  const blocks = [...pem.matchAll(PEM_BLOCK)].map((match) => match[1]!.replace(/\s+/g, ""));
  if (blocks.length === 0 || blocks.length > MAX_CHAIN_CERTS) throw new Error("chain");
  return blocks.map((block) => parseCertificate(bytesOfBase64(block)));
}

// ---- URL, signature, time ----

/** Amazon's certificate URL rules: https, host s3.amazonaws.com (any case), path under /echo.api/ after normalising, port 443 or none, no userinfo. */
export function isValidCertUrl(text: string): boolean {
  let url: URL;
  try {
    url = new URL(text);
  } catch {
    // no-fallback: an unparsable certificate URL just fails the check
    return false;
  }
  return url.protocol === HTTPS && url.hostname === CERT_HOST && url.port === "" && url.username === "" && url.password === "" && url.pathname.startsWith(CERT_PATH_PREFIX);
}

async function verifyRsa(spki: Uint8Array, hash: string, data: Uint8Array, signature: Uint8Array): Promise<boolean> {
  try {
    const key = await crypto.subtle.importKey("spki", spki, { name: SPKI_ALGORITHM, hash }, false, ["verify"]);
    return await crypto.subtle.verify(SPKI_ALGORITHM, key, signature, data);
  } catch {
    // no-fallback: a key or signature that cannot be used is a failed check, never an error to the caller
    return false;
  }
}

function validAt(certificate: ParsedCertificate, now: number): boolean {
  return certificate.notBefore <= now && now <= certificate.notAfter;
}

async function signedBy(certificate: ParsedCertificate, issuer: ParsedCertificate): Promise<boolean> {
  return issuer.isCa && sameBytes(certificate.issuer, issuer.subject) && verifyRsa(issuer.spki, certificate.hash, certificate.tbs, certificate.signature);
}

async function anchoredAt(certificate: ParsedCertificate, roots: readonly ParsedCertificate[], now: number): Promise<boolean> {
  for (const root of roots) {
    if (!validAt(root, now)) continue;
    if (sameBytes(certificate.spki, root.spki)) return true;
    if (await signedBy(certificate, root)) return true;
  }
  return false;
}

/** Leaf: valid now with echo-api.amazon.com among its names; each certificate valid now and signed by the next (a CA); the walk ends at a bundled root. */
export async function verifyChain(chain: readonly ParsedCertificate[], roots: readonly ParsedCertificate[], now: number): Promise<boolean> {
  const leaf = chain[0];
  if (leaf === undefined || leaf.isCa || !leaf.dnsNames.includes(SIGNING_DOMAIN)) return false; // an end-entity certificate is never a CA
  for (let index = 0; index < chain.length; index += 1) {
    const certificate = chain[index]!;
    if (!validAt(certificate, now) || (index > 0 && !certificate.isCa)) return false;
    if (await anchoredAt(certificate, roots, now)) return true;
    const next = chain[index + 1];
    if (next === undefined || !(await signedBy(certificate, next))) return false;
  }
  return false;
}

/** The whole stream as bytes, or null once it passes `max` (the rest is never read, whatever any length header said). */
export async function readCapped(stream: ReadableStream<Uint8Array> | null, max: number): Promise<Uint8Array | null> {
  if (stream === null) return new Uint8Array(0);
  const reader = stream.getReader();
  const parts: Uint8Array[] = [];
  let total = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    total += value.length;
    if (total > max) {
      await reader.cancel().catch(() => undefined);
      return null;
    }
    parts.push(value);
  }
  const bytes = new Uint8Array(total);
  let at = 0;
  for (const part of parts) {
    bytes.set(part, at);
    at += part.length;
  }
  return bytes;
}

async function defaultFetchChain(url: string): Promise<string | null> {
  const response = await fetch(url, { redirect: "manual", signal: AbortSignal.timeout(FETCH_TIMEOUT_MS) });
  if (!response.ok) return null;
  const bytes = await readCapped(response.body, MAX_CHAIN_BYTES);
  return bytes === null ? null : new TextDecoder().decode(bytes);
}

/** The one URL that is fetched and cached: origin and path only, so a query string cannot make a fresh entry. */
function chainKeyOf(url: string): string {
  const parsed = new URL(url);
  return `${parsed.origin}${parsed.pathname}`;
}

/** Only a verified success is stored. Expired entries go first, then the least recently used, so a new success is the only thing that ever evicts. */
function remember(cache: Map<string, ChainCacheEntry>, key: string, entry: ChainCacheEntry, now: number): void {
  cache.delete(key);
  for (const [other, value] of cache) if (value.expires <= now) cache.delete(other);
  while (cache.size >= MAX_CACHED_CHAINS) cache.delete(cache.keys().next().value as string);
  cache.set(key, entry);
}

let defaultRoots: readonly ParsedCertificate[] | null = null;

/** The trusted roots, parsed on first use and kept for the isolate: a bad root fails Alexa requests (the caller catches it), never the Worker import. */
function defaultRootsOf(): readonly ParsedCertificate[] {
  defaultRoots ??= TRUSTED_ROOTS.flatMap((root) => parsePemChain(root));
  return defaultRoots;
}

async function fetchedLeaf(key: string, options: VerifyOptions, now: number): Promise<ChainCacheEntry | null> {
  const pem = await (options.fetchChain ?? defaultFetchChain)(key);
  if (pem === null) return null;
  const chain = parsePemChain(pem);
  const roots = options.roots === undefined ? defaultRootsOf() : options.roots.flatMap((root) => parsePemChain(root));
  if (!(await verifyChain(chain, roots, now))) return null;
  return { spki: chain[0]!.spki, expires: Math.min(now + CHAIN_CACHE_MS, chain[0]!.notAfter) };
}

/** A failed fetch or check fails only this request and is never stored: the next request fetches again. */
async function leafKeyFor(url: string, options: VerifyOptions, now: number): Promise<Uint8Array | null> {
  const cache = options.cache ?? defaultCache;
  const key = chainKeyOf(url);
  const cached = cache.get(key);
  if (cached !== undefined && cached.expires > now) {
    cache.delete(key);
    cache.set(key, cached);
    return cached.spki;
  }
  const fetched = await fetchedLeaf(key, options, now).catch(() => null);
  if (fetched === null) return null;
  remember(cache, key, fetched, now);
  return fetched.spki;
}

function bytesOfSignature(header: string): Uint8Array | null {
  try {
    return bytesOfBase64(header.trim());
  } catch {
    // no-fallback: a signature that is not base64 fails the check
    return null;
  }
}

function applicationIdOf(envelope: AlexaEnvelope): string | null {
  const session = envelope.session as { application?: { applicationId?: unknown } } | undefined;
  const context = envelope.context as { System?: { application?: { applicationId?: unknown } } } | undefined;
  const id = session?.application?.applicationId ?? context?.System?.application?.applicationId;
  return typeof id === "string" ? id : null;
}

function isFresh(envelope: AlexaEnvelope, now: number): boolean {
  const request = envelope.request as { timestamp?: unknown } | undefined;
  const stamp = typeof request?.timestamp === "string" ? Date.parse(request.timestamp) : Number.NaN;
  return Number.isFinite(stamp) && Math.abs(now - stamp) <= MAX_REQUEST_AGE_MS;
}

function parseEnvelope(body: Uint8Array): AlexaEnvelope | null {
  try {
    const value: unknown = JSON.parse(new TextDecoder("utf-8", { fatal: true, ignoreBOM: false }).decode(body));
    return typeof value === "object" && value !== null && !Array.isArray(value) ? (value as AlexaEnvelope) : null;
  } catch {
    // no-fallback: a body that is not JSON fails the check
    return null;
  }
}

async function signatureHolds(request: Request, body: Uint8Array, options: VerifyOptions, now: number): Promise<boolean> {
  const url = request.headers.get(CERT_URL_HEADER);
  const header = request.headers.get(SIGNATURE_HEADER);
  if (url === null || header === null || !isValidCertUrl(url)) return false;
  const signature = bytesOfSignature(header);
  if (signature === null) return false;
  const spki = await leafKeyFor(url, options, now);
  return spki !== null && verifyRsa(spki, "SHA-256", body, signature);
}

/** Fresh and for this skill; fail closed: no id configured, nothing is accepted. */
function claimsHold(envelope: AlexaEnvelope, options: VerifyOptions, now: number): boolean {
  const skillId = options.skillId ?? "";
  return isFresh(envelope, now) && skillId !== "" && applicationIdOf(envelope) === skillId;
}

/**
 * The parsed body when the request is really from Alexa for this skill and fresh, else null. Never throws. The cheap checks (timestamp,
 * skill id, certificate URL) run on the not-yet-trusted body first so a forged request never costs a certificate fetch.
 */
export async function verifyAlexaRequest(request: Request, body: Uint8Array, options: VerifyOptions = {}): Promise<AlexaEnvelope | null> {
  const now = (options.now ?? new Date()).getTime();
  try {
    const envelope = parseEnvelope(body);
    if (envelope === null || !claimsHold(envelope, options, now)) return null;
    if (!(await signatureHolds(request, body, options, now))) return null;
    return envelope;
  } catch {
    // no-fallback: any failure while checking (a fetch error, a malformed certificate) rejects the request
    return null;
  }
}
