// Run: node --test worker/sonos-search.test.mjs (Slice 440: search on Sonos, both categories over the real handler and the pure matcher).
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";

import { serveObject } from "./downloads.ts";
import { handleSonos } from "./sonos.ts";
import { LABEL_LANGUAGES } from "./sonos-labels.ts";
import { SEARCH_LANGUAGES, SearchCategory, cleanTerm, foldText, matchesTerm, searchCategories, searchTracks } from "./sonos-search.ts";

const NS = "http://www.sonos.com/Services/1.1";
const ID_A = "7d0e5a4e000040008000000000000001";
const ID_B = "7d0e5a4e000040008000000000000002";

function files(id, meta, langs) {
  const folder = `dinoreader/stories/${id}/`;
  const out = [[`${folder}meta.json`, Buffer.from(JSON.stringify(meta))], [`${folder}story-v2.dino`, Buffer.from("pkg")]];
  for (const lang of langs) out.push([`${folder}audio-${lang}-v3.zip`, Buffer.from("zip")], [`${folder}listen-${lang}-v3.m4a`, Buffer.from("m4a")]);
  return out;
}

function bucketOf(list) {
  const objects = new Map(list);
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

const goats = { schemaVersion: 1, title: "The Three Billy Goats Gruff", language: "en", version: 2, added: "2026-11-01", titles: { fa: "سه بز کوهی", de: "Die drei Ziegenböcke" }, audio: { en: { version: 3, bytes: 5, listenSeconds: 360 }, fa: { version: 3, bytes: 5, listenSeconds: 340 }, de: { version: 3, bytes: 5, listenSeconds: 350 } } };
const bears = { schemaVersion: 1, title: "Café Bears", language: "en", version: 2, added: "2026-11-02", audio: { en: { version: 3, bytes: 5, listenSeconds: 200 } } };
const env = () => ({ DOWNLOADS: bucketOf([...files(ID_A, goats, ["en", "fa", "de"]), ...files(ID_B, bears, ["en"])]) });

async function search(id, term, { extra = "", language = "en" } = {}) {
  const body = `<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><ns:search xmlns:ns="${NS}"><ns:id>${id}</ns:id><ns:term>${term}</ns:term>${extra}</ns:search></s:Body></s:Envelope>`;
  const response = await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body, headers: { "Accept-Language": language } }), env(), "", serveObject);
  return { response, text: await response.text() };
}

const titles = (text) => [...text.matchAll(/<mediaMetadata><id>[^<]*<\/id><itemType>track<\/itemType><title>([^<]*)<\/title>/g)].map((m) => m[1]);

test("the matcher ignores case, accents and Persian letter variants", () => {
  assert.equal(matchesTerm("Café Bears", "cafe"), true);
  assert.equal(matchesTerm("Die drei Ziegenböcke", "ZIEGENBOCKE"), true);
  assert.equal(matchesTerm("كتاب", "کتاب"), true);
  assert.equal(matchesTerm("سه بز کوهی", "بز"), true);
  assert.equal(matchesTerm("Goats", "wolf"), false);
  assert.equal(matchesTerm("Goats", "   "), false);
  assert.equal(foldText("  É  a "), "E a");
  assert.equal(matchesTerm("がっこう", "か"), false);
  assert.equal(matchesTerm("はな", "ぱ"), false);
  assert.equal(matchesTerm("ばなな", "ば"), true);
  assert.equal(matchesTerm("ぱん", "は"), false);
  assert.equal(cleanTerm(null), "");
});

test("letters that fold to a different length still match (review finding 1)", () => {
  assert.equal(matchesTerm("Straße", "strasse"), true);
  assert.equal(matchesTerm("Strasse", "straße"), true);
  assert.equal(matchesTerm("Æsop", "aesop"), true);
  assert.equal(matchesTerm("Aesop", "æsop"), true);
  assert.equal(matchesTerm("Cœur", "coeur"), true);
  assert.equal(matchesTerm("Zie­gen", "ziegen"), true);
  assert.equal(matchesTerm("Zie​gen", "ziegen"), true);
});

