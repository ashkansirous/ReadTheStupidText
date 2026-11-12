// Run: node --test tools/usage-sessions/usage-sessions.test.mjs (offline: a stubbed fetch, a stubbed token read; never the live data).
import test from "node:test";
import assert from "node:assert/strict";

import { checkedDay, checkedSession, daySql, formatDay, formatTimeline, readToken, sessionSql, tokenFromEnvText, wizardTable } from "./usage-sessions-lib.mjs";
import { run, runSql } from "./usage-sessions.mjs";

const SESSION = "0123456789abcdef0123456789abcdef";

test("only a 32 hex session id and a real yyyy-mm-dd day reach the SQL text", () => {
  assert.equal(checkedSession(SESSION), SESSION);
  for (const bad of ["", "abc", SESSION.toUpperCase(), `${SESSION}'`, `${SESSION}0`, "x' OR '1'='1", null, 5]) assert.throws(() => checkedSession(bad), String(bad));
  assert.equal(checkedDay("2026-11-12"), "2026-11-12");
  for (const bad of ["2026-13-01", "2026-02-30", "2026-11-12' OR 1=1", "today", "", "26-11-12", null]) assert.throws(() => checkedDay(bad), String(bad));
});

test("a session is read from dino_usage in time order by since-launch then seq, never by timestamp", () => {
  const sql = sessionSql(SESSION);
  assert.match(sql, /FROM dino_usage WHERE blob16 = '0123456789abcdef0123456789abcdef'/);
  assert.match(sql, /ORDER BY double6, double2/);
  assert.doesNotMatch(sql, /ORDER BY.*timestamp/);
  assert.doesNotMatch(sql, /dino_usage_beta/);
});

test("the day's queries read production only, one UTC day, with test builds left out, and count weighted", () => {
  const queries = daySql("2026-11-12");
  assert.deepEqual(Object.keys(queries), ["launches", "wizardPages", "reads", "listens", "backgrounded"]);
  for (const sql of Object.values(queries)) {
    assert.match(sql, /FROM dino_usage WHERE blob8 = '2026-11-12'/);
    assert.match(sql, /blob6 NOT IN \('1\.0'/);
    assert.match(sql, /blob13 IN \('x64','x86'\)/);
    assert.match(sql, /SUM\(_sample_interval \* double1\)/);
    assert.doesNotMatch(sql, /dino_usage_beta/);
  }
  assert.match(queries.listens, /blob1 = 'book_listened'.*<> 'sonos'.*<> 'alexa'/);
});

test("the token comes from the variable, else the secrets file, and is never part of an error", () => {
  assert.equal(readToken({ CLOUDFLARE_API_TOKEN: "from-env" }, () => assert.fail("must not read the file")), "from-env");
  assert.equal(readToken({}, () => "OTHER=1\r\nCF_ANALYTICS_READ_TOKEN = \"s3cret\"\r\n"), "s3cret");
  assert.equal(tokenFromEnvText("CF_ANALYTICS_READ_TOKEN=abc\n"), "abc");
  assert.equal(tokenFromEnvText("CF_ANALYTICS_READ_TOKEN=\n"), null);
  assert.throws(() => readToken({}, () => { throw new Error("ENOENT s3cret"); }), (e) => !e.message.includes("s3cret") && /no token/.test(e.message));
  assert.throws(() => readToken({}, () => "OTHER=1"), /is not in/);
});

test("runSql posts the SQL with a bearer token and returns the rows", async () => {
  const calls = [];
  const fetchFn = async (url, init) => {
    calls.push({ url, init });
    return { ok: true, status: 200, json: async () => ({ data: [{ a: 1 }] }) };
  };
  assert.deepEqual(await runSql("SELECT 1", "tok", "acct", fetchFn), [{ a: 1 }]);
  assert.equal(calls[0].url, "https://api.cloudflare.com/client/v4/accounts/acct/analytics_engine/sql");
  assert.equal(calls[0].init.headers.Authorization, "Bearer tok");
  assert.equal(calls[0].init.body, "SELECT 1 FORMAT JSON");
  await assert.rejects(runSql("SELECT 1", "tok", "acct", async () => ({ ok: false, status: 403 })), /403/);
});

test("--sql prints the queries and reads no token and sends nothing", async () => {
  const lines = await run(["day", "2026-11-12", "--sql"], {}, async () => assert.fail("no request"), () => assert.fail("no token"));
  assert.equal(lines.length, 5);
  const one = await run(["session", SESSION, "--sql"], {}, async () => assert.fail("no request"), () => assert.fail("no token"));
  assert.match(one[0], /blob16/);
  await assert.rejects(run(["session", "nope"], {}, async () => assert.fail("no request"), () => "t"), /32 lower-case hex/);
  await assert.rejects(run(["wat"], {}), /usage:/);
});

test("a timeline prints in the order the rows came, with the screen and the detail", () => {
  const rows = [
    { since_launch: 0, seq: 1, name: "app_opened", screen: "none", detail: "", book: "none" },
    { since_launch: 4, seq: 3, name: "screen", screen: "intro_1", detail: "intro_1:shown", book: "none" },
    { since_launch: 9, seq: 5, name: "read_started", screen: "reading", detail: "shelf", book: "0123456789abcdef0123456789abcdef" },
  ];
  const lines = formatTimeline(rows);
  assert.equal(lines.length, 3);
  assert.match(lines[1], /^4s\s+#3\s+screen\s+intro_1\s+intro_1:shown$/);
  assert.match(lines[2], /read_started.*shelf {2}book=0123/);
  assert.match(formatTimeline([])[0], /no rows/);
});

test("the wizard table and the day summary read the counts back", () => {
  const rows = [
    { page_action: "intro_1:shown", sessions: 10 }, { page_action: "intro_1:next", sessions: 8 }, { page_action: "intro_1:close", sessions: 2 },
    { page_action: "intro_2:shown", sessions: 8 }, { page_action: "intro_2:skip", sessions: 7 },
  ];
  assert.deepEqual(wizardTable(rows)[0], "page 1: shown 10; next 8, skip 0, back 0, close 2");
  assert.deepEqual(wizardTable(rows)[3], "page 4: shown 0; next 0, skip 0, back 0, close 0");
  const text = formatDay("2026-11-12", {
    launches: [{ platform: "android", launches: 15 }, { platform: "ios", launches: 4 }],
    wizardPages: rows,
    reads: [{ origin: "shelf", reads: 5 }, { origin: "", reads: 2 }],
    listens: [{ listens: 1 }],
    backgrounded: [{ last_screen: "reading", times: 3 }],
  }).join("\n");
  assert.match(text, /Launches: 19 \(android 15, ios 4\)/);
  assert.match(text, /Reads started: 7 \(shelf 5, no origin \(older build\) 2\)/);
  assert.match(text, /Listened 30 s or more: 1/);
  assert.match(text, /reading 3/);
});
