// Slice 287: GET /api/stats, the data behind the public Dino usage dashboard (site/dino/stats.html). Public and read-only:
// it reads only the D1 table the hourly rollup fills (worker/stats-rollup.ts), never Analytics Engine, so a visitor cannot spend
// the token's quota. Only the documented query parameters are accepted (anything else is a 400) and the answer holds aggregate
// counts only: no per-event rows, no country, no version. Answers are data, no user-facing copy.
//
//   GET /api/stats?from=YYYY-MM-DD&to=YYYY-MM-DD&group=day|week|month&platform=android,iphone&book=<id>|own
//
// Without the STATS binding, or when D1 fails, it answers 503 with `{ "error": "data_unavailable" }`.

import { DAY_MS, META_LAST_RUN, METRICS, Metric, PLATFORM_IDS, isoDay } from "./stats-rollup.ts";
import type { MetricName, StatsEnv } from "./stats-rollup.ts";

export const STATS_API_PATH = "/api/stats";
export const STATS_PAGE_PATH = "/stats";
export const DATA_ERROR = "data_unavailable";
export const BAD_REQUEST_ERROR = "bad_request";
export const CACHE_SECONDS = 300;
export const CACHE_CONTROL = `public, max-age=${CACHE_SECONDS}`;
/** The Dino version that first sends listens and played seconds (Slice 288), shown in the page's "counts from" note. */
export const LISTENS_SINCE_VERSION = "1.115";
/** The day dino-v1.115.0 was tagged (PR #901, merge 41419276): the first release that sends listens and played seconds. */
export const LISTENS_SINCE_DAY = "2026-11-07";
const NO_CACHE = "no-store";
const JSON_TYPE = "application/json; charset=utf-8";
const MAX_RANGE_DAYS = 400;
const ALLOWED_PARAMS: readonly string[] = ["from", "to", "group", "platform", "book"];
const GROUPS = ["day", "week", "month"] as const;
type Group = (typeof GROUPS)[number];
const DAY_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;
const OWN_BOOK = "own";
const BOOK_PATTERN = new RegExp(`^([0-9a-f]{32}|${OWN_BOOK})$`);
const MONDAY_OFFSET = 6;
const DAYS_IN_WEEK = 7;

export function isStatsPath(path: string): boolean {
  return path === STATS_API_PATH || path === STATS_PAGE_PATH || path === `${STATS_PAGE_PATH}/`;
}

export function isStatsApiPath(path: string): boolean {
  return path === STATS_API_PATH;
}

export interface StatsQuery {
  readonly from: string;
  readonly to: string;
  readonly group: Group;
  readonly platforms: readonly string[];
  readonly book: string | null;
}

