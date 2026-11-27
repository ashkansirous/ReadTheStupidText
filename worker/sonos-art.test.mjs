// Run: node --test worker/sonos-art.test.mjs (Slice 437: folder art URLs, the 128-character limit and the square cover crop).
import test from "node:test";
import assert from "node:assert/strict";

import { MAX_ART_URI, TILE_LANGUAGES, fitArt, folderArtUrl, languageTileUrl, keptSquareCover, squareCoverPath, squareCoverResponse } from "./sonos-art.ts";
import { Shelf } from "./sonos-labels.ts";
import { trackOf } from "./sonos.ts";

const DINO = "https://dinoreader.sirous.uk";
const WORKERS_DEV = "https://readthestupidtext.example-account.workers.dev";
const ID = "7d0e5a4e000040008000000000000001";

test("every shelf has a folder picture and every app language a tile, each within 128 characters", () => {
  for (const origin of [DINO, WORKERS_DEV]) {
    for (const shelf of Object.values(Shelf)) {
      const url = folderArtUrl(origin, shelf);
      assert.ok(url !== null && url.length <= MAX_ART_URI && url.startsWith(`${origin}/dino/sonos-art/`) && url.endsWith(".png"), url);
    }
    for (const code of TILE_LANGUAGES) assert.ok(languageTileUrl(origin, code).length <= MAX_ART_URI);
  }
  assert.equal(languageTileUrl(DINO, "xx"), null);
});

test("an art link over 128 characters is dropped, never cut", () => {
  assert.equal(fitArt("x".repeat(128)).length, 128);
  assert.equal(fitArt("x".repeat(129)), null);
  assert.equal(folderArtUrl(`https://${"a".repeat(120)}.test`, Shelf.Free), null);
  assert.equal(fitArt(null), null);
});

test("a book's square cover link keeps the version and asks for the crop, and is dropped when too long", () => {
  assert.equal(squareCoverPath("/api/stories/x/cover?v=2"), "/api/stories/x/cover?v=2&sq=1");
  assert.equal(squareCoverPath("/api/stories/x/cover"), "/api/stories/x/cover?sq=1");
  const item = { id: ID, title: "T", author: null, coverUrl: `/api/stories/${ID}/cover?v=2`, audio: { en: { version: 3, listenSeconds: 60, listenUrl: "/x.m4a" } } };
  const dino = trackOf(item, "en", DINO);
  assert.equal(dino.squareCoverUrl, `${DINO}/api/stories/${ID}/cover?v=2&sq=1`);
  assert.equal(dino.coverUrl, `${DINO}/api/stories/${ID}/cover?v=2`, "Alexa still gets the cover as it is");
  assert.ok(dino.squareCoverUrl.length <= MAX_ART_URI);
  const long = trackOf(item, "en", `https://${"a".repeat(60)}.workers.dev`);
  assert.equal(long.squareCoverUrl, null);
});

test("a square link over the limit leaves the plain cover link that still fits", () => {
  const path = `/api/stories/${ID}/cover?v=2`;
  const origin = `https://${"a".repeat(MAX_ART_URI - path.length - "https://".length - 2)}`;
  const track = trackOf({ id: ID, title: "T", author: null, coverUrl: path, audio: { en: { version: 3, listenSeconds: 60, listenUrl: "/x.m4a" } } }, "en", origin);
  assert.equal(track.squareCoverUrl, null);
  assert.equal(track.coverUrl, `${origin}${path}`, "sonos.ts then offers this link instead (trackFields)");
});

function coverRequest(query = "?v=2&sq=1", method = "GET") {
  return new Request(`${DINO}/api/stories/${ID}/cover${query}`, { method });
}

function original() {
  return new Response(new Uint8Array([1, 2, 3, 4]), { status: 200, headers: { "Content-Type": "image/jpeg", "Content-Length": "4", ETag: '"e"', "Cache-Control": "public, max-age=3600" } });
}

function fakeImages(seen, { fail = false } = {}) {
  return {
    input(stream) {
      assert.ok(stream instanceof ReadableStream);
      return {
        transform(options) {
          seen.push(options);
          return this;
        },
        async output(options) {
          seen.push(options);
          if (fail) throw new Error("no");
          return { contentType: () => "image/jpeg", image: () => new Response(new Uint8Array([9, 9])).body };
        },
      };
    },
  };
}

test("?sq=1 on a cover is cropped to a centred square JPEG that is kept for a day, never immutable", async () => {
  const seen = [];
  const out = await squareCoverResponse(coverRequest(), { IMAGES: fakeImages(seen) }, original());
  assert.deepEqual(seen[0], { width: 600, height: 600, fit: "cover", gravity: "center" });
  assert.deepEqual(seen[1], { format: "image/jpeg" });
  assert.deepEqual([...new Uint8Array(await out.arrayBuffer())], [9, 9]);
  assert.equal(out.headers.get("Cache-Control"), "public, max-age=86400");
  assert.equal(out.headers.get("ETag"), null, "the original's ETag does not describe the crop");
  assert.equal(out.headers.get("Content-Length"), null);
});

