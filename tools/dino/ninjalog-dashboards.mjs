#!/usr/bin/env node
// Slice 429: Ninjalog dashboards have no filters (only a time range), so the "New users" dashboard is made once per
// platform from one template; Slice 430 adds the forced-update dashboard from its own. Run
// `node tools/dino/ninjalog-dashboards.mjs` after editing a docs/analytics/ninjalog-*.template.json (each VARIANTS entry
// names its template); `--check` fails when a generated file is out of date.
// `--upload` runs every widget query in Ninjalog (refusing a query that errors) and then saves each dashboard, with the
// MCP token in the NINJALOG_MCP_TOKEN environment variable (D:\secrets\dinoreader\secrets.env).
import { readFileSync, writeFileSync, mkdirSync, readdirSync, existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const analyticsDir = join(repoRoot, "docs", "analytics");
const outDir = join(analyticsDir, "ninjalog-dashboards");

/** The speaker platforms have no first launch and an estimated `seconds` (Slice 288), so "all" leaves them out. */
const APP_PLATFORMS_ONLY = "platform!=sonos platform!=alexa";
const NEW_USERS = "ninjalog-dashboard.template.json";
const FORCED_UPDATE = "ninjalog-forced-update.template.json";

/** One dashboard per entry, made from `template` (in docs/analytics); `split` is the field charts are broken down by. */
export const VARIANTS = [
  { key: "all", template: NEW_USERS, slug: "new-users", name: "New users (all platforms)", app: APP_PLATFORMS_ONLY, any: "", split: "platform" },
  { key: "android", template: NEW_USERS, slug: "new-users-android", name: "New users (Android)", app: "platform=android", any: "platform=android", split: "version" },
  { key: "ios", template: NEW_USERS, slug: "new-users-ios", name: "New users (iOS)", app: "platform=ios", any: "platform=ios", split: "version" },
  { key: "windows", template: NEW_USERS, slug: "new-users-windows", name: "New users (Windows)", app: "platform=windows", any: "platform=windows", split: "version" },
  // Slice 430: who still runs a build below the remote config's minVersion, and whether they move to the new one.
  { key: "forced-update", template: FORCED_UPDATE, slug: "forced-update", name: "Forced update (min version)", app: APP_PLATFORMS_ONLY, any: "", split: "platform" },
];

/** Fills the template's {{app}}, {{any}} and {{split}} tokens; an empty {{any}} leaves no double space. */
export function fill(text, variant) {
  return text
    .replaceAll("{{app}}", variant.app)
    .replaceAll("{{any}}", variant.any)
    .replaceAll("{{split}}", variant.split)
    .replace(/ {2,}/g, " ");
}

/** Builds one dashboard object from the template text. */
export function buildDashboard(templateText, variant) {
  const template = JSON.parse(templateText);
  const widgets = template.widgets.map((w) => ({ ...w, title: fill(w.title, variant), query: fill(w.query, variant) }));
  const leftover = widgets.find((w) => /\{\{\w+\}\}/.test(w.query + w.title));
  if (leftover) throw new Error(`widget ${leftover.id} has an unknown {{token}}`);
  return { ...template, slug: variant.slug, name: variant.name, widgets };
}

function render(dashboard) {
  return JSON.stringify(dashboard, null, 2) + "\n";
}

function outputs() {
  return VARIANTS.map((v) => {
    const templateText = readFileSync(join(analyticsDir, v.template), "utf8");
    return { path: join(outDir, `${v.slug}.json`), text: render(buildDashboard(templateText, v)) };
  });
}

function readOrEmpty(path) {
  try {
    return readFileSync(path, "utf8");
  } catch {
    return "";
  }
}

function check() {
  const stale = outputs().filter((o) => readOrEmpty(o.path).replaceAll("\r\n", "\n") !== o.text);
  const expected = new Set(VARIANTS.map((v) => `${v.slug}.json`));
  const extra = existsSync(outDir) ? readdirSync(outDir).filter((f) => !expected.has(f)) : [];
  for (const o of stale) console.error(`out of date: ${o.path}`);
  for (const f of extra) console.error(`not made by this script: ${join(outDir, f)}`);
  return stale.length + extra.length === 0;
}

function write() {
  mkdirSync(outDir, { recursive: true });
  for (const o of outputs()) writeFileSync(o.path, o.text);
  console.log(`wrote ${VARIANTS.length} dashboards to ${outDir}`);
}

const MCP_URL = "https://app.ninjalog.dev/v1/mcp";

/** One `execute` call on Ninjalog's MCP server; returns the script's result text. */
async function execute(token, code) {
  const res = await fetch(MCP_URL, {
    method: "POST",
    headers: { authorization: `Bearer ${token}`, "content-type": "application/json", accept: "application/json, text/event-stream" },
    body: JSON.stringify({ jsonrpc: "2.0", id: 1, method: "tools/call", params: { name: "execute", arguments: { code } } }),
  });
  const text = await res.text();
  const body = parseRpc(text);
  if (!res.ok || !body || body.error || body.result?.isError || !Array.isArray(body.result?.content)) {
    throw new Error(`Ninjalog: HTTP ${res.status} ${text.slice(0, 500)}`);
  }
  return body.result.content.map((c) => c.text).join("");
}

/** A JSON-RPC reply sent as plain JSON or as one server-sent `data:` line; null when it is neither. */
export function parseRpc(text) {
  const json = text.trimStart().startsWith("{") ? text : (text.match(/^data: (.*)$/m)?.[1] ?? "");
  try {
    return JSON.parse(json);
  } catch {
    return null;
  }
}

/** The script Ninjalog runs: every widget query of every dashboard first (any error saves nothing), then the saves. */
export function uploadScript(dashboards) {
  return `const all = ${JSON.stringify(dashboards)};
const bad = [];
for (const d of all) for (const w of d.widgets) {
  try { await tools.query_logs({ query: w.query, window_seconds: 604800, plan: "none", max_chars: 200 }); }
  catch (e) { bad.push(d.slug + "/" + w.id + ": " + String(e?.message ?? e)); }
}
if (bad.length) throw new Error("queries failed, nothing saved: " + bad.join(" | "));
const out = [];
for (const d of all) { const s = await tools.save_dashboard({ dashboard: d }); out.push({ slug: d.slug, widgets: s.widgets, url: s.url }); }
return JSON.stringify(out);`;
}

async function upload() {
  const token = process.env.NINJALOG_MCP_TOKEN;
  if (!token) throw new Error("set NINJALOG_MCP_TOKEN (D:\\secrets\\dinoreader\\secrets.env)");
  console.log(await execute(token, uploadScript(outputs().map((o) => JSON.parse(o.text)))));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  if (process.argv.includes("--check")) process.exit(check() ? 0 : 1);
  write();
  if (process.argv.includes("--upload")) await upload();
}
