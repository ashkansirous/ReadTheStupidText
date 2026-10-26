// Run: node --test worker/events.test.mjs (node 22.18+/24 strip the types of events.ts).
import test from "node:test";
import assert from "node:assert/strict";

import { MAX_EVENTS_PER_REQUEST, handleEvents, isEventsPath, parseEvent, toDataPoint } from "./events.ts";

const BOOK = "0123456789abcdef0123456789abcdef";
const shelfEvent = {
  name: "book_opened",
  book: BOOK,
  language: "en",
  source: "shelf",
  hasRecording: false,
  version: "1.42.0",
  platform: "android",
  day: "2026-10-25",
  sampleRate: 0.5,
};
const ownEvent = { ...shelfEvent, name: "book_finished", book: "own", source: "own", hasRecording: true };

const post = (body, headers = {}) =>
  new Request("https://dinoreader.sirous.uk/api/events", { method: "POST", headers, body: typeof body === "string" ? body : JSON.stringify(body) });

function recorder() {
  const points = [];
  return { points, USAGE: { writeDataPoint: (point) => points.push(point) } };
}

const on = async () => true;
const off = async () => false;

test("a batch of valid counts is written, one data point each, and answered with 204", async () => {
  const env = recorder();
  const response = await handleEvents(post({ schemaVersion: 1, events: [shelfEvent, ownEvent] }), env, on);
  assert.equal(response.status, 204);
  assert.equal(env.points.length, 2);
  assert.deepEqual(env.points[0], {
    indexes: ["book_opened"],
    blobs: ["book_opened", BOOK, "en", "shelf", "no", "1.42.0", "android", "2026-10-25", ""],
    doubles: [2],
  });
  assert.equal(env.points[1].blobs[4], "yes");
});

test("with telemetry off nothing is written, and the answer is the same", async () => {
  const env = recorder();
  const response = await handleEvents(post({ events: [shelfEvent] }), env, off);
  assert.equal(response.status, 204);
  assert.equal(env.points.length, 0);
});

test("a missing dataset binding is a 503 only when telemetry is on", async () => {
  assert.equal((await handleEvents(post({ events: [shelfEvent] }), {}, on)).status, 503);
  assert.equal((await handleEvents(post({ events: [shelfEvent] }), {}, off)).status, 204);
});

test("an unreadable config is a 503, not a silent drop", async () => {
  const broken = async () => {
    throw new Error("r2 down");
  };
  assert.equal((await handleEvents(post({ events: [shelfEvent] }), recorder(), broken)).status, 503);
});

test("invalid counts are dropped one by one and unknown fields never reach the dataset", async () => {
  const env = recorder();
  const extra = { ...shelfEvent, title: "My secret story", deviceId: "abc" };
  const bad = [
    { ...shelfEvent, name: "book_deleted" },
    { ...shelfEvent, book: "My Book" },
    { ...shelfEvent, source: "own" },
    { ...ownEvent, book: BOOK },
    { ...shelfEvent, language: "english please" },
    { ...shelfEvent, hasRecording: "yes" },
    { ...shelfEvent, version: "1.x" },
    { ...shelfEvent, platform: "tv" },
    { ...shelfEvent, day: "2026-02-30" },
    { ...shelfEvent, day: "2026-10-25T10:00:00Z" },
    { ...shelfEvent, sampleRate: 0 },
    { ...shelfEvent, sampleRate: 1.5 },
    { ...shelfEvent, sampleRate: undefined },
    "text",
    null,
  ];
  const response = await handleEvents(post({ events: [...bad, extra] }), env, on);
  assert.equal(response.status, 204);
  assert.equal(env.points.length, 1);
  assert.equal(env.points[0].blobs.length, 9);
  assert.ok(!JSON.stringify(env.points[0]).includes("secret"));
});

test("more than the cap in one request is refused whole", async () => {
  const env = recorder();
  const events = Array.from({ length: MAX_EVENTS_PER_REQUEST + 1 }, () => shelfEvent);
  assert.equal((await handleEvents(post({ events }), env, on)).status, 413);
  assert.equal(env.points.length, 0);
  const atCap = Array.from({ length: MAX_EVENTS_PER_REQUEST }, () => shelfEvent);
  assert.equal((await handleEvents(post({ events: atCap }), env, on)).status, 204);
  assert.equal(env.points.length, MAX_EVENTS_PER_REQUEST);
});

