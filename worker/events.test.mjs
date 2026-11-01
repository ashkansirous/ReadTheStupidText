// Run: node --test worker/events.test.mjs (node 22.18+/24 strip the types of events.ts).
import test from "node:test";
import assert from "node:assert/strict";

import { isUnstampedVersion, MAX_EVENTS_PER_REQUEST, handleEvents, handleRtstEvents, isEventsPath, parseEvent, parseRtstEvent, toDataPoint } from "./events.ts";

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
  return { points, DINO_USAGE: { writeDataPoint: (point) => points.push(point) } };
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
    blobs: ["book_opened", BOOK, "en", "shelf", "no", "1.42.0", "android", "2026-10-25", "", "none", "", "", "", "", ""],
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
  assert.equal(env.points[0].blobs.length, 15);
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
    { ...ownEvent, name: "own_book_added", detail: "pdf:7" },
    { ...ownEvent, name: "own_book_added", detail: "My diary:2-5" },
    { ...ownEvent, name: "own_book_added", detail: "pdf:2-5:x" },
    { ...ownEvent, name: "own_book_added", detail: "pdf:" },
    { ...ownEvent, name: "own_book_added", detail: ":2-5" },
    { ...ownEvent, name: "own_book_added", detail: "pdf:1-5" },
    { ...ownEvent, name: "own_book_added", detail: "mixed:" },
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

test("a count from an unstamped desk build (version 1.0) is dropped for both apps (Slice 167)", () => {
  for (const version of ["1.0", "1.0.0", "1.0.0.0", "0.0.0", "0.0.0.0"]) {
    assert.equal(parseEvent({ ...shelfEvent, version }), null, version);
    assert.equal(parseRtstEvent({ ...readStarted, version }), null, version);
  }
  assert.notEqual(parseEvent({ ...shelfEvent, version: "1.0.1" }), null);
  assert.equal(parseRtstEvent({ ...readStarted, version: "1.0" }), null);
  assert.notEqual(parseRtstEvent({ ...readStarted, version: "1.0.1" }), null);
});

test("any version below 1.0 (major part 0) is dropped for both apps (Slice 219)", () => {
  for (const version of ["0.1.1", "0.9", "0.0.1", "0.67.1.2"]) {
    assert.equal(parseEvent({ ...shelfEvent, version }), null, version);
    assert.equal(parseRtstEvent({ ...readStarted, version }), null, version);
  }
  assert.notEqual(parseEvent({ ...shelfEvent, version: "10.1.0" }), null);
  assert.notEqual(parseRtstEvent({ ...readStarted, version: "10.0.1" }), null);
  for (const version of ["00.5", "000.1", "000000.1.2"]) {
    assert.equal(parseEvent({ ...shelfEvent, version }), null, version);
    assert.equal(parseRtstEvent({ ...readStarted, version }), null, version);
  }
  assert.notEqual(parseEvent({ ...shelfEvent, version: "01.0.1" }), null);
  assert.notEqual(parseRtstEvent({ ...readStarted, version: "999999.1" }), null);
  for (const version of ["1234567.1", "-1.2", "0x1.2"]) {
    assert.equal(parseEvent({ ...shelfEvent, version }), null, version);
    assert.equal(parseRtstEvent({ ...readStarted, version }), null, version);
  }
});

// The same case table as UsageEligibilityTests (Slice 223): both sides must reject and accept the same versions.
const UNSTAMPED_CASES = ["1.0", "1.0.0", "1.0.0.0", "0.0.0", "0.0.0.0", "0.1.1", "0.67.1.2", "00.5", "000.1", "000000.1.2", "1234567.1", "-1.2", "0x1.2", "1", "1.x", "100.x", "", " 1.0 ", " 2.0 ", "2.0\n", "٣.1", "１.0"];
const STAMPED_CASES = ["1.0.1", "1.71.1", "2.0", "10.1.0", "100.0.1", "01.0.1", "999999.1"];

test("isUnstampedVersion decides alone on the shared case table, malformed input included (Slice 223)", () => {
  for (const version of UNSTAMPED_CASES) assert.equal(isUnstampedVersion(version), true, version);
  for (const version of STAMPED_CASES) assert.equal(isUnstampedVersion(version), false, version);
  for (const version of [undefined, null, 1.5, {}]) assert.equal(isUnstampedVersion(version), true, String(version));
});

