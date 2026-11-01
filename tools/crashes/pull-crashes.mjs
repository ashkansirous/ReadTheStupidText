#!/usr/bin/env node
// Slice 227: read the crash and error reports the phone apps post to the Worker (worker/crashes.ts), which keeps each one in R2 as
//   crashes/<app>/<yyyy-mm-dd>/<random id>.json   (bucket readthestupidtext-downloads)
// and print them grouped by exception type and top frame, with the counts and the newest log tail of each group.
//
//   node tools/crashes/pull-crashes.mjs [--app dino|rtst|both] [--from YYYY-MM-DD] [--to YYYY-MM-DD] [--top 15] [--tail 12] [--out DIR] [--kind managed,native] [--channel store,testflight]
//
// Defaults: both apps, the last 7 days (UTC), the 15 biggest groups, a 12-line log tail, reports saved under <temp>/rtst-crashes. Read-only means the script only lists and gets objects, never writes (the login it uses is the user's general wrangler login, not a read-only credential).
//
// Credentials (Slice 243): none to create. It uses wrangler's own login (`wrangler login`, already done on this PC): the OAuth token printed by `npx --no-install wrangler auth token`
// is sent as a Bearer token to the Cloudflare REST API (R2 list objects + get object, the only two calls it makes). The token is never printed. CLOUDFLARE_ACCOUNT_ID overrides the account
// that owns the Worker; a 401/403 usually means wrangler is logged in to another account. Needs node 22+, wrangler reachable through `npx --no-install` and no other packages.

import { execSync } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const BUCKET = "readthestupidtext-downloads";
const PREFIX = "crashes/";
const DEFAULT_ACCOUNT_ID = "985d711340bd2cf7bf5d71f04a7e84c9";
const API_BASE = "https://api.cloudflare.com/client/v4";
const LIST_PAGE_SIZE = 1000;
const APPS = ["dino", "rtst"];
const DAY_MS = 24 * 60 * 60 * 1000;

function parseArgs(argv) {
  const args = { app: "both", top: 15, tail: 12 };
  for (let i = 0; i < argv.length; i += 2) {
    const name = argv[i]?.replace(/^--/, "");
    const value = argv[i + 1];
    if (!name || value === undefined) throw new Error(`Missing value for ${argv[i]}`);
    args[name] = name === "top" || name === "tail" ? Number(value) : value;
  }
  return args;
}

const LOGIN_HINT = "run `npx wrangler login` (with the Cloudflare account that owns the Worker), or check that wrangler is installed";

// The token in the JSON that `wrangler auth token --json` prints ({"type":"oauth","token":"..."}); banner lines may come before the JSON.
export function parseTokenOutput(stdout) {
  const start = stdout.indexOf("{");
  if (start < 0) throw new Error(`wrangler printed no token: ${LOGIN_HINT}.`);
  let parsed;
  try {
    parsed = JSON.parse(stdout.slice(start));
  } catch {
    throw new Error(`wrangler printed unreadable token output: ${LOGIN_HINT}.`);
  }
  if (!parsed?.token) throw new Error(`wrangler printed no token: ${LOGIN_HINT}.`);
  return parsed.token;
}

