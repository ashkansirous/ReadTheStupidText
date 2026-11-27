// Run: node --test worker/sonos-link.test.mjs (Slice 438: every Sonos home links through the Dino app and saves its story language).
// The D1 database is a real SQLite (node:sqlite) running worker/schema/sonos-homes.sql, so the SQL the Worker sends is the SQL that is tested.
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";

import { serveObject } from "./downloads.ts";
import { handleSonos, isSonosPath } from "./sonos.ts";
import { STORY_LANGUAGES, callbackWithCode, currentWord, isSonosCallback, resetSubscriptionAnswers, storyLanguageTap, storyLanguageTitle } from "./sonos-link.ts";
import { LABEL_LANGUAGES, SUBSCRIBE_MESSAGES } from "./sonos-labels.ts";
import { parseSonosRequest } from "./sonos-soap.ts";

const NS = "http://www.sonos.com/Services/1.1";
const ID_A = "7d0e5a4e000040008000000000000001";
const ID_C = "7d0e5a4e000040008000000000000003";
const HOME_1 = "Sonos_HOME_ONE_abc123";
const HOME_2 = "Sonos_HOME_TWO_def456";
const CALLBACK = "sonos://sonos.com/applink?state=STATE42";
const PROOF = "ios:uk.sirous.dinoreader.sub.monthly:txn-100001";
const PROOF_RENEWED = "ios:uk.sirous.dinoreader.sub.monthly:txn-100002";
const NOW = 1_800_000_000_000;

function bookFiles(id, meta, listenLangs) {
  const folder = `dinoreader/stories/${id}/`;
  const files = [[`${folder}meta.json`, Buffer.from(JSON.stringify(meta))], [`${folder}story-v2.dino`, Buffer.from("pkg")], [`${folder}cover.jpg`, Buffer.from("jpg")]];
  for (const lang of listenLangs) files.push([`${folder}audio-${lang}-v3.zip`, Buffer.from("zip")], [`${folder}listen-${lang}-v3.m4a`, Buffer.from("m4a")]);
  return files;
}

function bucketOf(files) {
  const objects = new Map(files);
  return {
    async list({ prefix }) {
      return { objects: [...objects.keys()].filter((key) => key.startsWith(prefix)).map((key) => ({ key })), truncated: false };
    },
    async head(key) {
      return objects.has(key) ? { size: objects.get(key).length, httpEtag: '"e"' } : null;
    },
    async get(key) {
      const body = objects.get(key);
      return body ? { size: body.length, httpEtag: '"e"', body: new Response(body).body, json: async () => JSON.parse(body.toString()) } : null;
    },
  };
}

const bookA = { schemaVersion: 1, title: "Goldilocks & Bears", language: "en", version: 2, added: "2026-11-01", titles: { fa: "دخترک" }, ageMin: 3, ageMax: 5, audio: { en: { version: 3, bytes: 5, listenSeconds: 600 }, fa: { version: 3, bytes: 5, listenSeconds: 540 } } };
const bookC = { schemaVersion: 1, title: "Second", language: "en", version: 2, added: "2026-11-01", audio: { en: { version: 3, bytes: 5, listenSeconds: 120 } } };
const bucket = { DOWNLOADS: bucketOf([...bookFiles(ID_A, bookA, ["en", "fa"]), ...bookFiles(ID_C, bookC, ["en"])]) };

// A D1Database over node:sqlite: prepare().bind() with first/run/all, and batch() as one transaction.
function fakeD1() {
  const sqlite = new DatabaseSync(":memory:");
  sqlite.exec(readFileSync(new URL("./schema/sonos-homes.sql", import.meta.url), "utf8"));
  const statement = (sql, args = []) => ({
    sql,
    args,
    bind: (...values) => statement(sql, values),
    first: async () => sqlite.prepare(sql).get(...args) ?? null,
    all: async () => ({ results: sqlite.prepare(sql).all(...args) }),
    run: async () => ({ meta: { changes: Number(sqlite.prepare(sql).run(...args).changes) } }),
  });
  return {
    sqlite,
    prepare: (sql) => statement(sql),
    async batch(statements) {
      sqlite.exec("BEGIN");
      try {
        const results = [];
        for (const item of statements) results.push(await item.run());
        sqlite.exec("COMMIT");
        return results;
      } catch (error) {
        sqlite.exec("ROLLBACK");
        throw error;
      }
    },
  };
}

function rows(db, table) {
  return db.sqlite.prepare(`SELECT * FROM ${table}`).all();
}

function loginHeader(login) {
  return login === null ? "" : `<credentials xmlns="${NS}"><loginToken><token>${login.token}</token><key>${login.key}</key><householdId>${login.householdId}</householdId></loginToken><deviceId>XX</deviceId></credentials>`;
}

function envelope(action, inner = "", login = null) {
  return `<?xml version="1.0"?><s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Header>${loginHeader(login)}</s:Header><s:Body><ns:${action} xmlns:ns="${NS}">${inner}</ns:${action}></s:Body></s:Envelope>`;
}

function world(now = NOW) {
  const db = fakeD1();
  const clock = { now };
  // Slice 439: the subscription source the Worker builds from the paid gate; here it answers what `sub.answer` says and notes what it was asked.
  // The default answer is an active subscription, so a link made with a proof passes the store check at confirm (review 1, finding 3); tests change `sub.answer` after linking.
  const sub = { answer: { kind: "status", status: { state: "active", expiresAt: "2027-01-01T00:00:00Z" } }, asked: [] };
  const subscription = async (proof, clientKey) => {
    sub.asked.push({ proof, clientKey });
    if (proof === "") return sub.answer.kind === "off" ? sub.answer : { kind: "no-proof" }; // the real source reads its switch first, then wants a proof
    return sub.answer;
  };
  resetSubscriptionAnswers();
  const links = { db, now: () => clock.now, subscription };
  const soap = async (action, inner = "", { login = null, language = "en", host = "https://dino.test", files = bucket, headers = {} } = {}) => {
    const request = new Request(`${host}/sonos`, { method: "POST", body: envelope(action, inner, login), headers: { "Accept-Language": language, ...headers } });
    const response = await handleSonos(request, files, "", serveObject, undefined, undefined, links);
    return { status: response.status, text: await response.text() };
  };
  const api = async (body, extraHeaders = {}) => {
    const response = await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body: JSON.stringify(body), headers: { "Content-Type": "application/json", ...extraHeaders } }), bucket, "", serveObject, undefined, undefined, links);
    return { status: response.status, json: await response.json() };
  };
  // Slice 439: a GET of a file route of this module (the paid-book media route, the subscribe message), as a player sends it.
  const get = async (path, { token = null, method = "GET", headers = {}, files = bucket } = {}) => {
    const sent = { ...headers, ...(token === null ? {} : { Authorization: `Bearer ${token}` }) };
    const response = await handleSonos(new Request(`https://dino.test${path}`, { method, headers: sent }), files, "", serveObject, undefined, undefined, links);
    return { status: response.status, text: await response.text(), headers: response.headers };
  };
  return { db, clock, links, soap, api, get, sub };
}

