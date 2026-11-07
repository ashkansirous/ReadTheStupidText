#!/usr/bin/env node
// Slice 311: read the crash, hang and memory failures the Microsoft Store collects for the two Windows apps (Partner Center Health report) through the
// Store analytics API `failurehits` method, and print them grouped the way pull-crashes.mjs prints the phone reports.
//
//   node tools/crashes/pull-store.mjs [--app dino|rtst|both] [--from YYYY-MM-DD] [--to YYYY-MM-DD] [--top 15]
//
// Docs (read 2026-11-07): https://learn.microsoft.com/en-us/windows/uwp/monetize/get-error-reporting-data (failurehits: only the last 30 days),
// https://learn.microsoft.com/en-us/windows/uwp/monetize/access-analytics-data-using-windows-store-services (token request).
//
// Credentials: environment variables only, the same names the GitHub Actions secrets of store-submit.yml carry: AZURE_AD_TENANT_ID,
// AZURE_AD_APPLICATION_CLIENT_ID, AZURE_AD_APPLICATION_SECRET. They are write-only Actions secrets (not in D:\secrets), so a run on this PC needs them exported first.
// Store ids: STORE_PRODUCT_ID (RTST, required, no default) and DINOREADER_STORE_APP_ID (Dino, required for --app dino; --app both skips Dino with a note when unset). Nothing is printed of the secret or the token.
// Needs node 22+ and no packages. Read-only: one token POST and GET calls.

import { pathToFileURL } from "node:url";

const API_BASE = "https://manage.devcenter.microsoft.com/v1.0/my/analytics";
const TOKEN_RESOURCE = "https://manage.devcenter.microsoft.com";
const APPS = ["dino", "rtst"];
const DAY_MS = 24 * 60 * 60 * 1000;
const WINDOW_DAYS = 29; // the API serves only the last 30 days
const PAGE_SIZE = 10000;
const NO_ROWS_NOTE = "none (no failure rows: either no failures, or the Store shows nothing below its data threshold)";
const GROUP_BY = "failureName,failureHash,symbol,osVersion,eventType,market,deviceType,packageName,packageVersion";

const REQUEST_TIMEOUT_MS = 30000;
const ISO_DAY = /^\d{4}-\d{2}-\d{2}$/;
const KNOWN_FLAGS = ["app", "top", "from", "to"];

function validateArgs(args) {
  if (args.app !== "both" && !APPS.includes(args.app)) throw new Error(`--app must be dino, rtst or both, not "${args.app}".`);
  if (!Number.isInteger(args.top) || args.top < 1) throw new Error("--top must be a positive whole number.");
  for (const flag of ["from", "to"]) {
    if (args[flag] !== undefined && (!ISO_DAY.test(args[flag]) || Number.isNaN(Date.parse(`${args[flag]}T00:00:00Z`)))) throw new Error(`--${flag} must be a date as YYYY-MM-DD, not "${args[flag]}".`);
  }
  return args;
}

export function parseArgs(argv) {
  const args = { app: "both", top: 15 };
  for (let i = 0; i < argv.length; i += 2) {
    const name = argv[i]?.replace(/^--/, "");
    if (!name || argv[i + 1] === undefined) throw new Error(`Missing value for ${argv[i]}`);
    if (!KNOWN_FLAGS.includes(name)) throw new Error(`Unknown option ${argv[i]}.`);
    args[name] = name === "top" ? Number(argv[i + 1]) : argv[i + 1];
  }
  return validateArgs(args);
}

export function requireEnv(env, names) {
  const missing = names.filter((name) => !env[name]);
  if (missing.length > 0) throw new Error(`Missing environment variable(s): ${missing.join(", ")} (the store-submit.yml Actions secrets; export them first).`);
}