test("every phone event name drops a 0.x or malformed version on the shared envelope (Slice 223)", () => {
  const timing = rtst("timing", { stage: "first_audio", outcome: "done", durationBucket: "0.5-1" });
  const waitShown = rtst("wait_shown", { waitStage: "stalled", durationBucket: "2-5" });
  for (const event of [readStarted, timing, waitShown]) {
    assert.notEqual(parseRtstEvent(event), null, `${event.name} control`);
    for (const version of ["0.9.0", "0.0.0", "00.5", "1.x"]) assert.equal(parseRtstEvent({ ...event, version }), null, `${event.name} ${version}`);
  }
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
      blobs: ["read_started", "1.4.0", "android", "2026-10-26", "fr", "fr", "original", "supertonic", "yes", "200-1000", "", "", "", "", "", "", "", "none", "", ""],
      doubles: [2, -1, -1],
    },
  ]);
});

test("a Windows read count carries its source as a blob (Slice 140)", async () => {
  const env = rtstRecorder();
  const windowsRead = { ...rtst("read_started", { language: "en", mode: "translation", engine: "windows", voiceDefault: true, lengthBucket: "<200", sourceLanguage: "fr", source: "hotkey" }), platform: "windows" };
  assert.equal((await handleRtstEvents(rtstPost([windowsRead]), env, on)).status, 204);
  assert.deepEqual(env.points[0].blobs.slice(3), ["2026-10-26", "en", "fr", "translation", "windows", "yes", "<200", "", "", "", "", "hotkey", "", "", "none", "", ""]);
  assert.equal(env.points[0].blobs[2], "windows");
});

test("a timing count carries whether the head start had run, as the blob before the wait stage (Slice 139)", async () => {
  const env = rtstRecorder();
  const timing = rtst("timing", { stage: "first_audio", outcome: "done", durationBucket: "0.5-1", language: "en", early: true });
  assert.equal((await handleRtstEvents(rtstPost([timing]), env, on)).status, 204);
  assert.equal(env.points[0].blobs.at(-5), "yes");
  assert.equal(parseRtstEvent(rtst("timing", { stage: "first_audio", outcome: "done", durationBucket: "0.5-1", early: "yes" })), null);
  assert.equal(parseRtstEvent({ ...readStarted, early: true }), null);
});

test("a phone wait count carries its stage and duration, and the stage is the last blob (Slice 148)", async () => {
  const env = rtstRecorder();
  const shown = rtst("wait_shown", { waitStage: "stalled", durationBucket: "2-5" });
  const cancelled = { ...rtst("wait_cancelled", { waitStage: "preparing_voice", durationBucket: "<0.5" }), platform: "windows" };
  assert.equal((await handleRtstEvents(rtstPost([shown, cancelled]), env, on)).status, 204);
  assert.equal(env.points.length, 2);
  assert.equal(env.points[0].blobs.at(-4), "stalled");
  assert.equal(env.points[0].blobs.at(-7), "2-5");
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
  assert.notEqual(parseEvent({ ...intro, name: "intro_step", detail: "places:tried" }), null);
  assert.notEqual(parseEvent({ ...intro, name: "intro_step", detail: "read:skipped" }), null);
  for (const detail of ["", "listen:tried", "store", "store:done", "store:tried:x", ":tried"]) {
    assert.equal(parseEvent({ ...intro, name: "intro_step", detail }), null, detail);
  }
  assert.equal(parseEvent({ ...intro, name: "intro_started", detail: "finished" }), null);
  assert.equal(parseEvent({ ...intro, name: "intro_ended", detail: "replay" }), null);
  assert.equal(parseEvent({ ...intro, name: "intro_ended", detail: "finished", book: BOOK, source: "shelf" }), null);
});

test("Dino takes the story-language counts with closed details (Slice 173)", () => {
  const noBook = { ...shelfEvent, book: "none", source: "none", language: "fa" };
  assert.notEqual(parseEvent({ ...noBook, name: "story_languages_set", detail: "2:settings" }), null);
  assert.notEqual(parseEvent({ ...noBook, name: "story_languages_set", detail: "1:intro" }), null);
  for (const detail of ["", "2", "4:intro", "2:store", "2:intro:x", ":intro", "two:intro"]) {
    assert.equal(parseEvent({ ...noBook, name: "story_languages_set", detail }), null, detail);
  }
  assert.notEqual(parseEvent({ ...noBook, name: "story_language_added" }), null);
  assert.equal(parseEvent({ ...noBook, name: "story_language_added", detail: "fa" }), null);
  for (const detail of ["done", "failed", "cancelled"]) {
    assert.notEqual(parseEvent({ ...noBook, name: "voice_pack_download", detail }), null, detail);
  }
  for (const detail of ["", "ok", "done:x"]) {
    assert.equal(parseEvent({ ...noBook, name: "voice_pack_download", detail }), null, detail);
  }
});

test("a story-language switch is about a shelf book, an own book or no book (the shelf button), never a book id for the wrong source", () => {
  const switched = { ...shelfEvent, name: "story_language_switched", language: "fa" };
  assert.notEqual(parseEvent(switched), null);
  assert.notEqual(parseEvent({ ...switched, book: "own", source: "own" }), null);
  assert.notEqual(parseEvent({ ...switched, book: "none", source: "none" }), null);
  assert.equal(parseEvent({ ...switched, book: "own", source: "shelf" }), null);
  assert.equal(parseEvent({ ...switched, detail: "fa" }), null);
});

