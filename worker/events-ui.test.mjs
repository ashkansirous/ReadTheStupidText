// Run: node --test worker/events-ui.test.mjs (Slice 352: screens, taps, settings, read origins and the screen on every row).
import test from "node:test";
import assert from "node:assert/strict";

import { CONTROLS, READ_ORIGINS, SCREENS, SCREEN_ACTIONS, SETTINGS, handleEvents, parseEvent, toDataPoint } from "./events.ts";

const BOOK = "0123456789abcdef0123456789abcdef";
const base = {
  name: "screen",
  book: "none",
  language: "und",
  source: "none",
  hasRecording: false,
  version: "1.42.0",
  platform: "android",
  day: "2026-11-12",
  sampleRate: 1,
};
const ui = (name, detail, extra = {}) => ({ ...base, name, detail, ...extra });
const shelf = { ...base, book: BOOK, source: "shelf", language: "en" };

const post = (body) => new Request("https://dinoreader.sirous.uk/api/events", { method: "POST", body: JSON.stringify(body) });
const on = async () => true;

test("every screen with every action is accepted, and the detail is kept", () => {
  for (const screen of SCREENS) for (const action of SCREEN_ACTIONS) assert.equal(parseEvent(ui("screen", `${screen}:${action}`))?.detail, `${screen}:${action}`);
});

test("every screen with every control is accepted as a tap", () => {
  for (const screen of SCREENS) for (const control of CONTROLS) assert.equal(parseEvent(ui("tap", `${screen}:${control}`))?.name, "tap");
});

test("a screen or tap with an unknown screen, action or control, a missing half or free text is dropped", () => {
  const bad = ["", "shelf", "shelf:", ":shown", "nowhere:shown", "shelf:typed text", "shelf:shown:extra", "My Book:shown", "Shelf:shown", "intro_9:next"];
  for (const detail of bad) assert.equal(parseEvent(ui("screen", detail)), null, detail);
  for (const detail of ["shelf:shown", "shelf:next_book", "reading:Play", "reading:play:now"]) assert.equal(parseEvent(ui("tap", detail)), null, detail);
  assert.equal(parseEvent(ui("tap", undefined)), null);
});

test("screen, tap and setting counts are about no book: a shelf or own book is refused", () => {
  assert.equal(parseEvent({ ...shelf, name: "tap", detail: "shelf:book" }), null);
  assert.equal(parseEvent({ ...shelf, name: "screen", detail: "shelf:shown", source: "own", book: "own" }), null);
});

test("a setting change takes only the values of its own setting", () => {
  const good = ["usage_counts:on", "usage_counts:off", "animal_sounds:off", "reading_speed:slow", "reading_speed:fast", "reading_mode:picture_text", "app_language:fa", "app_language:nb"];
  for (const detail of good) assert.equal(parseEvent(ui("setting_changed", detail))?.detail, detail, detail);
  const bad = ["usage_counts:maybe", "reading_speed:1.2", "reading_speed:on", "reading_mode:fast", "app_language:xx", "app_language:Persian", "volume:on", "usage_counts", "__proto__:on", "constructor:on", ""];
  for (const detail of bad) assert.equal(parseEvent(ui("setting_changed", detail)), null, detail);
  assert.deepEqual([...SETTINGS], ["usage_counts", "animal_sounds", "reading_speed", "reading_mode", "app_language"]);
});

test("app_backgrounded and app_resumed carry a screen or none, nothing else", () => {
  for (const name of ["app_backgrounded", "app_resumed"]) {
    assert.equal(parseEvent(ui(name, "reading"))?.detail, "reading");
    assert.equal(parseEvent(ui(name, "none"))?.detail, "none");
    assert.equal(parseEvent(ui(name, "shelf:shown")), null);
    assert.equal(parseEvent(ui(name, "a book title")), null);
  }
});

test("book_opened and read_started carry a read origin, an older build's empty detail still works", () => {
  for (const name of ["book_opened", "read_started"]) {
    for (const origin of READ_ORIGINS) assert.equal(parseEvent({ ...shelf, name, detail: origin })?.detail, origin);
    assert.equal(parseEvent({ ...shelf, name })?.detail, "");
    assert.equal(parseEvent({ ...shelf, name, detail: "home" }), null);
    assert.equal(parseEvent({ ...shelf, name, detail: "shelf:book" }), null);
  }
  assert.deepEqual([...READ_ORIGINS], ["wizard", "shelf", "corner", "resume", "next_book"]);
});

test("the screen is stored as blob20 on every row; a bad or missing one is none and keeps the event", async () => {
  const env = { points: [], DINO_USAGE: { writeDataPoint: (point) => env.points.push(point) } };
  const events = [ui("tap", "reading:play", { screen: "reading" }), ui("tap", "reading:play", { screen: "Reading" }), ui("tap", "reading:play"), { ...shelf, name: "book_opened", detail: "shelf", screen: "shelf" }];
  assert.equal((await handleEvents(post({ events }), env, on)).status, 204);
  assert.deepEqual(env.points.map((p) => p.blobs[19]), ["reading", "none", "none", "shelf"]);
  assert.ok(env.points.every((p) => p.blobs.length === 20));
  assert.equal(env.points[0].blobs[8], "reading:play");
});

test("a speaker row has no screen", () => {
  const point = toDataPoint(parseEvent({ ...shelf, name: "book_opened" }));
  assert.equal(point.blobs[19], "none");
});