const between = (text, tagName) => new RegExp(`<${tagName}>([^<]*)</${tagName}>`).exec(text)?.[1];

async function startLink(w, household = HOME_1) {
  const { text } = await w.soap("getAppLink", `<ns:householdId>${household}</ns:householdId><ns:hardware>x</ns:hardware><ns:callbackPath>${CALLBACK}</ns:callbackPath>`);
  return between(text, "linkCode");
}

async function fullLink(w, { household = HOME_1, language = "fa", proof = null } = {}) {
  const code = await startLink(w, household);
  const confirmed = await w.api({ op: "confirm", code, language, ...(proof === null ? {} : { proof }) });
  assert.equal(confirmed.status, 200);
  const { text } = await w.soap("getDeviceAuthToken", `<ns:householdId>${household}</ns:householdId><ns:linkCode>${code}</ns:linkCode>`);
  return { code, login: { token: between(text, "authToken"), key: between(text, "privateKey"), householdId: household } };
}

const browse = (w, login, id = "root", options = {}) => w.soap("getMetadata", `<ns:id>${id}</ns:id><ns:index>0</ns:index><ns:count>100</ns:count>`, { login, ...options });

test("getAppLink returns the app link with a link code and keeps one pending row (hashes, not the household id)", async () => {
  const w = world();
  const { status, text } = await w.soap("getAppLink", `<ns:householdId>${HOME_1}</ns:householdId><ns:callbackPath>${CALLBACK}</ns:callbackPath>`);
  assert.equal(status, 200);
  const code = between(text, "linkCode");
  assert.match(code, /^[0-9a-f]{32}$/);
  assert.equal(between(text, "appUrl"), `https://dino.test/dino/sonos-link.html?code=${code}`);
  assert.equal(between(text, "appUrlStringId"), "SIGN_IN");
  const pending = rows(w.db, "sonos_pending_links");
  assert.equal(pending.length, 1);
  assert.equal(pending[0].story_language, null);
  assert.ok(!JSON.stringify(pending).includes(HOME_1) && !JSON.stringify(pending).includes(code));
});

test("getAppLink refuses a missing household or a callback that is not Sonos's", async () => {
  const w = world();
  for (const inner of [`<ns:callbackPath>${CALLBACK}</ns:callbackPath>`, `<ns:householdId>${HOME_1}</ns:householdId><ns:callbackPath>https://evil.example/x?state=1</ns:callbackPath>`, `<ns:householdId>${HOME_1}</ns:householdId><ns:callbackPath>javascript:alert(1)</ns:callbackPath>`]) {
    const { status, text } = await w.soap("getAppLink", inner);
    assert.equal(status, 500);
    assert.match(text, /Server\.ServiceUnknownError/);
  }
  assert.equal(rows(w.db, "sonos_pending_links").length, 0);
  assert.equal(isSonosCallback("https://play.sonos.com/x?state=1"), true);
  assert.equal(isSonosCallback("https://sonos.com.evil.example/"), false);
  assert.equal(isSonosCallback("sonos-app://x?state=1"), true);
  assert.equal(callbackWithCode(CALLBACK, "abc"), "sonos://sonos.com/applink?state=STATE42&code=abc");
});

test("getDeviceAuthToken asks Sonos to retry until the app confirms, then gives tokens; the callback carries the code and state", async () => {
  const w = world();
  const code = await startLink(w);
  const early = await w.soap("getDeviceAuthToken", `<ns:householdId>${HOME_1}</ns:householdId><ns:linkCode>${code}</ns:linkCode>`);
  assert.equal(early.status, 500);
  assert.match(early.text, /<faultcode>Client\.NOT_LINKED_RETRY<\/faultcode>/);
  assert.match(early.text, /<ExceptionInfo>NOT_LINKED_RETRY<\/ExceptionInfo><SonosError>5<\/SonosError>/);
  const confirmed = await w.api({ op: "confirm", code, language: "fa" });
  assert.equal(confirmed.status, 200);
  assert.equal(confirmed.json.callback, `${CALLBACK}&code=${code}`);
  const done = await w.soap("getDeviceAuthToken", `<ns:householdId>${HOME_1}</ns:householdId><ns:linkCode>${code}</ns:linkCode>`);
  assert.equal(done.status, 200);
  assert.match(between(done.text, "authToken"), /^[0-9a-f]{64}$/);
  assert.match(between(done.text, "privateKey"), /^[0-9a-f]{64}$/);
  const homes = rows(w.db, "sonos_homes");
  assert.equal(homes.length, 1);
  assert.equal(homes[0].story_language, "fa");
  assert.equal(rows(w.db, "sonos_pending_links").length, 0);
  const stored = JSON.stringify(homes);
  for (const secret of [HOME_1, between(done.text, "authToken"), between(done.text, "privateKey")]) assert.ok(!stored.includes(secret), "only hashes are stored");
});

test("one token pair per link: a second getDeviceAuthToken for the same code fails and the first pair keeps working", async () => {
  const w = world();
  const { code, login } = await fullLink(w);
  const second = await w.soap("getDeviceAuthToken", `<ns:householdId>${HOME_1}</ns:householdId><ns:linkCode>${code}</ns:linkCode>`);
  assert.match(second.text, /Client\.NOT_LINKED_FAILURE/);
  assert.equal((await browse(w, login)).status, 200);
});

test("a getAppLink for a linked home changes nothing stored until the new link is confirmed and its tokens are fetched", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "fa" });
  const before = JSON.stringify(rows(w.db, "sonos_homes"));
  const code = await startLink(w);
  assert.equal(JSON.stringify(rows(w.db, "sonos_homes")), before);
  assert.equal((await browse(w, login)).status, 200);
  const early = await w.soap("getDeviceAuthToken", `<ns:householdId>${HOME_1}</ns:householdId><ns:linkCode>${code}</ns:linkCode>`);
  assert.match(early.text, /Client\.NOT_LINKED_RETRY/);
  assert.equal(JSON.stringify(rows(w.db, "sonos_homes")), before);
  w.clock.now += 3_600_001;
  assert.equal((await w.api({ op: "confirm", code, language: "en" })).status, 404);
  assert.equal(JSON.stringify(rows(w.db, "sonos_homes").map((row) => [row.token_hash, row.story_language])), JSON.stringify(JSON.parse(before).map((row) => [row.token_hash, row.story_language])));
});

test("a start on a linked home leaves its row byte-identical, and a home keeps at most 3 pending rows (the 4th start drops the oldest)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "fa", proof: PROOF });
  const before = JSON.stringify(rows(w.db, "sonos_homes"));
  const codes = [];
  for (let i = 0; i < 5; i += 1) {
    w.clock.now += 1000;
    codes.push(await startLink(w));
    assert.equal(JSON.stringify(rows(w.db, "sonos_homes")), before, "starting never writes the home row");
  }
  assert.equal(rows(w.db, "sonos_pending_links").length, 3);
  await startLink(w, HOME_2);
  assert.equal(rows(w.db, "sonos_pending_links").length, 4, "another home has its own cap");
  assert.equal((await w.api({ op: "confirm", code: codes[0], language: "en" })).status, 404, "the oldest pending row went");
  assert.equal((await w.api({ op: "confirm", code: codes[4], language: "en" })).status, 200, "the newest stays");
  assert.equal((await browse(w, login)).status, 200);
});

