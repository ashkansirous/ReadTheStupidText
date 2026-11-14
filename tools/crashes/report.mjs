#!/usr/bin/env node
// Slice 243 item 4: one command that answers "did it crash?".
//
//   node tools/crashes/report.mjs -App dino|rtst [-Since YYYY-MM-DD] [-Out DIR]      (also --app / --since / --out)
//
// It runs, for the one app: the R2 pull (pull-crashes.mjs: our own reports, including each `unclean_exit`), pull-apple.py (TestFlight crash submissions; iOS) and
// pull-play.py (Play error reports; Android), each into a folder under --out (default <temp>/rtst-crash-report). Then it
//   * pairs each `unclean_exit` report with the Apple/Play crash of the same platform and version that is closest in time (within 24 h, each crash used once),
//   * symbolicates every iOS crash and every `native` MetricKit report with symbolicate.mjs (needs llvm and the build's dSYM; a missing one is said, not hidden),
//   * prints one grouped summary.
// A step that cannot run (no wrangler login, a Python package missing, no API access yet) is printed as `NOT RUN: <step>: <why>` and the rest still runs; the exit
// code is 1 when any step could not run, so a quiet "no crashes" never hides a step that did not look.

import { execFile } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { promisify } from "node:util";

import { ascLogText } from "./crash-frames.mjs";
import { groupReports } from "./pull-crashes.mjs";
import { symbolicateText } from "./symbolicate.mjs";

const run = promisify(execFile);
const HERE = dirname(fileURLToPath(import.meta.url));
const DAY_MS = 24 * 60 * 60 * 1000;
export const PAIR_WINDOW_MS = DAY_MS;
const SHOWN_FRAMES = 8;
const KIND_UNCLEAN = "unclean_exit";
const KIND_NATIVE = "native";

/** Arguments from `-App dino -Since 2026-11-01` or `--app dino --since ...`; names are case-insensitive. */
export function parseArgs(argv, now = new Date()) {
  const args = {};
  for (let i = 0; i < argv.length; i += 2) {
    const name = argv[i]?.replace(/^-+/, "").toLowerCase();
    if (!name || argv[i + 1] === undefined) throw new Error(`Missing value for ${argv[i]}`);
    args[name] = argv[i + 1];
  }
  if (args.app !== "dino" && args.app !== "rtst") throw new Error("Usage: node tools/crashes/report.mjs -App dino|rtst [-Since YYYY-MM-DD] [-Out DIR]");
  args.since ??= new Date(now.getTime() - 6 * DAY_MS).toISOString().slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(args.since)) throw new Error(`-Since must be YYYY-MM-DD, got "${args.since}"`);
  return args;
}

/** Whole days from `since` (YYYY-MM-DD) to `now`, at least 1 (pull-play.py takes --days). */
export function daysSince(since, now = new Date()) {
  return Math.max(1, Math.ceil((now.getTime() - Date.parse(`${since}T00:00:00Z`)) / DAY_MS));
}

// ---- reading what the pulls saved -------------------------------------------------------------

function jsonFiles(dir, prefix) {
  return existsSync(dir) ? readdirSync(dir).filter((name) => name.startsWith(prefix) && name.endsWith(".json")).map((name) => join(dir, name)) : [];
}

/** The time of an Apple crash log (text "Date/Time:" line, or an .ips "captureTime"/"timestamp"), as epoch ms, or null. */
export function appleCrashTime(logText) {
  const text = /^Date\/Time:\s+(\d{4}-\d{2}-\d{2}) (\d{2}:\d{2}:\d{2})(?:\.\d+)?\s*([+-]\d{4})?/m.exec(logText);
  if (text) return Date.parse(`${text[1]}T${text[2]}${text[3] ? `${text[3].slice(0, 3)}:${text[3].slice(3)}` : "Z"}`);
  const ips = /"(?:captureTime|timestamp)"\s*:\s*"(\d{4}-\d{2}-\d{2}) (\d{2}:\d{2}:\d{2})(?:\.\d+)?\s*([+-]\d{4})?"/.exec(logText);
  return ips ? Date.parse(`${ips[1]}T${ips[2]}${ips[3] ? `${ips[3].slice(0, 3)}:${ips[3].slice(3)}` : "Z"}`) : null;
}

