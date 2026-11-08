// Slice 287: the hourly rollup of Dino usage into D1 (`daily_stats`), so the /stats dashboard keeps its day, week and month
// history after Analytics Engine drops the raw rows. It reads the production dataset `dino_usage` (never `dino_usage_beta`)
// through the Analytics Engine SQL API with a read-only token. Rollup state (design in the slice file): the D1 table `rollup_days`
// has one row per day whose rollup was written. Each run replaces today and yesterday (UTC) always, plus at most MAX_OLD_DAYS_PER_RUN of
// the oldest days in [today-89, today-2] that have no `rollup_days` row; a day older than today-89 is never read or touched. A day is one
// D1 batch of exactly 3 statements (delete its rows, ONE multi-row insert of all its rows, mark the day), so data and mark land together or
// not at all. One AE query per metric covers exactly the run's days (an IN list; 4 fetches a run; a failed or malformed answer skips the
// whole run, nothing written); a day whose total of a metric is lower than D1's is "kept" (marked in rollup_days, rows untouched); a D1
// batch error skips that day. The run fails unless today's day was written or kept. Counting every D1 statement as a subrequest:
// 4 AE + 1 done-days read + 1 totals read + 12 days x 3 + 1 meta write = 43 at most, under 50.
// Without the STATS binding, the token or the account id it logs and returns: nothing throws.

export const Metric = {
  Downloads: "downloads",
  Listens: "listens",
  Seconds: "seconds",
  SecondsEstimated: "seconds_estimated",
} as const;
export type MetricName = (typeof Metric)[keyof typeof Metric];
export const METRICS: readonly MetricName[] = [Metric.Downloads, Metric.Listens, Metric.Seconds, Metric.SecondsEstimated];

export const Platform = {
  Android: "android",
  Iphone: "iphone",
  Ipad: "ipad",
  Windows: "windows",
  Sonos: "sonos",
  Alexa: "alexa",
} as const;
export const PLATFORM_IDS: readonly string[] = Object.values(Platform);

/** The page's "last at" time: set at the end of a run that wrote today's day. */
export const META_LAST_RUN = "last_run_at";
export const DAY_MS = 86_400_000;
/** Analytics Engine keeps raw rows about 90 days (assumed, not checked against Cloudflare's docs): a day older than today minus this is never read. */
export const RETENTION_DAYS = 89;
/** At most this many old days (no `rollup_days` row yet, oldest first) per run, besides today and yesterday. */
export const MAX_OLD_DAYS_PER_RUN = 10;
/** A new total below D1's by more than this is a bad (partial) answer. */
const TOTAL_EPSILON = 1e-6;
const IOS_PLATFORM = "ios";
const TABLET_FORM = "tablet";
const AE_SQL_URL = "https://api.cloudflare.com/client/v4/accounts";

export interface StatsEnv {
  readonly STATS?: D1Database;
  /** The read-only Analytics Engine token (a Worker secret; same value as `CF_ANALYTICS_READ_TOKEN` in the secrets store). */
  readonly CF_ANALYTICS_READ_TOKEN?: string;
  /** The Cloudflare account id (a var or a secret). */
  readonly ANALYTICS_ACCOUNT_ID?: string;
}

export interface StatRow {
  readonly day: string;
  readonly book: string;
  readonly platform: string;
  readonly metric: MetricName;
  readonly value: number;
}

export type RollupResult =
  | { readonly status: "done"; readonly rows: number; readonly written: readonly string[]; readonly kept: readonly string[]; readonly skipped: readonly string[] }
  | { readonly status: "skipped"; readonly reason: string }
  | { readonly status: "failed"; readonly reason: string };

export function isoDay(time: number): string {
  return new Date(time).toISOString().slice(0, 10);
}

/** The stats platform of an event: iOS splits by form factor (a tablet is an iPad, everything else, including unknown, an iPhone). */
export function statsPlatform(platform: unknown, formFactor: unknown): string | null {
  if (platform === IOS_PLATFORM) return formFactor === TABLET_FORM ? Platform.Ipad : Platform.Iphone;
  return typeof platform === "string" && PLATFORM_IDS.includes(platform) ? platform : null;
}

