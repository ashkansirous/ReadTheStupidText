// Run: node --test worker/sonos-link.test.mjs (Slice 438: every Sonos home links through the Dino app and saves its story language).
// The D1 database is a real SQLite (node:sqlite) running worker/schema/sonos-homes.sql, so the SQL the Worker sends is the SQL that is tested.
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";

import { serveObject } from "./downloads.ts";
import { handleSonos } from "./sonos.ts";
import { STORY_LANGUAGES, callbackWithCode, currentWord, isSonosCallback, storyLanguageTap, storyLanguageTitle } from "./sonos-link.ts";
import { LABEL_LANGUAGES } from "./sonos-labels.ts";
import { parseSonosRequest } from "./sonos-soap.ts";

const NS = "http://www.sonos.com/Services/1.1";
const ID_A = "7d0e5a4e000040008000000000000001";
const ID_C = "7d0e5a4e000040008000000000000003";
const HOME_1 = "Sonos_HOME_ONE_abc123";
const HOME_2 = "Sonos_HOME_TWO_def456";
const CALLBACK = "sonos://sonos.com/applink?state=STATE42";
const PROOF = "proof-key-of-one-subscription";
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
  const links = { db, now: () => clock.now };
  const soap = async (action, inner = "", { login = null, language = "en", host = "https://dino.test" } = {}) => {
    const request = new Request(`${host}/sonos`, { method: "POST", body: envelope(action, inner, login), headers: { "Accept-Language": language } });
    const response = await handleSonos(request, bucket, "", serveObject, undefined, undefined, links);
    return { status: response.status, text: await response.text() };
  };
  const api = async (body) => {
    const response = await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body: JSON.stringify(body), headers: { "Content-Type": "application/json" } }), bucket, "", serveObject, undefined, undefined, links);
    return { status: response.status, json: await response.json() };
  };
  return { db, clock, links, soap, api };
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

test("a relink without a proof keeps the stored proof, so unlink still finds the home", async () => {
  const w = world();
  await fullLink(w, { language: "fa", proof: PROOF });
  const proofBefore = rows(w.db, "sonos_homes")[0].proof_hash;
  assert.notEqual(proofBefore, null);
  await fullLink(w, { language: "en" });
  assert.equal(rows(w.db, "sonos_homes")[0].proof_hash, proofBefore);
  assert.equal((await w.api({ op: "unlink", proof: PROOF })).json.removed, 1);
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
  const third = await fullLink(w, { household: "Sonos_HOME_THREE", proof: "another-subscription-proof" });
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
  const other = await fullLink(w, { household: HOME_2, proof: "a-different-subscription" });
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
