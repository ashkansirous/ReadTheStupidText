// Scripted screenshot and DOM check of the Dino /stats page at the five sizes of Slice 318
// (docs/plan/slices/318-dino-stats-phone-tablet.md, Verification). Serves site/dino/stats.html and answers
// /api/stats and /api/stories from fixtures inside the browser (no server, no network), then checks the DOM.
//
// Run (Playwright is not a repo dependency; install it in a scratch folder inside the worktree, such as tmp/pw; never commit it):
//   npm install --prefix tmp/pw playwright && npx --prefix tmp/pw playwright install chromium
//   PLAYWRIGHT_DIR=tmp/pw node tools/stats-page-check/check.mjs [outDir]
// PLAYWRIGHT_DIR is the folder whose node_modules holds playwright (default: the current folder). A bare `import "playwright"` would
// resolve from this script's folder, which is why `npx -p playwright node ...` fails with ERR_MODULE_NOT_FOUND.
// Screenshots go to outDir (default tmp/stats-shots). Exit 0 = every check passed.
import { readFileSync, mkdirSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const OUT = resolve(process.argv[2] ?? resolve(ROOT, "tmp/stats-shots"));
const ORIGIN = "http://stats.test";
const DAY = 864e5;
const SIZES = [
  { name: "phone-portrait", w: 390, h: 844, layout: "phonep" },
  { name: "phone-landscape", w: 844, h: 390, layout: "phonel" },
  { name: "tablet-portrait", w: 820, h: 1180, layout: "tabp" },
  { name: "tablet-landscape", w: 1180, h: 820, layout: "side" },
  { name: "desktop", w: 1280, h: 800, layout: "desktop" },
];
const STORIES = [
  { id: "a".repeat(32), title: "The Three Little Pigs", coverUrl: `/api/stories/${"a".repeat(32)}/cover?v=1` },
  { id: "b".repeat(32), title: "Good Enough", coverUrl: "https://elsewhere.example/cover.png" },
  { id: "c".repeat(32), title: "Freddy and the Slipper" },
];
const fails = [];
const foreign = [];

function check(name, ok, detail = "") {
  if (!ok) fails.push(`${name} ${detail}`.trim());
  console.log(`${ok ? "PASS" : "FAIL"} ${name}${ok ? "" : " " + detail}`);
}

function iso(t) { return new Date(t).toISOString().slice(0, 10); }
function bucketKey(t, group) {
  if (group === "day") return iso(t);
  if (group === "week") return iso(t - ((new Date(t).getUTCDay() + 6) % 7) * DAY);
  const x = new Date(t); return iso(Date.UTC(x.getUTCFullYear(), x.getUTCMonth(), 1));
}

// busy: tens of listens a day with hours of played time; quiet: four listens of 22 s to 1 min 40 s.
function fixture(mode, q) {
  const from = Date.parse(q.get("from") + "T00:00:00Z"), to = Date.parse(q.get("to") + "T00:00:00Z"), group = q.get("group");
  const total = new Map();
  const add = (t, platform, dl, li, sec, est) => {
    const key = `${bucketKey(t, group)}|${platform}`, row = total.get(key) ?? { key: bucketKey(t, group), platform, downloads: 0, listens: 0, seconds: 0, secondsEstimated: 0 };
    row.downloads += dl; row.listens += li; row.seconds += sec; row.secondsEstimated += est; total.set(key, row);
  };
  const exact = /^sec:(\d+)$/.exec(mode);
  if (exact) {
    add(to, "android", 1, 1, Number(exact[1]), 0); // exactly this many played seconds in total (the Hours tile edge cases)
  } else if (mode === "quiet") {
    add(to, "android", 1, 1, 100, 0); add(to - DAY, "iphone", 0, 1, 75, 0); add(to - 3 * DAY, "ipad", 1, 1, 64, 0); add(to - 6 * DAY, "windows", 1, 0, 22, 0);
  } else {
    for (let t = from, i = 0; t <= to; t += DAY, i++) {
      add(t, "android", 5 + (i % 7), 12 + (i % 5), 3600 * 3, 0); add(t, "iphone", 2 + (i % 4), 6, 3600, 0);
      add(t, "ipad", 1, 3, 1800, 0); add(t, "windows", 2, 4, 2400, 0); add(t, "sonos", 0, 5, 0, 3000);
    }
  }
  const books = mode === "quiet"
    ? [{ book: STORIES[0].id, downloads: 2, listens: 2, seconds: 139, secondsEstimated: 0 }, { book: "own", downloads: 0, listens: 1, seconds: 30, secondsEstimated: 0 }]
    : [{ book: STORIES[0].id, downloads: 300, listens: 500, seconds: 400000, secondsEstimated: 40000 }, { book: STORIES[1].id, downloads: 200, listens: 300, seconds: 200000, secondsEstimated: 0 },
      { book: STORIES[2].id, downloads: 50, listens: 80, seconds: 20000, secondsEstimated: 0 }, { book: "own", downloads: 0, listens: 40, seconds: 9000, secondsEstimated: 0 }];
  return { from: q.get("from"), to: q.get("to"), group, series: [...total.values()], books, updatedAt: "2026-11-08T14:00:00Z", firstDay: "2026-01-01", platforms: ["android", "iphone", "ipad", "windows", "sonos"], listensFrom: { day: "2026-11-10", version: "1.112" } };
}

async function openPage(browser, size, mode, delayMs = 0, storiesDelayMs = 0) {
  const ctx = await browser.newContext({ viewport: { width: size.w, height: size.h }, hasTouch: size.w < 700 || size.h < 500 });
  const page = await ctx.newPage();
  await page.route("**/*", async (route) => {
    const url = new URL(route.request().url());
    if (url.origin !== ORIGIN) { foreign.push(url.href); return route.abort(); }
    if (url.pathname === "/stats.html") return route.fulfill({ contentType: "text/html", body: readFileSync(resolve(ROOT, "site/dino/stats.html")) });
    if (url.pathname === "/dino/icon.png") return route.fulfill({ contentType: "image/png", body: readFileSync(resolve(ROOT, "site/dino/icon.png")) });
    if (/^\/api\/stories\/[0-9a-f]{32}\/cover$/.test(url.pathname)) return route.fulfill({ contentType: "image/png", body: readFileSync(resolve(ROOT, "site/dino/icon.png")) });
    if (url.pathname === "/api/stories") {
      if (storiesDelayMs) await new Promise((r) => setTimeout(r, storiesDelayMs));
      return route.fulfill({ contentType: "application/json", body: JSON.stringify({ stories: STORIES }) });
    }
    if (url.pathname === "/api/stats") {
      if (delayMs) await new Promise((r) => setTimeout(r, delayMs));
      if (mode === "error") return route.fulfill({ status: 500, body: "{}" });
      return route.fulfill({ contentType: "application/json", body: JSON.stringify(fixture(mode, url.searchParams)) });
    }
    return route.fulfill({ status: 404, body: "" });
  });
  await page.goto(`${ORIGIN}/stats.html`);
  return { page, ctx };
}

const visible = (page, sel) => page.locator(sel).first().isVisible();
async function settled(page) { await page.waitForSelector("#tiles .tile .num"); await page.waitForFunction(() => !document.body.className.includes("loading") && document.querySelectorAll("#bars .bar").length > 0); }

async function checkLayout(browser, size) {
  const { page, ctx } = await openPage(browser, size, "busy");
  await settled(page);
  const n = `${size.name}`;
  check(`${n}: layout`, (await page.getAttribute("html", "data-layout")) === size.layout);
  const phone = size.layout === "phonep" || size.layout === "phonel";
  check(`${n}: Filters button only on phones`, (await visible(page, "#fbtn")) === phone);
  const cols = await page.locator(size.layout === "phonep" ? "#mrows .mrow:first-child .mn" : "#rows .row:first-child .cell .n").count();
  check(`${n}: table numbers per row`, cols === (size.layout === "phonep" ? 1 : 3), `got ${cols}`);
  check(`${n}: sidebar only at 1180`, (await page.evaluate(() => getComputedStyle(document.querySelector(".left")).display === "flex")) === (size.layout === "side"));
  check(`${n}: no Own books option`, !(await page.locator("#book option").allTextContents()).some((t) => /own books/i.test(t)));
  check(`${n}: no ✎ initial`, !(await page.locator("#rows, #mrows").allTextContents()).join("").includes("✎"));
  await checkBooksAndCovers(page, n, size);
  // tap a bar
  const idle = "Tap a bar to see its numbers.";
  const bars = await page.locator("#bars .bar").evaluateAll((els) => els.map((e) => ({ role: e.getAttribute("role"), tab: e.getAttribute("tabindex") })));
  if (size.layout === "desktop") check(`${n}: bars are not buttons or tab stops`, bars.every((b) => b.role === null && b.tab === null));
  else check(`${n}: bars are buttons with exactly one tab stop`, bars.every((b) => b.role === "button") && bars.filter((b) => b.tab === "0").length === 1);
  if (size.layout === "desktop") {
    check(`${n}: no readout line`, !(await visible(page, "#readout")));
    await page.locator('.bar[data-bar="3"]').click({ force: true });
    check(`${n}: bar tap does nothing`, (await page.locator("#bars.has-pick").count()) === 0);
  } else {
    check(`${n}: idle readout`, (await page.textContent("#readout")) === idle);
    await page.locator('.bar[data-bar="3"]').click();
    const text = await page.textContent("#readout");
    check(`${n}: readout after tap`, / · .+ — Android /.test(text), text);
    check(`${n}: other bars dimmed`, (await page.locator(".bars.has-pick").count()) === 1 && (await page.locator(".bar.pk").count()) === 1);
    await page.locator('.bar[data-bar="3"]').click();
    check(`${n}: second tap clears`, (await page.textContent("#readout")) === idle && (await page.locator(".bars.has-pick").count()) === 0);
  }
  if (phone) await checkSheet(page, n);
  if (size.layout === "phonel") {
    const t = await page.locator(".tiles").boundingBox(), c = await page.locator(".chart").boundingBox();
    check(`${n}: tiles beside the chart`, t.x + t.width <= c.x + 1 && Math.abs(t.y - c.y) < 2);
    check(`${n}: summary line`, await visible(page, "#sumline"));
  }
  const heights = await page.evaluate(() => document.querySelector(".grid").getBoundingClientRect().height);
  check(`${n}: plot height`, heights === { phonep: 170, phonel: 120, tabp: 240, side: 200, desktop: 240 }[size.layout], `got ${heights}`);
  await page.screenshot({ path: resolve(OUT, `stats-${size.w}x${size.h}.png`), fullPage: size.layout !== "side" });
  await ctx.close();
}

async function checkBooksAndCovers(page, n, size) {
  const listSel = size.layout === "phonep" ? "#mrows .mrow" : "#rows .row";
  check(`${n}: one row per store book, none for own`, (await page.locator(listSel).count()) === STORIES.length, `got ${await page.locator(listSel).count()}`);
  const covers = await page.locator(`${listSel} .cover`).evaluateAll((els) => els.map((e) => e.style.backgroundImage));
  check(`${n}: own cover is set, a cover on another host is not`, covers.filter((c) => c.includes("/api/stories/")).length === 1 && !covers.some((c) => c.includes("elsewhere.example")), covers.join(" | "));
  const presets = await page.locator(".top-presets button:visible").evaluateAll((els) => els.map((e) => e.getBoundingClientRect().height));
  check(`${n}: preset labels on one line`, presets.every((h) => h <= 44), presets.join(","));
  const grid = await page.evaluate(() => getComputedStyle(document.querySelector(".grid")).backgroundImage);
  // The mock's tablet and phone plots are top-anchored; the desktop plot keeps its bottom-anchored line.
  if (size.layout !== "desktop") check(`${n}: gridlines are top-anchored`, /linear-gradient\((to bottom, )?rgb\(235, 228, 214\) 1px, rgba\(0, 0, 0, 0\) 1px/.test(grid), grid);
  const summary = size.layout === "phonep" ? await page.textContent("#fsum") : null;
  if (summary !== null) check(`${n}: default filter summary says all platforms`, /all platforms/.test(summary), summary);
}

async function checkSheet(page, n) {
  check(`${n}: sheet closed at first`, !(await visible(page, "#filters")));
  await page.click("#fbtn");
  check(`${n}: sheet opens`, await visible(page, "#filters"));
  await page.click('[data-plat="windows"]');
  await page.waitForFunction(() => /4 platforms/.test(document.getElementById("fsum").textContent));
  check(`${n}: chip changes the summary`, /4 platforms/.test(await page.textContent("#fsum")), await page.textContent("#fsum"));
  await page.click("#done");
  check(`${n}: Done closes`, !(await visible(page, "#filters")));
  await page.click("#fbtn");
  await page.mouse.click(5, 5);
  check(`${n}: scrim closes`, !(await visible(page, "#filters")));
}

async function checkTimeFormat(browser) {
  const size = SIZES[0];
  const { page, ctx } = await openPage(browser, size, "quiet");
  await settled(page);
  const hours = (await page.textContent('[data-metric="hours"] .num')).trim();
  check("quiet: Hours tile in minutes", /^(under 1 min|\d+ min)$/.test(hours), hours);
  await page.click('[data-metric="hours"]');
  check("quiet: chart titled Minutes listened per day", (await page.textContent("#ctitle")) === "Minutes listened per day", await page.textContent("#ctitle"));
  await ctx.close();
  const big = await openPage(browser, SIZES[4], "busy");
  await settled(big.page);
  const busy = (await big.page.textContent('[data-metric="hours"] .num')).trim();
  check("busy: Hours tile as N.N h or N h", /^[\d,]+(\.\d)? h$/.test(busy), busy);
  await big.page.click('[data-metric="hours"]');
  check("busy: chart titled Hours listened per day", (await big.page.textContent("#ctitle")) === "Hours listened per day");
  await big.ctx.close();
}

// The Hours tile at the edges of each format: under a minute, a minute, the hour switch (3570 s rounds to 60 min, so it shows hours), ten hours.
const FDUR_EDGES = [[59, "under 1 min"], [60, "1 min"], [3569, "59 min"], [3570, "1.0 h"], [3599, "1.0 h"], [3600, "1.0 h"], [35999, "10 h"], [36000, "10 h"]];
async function checkDurationEdges(browser) {
  for (const [seconds, expected] of FDUR_EDGES) {
    const { page, ctx } = await openPage(browser, SIZES[4], `sec:${seconds}`);
    await settled(page);
    const shownText = (await page.textContent('[data-metric="hours"] .num')).trim();
    check(`Hours tile at ${seconds} s`, shownText === expected, `got "${shownText}", want "${expected}"`);
    await ctx.close();
  }
}

async function checkErrorAndLoading(browser) {
  const err = await openPage(browser, SIZES[0], "error");
  await err.page.waitForSelector("#error.show");
  check("error: banner with Try again", (await err.page.textContent("#error")).includes("Couldn’t load the numbers.") && (await err.page.locator("#retry").isVisible()));
  await err.ctx.close();
  for (const size of [SIZES[4], SIZES[0], SIZES[1]]) await checkLoading(browser, size, 900, 0, `${size.name}: slow numbers`);
  await checkLoading(browser, SIZES[0], 0, 900, "phone-portrait: slow book titles");
}

// The overlay is absent before 500 ms and present after it, whichever of the two requests (numbers, book titles) is slow.
async function checkLoading(browser, size, statsDelay, storiesDelay, name) {
  const slow = await openPage(browser, size, "busy", statsDelay, storiesDelay);
  await slow.page.waitForTimeout(250);
  check(`loading (${name}): no overlay before 500 ms`, (await slow.page.locator("#overlay.show").count()) === 0);
  await slow.page.waitForTimeout(450);
  // The chart overlay belongs to the numbers; slow book titles only put "Loading books…" in the book rows and leave the drawn chart uncovered.
  check(`loading (${name}): overlay after 500 ms only while the numbers load`, (await slow.page.locator("#overlay.show").count()) === (storiesDelay ? 0 : 1));
  if (storiesDelay) check(`loading (${name}): rows say Loading books`, (await slow.page.locator("#rows .rowsload, #mrows .rowsload").count()) >= 1);
  if (storiesDelay) check(`loading (${name}): no short-id book rows while titles load`, (await slow.page.locator("#mrows .mrow, #rows .row").count()) === 0);
  await settled(slow.page);
  check(`loading (${name}): overlay gone when settled`, (await slow.page.locator("#overlay.show").count()) === 0);
  await slow.ctx.close();
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const { chromium } = createRequire(resolve(process.env.PLAYWRIGHT_DIR ?? process.cwd(), "package.json"))("playwright");
  const browser = await chromium.launch();
  try {
    for (const size of SIZES) await checkLayout(browser, size);
    await checkTimeFormat(browser);
    await checkDurationEdges(browser);
    await checkErrorAndLoading(browser);
  } finally {
    await browser.close();
  }
  check("no request to another host", foreign.length === 0, foreign.join(", "));
  console.log(fails.length ? `${fails.length} check(s) failed` : "all checks passed");
  process.exit(fails.length ? 1 : 0);
}

main().catch((e) => { console.error(e); process.exit(1); });
