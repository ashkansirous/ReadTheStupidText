// Run: node --test worker/stories-audio.test.mjs (Slice 246 B2: the per-language audio packs of a store book).
import test from "node:test";
import assert from "node:assert/strict";

import { audioPackOf, groupFolders, handleStories, offeredAudio, parseAudio, parseMeta, toListItem } from "./stories.ts";

const ID = "7d0e5a4e000040008000000000000001";
const FOLDER = `dinoreader/stories/${ID}/`;
const baseMeta = { schemaVersion: 1, title: "Goldilocks", language: "en", version: 2 };

function fakeBucket(meta, files) {
  const keys = [...files.map((file) => `${FOLDER}${file}`), `${FOLDER}meta.json`];
  return {
    list: async ({ prefix }) => ({ objects: keys.filter((key) => key.startsWith(prefix)).map((key) => ({ key })), truncated: false }),
    get: async (key) => (key === `${FOLDER}meta.json` ? { json: async () => meta } : null),
  };
}

async function call(path, meta, files) {
  const served = [];
  const serve = async (_request, _bucket, file, cacheControl) => {
    served.push({ file, cacheControl });
    return new Response("zip", { status: 200 });
  };
  const response = await handleStories(new Request(`https://x.test${path}`), { DOWNLOADS: fakeBucket(meta, files) }, "", serve);
  return { response, served };
}

const withAudio = { ...baseMeta, audio: { fa: { version: 3, bytes: 2500000 } } };
const files = ["story-v2.dino", "audio-fa-v3.zip"];

test("an audio pack file name carries its language and version", () => {
  assert.deepEqual(audioPackOf("audio-fa-v3.zip"), { language: "fa", version: 3 });
  assert.deepEqual(audioPackOf("audio-pt-br-v12.zip"), { language: "pt-br", version: 12 });
  for (const bad of ["audio-fa-v0.zip", "audio-FA-v1.zip", "audio-fa-v1.dino", "audio--v1.zip", "story-v1.dino", "audio-fa-v01.zip"]) assert.equal(audioPackOf(bad), null, bad);
});

test("the audio map keeps good codes and versions and drops the rest", () => {
  assert.deepEqual(parseAudio({ fa: { version: 2, bytes: 10 }, FA: { version: 5 }, " nb ": { version: 1 }, de: { version: 1 }, xx: { version: 0 }, "": { version: 1 }, no: "x" }), { fa: { version: 2, bytes: 10 }, de: { version: 1, bytes: null } });
  assert.equal(parseAudio({ fa: { version: -1 } }), null);
  assert.equal(parseAudio(["fa"]), null);
  assert.equal(parseAudio(undefined), null);
});

test("a meta without audio still parses, and lists audio as null", () => {
  const meta = parseMeta(baseMeta);
  assert.equal(meta.audio, null);
  assert.equal(toListItem({ id: ID, meta, version: 2, hasCover: false }, "").audio, null);
});

test("a listed pack that is not in the folder is not offered", () => {
  const meta = parseMeta(withAudio);
  assert.equal(offeredAudio(meta, groupFolders([`${FOLDER}story-v2.dino`]).get(ID)), null);
  assert.equal(offeredAudio(meta, groupFolders([`${FOLDER}audio-fa-v2.zip`]).get(ID)), null, "another version of the same language is not the listed one");
  assert.deepEqual(offeredAudio(meta, groupFolders(files.map((f) => `${FOLDER}${f}`)).get(ID)), { fa: { version: 3, bytes: 2500000 } });
});

test("the list item carries each offered pack with its link", async () => {
  const { response } = await call("/api/stories", withAudio, files);
  const [story] = (await response.json()).stories;
  assert.deepEqual(story.audio, { fa: { version: 3, bytes: 2500000, url: `/api/stories/${ID}/audio/fa?v=3` } });
});

test("the pack is served for the listed language and version, immutable", async () => {
  const { response, served } = await call(`/api/stories/${ID}/audio/fa?v=3`, withAudio, files);
  assert.equal(response.status, 200);
  assert.equal(served[0].file.key, `${FOLDER}audio-fa-v3.zip`);
  assert.equal(served[0].file.contentType, "application/zip");
  assert.equal(served[0].cacheControl, "public, max-age=86400");
});

test("a wrong version, a missing version, another language or a bad language is a 404", async () => {
  for (const path of [`/api/stories/${ID}/audio/fa?v=2`, `/api/stories/${ID}/audio/fa`, `/api/stories/${ID}/audio/fa?v=x`, `/api/stories/${ID}/audio/de?v=3`, `/api/stories/${ID}/audio/..?v=3`, `/api/stories/${ID}/audio?v=3`, `/api/stories/${ID}/audio/fa/extra?v=3`]) {
    const { response, served } = await call(path, withAudio, files);
    assert.equal(response.status, 404, path);
    assert.equal(served.length, 0, path);
  }
});

test("Slice 360: the one older pack version the folder still holds is served too, an older one or a version never uploaded is a 404", async () => {
  const held = ["story-v2.dino", "audio-fa-v1.zip", "audio-fa-v2.zip", "audio-fa-v3.zip"];
  const older = await call(`/api/stories/${ID}/audio/fa?v=2`, withAudio, held);
  assert.equal(older.response.status, 200);
  assert.equal(older.served[0].file.key, `${FOLDER}audio-fa-v2.zip`);
  assert.equal((await call(`/api/stories/${ID}/audio/fa?v=1`, withAudio, held)).response.status, 404);
  assert.equal((await call(`/api/stories/${ID}/audio/fa?v=4`, withAudio, held)).response.status, 404);
  assert.equal((await call(`/api/stories/${ID}/audio/fa?v=2`, withAudio, files)).response.status, 404, "v2 is not in the folder");
});

test("a pack that meta lists but nobody uploaded, and a book without audio, are a 404", async () => {
  assert.equal((await call(`/api/stories/${ID}/audio/fa?v=3`, withAudio, ["story-v2.dino"])).response.status, 404);
  assert.equal((await call(`/api/stories/${ID}/audio/fa?v=3`, baseMeta, files)).response.status, 404);
});

test("the package and cover routes still work, and an unknown third part is still a 404", async () => {
  assert.equal((await call(`/api/stories/${ID}/download?v=2`, withAudio, files)).response.status, 200);
  assert.equal((await call(`/api/stories/${ID}/download/fa`, withAudio, files)).response.status, 404);
});
