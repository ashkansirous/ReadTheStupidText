// Run: node --test worker/stats*.test.mjs (node 22.18+/24 strip the types of the .ts files).
import test from "node:test";
import assert from "node:assert/strict";

import { BAD_REQUEST_ERROR, DATA_ERROR, bucketKey, handleStats, isStatsPath, parseStatsQuery, toBookTotals, toSeries } from "./stats.ts";
import { Metric, addDays, metricSql, runRollup, statsPlatform, targetDays, toStatRows } from "./stats-rollup.ts";
import { assetPageFor, canonicalRedirect, isHiddenDinoPath, resolveSite } from "./downloads.ts";

const BOOK = "0123456789abcdef0123456789abcdef";
const url = (query) => new Request(`https://dinoreader.sirous.uk/api/stats?${query}`);

/** A fake D1 that keeps the daily_stats, rollup_days and meta tables in maps and records every call. */
function fakeDb(seed = [], failDay = null) {
  const stats = new Map(seed.map((r) => [`${r.day}|${r.book}|${r.platform}|${r.metric}`, r]));
  const meta = new Map();
  const rollupDays = new Map();
  const calls = [];
  const callArgs = [];
  const ops = { count: 0 };
  const statement = (sql) => ({
    sql,
    args: [],
    bind(...args) {
      this.args = args;
      return this;
    },
    async first() {
      ops.count += 1;
      const value = meta.get(this.args[0]);
      return value === undefined ? null : { value };
    },
    async all() {
      ops.count += 1;
      if (sql.includes("FROM rollup_days")) return { results: [...rollupDays.keys()].filter((d) => d >= this.args[0] && d <= this.args[1]).map((day) => ({ day })) };
      const totals = new Map();
      for (const r of stats.values()) {
        if (r.day < this.args[0] || r.day > this.args[1]) continue;
        const key = `${r.day}|${r.metric}`;
        totals.set(key, { day: r.day, metric: r.metric, total: (totals.get(key)?.total ?? 0) + r.value });
      }
      return { results: [...totals.values()] };
    },
    async run() {
      ops.count += 1;
      if (sql.includes("INTO meta")) meta.set(this.args[0], this.args[1]);
      return {};
    },
  });
  return {
    stats,
    meta,
    rollupDays,
    calls,
    callArgs,
    ops,
    prepare: (sql) => statement(sql),
    async batch(statements) {
      ops.count += 1;
      calls.push(statements.map((s) => s.sql));
      callArgs.push(statements.map((s) => s.args));
      if (statements.length === 1 && statements[0].sql.includes("INTO rollup_days")) {
        if (failDay !== null && statements[0].args[0] === failDay) throw new Error("D1 down");
        rollupDays.set(statements[0].args[0], statements[0].args[1]);
        return [{ results: [] }];
      }
      if (statements[0].sql.includes("DELETE FROM daily_stats")) {
        if (failDay !== null && statements[0].args[0] === failDay) throw new Error("D1 down");
        for (const key of [...stats.keys()]) if (key.startsWith(`${statements[0].args[0]}|`)) stats.delete(key);
        for (const s of statements.slice(1)) {
          if (s.sql.includes("INTO rollup_days")) rollupDays.set(s.args[0], s.args[1]);
          else for (const r of JSON.parse(s.args[0])) stats.set(`${r.day}|${r.book}|${r.platform}|${r.metric}`, r);
        }
        return statements.map(() => ({ results: [] }));
      }
      return [
        { results: [{ day: "2026-11-10", platform: "android", metric: "downloads", value: 3 }, { day: "2026-11-11", platform: "android", metric: "downloads", value: 4 }] },
        { results: [{ book: BOOK, metric: "downloads", value: 7 }] },
        { results: [{ value: "2026-11-11T14:07:00.000Z" }] },
        { results: [{ day: "2026-11-01" }] },
        { results: [{ platform: "windows" }, { platform: "android" }] },
      ];
    },
  };
}

const NOW = Date.UTC(2026, 10, 11, 14);
const envOf = (db) => ({ STATS: db, CF_ANALYTICS_READ_TOKEN: "t", ANALYTICS_ACCOUNT_ID: "a" });
const row = (day, value = 5) => ({ day, book: BOOK, platform: "android", form: "phone", value });