test("letters with no decomposition and the Arabic tatweel fold (check finding 1, 2, 3)", () => {
  assert.equal(matchesTerm("Bjørn", "bjorn"), true);
  assert.equal(matchesTerm("Bjorn", "BJØRN"), true);
  assert.equal(matchesTerm("Łódź", "lodz"), true);
  assert.equal(matchesTerm("كتـاب", "كتاب"), true);
  assert.equal(matchesTerm("كتاب", "كتـاب"), true);
  assert.equal(matchesTerm("がっこう", "か"), false);
});

test("a long term is cut by whole characters and trimmed (review finding 6)", () => {
  const long = `${"a".repeat(63)}😀`;
  assert.equal(Array.from(cleanTerm(long)).length, 64);
  assert.equal(cleanTerm(long).endsWith("😀"), true);
  assert.equal(cleanTerm(`${"a".repeat(63)} b`), "a".repeat(63));
  assert.equal(Array.from(cleanTerm("😀".repeat(100))).length, 64);
  assert.equal(cleanTerm("x".repeat(100000)).length, 64);
});

test("the categories are named in every label language and match the strings file", () => {
  assert.deepEqual([...SEARCH_LANGUAGES], [...LABEL_LANGUAGES]);
  assert.deepEqual(searchCategories("de").map((c) => c.id), ["stories", "all-languages"]);
  assert.equal(searchCategories("xx")[0].name, "Stories");
  const strings = readFileSync(new URL("../site/dino/sonos/strings.xml", import.meta.url), "utf8");
  // Sonos accepts only these locales (docs.sonos.com/docs/localization); ar, fa, fi and ru are named by the Worker only.
  const sonosLocales = { da: "da-DK", de: "de-DE", en: "en-US", es: "es-ES", fr: "fr-FR", ja: "ja-JP", nb: "nb-NO", nl: "nl-NL", pt: "pt-BR", sv: "sv-SE", zh: "zh-CN" };
  const desktopOnly = ["ar", "fa", "fi", "ru"];
  for (const code of SEARCH_LANGUAGES) assert.ok(code in sonosLocales || desktopOnly.includes(code), `${code} is in strings.xml or listed as desktop-only`);
  const stringIds = { stories: "Stories", "all-languages": "AllLanguages" };
  for (const [code, locale] of Object.entries(sonosLocales)) {
    const from = strings.indexOf(`xml:lang="${locale}"`);
    const until = strings.indexOf("</stringtable>", from);
    assert.ok(from >= 0 && until > from, `${locale} table`);
    const table = strings.slice(from, until);
    for (const category of searchCategories(code)) assert.ok(table.includes(`<string stringId="${stringIds[category.id]}">${category.name}</string>`), `${code} ${category.id}`);
  }
  const map = readFileSync(new URL("../site/dino/sonos/presentation-map.xml", import.meta.url), "utf8");
  assert.match(map, /mappedId="stories"/);
  assert.match(map, /mappedId="all-languages"/);
});

test("Stories finds the current language's titles only", async () => {
  const { response, text } = await search(SearchCategory.Stories, "goat");
  assert.equal(response.status, 200);
  assert.deepEqual(titles(text), ["The Three Billy Goats Gruff"]);
  assert.match(text, /<searchResponse[^>]*><searchResult><index>0<\/index><count>1<\/count><total>1<\/total>/);
  assert.doesNotMatch(text, /Ages|English ·/);
});

