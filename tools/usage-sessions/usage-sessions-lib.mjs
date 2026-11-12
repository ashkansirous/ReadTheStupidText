// Slice 352: pure parts of tools/usage-sessions (query text, input checks, the token read, and how the answers are printed). No network and no files here except the token read, so
// usage-sessions.test.mjs can pin every query without touching the live data. Production dataset only (`dino_usage`); the TestFlight twin `dino_usage_beta` is never queried.

import { readFileSync } from "node:fs";

export const DATASET = "dino_usage";
export const SECRETS_ENV = "D:\\secrets\\readthestupidtext\\secrets.env";
export const TOKEN_NAME = "CF_ANALYTICS_READ_TOKEN";
// The account that owns the Worker (the same id tools/dino-usage-report.ps1 defaults to; it is an id, not a secret).
export const DEFAULT_ACCOUNT_ID = "985d711340bd2cf7bf5d71f04a7e84c9";

const SESSION_PATTERN = /^[0-9a-f]{32}$/;
const DAY_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
// Rows from test builds already stored (Slice 167 / 179): unstamped versions, and Android or iOS on Intel (emulators and simulators).
const REAL_USERS = "blob6 NOT IN ('1.0','1.0.0','1.0.0.0','0.0.0','0.0.0.0') AND NOT (blob7 IN ('android','ios') AND blob13 IN ('x64','x86'))";
const WEIGHT = "SUM(_sample_interval * double1)";
const MIN_LISTEN_SECONDS = 30;

/** The 32 lower-case hex digits of a session id, or throws: the id goes into the SQL text, so nothing else may. */
export function checkedSession(value) {
  if (typeof value !== "string" || !SESSION_PATTERN.test(value)) throw new Error("a session id is 32 lower-case hex digits");
  return value;
}

/** A yyyy-mm-dd day that is a real calendar date, or throws (it goes into the SQL text). */
export function checkedDay(value) {
  if (typeof value !== "string" || !DAY_PATTERN.test(value)) throw new Error("a day is yyyy-mm-dd");
  const date = new Date(`${value}T00:00:00Z`);
  if (Number.isNaN(date.getTime()) || date.toISOString().slice(0, 10) !== value) throw new Error("that day does not exist");
  return value;
}

