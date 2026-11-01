// Run: node --test tools/crashes/stub-server.test.mjs
import test from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { isExpectedReport, startStub } from "./stub-server.mjs";

const expected = {
  schemaVersion: 1,
  kind: "managed",
  exceptionType: "System.InvalidOperationException",
  message: "Slice 227 crash test",
  logTail: "12:00:00.000 I crash test about to throw",
};

test("only the report the Debug hook throws is expected", () => {
  assert.equal(isExpectedReport(expected), true);
  assert.equal(isExpectedReport({ ...expected, kind: "native" }), false);
  assert.equal(isExpectedReport({ ...expected, logTail: "" }), false);
  assert.equal(isExpectedReport({ ...expected, exceptionType: "System.Exception" }), false);
  assert.equal(isExpectedReport(null), false);
});

test("the stub answers 204 for the expected report and 400 for anything else, and saves what it was sent", async () => {
  const server = await startStub(0, mkdtempSync(join(tmpdir(), "stub-")));
  const base = `http://127.0.0.1:${server.address().port}`;
  const post = (path, body) => fetch(base + path, { method: "POST", body: typeof body === "string" ? body : JSON.stringify(body) });
  assert.equal((await post("/api/crash", expected)).status, 204);
  assert.equal((await post("/api/crash", { ...expected, message: "other" })).status, 400);
  assert.equal((await post("/api/crash", "not json")).status, 400);
  assert.equal((await post("/elsewhere", expected)).status, 400);
  server.close();
});
