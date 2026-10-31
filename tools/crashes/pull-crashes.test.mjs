// Run: node --test tools/crashes/pull-crashes.test.mjs
import test from "node:test";
import assert from "node:assert/strict";

import { groupReports, topFrame } from "./pull-crashes.mjs";

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
