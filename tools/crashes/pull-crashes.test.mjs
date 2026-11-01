// Run: node --test tools/crashes/pull-crashes.test.mjs
import test from "node:test";
import assert from "node:assert/strict";

import { groupReports, topFrame, listUrl, parseListPage, collectKeys, isReportKey, daysBetween, parseTokenOutput, apiErrorMessage, safeFileName, withRetry, isRetryable } from "./pull-crashes.mjs";

const base = { app: "dino", platform: "ios", version: "1.83.0", channel: "testflight", kind: "managed", exceptionType: "System.NullReferenceException", message: "m", logTail: "a\nb\nc", minute: "2026-10-31T10:00Z" };

test("the top frame loses its argument list and its file suffix", () => {
  assert.equal(topFrame({ stack: "   at DinoReader.ReadingSurface.Layout(Int32 width) in /src/ReadingSurface.cs:line 12\n   at X.Y()" }), "DinoReader.ReadingSurface.Layout");
  assert.equal(topFrame({ stack: "" }), "(no stack)");
});

test("a native report groups by a stable decoded frame, not its first raw line", () => {
  const tombstone = Buffer.concat([Buffer.from([0x0a, 0x03, 0x08, 0x01]), Buffer.from("  /data/app/lib/arm64/libsherpa-onnx-jni.so  "), Buffer.from([0x00, 0x01])]).toString("base64");
  assert.equal(topFrame({ stack: `tombstone-protobuf-base64:${tombstone}` }), "native:/data/app/lib/arm64/libsherpa-onnx-jni.so");
  const tree = { callStacks: [{ threadAttributed: false, callStackRootFrames: { binaryName: "other" } }, { threadAttributed: true, callStackRootFrames: { binaryName: "dyld", subFrames: [{ binaryName: "UIKit", subFrames: [{ binaryName: "DinoReader" }] }] } }] };
  assert.equal(topFrame({ stack: JSON.stringify(tree) }), "native:DinoReader");
  assert.equal(topFrame({ stack: '{"callStacks":[{"threadAttributed":true,"callStackRootFrames":{"binaryName":"libsystem_kernel.dylib","subFr' }), "native:libsystem_kernel.dylib");
  const a = { ...base, kind: "native", exceptionType: "MXCrashDiagnostic exception=1 signal=6", stack: JSON.stringify(tree) };
  assert.equal(groupReports([a, { ...a, minute: "2026-10-31T11:00Z" }]).length, 1);
});

test("reports group by kind, type and top frame, biggest first, with the newest report kept", () => {
  const frame = "at A.B(String s)\nat C.D()";
  const groups = groupReports([
    { ...base, stack: frame, minute: "2026-10-30T10:00Z" },
    { ...base, stack: frame, platform: "android", version: "1.84.0", minute: "2026-10-31T11:00Z", logTail: "newest" },
    { ...base, exceptionType: "System.IO.IOException", stack: "at Z.Z()" },
  ]);
  assert.equal(groups.length, 2);
  assert.equal(groups[0].count, 2);
  assert.deepEqual(groups[0].platforms, { ios: 1, android: 1 });
  assert.equal(groups[0].newest.logTail, "newest");
  assert.match(groups[0].key, /^managed \| System\.NullReferenceException \| A\.B$/);
});

test("the list URL carries the prefix, a page size and the cursor only when there is one", () => {
  const first = new URL(listUrl("acct", "crashes/dino/2026-11-01/"));
  assert.equal(first.pathname, "/client/v4/accounts/acct/r2/buckets/readthestupidtext-downloads/objects");
  assert.equal(first.searchParams.get("prefix"), "crashes/dino/2026-11-01/");
  assert.equal(first.searchParams.get("per_page"), "1000");
  assert.equal(first.searchParams.has("cursor"), false);
  assert.equal(new URL(listUrl("acct", "p/", "abc=")).searchParams.get("cursor"), "abc=");
});

