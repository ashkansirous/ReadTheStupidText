// Run: node --test worker/downloads.test.mjs (node 22.18+/24 strip the types of downloads.ts).
import test from "node:test";
import assert from "node:assert/strict";

import { readFileSync } from "node:fs";
import { redundantPaths, stripJsonc } from "./jsonc-run-first.mjs";
import {
  DINO_PATH_PREFIX,
  Host,
  Product,
  assetPageFor,
  canonicalRedirect,
  handleDownload,
  ifNoneMatchHits,
  ifRangeAllowsRange,
  isDownloadPath,
  isForwardableRange,
  isHiddenDinoPath,
  parseRange,
  resolveSite,
  serveObject,
} from "./downloads.ts";
import { completeIds, groupFolders, handleStories, isStoryPath, offeredVersion, packageVersionOf, parseMeta, parseStoryKey } from "./stories.ts";
import { storyCoverKey, storyMetaKey, storyPackageKey } from "./download-keys.mjs";

// A just-enough R2 bucket: list by prefix, get (with a single Range header), head.
function fakeBucket(objects) {
  return {
    async list({ prefix }) {
      const hits = [...objects.keys()].filter((key) => key.startsWith(prefix));
      return { objects: hits.map((key) => ({ key })), truncated: false };
    },
    async head(key) {
      const body = objects.get(key);
      return body ? { size: body.length, httpEtag: '"etag"' } : null;
    },
    async get(key, options) {
      const body = objects.get(key);
      if (!body) return null;
      const range = options?.range ? parseRange(options.range.get("Range"), body.length) : null;
      const slice = range && range !== "unsatisfiable" ? body.subarray(range.offset, range.offset + range.length) : body;
      return {
        size: body.length,
        httpEtag: '"etag"',
        range: range && range !== "unsatisfiable" ? { offset: range.offset, length: range.length } : undefined,
        body: new Response(slice).body,
        json: async () => JSON.parse(body.toString()),
      };
    },
  };
}

function get(path, init) {
  return new Request(`https://example.test${path}`, init);
}

test("parses a single byte range", () => {
  assert.deepEqual(parseRange("bytes=2-5", 10), { offset: 2, length: 4 });
  assert.deepEqual(parseRange("bytes=7-", 10), { offset: 7, length: 3 });
  assert.deepEqual(parseRange("bytes=-3", 10), { offset: 7, length: 3 });
  assert.equal(parseRange("bytes=10-", 10), "unsatisfiable");
  assert.equal(parseRange("bytes=0-1,4-5", 10), null);
  assert.equal(parseRange(null, 10), null);
});

const REDIRECTED_PATHS = ["/download", "/download/", "/download/android", "/download/android/latest", "/download/android/1.4.0", "/download/android/not-a-version"];

test("every download path redirects to the platforms section of the RTST site, for GET and HEAD", () => {
  for (const method of ["GET", "HEAD"]) {
    for (const path of REDIRECTED_PATHS) {
      const response = handleDownload(get(path, { method }));
      assert.equal(response.status, 302, `${method} ${path}`);
      assert.equal(response.headers.get("location"), "https://example.test/#platforms", `${method} ${path}`);
    }
  }
});

test("both landing pages still have the #platforms section the download redirect targets", () => {
  for (const page of ["../site/index.html", "../site/dino/index.html"]) {
    const html = readFileSync(new URL(page, import.meta.url), "utf8");
    assert.match(html, /id="platforms"/, page);
  }
});

test("the Dino host and its workers.dev /dino/ prefix redirect to the Dino platforms section", () => {
  for (const path of REDIRECTED_PATHS) {
    assert.equal(handleDownload(get(path), resolveSite(Host.DinoReader, path).home).headers.get("location"), "https://example.test/#platforms", path);
    const prefixed = `${DINO_PATH_PREFIX}${path}`;
    const site = resolveSite("x.workers.dev", prefixed);
    assert.ok(isDownloadPath(site.path), prefixed);
    assert.equal(handleDownload(get(prefixed), site.home).headers.get("location"), "https://example.test/dino/#platforms", prefixed);
  }
});