const WHERE_BY_METRIC: Readonly<Record<MetricName, string>> = {
  [Metric.Downloads]: "blob1 = 'store_download'",
  [Metric.Listens]: "blob1 = 'book_listened'",
  [Metric.Seconds]: "((blob1 IN ('read_paused', 'read_stopped') AND double5 >= 0) OR (blob1 = 'book_listened' AND blob9 = 'reported' AND double5 >= 0))",
  [Metric.SecondsEstimated]: "(blob1 = 'book_listened' AND blob9 = 'estimated' AND double5 >= 0)",
};

const VALUE_BY_METRIC: Readonly<Record<MetricName, string>> = {
  [Metric.Downloads]: "SUM(_sample_interval * double1)",
  [Metric.Listens]: "SUM(_sample_interval * double1)",
  [Metric.Seconds]: "SUM(_sample_interval * double1 * double5)",
  [Metric.SecondsEstimated]: "SUM(_sample_interval * double1 * double5)",
};

const DAY_EXPRESSION = "formatDateTime(timestamp, '%Y-%m-%d')";

/** The Analytics Engine SQL for one metric over exactly the given UTC days (an IN list, never a min-max span). */
export function metricSql(metric: MetricName, days: readonly string[]): string {
  const dayList = days.map((day) => `'${day}'`).join(", ");
  return [
    `SELECT ${DAY_EXPRESSION} AS day, blob2 AS book, blob7 AS platform, blob12 AS form, ${VALUE_BY_METRIC[metric]} AS value`,
    "FROM dino_usage",
    `WHERE ${WHERE_BY_METRIC[metric]} AND ${DAY_EXPRESSION} IN (${dayList})`,
    "GROUP BY day, book, platform, form",
    "FORMAT JSON",
  ].join("\n");
}

/** Folds query rows into stat rows: unknown platforms are dropped, iPhone rows from several form factors are summed. */
export function toStatRows(metric: MetricName, data: readonly Record<string, unknown>[]): StatRow[] {
  const merged = new Map<string, StatRow>();
  for (const line of data) {
    const platform = statsPlatform(line["platform"], line["form"]);
    const value = Number(line["value"]);
    if (platform === null || !Number.isFinite(value) || typeof line["day"] !== "string" || typeof line["book"] !== "string" || line["book"] === "") continue;
    const key = `${line["day"]}|${line["book"]}|${platform}`;
    const before = merged.get(key);
    merged.set(key, { day: line["day"], book: line["book"], platform, metric, value: (before?.value ?? 0) + value });
  }
  return [...merged.values()];
}

export function addDays(day: string, days: number): string {
  return isoDay(Date.parse(`${day}T00:00:00Z`) + days * DAY_MS);
}

/** The days one run works on, oldest first: the oldest `MAX_OLD_DAYS_PER_RUN` of [today-89, today-2] not in `done`, then yesterday and today. */
export function targetDays(today: string, done: ReadonlySet<string>): string[] {
  const old: string[] = [];
  for (let day = addDays(today, -RETENTION_DAYS); day <= addDays(today, -2) && old.length < MAX_OLD_DAYS_PER_RUN; day = addDays(day, 1)) {
    if (!done.has(day)) old.push(day);
  }
  return [...old, addDays(today, -1), today];
}

/** One metric's rows over exactly the given UTC days (one fetch); a non-200 or an answer without a `data` array throws (the run is then skipped). */
async function queryMetric(env: StatsEnv, fetchFn: typeof fetch, metric: MetricName, days: readonly string[]): Promise<StatRow[]> {
  const response = await fetchFn(`${AE_SQL_URL}/${env.ANALYTICS_ACCOUNT_ID}/analytics_engine/sql`, {
    method: "POST",
    headers: { Authorization: `Bearer ${env.CF_ANALYTICS_READ_TOKEN}` },
    body: metricSql(metric, days),
  });
  if (response.status !== 200) throw new Error(`Analytics Engine answered ${response.status} for ${metric}`);
  const body = (await response.json()) as { data?: unknown } | null;
  if (body === null || typeof body !== "object" || !Array.isArray(body.data)) throw new Error(`Analytics Engine answer for ${metric} has no data array`);
  return toStatRows(metric, body.data as Record<string, unknown>[]);
}