/** A fake Analytics Engine: answers each query (one range of days) with the rows of the days in it (downloads only), records the first day of each query, and answers every query with `failing()` when it is given. */
function fakeAe(byDay = {}, failing = null) {
  const asked = [];
  const lists = [];
  const fetchFn = async (_url, init) => {
    const askedDays = /%d'\) IN \(([^)]*)\)/.exec(init.body)[1].match(/\d{4}-\d{2}-\d{2}/g);
    asked.push(askedDays[0]);
    lists.push(askedDays);
    if (failing !== null) return failing();
    const days = Object.keys(byDay).filter((d) => askedDays.includes(d));
    const data = init.body.includes("store_download") ? days.flatMap((d) => byDay[d]) : [];
    return new Response(JSON.stringify({ data }), { status: 200 });
  };
  fetchFn.asked = asked;
  fetchFn.lists = lists;
  return fetchFn;
}

test("iOS rows split by form factor, others pass through, unknown platforms are dropped", () => {
  assert.equal(statsPlatform("ios", "tablet"), "ipad");
  assert.equal(statsPlatform("ios", "phone"), "iphone");
  assert.equal(statsPlatform("ios", ""), "iphone");
  assert.equal(statsPlatform("ios", "other"), "iphone");
  assert.equal(statsPlatform("android", "tablet"), "android");
  assert.equal(statsPlatform("sonos", ""), "sonos");
  assert.equal(statsPlatform("symbian", ""), null);
});

