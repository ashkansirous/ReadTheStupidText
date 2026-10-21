// Run: node --test worker/downloads.test.mjs (node 22.18+/24 strip the types of downloads.ts).
import test from "node:test";
import assert from "node:assert/strict";

import { readFileSync } from "node:fs";
import * as publisher from "../.github/scripts/publish-downloads.mjs";
import {
  DINO_PATH_PREFIX,
  Host,
  Product,
  apkKey,
  assetPageFor,
  canonicalRedirect,
  compareVersionsDescending,
  handleDownload,
  ifNoneMatchHits,
  ifRangeAllowsRange,
  isDownloadPath,
  isForwardableRange,
  isHiddenDinoPath,
  manifestKey,
  newestVersion,
  parseRange,
  renderDownloadPage,
  resolveSite,
  versionFromManifestKey,
} from "./downloads.ts";

const APK_BYTES = Buffer.from("0123456789");

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

function bucketWith(versions) {
  const objects = new Map();
  for (const version of versions) {
    objects.set(apkKey(version), APK_BYTES);
    objects.set(manifestKey(version), Buffer.from(JSON.stringify({ version, sizeBytes: 10 })));
  }
  return fakeBucket(objects);
}

function get(path, init) {
  return new Request(`https://example.test${path}`, init);
}

test("orders versions numerically, not as text", () => {
  assert.deepEqual(["1.9.0", "1.10.0", "1.2.0"].sort(compareVersionsDescending), ["1.10.0", "1.9.0", "1.2.0"]);
});

test("reads versions out of manifest keys and ignores other keys", () => {
  assert.equal(versionFromManifestKey("manifests/android/1.4.0.json"), "1.4.0");
  assert.equal(versionFromManifestKey("manifests/android/latest.json"), null);
  assert.equal(versionFromManifestKey("android/1.4.0/x.apk"), null);
  assert.equal(newestVersion(["manifests/android/1.9.0.json", "manifests/android/1.10.0.json", "junk"]), "1.10.0");
  assert.equal(newestVersion([]), null);
});

test("parses a single byte range", () => {
  assert.deepEqual(parseRange("bytes=2-5", 10), { offset: 2, length: 4 });
  assert.deepEqual(parseRange("bytes=7-", 10), { offset: 7, length: 3 });
  assert.deepEqual(parseRange("bytes=-3", 10), { offset: 7, length: 3 });
  assert.equal(parseRange("bytes=10-", 10), "unsatisfiable");
  assert.equal(parseRange("bytes=0-1,4-5", 10), null);
  assert.equal(parseRange(null, 10), null);
});

test("routes only the download paths", () => {
  for (const path of ["/download", "/download/", "/download/android", "/download/android/latest", "/download/android/1.2.3"]) assert.ok(isDownloadPath(path));
  for (const path of ["/", "/downloads"]) assert.ok(!isDownloadPath(path));
});

test("the page names the newest version and escapes it", () => {
  const html = renderDownloadPage({ version: "1.4.0", sizeBytes: 12_300_000 });
  assert.match(html, /href="\/download\/android\/latest"/);
  assert.match(html, /Version 1\.4\.0 \(12\.3 MB\)/);
  assert.ok(!renderDownloadPage({ version: null, sizeBytes: null }).includes("/download/android/latest"));
  assert.ok(!renderDownloadPage({ version: "<b>", sizeBytes: null }).includes("<b>"));
});

test("/download lists the newest version from the manifests", async () => {
  const response = await handleDownload(get("/download"), { DOWNLOADS: bucketWith(["1.3.0", "1.10.0"]) });
  assert.equal(response.status, 200);
  assert.match(await response.text(), /Version 1\.10\.0/);
});

