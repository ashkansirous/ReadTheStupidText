// Run: node --test worker/user-report-tools.test.mjs
// Slice 249: the desk side of "Report a problem" - pull-crashes.mjs --code and the separate listing of user reports. The file sits here (not beside the script) so the
// existing `node --test worker/*.test.mjs` run, in CI too, covers it.
import test from "node:test";
import assert from "node:assert/strict";

import { codeOf, formatUserReport, isValidCode, normalizeCode, splitUserReports, userReportLine, userReportPrefix } from "../tools/crashes/pull-crashes.mjs";
import { userReportCode } from "./crashes.ts";
import { isExpectedUserReport } from "../.maestro/dino/report-problem-stub.mjs";

const userReport = {
  app: "dino",
  platform: "ios",
  version: "1.85.3",
  channel: "testflight",
  kind: "user_report",
  message: "user report K7QX2MRD",
  minute: "2026-11-04T09:30Z",
  country: "NO",
  device: { osMajor: 26, formFactor: "phone", arch: "arm64", cores: "5-8", ram: "4-6" },
  logTail: "09:29:58.000 I page shown ReadingPage\n09:30:00.000 I reading paused (user)",
};
const crash = { ...userReport, kind: "managed", message: "boom", exceptionType: "System.Exception", stack: "at A.B()" };

test("the emulator flow's stub accepts only the user report with the fixed code, no stack and the app's trace lines", () => {
  const sent = { schemaVersion: 1, app: "dino", kind: "user_report", message: "user report K7QX2MRD", stack: "", logTail: "09:29:58.000 I page shown SettingsPage" };
  assert.ok(isExpectedUserReport(sent));
  assert.ok(!isExpectedUserReport({ ...sent, kind: "managed" }));
  assert.ok(!isExpectedUserReport({ ...sent, message: "user report ABCD2345" }));
  assert.ok(!isExpectedUserReport({ ...sent, stack: "at A.B()" }));
  assert.ok(!isExpectedUserReport({ ...sent, logTail: "" }), "an empty tail means the log lines never reached the report");
  assert.ok(isExpectedUserReport({ ...sent, message: "user report ABCD2345" }, "ABCD2345"));
});

test("a typed code ignores dashes, spaces and case", () => {
  assert.equal(normalizeCode("k7qx-2mrd"), "K7QX2MRD");
  assert.equal(normalizeCode(" K7QX 2MRD "), "K7QX2MRD");
  assert.ok(isValidCode("K7QX-2MRD"));
  assert.ok(isValidCode("k7qx2mrd"));
});

test("a code with a look-alike character or the wrong length is not a code", () => {
  for (const bad of ["", "K7QX-2MR", "K7QX-2MRDD", "K7QX-2MR0", "K7QX-2MRO", "K7QX-2MR1", "K7QX-2MRI", "K7QX-2MRL", "K7QX_2MRD"]) {
    assert.ok(!isValidCode(bad), bad);
  }
});

test("the lookup prefix is the stored name without the extension, so only that report is listed", () => {
  assert.equal(userReportPrefix("dino", "2026-11-04", "k7qx-2mrd"), "crashes/dino/2026-11-04/report-K7QX2MRD");
});

test("the desk's code reading and the Worker's agree on a report's code", () => {
  assert.equal(codeOf(userReport), "K7QX2MRD");
  assert.equal(userReportCode(userReport), "K7QX2MRD");
  assert.equal(codeOf(crash), undefined);
});

test("user reports are split from crashes, so the listing shows them apart", () => {
  const { crashes, userReports } = splitUserReports([crash, userReport, crash]);
  assert.equal(crashes.length, 2);
  assert.deepEqual(userReports, [userReport]);
});

test("the listing and the full form show a report's session id when it has one (Slice 250)", () => {
  const session = "0123456789abcdef0123456789abcdef";
  assert.match(userReportLine({ ...userReport, session }), new RegExp(`session ${session}$`));
  assert.doesNotMatch(userReportLine(userReport), /session/);
  assert.match(formatUserReport({ ...userReport, session }), new RegExp(`session: ${session}`));
});

test("the one-line form shows the code as the person saw it, and the full form shows the whole log tail", () => {
  assert.match(userReportLine(userReport), /^K7QX-2MRD {2}2026-11-04T09:30Z {2}ios 1\.85\.3 testflight/);
  const text = formatUserReport(userReport);
  assert.match(text, /^user report K7QX2MRD/);
  assert.match(text, /version: 1\.85\.3/);
  assert.match(text, /reading paused \(user\)$/);
});
