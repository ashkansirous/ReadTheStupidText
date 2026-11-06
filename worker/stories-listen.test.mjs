// Run: node --test worker/stories-listen.test.mjs (Slice 281: the listening track of a store book, served with the real serveObject so Range is tested end to end).
import test from "node:test";
import assert from "node:assert/strict";

import { parseRange, serveObject } from "./downloads.ts";
import { groupFolders, handleStories, listenTrackOf, offeredAudio, parseAudio, parseMeta } from "./stories.ts";

const ID = "7d0e5a4e000040008000000000000001";
const FOLDER = `dinoreader/stories/${ID}/`;
const TRACK = Buffer.from("0123456789");
const baseMeta = { schemaVersion: 1, title: "Goldilocks", language: "en", version: 2 };
const withListen = { ...baseMeta, audio: { en: { version: 3, bytes: 2500000, listenSeconds: 600 }, fa: { version: 1, bytes: 10 } } };
const FILES = new Map([
  [`${FOLDER}story-v2.dino`, Buffer.from("pkg")],
  [`${FOLDER}audio-en-v3.zip`, Buffer.from("zip")],
  [`${FOLDER}audio-fa-v1.zip`, Buffer.from("zip")],
  [`${FOLDER}listen-en-v3.m4a`, TRACK],
]);

// An R2 stand-in like R2's: a range outside the object makes get() throw, which serveObject turns into a 416.
function bucketOf(meta, files = FILES) {
  const objects = new Map(files);
  objects.set(`${FOLDER}meta.json`, Buffer.from(JSON.stringify(meta)));
  return {
    async list({ prefix }) {
      return { objects: [...objects.keys()].filter((key) => key.startsWith(prefix)).map((key) => ({ key })), truncated: false };
    },
    async head(key) {
      const body = objects.get(key);
      return body ? { size: body.length, httpEtag: '"etag"' } : null;
    },
    async get(key, options) {
      const body = objects.get(key);
      if (!body) return null;
      const range = options?.range ? parseRange(options.range.get("Range"), body.length) : null;
      if (range === "unsatisfiable") throw new Error("range not satisfiable");
      const slice = range ? body.subarray(range.offset, range.offset + range.length) : body;
      return {
        size: body.length,
        httpEtag: '"etag"',
        range: range ? { offset: range.offset, length: range.length } : undefined,
        body: new Response(slice).body,
        json: async () => JSON.parse(body.toString()),
      };
    },
  };
}

const call = (path, init, meta = withListen, files = FILES) =>
  handleStories(new Request(`https://x.test${path}`, init), { DOWNLOADS: bucketOf(meta, files) }, "", serveObject);
const LISTEN = `/api/stories/${ID}/listen/en?v=3`;

test("a listen track file name carries its language and version", () => {
  assert.deepEqual(listenTrackOf("listen-en-v3.m4a"), { language: "en", version: 3 });
  assert.deepEqual(listenTrackOf("listen-pt-br-v12.m4a"), { language: "pt-br", version: 12 });
  for (const bad of ["listen-en-v0.m4a", "listen-EN-v1.m4a", "listen-en-v1.zip", "audio-en-v1.zip", "listen--v1.m4a", "listen-en-v01.m4a"]) assert.equal(listenTrackOf(bad), null, bad);
});

test("the audio map keeps a track length only when it is a sane whole number", () => {
  assert.deepEqual(parseAudio({ en: { version: 1, listenSeconds: 90 } }), { en: { version: 1, bytes: null, listenSeconds: 90 } });
  assert.deepEqual(parseAudio({ en: { version: 1, listenSeconds: 0 } }), { en: { version: 1, bytes: null } });
  assert.deepEqual(parseAudio({ en: { version: 1, listenSeconds: 90.5 } }), { en: { version: 1, bytes: null } });
  assert.deepEqual(parseAudio({ en: { version: 1, listenSeconds: 999999 } }), { en: { version: 1, bytes: null } });
});

