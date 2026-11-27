// Run: node --test worker/stories-catalogue.test.mjs (node 22.18+/24 strip the types of stories.ts).
// Slice 283 F1-F4: the built catalogue survives a failed or slow rebuild, is kept in the Cache API, reads every meta.json at once and logs why a rebuild failed.
import test from "node:test";
import assert from "node:assert/strict";

import { serveObject } from "./downloads.ts";
import { handleStories } from "./stories.ts";
import { storyMetaKey, storyPackageKey } from "./download-keys.mjs";

const MINUTE = 60_000;
const idOf = (n) => n.toString(16).padStart(32, "0");

function metaFor(n) {
  return Buffer.from(JSON.stringify({ schemaVersion: 1, title: `Book ${n}`, language: "en" }));
}

// A just-enough R2 bucket whose list and get can be made to fail or wait, and which counts what it served.
function catalogueBucket(count, { delayMs = 0 } = {}) {
  const objects = new Map();
  for (let n = 1; n <= count; n += 1) {
    objects.set(storyMetaKey(idOf(n)), metaFor(n));
    objects.set(storyPackageKey(idOf(n), 1), Buffer.from("pkg"));
  }
  const state = { fail: false, delayMs, lists: 0, active: 0, maxActive: 0 };
  const bucket = {
    state,
    async list({ prefix }) {
      state.lists += 1;
      if (state.delayMs > 0) await new Promise((resolve) => setTimeout(resolve, state.delayMs));
      if (state.fail) throw new Error("r2 down");
      return { objects: [...objects.keys()].filter((key) => key.startsWith(prefix)).map((key) => ({ key })), truncated: false };
    },
    async get(key) {
      state.active += 1;
      state.maxActive = Math.max(state.maxActive, state.active);
      await new Promise((resolve) => setTimeout(resolve, 5));
      state.active -= 1;
      const body = objects.get(key);
      return body ? { json: async () => JSON.parse(body.toString()) } : null;
    },
  };
  return bucket;
}

function fakeShared() {
  const held = new Map();
  return {
    held,
    async match(request) {
      const hit = held.get(request.url);
      return hit === undefined ? undefined : hit.clone();
    },
    async put(request, response) {
      held.set(request.url, response);
    },
  };
}

function deferred() {
  const work = [];
  return { work, defer: (promise) => work.push(promise) };
}

const list = (bucket, runtime) => handleStories(new Request("https://example.test/api/stories"), { DOWNLOADS: bucket }, "", serveObject, undefined, undefined, undefined, runtime);

function captureWarnings(t) {
  const lines = [];
  t.mock.method(console, "warn", (line) => lines.push(String(line)));
  return lines;
}

function moveClock(t, startAt) {
  let now = startAt;
  t.mock.method(Date, "now", () => now);
  return (ms) => { now += ms; };
}

test("F3: meta.json reads run at a fixed concurrency of 24, not all at once", async () => {
  const bucket = catalogueBucket(60);
  const body = await (await list(bucket)).json();
  assert.equal(body.stories.length, 60);
  assert.equal(bucket.state.maxActive, 24);
});

test("review 1: the Sonos path never writes the apps' isolate cache", async () => {
  const bucket = catalogueBucket(2);
  await list(bucket, { defer: deferred().defer, shared: fakeShared() });
  const afterSonos = bucket.state.lists;
  await list(bucket);
  assert.equal(bucket.state.lists, afterSonos + 1, "the apps' first list still builds its own");
  await list(bucket);
  assert.equal(bucket.state.lists, afterSonos + 1, "and then caches it for a minute");
});

test("review 4: the Sonos isolate's own list older than 5 min is treated as not held", async (t) => {
  captureWarnings(t);
  const advance = moveClock(t, Date.now());
  const bucket = catalogueBucket(2);
  const runtime = { defer: deferred().defer };
  await list(bucket, runtime);
  advance(6 * MINUTE);
  bucket.state.fail = true;
  assert.equal((await list(bucket, runtime)).status, 503, "no own list older than 5 min, no shared copy: not served");
});

test("F1: a failed rebuild serves the last good list and says why (F4)", async (t) => {
  const warnings = captureWarnings(t);
  const advance = moveClock(t, Date.now());
  const bucket = catalogueBucket(3);
  const runtime = { defer: deferred().defer, shared: fakeShared() };
  const first = await (await list(bucket, runtime)).json();
  advance(10 * MINUTE);
  bucket.state.fail = true;
  const response = await list(bucket, runtime);
  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), first);
  assert.equal(warnings.filter((line) => line.includes("catalogue rebuild failed: Error")).length, 1, "one line per failure");
});

test("F5: after a failed rebuild R2 is left alone for 30 s, then tried again", async (t) => {
  captureWarnings(t);
  const advance = moveClock(t, Date.now());
  const bucket = catalogueBucket(2);
  const runtime = { defer: deferred().defer, shared: fakeShared() };
  await list(bucket, runtime);
  advance(10 * MINUTE);
  bucket.state.fail = true;
  await list(bucket, runtime);
  const afterFailure = bucket.state.lists;
  advance(20_000);
  await list(bucket, runtime);
  assert.equal(bucket.state.lists, afterFailure, "no new R2 list inside the back-off");
  advance(11_000);
  bucket.state.fail = false;
  await list(bucket, runtime);
  assert.equal(bucket.state.lists, afterFailure + 1, "tried again after the back-off");
});

