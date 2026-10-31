// Site routing and the /download redirect. Direct APK downloads were removed (user decision,
// 2026-10-27): every /download path redirects to the landing page's platforms
// section. This file also holds the R2 object streaming (serveObject) that the
// free story store uses. Keep this file free of enums and parameter properties:
// worker/downloads.test.mjs runs it under node's type stripping.

import { DINO_PATH_PREFIX, Host, JSON_CONTENT_TYPE, NO_CACHE, Product } from "./download-keys.mjs";

export { DINO_PATH_PREFIX, Host, Product };

export const DOWNLOAD_PATH = "/download";
const PLATFORMS_HASH = "#platforms";

const HttpHeader = {
  ContentType: "Content-Type",
  CacheControl: "Cache-Control",
  ContentDisposition: "Content-Disposition",
  ContentLength: "Content-Length",
  ContentRange: "Content-Range",
  AcceptRanges: "Accept-Ranges",
  ETag: "ETag",
  Range: "Range",
  IfNoneMatch: "If-None-Match",
  IfRange: "If-Range",
} as const;
const ErrorCode = {
  NotFound: "not-found",
  StoryUnavailable: "story-unavailable",
} as const;
type ErrorCode = (typeof ErrorCode)[keyof typeof ErrorCode];
const BYTES_PREFIX = "bytes ";
const BYTES_UNSATISFIED_PREFIX = "bytes */";
const WEAK_PREFIX = "W/";
const ANY_ETAG = "*";
const SINGLE_RANGE = /^bytes=(\d*)-(\d*)$/;

export interface ByteRange {
  readonly offset: number;
  readonly length: number;
}

/**
 * One `bytes=` range against a file of `size` bytes: the slice to send, or
 * 'unsatisfiable' (answer 416), or null for no usable range (serve the whole
 * file; a multi-range or malformed header may be ignored).
 */
export function parseRange(header: string | null, size: number): ByteRange | "unsatisfiable" | null {
  const match = header ? SINGLE_RANGE.exec(header.trim()) : null;
  if (!match) return null;
  const startText = match[1] ?? "";
  const endText = match[2] ?? "";
  if (startText === "" && endText === "") return null;
  if (startText === "") {
    const suffix = Number(endText);
    if (suffix === 0 || size === 0) return "unsatisfiable";
    const length = Math.min(suffix, size);
    return { offset: size - length, length };
  }
  const start = Number(startText);
  if (endText !== "" && Number(endText) < start) return null;
  if (start >= size) return "unsatisfiable";
  const end = endText === "" ? size - 1 : Math.min(Number(endText), size - 1);
  return { offset: start, length: end - start + 1 };
}

/** The first and last byte R2 resolved for a ranged read, or null for a whole-object read. */
export function servedSpan(range: R2Range | undefined, size: number): { start: number; end: number } | null {
  if (!range) return null;
  if ("suffix" in range && range.suffix !== undefined) {
    const length = Math.min(range.suffix, size);
    return { start: size - length, end: size - 1 };
  }
  const start = "offset" in range ? (range.offset ?? 0) : 0;
  const length = "length" in range ? (range.length ?? size - start) : size - start;
  return { start, end: start + length - 1 };
}

function textResponse(body: string, status: number, contentType: string, cacheControl = NO_CACHE): Response {
  return new Response(body, { status, headers: { [HttpHeader.ContentType]: contentType, [HttpHeader.CacheControl]: cacheControl } });
}

function errorResponse(code: ErrorCode, status: number): Response {
  return textResponse(JSON.stringify({ code }), status, JSON_CONTENT_TYPE);
}

function notFound(): Response {
  return errorResponse(ErrorCode.NotFound, 404);
}

/**
 * True for a well-formed single range that R2 may be asked for: both bounds
 * with end >= start, a start alone, or a non-empty suffix. `bytes=-` and
 * `bytes=5-2` are ignored (whole file), as is `bytes=-0`. A start beyond the
 * file still counts: R2 refuses it and the 416 path answers.
 */