test("All languages finds every language's book, the language's name in the subtitle", async () => {
  const english = await search(SearchCategory.AllLanguages, "goat");
  assert.deepEqual(titles(english.text), ["The Three Billy Goats Gruff"]);
  assert.match(english.text, /<album>English · /);
  const german = await search(SearchCategory.AllLanguages, "ziegenbock");
  assert.deepEqual(titles(german.text), ["Die drei Ziegenböcke"]);
  assert.match(german.text, /<album>German · /);
  const both = await search(SearchCategory.AllLanguages, "e", { extra: "<ns:count>2</ns:count>" });
  assert.match(both.text, /<count>2<\/count>/);
});

test("a Farsi term finds the Farsi book, and Stories in Farsi finds it too", async () => {
  const fa = await search(SearchCategory.AllLanguages, "بز");
  assert.deepEqual(titles(fa.text), ["سه بز کوهی"]);
  const stories = await search(SearchCategory.Stories, "بز", { language: "fa" });
  assert.deepEqual(titles(stories.text), ["سه بز کوهی"]);
});

test("no results is an empty page, an empty term too, an unknown category is a fault", async () => {
  const none = await search(SearchCategory.Stories, "zzzz");
  assert.equal(none.response.status, 200);
  assert.match(none.text, /<count>0<\/count><total>0<\/total>/);
  assert.match((await search(SearchCategory.Stories, "")).text, /<total>0<\/total>/);
  const unknown = await search("nope", "goat");
  assert.equal(unknown.response.status, 500);
  assert.match(unknown.text, /Client\.ItemNotFound/);
});

test("paging gives index, count and the total", async () => {
  const first = await search(SearchCategory.AllLanguages, "e", { extra: "<ns:index>0</ns:index><ns:count>1</ns:count>" });
  assert.match(first.text, /<index>0<\/index><count>1<\/count><total>3<\/total>/);
  const last = await search(SearchCategory.AllLanguages, "e", { extra: "<ns:index>2</ns:index><ns:count>5</ns:count>" });
  assert.match(last.text, /<index>2<\/index><count>1<\/count><total>3<\/total>/);
  const past = await search(SearchCategory.AllLanguages, "e", { extra: "<ns:index>9</ns:index><ns:count>5</ns:count>" });
  assert.match(past.text, /<count>0<\/count><total>3<\/total>/);
});

test("the search container lists the two categories as search items, in the phone's words", async () => {
  const body = `<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><ns:getMetadata xmlns:ns="${NS}"><ns:id>search</ns:id><ns:index>0</ns:index><ns:count>100</ns:count></ns:getMetadata></s:Body></s:Envelope>`;
  const response = await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body, headers: { "Accept-Language": "de" } }), env(), "", serveObject);
  const text = await response.text();
  assert.match(text, /<total>2<\/total>/);
  assert.match(text, /<id>stories<\/id><title>Geschichten<\/title><itemType>search<\/itemType>/);
  assert.match(text, /<id>all-languages<\/id><title>Alle Sprachen<\/title><itemType>search<\/itemType>/);
});

test("with no Accept-Language header search answers in English and Stories looks in the catalogue's first language (review finding 2)", async () => {
  const soap = (inner) => `<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body>${inner}</s:Body></s:Envelope>`;
  const send = async (inner) => (await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body: soap(inner) }), env(), "", serveObject)).text();
  const container = await send(`<ns:getMetadata xmlns:ns="${NS}"><ns:id>search</ns:id><ns:index>0</ns:index><ns:count>100</ns:count></ns:getMetadata>`);
  assert.match(container, /<id>stories<\/id><title>Stories<\/title>/);
  assert.match(container, /<id>all-languages<\/id><title>All languages<\/title>/);
  const found = await send(`<ns:search xmlns:ns="${NS}"><ns:id>stories</ns:id><ns:term>ziegenbock</ns:term></ns:search>`);
  assert.deepEqual(titles(found), ["Die drei Ziegenböcke"]);
});