test("routes only the download paths", () => {
  for (const path of ["/download", "/download/", "/download/android", "/download/android/latest", "/download/android/1.2.3"]) assert.ok(isDownloadPath(path));
  for (const path of ["/", "/downloads"]) assert.ok(!isDownloadPath(path));
});

test("only a valid single range is forwarded to R2", () => {
  for (const ok of ["bytes=0-1", "bytes=5-5", "bytes=7-", "bytes=-3", "bytes=99-"]) assert.equal(isForwardableRange(ok), true, ok);
  for (const bad of ["bytes=-", "bytes=5-2", "bytes=-0", "bytes=0-1,4-5", "items=0-1", "junk"]) assert.equal(isForwardableRange(bad), false, bad);
  assert.equal(parseRange("bytes=5-2", 3), null);
});

test("the conditional matchers follow weak (If-None-Match) and strong (If-Range) comparison", () => {
  assert.equal(ifNoneMatchHits(null, '"a"'), false);
  assert.equal(ifNoneMatchHits('W/"a"', '"a"'), true);
  assert.equal(ifRangeAllowsRange(null, '"a"'), true);
  assert.equal(ifRangeAllowsRange('W/"a"', 'W/"a"'), false);
});

// ---- Slice 74: Dino Reader on its own host ----

const DINO = Product.DinoReader;

test("the host picks the site; an unknown host gets Read The Stupid Text", () => {
  assert.deepEqual(resolveSite(Host.DinoReader, "/privacy"), { product: DINO, path: "/privacy", home: "/" });
  assert.deepEqual(resolveSite(Host.ReadTheStupidText, "/privacy"), { product: Product.ReadTheStupidText, path: "/privacy", home: "/" });
  assert.equal(resolveSite("example.test", "/").product, Product.ReadTheStupidText);
  assert.equal(resolveSite("evil.example", "/dino/").product, Product.ReadTheStupidText);
});

test("workers.dev reaches the Dino site under /dino/, and only there", () => {
  const host = "readthestupidtext.acct.workers.dev";
  assert.deepEqual(resolveSite(host, "/dino"), { product: DINO, path: "/", home: "/dino/" });
  assert.deepEqual(resolveSite(host, "/dino/privacy"), { product: DINO, path: "/privacy", home: "/dino/" });
  assert.deepEqual(resolveSite(host, "/dino/download/android"), { product: DINO, path: "/download/android", home: "/dino/" });
  assert.equal(resolveSite(host, "/dinosaur").product, Product.ReadTheStupidText);
  assert.equal(resolveSite(host, "/privacy").product, Product.ReadTheStupidText);
  assert.equal(DINO_PATH_PREFIX, "/dino");
});

test("pages map to the product's own files", () => {
  assert.equal(assetPageFor(resolveSite(Host.DinoReader, "/")), "/dino/index.html");
  assert.equal(assetPageFor(resolveSite(Host.DinoReader, "/index.html")), "/dino/index.html");
  assert.equal(assetPageFor(resolveSite(Host.DinoReader, "/privacy/")), "/dino/privacy.html");
  assert.equal(assetPageFor(resolveSite(Host.DinoReader, "/privacy.html")), "/dino/privacy.html");
  assert.equal(assetPageFor(resolveSite(Host.ReadTheStupidText, "/")), "/index.html");
  assert.equal(assetPageFor(resolveSite(Host.ReadTheStupidText, "/privacy")), "/privacy.html");
  assert.equal(assetPageFor(resolveSite("x.workers.dev", "/dino/privacy")), "/dino/privacy.html");
  assert.equal(assetPageFor(resolveSite(Host.DinoReader, "/dino/icon.png")), null);
  assert.equal(assetPageFor(resolveSite(Host.ReadTheStupidText, "/nope")), null);
});

test("/download/android is a download path for every product", () => {
  assert.ok(isDownloadPath("/download/android"));
  assert.ok(isDownloadPath("/download/android/latest"));
});

test("bare /dino on workers.dev redirects to /dino/, so relative links resolve", () => {
  assert.equal(canonicalRedirect("x.workers.dev", "/dino"), "/dino/");
  assert.equal(canonicalRedirect("x.workers.dev", "/dino/"), null);
  assert.equal(canonicalRedirect(Host.DinoReader, "/dino"), "/");
  assert.equal(canonicalRedirect(Host.ReadTheStupidText, "/dino"), null);
});