test("each metric has its own SQL on the production dataset", () => {
  const downloads = metricSql(Metric.Downloads, ["2026-11-07", "2026-11-09"]);
  assert.match(downloads, /FROM dino_usage\b(?!_beta)/);
  assert.match(downloads, /blob1 = 'store_download'/);
  assert.match(downloads, /SUM\(_sample_interval \* double1\) AS value/);
  assert.match(metricSql(Metric.Listens, ["2026-11-07"]), /blob1 = 'book_listened'/);
  const seconds = metricSql(Metric.Seconds, ["2026-11-07"]);
  assert.match(seconds, /read_paused', 'read_stopped'/);
  assert.match(seconds, /SUM\(_sample_interval \* double1 \* double5\)/);
  assert.match(metricSql(Metric.SecondsEstimated, ["2026-11-07"]), /blob9 = 'estimated'/);
  assert.match(downloads, /IN \('2026-11-07', '2026-11-09'\)/, "exactly the run's days, so 2026-11-08 is not read");
  assert.doesNotMatch(downloads, /timestamp >=/);
});

test("iPhone lines from several form factors are summed into one row", () => {
  const rows = toStatRows(Metric.Downloads, [
    { day: "2026-11-10", book: BOOK, platform: "ios", form: "phone", value: 2 },
    { day: "2026-11-10", book: BOOK, platform: "ios", form: "", value: 1.5 },
    { day: "2026-11-10", book: BOOK, platform: "ios", form: "tablet", value: 4 },
    { day: "2026-11-10", book: BOOK, platform: "nope", form: "", value: 9 },
  ]);
  assert.deepEqual(rows.map((r) => [r.platform, r.value]).sort(), [["ipad", 4], ["iphone", 3.5]]);
});

test("targets are the oldest 10 undone days of [today-89, today-2], then yesterday and today", () => {
  const first = targetDays("2026-11-11", new Set());
  assert.equal(first.length, 12);
  assert.equal(first[0], "2026-08-14");
  assert.equal(first[9], "2026-08-23");
  assert.deepEqual(first.slice(10), ["2026-11-10", "2026-11-11"]);
  const next = targetDays("2026-11-11", new Set(first.slice(0, 10)));
  assert.equal(next[0], "2026-08-24");
  const caughtUp = targetDays("2026-11-11", new Set(daysOfWindow("2026-11-11")));
  assert.deepEqual(caughtUp, ["2026-11-10", "2026-11-11"], "today and yesterday every run, nothing else");
});

function daysOfWindow(today) {
  const days = [];
  for (let d = addDays(today, -89); d <= addDays(today, -2); d = addDays(d, 1)) days.push(d);
  return days;
}

test("re-running a day gives the same rows, and today and yesterday are read every run", async () => {
  const db = fakeDb();
  for (const d of daysOfWindow("2026-11-11")) db.rollupDays.set(d, "x");
  const env = envOf(db);
  const ae = fakeAe({ "2026-11-11": [row("2026-11-11")] });
  const first = await runRollup(env, NOW, ae);
  const afterFirst = JSON.stringify([...db.stats.entries()].sort());
  const second = await runRollup(env, NOW + 3_600_000, ae);
  assert.equal(first.status, "done");
  assert.deepEqual(second.written, ["2026-11-10", "2026-11-11"]);
  assert.equal(JSON.stringify([...db.stats.entries()].sort()), afterFirst);
  assert.equal(db.stats.size, 1);
  assert.deepEqual([...new Set(ae.asked)], ["2026-11-10"]);
  assert.equal(ae.asked.length, 8, "four metrics, one query each covering both days, two runs");
});

test("a full catch-up run stays under 50 subrequests even when every D1 statement counts: 4 AE, 1 totals read, 12 days x 3 statements", async () => {
  const db = fakeDb();
  const ae = fakeAe();
  const result = await runRollup(envOf(db), NOW, ae);
  assert.equal(result.written.length, 12);
  assert.equal(ae.asked.length, 4);
  assert.equal(db.calls.length, 12, "one batch per day");
  assert.ok(db.calls.every((statements) => statements.length === 3), "each batch is delete, ONE insert, mark");
  const statements = db.calls.reduce((n, c) => n + c.length, 0);
  const subrequests = ae.asked.length + 1 + 1 + statements + 1;
  assert.ok(subrequests < 50, `${subrequests} subrequests (4 AE + done-days read + totals read + ${statements} batch statements + meta write)`);
});

test("a day with many rows is still one insert statement with one bound parameter", async () => {
  const many = Array.from({ length: 300 }, (_, i) => ({ day: "2026-11-11", book: i.toString(16).padStart(32, "0"), platform: "android", form: "phone", value: 1 }));
  const db = fakeDb();
  for (const d of daysOfWindow("2026-11-11")) db.rollupDays.set(d, "x");
  await runRollup(envOf(db), NOW, fakeAe({ "2026-11-11": many }));
  const todayBatch = db.calls.find((c) => c.some((sql) => sql.includes("json_each")));
  assert.equal(todayBatch.length, 3);
  assert.equal(db.stats.size, 300);
});

test("each AE query names exactly the run's days, never a span between them", async () => {
  const db = fakeDb();
  db.rollupDays.set("2026-08-14", "x");
  const ae = fakeAe();
  await runRollup(envOf(db), NOW, ae);
  assert.equal(ae.lists.length, 4);
  for (const list of ae.lists) {
    assert.equal(list.length, 12);
    assert.equal(list[0], "2026-08-15", "the done day is not asked for");
    assert.equal(list.includes("2026-11-09"), false, "a day between the old days and yesterday is not read");
  }
});

test("a key AE no longer returns for a re-read day is removed", async () => {
  const stale = { day: "2026-11-11", book: "b".repeat(32), platform: "android", metric: "downloads", value: 1 };
  const db = fakeDb([stale]);
  await runRollup(envOf(db), NOW, fakeAe({ "2026-11-11": [row("2026-11-11", 5)] }));
  assert.equal(db.stats.has(`2026-11-11|${"b".repeat(32)}|android|downloads`), false);
  assert.equal(db.stats.has(`2026-11-11|${BOOK}|android|downloads`), true);
});

test("a first backfill takes 10 old days a run, oldest first, then only today and yesterday", async () => {
  const db = fakeDb();
  const ae = fakeAe();
  const first = await runRollup(envOf(db), NOW, ae);
  assert.equal(first.written.length, 12);
  assert.equal(first.written[0], "2026-08-14");
  for (let i = 0; i < 7; i += 1) await runRollup(envOf(db), NOW, ae);
  assert.equal((await runRollup(envOf(db), NOW, ae)).written.length, 10, "the last run has eight old days left");
  assert.equal((await runRollup(envOf(db), NOW, ae)).written.length, 2);
  assert.equal(db.rollupDays.size, 90);
});

test("no day older than today-89 is ever read or touched", async () => {
  const ancient = { day: "2026-08-01", book: BOOK, platform: "android", metric: "downloads", value: 40 };
  const db = fakeDb([ancient]);
  db.rollupDays.set("2026-08-01", "x");
  const ae = fakeAe();
  for (let i = 0; i < 5; i += 1) await runRollup(envOf(db), NOW, ae);
  assert.equal(ae.asked.some((d) => d < "2026-08-14"), false);
  assert.equal(db.stats.get(`2026-08-01|${BOOK}|android|downloads`).value, 40);
});

test("a failed or malformed AE answer skips the whole run, writes nothing and is retried next run", async () => {
  for (const bad of [() => new Response("no", { status: 500 }), () => new Response(JSON.stringify({ rows: [] }), { status: 200 })]) {
    const skippedDb = fakeDb();
    assert.equal((await runRollup(envOf(skippedDb), NOW, fakeAe({}, bad))).status, "failed");
    assert.equal(skippedDb.calls.length, 0, "no D1 batch");
    assert.equal(skippedDb.meta.size, 0);
  }
  const db = fakeDb();
  for (const d of daysOfWindow("2026-11-11")) db.rollupDays.set(d, "x");
  db.rollupDays.delete("2026-11-05");
  const failing = fakeAe({}, () => new Response("{}", { status: 403 }));
  const result = await runRollup(envOf(db), NOW, failing);
  assert.equal(result.status, "failed", "the whole run failed");
  assert.equal(db.stats.size, 0);
  assert.equal(db.rollupDays.has("2026-11-05"), false, "an unmarked day stays unmarked");
  const retry = await runRollup(envOf(db), NOW + 3_600_000, fakeAe({ "2026-11-05": [row("2026-11-05", 2)] }));
  assert.equal(retry.status, "done");
  assert.equal(db.rollupDays.has("2026-11-05"), true);
  assert.equal(db.stats.has(`2026-11-05|${BOOK}|android|downloads`), true);
  assert.equal(failing.asked.includes("2026-11-05"), true);
});

test("a lower total from AE keeps D1's rows, logs it, marks the day and takes no backfill slot again", async () => {
  const kept = { day: "2026-11-10", book: BOOK, platform: "android", metric: "downloads", value: 9 };
  const db = fakeDb([kept]);
  const logs = [];
  const original = console.error;
  console.error = (...args) => logs.push(args.join(" "));
  try {
    const result = await runRollup(envOf(db), NOW, fakeAe({ "2026-11-10": [row("2026-11-10", 4)], "2026-11-11": [row("2026-11-11", 1)] }));
    assert.equal(result.status, "done");
    assert.equal(result.kept.includes("2026-11-10"), true);
    assert.equal(result.skipped.includes("2026-11-10"), false);
  } finally {
    console.error = original;
  }
  assert.equal(db.stats.get(`2026-11-10|${BOOK}|android|downloads`).value, 9);
  assert.equal(db.rollupDays.has("2026-11-10"), true, "kept days are marked");
  assert.equal(logs.some((l) => l.includes("stats-rollup: lower total from AE")), true);
  assert.equal(db.stats.has(`2026-11-11|${BOOK}|android|downloads`), true);
});

test("a lower-total old day is marked and no longer takes one of the 10 backfill slots", async () => {
  const kept = { day: "2026-08-14", book: BOOK, platform: "android", metric: "downloads", value: 9 };
  const db = fakeDb([kept]);
  const ae = fakeAe({ "2026-08-14": [row("2026-08-14", 1)] });
  const first = await runRollup(envOf(db), NOW, ae);
  assert.deepEqual(first.kept, ["2026-08-14"]);
  assert.equal(db.stats.get(`2026-08-14|${BOOK}|android|downloads`).value, 9);
  assert.equal(targetDays("2026-11-11", new Set(db.rollupDays.keys())).includes("2026-08-14"), false);
  const second = await runRollup(envOf(db), NOW, ae);
  assert.equal(second.written.includes("2026-08-14"), false);
});

test("a run fails when today's day was not written or kept, even if older days were written", async () => {
  const db = fakeDb([], "2026-11-11");
  const logs = [];
  const original = console.error;
  console.error = (...args) => logs.push(args.join(" "));
  try {
    const result = await runRollup(envOf(db), NOW, fakeAe({ "2026-11-10": [row("2026-11-10", 1)] }));
    assert.equal(result.status, "failed");
  } finally {
    console.error = original;
  }
  assert.equal(db.rollupDays.has("2026-11-10"), true, "the older day is still written");
  assert.equal(db.meta.has("last_run_at"), false);
  assert.equal(logs.some((l) => l.includes("D1 batch failed for 2026-11-11")), true, "a D1 batch error is logged on its own");
  assert.equal(logs.some((l) => l.includes("Analytics Engine")), false, "and not as an AE skip");
});

test("a failed D1 batch leaves the day unmarked and without data, and the days after it are still written", async () => {
  const db = fakeDb([], "2026-11-10");
  const result = await runRollup(envOf(db), NOW, fakeAe({ "2026-11-10": [row("2026-11-10", 1)], "2026-11-11": [row("2026-11-11", 2)] }));
  assert.equal(result.status, "done");
  assert.equal(db.stats.has(`2026-11-10|${BOOK}|android|downloads`), false);
  assert.equal(db.rollupDays.has("2026-11-10"), false);
  assert.equal(db.stats.has(`2026-11-11|${BOOK}|android|downloads`), true);
  assert.equal(db.rollupDays.has("2026-11-11"), true);
});

test("last_run_at is set when a run wrote today's day, and not otherwise", async () => {
  const db = fakeDb();
  await runRollup(envOf(db), NOW, fakeAe({}, () => new Response("no", { status: 500 })));
  assert.equal(db.meta.has("last_run_at"), false);
  await runRollup(envOf(db), NOW + 3_600_000, fakeAe());
  assert.equal(db.meta.get("last_run_at"), new Date(NOW + 3_600_000).toISOString());
  assert.equal(db.meta.has("last_rollup_at"), false);
});

test("the rollup logs and returns without the binding, the token or the account id", async () => {
  const never = async () => assert.fail("no request without settings");
  assert.equal((await runRollup({}, Date.now(), never)).status, "skipped");
  assert.equal((await runRollup({ STATS: fakeDb(), ANALYTICS_ACCOUNT_ID: "a" }, Date.now(), never)).status, "skipped");
  assert.equal((await runRollup({ STATS: fakeDb(), CF_ANALYTICS_READ_TOKEN: "t" }, Date.now(), never)).status, "skipped");
});

test("an Analytics Engine error is a failed run, not a throw, and writes nothing", async () => {
  const db = fakeDb();
  const env = { STATS: db, CF_ANALYTICS_READ_TOKEN: "t", ANALYTICS_ACCOUNT_ID: "a" };
  const result = await runRollup(env, Date.now(), async () => new Response("no", { status: 403 }));
  assert.equal(result.status, "failed");
  assert.equal(db.stats.size, 0);
});

test("week keys are Mondays and month keys the 1st (UTC)", () => {
  assert.equal(bucketKey("2026-11-08", "week"), "2026-11-02");
  assert.equal(bucketKey("2026-11-09", "week"), "2026-11-09");
  assert.equal(bucketKey("2026-11-15", "week"), "2026-11-09");
  assert.equal(bucketKey("2026-01-01", "week"), "2025-12-29");
  assert.equal(bucketKey("2026-11-20", "month"), "2026-11-01");
  assert.equal(bucketKey("2026-11-20", "day"), "2026-11-20");
});

test("day lines fold into one point per bucket and platform", () => {
  const lines = [
    { day: "2026-11-09", platform: "android", metric: "downloads", value: 1 },
    { day: "2026-11-12", platform: "android", metric: "downloads", value: 2 },
    { day: "2026-11-12", platform: "android", metric: "seconds", value: 60 },
    { day: "2026-11-12", platform: "sonos", metric: "seconds_estimated", value: 30 },
    { day: "2026-11-12", platform: "sonos", metric: "bogus", value: 99 },
  ];
  assert.deepEqual(toSeries(lines, "week"), [
    { key: "2026-11-09", platform: "android", downloads: 3, listens: 0, seconds: 60, secondsEstimated: 0 },
    { key: "2026-11-09", platform: "sonos", downloads: 0, listens: 0, seconds: 0, secondsEstimated: 30 },
  ]);
  assert.deepEqual(toBookTotals([{ book: BOOK, metric: "listens", value: 2 }, { book: BOOK, metric: "downloads", value: 1 }]), [
    { book: BOOK, downloads: 1, listens: 2, seconds: 0, secondsEstimated: 0 },
  ]);
});

test("unknown parameters, bad values and bad ranges are refused", () => {
  const ok = "from=2026-11-01&to=2026-11-10";
  assert.notEqual(parseStatsQuery(new URLSearchParams(ok)), null);
  for (const bad of [`${ok}&x=1`, "from=2026-11-01", `${ok}&group=year`, `${ok}&platform=android,palm`, `${ok}&book=../x`, "from=2026-11-10&to=2026-11-01", "from=2026-02-30&to=2026-03-01", "from=2024-01-01&to=2026-11-01"]) {
    assert.equal(parseStatsQuery(new URLSearchParams(bad)), null, bad);
  }
});

test("the answer is aggregate counts only, public and cached five minutes", async () => {
  const response = await handleStats(url(`from=2026-11-01&to=2026-11-11&group=day&platform=android&book=${BOOK}`), { STATS: fakeDb() });
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("Cache-Control"), "public, max-age=300");
  const body = await response.json();
  assert.deepEqual(Object.keys(body).sort(), ["books", "firstDay", "from", "group", "listensFrom", "platforms", "series", "to", "updatedAt"]);
  assert.deepEqual(body.platforms, ["android", "windows"]);
  assert.deepEqual(body.listensFrom, { day: "2026-11-07", version: "1.115" });
  assert.equal(body.updatedAt, "2026-11-11T14:07:00.000Z");
  for (const point of body.series) assert.deepEqual(Object.keys(point).sort(), ["downloads", "key", "listens", "platform", "seconds", "secondsEstimated"]);
});