test("the intro counts take the story-languages step (Slice 173)", () => {
  const intro = { ...shelfEvent, book: "none", source: "none", language: "en", name: "intro_step" };
  assert.notEqual(parseEvent({ ...intro, detail: "languages:tried" }), null);
  assert.notEqual(parseEvent({ ...intro, detail: "languages:skipped" }), null);
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
  const env = { ...rtstRecorder(), DINO_USAGE: { writeDataPoint: () => assert.fail("Dino's dataset must not be written") } };
  await handleRtstEvents(rtstPost([readStarted, { ...readStarted, mode: "x" }, "junk"]), env, on);
  assert.equal(env.points.length, 1);
});

test("a phone batch over the limit is refused and a GET is a 405", async () => {
  const events = Array.from({ length: MAX_EVENTS_PER_REQUEST + 1 }, () => readStarted);
  assert.equal((await handleRtstEvents(rtstPost(events), rtstRecorder(), on)).status, 413);
  const get = new Request("https://readthestupidtext.sirous.uk/api/events");
  assert.equal((await handleRtstEvents(get, rtstRecorder(), on)).status, 405);
});

// ---- Device context and country (Slice 166) --------------------------------------------------------------------------

const device = { osMajor: 15, formFactor: "tablet", arch: "arm64", cores: "5-8", ram: "4-6" };
const withCountry = (request, country) => Object.defineProperty(request, "cf", { value: { country } });
const dinoPost = (body, country) => withCountry(post(body), country);

test("Dino stamps the batch's country and device facts on every point as blob10-blob15", async () => {
  const env = recorder();
  const response = await handleEvents(dinoPost({ schemaVersion: 1, device, events: [shelfEvent, ownEvent] }, "GB"), env, on);
  assert.equal(response.status, 204);
  for (const point of env.points) assert.deepEqual(point.blobs.slice(9), ["GB", "15", "tablet", "arm64", "5-8", "4-6"]);
  assert.deepEqual(env.points[0].doubles, [2]);
});

test("a batch with no device object is still written, with the device fields empty (an older build)", async () => {
  const env = recorder();
  assert.equal((await handleEvents(dinoPost({ events: [shelfEvent] }, "DE"), env, on)).status, 204);
  assert.deepEqual(env.points[0].blobs.slice(9), ["DE", "", "", "", "", ""]);
  const nulled = recorder();
  assert.equal((await handleEvents(dinoPost({ events: [shelfEvent], device: null }, "DE"), nulled, on)).status, 204);
  assert.deepEqual(nulled.points[0].blobs.slice(10), ["", "", "", "", ""]);
});

test("an invalid device field becomes empty and never drops the count", async () => {
  const env = recorder();
  const badDevice = { osMajor: 150, formFactor: "fridge", arch: "mips", cores: "12", ram: "512GB", model: "Pixel 9" };
  assert.equal((await handleEvents(dinoPost({ events: [shelfEvent], device: badDevice }, "FR"), env, on)).status, 204);
  assert.deepEqual(env.points[0].blobs.slice(9), ["FR", "", "", "", "", ""]);
  assert.ok(!JSON.stringify(env.points[0]).includes("Pixel"));
  const partial = recorder();
  await handleEvents(dinoPost({ events: [shelfEvent], device: { ...device, osMajor: "15", cores: "9+" } }, "FR"), partial, on);
  assert.deepEqual(partial.points[0].blobs.slice(10), ["", "tablet", "arm64", "9+", "4-6"]);
});

test("the country is none for XX, T1, lower case, the wrong length, a non-string or no cf at all", async () => {
  for (const country of ["XX", "T1", "gb", "GBR", "G", "", 5, null, undefined]) {
    const env = recorder();
    await handleEvents(dinoPost({ events: [shelfEvent] }, country), env, on);
    assert.equal(env.points[0].blobs[9], "none", String(country));
  }
  const bare = recorder();
  await handleEvents(post({ events: [shelfEvent] }), bare, on);
  assert.equal(bare.points[0].blobs[9], "none");
});

test("the RTST phone count gets country, form factor and arch as blob18-blob20 and the bucket bounds as doubles", async () => {
  const env = rtstRecorder();
  const request = withCountry(new Request("https://readthestupidtext.sirous.uk/api/events", { method: "POST", body: JSON.stringify({ schemaVersion: 1, device, events: [readStarted] }) }), "SE");
  assert.equal((await handleRtstEvents(request, env, on)).status, 204);
  const point = env.points[0];
  assert.equal(point.blobs.length, 20);
  assert.deepEqual(point.blobs.slice(17), ["SE", "tablet", "arm64"]);
  assert.equal(point.blobs[10], "15", "osMajor (blob11) is filled on every event from the device object");
  assert.deepEqual(point.doubles, [2, 5, 4]);
});

