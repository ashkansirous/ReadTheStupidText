// Run: node --test worker/sonos-search.test.mjs (Slice 440: search on Sonos, both categories over the real handler and the pure matcher).
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

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
  assert.equal(cleanTerm(null), "");
});

test("the categories are named in every label language and match the strings file", () => {
  assert.deepEqual([...SEARCH_LANGUAGES], [...LABEL_LANGUAGES]);
  assert.deepEqual(searchCategories("de").map((c) => c.id), ["stories", "all-languages"]);
  assert.equal(searchCategories("xx")[0].name, "Stories");
  const strings = readFileSync(new URL("../site/dino/sonos/strings.xml", import.meta.url), "utf8");
  for (const code of SEARCH_LANGUAGES) for (const category of searchCategories(code)) assert.ok(strings.includes(`<stringtext language="${code}">${category.name}</stringtext>`), `${code} ${category.id}`);
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

test("searchTracks gives nothing for a blank term", () => {
  assert.deepEqual(searchTracks([], SearchCategory.Stories, "x", "en"), []);
});