export function isForwardableRange(header: string): boolean {
  const match = SINGLE_RANGE.exec(header.trim());
  if (!match) return false;
  const startText = match[1] ?? "";
  const endText = match[2] ?? "";
  if (startText === "") return endText !== "" && Number(endText) > 0;
  return endText === "" || Number(endText) >= Number(startText);
}

/** The request's own `Range` header when it is the one form we honour (a valid single `bytes=` range), else null. */
export function singleRangeHeaders(request: Request): Headers | null {
  const header = request.headers.get(HttpHeader.Range);
  if (!header || !isForwardableRange(header)) return null;
  return new Headers({ [HttpHeader.Range]: header.trim() });
}

/** One R2 object to stream: its key, how it is sent, and what a failed read is called. */
export interface ServedFile {
  readonly key: string;
  readonly contentType: string;
  readonly fileName: string;
  readonly disposition: "attachment" | "inline";
  readonly unavailable: ErrorCode;
}

function fileHeaders(file: ServedFile, etag: string | null, cacheControl: string): Headers {
  const headers = new Headers();
  headers.set(HttpHeader.ContentType, file.contentType);
  headers.set(HttpHeader.ContentDisposition, `${file.disposition}; filename="${file.fileName}"`);
  headers.set(HttpHeader.CacheControl, cacheControl);
  headers.set(HttpHeader.AcceptRanges, "bytes");
  if (etag) headers.set(HttpHeader.ETag, etag);
  return headers;
}

// `ranged` is whether the request asked for a range: local miniflare reports a
// range on a whole-object read, which must still be a 200.
function objectResponse(file: ServedFile, object: R2ObjectBody, cacheControl: string, ranged: boolean): Response {
  const headers = fileHeaders(file, object.httpEtag, cacheControl);
  const span = ranged ? servedSpan(object.range, object.size) : null;
  if (!span) {
    headers.set(HttpHeader.ContentLength, String(object.size));
    return new Response(object.body, { status: 200, headers });
  }
  headers.set(HttpHeader.ContentLength, String(span.end - span.start + 1));
  headers.set(HttpHeader.ContentRange, `${BYTES_PREFIX}${span.start}-${span.end}/${object.size}`);
  return new Response(object.body, { status: 206, headers });
}

function headResponse(file: ServedFile, head: R2Object, cacheControl: string): Response {
  const headers = fileHeaders(file, head.httpEtag, cacheControl);
  headers.set(HttpHeader.ContentLength, String(head.size));
  return new Response(null, { status: 200, headers });
}

function notModifiedResponse(file: ServedFile, etag: string, cacheControl: string): Response {
  return new Response(null, { status: 304, headers: fileHeaders(file, etag, cacheControl) });
}

function opaqueTag(tag: string): string {
  return tag.startsWith(WEAK_PREFIX) ? tag.slice(WEAK_PREFIX.length) : tag;
}

/** If-None-Match (weak comparison): true when `*` or any listed tag equals the object's ETag. */
export function ifNoneMatchHits(header: string | null, etag: string): boolean {
  if (header === null) return false;
  const tags = header.split(",").map((tag) => tag.trim());
  return tags.some((tag) => tag === ANY_ETAG || opaqueTag(tag) === opaqueTag(etag));
}

/** If-Range (strong comparison): a range may be honoured only when the validator is exactly the current ETag. */
export function ifRangeAllowsRange(header: string | null, etag: string): boolean {
  if (header === null) return true;
  const validator = header.trim();
  return !validator.startsWith(WEAK_PREFIX) && !etag.startsWith(WEAK_PREFIX) && validator === etag;
}

function isConditional(request: Request): boolean {
  return request.headers.has(HttpHeader.IfNoneMatch) || request.headers.has(HttpHeader.IfRange);
}