// appArg is "dino", "rtst" or "both". Under "both" a missing Dino id skips Dino with a note; asking for dino by name without it throws.
export function storeIds(env, appArg) {
  const all = { rtst: env.STORE_PRODUCT_ID, dino: env.DINOREADER_STORE_APP_ID };
  const notes = [];
  const apps = appArg === "both" ? [...APPS] : [appArg];
  if (!all.rtst && apps.includes("rtst")) throw new Error("STORE_PRODUCT_ID is not set (the RTST Store id; there is no default, like store-submit.yml).");
  if (!all.dino && apps.includes("dino")) {
    if (appArg === "dino") throw new Error("DINOREADER_STORE_APP_ID is not set (the Dino Store id).");
    notes.push("dino skipped: DINOREADER_STORE_APP_ID is not set (no Dino Store listing yet).");
    apps.splice(apps.indexOf("dino"), 1);
  }
  return { ids: Object.fromEntries(apps.map((app) => [app, all[app]])), notes };
}

// The token request body (form-urlencoded) and URL of the Entra client-credentials call.
export function tokenRequest(env) {
  const body = new URLSearchParams({ grant_type: "client_credentials", client_id: env.AZURE_AD_APPLICATION_CLIENT_ID, client_secret: env.AZURE_AD_APPLICATION_SECRET, resource: TOKEN_RESOURCE });
  return { url: `https://login.microsoftonline.com/${env.AZURE_AD_TENANT_ID}/oauth2/token`, body: body.toString() };
}

// The API wants mm/dd/yyyy for day aggregation.
export function apiDate(isoDay) {
  const [year, month, day] = isoDay.split("-");
  return `${month}/${day}/${year}`;
}

export function failureHitsUrl(applicationId, from, to) {
  const query = new URLSearchParams({ applicationId, startDate: apiDate(from), endDate: apiDate(to), top: String(PAGE_SIZE), skip: "0", groupby: GROUP_BY });
  return `${API_BASE}/failurehits?${query}`;
}

// A relative @nextLink ("failurehits?...", "/v1.0/...") is resolved against the analytics base. A link that resolves to another origin throws:
// the bearer token must never be sent anywhere but the API host.
export function nextUrl(body) {
  const link = body?.["@nextLink"];
  if (!link) return undefined;
  const resolved = new URL(link, `${API_BASE}/`);
  if (resolved.origin !== new URL(API_BASE).origin) throw new Error(`@nextLink points to another host (${resolved.origin}); not following it with the token.`);
  return resolved.href;
}

export function parseRows(body) {
  if (!Array.isArray(body?.Value)) throw new Error(`failurehits answered with no Value array: ${JSON.stringify(body).slice(0, 200)}`);
  return body.Value;
}

// Reads one page into `rows`; returns the next url. A repeated link, or an empty page that still has a link, is an incomplete read and throws.
async function readPage(url, fetchPage, rows, seen, state) {
  const body = await fetchPage(url);
  const page = parseRows(body);
  rows.push(...page);
  if (body.TotalCount !== undefined) state.total = Number(body.TotalCount);
  const next = nextUrl(body);
  if (!next) return undefined;
  if (page.length === 0) throw new Error(`an empty page still had a @nextLink (${rows.length} of ${state.total ?? "?"} rows read)`);
  if (seen.has(next)) throw new Error(`@nextLink repeated (${rows.length} of ${state.total ?? "?"} rows read)`);
  return next;
}

// Follows @nextLink to the last page. fetchPage(url) returns the parsed body. Anything short of the full TotalCount throws an error that
// carries the rows gathered so far in `rows`, so the caller marks the result PARTIAL, never a clean one.
export async function collectRows(firstUrl, fetchPage) {
  const rows = [];
  const seen = new Set();
  const state = { total: undefined };
  try {
    for (let url = firstUrl; url; ) {
      seen.add(url);
      url = await readPage(url, fetchPage, rows, seen, state);
    }
    if (state.total !== undefined && rows.length < state.total) throw new Error(`only ${rows.length} of ${state.total} rows were returned`);
  } catch (error) {
    error.rows = rows;
    throw error;
  }
  return rows;
}

const add = (map, key, amount) => {
  map[key] = (map[key] ?? 0) + amount;
};

