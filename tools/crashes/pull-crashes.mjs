#!/usr/bin/env node
// Slice 227: read the crash and error reports the phone apps post to the Worker (worker/crashes.ts), which keeps each one in R2 as
//   crashes/<app>/<yyyy-mm-dd>/<random id>.json   (bucket readthestupidtext-downloads)
// and print them grouped by exception type and top frame, with the counts and the newest log tail of each group.
//
//   node tools/crashes/pull-crashes.mjs [--app dino|rtst|both] [--from YYYY-MM-DD] [--to YYYY-MM-DD] [--top 15] [--tail 12] [--out DIR] [--kind managed,native] [--channel store,testflight]
//
// Defaults: both apps, the last 7 days (UTC), the 15 biggest groups, a 12-line log tail, reports saved under <temp>/rtst-crashes. Read-only: it lists and gets objects, never writes.
//
// Credentials (read-only R2 API token named CF_R2_CRASHES_READ; created by the owner, see worker/CLAUDE.md). An R2 token gives an S3 access key id and secret; both are read from
// the environment or, if unset, from D:\secrets\readthestupidtext\secrets.env (override with SECRETS_ENV):
//   CF_R2_CRASHES_READ_ACCESS_KEY_ID, CF_R2_CRASHES_READ_SECRET_ACCESS_KEY, and CLOUDFLARE_ACCOUNT_ID (defaults to the account that owns the Worker).
// The credentials are never printed. Needs node 22+ and no packages: the S3 request signing (AWS Signature V4, region "auto") is done here.

import { createHash, createHmac } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync, existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const BUCKET = "readthestupidtext-downloads";
const PREFIX = "crashes/";
const DEFAULT_ACCOUNT_ID = "985d711340bd2cf7bf5d71f04a7e84c9";
const DEFAULT_SECRETS_ENV = "D:\\secrets\\readthestupidtext\\secrets.env";
const REGION = "auto";
const SERVICE = "s3";
const APPS = ["dino", "rtst"];
const DAY_MS = 24 * 60 * 60 * 1000;
const EMPTY_HASH = createHash("sha256").update("").digest("hex");

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

function readSecretsFile() {
  const path = process.env.SECRETS_ENV ?? DEFAULT_SECRETS_ENV;
  if (!existsSync(path)) return {};
  const values = {};
  for (const line of readFileSync(path, "utf8").split(/\r?\n/)) {
    const match = /^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$/.exec(line);
    if (match) values[match[1]] = match[2].replace(/^["']|["']$/g, "");
  }
  return values;
}

function credentials() {
  const file = readSecretsFile();
  const pick = (name) => process.env[name] ?? file[name];
  const accessKeyId = pick("CF_R2_CRASHES_READ_ACCESS_KEY_ID");
  const secretAccessKey = pick("CF_R2_CRASHES_READ_SECRET_ACCESS_KEY");
  if (!accessKeyId || !secretAccessKey) {
    throw new Error("Set CF_R2_CRASHES_READ_ACCESS_KEY_ID and CF_R2_CRASHES_READ_SECRET_ACCESS_KEY (the read-only R2 token CF_R2_CRASHES_READ) in the environment or in the secrets store.");
  }
  return { accessKeyId, secretAccessKey, accountId: pick("CLOUDFLARE_ACCOUNT_ID") ?? DEFAULT_ACCOUNT_ID };
}

const sha256 = (text) => createHash("sha256").update(text).digest("hex");
const hmac = (key, text) => createHmac("sha256", key).update(text).digest();
const encode = (text) => encodeURIComponent(text).replace(/[!'()*]/g, (c) => `%${c.charCodeAt(0).toString(16).toUpperCase()}`);

// AWS Signature Version 4 for a GET with no body (R2's S3 API: region "auto").
function signedRequest(creds, path, query) {
  const host = `${creds.accountId}.r2.cloudflarestorage.com`;
  const now = new Date();
  const amzDate = now.toISOString().replace(/[-:]/g, "").replace(/\.\d{3}/, "");
  const date = amzDate.slice(0, 8);
  const canonicalQuery = Object.keys(query).sort().map((k) => `${encode(k)}=${encode(query[k])}`).join("&");
  const canonicalPath = path.split("/").map(encode).join("/");
  const headers = { host, "x-amz-content-sha256": EMPTY_HASH, "x-amz-date": amzDate };
  const signedHeaders = Object.keys(headers).sort().join(";");
  const canonicalHeaders = Object.keys(headers).sort().map((k) => `${k}:${headers[k]}\n`).join("");
  const canonical = ["GET", canonicalPath, canonicalQuery, canonicalHeaders, signedHeaders, EMPTY_HASH].join("\n");
  const scope = `${date}/${REGION}/${SERVICE}/aws4_request`;
  const toSign = ["AWS4-HMAC-SHA256", amzDate, scope, sha256(canonical)].join("\n");
  const signingKey = hmac(hmac(hmac(hmac(`AWS4${creds.secretAccessKey}`, date), REGION), SERVICE), "aws4_request");
  const signature = createHmac("sha256", signingKey).update(toSign).digest("hex");
  headers.authorization = `AWS4-HMAC-SHA256 Credential=${creds.accessKeyId}/${scope}, SignedHeaders=${signedHeaders}, Signature=${signature}`;
  return { url: `https://${host}${canonicalPath}${canonicalQuery ? `?${canonicalQuery}` : ""}`, headers };
}

async function s3Get(creds, path, query = {}) {
  const { url, headers } = signedRequest(creds, path, query);
  const response = await fetch(url, { headers });
  if (!response.ok) throw new Error(`R2 ${response.status} for ${path} ${await response.text().then((t) => t.slice(0, 200))}`);
  return response.text();
}

async function listKeys(creds, prefix) {
  const keys = [];
  let token;
  do {
    const query = { "list-type": "2", prefix, ...(token ? { "continuation-token": token } : {}) };
    const xml = await s3Get(creds, `/${BUCKET}`, query);
    for (const match of xml.matchAll(/<Key>([^<]+)<\/Key>/g)) keys.push(match[1]);
    token = /<IsTruncated>true<\/IsTruncated>/.test(xml) ? /<NextContinuationToken>([^<]+)<\/NextContinuationToken>/.exec(xml)?.[1] : undefined;
  } while (token);
  return keys;
}

function daysBetween(from, to) {
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
  const creds = credentials();
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
      for (const key of await listKeys(creds, `${PREFIX}${app}/${day}/`)) {
        const report = JSON.parse(await s3Get(creds, `/${BUCKET}/${key}`));
        if ((kinds && !kinds.includes(report.kind)) || (channels && !channels.includes(report.channel))) continue;
        reports.push(report);
        writeFileSync(join(out, key.replaceAll("/", "_")), JSON.stringify(report, null, 2));
      }
    }
    printGroups(app, reports, args.top, args.tail);
  }
  console.log(`\nReports ${from}..${to} saved in ${out}`);
}

// Run only as a script, so the grouping can be imported by the test.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exit(1);
  });
}