/** A failed ranged get is told apart by the object's size: gone is 404, a range outside it 416, else 502. */
async function failedReadResponse(bucket: R2Bucket, file: ServedFile, rangeHeader: string | null): Promise<Response> {
  const head = await bucket.head(file.key);
  if (!head) return notFound();
  if (parseRange(rangeHeader, head.size) === "unsatisfiable") {
    return new Response(null, { status: 416, headers: { [HttpHeader.ContentRange]: `${BYTES_UNSATISFIED_PREFIX}${head.size}` } });
  }
  return errorResponse(file.unavailable, 502);
}

// A ranged get that comes back without `range` is a whole-object read, which
// objectResponse serves as a 200.
async function readObject(bucket: R2Bucket, key: string, range: Headers | null): Promise<R2ObjectBody | null | "failed"> {
  try {
    return await bucket.get(key, range ? { range } : undefined);
  } catch {
    return "failed";
  }
}

/** Bucket folders the Worker never serves (Slice 227: crash reports are written by the Worker and read only with a token off-line). */
const PRIVATE_KEY_PREFIXES = ["crashes/"] as const;

/** True for an R2 key under a folder that no download or static route may ever serve. */
export function isPrivateKey(key: string): boolean {
  return PRIVATE_KEY_PREFIXES.some((prefix) => key.startsWith(prefix));
}

/** Streams one R2 object: a single Range honoured (206), anything else whole; conditional requests get one extra head. */
export async function serveObject(request: Request, bucket: R2Bucket, file: ServedFile, cacheControl: string): Promise<Response> {
  if (isPrivateKey(file.key)) return notFound();
  try {
    const isHead = request.method === "HEAD";
    const head = isHead || isConditional(request) ? await bucket.head(file.key) : null;
    if (head === null && (isHead || isConditional(request))) return notFound();
    if (head && ifNoneMatchHits(request.headers.get(HttpHeader.IfNoneMatch), head.httpEtag)) {
      return notModifiedResponse(file, head.httpEtag, cacheControl);
    }
    if (head && isHead) return headResponse(file, head, cacheControl);
    const wanted = singleRangeHeaders(request);
    const honoured = head && !ifRangeAllowsRange(request.headers.get(HttpHeader.IfRange), head.httpEtag);
    const range = honoured ? null : wanted;
    const object = await readObject(bucket, file.key, range);
    if (object === "failed") return await failedReadResponse(bucket, file, range?.get(HttpHeader.Range) ?? null);
    return object ? objectResponse(file, object, cacheControl, range !== null) : notFound();
  } catch {
    return errorResponse(file.unavailable, 502);
  }
}

/** True for every path this module answers. */
export function isDownloadPath(pathname: string): boolean {
  return pathname === DOWNLOAD_PATH || pathname.startsWith(`${DOWNLOAD_PATH}/`);
}

/**
 * Direct APK downloads are gone: Android comes from Google Play, iPhone and
 * iPad from TestFlight, Windows from the Microsoft Store. Every old download
 * link (/download, /download/android, /download/android/latest, /download/android/<version>)
 * redirects to the platforms section of the product's landing page (`homePath`:
 * "/", or "/dino/" under the workers.dev prefix).
 */
export function handleDownload(request: Request, homePath: string = ROOT_PATH): Response {
  const target = `${homePath}${PLATFORMS_HASH}`;
  return Response.redirect(new URL(target, request.url).toString(), 302);
}

// ---- Site routing: which product a request is for (used by index.ts) ----

export const WORKERS_DEV_SUFFIX = ".workers.dev";
const ROOT_PATH = "/";

const PAGE_FOR_PATH: Readonly<Record<string, string>> = {
  "/": "/index.html",
  "/index.html": "/index.html",
  "/privacy": "/privacy.html",
  "/privacy/": "/privacy.html",
  "/privacy.html": "/privacy.html",
};

export interface ResolvedSite {
  readonly product: Product;
  /** The request path as the product's site sees it (no `/dino` prefix). */
  readonly path: string;
  /** Where the product's landing page lives for this request: "/", or "/dino/" under the workers.dev prefix. */
  readonly home: string;
}