test("a relink without a proof clears the stored proof and its hash, as the app forgets it too (review 2, finding 4)", async () => {
  const w = world();
  await fullLink(w, { language: "fa", proof: PROOF });
  assert.notEqual(rows(w.db, "sonos_homes")[0].proof_hash, null);
  await fullLink(w, { language: "en" });
  const [row] = rows(w.db, "sonos_homes");
  assert.deepEqual([row.proof_hash, row.proof, row.story_language], [null, null, "en"]);
  assert.equal((await w.api({ op: "unlink", proof: PROOF })).json.removed, 0, "no home holds the old proof any more");
});

test("a database without the proof column still links: the row is written without the proof and the home has no subscription (review 2, finding 1)", async () => {
  const w = world();
  w.db.sqlite.exec("ALTER TABLE sonos_homes DROP COLUMN proof; ALTER TABLE sonos_pending_links DROP COLUMN proof");
  const warned = [];
  const original = console.warn;
  console.warn = (line) => warned.push(String(line));
  try {
    const { login } = await fullLink(w, { language: "en", proof: PROOF });
    const browsed = await browse(w, login);
    assert.equal(browsed.status, 200);
    assert.equal(rows(w.db, "sonos_homes").length, 1);
    await fullLink(w, { language: "fa" });
    assert.equal(rows(w.db, "sonos_homes")[0].story_language, "fa", "a relink works too");
  } finally {
    console.warn = original;
  }
  assert.ok(warned.some((line) => line.includes("fallback=no-proof-column")));
  assert.ok(warned.every((line) => !line.includes(PROOF) && !line.includes(HOME_1)));
});

test("every link call purges expired pending links, not only getAppLink", async () => {
  const w = world();
  await startLink(w, HOME_2);
  assert.equal(rows(w.db, "sonos_pending_links").length, 1);
  w.clock.now += 3_700_000;
  await w.api({ op: "confirm", code: "0".repeat(32), language: "en" });
  assert.equal(rows(w.db, "sonos_pending_links").length, 0);
  await startLink(w, HOME_2);
  w.clock.now += 3_700_000;
  await w.soap("getDeviceAuthToken", `<ns:householdId>${HOME_1}</ns:householdId><ns:linkCode>${"0".repeat(32)}</ns:linkCode>`);
  assert.equal(rows(w.db, "sonos_pending_links").length, 0);
  await startLink(w, HOME_2);
  w.clock.now += 3_700_000;
  await w.api({ op: "unlink", proof: PROOF });
  assert.equal(rows(w.db, "sonos_pending_links").length, 0);
});

test("a wrong household, an unknown code and an expired code are the permanent not-linked failure", async () => {
  const w = world();
  const code = await startLink(w);
  await w.api({ op: "confirm", code, language: "en" });
  const wrong = await w.soap("getDeviceAuthToken", `<ns:householdId>${HOME_2}</ns:householdId><ns:linkCode>${code}</ns:linkCode>`);
  assert.match(wrong.text, /Client\.NOT_LINKED_FAILURE/);
  assert.match(wrong.text, /<SonosError>6<\/SonosError>/);
  const unknown = await w.soap("getDeviceAuthToken", `<ns:householdId>${HOME_1}</ns:householdId><ns:linkCode>${"0".repeat(32)}</ns:linkCode>`);
  assert.match(unknown.text, /Client\.NOT_LINKED_FAILURE/);
  w.clock.now += 3_600_001;
  const expired = await w.soap("getDeviceAuthToken", `<ns:householdId>${HOME_1}</ns:householdId><ns:linkCode>${code}</ns:linkCode>`);
  assert.match(expired.text, /Client\.NOT_LINKED_FAILURE/);
  assert.equal((await w.api({ op: "confirm", code, language: "en" })).status, 404);
  assert.equal(rows(w.db, "sonos_homes").length, 0);
});

test("the confirm call checks its input: language, code and proof", async () => {
  const w = world();
  const code = await startLink(w);
  assert.equal((await w.api({ op: "confirm", code, language: "xx" })).status, 400);
  assert.equal((await w.api({ op: "confirm", code: "short", language: "fa" })).status, 400);
  assert.equal((await w.api({ op: "confirm", code, language: "fa", proof: "x y" })).status, 400);
  assert.equal((await w.api({ op: "nope" })).status, 400);
  assert.equal((await w.api({ op: "confirm", code: "f".repeat(32), language: "fa" })).status, 404);
  assert.equal((await w.api({ op: "confirm", code, language: "fa", proof: PROOF })).status, 200);
});

test("an unlinked request, a wrong token and a request with no database all fault; a linked one browses", async () => {
  const w = world();
  const { login } = await fullLink(w);
  const anonymous = await browse(w, null);
  assert.equal(anonymous.status, 500);
  assert.match(anonymous.text, /Client\.AuthTokenExpired/);
  const wrongToken = await browse(w, { ...login, token: "f".repeat(64) });
  assert.match(wrongToken.text, /Client\.AuthTokenExpired/);
  const otherHome = await browse(w, { ...login, householdId: HOME_2 });
  assert.match(otherHome.text, /Client\.AuthTokenExpired/);
  const ok = await browse(w, login);
  assert.equal(ok.status, 200);
  const noDb = await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body: envelope("getMetadata", "<ns:id>root</ns:id>", login) }), bucket, "", serveObject, undefined, undefined, { db: undefined });
  assert.equal(noDb.status, 500);
  assert.match(await noDb.text(), /Server\.ServiceUnavailable/);
});

test("the root of a linked home starts with the Story language row and shows the saved language's shelves in the phone's words", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "fa" });
  const english = (await browse(w, login, "root", { language: "en" })).text;
  assert.match(english, /<title>Story language: Farsi<\/title>/);
  assert.match(english, /<artist>Tap to change · saved for your home<\/artist>/);
  assert.ok(english.indexOf("Story language") < english.indexOf("Bedtime: 10 minutes or less"));
  assert.match(english, /<id>lang:fa-en\/bedtime<\/id>/);
  assert.match(english, /<id>pick:en<\/id>/);
  assert.ok(!/Other languages/.test(english), "no Other languages row");
  const farsi = (await browse(w, login, "root", { language: "fa-IR" })).text;
  assert.match(farsi, /<title>زبان قصه‌ها: فارسی<\/title>/);
  assert.match(farsi, /<artist>برای تغییر بزنید · برای خانه‌ی شما ذخیره می‌شود<\/artist>/);
  assert.match(farsi, /<id>lang:fa\/bedtime<\/id>/, "the same story and phone language needs no label in the id");
});

test("a shelf in the saved language lists its books with the phone-language title and no age part; the story-language-only shelf keeps its own words", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "fa" });
  const shelf = (await browse(w, login, "lang:fa-en/bedtime", { language: "en" })).text;
  assert.match(shelf, /<title>دخترک<\/title>/);
  assert.match(shelf, /<album>Goldilocks &amp; Bears · 9 min · Free<\/album>/);
  assert.ok(!/Ages 3/.test(shelf));
  const same = (await browse(w, login, "lang:fa/bedtime", { language: "fa" })).text;
  assert.match(same, /<album>[^<]*۹ دقیقه[^<]*<\/album>/);
  assert.ok(!/Goldilocks/.test(same), "no second title when the two languages are the same");
});