// The token of wrangler's own login. stderr is ignored so no token text can reach an error message.
function wranglerToken() {
  let output;
  try {
    output = execSync("npx --no-install wrangler auth token --json", { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
  } catch {
    throw new Error(`wrangler could not give a token: ${LOGIN_HINT}.`);
  }
  return parseTokenOutput(output);
}

// The error text of a failed R2 call; a 401/403 almost always means the wrangler login is for another Cloudflare account.
export function apiErrorMessage(status, urlWithoutQuery, bodyText) {
  const hint = status === 401 || status === 403 ? ` Wrong account? ${LOGIN_HINT}; CLOUDFLARE_ACCOUNT_ID overrides the account id.` : "";
  return `R2 ${status} for ${urlWithoutQuery} ${bodyText.slice(0, 200)}${hint}`;
}

const bucketUrl = (accountId) => `${API_BASE}/accounts/${accountId}/r2/buckets/${BUCKET}/objects`;

export function listUrl(accountId, prefix, cursor) {
  const query = new URLSearchParams({ prefix, per_page: String(LIST_PAGE_SIZE), ...(cursor ? { cursor } : {}) });
  return `${bucketUrl(accountId)}?${query}`;
}

// One page of the R2 "list objects" answer: its keys, and the cursor of the next page (undefined on the last page).
export function parseListPage(body) {
  if (!body?.success) throw new Error(`R2 list failed: ${JSON.stringify(body?.errors ?? body).slice(0, 200)}`);
  const info = body.result_info ?? {};
  return { keys: (body.result ?? []).map((object) => object.key), cursor: info.is_truncated ? info.cursor : undefined };
}

// Follows the cursor until the last page. fetchPage(cursor) returns a parsed page.
export async function collectKeys(fetchPage) {
  const keys = [];
  let cursor;
  do {
    const page = await fetchPage(cursor);
    keys.push(...page.keys);
    cursor = page.cursor;
  } while (cursor);
  return keys;
}

// Only keys under crashes/<app>/<day>/ and ending .json are reports.
export function isReportKey(key, app, day) {
  return key.startsWith(`${PREFIX}${app}/${day}/`) && key.endsWith(".json");
}

async function apiGet(session, url) {
  const response = await fetch(url, { headers: { authorization: `Bearer ${session.token}` } });
  if (!response.ok) throw Object.assign(new Error(apiErrorMessage(response.status, url.split("?")[0], await response.text())), { status: response.status });
  return response;
}

const RETRY_ATTEMPTS = 4;
const RETRY_BASE_MS = 500;
const HTTP_TOO_MANY = 429;
const HTTP_SERVER_ERROR = 500;
const HTTP_NOT_FOUND = 404;

// 429 and 5xx are worth another try; the rest are not.
export function isRetryable(status) {
  return status === HTTP_TOO_MANY || status >= HTTP_SERVER_ERROR;
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// Runs action(); a retryable HTTP failure is tried again with doubling backoff, up to RETRY_ATTEMPTS tries in all.
export async function withRetry(action, wait = sleep) {
  for (let attempt = 1; ; attempt++) {
    try {
      return await action();
    } catch (error) {
      if (!isRetryable(error.status) || attempt >= RETRY_ATTEMPTS) throw error;
      await wait(RETRY_BASE_MS * 2 ** (attempt - 1));
    }
  }
}

// A file name for a report key that cannot leave the output folder: no separators, no "..", no drive letter colon.
export function safeFileName(key) {
  const name = key.replace(/[\\/:]+/g, "_").replace(/\.{2,}/g, "_").replace(/^\.+/, "_");
  return name.length > 0 ? name : "_";
}

async function listKeys(session, prefix) {
  const fetchPage = async (cursor) => parseListPage(await (await apiGet(session, listUrl(session.accountId, prefix, cursor))).json());
  return collectKeys(fetchPage);
}

async function getReport(session, key) {
  return JSON.parse(await (await apiGet(session, `${bucketUrl(session.accountId)}/${key}`)).text());
}

// A report that vanished between list and get (404, the 90-day lifecycle rule) or is not valid JSON is skipped with a warning naming its key.
// A 429/5xx is retried; if it still fails the report is counted in session.notFetched (the run then exits non-zero). Any other error (401/403 ...) stops the run.
async function getReportOrSkip(session, key) {
  try {
    return await withRetry(() => getReport(session, key));
  } catch (error) {
    if (isRetryable(error.status)) {
      session.notFetched += 1;
      console.warn(`NOT FETCHED ${key}: ${error.message}`);
      return undefined;
    }
    if (error.status !== undefined && error.status !== HTTP_NOT_FOUND) throw error;
    console.warn(`Skipped ${key}: ${error.message}`);
    return undefined;
  }
}

export function daysBetween(from, to) {
  const days = [];
  for (let t = Date.parse(`${from}T00:00:00Z`); t <= Date.parse(`${to}T00:00:00Z`); t += DAY_MS) days.push(new Date(t).toISOString().slice(0, 10));
  return days;
}

const TOMBSTONE_PREFIX = "tombstone-protobuf-base64:";
const FRAME_LIMIT = 160;
const MIN_PRINTABLE_RUN = 6;
const PRINTABLE_RUN = new RegExp(`[\\x20-\\x7e]{${MIN_PRINTABLE_RUN},}`, "g");

// A native report's stack is not text: Android keeps the tombstone as base64 of a protobuf, iOS the MetricKit call-stack tree as JSON. Both have one stable
// frame to group by: the first readable library or function string of a tombstone, the crashed thread's deepest binary in the JSON tree.
function nativeFrame(stack) {
  if (stack.startsWith(TOMBSTONE_PREFIX)) {
    const text = Buffer.from(stack.slice(TOMBSTONE_PREFIX.length), "base64").toString("latin1");
    const runs = text.match(PRINTABLE_RUN) ?? [];
    const frame = runs.find((run) => /\.so|\/|::/.test(run)) ?? runs[0];
    return frame ? `native:${frame.trim()}` : "(undecodable tombstone)";
  }
  if (stack.startsWith("{")) return `native:${jsonLeafBinary(stack)}`;
  return undefined;
}

// The binary at the end of the first sub-frame chain of the attributed (crashed) thread; a JSON cut short by the app's cap falls back to the first binary named.
function jsonLeafBinary(json) {
  try {
    const stacks = JSON.parse(json).callStacks ?? [];
    let frame = (stacks.find((s) => s.threadAttributed) ?? stacks[0])?.callStackRootFrames;
    let name;
    while (frame) {
      name = frame.binaryName ?? name;
      frame = frame.subFrames?.[0];
    }
    if (name) return name;
  } catch {
    // truncated: use the text
  }
  return /"binaryName"\s*:\s*"([^"]+)"/.exec(json)?.[1] ?? "(unknown binary)";
}

// The first "at ..." line of a managed stack with its argument list and any file/line suffix removed; for a native report the stable frame from nativeFrame.
export function topFrame(report) {
  const stack = String(report.stack ?? "").trim();
  const native = nativeFrame(stack);
  if (native) return native.slice(0, FRAME_LIMIT);
  const lines = stack.split("\n").map((l) => l.trim());
  const first = lines.find((l) => l.startsWith("at ")) ?? lines.find((l) => l.length > 0);
  if (!first) return "(no stack)";
  return first.replace(/^at\s+/, "").replace(/\(.*$/, "").replace(/\s+in\s+.*$/, "").slice(0, FRAME_LIMIT);
}

export function groupReports(reports) {
  const groups = new Map();
  for (const report of reports) {
    const key = `${report.kind} | ${report.exceptionType} | ${topFrame(report)}`;
    const group = groups.get(key) ?? { key, count: 0, platforms: {}, versions: {}, channels: {}, newest: report };
    group.count += 1;
    group.platforms[report.platform] = (group.platforms[report.platform] ?? 0) + 1;
    group.versions[report.version] = (group.versions[report.version] ?? 0) + 1;
    group.channels[report.channel] = (group.channels[report.channel] ?? 0) + 1;
    if (String(report.minute) > String(group.newest.minute)) group.newest = report;
    groups.set(key, group);
  }
  return [...groups.values()].sort((a, b) => b.count - a.count);
}

const counts = (map) => Object.entries(map).sort((a, b) => b[1] - a[1]).map(([k, v]) => `${k}:${v}`).join(" ");

function printGroups(app, reports, top, tailLines) {
  console.log(`\n=== ${app}: ${reports.length} report(s) ===`);
  for (const group of groupReports(reports).slice(0, top)) {
    console.log(`\n${group.count} x ${group.key}`);
    console.log(`   platforms ${counts(group.platforms)} | versions ${counts(group.versions)} | channels ${counts(group.channels)} | newest ${group.newest.minute}`);
    if (group.newest.message) console.log(`   message: ${String(group.newest.message).slice(0, 300)}`);
    const tail = String(group.newest.logTail ?? "").split("\n").slice(-tailLines);
    if (tail.length > 0 && tail[0] !== "") console.log(`   log tail (newest report):\n${tail.map((l) => `     ${l}`).join("\n")}`);
  }
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const session = { token: wranglerToken(), accountId: process.env.CLOUDFLARE_ACCOUNT_ID ?? DEFAULT_ACCOUNT_ID, notFetched: 0 };
  const today = new Date().toISOString().slice(0, 10);
  const to = args.to ?? today;
  const from = args.from ?? new Date(Date.parse(`${to}T00:00:00Z`) - 6 * DAY_MS).toISOString().slice(0, 10);
  const apps = args.app === "both" ? APPS : [args.app];
  const kinds = args.kind?.split(",");
  const channels = args.channel?.split(",");
  const out = args.out ?? join(tmpdir(), "rtst-crashes");
  mkdirSync(out, { recursive: true });

  for (const app of apps) {
    const reports = [];
    for (const day of daysBetween(from, to)) {
      for (const key of (await listKeys(session, `${PREFIX}${app}/${day}/`)).filter((k) => isReportKey(k, app, day))) {
        const report = await getReportOrSkip(session, key);
        if (!report) continue;
        if ((kinds && !kinds.includes(report.kind)) || (channels && !channels.includes(report.channel))) continue;
        reports.push(report);
        writeFileSync(join(out, safeFileName(key)), JSON.stringify(report, null, 2));
      }
    }
    printGroups(app, reports, args.top, args.tail);
  }
  console.log(`\nReports ${from}..${to} saved in ${out}`);
  if (session.notFetched > 0) {
    console.error(`${session.notFetched} report(s) could not be fetched (rate limit or server error); the counts above are too low. Run again.`);
    process.exitCode = 1;
  }
}

// Run only as a script, so the grouping can be imported by the test.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exit(1);
  });
}