/** The app version of an Apple crash log ("Version: 1.90.0 (build)" or the .ips CFBundleShortVersionString), or null. */
export function appleCrashVersion(logText) {
  return /^Version:\s+(\S+)/m.exec(logText)?.[1] ?? /"CFBundleShortVersionString"\s*:\s*"([^"]+)"/.exec(logText)?.[1] ?? null;
}

/** The TestFlight crash logs pull-apple.py saved: [{ source: "apple", time, version, text, file }]. */
export function readAppleCrashes(dir) {
  const crashes = [];
  for (const file of jsonFiles(dir, "")) {
    if (!/_tf_crashlog_/.test(file)) continue;
    const raw = readFileSync(file, "utf8");
    const text = ascLogText(raw) ?? "";
    crashes.push({ source: "apple", time: appleCrashTime(text), version: appleCrashVersion(text), text: raw, file });
  }
  return crashes;
}

/** The Play error reports pull-play.py saved: [{ source: "play", time, version (versionCode as text), text, file }]. */
export function readPlayCrashes(dir) {
  const crashes = [];
  for (const file of jsonFiles(dir, "")) {
    if (!/_errorReports_/.test(file)) continue;
    let body;
    try {
      body = JSON.parse(readFileSync(file, "utf8"));
    } catch {
      continue; // an unreadable page was already reported by the pull step (its non-200 answer)
    }
    for (const item of body.errorReports ?? []) {
      crashes.push({ source: "play", time: item.eventTime ? Date.parse(item.eventTime) : null, version: item.appVersion?.versionCode ? String(item.appVersion.versionCode) : null, text: item.reportText ?? "", issue: item.issue ?? "", file });
    }
  }
  return crashes;
}

/** Our R2 reports pull-crashes.mjs saved into `dir`. */
export function readOurReports(dir) {
  return jsonFiles(dir, "").map((file) => JSON.parse(readFileSync(file, "utf8")));
}

// ---- pairing ----------------------------------------------------------------------------------

const reportTime = (report) => Date.parse(report.minute.replace(/Z$/, ":00Z"));

// The same build: iOS compares the app version, Android the versionCode the report carries as `build` (a report from before Slice 243 has none and matches any).
function sameBuild(report, crash) {
  if (crash.source === "apple") return !crash.version || crash.version === report.version;
  return !crash.version || !report.build || String(report.build) === crash.version;
}

/**
 * Pairs each `unclean_exit` report with the closest external crash of its platform and build, within PAIR_WINDOW_MS, each crash used once (the closest pair is made first).
 * Returns [{ report, crash|null, diffMinutes|null }] in the reports' order.
 */
export function pairUncleanExits(reports, crashes) {
  const exits = reports.filter((report) => report.kind === KIND_UNCLEAN);
  const candidates = [];
  exits.forEach((report, index) => {
    const source = report.platform === "ios" ? "apple" : "play";
    crashes.forEach((crash, crashIndex) => {
      if (crash.source !== source || crash.time === null || !sameBuild(report, crash)) return;
      const diff = Math.abs(crash.time - reportTime(report));
      if (diff <= PAIR_WINDOW_MS) candidates.push({ index, crashIndex, diff });
    });
  });
  candidates.sort((a, b) => a.diff - b.diff);
  const pairs = exits.map((report) => ({ report, crash: null, diffMinutes: null }));
  const usedCrash = new Set();
  for (const { index, crashIndex, diff } of candidates) {
    if (pairs[index].crash || usedCrash.has(crashIndex)) continue;
    pairs[index].crash = crashes[crashIndex];
    pairs[index].diffMinutes = Math.round(diff / 60000);
    usedCrash.add(crashIndex);
  }
  return pairs;
}

// ---- the steps --------------------------------------------------------------------------------

async function runStep(name, command, args, notRun) {
  try {
    await run(command, args, { cwd: HERE, maxBuffer: 256 * 1024 * 1024 });
    return true;
  } catch (error) {
    const why = error.code === "ENOENT" ? `${command} was not found` : String(error.stderr || error.message).trim().split(/\r?\n/).slice(-2).join(" | ");
    notRun.push(`${name}: ${why}`);
    return false;
  }
}