test("a bad query is a 400 and a missing binding is a 503 with a data error code", async () => {
  const bad = await handleStats(url("from=nope&to=2026-11-10"), { STATS: fakeDb() });
  assert.equal(bad.status, 400);
  assert.equal((await bad.json()).error, BAD_REQUEST_ERROR);
  const absent = await handleStats(url("from=2026-11-01&to=2026-11-10"), {});
  assert.equal(absent.status, 503);
  assert.equal(absent.headers.get("Cache-Control"), "no-store");
  assert.equal((await absent.json()).error, DATA_ERROR);
  const broken = { batch: async () => { throw new Error("d1 down"); }, prepare: () => ({ bind: () => ({}) }) };
  const failed = await handleStats(url("from=2026-11-01&to=2026-11-10"), { STATS: broken });
  assert.equal(failed.status, 503);
  assert.equal((await failed.json()).error, DATA_ERROR);
  assert.equal((await handleStats(new Request("https://dinoreader.sirous.uk/api/stats?from=2026-11-01&to=2026-11-10", { method: "POST" }), {})).status, 405);
});

test("the dashboard has one URL on the Dino host and is not a page on the RTST host", () => {
  assert.equal(canonicalRedirect("dinoreader.sirous.uk", "/dino/stats", "?x=1"), "/stats?x=1");
  assert.equal(canonicalRedirect("dinoreader.sirous.uk", "/dino/stats.html"), "/stats");
  assert.equal(canonicalRedirect("dinoreader.sirous.uk", "/stats/"), "/stats");
  assert.equal(canonicalRedirect("dinoreader.sirous.uk", "/stats"), null);
  assert.equal(canonicalRedirect("dinoreader.sirous.uk", "/stats.html"), "/stats");
  assert.equal(canonicalRedirect("x.workers.dev", "/dino/stats.html"), "/dino/stats");
  // index.ts answers 404 for every stats path unless the site's product is Dino Reader (serveStats), and hides /dino/* on other hosts.
  assert.notEqual(resolveSite("readthestupidtext.sirous.uk", "/stats").product, "dinoreader");
  assert.equal(isHiddenDinoPath("readthestupidtext.sirous.uk", "/dino/stats"), true);
  assert.equal(resolveSite("dinoreader.sirous.uk", "/stats").product, "dinoreader");
});