/** The run's rows grouped by day: one AE fetch per metric (four in all) covering every target day. */
async function queryDays(env: StatsEnv, fetchFn: typeof fetch, days: readonly string[]): Promise<Map<string, StatRow[]>> {
  const perMetric = await Promise.all(METRICS.map((metric) => queryMetric(env, fetchFn, metric, days)));
  const byDay = new Map<string, StatRow[]>(days.map((day) => [day, []]));
  for (const row of perMetric.flat()) byDay.get(row.day)?.push(row);
  return byDay;
}

/** ONE multi-row insert for a whole day with ONE bound parameter (a JSON array of rows), so D1's per-statement bound-parameter limit never splits it. */
const INSERT_DAY_SQL =
  "INSERT INTO daily_stats (day, book, platform, metric, value) " +
  "SELECT json_extract(value, '$.day'), json_extract(value, '$.book'), json_extract(value, '$.platform'), json_extract(value, '$.metric'), json_extract(value, '$.value') " +
  "FROM json_each(?)";
const META_SQL = "INSERT INTO meta (key, value) VALUES (?, ?) ON CONFLICT (key) DO UPDATE SET value = excluded.value";
const DELETE_DAY_SQL = "DELETE FROM daily_stats WHERE day = ?";
const MARK_DAY_SQL = "INSERT INTO rollup_days (day, rolled_at) VALUES (?, ?) ON CONFLICT (day) DO UPDATE SET rolled_at = excluded.rolled_at";
const DAYS_TOTALS_SQL = "SELECT day, metric, SUM(value) AS total FROM daily_stats WHERE day >= ? AND day <= ? GROUP BY day, metric";
const DONE_DAYS_SQL = "SELECT day FROM rollup_days WHERE day >= ? AND day <= ?";

/** Replaces one UTC day in one D1 batch of exactly 3 statements: delete its rows, insert all the new ones at once, mark the day done. All or nothing. */
async function replaceDay(db: D1Database, day: string, rows: readonly StatRow[], now: number): Promise<void> {
  const rowsJson = JSON.stringify(rows.map((row) => ({ day: row.day, book: row.book, platform: row.platform, metric: row.metric, value: row.value })));
  await db.batch([db.prepare(DELETE_DAY_SQL).bind(day), db.prepare(INSERT_DAY_SQL).bind(rowsJson), db.prepare(MARK_DAY_SQL).bind(day, new Date(now).toISOString())]);
}

function totalsOf(rows: readonly { readonly metric: string; readonly value: number }[]): Map<string, number> {
  const totals = new Map<string, number>();
  for (const row of rows) totals.set(row.metric, (totals.get(row.metric) ?? 0) + row.value);
  return totals;
}

/** D1's stored totals per day and metric for [first, last], in one query. */
async function storedTotals(db: D1Database, first: string, last: string): Promise<Map<string, Map<string, number>>> {
  const stored = await db.prepare(DAYS_TOTALS_SQL).bind(first, last).all<{ day: string; metric: string; total: number }>();
  const byDay = new Map<string, Map<string, number>>();
  for (const line of stored.results ?? []) {
    const metrics = byDay.get(line.day) ?? new Map<string, number>();
    metrics.set(line.metric, line.total);
    byDay.set(line.day, metrics);
  }
  return byDay;
}

/** True when any metric's new total for the day is lower than the total D1 already holds (AE only appends inside its retention, so that answer is partial). */
function lowerThanStored(stored: ReadonlyMap<string, number> | undefined, rows: readonly StatRow[]): boolean {
  const fresh = totalsOf(rows);
  return [...(stored ?? [])].some(([metric, total]) => (fresh.get(metric) ?? 0) < total - TOTAL_EPSILON);
}

/** Marks a day done without touching its rows (D1 already holds the higher, correct data), so it takes no more backfill slots. */
async function markDay(db: D1Database, day: string, now: number): Promise<void> {
  await db.batch([db.prepare(MARK_DAY_SQL).bind(day, new Date(now).toISOString())]);
}