function isDinoPrefixed(pathname: string): boolean {
  return pathname === DINO_PATH_PREFIX || pathname.startsWith(`${DINO_PATH_PREFIX}/`);
}

/** Picks the site for a request: by host, plus the `/dino/` prefix on workers.dev only. */
export function resolveSite(hostname: string, pathname: string): ResolvedSite {
  if (hostname === Host.DinoReader) return { product: Product.DinoReader, path: pathname, home: ROOT_PATH };
  if (hostname.endsWith(WORKERS_DEV_SUFFIX) && isDinoPrefixed(pathname)) {
    return { product: Product.DinoReader, path: pathname.slice(DINO_PATH_PREFIX.length) || ROOT_PATH, home: `${DINO_PATH_PREFIX}/` };
  }
  return { product: Product.ReadTheStupidText, path: pathname, home: ROOT_PATH };
}

/** The asset file a page path maps to, or null when the path is not a page. */
export function assetPageFor(site: ResolvedSite): string | null {
  const page = PAGE_FOR_PATH[site.path];
  if (page === undefined) return null;
  return site.product === Product.DinoReader ? `${DINO_PATH_PREFIX}${page}` : page;
}

const INDEX_HTML = "/index.html";
const PRIVACY_PATH = "/privacy";
const PRIVACY_HTML = "/privacy.html";
const TRAILING_SLASH = /\/+$/;

function isWorkersDev(hostname: string): boolean {
  return hostname.endsWith(WORKERS_DEV_SUFFIX) && hostname !== Host.DinoReader;
}

/**
 * True for a `/dino` path on a host that must not serve the Dino site under a
 * prefix: only workers.dev does (the Dino host serves it at its root).
 */
export function isHiddenDinoPath(hostname: string, pathname: string): boolean {
  return hostname !== Host.DinoReader && !isWorkersDev(hostname) && isDinoPrefixed(pathname);
}

function withoutTrailingSlash(pathname: string): string {
  const trimmed = pathname.replace(TRAILING_SLASH, "");
  return trimmed === "" ? ROOT_PATH : trimmed;
}

/** The Dino host's canonical form of a raw `/dino/...` path, or null when it is not a page. */
function dinoHostCanonical(pathname: string): string | null {
  const rest = pathname.slice(DINO_PATH_PREFIX.length);
  if (rest === "" || rest === ROOT_PATH || rest === INDEX_HTML) return ROOT_PATH;
  if (rest === PRIVACY_PATH || rest === PRIVACY_HTML || rest === `${PRIVACY_PATH}/`) return PRIVACY_PATH;
  return null;
}

function canonicalPath(hostname: string, pathname: string): string | null {
  if (hostname === Host.DinoReader) {
    if (isDinoPrefixed(pathname)) return dinoHostCanonical(pathname);
    return pathname === `${PRIVACY_PATH}/` ? PRIVACY_PATH : null;
  }
  if (isWorkersDev(hostname) && pathname === DINO_PATH_PREFIX) return `${DINO_PATH_PREFIX}/`;
  const trimmed = withoutTrailingSlash(pathname);
  const isPage = trimmed === PRIVACY_PATH || trimmed === `${DINO_PATH_PREFIX}${PRIVACY_PATH}`;
  return isPage && trimmed !== pathname ? trimmed : null;
}

/**
 * Where a request must be redirected so relative links resolve and each page has
 * one URL (the query string is kept): bare `/dino` on workers.dev becomes
 * `/dino/`, `/privacy/` becomes `/privacy` (and its `/dino` twin), and the raw
 * `/dino/...` pages on the Dino host become `/` and `/privacy`. Null otherwise.
 */
export function canonicalRedirect(hostname: string, pathname: string, search = ""): string | null {
  const target = canonicalPath(hostname, pathname);
  return target === null ? null : `${target}${search}`;
}