test("without the binding, without sq, on a HEAD, a 404 or a 206 the answer is untouched", async () => {
  const seen = [];
  const images = fakeImages(seen);
  const plain = original();
  assert.equal(await squareCoverResponse(coverRequest(), {}, plain), plain);
  assert.equal(await squareCoverResponse(coverRequest("?v=2"), { IMAGES: images }, plain), plain);
  assert.equal(await squareCoverResponse(coverRequest("?sq=1", "HEAD"), { IMAGES: images }, plain), plain);
  const missing = new Response(null, { status: 404 });
  assert.equal(await squareCoverResponse(coverRequest(), { IMAGES: images }, missing), missing);
  const partial = new Response("x", { status: 206 });
  assert.equal(await squareCoverResponse(coverRequest(), { IMAGES: images }, partial), partial);
  assert.equal(seen.length, 0);
});

test("a crop that fails serves the original cover bytes, not an error", async () => {
  const out = await squareCoverResponse(coverRequest(), { IMAGES: fakeImages([], { fail: true }) }, original());
  assert.equal(out.status, 200);
  assert.deepEqual([...new Uint8Array(await out.arrayBuffer())], [1, 2, 3, 4]);
  assert.equal(out.headers.get("Content-Type"), "image/jpeg");
});

test("only a cover path is cropped, whatever else carries sq=1", async () => {
  const seen = [];
  const other = new Request(`${DINO}/api/stories/${ID}/download?sq=1`);
  const plain = original();
  assert.equal(await squareCoverResponse(other, { IMAGES: fakeImages(seen) }, plain), plain);
  assert.equal(seen.length, 0);
});

function fakeCache() {
  const kept = new Map();
  return {
    kept,
    match: async (request) => kept.get(request.url)?.clone(),
    put: async (request, response) => void kept.set(request.url, response),
  };
}

test("a cropped cover is kept and the next request is answered from the cache before any R2 read (review 2 #1, #2)", async () => {
  const cache = fakeCache();
  const seen = [];
  const env = { IMAGES: fakeImages(seen) };
  assert.equal(await keptSquareCover(coverRequest(), env, cache), undefined, "a miss");
  const first = await squareCoverResponse(coverRequest(), env, original(), cache);
  assert.equal(first.headers.get("Cache-Control"), "public, max-age=86400");
  assert.equal(cache.kept.size, 1);
  const again = await keptSquareCover(coverRequest(), env, cache);
  assert.deepEqual([...new Uint8Array(await again.arrayBuffer())], [9, 9]);
  assert.equal(seen.length, 2, "one transform (two recorded calls) for two requests");
});

test("a Range or Authorization header does not change the cache key (review 2 #3)", async () => {
  const cache = fakeCache();
  const env = { IMAGES: fakeImages([]) };
  const odd = new Request(coverRequest().url, { headers: { Range: "bytes=0-9", Authorization: "x", Cookie: "a=b" } });
  await squareCoverResponse(odd, env, original(), cache);
  assert.deepEqual([...cache.kept.keys()], [coverRequest().url]);
  assert.ok(await keptSquareCover(coverRequest(), env, cache));
});

test("no cache, a cache that throws, no binding, a non-cover or a HEAD never answers from the cache", async () => {
  const env = { IMAGES: fakeImages([]) };
  assert.equal(await keptSquareCover(coverRequest(), env, undefined), undefined);
  assert.equal(await keptSquareCover(coverRequest(), env, { match: async () => { throw new Error("none"); }, put: async () => {} }), undefined);
  const cache = fakeCache();
  await squareCoverResponse(coverRequest(), env, original(), cache);
  assert.equal(await keptSquareCover(coverRequest(), {}, cache), undefined);
  assert.equal(await keptSquareCover(coverRequest("?v=2"), env, cache), undefined);
  assert.equal(await keptSquareCover(coverRequest("?v=2&sq=1", "HEAD"), env, cache), undefined);
});

test("a cache that fails to store still serves the cropped picture", async () => {
  const cache = { match: async () => undefined, put: async () => { throw new Error("full"); } };
  const out = await squareCoverResponse(coverRequest(), { IMAGES: fakeImages([]) }, original(), cache);
  assert.deepEqual([...new Uint8Array(await out.arrayBuffer())], [9, 9]);
});

test("the tile languages, the Worker's label sets and the tiles on disk are one list (review 1 #6)", async () => {
  const { LABEL_LANGUAGES } = await import("./sonos-labels.ts");
  const { readdirSync } = await import("node:fs");
  assert.deepEqual([...TILE_LANGUAGES].sort(), [...LABEL_LANGUAGES]);
  const tiles = readdirSync(new URL("../site/dino/sonos-art/", import.meta.url)).filter((name) => name.startsWith("lang-")).map((name) => name.slice(5, -4));
  assert.deepEqual(tiles.sort(), [...TILE_LANGUAGES].sort());
});
