// Run: node --test worker/events.test.mjs (node 22.18+/24 strip the types of events.ts).
import test from "node:test";
import assert from "node:assert/strict";

import { MAX_EVENTS_PER_REQUEST, handleEvents, handleRtstEvents, isEventsPath, parseEvent, parseRtstEvent, toDataPoint } from "./events.ts";

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

// ---- Read The Stupid Text's phone counts (Slice 137) ----------------------------------------------------------------

const rtst = (name, fields) => ({ name, version: "1.4.0", platform: "android", day: "2026-10-26", sampleRate: 0.5, ...fields });
const rtstPost = (events) =>
  new Request("https://readthestupidtext.sirous.uk/api/events", { method: "POST", body: JSON.stringify({ schemaVersion: 1, events }) });
const rtstRecorder = () => {
  const points = [];
  return { points, RTST_USAGE: { writeDataPoint: (point) => points.push(point) } };
};

const readStarted = rtst("read_started", { language: "fr", sourceLanguage: "fr", mode: "original", engine: "supertonic", voiceDefault: true, lengthBucket: "200-1000" });

test("a phone read count is written with its fields in the fixed blob order", async () => {
  const env = rtstRecorder();
  const response = await handleRtstEvents(rtstPost([readStarted]), env, on);
  assert.equal(response.status, 204);
  assert.deepEqual(env.points, [
    {
      indexes: ["read_started"],
      blobs: ["read_started", "1.4.0", "android", "2026-10-26", "fr", "fr", "original", "supertonic", "yes", "200-1000", "", "", "", "", "", "", ""],
      doubles: [2],
    },
  ]);
});

test("a Windows read count carries its source as a blob (Slice 140)", async () => {
  const env = rtstRecorder();
  const windowsRead = { ...rtst("read_started", { language: "en", mode: "translation", engine: "windows", voiceDefault: true, lengthBucket: "<200", sourceLanguage: "fr", source: "hotkey" }), platform: "windows" };
  assert.equal((await handleRtstEvents(rtstPost([windowsRead]), env, on)).status, 204);
  assert.deepEqual(env.points[0].blobs.slice(3), ["2026-10-26", "en", "fr", "translation", "windows", "yes", "<200", "", "", "", "", "hotkey", "", ""]);
  assert.equal(env.points[0].blobs[2], "windows");
});

test("a timing count carries whether the head start had run, as the blob before the wait stage (Slice 139)", async () => {
  const env = rtstRecorder();
  const timing = rtst("timing", { stage: "first_audio", outcome: "done", durationBucket: "0.5-1", language: "en", early: true });
  assert.equal((await handleRtstEvents(rtstPost([timing]), env, on)).status, 204);
  assert.equal(env.points[0].blobs.at(-2), "yes");
  assert.equal(parseRtstEvent(rtst("timing", { stage: "first_audio", outcome: "done", durationBucket: "0.5-1", early: "yes" })), null);
  assert.equal(parseRtstEvent({ ...readStarted, early: true }), null);
});

test("a phone wait count carries its stage and duration, and the stage is the last blob (Slice 148)", async () => {
  const env = rtstRecorder();
  const shown = rtst("wait_shown", { waitStage: "stalled", durationBucket: "2-5" });
  const cancelled = { ...rtst("wait_cancelled", { waitStage: "preparing_voice", durationBucket: "<0.5" }), platform: "windows" };
  assert.equal((await handleRtstEvents(rtstPost([shown, cancelled]), env, on)).status, 204);
  assert.equal(env.points.length, 2);
  assert.equal(env.points[0].blobs.at(-1), "stalled");
  assert.equal(env.points[0].blobs.at(-4), "2-5");
});

test("a phone wait count with an unknown stage, a missing duration or an extra field is dropped", () => {
  assert.equal(parseRtstEvent(rtst("wait_shown", { waitStage: "loading", durationBucket: "2-5" })), null);
  assert.equal(parseRtstEvent(rtst("wait_shown", { waitStage: "stalled" })), null);
  assert.equal(parseRtstEvent(rtst("wait_cancelled", { waitStage: "stalled", durationBucket: "3-4" })), null);
  assert.equal(parseRtstEvent(rtst("wait_shown", { waitStage: "stalled", durationBucket: "2-5", language: "en" })), null);
  assert.equal(parseRtstEvent({ ...readStarted, waitStage: "stalled" }), null);
});

test("Dino takes wait counts about no book, with a stage:bucket detail from the closed lists (Slice 148)", () => {
  const wait = { ...shelfEvent, name: "wait_shown", book: "none", source: "none", language: "und", detail: "stalled:2-5" };
  assert.notEqual(parseEvent(wait), null);
  assert.notEqual(parseEvent({ ...wait, name: "wait_cancelled", detail: "getting_file:>20" }), null);
  for (const detail of ["", "stalled", "stalled:", "stalled:3-4", "loading:2-5", "stalled:2-5:x", ":2-5"]) {
    assert.equal(parseEvent({ ...wait, detail }), null, detail);
  }
  assert.equal(parseEvent({ ...wait, book: BOOK, source: "shelf" }), null);
  assert.equal(parseEvent({ ...wait, language: "en" }), null); // a wait count carries no language
});

