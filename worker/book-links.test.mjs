// Run: node --test worker/book-links.test.mjs (Slice 423: printed-book short links /book/<id> on the Dino host).
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

import { bookIdOf, bookLinkLocation, handleBookLink, isBookLinkPath } from "./book-links.ts";
import { canonicalRedirect, resolveSite } from "./downloads.ts";

const TARGET = "https://dinoreader.sirous.uk/";

test("a valid id forwards with ?book=<id>", () => {
  assert.equal(bookLinkLocation("/book/love-from-seagull-island", TARGET), `${TARGET}?book=love-from-seagull-island`);
});

test("a trailing slash gives the same forward", () => {
  assert.equal(bookLinkLocation("/book/abc-1/", TARGET), `${TARGET}?book=abc-1`);
});

test("an id that is not a plain id forwards to the bare target, never echoed", () => {
  for (const path of ["/book/", "/book/UPPER", "/book/a_b", "/book/a%20b", "/book/%3Cscript%3E", "/book/a/b", "/book/" + "a".repeat(65), "/book/a.b"]) {
    assert.equal(bookIdOf(path), null, path);
    assert.equal(bookLinkLocation(path, TARGET), TARGET, path);
  }
});

test("a 64-character id is accepted", () => {
  const id = "a".repeat(64);
  assert.equal(bookLinkLocation(`/book/${id}`, TARGET), `${TARGET}?book=${id}`);
});

test("handleBookLink answers a 302 with the location and sets no cookie", () => {
  const response = handleBookLink("/book/x1", TARGET);
  assert.equal(response.status, 302);
  assert.equal(response.headers.get("location"), `${TARGET}?book=x1`);
  assert.equal(response.headers.get("set-cookie"), null);
});

test("only /book/... paths are book links", () => {
  assert.equal(isBookLinkPath("/book/x"), true);
  assert.equal(isBookLinkPath("/books"), false);
  assert.equal(isBookLinkPath("/book"), false);
});

test("canonicalRedirect leaves /book/... alone on the Dino host", () => {
  assert.equal(canonicalRedirect("dinoreader.sirous.uk", "/book/love-from-seagull-island", ""), null);
});

test("the workers.dev twin /dino/book/<id> resolves to the Dino site path /book/<id>", () => {
  const site = resolveSite("readthestupidtext.example.workers.dev", "/dino/book/abc");
  assert.equal(site.path, "/book/abc");
  assert.equal(isBookLinkPath(site.path), true);
});

test("the readthestupidtext host keeps its own product, so the Dino-only handler skips it", () => {
  const site = resolveSite("readthestupidtext.sirous.uk", "/book/abc");
  assert.notEqual(site.product, resolveSite("dinoreader.sirous.uk", "/").product);
});

test("a percent-encoded valid id is decoded once; a failed decode means no ?book=", () => {
  assert.equal(bookLinkLocation("/book/abc%2D1", TARGET), `${TARGET}?book=abc-1`);
  assert.equal(bookLinkLocation("/book/%61bc", TARGET), `${TARGET}?book=abc`);
  assert.equal(bookIdOf("/book/%E0%A4%A"), null);
  assert.equal(bookLinkLocation("/book/%E0%A4%A", TARGET), TARGET);
});

test("a missing or invalid target falls back to the request's own origin root, never throws", () => {
  const origin = "https://dinoreader.sirous.uk";
  for (const bad of [undefined, "", "dinoreader.sirous.uk", "javascript:alert(1)"]) {
    assert.equal(bookLinkLocation("/book/x1", bad, origin), `${origin}/?book=x1`, String(bad));
    assert.equal(handleBookLink("/book/x1", bad, origin).headers.get("location"), `${origin}/?book=x1`, String(bad));
    assert.equal(bookLinkLocation("/book/", bad, origin), `${origin}/`, String(bad));
  }
});

// worker/index.ts uses extensionless imports, so node cannot load it here; the routing order is pinned by reading its source.
test("serveAsset routes /book/<id> on the Dino host after canonicalRedirect, before the story routes and the assets", () => {
  const source = readFileSync(new URL("./index.ts", import.meta.url), "utf8");
  const at = (needle) => {
    const index = source.indexOf(needle);
    assert.notEqual(index, -1, needle);
    return index;
  };
  const book = at("site.product === Product.DinoReader && isBookLinkPath(site.path)");
  assert.ok(source.slice(book).includes("handleBookLink(site.path, env.BOOK_LINK_TARGET, url.origin)"));
  assert.ok(at("canonicalRedirect(url.hostname") < book, "after canonicalRedirect");
  assert.ok(book < at("if (isStoryPath(site.path))"), "before the story routes");
  assert.ok(book < at("env.ASSETS.fetch("), "before the assets");
});