test("a listed track whose .m4a was never uploaded is not offered, the pack still is", () => {
  const meta = parseMeta(withListen);
  const folder = groupFolders([`${FOLDER}audio-en-v3.zip`]).get(ID);
  assert.deepEqual(offeredAudio(meta, folder), { en: { version: 3, bytes: 2500000 } });
});

test("the list item links the track only for a language that has one", async () => {
  const response = await call("/api/stories");
  const [story] = (await response.json()).stories;
  assert.equal(story.audio.en.listenUrl, `/api/stories/${ID}/listen/en?v=3`);
  assert.equal(story.audio.en.listenSeconds, 600);
  assert.equal(story.audio.fa.listenUrl, undefined);
  assert.equal(story.audio.fa.listenSeconds, undefined);
});

test("200: the whole track, audio/mp4, inline, immutable, ranges accepted", async () => {
  const response = await call(LISTEN);
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("Content-Type"), "audio/mp4");
  assert.match(response.headers.get("Content-Disposition"), /^inline; filename="listen-en-v3\.m4a"$/);
  assert.equal(response.headers.get("Cache-Control"), "public, max-age=86400");
  assert.equal(response.headers.get("Accept-Ranges"), "bytes");
  assert.equal(Buffer.from(await response.arrayBuffer()).toString(), "0123456789");
});

test("206: a byte range answers the slice with Content-Range, and an open range runs to the end", async () => {
  const ranged = await call(LISTEN, { headers: { Range: "bytes=2-4" } });
  assert.equal(ranged.status, 206);
  assert.equal(ranged.headers.get("Content-Range"), "bytes 2-4/10");
  assert.equal(ranged.headers.get("Content-Length"), "3");
  assert.equal(Buffer.from(await ranged.arrayBuffer()).toString(), "234");
  const open = await call(LISTEN, { headers: { Range: "bytes=8-" } });
  assert.equal(open.status, 206);
  assert.equal(open.headers.get("Content-Range"), "bytes 8-9/10");
});

test("416: a range past the end of the track", async () => {
  const response = await call(LISTEN, { headers: { Range: "bytes=50-60" } });
  assert.equal(response.status, 416);
  assert.equal(response.headers.get("Content-Range"), "bytes */10");
});

test("HEAD: headers and length, no body", async () => {
  const response = await call(LISTEN, { method: "HEAD" });
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("Content-Type"), "audio/mp4");
  assert.equal(response.headers.get("Content-Length"), "10");
  assert.equal(response.headers.get("Accept-Ranges"), "bytes");
  assert.equal((await response.arrayBuffer()).byteLength, 0);
});

test("404: a wrong or missing version, another language, a language without a track, a bad path", async () => {
  const paths = [
    `/api/stories/${ID}/listen/en?v=2`,
    `/api/stories/${ID}/listen/en`,
    `/api/stories/${ID}/listen/en?v=x`,
    `/api/stories/${ID}/listen/de?v=3`,
    `/api/stories/${ID}/listen/fa?v=1`,
    `/api/stories/${ID}/listen/..?v=3`,
    `/api/stories/${ID}/listen?v=3`,
    `/api/stories/${ID}/listen/en/extra?v=3`,
  ];
  for (const path of paths) {
    const response = await call(path);
    assert.equal(response.status, 404, path);
    assert.equal(response.headers.get("Cache-Control"), "no-cache", `${path}: a 404 is never cached as immutable`);
  }
  const unuploaded = await call(LISTEN, { method: "HEAD" }, withListen, new Map([...FILES].filter(([key]) => !key.endsWith(".m4a"))));
  assert.equal(unuploaded.status, 404, "listed but never uploaded");
  assert.equal(unuploaded.headers.get("Cache-Control"), "no-cache", "a listed-but-missing track is not cached as immutable");
  assert.equal((await call(LISTEN, undefined, baseMeta)).status, 404, "a book without audio");
});

test("the zip route is unchanged by the track", async () => {
  const response = await call(`/api/stories/${ID}/audio/en?v=3`);
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("Content-Type"), "application/zip");
});