test("latest streams the newest APK with install headers", async () => {
  const response = await handleDownload(get("/download/android/latest"), { DOWNLOADS: bucketWith(["1.3.0", "1.4.0"]) });
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("content-type"), "application/vnd.android.package-archive");
  assert.equal(response.headers.get("content-disposition"), 'attachment; filename="ReadTheStupidText-1.4.0.apk"');
  assert.equal(response.headers.get("cache-control"), "no-cache");
  assert.equal(response.headers.get("content-length"), "10");
  assert.equal(await response.text(), "0123456789");
});

test("a Range request is answered 206 with Content-Range", async () => {
  const response = await handleDownload(get("/download/android/1.4.0", { headers: { Range: "bytes=2-5" } }), { DOWNLOADS: bucketWith(["1.4.0"]) });
  assert.equal(response.status, 206);
  assert.equal(response.headers.get("content-range"), "bytes 2-5/10");
  assert.equal(await response.text(), "2345");
});

test("HEAD returns the headers and no body", async () => {
  const response = await handleDownload(get("/download/android/1.4.0", { method: "HEAD" }), { DOWNLOADS: bucketWith(["1.4.0"]) });
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("content-length"), "10");
  assert.equal(await response.text(), "");
});

test("a missing version, a malformed version and an empty bucket are 404", async () => {
  const env = { DOWNLOADS: bucketWith(["1.4.0"]) };
  assert.equal((await handleDownload(get("/download/android/9.9.9"), env)).status, 404);
  assert.equal((await handleDownload(get("/download/android/not-a-version"), env)).status, 404);
  assert.equal((await handleDownload(get("/download/android/latest"), { DOWNLOADS: fakeBucket(new Map()) })).status, 404);
});

test("a missing binding or an unreadable bucket is 503, and other methods 405", async () => {
  assert.equal((await handleDownload(get("/download"), {})).status, 503);
  const broken = { list: async () => { throw new Error("down"); } };
  assert.equal((await handleDownload(get("/download"), { DOWNLOADS: broken })).status, 503);
  assert.equal((await handleDownload(get("/download", { method: "POST" }), { DOWNLOADS: bucketWith([]) })).status, 405);
});

test("the Worker and the publisher share one key layout", () => {
  for (const version of ["0.0.1", "1.4.0", "12.34.567"]) {
    assert.equal(apkKey(version), publisher.apkKey(version));
    assert.equal(manifestKey(version), publisher.manifestKey(version));
    assert.equal(versionFromManifestKey(manifestKey(version)), version);
  }
});

test("only a valid single range is forwarded to R2", () => {
  for (const ok of ["bytes=0-1", "bytes=5-5", "bytes=7-", "bytes=-3", "bytes=99-"]) assert.equal(isForwardableRange(ok), true, ok);
  for (const bad of ["bytes=-", "bytes=5-2", "bytes=-0", "bytes=0-1,4-5", "items=0-1", "junk"]) assert.equal(isForwardableRange(bad), false, bad);
  assert.equal(parseRange("bytes=5-2", 3), null);
});

test("an invalid Range is ignored and the whole file is served", async () => {
  for (const range of ["bytes=-", "bytes=5-2"]) {
    const response = await handleDownload(get("/download/android/1.4.0", { headers: { Range: range } }), { DOWNLOADS: bucketWith(["1.4.0"]) });
    assert.equal(response.status, 200, range);
    assert.equal(await response.text(), "0123456789");
  }
});

test("/download still offers the version and button when the manifest cannot be read", async () => {
  const bucket = bucketWith(["1.4.0"]);
  const failing = { ...bucket, list: bucket.list, get: async () => { throw new Error("boom"); } };
  const html = await (await handleDownload(get("/download"), { DOWNLOADS: failing })).text();
  assert.match(html, /Version 1\.4\.0\./);
  assert.match(html, /Download for Android/);
  assert.doesNotMatch(html, /MB/);
});