test("the picker lists the saved language first as current, then the rest with their story counts, in the phone's language", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en" });
  const pick = (await browse(w, login, "pick:en", { language: "en" })).text;
  const titles = [...pick.matchAll(/<title>([^<]*)<\/title>/g)].map((match) => match[1]);
  assert.deepEqual(titles, ["English", "فارسی"]);
  assert.match(pick, /<artist>English · current<\/artist>/);
  assert.match(pick, /<artist>Farsi · 1 story<\/artist>/);
  assert.match(pick, /<id>setlang:fa:en<\/id>/);
});

test("browsing setlang saves the language for the home and answers with the new root; the catalog token follows it", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en" });
  const update = (await w.soap("getLastUpdate", "", { login })).text;
  const saved = await browse(w, login, "setlang:fa:en");
  assert.equal(saved.status, 200);
  assert.match(saved.text, /<title>Story language: Farsi<\/title>/);
  assert.match(saved.text, /<id>lang:fa-en\/bedtime<\/id>/);
  assert.equal(rows(w.db, "sonos_homes")[0].story_language, "fa");
  assert.notEqual((await w.soap("getLastUpdate", "", { login })).text, update);
  assert.match((await browse(w, login, "root")).text, /Story language: Farsi/);
  assert.match((await browse(w, login, "setlang:zz:en")).text, /Client\.ItemNotFound/);
  assert.match((await browse(w, null, "setlang:fa:en")).text, /Client\.AuthTokenExpired/);
});

test("the Dino app's story language is what the home starts with, and a home with no books in it falls back to the phone's language", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "de" });
  const root = (await browse(w, login, "root", { language: "fa" })).text;
  assert.match(root, /<title>زبان قصه‌ها: فارسی<\/title>/);
});

test("one row per home: a second home linked with the same subscription replaces the first; another subscription keeps both", async () => {
  const w = world();
  const first = await fullLink(w, { household: HOME_1, proof: PROOF });
  const second = await fullLink(w, { household: HOME_2, proof: PROOF });
  const homes = rows(w.db, "sonos_homes");
  assert.equal(homes.length, 1);
  assert.equal((await browse(w, second.login)).status, 200);
  assert.match((await browse(w, first.login)).text, /Client\.AuthTokenExpired/);
  const third = await fullLink(w, { household: "Sonos_HOME_THREE", proof: "android:uk.sirous.dinoreader.sub.yearly:token-200002" });
  assert.equal(rows(w.db, "sonos_homes").length, 2);
  assert.equal((await browse(w, second.login)).status, 200);
  assert.equal((await browse(w, third.login)).status, 200);
  await fullLink(w, { household: HOME_2, language: "en", proof: PROOF });
  assert.equal(rows(w.db, "sonos_homes").length, 2, "the same home linking again keeps its one row");
});

test("refreshAuthToken gives new tokens for the right token and key and retires the old ones", async () => {
  const w = world();
  const { login } = await fullLink(w);
  const wrongKey = await w.soap("refreshAuthToken", "", { login: { ...login, key: "0".repeat(64) } });
  assert.match(wrongKey.text, /Client\.AuthTokenExpired/);
  const refreshed = await w.soap("refreshAuthToken", "", { login });
  assert.equal(refreshed.status, 200);
  const next = { ...login, token: between(refreshed.text, "authToken"), key: between(refreshed.text, "privateKey") };
  assert.notEqual(next.token, login.token);
  assert.match((await browse(w, login)).text, /Client\.AuthTokenExpired/);
  assert.equal((await browse(w, next)).status, 200);
  assert.match((await w.soap("refreshAuthToken", "", { login: null })).text, /Client\.AuthTokenExpired/);
});

test("unlinking before Sonos asks for the token also drops the confirmed pending link, so no token is issued with the revoked proof", async () => {
  const w = world();
  const code = await startLink(w);
  assert.equal((await w.api({ op: "confirm", code, language: "fa", proof: PROOF })).status, 200);
  assert.deepEqual((await w.api({ op: "unlink", proof: PROOF })).json, { removed: 0 });
  assert.equal(rows(w.db, "sonos_pending_links").length, 0);
  const { text } = await w.soap("getDeviceAuthToken", `<ns:householdId>${HOME_1}</ns:householdId><ns:linkCode>${code}</ns:linkCode>`);
  assert.match(text, /Client\.NOT_LINKED_FAILURE/);
  assert.equal(rows(w.db, "sonos_homes").length, 0);
});

test("unlink deletes the homes of one subscription proof and nothing else; the removed home's token faults", async () => {
  const w = world();
  const { login } = await fullLink(w, { household: HOME_1, proof: PROOF });
  const other = await fullLink(w, { household: HOME_2, proof: "android:uk.sirous.dinoreader.sub:token-300003" });
  assert.equal((await w.api({ op: "unlink", proof: "bad proof" })).status, 400);
  assert.deepEqual((await w.api({ op: "unlink", proof: PROOF })).json, { removed: 1 });
  assert.deepEqual((await w.api({ op: "unlink", proof: PROOF })).json, { removed: 0 });
  assert.match((await browse(w, login)).text, /Client\.AuthTokenExpired/);
  assert.equal((await browse(w, other.login)).status, 200);
});

test("a link started later deletes pending rows past their hour and homes idle for 400 days; a use is noted at most once a day", async () => {
  const w = world();
  const { login } = await fullLink(w);
  const day = 86_400_000;
  w.clock.now += day + 1;
  assert.equal((await browse(w, login)).status, 200);
  assert.equal(rows(w.db, "sonos_homes")[0].last_seen, w.clock.now);
  const seen = w.clock.now;
  w.clock.now += 1000;
  await browse(w, login);
  assert.equal(rows(w.db, "sonos_homes")[0].last_seen, seen, "not written again within a day");
  await startLink(w, HOME_2);
  w.clock.now += 3_700_000;
  await startLink(w, HOME_2);
  assert.equal(rows(w.db, "sonos_pending_links").length, 1);
  w.clock.now += 401 * day;
  await startLink(w, HOME_2);
  assert.equal(rows(w.db, "sonos_homes").length, 0);
});

test("a D1 failure is a service-unavailable fault, logged by error name only", async () => {
  const w = world();
  const lines = [];
  const original = console.warn;
  console.warn = (line) => lines.push(String(line));
  try {
    w.db.sqlite.exec("DROP TABLE sonos_homes");
    const { login } = { login: { token: "t".repeat(64), key: "k".repeat(64), householdId: HOME_1 } };
    const response = await browse(w, login);
    assert.match(response.text, /Server\.ServiceUnavailable/);
    const confirm = await w.api({ op: "unlink", proof: PROOF });
    assert.equal(confirm.status, 503);
  } finally {
    console.warn = original;
  }
  assert.ok(lines.length >= 2);
  for (const line of lines) assert.match(line, /^sonos link d1 error=\w+$/);
});

