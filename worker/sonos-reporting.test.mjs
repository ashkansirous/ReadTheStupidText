// Run: node --test worker/sonos-reporting.test.mjs (Slice 436 A2: Sonos play reports at /sonos/v2.0/timePlayed).
import test from "node:test";
import assert from "node:assert/strict";

import { readFileSync } from "node:fs";

import { serveObject } from "./downloads.ts";
import { runFirst, stripJsonc } from "./jsonc-run-first.mjs";
import { MAX_COUNTED_PER_REQUEST, SONOS_REPORTING_PATH, countedSeconds, handleSonosReporting, isSonosReportingPath, parseReports } from "./sonos-reporting.ts";

const ID_A = "7d0e5a4e000040008000000000000001";

function bucket() {
  const folder = `dinoreader/stories/${ID_A}/`;
  const objects = new Map([
    [`${folder}meta.json`, Buffer.from(JSON.stringify({ schemaVersion: 1, title: "A", language: "en", version: 2, added: "2026-11-01", audio: { en: { version: 3, bytes: 3, listenSeconds: 540 } } }))],
    [`${folder}story-v2.dino`, Buffer.from("pkg")], [`${folder}cover.jpg`, Buffer.from("jpg")],
    [`${folder}audio-en-v3.zip`, Buffer.from("zip")], [`${folder}listen-en-v3.m4a`, Buffer.from("m4a")],
  ]);
  return {
    async list({ prefix }) { return { objects: [...objects.keys()].filter((key) => key.startsWith(prefix)).map((key) => ({ key })), truncated: false }; },
    async head(key) { return objects.has(key) ? { size: objects.get(key).length, httpEtag: '"e"' } : null; },
    async get(key) {
      const body = objects.get(key);
      return body ? { size: body.length, httpEtag: '"e"', body: new Response(body).body, json: async () => JSON.parse(body.toString()) } : null;
    },
  };
}

function usageOf(enabled = true) {
  const points = [];
  return { points, usage: { dataset: { writeDataPoint: (point) => points.push(point) }, telemetryEnabled: async () => enabled } };
}

async function report(body, usage, method = "POST") {
  const init = { method, body: method === "POST" ? (typeof body === "string" ? body : JSON.stringify(body)) : undefined };
  return handleSonosReporting(new Request("https://dino.test/sonos/v2.0/timePlayed", init), { DOWNLOADS: bucket() }, "", serveObject, usage);
}

const item = (type, millis, id = `track:en:${ID_A}`) => ({ objectId: id, type, durationPlayedMillis: millis });

test("the path is /sonos/v2.0/timePlayed", () => {
  assert.equal(isSonosReportingPath("/sonos/v2.0/timePlayed"), true);
  assert.equal(isSonosReportingPath("/sonos/timePlayed"), false);
  assert.equal(isSonosReportingPath("/sonos"), false);
});

test("a final report of 30 s or more is one reported row with the played seconds", async () => {
  const { points, usage } = usageOf();
  const response = await report({ items: [item("final", 120_000)] }, usage);
  assert.equal(response.status, 200);
  assert.equal(points.length, 1);
  assert.equal(points[0].blobs[0], "book_listened");
  assert.equal(points[0].blobs[1], ID_A);
  assert.equal(points[0].blobs[2], "en");
  assert.equal(points[0].blobs[6], "sonos");
  assert.equal(points[0].blobs[8], "reported");
  assert.equal(points[0].doubles[4], 120);
});

test("the seconds are capped at the book's length", async () => {
  const { points, usage } = usageOf();
  await report({ items: [item("final", 99_000_000)] }, usage);
  assert.equal(points[0].doubles[4], 540);
});

test("update reports, short plays and a telemetry-off switch write no row", async () => {
  const one = usageOf();
  assert.equal((await report({ items: [item("update", 300_000), item("final", 29_999)] }, one.usage)).status, 200);
  assert.equal(one.points.length, 0);
  const off = usageOf(false);
  assert.equal((await report({ items: [item("final", 300_000)] }, off.usage)).status, 200);
  assert.equal(off.points.length, 0);
});

test("an unknown or malformed item is skipped: the valid reports around it count, the answer is 200, and no fault row is written", async () => {
  const { points, usage } = usageOf();
  const response = await report({ items: [item("final", 60_000), item("final", 60_000, "track:en:nope"), { type: "final", objectId: `track:en:${ID_A}` }, "junk", item("final", 90_000)] }, usage);
  assert.equal(response.status, 200);
  assert.deepEqual(points.map((point) => point.blobs[0]), ["book_listened", "book_listened"]);
  assert.deepEqual(points.map((point) => point.doubles[4]), [60, 90]);
});

