// Slice 429: the per-platform Ninjalog dashboards come from one template.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { VARIANTS, buildDashboard, fill, parseRpc, uploadScript } from "./ninjalog-dashboards.mjs";

const templateUrl = new URL("../../docs/analytics/ninjalog-dashboard.template.json", import.meta.url);
const template = readFileSync(templateUrl, "utf8");
const byKey = Object.fromEntries(VARIANTS.map((v) => [v.key, v]));

test("all-platforms dashboard keeps the old slug and leaves the speakers out", () => {
  const d = buildDashboard(template, byKey.all);
  assert.equal(d.slug, "new-users");
  const funnel = d.widgets.find((w) => w.id === "funnel");
  assert.match(funnel.query, /platform!=sonos platform!=alexa/);
  assert.match(funnel.query, /by platform$/);
});

test("a platform dashboard filters every widget to that platform and splits by version", () => {
  const d = buildDashboard(template, byKey.ios);
  for (const w of d.widgets) assert.match(w.query, /platform=ios/, w.id);
  assert.match(d.widgets.find((w) => w.id === "device-ram").query, /chart count over ram by version$/);
});

test("an empty {{any}} leaves no double space before a pipe", () => {
  assert.equal(fill("index=dino_spans {{any}} | spantree", byKey.all), "index=dino_spans | spantree");
});

test("an unknown token is refused", () => {
  const bad = JSON.stringify({ widgets: [{ id: "x", title: "t", query: "a {{nope}}" }] });
  assert.throws(() => buildDashboard(bad, byKey.all), /unknown/);
});

test("every template's widgets stay on the 12-column grid without overlapping", () => {
  for (const file of new Set(VARIANTS.map((v) => v.template))) {
    const ws = JSON.parse(readFileSync(new URL(`../../docs/analytics/${file}`, import.meta.url), "utf8")).widgets;
    assertGrid(ws);
  }
});

function assertGrid(ws) {
  for (const a of ws) {
    assert.ok(a.x + a.w <= 12, a.id);
    for (const b of ws) {
      if (a === b) continue;
      const apart = a.x + a.w <= b.x || b.x + b.w <= a.x || a.y + a.h <= b.y || b.y + b.h <= a.y;
      assert.ok(apart, `${a.id} overlaps ${b.id}`);
    }
  }
}

test("an MCP reply is read as plain JSON or as a server-sent data line", () => {
  assert.deepEqual(parseRpc('{"id":1}'), { id: 1 });
  assert.deepEqual(parseRpc('event: message\ndata: {"id":2}\n\n'), { id: 2 });
  assert.equal(parseRpc(""), null);
});

test("the upload script checks every query before the first save", () => {
  const s = uploadScript([{ slug: "a", widgets: [] }]);
  assert.ok(s.indexOf("query_logs") < s.indexOf("save_dashboard"));
  assert.match(s, /nothing saved/);
});

test("the forced-update dashboard groups by platform and leaves the speakers out", () => {
  const forced = readFileSync(new URL("../../docs/analytics/ninjalog-forced-update.template.json", import.meta.url), "utf8");
  const d = buildDashboard(forced, byKey["forced-update"]);
  assert.equal(d.slug, "forced-update");
  assert.match(d.widgets.find((w) => w.id === "update-page-sessions").query, /screen=update platform!=sonos platform!=alexa .*by platform$/);
  assert.equal(d.defaultRange, undefined, "the 24-hour table keeps its own range only without a dashboard range");
});

test("the committed dashboards match the template", () => {
  const script = fileURLToPath(new URL("./ninjalog-dashboards.mjs", import.meta.url));
  execFileSync(process.execPath, [script, "--check"], { stdio: "pipe" });
});