// Rows group by event type, failure name and failure hash (distinct crashes stay apart), biggest event count first; counts are rounded because the Store reports sampled, fractional counts.
export function groupRows(rows) {
  const groups = new Map();
  for (const row of rows) {
    const key = row.failureHash ? `${row.eventType} | ${row.failureName} | ${row.failureHash}` : `${row.eventType} | ${row.failureName}`;
    const group = groups.get(key) ?? { key, count: 0, deviceDays: 0, versions: {}, markets: {}, newest: undefined };
    group.count += Number(row.eventCount) || 0;
    group.deviceDays += Number(row.deviceCount) || 0;
    add(group.versions, row.packageVersion, Number(row.eventCount) || 0);
    add(group.markets, row.market, Number(row.eventCount) || 0);
    if (row.date && (!group.newest || String(row.date) > String(group.newest))) group.newest = row.date;
    groups.set(key, group);
  }
  return [...groups.values()].sort((a, b) => b.count - a.count);
}

const counts = (map) => Object.entries(map).sort((a, b) => b[1] - a[1]).map(([k, v]) => `${k}:${Math.round(v)}`).join(" ");

// Same shape as pull-crashes.mjs printGroups: a header, then per group "N x key" and one summary line.
export function formatGroups(app, rows, top) {
  const lines = [`\n=== ${app}: ${rows.length} failure row(s) ===`];
  if (rows.length === 0) lines.push(NO_ROWS_NOTE);
  for (const group of groupRows(rows).slice(0, top)) {
    lines.push(`\n${Math.round(group.count)} x ${group.key}`);
    lines.push(`   versions ${counts(group.versions)} | markets ${counts(group.markets)} | device-days ~${Math.round(group.deviceDays)} (not unique devices) | newest ${group.newest ?? "n/a"}`);
  }
  return lines.join("\n");
}

// Only the status is printed for a failed token call: the Entra error body can carry trace ids, tenant details and the client id.
async function fetchToken(env) {
  const { url, body } = tokenRequest(env);
  const response = await fetch(url, { method: "POST", headers: { "content-type": "application/x-www-form-urlencoded" }, body, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
  if (!response.ok) throw new Error(`Token request failed: HTTP ${response.status}`);
  const token = (await response.json()).access_token;
  if (!token) throw new Error("Token request answered with no access_token.");
  return token;
}

function pageFetcher(token) {
  return async (url) => {
    const response = await fetch(url, { headers: { authorization: `Bearer ${token}` }, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
    if (!response.ok) throw new Error(`failurehits HTTP ${response.status}`);
    return response.json();
  };
}

export function dateRange(args, today = new Date().toISOString().slice(0, 10)) {
  const to = args.to ?? today;
  const from = args.from ?? new Date(Date.parse(`${to}T00:00:00Z`) - WINDOW_DAYS * DAY_MS).toISOString().slice(0, 10);
  if (from > to) throw new Error(`--from (${from}) must not be after --to (${to}).`);
  return { from, to };
}

// Prints one app's rows. A failed page prints the rows gathered so far plus a warning and returns false.
async function printApp(app, url, fetchPage, top) {
  try {
    console.log(formatGroups(app, await collectRows(url, fetchPage), top));
    return true;
  } catch (error) {
    if (error.rows?.length > 0) console.log(formatGroups(`${app} (PARTIAL)`, error.rows, top));
    const outcome = error.rows?.length > 0 ? "the rows above are incomplete" : "no rows were read, this is NOT a result of \"no crashes\"";
    console.error(`FAILED: ${app} pull failed (${error.message}); ${outcome}.`);
    return false;
  }
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  requireEnv(process.env, ["AZURE_AD_TENANT_ID", "AZURE_AD_APPLICATION_CLIENT_ID", "AZURE_AD_APPLICATION_SECRET"]);
  const { ids, notes } = storeIds(process.env, args.app);
  notes.forEach((note) => console.error(`WARNING: ${note}`));
  const { from, to } = dateRange(args);
  const fetchPage = pageFetcher(await fetchToken(process.env));
  let allOk = true;
  for (const [app, id] of Object.entries(ids)) allOk = (await printApp(app, failureHitsUrl(id, from, to), fetchPage, args.top)) && allOk;
  console.log(`\nStore failures ${from}..${to} (the API keeps 30 days)`);
  if (!allOk) process.exitCode = 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
