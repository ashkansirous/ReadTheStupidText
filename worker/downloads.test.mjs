// Run: node --test worker/downloads.test.mjs (node 22.18+/24 strip the types of downloads.ts).
import test from "node:test";
import assert from "node:assert/strict";

import * as publisher from "../.github/scripts/publish-downloads.mjs";
import {
  apkKey,
  compareVersionsDescending,
  handleDownload,
  ifNoneMatchHits,
  ifRangeAllowsRange,
  isDownloadPath,
  isForwardableRange,
  manifestKey,
  newestVersion,
  parseRange,
  renderDownloadPage,
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
  for (const path of ["/download", "/download/", "/download/android/latest", "/download/android/1.2.3"]) assert.ok(isDownloadPath(path));
  for (const path of ["/", "/downloads", "/download/android"]) assert.ok(!isDownloadPath(path));
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