test("the request reader finds the login token in the header only, and the link parameters in the body", () => {
  const login = { token: "T1", key: "K1", householdId: "H1" };
  const parsed = parseSonosRequest(envelope("getDeviceAuthToken", "<ns:householdId>H2</ns:householdId><ns:linkCode>abc</ns:linkCode>", login));
  assert.deepEqual(parsed.login, login);
  assert.equal(parsed.householdId, "H2");
  assert.equal(parsed.linkCode, "abc");
  const forged = parseSonosRequest(envelope("getMetadata", "<ns:id>root</ns:id><loginToken><token>X</token><key>Y</key><householdId>Z</householdId></loginToken>"));
  assert.equal(forged.login, null);
});

test("the story-language words cover every label language, and every story language is one the labels know", () => {
  assert.deepEqual([...STORY_LANGUAGES].sort(), [...LABEL_LANGUAGES].sort());
  for (const language of STORY_LANGUAGES) {
    assert.ok(storyLanguageTitle(language, "X").includes("X"), language);
    assert.ok(storyLanguageTap(language).length > 5 && currentWord(language).length > 1, language);
  }
  assert.notEqual(storyLanguageTitle("fa", "X"), storyLanguageTitle("en", "X"));
});

test("without links the service stays the anonymous one: no token needed, link calls unsupported", async () => {
  const request = new Request("https://dino.test/sonos", { method: "POST", body: envelope("getMetadata", "<ns:id>root</ns:id>") });
  assert.equal((await handleSonos(request, bucket, "", serveObject)).status, 200);
  const link = await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body: envelope("getAppLink", `<ns:householdId>${HOME_1}</ns:householdId>`) }), bucket, "", serveObject);
  assert.match(await link.text(), /Unsupported call/);
});

// Slice 439: a linked home with an active Dino subscription plays the paid books; every other home hears the spoken "subscribe in the Dino app" message.
const ID_P = "7d0e5a4e000040008000000000000009";
const bookP = { schemaVersion: 1, title: "Big Paid", language: "en", version: 2, added: "2026-11-02", access: "paid", ageMin: 3, ageMax: 5, audio: { en: { version: 3, bytes: 5, listenSeconds: 420 }, fa: { version: 3, bytes: 5, listenSeconds: 420 } } };
const SUBSCRIBE_KEY = "dinoreader/sonos/subscribe-en.m4a";
const paidFiles = () => ({ DOWNLOADS: bucketOf([...bookFiles(ID_A, bookA, ["en", "fa"]), ...bookFiles(ID_P, bookP, ["en", "fa"]), [SUBSCRIBE_KEY, Buffer.from("spoken")]]) });
const ACTIVE = { kind: "status", status: { state: "active", expiresAt: "2027-01-01T00:00:00Z" } };
const status = (state) => ({ kind: "status", status: { state, expiresAt: null } });
const PAID_URI = `https://dino.test/api/stories/${ID_P}/listen/en?v=3`;
const SUBSCRIBE_URI = "https://dino.test/sonos/subscribe/en.m4a";

async function mediaUri(w, login, id = ID_P, files = paidFiles()) {
  return w.soap("getMediaURI", `<ns:id>track:en:${id}</ns:id>`, { login, files });
}

test("confirm keeps the subscription proof as sent and refuses a single-book or Windows proof with a clear reason (C1)", async () => {
  const w = world();
  const code = await startLink(w);
  const book = await w.api({ op: "confirm", code, language: "en", proof: "ios:uk.sirous.dinoreader.book.big_paid:txn-5555555" });
  assert.deepEqual([book.status, book.json], [400, { error: "not_subscription" }]);
  assert.equal((await w.api({ op: "confirm", code, language: "en", proof: "windows::keykeykey" })).json.error, "not_subscription");
  assert.equal((await w.api({ op: "confirm", code, language: "en", proof: PROOF })).status, 200);
  assert.equal(rows(w.db, "sonos_pending_links")[0].proof, PROOF);
  await w.soap("getDeviceAuthToken", `<ns:householdId>${HOME_1}</ns:householdId><ns:linkCode>${code}</ns:linkCode>`);
  assert.equal(rows(w.db, "sonos_homes")[0].proof, PROOF);
  assert.notEqual(rows(w.db, "sonos_homes")[0].proof_hash, null);
});

const IP = "203.0.113.7";
const SHELF_ALL = "<ns:id>lang:en/all</ns:id><ns:index>0</ns:index><ns:count>50</ns:count>";
const PAID_MEDIA = `/sonos/media/en/${ID_P}.m4a`;

test("confirm asks the store first: a lapsed, unknown or unverifiable proof is refused and never stored, and the limiter key is the connection's IP, not the home hash (review 1, finding 3)", async () => {
  const cases = [[status("expired"), 403, "subscription_inactive"], [{ kind: "bad-proof" }, 403, "subscription_inactive"], [{ kind: "rate-limited" }, 429, "rate_limited"], [{ kind: "cannot-verify" }, 503, "unavailable"]];
  for (const [answer, httpStatus, error] of cases) {
    const w = world();
    const code = await startLink(w);
    w.sub.answer = answer;
    const refused = await w.api({ op: "confirm", code, language: "en", proof: PROOF }, { "CF-Connecting-IP": IP });
    assert.deepEqual([refused.status, refused.json], [httpStatus, { error }], answer.kind);
    assert.equal(rows(w.db, "sonos_pending_links")[0].proof, null, "a refused proof is not stored");
    assert.deepEqual(w.sub.asked, [{ proof: PROOF, clientKey: IP }]);
  }
  const w = world();
  const code = await startLink(w);
  assert.equal((await w.api({ op: "confirm", code, language: "en", proof: PROOF })).status, 200);
  assert.equal(w.sub.asked.at(-1).clientKey, "unknown", "no IP header: the gate's own 'unknown' key, never a per-home key");
  assert.equal((await w.api({ op: "confirm", code: await startLink(w, HOME_2), language: "en" })).status, 200);
  assert.equal(w.sub.asked.length, 1, "a link without a proof asks the store nothing");
});

test("with the paid-books switch off, confirm links the home without storing the proof and renew answers 'paid_books_off' with a 200, never a 503 (review 2, finding 2)", async () => {
  const w = world();
  const code = await startLink(w);
  w.sub.answer = { kind: "off" };
  const confirmed = await w.api({ op: "confirm", code, language: "en", proof: PROOF });
  assert.equal(confirmed.status, 200);
  assert.deepEqual([rows(w.db, "sonos_pending_links")[0].proof, rows(w.db, "sonos_pending_links")[0].proof_hash], [null, null]);
  const linked = await fullLink(w, { language: "en", proof: PROOF });
  assert.equal(rows(w.db, "sonos_homes")[0].proof, null, "no proof is stored while the switch is off");
  w.sub.answer = ACTIVE;
  const second = await fullLink(w, { household: HOME_2, language: "en", proof: PROOF });
  assert.ok(second.login.token !== linked.login.token);
  w.sub.answer = { kind: "off" };
  const renewed = await w.api({ op: "renew", oldProof: PROOF, newProof: PROOF_RENEWED });
  assert.deepEqual([renewed.status, renewed.json.status], [200, "paid_books_off"]);
  assert.equal(rows(w.db, "sonos_homes").find((row) => row.proof !== null).proof, PROOF, "nothing was swapped");
});

