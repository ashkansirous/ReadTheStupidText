// Run: node --test worker/ninjalog-spans.test.mjs (Slice 421: Dino counts also posted to Ninjalog as OTLP spans, one trace per session).
import test from "node:test";
import assert from "node:assert/strict";

import { handleEvents } from "./events.ts";
import { ninjalogTee } from "./ninjalog.ts";
import { NINJALOG_SPANS_INDEX, NINJALOG_TRACES_URL, ninjalogTraces, postSpansToNinjalog, spanIdOf, spanName } from "./ninjalog-spans.ts";

const BOOK = "0123456789abcdef0123456789abcdef";
const SESSION = "fedcba9876543210fedcba9876543210";
const RECEIVED = new Date("2026-11-19T10:00:00Z");
const RECEIVED_MS = RECEIVED.getTime();
const baseEvent = {
  name: "book_opened", detail: "", book: BOOK, language: "en", source: "shelf", hasRecording: false, version: "1.42.0", platform: "android", day: "2026-11-19",
  sampleRate: 1, session: SESSION, seq: 3, sinceLaunch: 12, count: -1, deviceCount: -1, seconds: -1, uiLang: "en", installAge: "first", launchNo: "1", screen: "shelf",
};
const context = { country: "IN", device: { osMajor: 15, formFactor: "phone", arch: "arm64", cores: "5-8", ram: "4-6" } };
const sentOf = (over) => ({ event: { ...baseEvent, ...over }, context });
const ON = { NINJALOG_ENABLED: "true", NINJALOG_TOKEN: "secret-token" };
const on = async () => true;
const spansOf = (traces) => traces.resourceSpans[0].scopeSpans[0].spans;
const ns = (ms) => (BigInt(ms) * 1_000_000n).toString();

function post(body) {
  const request = new Request("https://dinoreader.sirous.uk/api/events", { method: "POST", headers: { "CF-Connecting-IP": "203.0.113.9" }, body: JSON.stringify(body) });
  return Object.defineProperty(request, "cf", { value: { country: "IN" } });
}

test("ids and parents: seq 1 is the root, seq 5 points at seq 4, the trace is the session", () => {
  const traces = ninjalogTraces([sentOf({ seq: 5 }), sentOf({ seq: 1, name: "app_opened" }), sentOf({ seq: 4 })], RECEIVED);
  const spans = spansOf(traces);
  assert.deepEqual(spans.map((s) => s.spanId), ["0000000000000001", "0000000000000004", "0000000000000005"]);
  assert.equal("parentSpanId" in spans[0], false);
  assert.equal(spans[1].parentSpanId, "0000000000000003");
  assert.equal(spans[2].parentSpanId, "0000000000000004");
  assert.ok(spans.every((s) => s.traceId === SESSION && s.kind === 1));
  assert.equal(spanIdOf(255), "00000000000000ff");
});

test("names: a screen count is its screen, a tap is `tap <detail>`, the rest the event name", () => {
  assert.equal(spanName(sentOf({ name: "screen", detail: "shelf:shown" })), "shelf");
  assert.equal(spanName(sentOf({ name: "screen", detail: "intro_1:skip" })), "intro_1 skip");
  assert.equal(spanName(sentOf({ name: "screen", detail: "intro_1:shown" })), "intro_1");
  assert.equal(spanName(sentOf({ name: "tap", detail: "shelf:open_book" })), "tap shelf:open_book");
  assert.equal(spanName(sentOf({ name: "tap", detail: "" })), "tap");
  assert.equal(spanName(sentOf({ name: "read_started", detail: "shelf" })), "read_started");
});

test("start and end come from sinceLaunch; the last event ends where it starts; no sinceLaunch uses the receive time", () => {
  const spans = spansOf(ninjalogTraces([sentOf({ seq: 1, sinceLaunch: 2 }), sentOf({ seq: 2, sinceLaunch: 7 }), sentOf({ seq: 3, sinceLaunch: 12 }), sentOf({ seq: 4, sinceLaunch: -1 })], RECEIVED));
  assert.equal(spans[0].startTimeUnixNano, ns(RECEIVED_MS - 10_000));
  assert.equal(spans[0].endTimeUnixNano, ns(RECEIVED_MS - 5_000));
  assert.equal(spans[1].endTimeUnixNano, spans[2].startTimeUnixNano);
  assert.equal(spans[2].startTimeUnixNano, ns(RECEIVED_MS));
  assert.equal(spans[3].startTimeUnixNano, ns(RECEIVED_MS));
  assert.equal(spans[3].endTimeUnixNano, ns(RECEIVED_MS));
  assert.equal(typeof spans[0].startTimeUnixNano, "string");
});

test("a count with no sinceLaunch mid-session takes the previous span's start, not the receive time", () => {
  const spans = spansOf(ninjalogTraces([sentOf({ seq: 1, sinceLaunch: 2 }), sentOf({ seq: 2, sinceLaunch: -1 }), sentOf({ seq: 3, sinceLaunch: 12 })], RECEIVED));
  assert.equal(spans[1].startTimeUnixNano, spans[0].startTimeUnixNano);
  assert.equal(spans[2].startTimeUnixNano, ns(RECEIVED_MS));
  const first = spansOf(ninjalogTraces([sentOf({ seq: 1, sinceLaunch: -1 }), sentOf({ seq: 2, sinceLaunch: 5 })], RECEIVED));
  assert.equal(first[0].startTimeUnixNano, ns(RECEIVED_MS));
});

