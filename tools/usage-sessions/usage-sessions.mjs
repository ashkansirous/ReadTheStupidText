// Slice 352: one Dino launch's timeline, and a day's summary, from the live usage data (Analytics Engine dataset dino_usage, production only).
//
//   node tools/usage-sessions/usage-sessions.mjs session <32 hex session id>     one launch in order (seconds since launch, then seq), with the screen and each press
//   node tools/usage-sessions/usage-sessions.mjs day <yyyy-mm-dd>                launches, where they stopped in the wizard, reads by origin, 30 s listens
//   add --sql to print the query text and send nothing.
//
// The token is the read-only CF_ANALYTICS_READ_TOKEN: CLOUDFLARE_API_TOKEN if set, else read from D:\secrets\readthestupidtext\secrets.env at run time, kept in memory,
// never printed and never written to a file. Queries and rows are about closed values and a random per-launch id; no user text exists in the data.
import { fileURLToPath } from "node:url";
import { DATASET, DEFAULT_ACCOUNT_ID, daySql, formatDay, formatTimeline, readToken, sessionSql } from "./usage-sessions-lib.mjs";

const API = "https://api.cloudflare.com/client/v4/accounts";

/** Runs one SQL text through the Analytics Engine SQL API and returns its rows. `fetchFn` is replaced in the test. */
export async function runSql(sql, token, accountId, fetchFn = fetch) {
  const response = await fetchFn(`${API}/${accountId}/analytics_engine/sql`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}`, "Content-Type": "text/plain" },
    body: `${sql} FORMAT JSON`,
  });
  if (!response.ok) throw new Error(`the SQL API answered ${response.status}`);
  const body = await response.json();
  return Array.isArray(body.data) ? body.data : [];
}

async function dayAnswers(day, token, accountId, fetchFn) {
  const queries = daySql(day);
  const entries = await Promise.all(Object.entries(queries).map(async ([name, sql]) => [name, await runSql(sql, token, accountId, fetchFn)]));
  return Object.fromEntries(entries);
}

/** The text a command prints; with `printSql` the queries only (no token is read, nothing is sent). */
export async function run(args, env = process.env, fetchFn = fetch, tokenFn = () => readToken(env)) {
  const [command, value] = args.filter((a) => !a.startsWith("--"));
  const printSql = args.includes("--sql");
  const accountId = env.CLOUDFLARE_ACCOUNT_ID ?? DEFAULT_ACCOUNT_ID;
  if (command === "session") {
    const sql = sessionSql(value);
    return printSql ? [sql] : formatTimeline(await runSql(sql, tokenFn(), accountId, fetchFn));
  }
  if (command === "day") {
    if (printSql) return Object.entries(daySql(value)).map(([name, sql]) => `-- ${name}\n${sql}`);
    return formatDay(value, await dayAnswers(value, tokenFn(), accountId, fetchFn));
  }
  throw new Error(`usage: usage-sessions.mjs session <32 hex> | day <yyyy-mm-dd> [--sql]   (dataset ${DATASET})`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  run(process.argv.slice(2)).then(
    (lines) => console.log(lines.join("\n")),
    (error) => {
      console.error(error.message);
      process.exitCode = 1;
    },
  );
}
