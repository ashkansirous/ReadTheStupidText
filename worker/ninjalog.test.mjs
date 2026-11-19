// Run: node --test worker/ninjalog.test.mjs (Slice 419: Dino counts also posted to Ninjalog, off by default, never in the request path).
import test from "node:test";
import assert from "node:assert/strict";

import { BETA_EVENTS_PATH, handleEvents } from "./events.ts";
import { NINJALOG_INGEST_URL, ninjalogDocument, ninjalogTee, ninjalogToken, postToNinjalog } from "./ninjalog.ts";
import { SPEAKER_PLATFORM_SONOS, recordSpeakerPlay } from "./speaker-usage.ts";

const BOOK = "0123456789abcdef0123456789abcdef";
const SESSION = "0123456789abcdef0123456789abcdef";
const event = {
  name: "book_opened",
  book: BOOK,
  language: "en",
  source: "shelf",
  hasRecording: false,
  version: "1.42.0",
  platform: "android",
  day: "2026-11-19",
  sampleRate: 0.5,
  session: SESSION,
  seq: 3,
  sinceLaunch: 12,
  uiLang: "en",
  installAge: "first",
  launchNo: "1",
  screen: "shelf",
};
const device = { osMajor: 15, formFactor: "phone", arch: "arm64", cores: "5-8", ram: "4-6" };
const ON = { NINJALOG_ENABLED: "true", NINJALOG_TOKEN: "secret-token" };
const on = async () => true;
const off = async () => false;

function post(body, path = "/api/events", ip = "203.0.113.9") {
  const request = new Request(`https://dinoreader.sirous.uk${path}`, { method: "POST", headers: { "CF-Connecting-IP": ip }, body: JSON.stringify(body) });
  return Object.defineProperty(request, "cf", { value: { country: "IN" } });
}

function harness(env = ON, send) {
  const points = [];
  const posts = [];
  const deferred = [];
  const sender = send ?? (async (url, init) => { posts.push({ url, init }); return new Response(null, { status: 200 }); });
  const tee = ninjalogTee(env, (work) => deferred.push(work), sender, () => new Date("2026-11-19T10:00:00Z"));
  return { points, posts, deferred, tee, env: { DINO_USAGE: { writeDataPoint: (p) => points.push(p) }, DINO_USAGE_BETA: { writeDataPoint: (p) => points.push(p) } } };
}

const documentsOf = (posted) => posted.init.body.split("\n").map((line) => JSON.parse(line));

test("a batch writes to Analytics Engine and posts one NDJSON request with the same named fields", async () => {
  const h = harness();
  const response = await handleEvents(post({ device, events: [event, { ...event, name: "read_started", detail: "shelf", seq: 4 }] }), h.env, on, h.tee);
  await Promise.all(h.deferred);
  assert.equal(response.status, 204);
  assert.equal(h.points.length, 2);
  assert.equal(h.posts.length, 1);
  assert.equal(h.posts[0].url, NINJALOG_INGEST_URL);
  assert.equal(h.posts[0].init.headers.Authorization, "Bearer secret-token");
  const [first, second] = documentsOf(h.posts[0]);
  assert.equal(first["#index"], "dino_usage");
  assert.equal(first.timestamp, "2026-11-19T10:00:00.000Z");
  assert.deepEqual(
    { name: first.name, book: first.book, version: first.version, platform: first.platform, session: first.session, seq: first.seq, sinceLaunch: first.sinceLaunch, installAge: first.installAge, screen: first.screen, country: first.country, os: first.os, form: first.form, arch: first.arch, cores: first.cores, ram: first.ram, sampleInterval: first.sampleInterval },
    { name: "book_opened", book: BOOK, version: "1.42.0", platform: "android", session: SESSION, seq: 3, sinceLaunch: 12, installAge: "first", screen: "shelf", country: "IN", os: "15", form: "phone", arch: "arm64", cores: "5-8", ram: "4-6", sampleInterval: 2 },
  );
  assert.equal(second.detail, "shelf");
  assert.equal(Object.keys(first).some((key) => /^blob\d/.test(key)), false);
});

test("fields a count did not carry are left out, not sent as -1 or empty", () => {
  const sent = { event: { ...event, session: "", seq: -1, sinceLaunch: -1, count: -1, deviceCount: -1, seconds: -1, detail: "" }, context: { country: "GB", device: { osMajor: "none", formFactor: "none", arch: "none", cores: "none", ram: "none" } } };
  const document = ninjalogDocument(sent, new Date("2026-11-19T10:00:00Z"));
  for (const key of ["session", "seq", "sinceLaunch", "count", "deviceCount", "seconds", "detail"]) assert.equal(key in JSON.parse(JSON.stringify(document)), false, key);
});

test("the beta route posts nothing and writes only the beta dataset", async () => {
  const h = harness();
  const response = await handleEvents(post({ channel: "beta", events: [event] }, BETA_EVENTS_PATH), h.env, on, h.tee);
  await Promise.all(h.deferred);
  assert.equal(response.status, 204);
  assert.equal(h.points.length, 1);
  assert.equal(h.posts.length, 0);
});

test("flag off, flag not exactly true, or no token: nothing is posted", async () => {
  for (const env of [{ NINJALOG_TOKEN: "t" }, { NINJALOG_ENABLED: "false", NINJALOG_TOKEN: "t" }, { NINJALOG_ENABLED: "TRUE", NINJALOG_TOKEN: "t" }, { NINJALOG_ENABLED: "true" }, { NINJALOG_ENABLED: "true", NINJALOG_TOKEN: "" }, {}]) {
    const h = harness(env);
    assert.equal(h.tee, undefined);
    assert.equal(ninjalogToken(env), null);
    assert.equal((await handleEvents(post({ events: [event] }), h.env, on, h.tee)).status, 204);
    assert.equal(h.posts.length, 0);
    assert.equal(h.points.length, 1);
  }
});

