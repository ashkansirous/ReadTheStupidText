// Slice 423: short links for printed books, `/book/<book-id>` on the Dino host (workers.dev: /dino/book/<book-id>). A printed QR code
// can never change, so the link stays here and only BOOK_LINK_TARGET (wrangler `vars`) says where it forwards. No catalogue lookup: the
// id is checked by its characters only, and an odd or unknown id still forwards to the target without `?book=`, never a 404.

export const BOOK_LINK_PREFIX = "/book/";
export const BOOK_QUERY_PARAM = "book";
export const BOOK_LINK_STATUS = 302;
const BOOK_ID = /^[a-z0-9-]{1,64}$/;
const TRAILING_SLASHES = /\/+$/;

/** True for `/book/...` (the part after the Dino site's own prefix). */
export function isBookLinkPath(pathname: string): boolean {
  return pathname.startsWith(BOOK_LINK_PREFIX);
}

/** The book id of a `/book/<id>` or `/book/<id>/` path, or null when it is not a plain id (never echoed back). */
export function bookIdOf(pathname: string): string | null {
  const decoded = decodeSegment(pathname.slice(BOOK_LINK_PREFIX.length));
  if (decoded === null) return null;
  const id = decoded.replace(TRAILING_SLASHES, "");
  return BOOK_ID.test(id) ? id : null;
}

/** The segment percent-decoded once (so `%2D` counts as `-`); null when it is not valid percent-encoding. */
function decodeSegment(segment: string): string | null {
  try {
    return decodeURIComponent(segment);
  } catch {
    return null;
  }
}

/** The configured target when it is an absolute http(s) URL, else the request's own origin root (a printed code must never 500). */
export function resolveTarget(target: string | undefined, fallbackOrigin: string): string {
  try {
    const parsed = new URL(target ?? "");
    if (parsed.protocol === "https:" || parsed.protocol === "http:") return parsed.toString();
  } catch {
    // Missing or not an absolute URL: use the fallback below.
  }
  return `${fallbackOrigin}/`;
}

/** Where a book link forwards to: the target with `?book=<id>` for a valid id, the bare target otherwise. */
export function bookLinkLocation(pathname: string, target: string | undefined, fallbackOrigin = ""): string {
  const resolved = resolveTarget(target, fallbackOrigin);
  const id = bookIdOf(pathname);
  if (id === null) return resolved;
  const location = new URL(resolved);
  location.searchParams.set(BOOK_QUERY_PARAM, id);
  return location.toString();
}

/** The 302 answer for a book link. A temporary redirect, so browsers and link checkers never keep it for good. */
export function handleBookLink(pathname: string, target: string | undefined, fallbackOrigin = ""): Response {
  return Response.redirect(bookLinkLocation(pathname, target, fallbackOrigin), BOOK_LINK_STATUS);
}