test("canonical redirects keep the query string and drop trailing slashes on pages", () => {
  assert.equal(canonicalRedirect("x.workers.dev", "/dino", "?a=1"), "/dino/?a=1");
  assert.equal(canonicalRedirect(Host.ReadTheStupidText, "/privacy/", "?utm=x"), "/privacy?utm=x");
  assert.equal(canonicalRedirect(Host.ReadTheStupidText, "/privacy"), null);
  assert.equal(canonicalRedirect(Host.ReadTheStupidText, "/privacy.html"), null);
  assert.equal(canonicalRedirect("x.workers.dev", "/dino/privacy/", "?q"), "/dino/privacy?q");
  assert.equal(canonicalRedirect("x.workers.dev", "/dino/privacy"), null);
  assert.equal(canonicalRedirect(Host.DinoReader, "/privacy/"), "/privacy");
});

test("the Dino host redirects the raw /dino pages to its canonical URLs", () => {
  assert.equal(canonicalRedirect(Host.DinoReader, "/dino/index.html", "?a=1"), "/?a=1");
  assert.equal(canonicalRedirect(Host.DinoReader, "/dino/privacy.html"), "/privacy");
  assert.equal(canonicalRedirect(Host.DinoReader, "/dino/privacy"), "/privacy");
  assert.equal(canonicalRedirect(Host.DinoReader, "/dino/privacy/"), "/privacy");
  assert.equal(canonicalRedirect(Host.DinoReader, "/dino"), "/");
  assert.equal(canonicalRedirect(Host.DinoReader, "/dino/"), "/");
  assert.equal(canonicalRedirect(Host.DinoReader, "/"), null);
  assert.equal(canonicalRedirect(Host.DinoReader, "/privacy"), null);
});

test("only workers.dev and the Dino host serve the /dino prefix; the RTST host hides it", () => {
  assert.ok(isHiddenDinoPath(Host.ReadTheStupidText, "/dino"));
  assert.ok(isHiddenDinoPath(Host.ReadTheStupidText, "/dino/privacy"));
  assert.ok(isHiddenDinoPath("example.test", "/dino/"));
  assert.ok(!isHiddenDinoPath(Host.ReadTheStupidText, "/dinosaur"));
  assert.ok(!isHiddenDinoPath("x.workers.dev", "/dino/privacy"));
  assert.ok(!isHiddenDinoPath(Host.DinoReader, "/dino/privacy"));
});

test("the /download/* globs in run_worker_first cover /download/android (no redundant exact entry)", () => {
  for (const file of ["../wrangler.jsonc", "../wrangler.workers-dev.jsonc"]) {
    const config = JSON.parse(stripJsonc(readFileSync(new URL(file, import.meta.url), "utf8")));
    const runFirst = config.assets.run_worker_first;
    assert.deepEqual(redundantPaths(config), [], file);
    assert.ok(runFirst.includes("/download/*"), file);
    assert.ok(runFirst.includes("/dino/download/*"), file);
    assert.ok(!runFirst.includes("/download/android"), file);
    assert.ok(!runFirst.includes("/dino/download/android"), file);
  }
});

test("redundantPaths flags entries a trailing glob already covers, and passes a clean config", () => {
  const config = (entries) => ({ assets: { run_worker_first: entries } });
  assert.deepEqual(redundantPaths(config(["/download/*", "/download/android", "/privacy"])), ["/download/android"]);
  assert.deepEqual(redundantPaths(config(["/download/*", "/dino/download/*", "/", "/privacy"])), []);
  assert.deepEqual(redundantPaths(config(["/a/*", "/a/*"])), []);
  assert.deepEqual(redundantPaths({}), []);
});

// ---- Slice 92: the free Dino story store (worker/stories.ts) ----

const ID_A = "a".repeat(32);
const ID_B = "b".repeat(32);
const ID_C = "c".repeat(32);
const PACKAGE_BYTES = Buffer.from("0123456789");
const COVER_BYTES = Buffer.from("jpeg");

function metaJson(overrides = {}) {
  return Buffer.from(JSON.stringify({ schemaVersion: 1, title: "The Bear", language: "en", pageCount: 12, minutes: 6, added: "2026-10-01", ...overrides }));
}