test("an invalid receive date sends no traces and never throws", () => {
  assert.equal(ninjalogTraces([sentOf({ seq: 1 })], new Date(NaN)), null);
});

test("each numeric attribute keeps one OTLP type whatever its value", () => {
  const type = (over, key) => Object.keys(spansOf(ninjalogTraces([sentOf(over)], RECEIVED))[0].attributes.find((a) => a.key === key).value)[0];
  for (const rate of [1, 0.5, 0.001]) assert.equal(type({ sampleRate: rate, sampleInterval: 1 / rate }, "sampleRate"), "doubleValue");
  assert.equal(type({ sampleRate: 1, sampleInterval: 1 }, "sampleInterval"), "doubleValue");
  assert.equal(type({ sampleRate: 0.5, sampleInterval: 2 }, "sampleInterval"), "doubleValue");
  assert.equal(type({ seq: 2 }, "seq"), "intValue");
});

test("counts without a usable session or seq make no span", () => {
  const unusable = [sentOf({ session: "" }), sentOf({ seq: -1 }), sentOf({ seq: 0 }), sentOf({ seq: 1.5 }), sentOf({ session: "0".repeat(32) }), sentOf({ session: "ABCDEF" }), sentOf({ session: SESSION.toUpperCase() })];
  assert.equal(ninjalogTraces(unusable, RECEIVED), null);
  assert.equal(spansOf(ninjalogTraces([...unusable, sentOf({ seq: 2 })], RECEIVED)).length, 1);
});

test("the resource carries the service name and the #index; attributes are the log fields without timestamp", () => {
  const traces = ninjalogTraces([sentOf({ seq: 2, detail: "shelf" })], RECEIVED);
  const resource = Object.fromEntries(traces.resourceSpans[0].resource.attributes.map((a) => [a.key, a.value.stringValue]));
  assert.deepEqual(resource, { "service.name": "dino", "#index": NINJALOG_SPANS_INDEX });
  const attributes = Object.fromEntries(spansOf(traces)[0].attributes.map((a) => [a.key, a.value]));
  assert.deepEqual(attributes.name, { stringValue: "book_opened" });
  assert.deepEqual(attributes.seq, { intValue: "2" });
  assert.deepEqual(attributes.hasRecording, { boolValue: false });
  assert.deepEqual(attributes.sampleRate, { doubleValue: 1 });
  assert.deepEqual(spansOf(ninjalogTraces([sentOf({ sampleRate: 0.5 })], RECEIVED))[0].attributes.find((a) => a.key === "sampleRate").value, { doubleValue: 0.5 });
  assert.equal("timestamp" in attributes, false);
  assert.equal("#index" in attributes, false);
  assert.equal("count" in attributes, false);
});

test("a handled batch posts the spans to /v1/traces with the token, as JSON", async () => {
  const posts = [];
  const deferred = [];
  const tee = ninjalogTee(ON, (work) => deferred.push(work), async (url, init) => { posts.push({ url, init }); return new Response(null, { status: 200 }); }, () => RECEIVED);
  const env = { DINO_USAGE: { writeDataPoint: () => {} }, DINO_USAGE_BETA: { writeDataPoint: () => {} } };
  const device = { osMajor: 15, formFactor: "phone", arch: "arm64", cores: "5-8", ram: "4-6" };
  const event = { name: "book_opened", book: BOOK, language: "en", source: "shelf", hasRecording: false, version: "1.42.0", platform: "android", day: "2026-11-19", sampleRate: 1, session: SESSION, seq: 3, sinceLaunch: 12, uiLang: "en", installAge: "first", launchNo: "1", screen: "shelf" };
  assert.equal((await handleEvents(post({ device, events: [event] }), env, on, tee)).status, 204);
  await Promise.all(deferred);
  const traced = posts.filter((p) => p.url === NINJALOG_TRACES_URL);
  assert.equal(traced.length, 1);
  assert.equal(traced[0].init.headers.Authorization, "Bearer secret-token");
  assert.equal(traced[0].init.headers["Content-Type"], "application/json");
  assert.equal(spansOf(JSON.parse(traced[0].init.body))[0].spanId, "0000000000000003");
});

test("switch off: no tee, so no spans; a batch with no usable session sends no traces request", async () => {
  assert.equal(ninjalogTee({ NINJALOG_ENABLED: "false", NINJALOG_TOKEN: "t" }, () => {}), undefined);
  const posts = [];
  const tee = ninjalogTee(ON, () => {}, async (url) => { posts.push(url); return new Response(null, { status: 200 }); }, () => RECEIVED);
  tee([sentOf({ session: "" })]);
  assert.deepEqual(posts.filter((url) => url === NINJALOG_TRACES_URL), []);
});

test("a failing or timed-out post never throws", async () => {
  const traces = ninjalogTraces([sentOf({ seq: 1 })], RECEIVED);
  const failures = [async () => new Response("boom", { status: 500 }), async () => { throw new TypeError("network down"); }, async () => { throw new DOMException("timed out", "TimeoutError"); }];
  for (const failing of failures) assert.equal(await postSpansToNinjalog("t", traces, failing), false);
  assert.equal(await postSpansToNinjalog("t", null, async () => assert.fail("no spans, no post")), false);
  let seen;
  await postSpansToNinjalog("t", traces, async (_url, init) => { seen = init.signal; return new Response(null, { status: 200 }); });
  assert.ok(seen instanceof AbortSignal);
});
