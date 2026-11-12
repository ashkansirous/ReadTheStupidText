// Run: node --test worker/speaker-usage.test.mjs (Slice 289: a speaker play counted as one book_listened row, platform-neutral).
import test from "node:test";
import assert from "node:assert/strict";

import { toDataPoint } from "./events.ts";
import { BOOK_LISTENED, DETAIL_ESTIMATED, SPEAKER_PLATFORM_SONOS, recordSpeakerPlay, speakerEvent } from "./speaker-usage.ts";

const BOOK = "7d0e5a4e000040008000000000000001";
const NOW = new Date("2026-12-01T23:59:59Z");
const PLAY = { platform: SPEAKER_PLATFORM_SONOS, book: BOOK, language: "en", seconds: 600 };

function datasetOf() {
  const points = [];
  return { points, writeDataPoint: (point) => points.push(point) };
}

function usageOf(dataset, enabled = true) {
  return { dataset, telemetryEnabled: async () => enabled };
}

const withCountry = (country) => Object.assign(new Request("https://dino.test/sonos"), { cf: { country } });

test("a play is one book_listened row: shelf book, estimated seconds, platform and day, no version, session or counts", () => {
  const event = speakerEvent(PLAY, NOW);
  assert.equal(event.name, BOOK_LISTENED);
  assert.equal(event.source, "shelf");
  assert.equal(event.detail, DETAIL_ESTIMATED);
  assert.equal(event.day, "2026-12-01");
  const point = toDataPoint(event);
  assert.deepEqual(point.indexes, ["book_listened"]);
  assert.deepEqual(point.blobs.slice(0, 9), ["book_listened", BOOK, "en", "shelf", "no", "", "sonos", "2026-12-01", "estimated"]);
  assert.equal(point.blobs[15], "");
  assert.deepEqual(point.doubles, [1, -1, -1, -1, 600, -1]); // Slice 351: double6 (seconds since launch) is -1 for a speaker row
});

test("a caller that measured the seconds can say so (Alexa): detail reported, default stays estimated", () => {
  assert.equal(speakerEvent({ ...PLAY, platform: "alexa", detail: "reported" }, NOW).detail, "reported");
  assert.equal(speakerEvent(PLAY, NOW).detail, DETAIL_ESTIMATED);
});

test("the platform is the caller's, so Alexa can use the same module", () => {
  assert.equal(speakerEvent({ ...PLAY, platform: "alexa" }, NOW).platform, "alexa");
});

test("a book that is not a store id, or a bad language, is not counted", () => {
  assert.equal(speakerEvent({ ...PLAY, book: "own" }, NOW), null);
  assert.equal(speakerEvent({ ...PLAY, book: "x".repeat(40) }, NOW), null);
  assert.equal(speakerEvent({ ...PLAY, language: "EN!" }, NOW), null);
});

test("seconds are rounded; out-of-range ones are dropped from the row, not the play", () => {
  assert.equal(speakerEvent({ ...PLAY, seconds: 59.6 }, NOW).seconds, 60);
  assert.equal(speakerEvent({ ...PLAY, seconds: -5 }, NOW).seconds, -1);
  assert.equal(speakerEvent({ ...PLAY, seconds: 86_401 }, NOW).seconds, -1);
  assert.equal(speakerEvent({ ...PLAY, seconds: Number.NaN }, NOW).seconds, -1);
});

test("with telemetry on, one row is written with the connection's country and no device facts", async () => {
  const dataset = datasetOf();
  assert.equal(await recordSpeakerPlay(usageOf(dataset), PLAY, withCountry("SE"), NOW), true);
  assert.equal(dataset.points.length, 1);
  assert.deepEqual(dataset.points[0].blobs.slice(9, 15), ["SE", "", "", "", "", ""]);
});

test("telemetry off writes nothing", async () => {
  const dataset = datasetOf();
  assert.equal(await recordSpeakerPlay(usageOf(dataset, false), PLAY, withCountry("SE"), NOW), false);
  assert.equal(dataset.points.length, 0);
});

test("no usage wiring, no dataset, or an unreadable switch writes nothing and never throws", async () => {
  const request = withCountry("SE");
  assert.equal(await recordSpeakerPlay(undefined, PLAY, request, NOW), false);
  assert.equal(await recordSpeakerPlay({ dataset: undefined, telemetryEnabled: async () => true }, PLAY, request, NOW), false);
  const dataset = datasetOf();
  assert.equal(await recordSpeakerPlay({ dataset, telemetryEnabled: async () => { throw new Error("r2"); } }, PLAY, request, NOW), false);
  assert.equal(dataset.points.length, 0);
});

test("a dataset that throws does not fail the play", async () => {
  const dataset = { writeDataPoint: () => { throw new Error("boom"); } };
  assert.equal(await recordSpeakerPlay(usageOf(dataset), PLAY, withCountry("SE"), NOW), false);
});