function isRealDay(value: string | null): value is string {
  const parts = value === null ? null : DAY_PATTERN.exec(value);
  if (parts === null) return false;
  const [year, month, day] = [Number(parts[1]), Number(parts[2]), Number(parts[3])];
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

function parsePlatforms(raw: string | null): string[] | null {
  if (raw === null) return [...PLATFORM_IDS];
  const wanted = raw.split(",");
  return wanted.length > 0 && wanted.every((id) => PLATFORM_IDS.includes(id)) ? [...new Set(wanted)].sort() : null;
}

/** The checked query, or null for an unknown parameter, a bad value, a backwards or over-long range. */
export function parseStatsQuery(params: URLSearchParams): StatsQuery | null {
  if ([...params.keys()].some((name) => !ALLOWED_PARAMS.includes(name))) return null;
  const from = params.get("from");
  const to = params.get("to");
  const group = params.get("group") ?? "day";
  const book = params.get("book");
  const platforms = parsePlatforms(params.get("platform"));
  if (!isRealDay(from) || !isRealDay(to) || from > to || platforms === null) return null;
  if (!(GROUPS as readonly string[]).includes(group) || (book !== null && !BOOK_PATTERN.test(book))) return null;
  if ((Date.parse(to) - Date.parse(from)) / DAY_MS >= MAX_RANGE_DAYS) return null;
  return { from, to, group: group as Group, platforms, book };
}

/** The bucket a day falls in: the day, its Monday (UTC), or the 1st of its month. */
export function bucketKey(day: string, group: Group): string {
  if (group === "day") return day;
  if (group === "month") return `${day.slice(0, 7)}-01`;
  const time = Date.parse(day);
  const sinceMonday = (new Date(time).getUTCDay() + MONDAY_OFFSET) % DAYS_IN_WEEK;
  return isoDay(time - sinceMonday * DAY_MS);
}

export interface SeriesPoint {
  readonly key: string;
  readonly platform: string;
  readonly downloads: number;
  readonly listens: number;
  readonly seconds: number;
  readonly secondsEstimated: number;
}

export interface BookTotals {
  readonly book: string;
  readonly downloads: number;
  readonly listens: number;
  readonly seconds: number;
  readonly secondsEstimated: number;
}

type Totals = { downloads: number; listens: number; seconds: number; secondsEstimated: number };
const FIELD_BY_METRIC: Readonly<Record<MetricName, keyof Totals>> = {
  [Metric.Downloads]: "downloads",
  [Metric.Listens]: "listens",
  [Metric.Seconds]: "seconds",
  [Metric.SecondsEstimated]: "secondsEstimated",
};

function emptyTotals(): Totals {
  return { downloads: 0, listens: 0, seconds: 0, secondsEstimated: 0 };
}

interface SeriesLine { readonly day: string; readonly platform: string; readonly metric: string; readonly value: number }
interface BookLine { readonly book: string; readonly metric: string; readonly value: number }

function isMetric(name: string): name is MetricName {
  return (METRICS as readonly string[]).includes(name);
}

/** D1's per-day lines folded into one point per bucket and platform. */
export function toSeries(lines: readonly SeriesLine[], group: Group): SeriesPoint[] {
  const points = new Map<string, SeriesPoint & Totals>();
  for (const line of lines) {
    if (!isMetric(line.metric)) continue;
    const key = bucketKey(line.day, group);
    const id = `${key}|${line.platform}`;
    const point = points.get(id) ?? { key, platform: line.platform, ...emptyTotals() };
    point[FIELD_BY_METRIC[line.metric]] += line.value;
    points.set(id, point);
  }
  return [...points.values()].sort((a, b) => (a.key === b.key ? a.platform.localeCompare(b.platform) : a.key.localeCompare(b.key)));
}

export function toBookTotals(lines: readonly BookLine[]): BookTotals[] {
  const books = new Map<string, BookTotals & Totals>();
  for (const line of lines) {
    if (!isMetric(line.metric)) continue;
    const entry = books.get(line.book) ?? { book: line.book, ...emptyTotals() };
    entry[FIELD_BY_METRIC[line.metric]] += line.value;
    books.set(line.book, entry);
  }
  return [...books.values()];
}

function placeholders(count: number): string {
  return Array.from({ length: count }, () => "?").join(", ");
}

function readStatements(db: D1Database, query: StatsQuery): D1PreparedStatement[] {
  const range = [query.from, query.to];
  const where = `day BETWEEN ? AND ? AND platform IN (${placeholders(query.platforms.length)})`;
  // Own books are not counted anywhere on the dashboard (mock note "Filters"): unless the own bucket itself is asked for, the series and the book totals both leave it out, so tiles, chart and rows agree.
  const ownAsked = query.book === OWN_BOOK;
  const seriesClause = query.book === null ? " AND book != ?" : " AND book = ?";
  const seriesArgs = [...range, ...query.platforms, query.book ?? OWN_BOOK];
  const totalsClause = ownAsked ? "" : " AND book != ?";
  const totalsArgs = [...range, ...query.platforms, ...(ownAsked ? [] : [OWN_BOOK])];
  return [
    db.prepare(`SELECT day, platform, metric, SUM(value) AS value FROM daily_stats WHERE ${where}${seriesClause} GROUP BY day, platform, metric`).bind(...seriesArgs),
    db.prepare(`SELECT book, metric, SUM(value) AS value FROM daily_stats WHERE ${where}${totalsClause} GROUP BY book, metric`).bind(...totalsArgs),
    db.prepare("SELECT value FROM meta WHERE key = ?").bind(META_LAST_RUN),
    db.prepare("SELECT MIN(day) AS day FROM daily_stats"),
    db.prepare("SELECT DISTINCT platform FROM daily_stats WHERE value > 0"),
  ];
}

async function readStats(db: D1Database, query: StatsQuery): Promise<Record<string, unknown>> {
  const [series, books, meta, first, seen] = await db.batch(readStatements(db, query));
  return {
    from: query.from,
    to: query.to,
    group: query.group,
    series: toSeries((series?.results ?? []) as SeriesLine[], query.group),
    books: toBookTotals((books?.results ?? []) as BookLine[]),
    updatedAt: (meta?.results[0] as { value: string } | undefined)?.value ?? null,
    firstDay: (first?.results[0] as { day: string | null } | undefined)?.day ?? null,
    // Platforms that have any count in the table (whatever the filter), so the page can show a platform's chip as live once its rows exist.
    platforms: ((seen?.results ?? []) as { platform: string }[]).map((row) => row.platform).sort(),
    listensFrom: { day: LISTENS_SINCE_DAY, version: LISTENS_SINCE_VERSION },
  };
}

function json(body: unknown, status: number, cacheControl: string): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": JSON_TYPE, "Cache-Control": cacheControl } });
}

