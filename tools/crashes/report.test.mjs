// Run: node --test tools/crashes/report.test.mjs
import test from "node:test";
import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { appleCrashTime, appleCrashVersion, buildReport, daysSince, pairUncleanExits, parseArgs, readAppleCrashes, readPlayCrashes } from "./report.mjs";

const log = (time, version) => `Incident Identifier: x\nDate/Time:         ${time}\nVersion:           ${version} (20261101.9)\n`;
const exit = (minute, version = "1.88.0", platform = "ios", extra = {}) => ({ app: "dino", platform, version, channel: "testflight", kind: "unclean_exit", exceptionType: "unclean_exit", message: "m", stack: "", logTail: "", minute, ...extra });
const apple = (time, version = "1.88.0") => ({ source: "apple", time: Date.parse(time), version, text: "{}" });

test("arguments accept -App/-Since and --app/--since, and refuse an unknown app or a bad date", () => {
  const now = new Date("2026-11-14T12:00:00Z");
  assert.deepEqual(parseArgs(["-App", "dino", "-Since", "2026-11-01"], now), { app: "dino", since: "2026-11-01" });
  assert.equal(parseArgs(["--app", "rtst"], now).since, "2026-11-08");
  assert.throws(() => parseArgs(["-App", "x"], now), /Usage/);
  assert.throws(() => parseArgs(["-App", "dino", "-Since", "11/1"], now), /YYYY-MM-DD/);
  assert.equal(daysSince("2026-11-01", now), 14);
});

test("an Apple log's time and version are read from the text and the .ips forms", () => {
  assert.equal(appleCrashTime(log("2026-11-01 10:02:03.123 +0100", "1.88.0")), Date.parse("2026-11-01T10:02:03+01:00"));
  assert.equal(appleCrashTime('{"captureTime":"2026-11-01 10:02:03.00 +0000"}'), Date.parse("2026-11-01T10:02:03Z"));
  assert.equal(appleCrashVersion(log("x", "1.88.0")), "1.88.0");
  assert.equal(appleCrashTime("nothing"), null);
});

test("each unclean exit is paired with the closest crash of its platform and version, and a crash is used once", () => {
  const reports = [exit("2026-11-01T10:10Z"), exit("2026-11-01T10:20Z")];
  const crashes = [apple("2026-11-01T10:05:00Z"), apple("2026-11-01T10:18:00Z"), apple("2026-11-01T10:09:00Z", "1.73.1")];
  const pairs = pairUncleanExits(reports, crashes);
  assert.equal(pairs[0].crash, crashes[0]);
  assert.equal(pairs[0].diffMinutes, 5);
  assert.equal(pairs[1].crash, crashes[1]);
});

test("a crash of another version, another platform or more than 24 hours away is not paired", () => {
  const far = apple("2026-10-30T10:00:00Z");
  const other = apple("2026-11-01T10:05:00Z", "1.73.1");
  const play = { source: "play", time: Date.parse("2026-11-01T10:05:00Z"), version: "42" };
  assert.equal(pairUncleanExits([exit("2026-11-01T10:10Z")], [far, other, play])[0].crash, null);
});

test("an Android unclean exit pairs with a Play report of the same versionCode, and with any when the report has no build", () => {
  const crash = { source: "play", time: Date.parse("2026-11-01T10:05:00Z"), version: "42" };
  assert.equal(pairUncleanExits([exit("2026-11-01T10:10Z", "1.88.0", "android", { build: "42" })], [crash])[0].crash, crash);
  assert.equal(pairUncleanExits([exit("2026-11-01T10:10Z", "1.88.0", "android", { build: "43" })], [crash])[0].crash, null);
  assert.equal(pairUncleanExits([exit("2026-11-01T10:10Z", "1.88.0", "android")], [crash])[0].crash, crash);
});

test("the whole report with stubbed pulls: pairing, symbolicated frames, and a step that did not run is named and fails the run", async () => {
  const root = mkdtempSync(join(tmpdir(), "rtst-report-test-"));
  const dirs = { ours: join(root, "ours"), apple: join(root, "apple"), play: join(root, "play") };
  Object.values(dirs).forEach((d) => mkdirSync(d, { recursive: true }));
  writeFileSync(join(dirs.ours, "a.json"), JSON.stringify(exit("2026-11-01T10:10Z")));
  writeFileSync(join(dirs.apple, "dino_tf_crashlog_1.json"), JSON.stringify({ data: { attributes: { logText: log("2026-11-01 10:05:00 +0000", "1.88.0") } } }));
  writeFileSync(join(dirs.play, "dino_errorReports_0.json"), JSON.stringify({ errorReports: [{ eventTime: "2026-11-01T09:00:00Z", reportText: "x", issue: "i1", appVersion: { versionCode: 42 } }] }));
  const { text, notRun } = await buildReport({ app: "dino", since: "2026-11-01" }, root, {
    pull: async (_args, _out, missing) => (missing.push("Play (pull-play.py): API not enabled yet"), dirs),
    symbolize: async () => "Method() at /src/File.cs:1:1",
  });
  assert.match(text, /unclean_exit 1\.88\.0 \(ios\).*matches the apple crash 5 min away/);
  assert.match(text, /Method\(\) at \/src\/File\.cs:1:1/);
  assert.match(text, /NOT RUN: Play \(pull-play\.py\): API not enabled yet/);
  assert.equal(notRun.length, 1);
  assert.equal(readAppleCrashes(dirs.apple).length, 1);
  assert.equal(readPlayCrashes(dirs.play)[0].version, "42");
});