async function doneDays(db: D1Database, today: string): Promise<Set<string>> {
  const result = await db.prepare(DONE_DAYS_SQL).bind(addDays(today, -RETENTION_DAYS), addDays(today, -2)).all<{ day: string }>();
  return new Set((result.results ?? []).map((row) => row.day));
}

type DayOutcome = { readonly kind: "written"; readonly rows: number } | { readonly kind: "kept" } | { readonly kind: "failed" };

/** Writes one day (one D1 batch): kept when AE's total is lower than D1's (marked, rows untouched), failed when the D1 batch throws (logged, retried next run). */
async function rollupDay(db: D1Database, day: string, rows: readonly StatRow[], stored: ReadonlyMap<string, number> | undefined, now: number): Promise<DayOutcome> {
  try {
    if (lowerThanStored(stored, rows)) {
      console.error(`stats-rollup: lower total from AE for ${day}, kept D1's rows`);
      await markDay(db, day, now);
      return { kind: "kept" };
    }
    await replaceDay(db, day, rows, now);
    return { kind: "written", rows: rows.length };
  } catch (error) {
    console.error(`stats rollup: D1 batch failed for ${day}, retried next run: ${error instanceof Error ? error.message : String(error)}`);
    return { kind: "failed" };
  }
}

function missingSetting(env: StatsEnv): string | null {
  if (env.STATS === undefined) return "no STATS binding";
  if (!env.CF_ANALYTICS_READ_TOKEN) return "no CF_ANALYTICS_READ_TOKEN secret";
  if (!env.ANALYTICS_ACCOUNT_ID) return "no ANALYTICS_ACCOUNT_ID";
  return null;
}

/** One rollup run. `now` and `fetchFn` are seams for tests. */
export async function runRollup(env: StatsEnv, now: number = Date.now(), fetchFn: typeof fetch = fetch): Promise<RollupResult> {
  const missing = missingSetting(env);
  if (missing !== null || env.STATS === undefined) {
    console.log(`stats rollup skipped: ${missing}`);
    return { status: "skipped", reason: missing ?? "" };
  }
  const db = env.STATS;
  const today = isoDay(now);
  let days: string[];
  let byDay: Map<string, StatRow[]>;
  try {
    days = targetDays(today, await doneDays(db, today));
    byDay = await queryDays(env, fetchFn, days);
  } catch (error) {
    return failRun(`Analytics Engine or state read failed, run skipped, nothing written: ${error instanceof Error ? error.message : String(error)}`);
  }
  try {
    return await writeDays(db, days, byDay, today, now);
  } catch (error) {
    return failRun(`D1 failed: ${error instanceof Error ? error.message : String(error)}`);
  }
}

function failRun(reason: string): RollupResult {
  console.error(`stats rollup failed: ${reason}`);
  return { status: "failed", reason };
}

/** One D1 totals read, then one 3-statement D1 batch per day (at most 12 days): decides each day, and the run fails unless today's day was written or kept. */
async function writeDays(db: D1Database, days: readonly string[], byDay: ReadonlyMap<string, readonly StatRow[]>, today: string, now: number): Promise<RollupResult> {
  const stored = await storedTotals(db, days[0] ?? today, today);
  const written: string[] = [];
  const kept: string[] = [];
  const skipped: string[] = [];
  let rows = 0;
  for (const day of days) {
    const outcome = await rollupDay(db, day, byDay.get(day) ?? [], stored.get(day), now);
    if (outcome.kind === "written") {
      written.push(day);
      rows += outcome.rows;
    } else (outcome.kind === "kept" ? kept : skipped).push(day);
  }
  if (!written.includes(today) && !kept.includes(today)) return failRun(`today's day ${today} was not written (skipped: ${skipped.join(", ")})`);
  await db.prepare(META_SQL).bind(META_LAST_RUN, new Date(now).toISOString()).run();
  console.log(`stats rollup done: ${rows} rows over ${written.length} days${kept.length > 0 ? `, kept ${kept.join(", ")}` : ""}${skipped.length > 0 ? `, skipped ${skipped.join(", ")}` : ""}`);
  return { status: "done", rows, written, kept, skipped };
}
