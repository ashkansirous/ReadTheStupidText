// Slice 437: the pictures the Sonos browse tree shows. Folder and language tiles are static PNGs in site/dino/sonos-art/ (made once by
// tools/dino/sonos-art/make-art.mjs); a book's cover is square-cropped by the Worker when the `IMAGES` binding is present, else served as it is.
// Sonos asks for an absolute URI of at most 128 characters; a longer one is dropped (the row then has no picture).

import type { ShelfName } from "./sonos-labels.ts";

export const MAX_ART_URI = 128;
const ART_PATH = "/dino/sonos-art/";
const ART_EXTENSION = ".png";
const LANGUAGE_TILE_PREFIX = "lang-";
const SQUARE_PARAM = "sq";
const SQUARE_VALUE = "1";
const COVER_SUFFIX = "/cover";
const SQUARE_SIZE = 600;
const JPEG_TYPE = "image/jpeg";
/**
 * The cropped cover is kept a day, never `immutable`: `?v=` is the pack version, and the owner may replace a book's `cover.jpg`
 * without bumping it, so a replaced cover shows on Sonos within a day. (The cache key is read before R2, so an etag cannot key it.)
 */
const CROPPED_CACHE_CONTROL = "public, max-age=86400";
/** The Cache API's own `caches.default` (absent outside the Workers runtime). */
interface CoverCache {
  match(request: Request): Promise<Response | undefined>;
  put(request: Request, response: Response): Promise<void>;
}
/** The languages that have a tile (the 15 app languages); any other code has no picture. */
export const TILE_LANGUAGES: readonly string[] = ["ar", "da", "de", "en", "es", "fa", "fi", "fr", "ja", "nb", "nl", "pt", "ru", "sv", "zh"];
/** Headers that describe the original bytes and must not be copied onto the cropped picture. */
const ORIGINAL_ONLY_HEADERS = ["Content-Length", "Content-Range", "Accept-Ranges", "ETag", "Content-Encoding"] as const;

/** The URL itself when it fits Sonos's limit, else null. */
export function fitArt(url: string | null): string | null {
  return url !== null && url.length <= MAX_ART_URI ? url : null;
}

/** The square tile of a shelf (Bedtime moon, New sparkle, ...); the shelf's own name is the file name. */
export function folderArtUrl(origin: string, shelf: ShelfName): string | null {
  return fitArt(`${origin}${ART_PATH}${shelf}${ART_EXTENSION}`);
}

/** The tile with a language's two-letter code, null for a language with no tile. */
export function languageTileUrl(origin: string, code: string): string | null {
  return TILE_LANGUAGES.includes(code) ? fitArt(`${origin}${ART_PATH}${LANGUAGE_TILE_PREFIX}${code}${ART_EXTENSION}`) : null;
}

/** A cover link that asks for the square crop (`/api/stories/<id>/cover?v=2` becomes `...?v=2&sq=1`). */
export function squareCoverPath(coverPath: string): string {
  return `${coverPath}${coverPath.includes("?") ? "&" : "?"}${SQUARE_PARAM}=${SQUARE_VALUE}`;
}

function wantsSquareCover(request: Request): boolean {
  if (request.method !== "GET") return false;
  const url = new URL(request.url);
  return url.pathname.endsWith(COVER_SUFFIX) && url.searchParams.get(SQUARE_PARAM) === SQUARE_VALUE;
}

function withoutOriginalHeaders(source: Headers): Headers {
  const headers = new Headers(source);
  for (const name of ORIGINAL_ONLY_HEADERS) headers.delete(name);
  return headers;
}

/**
 * A `cover?sq=1` answer cropped to a centred square. Every other answer, a binding that is absent and a crop that fails are
 * returned as they are (the original cover), so Sonos always gets a picture.
 */
export async function squareCoverResponse(request: Request, env: { IMAGES?: ImagesBinding }, response: Response, cache: CoverCache | undefined = defaultCache()): Promise<Response> {
  if (env.IMAGES === undefined || response.status !== 200 || response.body === null || !wantsSquareCover(request)) return response;
  const bytes = await response.arrayBuffer();
  try {
    const cropped = await env.IMAGES.input(new Response(bytes).body as ReadableStream<Uint8Array>)
      .transform({ width: SQUARE_SIZE, height: SQUARE_SIZE, fit: "cover", gravity: "center" })
      .output({ format: JPEG_TYPE });
    const headers = withoutOriginalHeaders(response.headers);
    headers.set("Content-Type", cropped.contentType());
    headers.set("Cache-Control", CROPPED_CACHE_CONTROL);
    const answer = new Response(cropped.image(), { status: 200, headers });
    return await keep(cache, request, answer);
  } catch {
    // no-fallback: an uncropped cover is a fine picture for Sonos; nothing is logged (worker/CLAUDE.md)
    return new Response(bytes, { status: response.status, headers: response.headers });
  }
}

function defaultCache(): CoverCache | undefined {
  return (globalThis as { caches?: { default?: CoverCache } }).caches?.default;
}

/** The cache key: the bare URL as a plain GET, so a Range, Authorization or Cookie header never changes how it is keyed. */
function coverKey(request: Request): Request {
  return new Request(request.url, { method: "GET" });
}

/**
 * The kept crop of a `cover?sq=1` GET, or undefined (no binding, not a square cover request, no cache, a miss or a cache that
 * throws). Asked before the cover is read from R2, so a hit costs no R2 read and no transform.
 */
export async function keptSquareCover(request: Request, env: { IMAGES?: ImagesBinding }, cache: CoverCache | undefined = defaultCache()): Promise<Response | undefined> {
  if (env.IMAGES === undefined || cache === undefined || !wantsSquareCover(request)) return undefined;
  try {
    return await cache.match(coverKey(request));
  } catch {
    // no-fallback: a cache that cannot be read (workers.dev has none) is a miss; the cover is cropped again
    return undefined;
  }
}

/** Stores the cropped picture under the request's URL (one billed transform per cover and version) and returns it; a failed store still returns it. */
async function keep(cache: CoverCache | undefined, request: Request, answer: Response): Promise<Response> {
  if (cache === undefined) return answer;
  const copy = answer.clone();
  try {
    await cache.put(coverKey(request), copy);
  } catch {
    // no-fallback: the cropped picture is served either way; the next request just crops again
  }
  return answer;
}