test("a final report with only the track's length counts nothing: only durationPlayedMillis is a played time", async () => {
  const { points, usage } = usageOf();
  const response = await report({ items: [{ objectId: `track:en:${ID_A}`, type: "final", durationMillis: 540_000, duration: 540, positionMillis: 540_000 }] }, usage);
  assert.equal(response.status, 200);
  assert.equal(points.length, 0);
});

test("the documented cloud queue shape (id, reportId, mediaUrl) is read", async () => {
  const { points, usage } = usageOf();
  const queueItem = { reportId: "0e5614b9-fcc7-4eec-b087-1892b7e64fa6", id: `track:en:${ID_A}`, mediaUrl: "http://media.example.com/a.m4a", type: "final", positionMillis: 500, durationPlayedMillis: 45_000 };
  assert.equal((await report({ items: [queueItem] }, usage)).status, 200);
  assert.equal(points.length, 1);
  assert.equal(points[0].doubles[4], 45);
});

test("one request writes at most MAX_COUNTED_PER_REQUEST rows", async () => {
  const { points, usage } = usageOf();
  await report({ items: Array.from({ length: 20 }, () => item("final", 60_000)) }, usage);
  assert.equal(points.length, MAX_COUNTED_PER_REQUEST);
});

test("a repeated reportId counts once: inside one request and on a Sonos retry (Cache API memory); a report with no reportId counts each time", async () => {
  const kept = new Map();
  globalThis.caches = { default: { match: async (key) => kept.get(key.url), put: async (key, response) => void kept.set(key.url, response) } };
  try {
    const { points, usage } = usageOf();
    const withId = (reportId) => ({ ...item("final", 60_000), reportId });
    await report({ items: [withId("r1"), withId("r1")] }, usage);
    assert.equal(points.length, 1, "twice in one request");
    await report({ items: [withId("r1"), withId("r2")] }, usage);
    assert.equal(points.length, 2, "the retried r1 is skipped, r2 is new");
    assert.match([...kept.values()][0].headers.get("Cache-Control"), /max-age=\d{5,}/);
    await report({ items: [item("final", 60_000)] }, usage);
    await report({ items: [item("final", 60_000)] }, usage);
    assert.equal(points.length, 4, "no reportId, counted as before");
  } finally {
    delete globalThis.caches;
  }
});

test("a reportId whose row write failed is not remembered: the retry counts it", async () => {
  const kept = new Map();
  globalThis.caches = { default: { match: async (key) => kept.get(key.url), put: async (key, response) => void kept.set(key.url, response) } };
  try {
    const { points, usage } = usageOf();
    const failing = { ...usage, dataset: { writeDataPoint: () => { throw new Error("write failed"); } } };
    const body = { items: [{ ...item("final", 60_000), reportId: "r9" }] };
    await report(body, failing);
    assert.equal(kept.size, 0, "nothing remembered after a failed write");
    await report(body, usage);
    assert.equal(points.length, 1, "the retry is counted");
  } finally {
    delete globalThis.caches;
  }
});

test("a bad payload is 400 and a wrong method 405", async () => {
  assert.equal((await report("not json", usageOf().usage)).status, 400);
  assert.equal((await report({ nothing: 1 }, usageOf().usage)).status, 400);
  assert.equal((await report([item("final", 60_000)], usageOf().usage)).status, 400);
  assert.equal((await report("x".repeat(20_000), usageOf().usage)).status, 400, "over the byte cap");
  assert.equal((await report(null, usageOf().usage, "GET")).status, 405);
});

test("both wrangler configs run the Worker first for the report path and its /dino twin (assets would answer 404)", () => {
  for (const name of ["wrangler.jsonc", "wrangler.workers-dev.jsonc"]) {
    const config = JSON.parse(stripJsonc(readFileSync(new URL(`../${name}`, import.meta.url), "utf8")));
    assert.ok(runFirst(config).includes(SONOS_REPORTING_PATH), `${name} lacks ${SONOS_REPORTING_PATH}`);
    assert.ok(runFirst(config).includes(`/dino${SONOS_REPORTING_PATH}`), `${name} lacks the /dino twin`);
  }
});

test("parseReports keeps only final reports; countedSeconds applies the 30 s bar", () => {
  assert.deepEqual(parseReports({ items: [item("update", 1000), item("final", 45_000)] }), { ok: true, reports: [{ id: `track:en:${ID_A}`, seconds: 45 }] });
  assert.equal(countedSeconds({ id: "x", seconds: 29.9 }, { seconds: 540 }), null);
  assert.equal(countedSeconds({ id: "x", seconds: 30 }, { seconds: 540 }), 30);
});
