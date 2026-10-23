// The Android APK downloads (Slice 69). CI copies each signed build into the
// `DOWNLOADS` R2 bucket (.github/scripts/publish-downloads.mjs); this reads only
// that bucket, never GitHub. The key layout lives in download-keys.mjs, shared
// with the publisher; the manifest is uploaded last, so a half upload is never
// offered. "Latest" is the highest manifest version. Keep this file free of
// enums and parameter properties: worker/downloads.test.mjs runs it under
// node's type stripping.

import {
  DINO_PATH_PREFIX,
  Host,
  JSON_CONTENT_TYPE,
  LISTING_CACHE_MS,
  LIST_PAGE_SIZE,
  MANIFEST_SUFFIX,
  MAX_LIST_PAGES,
  NO_CACHE,
  Product,
  VERSIONED_CACHE,
  apkFileName,
  apkKey,
  manifestKey,
  manifestPrefix,
} from "./download-keys.mjs";

export { DINO_PATH_PREFIX, Host, Product, apkFileName, apkKey, manifestKey };

export const DOWNLOAD_PATH = "/download";
export const LATEST_PATH = "/download/android/latest";
export const VERSION_PATH_PREFIX = "/download/android/";
// Slice 74: /download/android is the newest APK of whichever product the host serves. Dino Reader
// also sends /download to its landing page.
export const ANDROID_PATH = "/download/android";

const APK_CONTENT_TYPE = "application/vnd.android.package-archive";
const HTML_CONTENT_TYPE = "text/html; charset=utf-8";

const HttpHeader = {
  ContentType: "Content-Type",
  CacheControl: "Cache-Control",
  ContentDisposition: "Content-Disposition",
  ContentLength: "Content-Length",
  ContentRange: "Content-Range",
  AcceptRanges: "Accept-Ranges",
  ETag: "ETag",
  Range: "Range",
  Allow: "Allow",
  IfNoneMatch: "If-None-Match",
  IfRange: "If-Range",
} as const;
const ErrorCode = {
  Unavailable: "downloads-unavailable",
  NotFound: "not-found",
  ApkUnavailable: "apk-unavailable",
  StoryUnavailable: "story-unavailable",
} as const;
type ErrorCode = (typeof ErrorCode)[keyof typeof ErrorCode];
const ALLOWED_METHODS = "GET, HEAD";
const BYTES_PREFIX = "bytes ";
const BYTES_UNSATISFIED_PREFIX = "bytes */";
const WEAK_PREFIX = "W/";
const ANY_ETAG = "*";
const HTTP_ERROR_STATUS = 400;
const VERSION_PATTERN = /^\d+\.\d+\.\d+$/;
const SINGLE_RANGE = /^bytes=(\d*)-(\d*)$/;
const STORE_URL = "https://apps.microsoft.com/detail/9NGT1BN1H92V";
const PLAY_URL = "https://play.google.com/apps/testing/uk.sirous.readthestupidtext";

/** The version a manifest key names, or null for any other key. */
export function versionFromManifestKey(key: string, product: Product = Product.ReadTheStupidText): string | null {
  const prefix = manifestPrefix(product);
  if (!key.startsWith(prefix) || !key.endsWith(MANIFEST_SUFFIX)) return null;
  const version = key.slice(prefix.length, key.length - MANIFEST_SUFFIX.length);
  return VERSION_PATTERN.test(version) ? version : null;
}

/** Highest first. */
export function compareVersionsDescending(a: string, b: string): number {
  const left = a.split(".").map(Number);
  const right = b.split(".").map(Number);
  for (let i = 0; i < 3; i += 1) {
    const difference = (right[i] ?? 0) - (left[i] ?? 0);
    if (difference !== 0) return difference;
  }
  return 0;
}

/** The newest version among manifest keys, ignoring keys that are not manifests. */
export function newestVersion(keys: readonly string[], product: Product = Product.ReadTheStupidText): string | null {
  const versions: string[] = [];
  for (const key of keys) {
    const version = versionFromManifestKey(key, product);
    if (version !== null) versions.push(version);
  }
  versions.sort(compareVersionsDescending);
  return versions[0] ?? null;
}

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

function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);
}

export interface PageModel {
  readonly version: string | null;
  readonly sizeBytes: number | null;
}

function megabytes(sizeBytes: number | null): string {
  return sizeBytes === null ? "" : ` (${(sizeBytes / 1_000_000).toFixed(1)} MB)`;
}

