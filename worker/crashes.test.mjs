// Run: node --test worker/crashes.test.mjs (node 22.18+/24 strip the types of crashes.ts).
import test from "node:test";
import assert from "node:assert/strict";

import { CRASH_PATH, MAX_CRASH_BODY_BYTES, capHead, capTail, crashKey, handleCrash, isCrashPath, isDroppedReport, parseCrash, toCrashDataPoint } from "./crashes.ts";
import { parseAppConfig } from "./appconfig.ts";

const report = {
  schemaVersion: 1,
  app: "dino",
  platform: "ios",
  version: "1.83.0",
  channel: "testflight",
  kind: "managed",
  exceptionType: "System.NullReferenceException",
  message: "Object reference not set",
  stack: "at A.B()\nat C.D()",
  logTail: "12:00:00.000 I hello\n12:00:01.000 E boom",
  minute: "2026-10-31T14:05Z",
  device: { osMajor: 26, formFactor: "phone", arch: "arm64", cores: "5-8", ram: "4-6" },
};

const post = (body, headers = {}) =>
  new Request("https://dinoreader.sirous.uk/api/crash", { method: "POST", headers, body: typeof body === "string" ? body : JSON.stringify(body) });

function bindings(overrides = {}) {
  const objects = new Map();
  const points = [];
  return {
    objects,
    points,
    DOWNLOADS: { put: async (key, value, options) => objects.set(key, { value, options }) },
    APP_CRASHES: { writeDataPoint: (point) => points.push(point) },
    CRASH_LIMITER: { limit: async () => ({ success: true }) },
    ...overrides,
  };
}

const on = async () => true;
const off = async () => false;

test("a valid report is kept whole in R2 under crashes/<app>/<day>/<random id> and counted once", async () => {
  const env = bindings();
  const response = await handleCrash(post(report), env, on, "dino");
  assert.equal(response.status, 204);
  assert.equal(env.objects.size, 1);
  const [key, stored] = [...env.objects.entries()][0];
  assert.match(key, /^crashes\/dino\/\d{4}-\d{2}-\d{2}\/[0-9a-f-]{36}\.json$/);
  assert.equal(stored.options.httpMetadata.contentType, "application/json; charset=utf-8");
  const saved = JSON.parse(stored.value);
  assert.equal(saved.exceptionType, "System.NullReferenceException");
  assert.equal(saved.logTail, report.logTail);
  assert.equal(saved.country, "none");
  assert.equal(env.points.length, 1);
  assert.deepEqual(env.points[0], {
    indexes: ["managed"],
    blobs: ["dino", "ios", "1.83.0", "testflight", "managed", "System.NullReferenceException", "none", "26", "phone", "arm64", "5-8", "4-6"],
    doubles: [1, 5, 4],
  });
});

test("two reports get two different random names, and nothing in the key or the stored object is an id of the sender", async () => {
  const env = bindings();
  await handleCrash(post(report, { "CF-Connecting-IP": "203.0.113.9", "User-Agent": "secret-agent" }), env, on, "dino");
  await handleCrash(post(report), env, on, "dino");
  assert.equal(env.objects.size, 2);
  const everything = JSON.stringify([...env.objects.entries()]) + JSON.stringify(env.points);
  assert.doesNotMatch(everything, /203\.0\.113\.9|secret-agent/);
});

test("with the remote switch off nothing is read or written, and the answer is the same", async () => {
  const env = bindings();
  const response = await handleCrash(post("not even json"), env, off, "dino");
  assert.equal(response.status, 204);
  assert.equal(env.objects.size, 0);
  assert.equal(env.points.length, 0);
});

test("an unreadable config is a 503 so the app keeps the report; a missing bucket is a 503 only when the switch is on", async () => {
  const broken = async () => {
    throw new Error("r2 down");
  };
  assert.equal((await handleCrash(post(report), bindings(), broken, "dino")).status, 503);
  assert.equal((await handleCrash(post(report), {}, on, "dino")).status, 503);
  const unlimited = bindings();
  delete unlimited.CRASH_LIMITER;
  assert.equal((await handleCrash(post(report), unlimited, on, "dino")).status, 503, "no rate-limit binding: refused");
  assert.equal(unlimited.objects.size, 0);
  assert.equal(unlimited.points.length, 0);
  assert.equal((await handleCrash(post(report), {}, off, "dino")).status, 204);
});

test("a failing R2 write is a 503 and nothing is counted", async () => {
  const env = bindings({ DOWNLOADS: { put: async () => Promise.reject(new Error("r2 write")) } });
  assert.equal((await handleCrash(post(report), env, on, "dino")).status, 503);
  assert.equal(env.points.length, 0);
});

test("only POST is answered", async () => {
  const response = await handleCrash(new Request("https://dinoreader.sirous.uk/api/crash"), bindings(), on, "dino");
  assert.equal(response.status, 405);
  assert.equal(response.headers.get("Allow"), "POST");
});

test("the app must be the one whose host it arrived on, and every list is closed", async () => {
  assert.equal((await handleCrash(post({ ...report, app: "rtst" }), bindings(), on, "dino")).status, 400);
  for (const change of [{ platform: "windows" }, { kind: "boom" }, { channel: "dev" }, { version: "v1" }, { schemaVersion: 2 }, { app: undefined }]) {
    assert.equal((await handleCrash(post({ ...report, ...change }), bindings(), on, "dino")).status, 400, JSON.stringify(change));
  }
  assert.equal((await handleCrash(post([report]), bindings(), on, "dino")).status, 400);
  assert.equal((await handleCrash(post("{broken"), bindings(), on, "dino")).status, 400);
});