test("an active subscription plays a paid book: no access word, canPlay, and getMediaURI hands the player the Worker's media URL with the home's own Sonos token, never the proof (C2, review 1 finding 2)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  const shown = await w.soap("getMetadata", SHELF_ALL, { login, files: paidFiles() });
  assert.match(shown.text, /<album>Ages 3–5 · 7 min<\/album>.*?<canPlay>true<\/canPlay>/s);
  const asked = w.sub.asked.length;
  const played = await mediaUri(w, login);
  assert.equal(played.status, 200);
  assert.match(played.text, new RegExp(`<getMediaURIResult>https://dino\\.test${PAID_MEDIA.replace("/", "/")}</getMediaURIResult>`));
  assert.match(played.text, new RegExp(`<httpHeaders><httpHeader><header>Authorization</header><value>Bearer ${login.token}</value></httpHeader></httpHeaders></getMediaURIResponse>`));
  assert.doesNotMatch(played.text, /txn-100001|X-Purchase-Proof|listen/, "the proof never leaves the Worker");
  assert.equal(w.sub.asked.length, asked, "getMediaURI does not call the store");
  const free = await mediaUri(w, login, ID_A);
  assert.doesNotMatch(free.text, /httpHeaders/, "a free book carries no header");
});

test("confirm says whether the proof was stored: true when kept, false with no proof, with the switch off, or on a database without the proof column (review 3, finding 1)", async () => {
  const w = world();
  assert.equal((await w.api({ op: "confirm", code: await startLink(w), language: "en", proof: PROOF })).json.proofStored, true);
  assert.equal((await w.api({ op: "confirm", code: await startLink(w, HOME_2), language: "en" })).json.proofStored, false);
  w.sub.answer = { kind: "off" };
  assert.equal((await w.api({ op: "confirm", code: await startLink(w), language: "en", proof: PROOF })).json.proofStored, false);
  const old = world();
  old.db.sqlite.exec("ALTER TABLE sonos_homes DROP COLUMN proof; ALTER TABLE sonos_pending_links DROP COLUMN proof");
  const original = console.warn;
  console.warn = () => {};
  try {
    assert.equal((await old.api({ op: "confirm", code: await startLink(old), language: "en", proof: PROOF })).json.proofStored, false);
  } finally {
    console.warn = original;
  }
});

test("with the paid-books switch off, a linked home's paid book plays like everyone else's, with or without a proof, never the subscribe clip (review 3, finding 2)", async () => {
  const w = world();
  const withProof = await fullLink(w, { household: HOME_1, language: "en", proof: PROOF });
  const without = await fullLink(w, { household: HOME_2, language: "en" });
  assert.equal((await w.get(PAID_MEDIA, { token: without.login.token, files: paidFiles() })).text, "spoken", "switch on, no subscription: the message");
  w.sub.answer = { kind: "off" };
  for (const { login } of [withProof, without]) {
    assert.deepEqual((({ status, text }) => [status, text])(await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() })), [200, "m4a"]);
  }
});

test("a Range or HEAD request is answered from the kept answer, so the store-call limit cannot cut a book mid-play; only the first request asks the store (review 3, finding 5)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() })).text, "m4a");
  const asked = w.sub.asked.length;
  w.sub.answer = { kind: "rate-limited" };
  const ranged = await w.get(PAID_MEDIA, { token: login.token, files: paidFiles(), headers: { Range: "bytes=1-2" } });
  assert.deepEqual([ranged.status, ranged.text], [200, "m4a"], "the file is served (the test's object server ignores Range), not a 503");
  assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles(), method: "HEAD" })).status, 200);
  assert.equal(w.sub.asked.length, asked, "neither continuation asked the store");
  w.clock.now += 11 * 60_000;
  assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles(), headers: { Range: "bytes=1-2" } })).status, 503, "past the kept answer's time the store is asked again");
});

test("a browse keys the store-call limiter on the linked home, whatever the caller's IP (review 3, finding 1)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  w.sub.asked.length = 0;
  assert.equal((await browse(w, login, "root", { headers: { "CF-Connecting-IP": IP } })).status, 200);
  const keys = w.sub.asked.map((ask) => ask.clientKey);
  assert.ok(keys.length > 0 && keys.every((key) => key === `home:${rows(w.db, "sonos_homes")[0].home_hash}`), JSON.stringify(keys));
});

test("the media route streams the paid book for an active or grace subscription and asks the store with the stored proof at play time (C2)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  const file = await w.get(PAID_MEDIA, { token: login.token, files: paidFiles(), headers: { "CF-Connecting-IP": IP } });
  assert.deepEqual([file.status, file.text], [200, "m4a"]);
  assert.equal(file.headers.get("Cache-Control"), "private, no-store");
  assert.deepEqual(w.sub.asked.at(-1), { proof: PROOF, clientKey: `home:${rows(w.db, "sonos_homes")[0].home_hash}` }, "a Sonos call keys the limiter on the linked home, not the shared Sonos IP (review 3, finding 1)");
  w.sub.answer = status("grace");
  assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() })).text, "m4a", "a grace period still plays");
  assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles(), method: "HEAD" })).status, 200);
});

test("the media route refuses a missing or wrong token, a book that is not paid and a POST (review 1, finding 2)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  const asked = w.sub.asked.length;
  assert.equal((await w.get(PAID_MEDIA, { files: paidFiles() })).status, 401);
  assert.equal((await w.get(PAID_MEDIA, { token: "f".repeat(64), files: paidFiles() })).status, 401);
  assert.equal((await w.get(PAID_MEDIA, { token: PROOF, files: paidFiles() })).status, 401, "the store proof is not a credential");
  assert.equal((await w.get(`/sonos/media/en/${ID_A}.m4a`, { token: login.token, files: paidFiles() })).status, 404, "a free book is not served here");
  assert.equal((await w.get("/sonos/media/en/nope.m4a", { token: login.token, files: paidFiles() })).status, 404);
  assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles(), method: "POST" })).status, 405);
  assert.equal(w.sub.asked.length, asked, "none of these reached the store");
  assert.ok(isSonosPath(PAID_MEDIA) && !isSonosPath("/sonos/media/en/x.mp3") && !isSonosPath("/sonos/media/../x.m4a"));
});

test("a lapsed, unknown or missing subscription plays the spoken subscribe message from the media route, and the shelf lists the book playable with 'In the Dino app' (C2, C5, review 1 finding 9)", async () => {
  for (const answer of [status("expired"), status("revoked"), { kind: "bad-proof" }]) {
    const w = world();
    const { login } = await fullLink(w, { language: "en", proof: PROOF });
    w.sub.answer = answer;
    const played = await mediaUri(w, login);
    assert.match(played.text, /<getMediaURIResult>https:\/\/dino\.test\/sonos\/media\/en\//, "the same media URL: the route decides");
    const message = await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() });
    assert.deepEqual([message.status, message.text], [200, "spoken"], answer.kind);
    const shown = await w.soap("getMetadata", SHELF_ALL, { login, files: paidFiles() });
    assert.match(shown.text, /<album>Ages 3–5 · 7 min · In the Dino app<\/album>.*?<canPlay>true<\/canPlay>/s, "playable, so the message is reachable from browse");
  }
});