function addStory(objects, id, { meta = metaJson(), versions = [1], cover = true } = {}) {
  if (meta) objects.set(storyMetaKey(id), meta);
  for (const version of versions) objects.set(storyPackageKey(id, version), PACKAGE_BYTES);
  if (cover) objects.set(storyCoverKey(id), COVER_BYTES);
}

test("age range and author pass through the list; a bad range or author is null", async () => {
  const objects = new Map();
  addStory(objects, ID_A, { meta: metaJson({ title: "Ranged", ageMin: 3, ageMax: 6, author: " Beatrix Potter " }) });
  addStory(objects, ID_B, { meta: metaJson({ title: "Plain" }) });
  addStory(objects, ID_C, { meta: metaJson({ title: "Odd", ageMin: 9, ageMax: 4, author: 7 }) });
  const body = await (await storyApi(get("/api/stories"), fakeBucket(objects))).json();
  const byTitle = Object.fromEntries(body.stories.map((story) => [story.title, story]));
  assert.deepEqual([byTitle.Ranged.ageMin, byTitle.Ranged.ageMax, byTitle.Ranged.author], [3, 6, "Beatrix Potter"]);
  assert.deepEqual([byTitle.Plain.ageMin, byTitle.Plain.ageMax, byTitle.Plain.author], [null, null, null]);
  assert.deepEqual([byTitle.Odd.ageMin, byTitle.Odd.ageMax, byTitle.Odd.author], [null, null, null]);
});

test("parseMeta age range: integers 0-18, both ends needed, min <= max", () => {
  const base = { schemaVersion: 1, title: "x", language: "en" };
  const range = (min, max) => { const m = parseMeta({ ...base, ageMin: min, ageMax: max }); return [m.ageMin, m.ageMax]; };
  assert.deepEqual(range(0, 18), [0, 18]);
  assert.deepEqual(range(5, 5), [5, 5]);
  assert.deepEqual(range(-1, 5), [null, null]);
  assert.deepEqual(range(5, 19), [null, null]);
  assert.deepEqual(range(3.5, 6), [null, null]);
  assert.deepEqual(range(3, undefined), [null, null]);
  assert.deepEqual(range("3", 6), [null, null]);
});

const storyApi = (request, bucket, prefix = "") => handleStories(request, { DOWNLOADS: bucket }, prefix, serveObject);

test("story keys: only <id>/<file> under the prefix, with a 32-hex id", () => {
  assert.deepEqual(parseStoryKey(`dinoreader/stories/${ID_A}/meta.json`), { id: ID_A, file: "meta.json" });
  assert.equal(parseStoryKey(`dinoreader/stories/${ID_A}/sub/meta.json`), null);
  assert.equal(parseStoryKey("dinoreader/stories/not-an-id/meta.json"), null);
  assert.equal(parseStoryKey(`android/${ID_A}/meta.json`), null);
  assert.equal(packageVersionOf("story-v3.dino"), 3);
  assert.equal(packageVersionOf("story-v0.dino"), null);
  assert.equal(packageVersionOf("story-v1.zip"), null);
});

test("a folder is complete only with meta.json and a package", () => {
  const keys = [storyMetaKey(ID_A), storyPackageKey(ID_A, 1), storyPackageKey(ID_B, 1), storyMetaKey(ID_C)];
  assert.deepEqual(completeIds(groupFolders(keys)), [ID_A]);
});

test("meta is checked; the offered version is meta's own when uploaded, else the highest", () => {
  assert.equal(parseMeta({ schemaVersion: 1, title: "", language: "en" }), null);
  assert.equal(parseMeta({ schemaVersion: "1", title: "x", language: "en" }), null);
  assert.equal(parseMeta(null), null);
  const meta = parseMeta({ schemaVersion: 1, title: "x", language: "en", pageCount: -3, version: 2 });
  assert.equal(meta.pageCount, null);
  assert.equal(offeredVersion(meta, [1, 2]), 2);
  assert.equal(offeredVersion(meta, [1]), null);
  assert.equal(offeredVersion({ ...meta, version: null }, [1, 4, 2]), 4);
});