test("telemetry off, dropped rows and robot rows are not posted", async () => {
  const h = harness();
  await handleEvents(post({ events: [event] }), h.env, off, h.tee);
  await handleEvents(post({ events: [{ ...event, version: "1.0.0" }, { ...event, name: "not_an_event" }] }), h.env, on, h.tee);
  await handleEvents(post({ device: { ...device, arch: "x64" }, events: [event] }), h.env, on, h.tee);
  await Promise.all(h.deferred);
  assert.equal(h.points.length, 0);
  assert.equal(h.posts.length, 0);
});

test("a rejected batch posts nothing", async () => {
  const h = harness();
  assert.equal((await handleEvents(post({ nope: true }), h.env, on, h.tee)).status, 400);
  assert.equal(h.deferred.length, 0);
});

test("a Ninjalog 500, a network error or a timeout leaves the 204 and the Analytics Engine write", async () => {
  const failures = [
    async () => new Response("boom", { status: 500 }),
    async () => { throw new TypeError("network down"); },
    async () => { throw new DOMException("timed out", "TimeoutError"); },
  ];
  for (const failing of failures) {
    const h = harness(ON, failing);
    const response = await handleEvents(post({ events: [event] }), h.env, on, h.tee);
    const results = await Promise.all(h.deferred);
    assert.equal(response.status, 204);
    assert.equal(h.points.length, 1);
    assert.deepEqual(results, [false]);
  }
});

test("a tee that throws does not change the answer", async () => {
  const h = harness();
  const throwing = () => { throw new Error("tee broke"); };
  const response = await handleEvents(post({ events: [event] }), h.env, on, throwing);
  assert.equal(response.status, 204);
  assert.equal(h.points.length, 1);
  const usage = { dataset: h.env.DINO_USAGE, telemetryEnabled: on, tee: throwing };
  assert.equal(await recordSpeakerPlay(usage, { platform: SPEAKER_PLATFORM_SONOS, book: BOOK, language: "en", seconds: 60 }, post({})), true);
  const brokenDefer = ninjalogTee(ON, () => { throw new Error("defer broke"); }, async () => new Response(null, { status: 200 }));
  assert.doesNotThrow(() => brokenDefer([{ event, context: { country: "IN", device: {} } }]));
});

test("the posted body holds no IP, no raw body and no header", async () => {
  const h = harness();
  await handleEvents(post({ device, events: [{ ...event, ip: "198.51.100.7", extra: "x" }] }, "/api/events", "198.51.100.7"), h.env, on, h.tee);
  await Promise.all(h.deferred);
  const text = h.posts[0].init.body;
  assert.equal(text.includes("198.51.100.7"), false);
  assert.equal(text.includes("203.0.113.9"), false);
  assert.equal("extra" in documentsOf(h.posts[0])[0], false);
});

test("a send that never resolves does not hold the answer, and its work is only deferred", async () => {
  const h = harness(ON, () => new Promise(() => {}));
  const response = await handleEvents(post({ events: [event] }), h.env, on, h.tee);
  assert.equal(response.status, 204);
  assert.equal(h.deferred.length, 1);
  assert.equal(h.points.length, 1);
});

test("a speaker play with a failing send still writes its row", async () => {
  const h = harness(ON, async () => { throw new TypeError("network down"); });
  const usage = { dataset: h.env.DINO_USAGE, telemetryEnabled: on, tee: h.tee };
  const wrote = await recordSpeakerPlay(usage, { platform: SPEAKER_PLATFORM_SONOS, book: BOOK, language: "en", seconds: 60 }, post({}));
  assert.equal(wrote, true);
  assert.deepEqual(await Promise.all(h.deferred), [false]);
  assert.equal(h.points.length, 1);
});

test("a timeout signal is attached to the post", async () => {
  let seen;
  await postToNinjalog("t", [{ a: 1 }], async (_url, init) => { seen = init.signal; return new Response(null, { status: 200 }); });
  assert.ok(seen instanceof AbortSignal);
  assert.equal(await postToNinjalog("t", [], async () => assert.fail("no documents, no post")), false);
});

test("a speaker play is forwarded as the same row it wrote", async () => {
  const h = harness();
  const usage = { dataset: h.env.DINO_USAGE, telemetryEnabled: on, tee: h.tee };
  const wrote = await recordSpeakerPlay(usage, { platform: SPEAKER_PLATFORM_SONOS, book: BOOK, language: "en", seconds: 600 }, post({}), new Date("2026-12-01T00:00:00Z"));
  await Promise.all(h.deferred);
  assert.equal(wrote, true);
  assert.equal(h.points.length, 1);
  const [document] = documentsOf(h.posts[0]);
  assert.deepEqual({ name: document.name, platform: document.platform, seconds: document.seconds, detail: document.detail, country: document.country }, { name: "book_listened", platform: "sonos", seconds: 600, detail: "estimated", country: "IN" });
  const off = harness(ON);
  assert.equal(await recordSpeakerPlay({ dataset: off.env.DINO_USAGE, telemetryEnabled: async () => false, tee: off.tee }, { platform: SPEAKER_PLATFORM_SONOS, book: BOOK, language: "en", seconds: 60 }, post({})), false);
  assert.equal(off.posts.length, 0);
});