test("an anonymous browse still lists a paid book unplayable and its getMediaURI faults (Slice 437 A1)", async () => {
  const request = new Request("https://dino.test/sonos", { method: "POST", body: envelope("getMetadata", SHELF_ALL) });
  const shown = await (await handleSonos(request, paidFiles(), "", serveObject)).text();
  assert.match(shown, /In the Dino app<\/album>.*?<canPlay>false<\/canPlay>/s);
});

test("a home linked with no subscription never asks the store about a proof and hears the message; its free books still play (C2, C5)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en" });
  const message = await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() });
  assert.equal(message.text, "spoken");
  assert.ok(w.sub.asked.every((asked) => asked.proof === ""), "only the paid-books switch is read, with no proof");
  assert.match((await mediaUri(w, login, ID_A)).text, /<getMediaURIResult>https:\/\/dino\.test\/api\/stories\/[0-9a-f]+\/listen\/en\?v=3<\/getMediaURIResult>/);
});

test("the message is in the book's language and the subscription is checked again at every play (C2, C3)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  const files = { DOWNLOADS: bucketOf([...bookFiles(ID_P, bookP, ["en", "fa"]), ["dinoreader/sonos/subscribe-fa.m4a", Buffer.from("parlé")], [SUBSCRIBE_KEY, Buffer.from("spoken")]]) };
  assert.equal((await w.get(`/sonos/media/fa/${ID_P}.m4a`, { token: login.token, files })).text, "m4a");
  w.sub.answer = status("expired");
  assert.equal((await w.get(`/sonos/media/fa/${ID_P}.m4a`, { token: login.token, files })).text, "parlé");
  assert.equal(w.sub.asked.length, 3, "each play asks the subscription source again (the link's own check is the first)");
});

test("a subscribe clip missing from R2 plays the English one, and with that missing too the media route answers 503, never 404, and logs the miss (review 3, finding 6)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  w.sub.answer = status("expired");
  const warnings = [];
  const realWarn = console.warn;
  console.warn = (line) => warnings.push(String(line));
  try {
    const noFa = { DOWNLOADS: bucketOf([...bookFiles(ID_P, bookP, ["en", "fa"]), [SUBSCRIBE_KEY, Buffer.from("spoken")]]) };
    assert.equal((await w.get(`/sonos/media/fa/${ID_P}.m4a`, { token: login.token, files: noFa })).text, "spoken", "no Farsi clip yet: the English one plays");
    assert.ok(warnings.some((line) => line.includes("language=fa")), "the miss is logged");
    const none = { DOWNLOADS: bucketOf([...bookFiles(ID_P, bookP, ["en", "fa"])]) };
    for (const path of [PAID_MEDIA, `/sonos/media/fa/${ID_P}.m4a`]) {
      assert.equal((await w.get(path, { token: login.token, files: none })).status, 503, `${path}: no clip at all is a 503`);
    }
    assert.ok(warnings.some((line) => line.includes("language=en")));
  } finally {
    console.warn = realWarn;
  }
});

test("a store error or rate limit keeps the home's last real answer for ten minutes, then the message plays (review 1, finding 10)", async () => {
  for (const failing of [{ kind: "cannot-verify" }, { kind: "rate-limited" }, { kind: "off" }]) {
    const w = world();
    const { login } = await fullLink(w, { language: "en", proof: PROOF });
    assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() })).text, "m4a");
    w.sub.answer = failing;
    assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() })).text, "m4a", `${failing.kind}: a paying home keeps playing`);
    assert.match((await w.soap("getMetadata", SHELF_ALL, { login, files: paidFiles() })).text, /<album>Ages 3–5 · 7 min<\/album>/, "and keeps its shelf");
    w.clock.now += 11 * 60_000;
    const after = await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() });
    if (failing.kind === "off") assert.equal(after.text, "m4a", "the switch is off: the paid books are free for everyone, as on the app's own route");
    else assert.equal(after.status, 503, "the kept answer ends after its time; the store is unknown, so the player retries");
  }
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  for (const failing of [{ kind: "cannot-verify" }, { kind: "rate-limited" }]) {
    w.sub.answer = failing;
    const unknown = await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() });
    assert.equal(unknown.status, 503, `${failing.kind}, no earlier answer: the player retries, never the subscribe clip (review 2, finding 5)`);
    assert.notEqual(unknown.text, "spoken");
  }
  w.sub.answer = { kind: "off" };
  assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() })).text, "m4a", "switch off, no earlier answer: the paid books play (review 3, finding 2)");
  w.sub.answer = ACTIVE;
  assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() })).text, "m4a");
  w.sub.answer = status("expired");
  assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() })).text, "spoken", "a real lapsed answer replaces the kept one at once");
});

test("the catalog token follows the last real answer; getLastUpdate itself never calls the store (C2, review 1 finding 10)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  const token = async () => between((await w.soap("getLastUpdate", "", { login, files: paidFiles() })).text, "catalog");
  const browseOnce = () => w.soap("getMetadata", SHELF_ALL, { login, files: paidFiles() });
  const before = w.sub.asked.length;
  const unknown = await token();
  assert.equal(await token(), unknown);
  assert.equal(w.sub.asked.length, before, "polling never reaches the store");
  await browseOnce();
  const active = await token();
  assert.notEqual(unknown, active, "a browse that saw the active subscription changes the token");
  w.sub.answer = status("expired");
  await browseOnce();
  assert.equal(await token(), unknown, "and a lapsed answer changes it back");
});

test("a subscribed home's paid books stay off its Free stories shelf (review 1, finding 11)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  const free = (text) => text.includes("Big Paid");
  const onShelf = async (shelf) => (await w.soap("getMetadata", `<ns:id>lang:en/${shelf}</ns:id><ns:index>0</ns:index><ns:count>50</ns:count>`, { login, files: paidFiles() })).text;
  assert.ok(free(await onShelf("all")), "the paid book is on A-Z");
  assert.ok(!free(await onShelf("free")), "but not on Free");
  assert.match(await onShelf("free"), /Goldilocks/);
  w.sub.answer = status("expired");
  assert.ok(!free(await onShelf("free")), "an unsubscribed home never had it there either");
});

test("a database where the proof column was not added yet still reads the home: it has no proof, its paid books play the message, nothing faults (review 1, finding 4)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  w.db.sqlite.exec("ALTER TABLE sonos_homes DROP COLUMN proof");
  const warned = [];
  const original = console.warn;
  console.warn = (line) => warned.push(String(line));
  try {
    assert.equal((await browse(w, login)).status, 200);
    assert.equal((await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() })).text, "spoken");
  } finally {
    console.warn = original;
  }
  assert.ok(warned.some((line) => line.includes("fallback=no-proof-column")), "reported, by name only");
  assert.ok(warned.every((line) => !line.includes(PROOF) && !line.includes(HOME_1)));
});

