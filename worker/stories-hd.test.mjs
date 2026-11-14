// Run: node --test worker/stories-hd.test.mjs (Slice 375 B: the high-res package of a store book, story-v<n>.hd.dino).
import test from "node:test";
import assert from "node:assert/strict";

import { groupFolders, handleStories, hdPackageVersionOf, offersHd, parseMeta, toListItem } from "./stories.ts";

const ID = "7d0e5a4e000040008000000000000002";
const FOLDER = `dinoreader/stories/${ID}/`;
const baseMeta = { schemaVersion: 1, title: "Lion and Mouse", language: "en", version: 2, bytes: 3000000 };
const withHd = { ...baseMeta, hdBytes: 10000000 };
const both = ["story-v2.dino", "story-v2.hd.dino"];

function fakeBucket(meta, files) {
  const keys = [...files.map((file) => `${FOLDER}${file}`), `${FOLDER}meta.json`];
  return {
    list: async ({ prefix }) => ({ objects: keys.filter((key) => key.startsWith(prefix)).map((key) => ({ key })), truncated: false }),
    get: async (key) => (key === `${FOLDER}meta.json` ? { json: async () => meta } : null),
  };
}

async function call(path, meta, files, method = "GET") {
  const served = [];
  const serve = async (_request, _bucket, file, cacheControl) => {
    served.push({ file, cacheControl });
    return new Response("pkg", { status: 200 });
  };
  const response = await handleStories(new Request(`https://x.test${path}`, { method }), { DOWNLOADS: fakeBucket(meta, files) }, "", serve);
  return { response, served };
}

test("a high-res package file name carries its version, and the standard name is not one", () => {
  assert.equal(hdPackageVersionOf("story-v3.hd.dino"), 3);
  for (const bad of ["story-v3.dino", "story-v0.hd.dino", "story-v3.hd.zip", "story-v03.hd.dino", "audio-fa-v1.zip"]) assert.equal(hdPackageVersionOf(bad), null, bad);
  assert.deepEqual(groupFolders(both.map((f) => `${FOLDER}${f}`)).get(ID).versions, [2], "the .hd.dino is never read as a standard package");
});

test("meta.hdBytes must be a positive integer", () => {
  assert.equal(parseMeta(withHd).hdBytes, 10000000);
  for (const bad of [0, -5, 1.5, "big", null]) assert.equal(parseMeta({ ...baseMeta, hdBytes: bad }).hdBytes, null, String(bad));
  assert.equal(parseMeta(baseMeta).hdBytes, null);
});

test("the list item gains hdDownloadUrl and hdBytes only when meta lists them and the folder holds the file at the listed version", async () => {
  const offered = (await (await call("/api/stories", withHd, both)).response.json()).stories[0];
  assert.equal(offered.hdDownloadUrl, `/api/stories/${ID}/download?v=2&size=hd`);
  assert.equal(offered.hdBytes, 10000000);
  assert.equal(offered.downloadUrl, `/api/stories/${ID}/download?v=2`);

  const notUploaded = (await (await call("/api/stories", withHd, ["story-v2.dino"])).response.json()).stories[0];
  assert.equal("hdDownloadUrl" in notUploaded, false, "listed in meta but never uploaded");
  const otherVersion = (await (await call("/api/stories", withHd, ["story-v2.dino", "story-v1.hd.dino"])).response.json()).stories[0];
  assert.equal("hdBytes" in otherVersion, false, "only the listed version counts");
  const noMeta = (await (await call("/api/stories", baseMeta, both)).response.json()).stories[0];
  assert.equal("hdDownloadUrl" in noMeta && "hdBytes" in noMeta, false, "uploaded but meta does not list it");
});