test("F1: with no runtime a failed rebuild is a 503 and the next call rebuilds, exactly as on main", async (t) => {
  const warnings = captureWarnings(t);
  const advance = moveClock(t, Date.now());
  const bucket = catalogueBucket(2);
  assert.equal((await list(bucket)).status, 200);
  advance(2 * MINUTE);
  bucket.state.fail = true;
  assert.equal((await list(bucket)).status, 503, "no stale list for the apps");
  assert.equal((await list(bucket)).status, 503, "no back-off for the apps");
  bucket.state.fail = false;
  const recovered = await list(bucket);
  assert.equal(recovered.status, 200, "no stuck pending rebuild");
  assert.equal((await recovered.json()).stories.length, 2);
  assert.deepEqual(warnings, [], "the apps' path logs nothing");
});

test("F2: a shared copy from another schema version or with a broken entry is ignored", async () => {
  const shared = fakeShared();
  const key = "https://catalogue.internal/dino/stories-entries/v1";
  const body = (extra) => new Response(JSON.stringify({ at: Date.now(), entries: [{}], ...extra }));
  for (const copy of [body({ v: 0 }), body({ v: 1 }), body({ v: 1, entries: "x" })]) {
    shared.held.set(key, copy);
    const bucket = catalogueBucket(2);
    const response = await list(bucket, { defer: deferred().defer, shared });
    assert.equal((await response.json()).stories.length, 2, "rebuilt from R2, not served from the broken copy");
  }
});

test("F3: a slower, older build never replaces a newer shared copy", async (t) => {
  const advance = moveClock(t, Date.now());
  const shared = fakeShared();
  const url = new Request("https://catalogue.internal/dino/stories-entries/v1");
  const newer = deferred();
  await list(catalogueBucket(3), { defer: newer.defer, shared });
  await Promise.all(newer.work);
  const stored = await (await shared.match(url)).json();
  advance(-5000);
  const older = deferred();
  await list(catalogueBucket(1), { defer: older.defer, shared });
  await Promise.all(older.work);
  const after = await (await shared.match(url)).json();
  assert.equal(after.at, stored.at);
  assert.equal(after.entries.length, 3);
});

test("F9: a shared copy older than an hour is not used", async (t) => {
  const advance = moveClock(t, Date.now());
  const shared = fakeShared();
  const first = deferred();
  await list(catalogueBucket(2), { defer: first.defer, shared });
  await Promise.all(first.work);
  advance(61 * MINUTE);
  const body = await (await list(catalogueBucket(3), { defer: deferred().defer, shared })).json();
  assert.equal(body.stories.length, 3, "rebuilt from R2, not the hour-old copy");
});

test("F4: with no list ever read a failed rebuild is a 503 that names its cause in the log", async (t) => {
  const warnings = captureWarnings(t);
  const bucket = catalogueBucket(2);
  bucket.state.fail = true;
  assert.equal((await list(bucket, { defer: deferred().defer })).status, 503);
  assert.ok(warnings.some((line) => line.includes("catalogue rebuild failed: Error")));
  assert.ok(warnings.every((line) => !line.includes("r2 down")), "only the error's name is logged");
});

test("F1: a rebuild past the budget answers with the last good list while it finishes", async (t) => {
  const warnings = captureWarnings(t);
  const advance = moveClock(t, Date.now());
  const bucket = catalogueBucket(3);
  const later = deferred();
  const runtime = { defer: later.defer, rebuildBudgetMs: 20, shared: fakeShared() };
  const first = await (await list(bucket, runtime)).json();
  advance(10 * MINUTE);
  bucket.state.delayMs = 300;
  const started = performance.now();
  const body = await (await list(bucket, runtime)).json();
  assert.ok(performance.now() - started < 250, "answered before the slow rebuild finished");
  assert.deepEqual(body, first);
  await Promise.all(later.work);
});

test("F2: a new isolate answers from the shared Cache API copy without waiting for R2", async () => {
  const shared = fakeShared();
  const first = deferred();
  const warm = catalogueBucket(4);
  const built = await (await list(warm, { defer: first.defer, shared })).json();
  await Promise.all(first.work);
  assert.equal(shared.held.size, 1, "the built list was put into the shared cache");

  const second = deferred();
  const cold = catalogueBucket(4, { delayMs: 300 });
  const started = performance.now();
  const body = await (await list(cold, { defer: second.defer, shared })).json();
  assert.ok(performance.now() - started < 250, "no rebuild on the request path");
  assert.deepEqual(body, built);
  await Promise.all(second.work);
  assert.equal(cold.state.lists, 1, "the refresh ran after the answer");
});

test("F2: a broken Cache API never fails the list", async (t) => {
  const warnings = captureWarnings(t);
  const shared = { async match() { throw new Error("cache down"); }, async put() { throw new Error("cache down"); } };
  const body = await (await list(catalogueBucket(2), { shared })).json();
  assert.equal(body.stories.length, 2);
  assert.ok(warnings.some((line) => line.includes("shared read failed")));
  assert.ok(warnings.some((line) => line.includes("shared write failed")));
});

test("without a runtime the list is cached for a minute and rebuilt after it, as before", async (t) => {
  const advance = moveClock(t, Date.now());
  const bucket = catalogueBucket(2);
  await list(bucket);
  await list(bucket);
  assert.equal(bucket.state.lists, 1);
  advance(2 * MINUTE);
  await list(bucket);
  assert.equal(bucket.state.lists, 2);
});