test("/api/stories lists complete stories only, newest first, with versioned links", async () => {
  const objects = new Map();
  addStory(objects, ID_A, { meta: metaJson({ title: "Old", added: "2026-09-01" }) });
  addStory(objects, ID_B, { meta: metaJson({ title: "New", added: "2026-10-01" }), versions: [1, 2], cover: false });
  addStory(objects, ID_C, { meta: null });
  objects.set(storyMetaKey("d".repeat(32)), metaJson());
  addStory(objects, "e".repeat(32), { meta: Buffer.from("{not json") });
  const response = await storyApi(get("/api/stories"), fakeBucket(objects));
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("Cache-Control"), "public, max-age=60");
  const body = await response.json();
  assert.equal(body.schemaVersion, 1);
  assert.deepEqual(body.stories.map((story) => story.title), ["New", "Old"]);
  assert.equal(body.stories[0].version, 2);
  assert.equal(body.stories[0].coverUrl, null);
  assert.equal(body.stories[0].downloadUrl, `/api/stories/${ID_B}/download?v=2`);
  assert.equal(body.stories[1].coverUrl, `/api/stories/${ID_A}/cover?v=1`);
  assert.equal(body.stories[1].pageCount, 12);
});

test("credits pass through the list when meta has them, and are null when absent or malformed", async () => {
  const objects = new Map();
  addStory(objects, ID_A, { meta: metaJson({ title: "Credited", credits: "Beatrix Potter, 1902" }) });
  addStory(objects, ID_B, { meta: metaJson({ title: "Plain" }) });
  addStory(objects, ID_C, { meta: metaJson({ title: "Odd", credits: 7 }) });
  const body = await (await storyApi(get("/api/stories"), fakeBucket(objects))).json();
  const credits = Object.fromEntries(body.stories.map((story) => [story.title, story.credits]));
  assert.deepEqual(credits, { Credited: "Beatrix Potter, 1902", Plain: null, Odd: null });
  assert.equal(parseMeta({ schemaVersion: 1, title: "x", language: "en", credits: "y".repeat(201) }).credits, null);
});

test("on workers.dev the list links carry the /dino prefix", async () => {
  const objects = new Map();
  addStory(objects, ID_A);
  const body = await (await storyApi(get("/api/stories"), fakeBucket(objects), "/dino")).json();
  assert.equal(body.stories[0].downloadUrl, `/dino/api/stories/${ID_A}/download?v=1`);
});

test("an empty store is an empty list, an unreadable bucket is 503, a missing binding 503", async () => {
  const empty = await (await storyApi(get("/api/stories"), fakeBucket(new Map()))).json();
  assert.deepEqual(empty.stories, []);
  const broken = { async list() { throw new Error("r2 down"); } };
  assert.equal((await storyApi(get("/api/stories"), broken)).status, 503);
  assert.equal((await handleStories(get("/api/stories"), {}, "", serveObject)).status, 503);
});

test("download without ?v= is the newest package and never cached; with ?v= it is cached and ranged", async () => {
  const objects = new Map();
  addStory(objects, ID_A, { versions: [1, 2] });
  const bucket = fakeBucket(objects);
  const latest = await storyApi(get(`/api/stories/${ID_A}/download`), bucket);
  assert.equal(latest.status, 200);
  assert.equal(latest.headers.get("Cache-Control"), "no-cache");
  assert.match(latest.headers.get("Content-Disposition"), /story-v2\.dino/);
  assert.equal(Buffer.from(await latest.arrayBuffer()).toString(), "0123456789");
  const pinned = await storyApi(get(`/api/stories/${ID_A}/download?v=1`, { headers: { Range: "bytes=2-4" } }), bucket);
  assert.equal(pinned.status, 206);
  assert.equal(pinned.headers.get("Cache-Control"), "public, max-age=86400");
  assert.equal(pinned.headers.get("Content-Range"), "bytes 2-4/10");
  assert.equal(Buffer.from(await pinned.arrayBuffer()).toString(), "234");
});

