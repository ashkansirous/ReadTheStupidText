// Run: node --test worker/sonos-reporting.test.mjs (Slice 436 A2: Sonos play reports at /sonos/timePlayed).
import test from "node:test";
import assert from "node:assert/strict";

import { serveObject } from "./downloads.ts";
import { countedSeconds, handleSonosReporting, isSonosReportingPath, parseReports } from "./sonos-reporting.ts";

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
  return handleSonosReporting(new Request("https://dino.test/sonos/timePlayed", init), { DOWNLOADS: bucket() }, "", serveObject, usage);
}

const item = (type, millis, id = `track:en:${ID_A}`) => ({ objectId: id, type, durationPlayedMillis: millis });

test("the path is /sonos/timePlayed", () => {
  assert.equal(isSonosReportingPath("/sonos/timePlayed"), true);
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

test("an unknown item is a 400 with one fault row and nothing counted", async () => {
  const { points, usage } = usageOf();
  const response = await report({ items: [item("final", 60_000), item("final", 60_000, "track:en:nope")] }, usage);
  assert.equal(response.status, 400);
  assert.equal(points.length, 1);
  assert.equal(points[0].blobs[0], "fallback");
});

test("a bad payload is 400 and a wrong method 405", async () => {
  assert.equal((await report("not json", usageOf().usage)).status, 400);
  assert.equal((await report({ nothing: 1 }, usageOf().usage)).status, 400);
  assert.equal((await report({ items: [{ type: "final" }] }, usageOf().usage)).status, 400);
  assert.equal((await report(null, usageOf().usage, "GET")).status, 405);
});

test("parseReports keeps only final reports; countedSeconds applies the 30 s bar", () => {
  assert.deepEqual(parseReports({ items: [item("update", 1000), item("final", 45_000)] }), { ok: true, reports: [{ id: `track:en:${ID_A}`, seconds: 45 }] });
  assert.equal(countedSeconds({ id: "x", seconds: 29.9 }, { seconds: 540 }), null);
  assert.equal(countedSeconds({ id: "x", seconds: 30 }, { seconds: 540 }), 30);
});