test("the testflight channel is accepted only for ios", async () => {
  const android = { ...report, platform: "android", channel: "testflight" };
  const env = bindings();
  assert.equal((await handleCrash(post(android), env, on, "dino")).status, 400);
  assert.equal(env.objects.size, 0);
  assert.equal(parseCrash(android, "dino"), null);
  assert.equal((await handleCrash(post({ ...android, channel: "store" }), bindings(), on, "dino")).status, 204);
  assert.equal((await handleCrash(post(report), bindings(), on, "dino")).status, 204);
});

test("an unstamped version and an Intel android or ios device are answered 204 and dropped", async () => {
  for (const changed of [{ version: "1.0.0" }, { version: "0.9.1" }, { platform: "android", channel: "store", device: { ...report.device, arch: "x64" } }, { device: { ...report.device, arch: "x86" } }]) {
    const env = bindings();
    assert.equal((await handleCrash(post({ ...report, ...changed }), env, on, "dino")).status, 204, JSON.stringify(changed));
    assert.equal(env.objects.size, 0);
    assert.equal(env.points.length, 0);
  }
});

test("a body over 64 KB is refused before it is buffered, by length and by stream", async () => {
  const big = JSON.stringify({ ...report, logTail: "x".repeat(MAX_CRASH_BODY_BYTES) });
  const env = bindings();
  assert.equal((await handleCrash(post(big, { "Content-Length": String(big.length) }), env, on, "dino")).status, 413);
  assert.equal((await handleCrash(post(big), env, on, "dino")).status, 413);
  assert.equal(env.objects.size, 0);
});

test("strings are capped in UTF-8 bytes, the log keeps its newest end, and a cut never leaves a broken character", () => {
  const parsed = parseCrash(
    { ...report, message: "m".repeat(5000), stack: "s".repeat(40000), logTail: "OLD" + "x".repeat(40000) + "NEWEST", exceptionType: "T".repeat(500) },
    "dino",
  );
  assert.equal(parsed.message.length, 1024);
  assert.equal(parsed.stack.length, 16 * 1024);
  assert.ok(new TextEncoder().encode(parsed.logTail).length <= 32 * 1024);
  assert.ok(parsed.logTail.endsWith("NEWEST"));
  assert.equal(parsed.exceptionType, "T".repeat(200));
  const persian = "سلام😀".repeat(400);
  assert.ok(new TextEncoder().encode(capHead(persian, 1001)).length <= 1001);
  assert.ok(new TextEncoder().encode(capTail(persian, 1001)).length <= 1001);
  assert.doesNotMatch(capHead(persian, 1001) + capTail(persian, 1001), /�/);
});

test("an odd exception type becomes 'other' for the dataset, and a missing minute is the server's", () => {
  const parsed = parseCrash({ ...report, exceptionType: "<script>alert(1)</script>\n", minute: "yesterday" }, "dino", new Date("2026-10-31T09:08:07Z"));
  assert.equal(parsed.exceptionType, "other");
  assert.equal(parsed.minute, "2026-10-31T09:08Z");
  assert.equal(parseCrash({ ...report, exceptionType: "MXCrashDiagnostic exception=1 signal=11" }, "dino").exceptionType, "MXCrashDiagnostic exception=1 signal=11");
});

test("device fields outside their closed lists are empty, never an error", () => {
  const parsed = parseCrash({ ...report, device: { osMajor: 1000, formFactor: "toaster", arch: "arm64", cores: "5-8", ram: "lots" } }, "dino");
  assert.deepEqual(parsed.device, { osMajor: "", formFactor: "", arch: "arm64", cores: "5-8", ram: "" });
});

test("the rate limit refuses with 429 before the body is read and the IP only ever reaches the limiter as its key", async () => {
  const keys = [];
  const env = bindings({ CRASH_LIMITER: { limit: async ({ key }) => (keys.push(key), { success: keys.length < 2 }) } });
  assert.equal((await handleCrash(post(report, { "CF-Connecting-IP": "198.51.100.7" }), env, on, "dino")).status, 204);
  assert.equal((await handleCrash(post("not read"), env, on, "dino")).status, 429);
  assert.deepEqual(keys, ["198.51.100.7", "unknown"]);
  assert.equal(env.objects.size, 1);
});

test("the country comes from the connection only, and a request without one is 'none'", async () => {
  const env = bindings();
  const request = post(report);
  Object.defineProperty(request, "cf", { value: { country: "NO" } });
  await handleCrash(request, env, on, "dino");
  assert.equal(env.points[0].blobs[6], "NO");
});

test("helpers: the path, the key and the drop rule", () => {
  assert.equal(CRASH_PATH, "/api/crash");
  assert.ok(isCrashPath("/api/crash"));
  assert.ok(!isCrashPath("/api/events"));
  assert.equal(crashKey("rtst", "2026-10-31", "abc"), "crashes/rtst/2026-10-31/abc.json");
  const parsed = parseCrash(report, "dino");
  assert.equal(isDroppedReport(report, parsed), false);
  assert.equal(toCrashDataPoint(parsed, "NO").blobs[6], "NO");
});

test("the app config carries crashReports.enabled, off unless a literal true", () => {
  assert.deepEqual(parseAppConfig({}).crashReports, { enabled: false });
  assert.deepEqual(parseAppConfig({ crashReports: { enabled: true } }).crashReports, { enabled: true });
  for (const value of ["true", 1, null, [], { enabled: "true" }, { enabled: 1 }, { on: true }]) {
    assert.deepEqual(parseAppConfig({ crashReports: value }).crashReports, { enabled: false }, JSON.stringify(value));
  }
});