test("the RTST bucket doubles cover every bucket, and -1 means the build sent none", async () => {
  const cases = [["1-4", "<3", 1, 0], ["5-8", "3-4", 5, 3], ["9+", "4-6", 9, 4], ["1-4", "6-8", 1, 6], ["9+", "8+", 9, 8]];
  for (const [cores, ram, coreBound, ramBound] of cases) {
    const env = rtstRecorder();
    const request = new Request("https://readthestupidtext.sirous.uk/api/events", { method: "POST", body: JSON.stringify({ device: { ...device, cores, ram }, events: [readStarted] }) });
    await handleRtstEvents(request, env, on);
    assert.deepEqual(env.points[0].doubles.slice(1), [coreBound, ramBound], `${cores} ${ram}`);
  }
  const old = rtstRecorder();
  await handleRtstEvents(rtstPost([readStarted]), old, on);
  assert.deepEqual(old.points[0].doubles, [2, -1, -1]);
});

test("an old RTST build keeps its own osMajor on app_opened, and the event's value wins over the device's", async () => {
  const env = rtstRecorder();
  const opened = rtst("app_opened", { language: "en", osMajor: 14 });
  const request = new Request("https://readthestupidtext.sirous.uk/api/events", { method: "POST", body: JSON.stringify({ device, events: [opened] }) });
  await handleRtstEvents(request, env, on);
  assert.equal(env.points[0].blobs[10], "14");
  const old = rtstRecorder();
  await handleRtstEvents(rtstPost([opened]), old, on);
  assert.deepEqual([old.points[0].blobs[10], old.points[0].blobs[17]], ["14", "none"]);
});

test("an RTST batch with an invalid device object is still written with empty device fields", async () => {
  const env = rtstRecorder();
  const request = new Request("https://readthestupidtext.sirous.uk/api/events", { method: "POST", body: JSON.stringify({ device: "phone", events: [readStarted] }) });
  assert.equal((await handleRtstEvents(request, env, on)).status, 204);
  assert.deepEqual(env.points[0].blobs.slice(17), ["none", "", ""]);
});

// ---- Robot and emulator drop (Slice 179) -----------------------------------------------------------------------------

test("an Android or iOS Dino batch from an x64 or x86 device is accepted and writes nothing", async () => {
  for (const arch of ["x64", "x86"]) {
    for (const platform of ["android", "ios"]) {
      const env = recorder();
      const response = await handleEvents(dinoPost({ device: { ...device, arch }, events: [{ ...shelfEvent, platform }] }, "US"), env, on);
      assert.equal(response.status, 204, `${platform} ${arch}`);
      assert.equal(env.points.length, 0, `${platform} ${arch}`);
    }
  }
});

test("an arm64 phone batch writes, and so does a Windows desktop on x64", async () => {
  const arm = recorder();
  await handleEvents(dinoPost({ device, events: [shelfEvent] }, "US"), arm, on);
  assert.equal(arm.points.length, 1);
  const desktop = recorder();
  await handleEvents(dinoPost({ device: { ...device, arch: "x64", formFactor: "desktop" }, events: [{ ...shelfEvent, platform: "windows" }] }, "US"), desktop, on);
  assert.equal(desktop.points.length, 1);
});

test("the RTST phone batch from x64 is dropped, a batch with no device object still writes", async () => {
  const dropped = rtstRecorder();
  const request = new Request("https://readthestupidtext.sirous.uk/api/events", { method: "POST", body: JSON.stringify({ device: { ...device, arch: "x64" }, events: [readStarted] }) });
  assert.equal((await handleRtstEvents(request, dropped, on)).status, 204);
  assert.equal(dropped.points.length, 0);
  const old = rtstRecorder();
  await handleRtstEvents(rtstPost([readStarted]), old, on);
  assert.equal(old.points.length, 1);
});

test("an own-book count takes a kind alone or kind:pages with the Slice 178 kinds", () => {
  for (const detail of ["pdf", "typed", "docx:1", "txt:2-5", "mixed:6-10", "photo:11+", "scan:1", "restore", "restore:2-5"]) {
    assert.equal(parseEvent({ ...ownEvent, name: "own_book_added", detail })?.detail, detail);
  }
});

test("an own-book restore count with free text or a bad bucket is refused", () => {
  for (const detail of ["restore:", "restore:3", "restore:2-5:x", "restore:hello"]) {
    assert.equal(parseEvent({ ...ownEvent, name: "own_book_added", detail }), null);
  }
});