async function pullAll(args, out, notRun, runner = runStep) {
  const dirs = { ours: join(out, "ours"), apple: join(out, "apple"), play: join(out, "play") };
  Object.values(dirs).forEach((dir) => mkdirSync(dir, { recursive: true }));
  const days = String(daysSince(args.since));
  await runner("R2 reports (pull-crashes.mjs)", process.execPath, [join(HERE, "pull-crashes.mjs"), "--app", args.app, "--from", args.since, "--out", dirs.ours], notRun);
  await runner("Apple (pull-apple.py)", "python", ["-I", join(HERE, "pull-apple.py"), "--app", args.app, "--out", dirs.apple], notRun);
  await runner("Play (pull-play.py)", "python", ["-I", join(HERE, "pull-play.py"), "--app", args.app, "--days", days, "--out", dirs.play], notRun);
  return dirs;
}

async function frames(text, args, symbolize) {
  try {
    return (await symbolize(text, { app: args.app })).split("\n").slice(0, SHOWN_FRAMES).join("\n");
  } catch (error) {
    return `(not symbolicated: ${error.message})`;
  }
}

// ---- the summary ------------------------------------------------------------------------------

/** The printable summary; `symbolized` maps an Apple crash file or a report's index to its symbolicated frames. */
export function formatSummary({ args, reports, crashes, pairs, symbolized, notRun }) {
  const out = [`== ${args.app}: reports since ${args.since} ==`];
  const kinds = {};
  reports.forEach((report) => (kinds[report.kind] = (kinds[report.kind] ?? 0) + 1));
  out.push(`our reports: ${reports.length} (${Object.entries(kinds).map(([k, n]) => `${k} ${n}`).join(", ") || "none"}); Apple crashes: ${crashes.filter((c) => c.source === "apple").length}; Play error reports: ${crashes.filter((c) => c.source === "play").length}`);
  for (const group of groupReports(reports.filter((r) => r.kind !== KIND_UNCLEAN)).slice(0, 15)) {
    out.push(`\n${group.count} x ${group.key}\n   versions ${Object.entries(group.versions).map(([v, n]) => `${v} x${n}`).join(", ")} | newest ${group.newest.minute}`);
    const native = symbolized.get(group.newest);
    if (native) out.push(`   top frames:\n${native.split("\n").map((l) => `     ${l}`).join("\n")}`);
  }
  for (const { report, crash, diffMinutes } of pairs) {
    out.push(`\nunclean_exit ${report.version} (${report.platform}) at ${report.minute}: ${crash ? `matches the ${crash.source} crash ${diffMinutes} min away` : "no Apple/Play crash within 24 h of the same build"}`);
    if (crash && symbolized.get(crash)) out.push(`   top frames:\n${symbolized.get(crash).split("\n").map((l) => `     ${l}`).join("\n")}`);
    else if (crash?.issue) out.push(`   Play issue: ${crash.issue}`);
  }
  const unpaired = crashes.filter((c) => !pairs.some((p) => p.crash === c));
  if (unpaired.length) out.push(`\n${unpaired.length} Apple/Play crash(es) with no unclean_exit report of ours`);
  notRun.forEach((line) => out.push(`NOT RUN: ${line}`));
  return out.join("\n");
}

/** Runs every step and returns { text, notRun }. `deps` replaces the pulls and the symbolicator in tests. */
export async function buildReport(args, out, deps = {}) {
  const notRun = [];
  const dirs = await (deps.pull ?? pullAll)(args, out, notRun);
  const reports = readOurReports(dirs.ours).filter((r) => r.minute >= args.since);
  const crashes = [...readAppleCrashes(dirs.apple), ...readPlayCrashes(dirs.play)];
  const pairs = pairUncleanExits(reports, crashes);
  const symbolize = deps.symbolize ?? symbolicateText;
  const symbolized = new Map();
  for (const crash of crashes.filter((c) => c.source === "apple" && pairs.some((p) => p.crash === c))) symbolized.set(crash, await frames(crash.text, args, symbolize));
  for (const report of reports.filter((r) => r.kind === KIND_NATIVE && r.platform === "ios")) symbolized.set(report, await frames(JSON.stringify(report), args, symbolize));
  return { text: formatSummary({ args, reports, crashes, pairs, symbolized, notRun }), notRun };
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const out = args.out ?? join(tmpdir(), "rtst-crash-report");
  const { text, notRun } = await buildReport(args, out);
  console.log(text);
  console.log(`\nRaw pulls saved in ${out}`);
  if (notRun.length) process.exitCode = 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exit(1);
  });
}