/** The value of `name` in the text of an env file (quotes and spaces trimmed), or null. Never logs or returns anything else. */
export function tokenFromEnvText(text, name = TOKEN_NAME) {
  const line = text.split(/\r?\n/).find((l) => new RegExp(`^\\s*${name}\\s*=`).test(l));
  if (line === undefined) return null;
  const value = line.replace(/^[^=]*=/, "").trim().replace(/^["']|["']$/g, "");
  return value === "" ? null : value;
}

/** The token: the CLOUDFLARE_API_TOKEN variable, else the secrets store, read at run time and kept in memory only. */
export function readToken(env = process.env, read = (path) => readFileSync(path, "utf8")) {
  if (env.CLOUDFLARE_API_TOKEN) return env.CLOUDFLARE_API_TOKEN;
  let text = "";
  try {
    text = read(SECRETS_ENV);
  } catch {
    throw new Error(`no token: set CLOUDFLARE_API_TOKEN or keep ${TOKEN_NAME} in ${SECRETS_ENV}`);
  }
  const token = tokenFromEnvText(text);
  if (token === null) throw new Error(`${TOKEN_NAME} is not in ${SECRETS_ENV}`);
  return token;
}

/** One launch in order: seconds since launch, then seq (never the timestamp, which is the batch's send time). */
export function sessionSql(session) {
  const id = checkedSession(session);
  return `SELECT double6 AS since_launch, double2 AS seq, blob1 AS name, blob20 AS screen, blob9 AS detail, blob2 AS book, blob3 AS language, blob7 AS platform, blob6 AS version FROM ${DATASET} WHERE blob16 = '${id}' ORDER BY double6, double2 LIMIT 1000`;
}

/** The day's queries by name; each answers rows. `day` is the app's own UTC day (blob8). */
export function daySql(day) {
  const d = checkedDay(day);
  const base = `FROM ${DATASET} WHERE blob8 = '${d}' AND ${REAL_USERS}`;
  return {
    launches: `SELECT blob7 AS platform, ${WEIGHT} AS launches ${base} AND blob1 = 'app_opened' GROUP BY platform ORDER BY launches DESC`,
    wizardPages: `SELECT blob9 AS page_action, ${WEIGHT} AS sessions ${base} AND blob1 = 'screen' AND blob9 LIKE 'intro_%' GROUP BY page_action ORDER BY page_action`,
    reads: `SELECT blob9 AS origin, ${WEIGHT} AS reads ${base} AND blob1 = 'read_started' GROUP BY origin ORDER BY reads DESC`,
    listens: `SELECT ${WEIGHT} AS listens ${base} AND blob1 = 'book_listened' AND blob7 <> 'sonos' AND blob7 <> 'alexa'`,
    backgrounded: `SELECT blob9 AS last_screen, ${WEIGHT} AS times ${base} AND blob1 = 'app_backgrounded' GROUP BY last_screen ORDER BY times DESC`,
  };
}

const pad = (value, width) => String(value).padEnd(width);

/** A timeline as text lines: `  95s  #7  tap  reading  reading:play`. */
export function formatTimeline(rows) {
  if (rows.length === 0) return ["(no rows: an unknown session, or its counts were not sent)"];
  return rows.map((r) => `${pad(`${r.since_launch}s`, 7)} #${pad(r.seq, 4)} ${pad(r.name, 18)} ${pad(r.screen ?? "none", 16)} ${r.detail ?? ""}${r.book && r.book !== "none" ? `  book=${r.book}` : ""}`.trimEnd());
}

const page = (rows, name) => Number(rows.find((r) => r.page_action === name)?.sessions ?? 0);
const total = (rows) => rows.reduce((sum, r) => sum + Number(Object.values(r).find((v) => typeof v === "number") ?? 0), 0);

/** The wizard table: for each page, the launches that showed it and how each ended (counts are weighted, so they are estimates under sampling). */
export function wizardTable(rows) {
  const lines = [];
  for (let n = 1; n <= 4; n += 1) {
    const shown = page(rows, `intro_${n}:shown`);
    const ends = ["next", "skip", "back", "close"].map((a) => `${a} ${page(rows, `intro_${n}:${a}`)}`).join(", ");
    lines.push(`page ${n}: shown ${shown}; ${ends}`);
  }
  return lines;
}

/** The day's summary as text: launches, where they stopped in the wizard, reads by origin, 30 s listens, last screens before the app went to the background. */
export function formatDay(day, answers) {
  const launches = total(answers.launches);
  const listens = Number(answers.listens[0]?.listens ?? 0);
  const reads = total(answers.reads);
  const lines = [`Day ${day} (UTC), production builds only; weighted, so sampled launches count in full`];
  lines.push(`Launches: ${launches}${answers.launches.length ? ` (${answers.launches.map((r) => `${r.platform} ${r.launches}`).join(", ")})` : ""}`);
  lines.push("Wizard (launches that showed each page, and how it ended):", ...wizardTable(answers.wizardPages).map((l) => `  ${l}`));
  lines.push(`Reads started: ${reads}${answers.reads.length ? ` (${answers.reads.map((r) => `${r.origin === "" ? "no origin (older build)" : r.origin} ${r.reads}`).join(", ")})` : ""}`);
  lines.push(`Listened ${MIN_LISTEN_SECONDS} s or more: ${listens}`);
  lines.push(`Last screen before the app went to the background: ${answers.backgrounded.map((r) => `${r.last_screen} ${r.times}`).join(", ") || "none counted"}`);
  return lines;
}