test("offersHd and toListItem agree, and an entry without held files offers none", () => {
  const meta = parseMeta(withHd);
  assert.equal(offersHd({ id: ID, meta, version: 2, hasCover: false }), false);
  assert.equal(offersHd({ id: ID, meta, version: 2, hasCover: false, heldHd: [2] }), true);
  assert.equal("hdDownloadUrl" in toListItem({ id: ID, meta, version: 2, hasCover: false }, "/dino"), false);
  assert.equal(toListItem({ id: ID, meta, version: 2, hasCover: false, heldHd: [2] }, "/dino").hdDownloadUrl, `/dino/api/stories/${ID}/download?v=2&size=hd`);
});

test("size=hd serves story-v<n>.hd.dino, immutable with ?v= and uncached without", async () => {
  const versioned = await call(`/api/stories/${ID}/download?v=2&size=hd`, withHd, both);
  assert.equal(versioned.response.status, 200);
  assert.equal(versioned.served[0].file.key, `${FOLDER}story-v2.hd.dino`);
  assert.equal(versioned.served[0].file.fileName, "story-v2.hd.dino");
  assert.match(versioned.served[0].cacheControl, /max-age=86400/);

  const newest = await call(`/api/stories/${ID}/download?size=hd`, withHd, both);
  assert.equal(newest.served[0].file.key, `${FOLDER}story-v2.hd.dino`);
  assert.equal(newest.served[0].cacheControl, "no-cache");
});

test("size=standard and no size serve the standard package, as before", async () => {
  for (const path of [`/api/stories/${ID}/download?v=2`, `/api/stories/${ID}/download?v=2&size=standard`]) {
    const { response, served } = await call(path, withHd, both);
    assert.equal(response.status, 200, path);
    assert.equal(served[0].file.key, `${FOLDER}story-v2.dino`, path);
  }
});

test("an unknown size is a 400 with a message, for GET and a bare HEAD, and serves nothing", async () => {
  for (const size of ["huge", "HD", "", "hd,standard"]) {
    const { response, served } = await call(`/api/stories/${ID}/download?v=2&size=${encodeURIComponent(size)}`, withHd, both);
    assert.equal(response.status, 400, size);
    assert.match((await response.json()).message, /Unknown picture size/);
    assert.equal(served.length, 0, size);
  }
  const head = await call(`/api/stories/${ID}/download?v=2&size=huge`, withHd, both, "HEAD");
  assert.equal(head.response.status, 400);
  assert.equal(await head.response.text(), "");
});

test("a missing high-res file is a 404, never the standard package in its place", async () => {
  for (const path of [`/api/stories/${ID}/download?v=2&size=hd`, `/api/stories/${ID}/download?size=hd`, `/api/stories/${ID}/download?v=1&size=hd`]) {
    const { response, served } = await call(path, baseMeta, ["story-v2.dino"]);
    assert.equal(response.status, 404, path);
    assert.equal(served.length, 0, path);
  }
  assert.equal((await call(`/api/stories/${ID}/download?v=1&size=hd`, withHd, both)).response.status, 404, "v1 hd was never uploaded");
});

test("a bad ?v= with size=hd is still a 404 and the cover ignores size", async () => {
  assert.equal((await call(`/api/stories/${ID}/download?v=x&size=hd`, withHd, both)).response.status, 404);
  const cover = await call(`/api/stories/${ID}/cover?size=hd`, withHd, [...both, "cover.jpg"]);
  assert.equal(cover.response.status, 200);
  assert.equal(cover.served[0].file.key, `${FOLDER}cover.jpg`);
});

test("a paid book's high-res package is behind the same gate as its standard package", async () => {
  const paid = { ...withHd, access: "paid", productId: "uk.sirous.dinoreader.book.lion" };
  const gateAnswers = [];
  const paidGate = async () => { gateAnswers.push("asked"); return "no-proof"; };
  const served = [];
  const serve = async (_r, _b, file) => { served.push(file.key); return new Response("pkg"); };
  const response = await handleStories(new Request(`https://x.test/api/stories/${ID}/download?v=2&size=hd`), { DOWNLOADS: fakeBucket(paid, both) }, "", serve, paidGate);
  assert.equal(gateAnswers.length, 1);
  assert.equal(response.status, 402);
  assert.deepEqual(served, []);
});