test("answers are kept in the edge cache under the normalised query, and only 200s", async () => {
  const store = new Map();
  const cache = {
    match: async (key) => store.get(key.url)?.clone(),
    put: async (key, response) => void store.set(key.url, response),
  };
  const db = fakeDb();
  const first = await handleStats(url("from=2026-11-01&to=2026-11-10&platform=iphone,android"), { STATS: db }, cache);
  assert.equal(first.status, 200);
  const batches = db.calls.length;
  const second = await handleStats(url("platform=android,iphone&to=2026-11-10&from=2026-11-01"), { STATS: db }, cache);
  assert.equal(second.status, 200);
  assert.equal(db.calls.length, batches, "a reordered URL is a cache hit and reads no D1");
  assert.equal(store.size, 1);
  await handleStats(url("from=nope&to=2026-11-10"), { STATS: db }, cache);
  assert.equal(store.size, 1);
});

test("a failing cache put still answers 200 with the D1 data", async () => {
  const cache = { match: async () => undefined, put: async () => { throw new Error("cache down"); } };
  const response = await handleStats(url("from=2026-11-01&to=2026-11-10"), { STATS: fakeDb() }, cache);
  assert.equal(response.status, 200);
});

test("both wrangler configs let the Worker run first for every stats page form and the API", async () => {
  const { readFileSync } = await import("node:fs");
  const { stripJsonc } = await import("./jsonc-run-first.mjs");
  for (const file of ["../wrangler.jsonc", "../wrangler.workers-dev.jsonc"]) {
    const config = JSON.parse(stripJsonc(readFileSync(new URL(file, import.meta.url), "utf8")));
    for (const path of ["/stats", "/stats/", "/stats.html", "/api/stats", "/dino/stats", "/dino/stats/", "/dino/stats.html", "/dino/api/stats"]) {
      assert.ok(config.assets.run_worker_first.includes(path), `${file} lacks ${path}`);
    }
  }
});