test("a list page gives its keys and the cursor only while truncated; a failure throws", () => {
  const body = { success: true, result: [{ key: "a" }, { key: "b" }], result_info: { is_truncated: true, cursor: "c1" } };
  assert.deepEqual(parseListPage(body), { keys: ["a", "b"], cursor: "c1" });
  assert.deepEqual(parseListPage({ success: true, result: [{ key: "z" }], result_info: { is_truncated: false, cursor: "stale" } }), { keys: ["z"], cursor: undefined });
  assert.deepEqual(parseListPage({ success: true, result: [] }), { keys: [], cursor: undefined });
  assert.throws(() => parseListPage({ success: false, errors: [{ code: 10000 }] }), /R2 list failed/);
});

test("collectKeys follows the cursor to the last page", async () => {
  const pages = { undefined: { keys: ["a"], cursor: "1" }, 1: { keys: ["b", "c"], cursor: "2" }, 2: { keys: ["d"], cursor: undefined } };
  const seen = [];
  const keys = await collectKeys(async (cursor) => (seen.push(cursor), pages[cursor]));
  assert.deepEqual(keys, ["a", "b", "c", "d"]);
  assert.deepEqual(seen, [undefined, "1", "2"]);
});

test("only .json keys under the app and day count as reports; days run inclusive", () => {
  assert.equal(isReportKey("crashes/dino/2026-11-01/x.json", "dino", "2026-11-01"), true);
  assert.equal(isReportKey("crashes/rtst/2026-11-01/x.json", "dino", "2026-11-01"), false);
  assert.equal(isReportKey("crashes/dino/2026-11-02/x.json", "dino", "2026-11-01"), false);
  assert.equal(isReportKey("crashes/dino/2026-11-01/x.txt", "dino", "2026-11-01"), false);
  assert.deepEqual(daysBetween("2026-10-30", "2026-11-02"), ["2026-10-30", "2026-10-31", "2026-11-01", "2026-11-02"]);
});

test("the wrangler token is read from its JSON, even after banner lines; anything else points to wrangler login", () => {
  assert.equal(parseTokenOutput('\n wrangler 4\n----\n{\n  "type": "oauth",\n  "token": "abc"\n}\n'), "abc");
  assert.throws(() => parseTokenOutput("abc"), /wrangler login/);
  assert.throws(() => parseTokenOutput("{not json"), /wrangler login/);
  assert.throws(() => parseTokenOutput('{"type":"oauth"}'), /wrangler login/);
});

test("a report key becomes a file name that stays inside the output folder", () => {
  assert.equal(safeFileName("crashes/dino/2026-11-01/x.json"), "crashes_dino_2026-11-01_x.json");
  for (const key of ["..\\..\\evil.json", "crashes/../../evil.json", "C:\\evil.json", "/abs/evil.json", "....//x"]) {
    const name = safeFileName(key);
    assert.doesNotMatch(name, /[\\/:]|\.\./);
    assert.doesNotMatch(name, /^\./);
  }
  assert.equal(safeFileName(""), "_");
});

test("429 and 5xx are retried with doubling backoff, then thrown; 404 is not retried", async () => {
  assert.equal(isRetryable(429), true);
  assert.equal(isRetryable(503), true);
  assert.equal(isRetryable(404), false);
  assert.equal(isRetryable(undefined), false);
  const waits = [];
  let calls = 0;
  const flaky = async () => {
    calls += 1;
    if (calls < 3) throw Object.assign(new Error("busy"), { status: 429 });
    return "ok";
  };
  assert.equal(await withRetry(flaky, async (ms) => waits.push(ms)), "ok");
  assert.deepEqual(waits, [500, 1000]);
  calls = 0;
  const down = async () => (calls += 1, Promise.reject(Object.assign(new Error("down"), { status: 503 })));
  await assert.rejects(withRetry(down, async () => {}), /down/);
  assert.equal(calls, 4);
  calls = 0;
  await assert.rejects(withRetry(async () => (calls += 1, Promise.reject(Object.assign(new Error("gone"), { status: 404 }))), async () => {}), /gone/);
  assert.equal(calls, 1);
});

test("a 401 or 403 from R2 names the wrong-account cause; other errors do not", () => {
  assert.match(apiErrorMessage(403, "https://x/objects", "denied"), /Wrong account\?.*wrangler login/);
  assert.doesNotMatch(apiErrorMessage(404, "https://x/objects", "gone"), /Wrong account/);
});