test("cover is an inline JPEG; a story without one, an unknown id, a bad version and an unfinished folder are 404", async () => {
  const objects = new Map();
  addStory(objects, ID_A);
  addStory(objects, ID_B, { cover: false });
  addStory(objects, ID_C, { meta: null });
  const bucket = fakeBucket(objects);
  const cover = await storyApi(get(`/api/stories/${ID_A}/cover?v=1`), bucket);
  assert.equal(cover.status, 200);
  assert.equal(cover.headers.get("Content-Type"), "image/jpeg");
  assert.match(cover.headers.get("Content-Disposition"), /^inline/);
  assert.equal(cover.headers.get("Cache-Control"), "public, max-age=3600");
  const missing = [`/api/stories/${ID_B}/cover`, `/api/stories/${"f".repeat(32)}/download`, `/api/stories/${ID_A}/download?v=abc`,
    `/api/stories/${ID_C}/download`, `/api/stories/${ID_A}/other`, `/api/stories/${ID_A}/download/extra`, `/api/stories/${ID_A}`];
  for (const path of missing) {
    assert.equal((await storyApi(get(path), bucket)).status, 404, path);
  }
});

test("a pinned version that was never uploaded is 404; other methods are 405; HEAD errors have no body", async () => {
  const objects = new Map();
  addStory(objects, ID_A);
  const bucket = fakeBucket(objects);
  assert.equal((await storyApi(get(`/api/stories/${ID_A}/download?v=9`), bucket)).status, 404);
  assert.equal((await storyApi(get("/api/stories", { method: "POST" }), bucket)).status, 405);
  const head = await storyApi(get(`/api/stories/${ID_A}/download?v=9`, { method: "HEAD" }), bucket);
  assert.equal(head.status, 404);
  assert.equal(await head.text(), "");
  assert.equal((await storyApi(get("/api/stories", { method: "HEAD" }), bucket)).status, 200);
});

test("story paths route on the Dino host and under /dino on workers.dev, and both configs let the Worker run first", () => {
  assert.equal(isStoryPath("/api/stories"), true);
  assert.equal(isStoryPath(`/api/stories/${ID_A}/cover`), true);
  assert.equal(isStoryPath("/api/storiesx"), false);
  assert.equal(resolveSite(Host.DinoReader, "/api/stories").path, "/api/stories");
  const dev = resolveSite("x.workers.dev", "/dino/api/stories");
  assert.equal(dev.product, Product.DinoReader);
  assert.equal(dev.path, "/api/stories");
  for (const file of ["../wrangler.jsonc", "../wrangler.workers-dev.jsonc"]) {
    const config = JSON.parse(stripJsonc(readFileSync(new URL(file, import.meta.url), "utf8")));
    for (const path of ["/api/stories", "/api/stories/*", "/dino/api/stories", "/dino/api/stories/*"]) {
      assert.ok(config.assets.run_worker_first.includes(path), `${file} lacks ${path}`);
    }
  }
});

test("a failed meta.json read makes the list 503 rather than dropping the story", async () => {
  const objects = new Map();
  addStory(objects, ID_A);
  addStory(objects, ID_B);
  const bucket = fakeBucket(objects);
  const flaky = { list: bucket.list, head: bucket.head, async get(key, options) {
    if (key === storyMetaKey(ID_B)) throw new Error("r2 hiccup");
    return bucket.get(key, options);
  } };
  assert.equal((await storyApi(get("/api/stories"), flaky)).status, 503);
});

test("concurrent list requests share one rebuild", async () => {
  const objects = new Map();
  addStory(objects, ID_A);
  const inner = fakeBucket(objects);
  let lists = 0;
  const bucket = Object.create(inner);
  bucket.list = async (options) => {
    lists += 1;
    return inner.list(options);
  };
  const responses = await Promise.all([1, 2, 3, 4].map(() => storyApi(get("/api/stories"), bucket)));
  assert.deepEqual(responses.map((response) => response.status), [200, 200, 200, 200]);
  assert.equal(lists, 1);
});

test("a story uploaded after the listing was cached is still served by cover and download", async () => {
  const objects = new Map();
  addStory(objects, ID_A);
  const bucket = fakeBucket(objects);
  assert.equal((await storyApi(get("/api/stories"), bucket)).status, 200);
  addStory(objects, ID_B, { versions: [3] });
  assert.equal((await storyApi(get(`/api/stories/${ID_B}/download`), bucket)).status, 200);
  assert.equal((await storyApi(get(`/api/stories/${ID_B}/cover`), bucket)).status, 200);
});