test("a body that is not a batch is a 400, and an oversized body a 413", async () => {
  for (const body of ["{not json", "[]", "null", JSON.stringify({ events: "no" }), JSON.stringify({})]) {
    assert.equal((await handleEvents(post(body), recorder(), on)).status, 400);
  }
  const huge = JSON.stringify({ events: [], pad: "x".repeat(40 * 1024) });
  assert.equal((await handleEvents(post(huge), recorder(), on)).status, 413);
  assert.equal((await handleEvents(post("{}", { "Content-Length": "999999" }), recorder(), on)).status, 413);
});

test("an oversized body without a Content-Length is cut off by the byte cap", async () => {
  const pad = "é".repeat(20 * 1024); // 20 K characters, 40 KB as UTF-8
  const chunked = new Request("https://dinoreader.sirous.uk/api/events", {
    method: "POST",
    body: new ReadableStream({ start(c) { c.enqueue(new TextEncoder().encode(JSON.stringify({ events: [], pad }))); c.close(); } }),
    duplex: "half",
  });
  assert.equal((await handleEvents(chunked, recorder(), on)).status, 413);
});

test("with telemetry off or no binding the body is never read", async () => {
  const unread = () => new Request("https://dinoreader.sirous.uk/api/events", { method: "POST", body: "{not json" });
  assert.equal((await handleEvents(unread(), recorder(), off)).status, 204);
  assert.equal((await handleEvents(unread(), {}, on)).status, 503);
});

test("only POST is answered", async () => {
  const response = await handleEvents(new Request("https://dinoreader.sirous.uk/api/events"), recorder(), on);
  assert.equal(response.status, 405);
  assert.equal(response.headers.get("Allow"), "POST");
});

test("the path check and the parser agree on the shapes they accept", () => {
  assert.ok(isEventsPath("/api/events"));
  assert.ok(!isEventsPath("/api/events/"));
  assert.deepEqual(parseEvent(shelfEvent), { ...shelfEvent, detail: "" });
  assert.equal(parseEvent({ ...shelfEvent, language: "pt-BR" })?.language, "pt-BR");
  assert.equal(toDataPoint(shelfEvent).indexes.length, 1);
});

const noBook = { ...shelfEvent, book: "none", source: "none" };
const valid134 = [
  { ...shelfEvent, name: "store_download", detail: "age+author" },
  { ...ownEvent, name: "own_book_added", detail: "pdf", hasRecording: false },
  { ...shelfEvent, name: "recording_made", detail: "2-5" },
  { ...ownEvent, name: "recording_made", detail: "11+" },
  { ...noBook, name: "voice_changed", detail: "flavour" },
  { ...noBook, name: "reading_language_changed", language: "nb", detail: "en" },
  { ...noBook, name: "app_opened" },
];

test("each Slice 134 event is accepted with its own detail and carries it as the last blob", async () => {
  const env = recorder();
  assert.equal((await handleEvents(post({ events: valid134 }), env, on)).status, 204);
  assert.equal(env.points.length, valid134.length);
  assert.deepEqual(env.points.map((p) => p.blobs[8]), ["age+author", "pdf", "2-5", "11+", "flavour", "en", ""]);
  assert.equal(env.points[4].blobs[1], "none");
  assert.equal(env.points[5].blobs[2], "nb");
});

test("a Slice 134 event with a wrong detail, source or book is dropped, and free text never gets through", () => {
  const bad = [
    { ...shelfEvent, name: "store_download", detail: "Dinosaurs" },
    { ...shelfEvent, name: "store_download" },
    { ...ownEvent, name: "store_download", detail: "none" },
    { ...shelfEvent, name: "own_book_added", detail: "photo" },
    { ...ownEvent, name: "own_book_added", detail: "My diary" },
    { ...shelfEvent, name: "recording_made", detail: "7" },
    { ...noBook, name: "voice_changed", detail: "Fred's voice" },
    { ...shelfEvent, name: "voice_changed", detail: "narrator" },
    { ...noBook, name: "voice_changed", book: BOOK },
    { ...noBook, name: "reading_language_changed", detail: "english please" },
    { ...noBook, name: "reading_language_changed", detail: "xyz" },
    { ...noBook, name: "reading_language_changed" },
    { ...noBook, name: "app_opened", detail: "extra" },
    { ...shelfEvent, detail: "age" },
    { ...shelfEvent, name: "constructor" },
    { ...shelfEvent, name: "__proto__" },
  ];
  for (const event of bad) assert.equal(parseEvent(event), null, JSON.stringify(event));
});

test("an older client's reader events, which have no detail, are still taken", () => {
  assert.equal(parseEvent(ownEvent)?.detail, "");
});