// A D1Database over node:sqlite, as sonos-link.test.mjs builds it (only what the link flow calls).
function fakeD1() {
  const sqlite = new DatabaseSync(":memory:");
  sqlite.exec(readFileSync(new URL("./schema/sonos-homes.sql", import.meta.url), "utf8"));
  const statement = (sql, args = []) => ({
    bind: (...values) => statement(sql, values),
    first: async () => sqlite.prepare(sql).get(...args) ?? null,
    all: async () => ({ results: sqlite.prepare(sql).all(...args) }),
    run: async () => ({ meta: { changes: Number(sqlite.prepare(sql).run(...args).changes) } }),
  });
  return {
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

test("a linked home's Stories search looks in its saved language, worded in the phone's, and a paid book shows 'In the Dino app' (review finding 3)", async () => {
  const paidBook = { schemaVersion: 1, title: "Paid Tale", language: "en", version: 2, added: "2026-11-03", access: "paid", titles: { fa: "قصه پولی" }, audio: { en: { version: 3, bytes: 5, listenSeconds: 420 }, fa: { version: 3, bytes: 5, listenSeconds: 420 } } };
  const ID_P = "7d0e5a4e000040008000000000000009";
  const store = { DOWNLOADS: bucketOf([...files(ID_A, goats, ["en", "fa", "de"]), ...files(ID_P, paidBook, ["en", "fa"])]) };
  const links = { db: fakeD1(), now: () => 1_800_000_000_000 };
  const send = async (inner, login = null, language = "en") => {
    const header = login === null ? "" : `<s:Header><credentials xmlns="${NS}"><loginToken><token>${login.token}</token><key>${login.key}</key><householdId>HOME_ONE</householdId></loginToken><deviceId>XX</deviceId></credentials></s:Header>`;
    const body = `<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">${header}<s:Body>${inner}</s:Body></s:Envelope>`;
    return (await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body, headers: { "Accept-Language": language } }), store, "", serveObject, undefined, undefined, links)).text();
  };
  const linkText = await send(`<ns:getAppLink xmlns:ns="${NS}"><ns:householdId>HOME_ONE</ns:householdId><ns:callbackPath>sonos://sonos.com/applink?state=S</ns:callbackPath></ns:getAppLink>`);
  const code = /<linkCode>([^<]*)<\/linkCode>/.exec(linkText)[1];
  const confirmed = await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body: JSON.stringify({ op: "confirm", code, language: "fa" }), headers: { "Content-Type": "application/json" } }), store, "", serveObject, undefined, undefined, links);
  assert.equal(confirmed.status, 200);
  const tokens = await send(`<ns:getDeviceAuthToken xmlns:ns="${NS}"><ns:householdId>HOME_ONE</ns:householdId><ns:linkCode>${code}</ns:linkCode></ns:getDeviceAuthToken>`);
  const login = { token: /<authToken>([^<]*)<\/authToken>/.exec(tokens)[1], key: /<privateKey>([^<]*)<\/privateKey>/.exec(tokens)[1] };
  const stories = await send(`<ns:search xmlns:ns="${NS}"><ns:id>stories</ns:id><ns:term>بز</ns:term></ns:search>`, login);
  assert.deepEqual(titles(stories), ["سه بز کوهی"]);
  assert.match(stories, /<album>[^<]*Free/, "the subtitle is in the phone's words (English)");
  const paid = await send(`<ns:search xmlns:ns="${NS}"><ns:id>stories</ns:id><ns:term>قصه</ns:term></ns:search>`, login);
  assert.deepEqual(titles(paid), ["قصه پولی"]);
  assert.match(paid, /<album>[^<]*In the Dino app/);
  const english = await send(`<ns:search xmlns:ns="${NS}"><ns:id>stories</ns:id><ns:term>goat</ns:term></ns:search>`, login);
  assert.deepEqual(titles(english), [], "an English title is not found in the Farsi Stories category");
});

test("searchTracks gives nothing for a blank term", () => {
  assert.deepEqual(searchTracks([], SearchCategory.Stories, "x", "en"), []);
});