test("Dino takes the intro counts about no book, with a closed detail (Slice 155)", () => {
  const intro = { ...shelfEvent, book: "none", source: "none", language: "en" };
  assert.notEqual(parseEvent({ ...intro, name: "intro_started", detail: "first_run" }), null);
  assert.notEqual(parseEvent({ ...intro, name: "intro_started", detail: "replay" }), null);
  assert.notEqual(parseEvent({ ...intro, name: "intro_step", detail: "store:tried" }), null);
  assert.notEqual(parseEvent({ ...intro, name: "intro_step", detail: "add:skipped" }), null);
  assert.notEqual(parseEvent({ ...intro, name: "intro_ended", detail: "skipped_all" }), null);
  assert.notEqual(parseEvent({ ...intro, name: "intro_ended", detail: "finished" }), null);
  for (const detail of ["", "read:tried", "store", "store:done", "store:tried:x", ":tried"]) {
    assert.equal(parseEvent({ ...intro, name: "intro_step", detail }), null, detail);
  }
  assert.equal(parseEvent({ ...intro, name: "intro_started", detail: "finished" }), null);
  assert.equal(parseEvent({ ...intro, name: "intro_ended", detail: "replay" }), null);
  assert.equal(parseEvent({ ...intro, name: "intro_ended", detail: "finished", book: BOOK, source: "shelf" }), null);
});

test("the Windows translate switch count needs no source language, but a source outside the list drops a read", () => {
  assert.notEqual(parseRtstEvent(rtst("translate_chosen", { language: "en" })), null);
  assert.equal(parseRtstEvent({ ...readStarted, source: "window title" }), null);
  assert.equal(parseRtstEvent(rtst("voice_changed", { language: "en", engine: "windows", source: "panel" })), null);
});

test("every phone event name is taken with its required fields", () => {
  const good = [
    rtst("app_opened", { language: "en", osMajor: 15 }),
    readStarted,
    rtst("translate_offered", { sourceLanguage: "fr", language: "en" }),
    rtst("translate_chosen", { sourceLanguage: "fr", language: "en" }),
    rtst("voice_changed", { language: "en", engine: "phone" }),
    rtst("timing", { stage: "translate", outcome: "done", durationBucket: "1-2", sourceLanguage: "fr", language: "en" }),
    rtst("timing", { stage: "first_audio", outcome: "cancelled", durationBucket: ">20", language: "en" }),
  ];
  for (const event of good) assert.notEqual(parseRtstEvent(event), null, event.name);
});

test("an unknown field is ignored and never reaches the dataset", () => {
  const event = parseRtstEvent(rtst("app_opened", { language: "en", osMajor: 15, text: "hello", deviceId: "abc" }));
  assert.deepEqual(event.fields, { language: "en", osMajor: 15 });
});

test("a phone count with a missing, extra or out-of-list field is dropped whole", () => {
  const bad = [
    rtst("app_opened", { language: "en" }),
    rtst("app_opened", { language: "en", osMajor: 0 }),
    rtst("app_opened", { language: "en", osMajor: 15.5 }),
    rtst("app_opened", { language: "en", osMajor: 15, mode: "original" }),
    { ...readStarted, mode: "both" },
    { ...readStarted, engine: "neural" },
    { ...readStarted, voiceDefault: "yes" },
    { ...readStarted, lengthBucket: "long" },
    { ...readStarted, language: "french please" },
    rtst("translate_offered", { language: "en" }),
    rtst("voice_changed", { language: "en", engine: "phone", mode: "original" }),
    rtst("timing", { stage: "download", outcome: "done", durationBucket: "1-2" }),
    rtst("timing", { stage: "translate", outcome: "slow", durationBucket: "1-2" }),
    rtst("timing", { stage: "translate", outcome: "done", durationBucket: "3-4" }),
    rtst("book_opened", { language: "en" }),
    { ...readStarted, platform: "tv" },
    { ...readStarted, day: "2026-10-26T10:00:00Z" },
    { ...readStarted, version: "1.x" },
    { ...readStarted, sampleRate: 0 },
    "not an event",
    null,
  ];
  for (const event of bad) assert.equal(parseRtstEvent(event), null, JSON.stringify(event));
});

test("with telemetry off the phone endpoint writes nothing and answers 204; with it on and no binding it is a 503", async () => {
  const env = rtstRecorder();
  assert.equal((await handleRtstEvents(rtstPost([readStarted]), env, off)).status, 204);
  assert.equal(env.points.length, 0);
  assert.equal((await handleRtstEvents(rtstPost([readStarted]), {}, off)).status, 204);
  assert.equal((await handleRtstEvents(rtstPost([readStarted]), {}, on)).status, 503);
});

test("only valid phone counts of a batch reach the dataset, and Dino's dataset is never used", async () => {
  const env = { ...rtstRecorder(), USAGE: { writeDataPoint: () => assert.fail("Dino's dataset must not be written") } };
  await handleRtstEvents(rtstPost([readStarted, { ...readStarted, mode: "x" }, "junk"]), env, on);
  assert.equal(env.points.length, 1);
});

test("a phone batch over the limit is refused and a GET is a 405", async () => {
  const events = Array.from({ length: MAX_EVENTS_PER_REQUEST + 1 }, () => readStarted);
  assert.equal((await handleRtstEvents(rtstPost(events), rtstRecorder(), on)).status, 413);
  const get = new Request("https://readthestupidtext.sirous.uk/api/events");
  assert.equal((await handleRtstEvents(get, rtstRecorder(), on)).status, 405);
});