test("renew swaps the stored proof of the home that held the old one, once the store confirms the new one; the new proof decides from then on (C4)", async () => {
  const w = world();
  const { login } = await fullLink(w, { language: "en", proof: PROOF });
  const renewed = await w.api({ op: "renew", oldProof: PROOF, newProof: PROOF_RENEWED }, { "CF-Connecting-IP": IP });
  assert.deepEqual([renewed.status, renewed.json], [200, { renewed: 1 }]);
  assert.deepEqual(w.sub.asked.at(-1), { proof: PROOF_RENEWED, clientKey: IP }, "the store was asked about the new proof");
  const [row] = rows(w.db, "sonos_homes");
  assert.equal(row.proof, PROOF_RENEWED);
  await w.get(PAID_MEDIA, { token: login.token, files: paidFiles() });
  assert.equal(w.sub.asked.at(-1).proof, PROOF_RENEWED);
  assert.deepEqual((await w.api({ op: "unlink", proof: PROOF_RENEWED })).json, { removed: 1 }, "unlink finds the home by the new proof");
});

test("renew changes nothing unless the store confirms the new proof as active: a made-up, lapsed or unverifiable one is refused (review 1, finding 1)", async () => {
  for (const [answer, httpStatus] of [[status("expired"), 403], [{ kind: "bad-proof" }, 403], [{ kind: "rate-limited" }, 429], [{ kind: "cannot-verify" }, 503]]) {
    const w = world();
    await fullLink(w, { language: "en", proof: PROOF });
    w.sub.answer = answer;
    const refused = await w.api({ op: "renew", oldProof: PROOF, newProof: "ios:uk.sirous.dinoreader.sub.monthly:junk-junk" });
    assert.equal(refused.status, httpStatus, answer.kind);
    assert.equal(rows(w.db, "sonos_homes")[0].proof, PROOF, "the stored proof is untouched");
  }
});

test("renew refuses an unknown old proof without asking the store, a non-subscription proof and a malformed body; the same proof twice is harmless (C4)", async () => {
  const w = world();
  await fullLink(w, { language: "en", proof: PROOF });
  const asked = w.sub.asked.length;
  assert.deepEqual(await w.api({ op: "renew", oldProof: "ios:uk.sirous.dinoreader.sub.yearly:nobody-has-this", newProof: PROOF_RENEWED }), { status: 404, json: { error: "unknown_home" } });
  assert.equal(w.sub.asked.length, asked, "no home holds the old proof, so no store call is spent");
  assert.equal(rows(w.db, "sonos_homes")[0].proof, PROOF);
  assert.equal((await w.api({ op: "renew", oldProof: PROOF, newProof: "ios:uk.sirous.dinoreader.book.big_paid:txn-5555555" })).json.error, "not_subscription");
  assert.equal((await w.api({ op: "renew", oldProof: PROOF })).status, 400);
  assert.deepEqual((await w.api({ op: "renew", oldProof: PROOF, newProof: PROOF })).json, { renewed: 1 });
  assert.equal(rows(w.db, "sonos_homes")[0].proof, PROOF);
});

test("renew never deletes another home: a new proof another home already holds is refused and both homes stay (review 1, finding 1)", async () => {
  const w = world();
  await fullLink(w, { household: HOME_1, proof: PROOF });
  await fullLink(w, { household: HOME_2, proof: PROOF_RENEWED });
  const refused = await w.api({ op: "renew", oldProof: PROOF, newProof: PROOF_RENEWED });
  assert.deepEqual([refused.status, refused.json], [409, { error: "proof_taken" }]);
  const left = rows(w.db, "sonos_homes");
  assert.equal(left.length, 2);
  assert.deepEqual(left.map((row) => row.proof).sort(), [PROOF, PROOF_RENEWED].sort());
});

test("the spoken message is served from R2 at /sonos/subscribe/<language>.m4a and nowhere else (C3)", async () => {
  const get = (path, method = "GET") => handleSonos(new Request(`https://dino.test${path}`, { method }), paidFiles(), "", serveObject, undefined, undefined, undefined);
  const spoken = await get("/sonos/subscribe/en.m4a");
  assert.equal(spoken.status, 200);
  assert.equal(await spoken.text(), "spoken");
  assert.equal(spoken.headers.get("Content-Type"), "audio/mp4");
  assert.equal((await get("/sonos/subscribe/de.m4a")).status, 404, "a language not rendered yet is not found");
  assert.equal((await get("/sonos/subscribe/en.m4a", "POST")).status, 405);
  assert.ok(isSonosPath("/sonos/subscribe/en.m4a") && !isSonosPath("/sonos/subscribe/en.mp3") && !isSonosPath("/sonos/subscribe/xx.m4a") && !isSonosPath("/sonos/subscribe/../x.m4a"));
});

test("the subscribe message has words in exactly the 15 story languages, each short and naming the Dino app (C3)", () => {
  assert.deepEqual(Object.keys(SUBSCRIBE_MESSAGES).sort(), [...STORY_LANGUAGES].sort());
  for (const [language, words] of Object.entries(SUBSCRIBE_MESSAGES)) {
    assert.ok(words.length > 20 && words.length <= 140, `${language} is a sentence or two`);
    assert.match(words, /Dino|دینو|دينو/, `${language} names the app`);
  }
});

// The link page's store choice (B4): the script block between the storeFor markers is run as it is shipped.
const page = readFileSync(new URL("../site/dino/sonos-link.html", import.meta.url), "utf8");
const storeFor = new Function(`${page.split("// storeFor:start")[1].split("// storeFor:end")[0]}; return storeFor;`)();
const PLAY = "https://play.google.com/store/apps/details?id=uk.sirous.dinoreader";

test("the link page sends a phone without the app to the store of its system", () => {
  const android = "Mozilla/5.0 (Linux; Android 14; SM-G991B) AppleWebKit/537.36 Chrome/120 Mobile Safari/537.36";
  const iphone = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148 Safari/604.1";
  const ipadAsMac = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 Version/17.0 Safari/605.1.15";
  const windows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36";
  assert.deepEqual(storeFor(android, 5), { system: "android", url: PLAY });
  assert.equal(storeFor(iphone, 5).url, "https://apps.apple.com/app/id6825053435");
  assert.equal(storeFor(ipadAsMac, 5).system, "ios");
  assert.equal(storeFor(ipadAsMac, 0).system, "other");
  assert.equal(storeFor(windows, 0).url, "https://apps.microsoft.com/detail/9N9VHRLCZXN5");
  assert.deepEqual(storeFor("Mozilla/5.0 (X11; Linux x86_64) Firefox/120", 0), { system: "other", url: "https://dinoreader.sirous.uk/" });
  assert.equal(storeFor("", 0).system, "other");
  const home = readFileSync(new URL("../site/dino/index.html", import.meta.url), "utf8");
  for (const url of [PLAY, "https://apps.apple.com/app/id6825053435", "https://apps.microsoft.com/detail/9N9VHRLCZXN5"]) assert.ok(home.includes(url), `${url} is on the Dino home page`);
});