function androidBlock(model: PageModel): string {
  if (model.version === null) {
    return `<p class="note">No Android build has been published here yet. Android is also in <a href="${PLAY_URL}">Google Play closed testing</a>.</p>`;
  }
  const version = escapeHtml(model.version);
  return `<a class="btn" href="${LATEST_PATH}">Download for Android</a>
    <p class="note">Version ${version}${megabytes(model.sizeBytes)}. Allow installs from your browser when Android asks.</p>`;
}

/** Pure: the /download page for the newest build (or none). */
export function renderDownloadPage(model: PageModel): string {
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Download Read The Stupid Text</title>
  <style>
    :root { color-scheme: light dark; }
    body { font-family: "Segoe UI", system-ui, -apple-system, sans-serif; line-height: 1.6; max-width: 720px; margin: 0 auto; padding: 3rem 1.25rem 4rem; color: #1a1a2e; background: #fbfbfd; text-align: center; }
    @media (prefers-color-scheme: dark) { body { color: #e6e6ea; background: #1b1b1f; } a.btn { background: #5b8df0; } }
    a.btn { display: inline-block; margin: 1.5rem .4rem 0; padding: .7rem 1.4rem; border-radius: 10px; background: #3b6fe3; color: #fff; text-decoration: none; font-weight: 600; }
    a.btn.alt { background: transparent; border: 1px solid rgba(128,128,128,.4); color: inherit; }
    .note { font-size: .9rem; color: #777; margin-top: 1rem; }
    footer { margin-top: 3rem; font-size: .85rem; color: #888; }
    footer a { color: inherit; }
  </style>
</head>
<body>
  <h1>Download Read The Stupid Text</h1>
  <div>
    ${androidBlock(model)}
  </div>
  <div>
    <a class="btn alt" href="${STORE_URL}">Windows &mdash; Microsoft Store</a>
  </div>
  <footer><a href="/">Home</a> &middot; <a href="/privacy.html">Privacy Policy</a></footer>
</body>
</html>
`;
}

interface Manifest {
  readonly version: string;
  readonly sizeBytes: number | null;
}

interface CachedListing {
  readonly at: number;
  readonly keys: readonly string[];
}

/** Per-isolate listing cache, keyed by bucket object so a test's fake never sees another's. */
const listings = new WeakMap<R2Bucket, Map<Product, CachedListing>>();

async function listManifestKeys(bucket: R2Bucket, product: Product): Promise<string[]> {
  const keys: string[] = [];
  let cursor: string | undefined;
  for (let page = 0; page < MAX_LIST_PAGES; page += 1) {
    const result = await bucket.list({ prefix: manifestPrefix(product), cursor, limit: LIST_PAGE_SIZE });
    keys.push(...result.objects.map((object) => object.key));
    if (!result.truncated) break;
    cursor = result.cursor;
  }
  return keys;
}

function storeListing(bucket: R2Bucket, product: Product, keys: readonly string[]): void {
  const perProduct = listings.get(bucket) ?? new Map<Product, CachedListing>();
  perProduct.set(product, { at: Date.now(), keys });
  listings.set(bucket, perProduct);
}

/** Manifest keys, cached for a minute. Null when the bucket can't be read (never "empty"). */
async function currentKeys(bucket: R2Bucket, product: Product): Promise<readonly string[] | null> {
  const cached = listings.get(bucket)?.get(product);
  if (cached && Date.now() - cached.at < LISTING_CACHE_MS) return cached.keys;
  try {
    const keys = await listManifestKeys(bucket, product);
    storeListing(bucket, product, keys);
    return keys;
  } catch {
    return null;
  }
}

async function readManifest(bucket: R2Bucket, version: string, product: Product): Promise<Manifest | null> {
  try {
    const object = await bucket.get(manifestKey(version, product));
    if (!object) return null;
    const parsed: unknown = await object.json();
    const size = (parsed as { sizeBytes?: unknown }).sizeBytes;
    return { version, sizeBytes: typeof size === "number" ? size : null };
  } catch {
    return null;
  }
}

function textResponse(body: string, status: number, contentType: string, cacheControl = NO_CACHE): Response {
  return new Response(body, { status, headers: { [HttpHeader.ContentType]: contentType, [HttpHeader.CacheControl]: cacheControl } });
}

function errorResponse(code: ErrorCode, status: number): Response {
  return textResponse(JSON.stringify({ code }), status, JSON_CONTENT_TYPE);
}

function unavailable(): Response {
  return errorResponse(ErrorCode.Unavailable, 503);
}

function notFound(): Response {
  return errorResponse(ErrorCode.NotFound, 404);
}

function methodNotAllowed(): Response {
  return new Response(null, { status: 405, headers: { [HttpHeader.Allow]: ALLOWED_METHODS } });
}

async function downloadPage(request: Request, bucket: R2Bucket): Promise<Response> {
  const keys = await currentKeys(bucket, Product.ReadTheStupidText);
  if (keys === null) return unavailable();
  const version = newestVersion(keys, Product.ReadTheStupidText);
  // The version comes from the listing; the manifest only adds the size, so a
  // failed manifest read still shows the version and the button.
  const manifest = version === null ? null : await readManifest(bucket, version, Product.ReadTheStupidText);
  const html = renderDownloadPage({ version, sizeBytes: manifest?.sizeBytes ?? null });
  return textResponse(request.method === "HEAD" ? "" : html, 200, HTML_CONTENT_TYPE);
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

function apkFile(version: string, product: Product): ServedFile {
  return {
    key: apkKey(version, product),
    contentType: APK_CONTENT_TYPE,
    fileName: apkFileName(version, product),
    disposition: "attachment",
    unavailable: ErrorCode.ApkUnavailable,
  };
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

/** Streams one R2 object: a single Range honoured (206), anything else whole; conditional requests get one extra head. */
export async function serveObject(request: Request, bucket: R2Bucket, file: ServedFile, cacheControl: string): Promise<Response> {
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

function serveApk(request: Request, bucket: R2Bucket, version: string, cacheControl: string, product: Product): Promise<Response> {
  return serveObject(request, bucket, apkFile(version, product), cacheControl);
}

async function serveLatest(request: Request, bucket: R2Bucket, product: Product): Promise<Response> {
  const keys = await currentKeys(bucket, product);
  if (keys === null) return unavailable();
  const version = newestVersion(keys, product);
  if (version === null) return notFound();
  // The target changes with every release, so never let a browser or the edge keep it.
  return serveApk(request, bucket, version, NO_CACHE, product);
}

/** True for every path this module answers. */
export function isDownloadPath(pathname: string): boolean {
  return pathname === DOWNLOAD_PATH || pathname === `${DOWNLOAD_PATH}/` || pathname === ANDROID_PATH || pathname.startsWith(VERSION_PATH_PREFIX);
}

function isLatestPath(pathname: string): boolean {
  return pathname === LATEST_PATH || pathname === ANDROID_PATH;
}

function redirectTo(request: Request, path: string): Response {
  return Response.redirect(new URL(path, request.url).toString(), 302);
}

function route(request: Request, env: { DOWNLOADS?: R2Bucket }, product: Product, homePath: string): Promise<Response> | Response {
  if (request.method !== "GET" && request.method !== "HEAD") return methodNotAllowed();
  const bucket = env.DOWNLOADS;
  if (!bucket) return unavailable();
  const { pathname } = new URL(request.url);
  if (pathname === DOWNLOAD_PATH || pathname === `${DOWNLOAD_PATH}/`) {
    return product === Product.DinoReader ? redirectTo(request, homePath) : downloadPage(request, bucket);
  }
  if (isLatestPath(pathname)) return serveLatest(request, bucket, product);
  if (!pathname.startsWith(VERSION_PATH_PREFIX)) return notFound();
  const version = pathname.slice(VERSION_PATH_PREFIX.length);
  if (!VERSION_PATTERN.test(version)) return notFound();
  return serveApk(request, bucket, version, VERSIONED_CACHE, product);
}

/**
 * Routes /download, /download/android/latest and /download/android/<version>
 * and /download/android (the latest APK too). Dino Reader's /download goes to
 * `homePath`, its landing page (under the workers.dev prefix, not "/").
 * A HEAD error carries no body.
 */
export async function handleDownload(
  request: Request,
  env: { DOWNLOADS?: R2Bucket },
  product: Product = Product.ReadTheStupidText,
  homePath: string = ROOT_PATH,
): Promise<Response> {
  const response = await route(request, env, product, homePath);
  if (request.method !== "HEAD" || response.status < HTTP_ERROR_STATUS) return response;
  return new Response(null, { status: response.status, headers: response.headers });
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