test("a ranged get that comes back without a range is served whole as 200, not 502", async () => {
  const bucket = bucketWith(["1.4.0"]);
  const omitsRange = { ...bucket, get: async (key) => ({ ...(await bucket.get(key)), range: undefined }) };
  const response = await handleDownload(get("/download/android/1.4.0", { headers: { Range: "bytes=2-5" } }), { DOWNLOADS: omitsRange });
  assert.equal(response.status, 200);
  assert.equal(await response.text(), "0123456789");
});

test("If-None-Match: a matching ETag is 304 for latest and versioned, a stale one is 200", async () => {
  const env = { DOWNLOADS: bucketWith(["1.4.0"]) };
  for (const path of ["/download/android/latest", "/download/android/1.4.0"]) {
    for (const header of ['"etag"', 'W/"etag"', '"x", "etag"', "*"]) {
      const response = await handleDownload(get(path, { headers: { "If-None-Match": header } }), env);
      assert.equal(response.status, 304, `${path} ${header}`);
      assert.equal(response.headers.get("etag"), '"etag"');
      assert.equal(await response.text(), "");
    }
    assert.equal((await handleDownload(get(path, { headers: { "If-None-Match": '"other"' } }), env)).status, 200);
  }
});

test("If-None-Match on a missing version is still 404", async () => {
  const response = await handleDownload(get("/download/android/9.9.9", { headers: { "If-None-Match": "*" } }), { DOWNLOADS: bucketWith(["1.4.0"]) });
  assert.equal(response.status, 404);
});

test("If-Range: a matching ETag keeps the range, anything else serves the whole file", async () => {
  const env = { DOWNLOADS: bucketWith(["1.4.0"]) };
  const ranged = (ifRange) => get("/download/android/1.4.0", { headers: { Range: "bytes=2-5", "If-Range": ifRange } });
  assert.equal((await handleDownload(ranged('"etag"'), env)).status, 206);
  for (const stale of ['"old"', 'W/"etag"', "Wed, 21 Oct 2026 07:28:00 GMT"]) {
    const response = await handleDownload(ranged(stale), env);
    assert.equal(response.status, 200, stale);
    assert.equal(await response.text(), "0123456789");
  }
});

test("the conditional matchers follow weak (If-None-Match) and strong (If-Range) comparison", () => {
  assert.equal(ifNoneMatchHits(null, '"a"'), false);
  assert.equal(ifNoneMatchHits('W/"a"', '"a"'), true);
  assert.equal(ifRangeAllowsRange(null, '"a"'), true);
  assert.equal(ifRangeAllowsRange('W/"a"', 'W/"a"'), false);
});

test("error responses to HEAD carry no body", async () => {
  const env = { DOWNLOADS: bucketWith(["1.4.0"]) };
  const missing = await handleDownload(get("/download/android/9.9.9", { method: "HEAD" }), env);
  assert.equal(missing.status, 404);
  assert.equal(await missing.text(), "");
  const down = await handleDownload(get("/download", { method: "HEAD" }), {});
  assert.equal(down.status, 503);
  assert.equal(await down.text(), "");
  const getMissing = await handleDownload(get("/download/android/9.9.9"), env);
  assert.match(await getMissing.text(), /not-found/);
});

// ---- Slice 74: Dino Reader on its own host ----

const DINO = Product.DinoReader;

function dinoBucketWith(versions) {
  const objects = new Map();
  for (const version of versions) {
    objects.set(apkKey(version, DINO), APK_BYTES);
    objects.set(manifestKey(version, DINO), Buffer.from(JSON.stringify({ version, sizeBytes: 10 })));
  }
  return fakeBucket(objects);
}

test("Dino Reader keys live under their own prefix, next to the existing layout", () => {
  assert.equal(apkKey("1.4.0", DINO), "dinoreader/android/1.4.0/DinoReader-1.4.0.apk");
  assert.equal(manifestKey("1.4.0", DINO), "dinoreader/manifests/android/1.4.0.json");
  assert.equal(apkKey("1.4.0"), "android/1.4.0/ReadTheStupidText-1.4.0.apk");
  assert.equal(versionFromManifestKey("dinoreader/manifests/android/1.4.0.json", DINO), "1.4.0");
  assert.equal(versionFromManifestKey("dinoreader/manifests/android/1.4.0.json"), null);
  assert.equal(versionFromManifestKey("manifests/android/1.4.0.json", DINO), null);
});

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