/** The cache key of a checked query: one URL per distinct answer (platforms already sorted, parameters in a fixed order), so reordered or noisy URLs share one entry. */
export function cacheKeyFor(origin: string, query: StatsQuery): Request {
  const key = new URL(STATS_API_PATH, origin);
  key.searchParams.set("from", query.from);
  key.searchParams.set("to", query.to);
  key.searchParams.set("group", query.group);
  key.searchParams.set("platform", query.platforms.join(","));
  if (query.book !== null) key.searchParams.set("book", query.book);
  return new Request(key.toString());
}

/** The Workers edge cache of this data centre, or undefined where there is none (node tests). */
function edgeCache(): Cache | undefined {
  return (globalThis as { caches?: { default?: Cache } }).caches?.default;
}

/** Stores an answer in the edge cache; a failed put is logged and never changes the answer the caller already has. */
async function keepInCache(cache: Cache | undefined, key: Request, response: Response): Promise<void> {
  try {
    await cache?.put(key, response);
  } catch (error) {
    console.error(`stats cache put failed: ${error instanceof Error ? error.message : String(error)}`);
  }
}

/** Answers GET /api/stats. The 200 answer is kept in the Workers Cache API for CACHE_SECONDS, keyed by the normalised query, so repeats do not touch D1. */
export async function handleStats(request: Request, env: StatsEnv, cache: Cache | undefined = edgeCache()): Promise<Response> {
  if (request.method !== "GET") return json({ error: BAD_REQUEST_ERROR }, 405, NO_CACHE);
  const url = new URL(request.url);
  const query = parseStatsQuery(url.searchParams);
  if (query === null) return json({ error: BAD_REQUEST_ERROR }, 400, NO_CACHE);
  if (env.STATS === undefined) return json({ error: DATA_ERROR }, 503, NO_CACHE);
  const key = cacheKeyFor(url.origin, query);
  try {
    const hit = await cache?.match(key);
    if (hit !== undefined) return hit;
    const response = json(await readStats(env.STATS, query), 200, CACHE_CONTROL);
    await keepInCache(cache, key, response.clone());
    return response;
  } catch (error) {
    console.error(`stats read failed: ${error instanceof Error ? error.message : String(error)}`);
    return json({ error: DATA_ERROR }, 503, NO_CACHE);
  }
}