test("every stats path in run_worker_first is a page or a redirect on the Dino host and workers.dev, never a 404", async () => {
  const { readFileSync } = await import("node:fs");
  const { stripJsonc } = await import("./jsonc-run-first.mjs");
  const config = JSON.parse(stripJsonc(readFileSync(new URL("../wrangler.jsonc", import.meta.url), "utf8")));
  const statsPaths = config.assets.run_worker_first.filter((p) => p.includes("stats"));
  assert.ok(statsPaths.length >= 8);
  for (const [host, prefix] of [["dinoreader.sirous.uk", ""], ["x.workers.dev", "/dino"]]) {
    for (const path of statsPaths.filter((p) => (prefix === "" ? !p.startsWith("/dino/") : p.startsWith("/dino/")))) {
      const redirect = canonicalRedirect(host, path);
      const site = resolveSite(host, path);
      const served = isStatsPath(site.path);
      assert.ok(redirect !== null || served, `${host}${path} is neither redirected nor served`);
    }
  }
});

test("without a book filter the series and book totals leave the own bucket out (bound, not inlined); an explicit own filter still reads it", async () => {
  const all = fakeDb();
  await handleStats(url("from=2026-11-01&to=2026-11-11"), { STATS: all });
  const [series, books] = all.calls.at(-1);
  const [seriesArgs, bookArgs] = all.callArgs.at(-1);
  assert.match(series, /book != ?/);
  assert.match(books, /book != ?/);
  assert.doesNotMatch(series + books, /'own'/);
  assert.equal(seriesArgs.at(-1), "own");
  assert.equal(bookArgs.at(-1), "own");
  const own = fakeDb();
  await handleStats(url("from=2026-11-01&to=2026-11-11&book=own"), { STATS: own });
  const [ownSeries, ownBooks] = own.calls.at(-1);
  assert.match(ownSeries, /book = ?/);
  assert.doesNotMatch(ownSeries, /book != /);
  assert.doesNotMatch(ownBooks, /book != /, "with the own filter the book totals read the own bucket too, so rows agree with the chart");
  assert.equal(own.callArgs.at(-1)[0].at(-1), "own");
});

test("/stats is a Dino page on both hosts' mapping", () => {
  assert.equal(assetPageFor(resolveSite("dinoreader.sirous.uk", "/stats")), "/dino/stats.html");
  assert.equal(assetPageFor(resolveSite("x.workers.dev", "/dino/stats")), "/dino/stats.html");
});
