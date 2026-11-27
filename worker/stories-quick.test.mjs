// Run: node --test worker/stories-quick.test.mjs (Slice 463: the quick first-book package, story-v<n>.quick.dino, and the quick copy of an audio pack).
import test from "node:test";
import assert from "node:assert/strict";

import { groupFolders, handleStories, offersQuick, parseAudio, parseMeta, quickAudioPackOf, quickPackageVersionOf } from "./stories.ts";

const ID = "7d0e5a4e000040008000000000000002";
const FOLDER = `dinoreader/stories/${ID}/`;
const baseMeta = { schemaVersion: 1, title: "Lion and Mouse", language: "en", version: 2, bytes: 4140000, hdBytes: 13100000 };
const withQuick = { ...baseMeta, quickBytes: 1180000 };
const files = ["story-v2.dino", "story-v2.hd.dino", "story-v2.quick.dino"];
const audioMeta = { ...withQuick, audio: { en: { version: 3, bytes: 1660000, quickBytes: 850000 } } };
const audioFiles = [...files, "audio-en-v3.zip", "audio-en-v3.quick.zip"];

function fakeBucket(meta, names) {
  const keys = [...names.map((file) => `${FOLDER}${file}`), `${FOLDER}meta.json`];
  return {
    list: async ({ prefix }) => ({ objects: keys.filter((key) => key.startsWith(prefix)).map((key) => ({ key })), truncated: false }),
    get: async (key) => (key === `${FOLDER}meta.json` ? { json: async () => meta } : null),
  };
}

async function call(path, meta, names) {
  const served = [];
  const serve = async (_request, _bucket, file, cacheControl) => {
    served.push({ file, cacheControl });
    return new Response("pkg", { status: 200 });
  };
  const response = await handleStories(new Request(`https://x.test${path}`), { DOWNLOADS: fakeBucket(meta, names) }, "", serve);
  return { response, served };
}

const listed = async (meta, names) => (await (await call("/api/stories", meta, names)).response.json()).stories[0];

test("a quick package file name carries its version and is neither a standard nor a high-res package", () => {
  assert.equal(quickPackageVersionOf("story-v3.quick.dino"), 3);
  for (const bad of ["story-v3.dino", "story-v3.hd.dino", "story-v0.quick.dino", "story-v03.quick.dino", "audio-en-v3.quick.zip"]) assert.equal(quickPackageVersionOf(bad), null, bad);
  const folder = groupFolders(files.map((f) => `${FOLDER}${f}`)).get(ID);
  assert.deepEqual(folder.versions, [2]);
  assert.deepEqual(folder.hdVersions, [2]);
  assert.deepEqual(folder.quickVersions, [2]);
});

test("a quick audio zip name carries its language and version and is not a standard pack", () => {
  assert.deepEqual(quickAudioPackOf("audio-en-v3.quick.zip"), { language: "en", version: 3 });
  assert.equal(quickAudioPackOf("audio-en-v3.zip"), null);
  const folder = groupFolders(["audio-en-v3.zip", "audio-en-v3.quick.zip"].map((f) => `${FOLDER}${f}`)).get(ID);
  assert.deepEqual(folder.audio, { en: [3] });
  assert.deepEqual(folder.audioQuick, { en: [3] });
});

test("meta quickBytes must be a positive integer, also inside an audio pack", () => {
  assert.equal(parseMeta(withQuick).quickBytes, 1180000);
  for (const bad of [0, -5, 1.5, "big", null]) assert.equal(parseMeta({ ...baseMeta, quickBytes: bad }).quickBytes, null, String(bad));
  assert.equal(parseAudio({ en: { version: 3, bytes: 5, quickBytes: 2 } }).en.quickBytes, 2);
  assert.equal("quickBytes" in parseAudio({ en: { version: 3, bytes: 5, quickBytes: -1 } }).en, false);
});

test("the list lists all three variants of a book: standard, high-res and quick", async () => {
  const item = await listed(withQuick, files);
  assert.equal(item.downloadUrl, `/api/stories/${ID}/download?v=2`);
  assert.equal(item.hdDownloadUrl, `/api/stories/${ID}/download?v=2&size=hd`);
  assert.equal(item.quickDownloadUrl, `/api/stories/${ID}/download?v=2&size=quick`);
  assert.equal(item.quickBytes, 1180000);
});

test("the quick keys are absent unless meta lists the size AND the folder holds the file at the listed version", async () => {
  const notUploaded = await listed(withQuick, ["story-v2.dino"]);
  assert.equal("quickDownloadUrl" in notUploaded || "quickBytes" in notUploaded, false, "listed in meta but never uploaded");
  const otherVersion = await listed(withQuick, ["story-v2.dino", "story-v1.quick.dino"]);
  assert.equal("quickBytes" in otherVersion, false, "only the listed version counts");
  const noMeta = await listed(baseMeta, files);
  assert.equal("quickDownloadUrl" in noMeta, false, "uploaded but meta does not list it");
  assert.equal(offersQuick({ id: ID, meta: parseMeta(withQuick), version: 2, hasCover: false }), false);
});

test("an audio pack lists quickUrl and quickBytes only when its quick zip is in the folder", async () => {
  const offered = (await listed(audioMeta, audioFiles)).audio.en;
  assert.equal(offered.url, `/api/stories/${ID}/audio/en?v=3`);
  assert.equal(offered.quickUrl, `/api/stories/${ID}/audio/en?v=3&size=quick`);
  assert.equal(offered.quickBytes, 850000);
  const notUploaded = (await listed(audioMeta, [...files, "audio-en-v3.zip"])).audio.en;
  assert.equal("quickUrl" in notUploaded || "quickBytes" in notUploaded, false);
});

test("size=quick serves the quick package, the standard and hd ones are unchanged, and a missing quick file is a 404", async () => {
  const quick = await call(`/api/stories/${ID}/download?v=2&size=quick`, withQuick, files);
  assert.equal(quick.served[0].file.key, `${FOLDER}story-v2.quick.dino`);
  assert.equal(quick.served[0].file.fileName, "story-v2.quick.dino");
  assert.equal((await call(`/api/stories/${ID}/download?v=2`, withQuick, files)).served[0].file.key, `${FOLDER}story-v2.dino`);
  assert.equal((await call(`/api/stories/${ID}/download?v=2&size=hd`, withQuick, files)).served[0].file.key, `${FOLDER}story-v2.hd.dino`);
  const missing = await call(`/api/stories/${ID}/download?v=2&size=quick`, withQuick, ["story-v2.dino"]);
  assert.equal(missing.response.status, 404);
  assert.equal(missing.served.length, 0);
  assert.equal((await call(`/api/stories/${ID}/download?v=2&size=tiny`, withQuick, files)).response.status, 400);
});

test("size=quick on an audio pack serves its quick zip for the listed version only", async () => {
  const quick = await call(`/api/stories/${ID}/audio/en?v=3&size=quick`, audioMeta, audioFiles);
  assert.equal(quick.served[0].file.key, `${FOLDER}audio-en-v3.quick.zip`);
  assert.equal((await call(`/api/stories/${ID}/audio/en?v=3`, audioMeta, audioFiles)).served[0].file.key, `${FOLDER}audio-en-v3.zip`);
  const noQuick = await call(`/api/stories/${ID}/audio/en?v=3&size=quick`, audioMeta, [...files, "audio-en-v3.zip"]);
  assert.equal(noQuick.response.status, 404);
  assert.equal(noQuick.served.length, 0);
});