test("an exact /download/android entry is in run_worker_first so the Worker handles it", () => {
  for (const file of ["../wrangler.jsonc", "../wrangler.workers-dev.jsonc"]) {
    const text = readFileSync(new URL(file, import.meta.url), "utf8").replace(/^\s*\/\/.*$/gm, "");
    const runFirst = JSON.parse(text.replace(/,(\s*[}\]])/g, "$1")).assets.run_worker_first;
    assert.ok(runFirst.includes("/download/android"), file);
    assert.ok(runFirst.includes("/dino/download/android"), file);
  }
});

test("a failed listing read is not cached: the next request retries and succeeds", async () => {
  const objects = new Map([[manifestKey("1.0.0", DINO), Buffer.from(JSON.stringify({ version: "1.0.0", sizeBytes: 10 }))], [apkKey("1.0.0", DINO), APK_BYTES]]);
  const inner = fakeBucket(objects);
  let fail = true;
  const bucket = Object.create(inner);
  bucket.list = async (options) => {
    if (fail) throw new Error("r2 down");
    return inner.list(options);
  };
  assert.equal((await handleDownload(get("/download/android"), { DOWNLOADS: bucket }, DINO)).status >= 500, true);
  fail = false;
  assert.equal((await handleDownload(get("/download/android"), { DOWNLOADS: bucket }, DINO)).status, 200);
});

test("Dino latest streams the newest Dino APK from /download/android and /latest", async () => {
  for (const path of ["/download/android", "/download/android/latest"]) {
    const response = await handleDownload(get(path), { DOWNLOADS: dinoBucketWith(["1.3.0", "1.10.0"]) }, DINO);
    assert.equal(response.status, 200, path);
    assert.equal(response.headers.get("content-disposition"), 'attachment; filename="DinoReader-1.10.0.apk"');
    assert.equal(response.headers.get("cache-control"), "no-cache");
    assert.equal(await response.text(), "0123456789");
  }
});

test("Dino serves a specific version and ignores Read The Stupid Text builds", async () => {
  const bucket = dinoBucketWith(["1.4.0"]);
  const response = await handleDownload(get("/download/android/1.4.0"), { DOWNLOADS: bucket }, DINO);
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("content-disposition"), 'attachment; filename="DinoReader-1.4.0.apk"');
  const rtstOnly = await handleDownload(get("/download/android/latest"), { DOWNLOADS: bucketWith(["2.0.0"]) }, DINO);
  assert.equal(rtstOnly.status, 404);
  const dinoOnly = await handleDownload(get("/download/android/latest"), { DOWNLOADS: dinoBucketWith(["2.0.0"]) });
  assert.equal(dinoOnly.status, 404);
});

test("Dino's /download goes to its landing page (under the prefix too); /download/android is the latest for both", async () => {
  const redirect = await handleDownload(get("/download"), { DOWNLOADS: dinoBucketWith([]) }, DINO);
  assert.equal(redirect.status, 302);
  assert.equal(redirect.headers.get("location"), "https://example.test/");
  const prefixed = await handleDownload(get("/download"), { DOWNLOADS: dinoBucketWith([]) }, DINO, "/dino/");
  assert.equal(prefixed.headers.get("location"), "https://example.test/dino/");
  const rtst = await handleDownload(get("/download/android"), { DOWNLOADS: bucketWith(["1.4.0"]) });
  assert.equal(rtst.status, 200);
  assert.equal(rtst.headers.get("content-disposition"), 'attachment; filename="ReadTheStupidText-1.4.0.apk"');
});
