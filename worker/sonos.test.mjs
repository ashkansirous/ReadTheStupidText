// Run: node --test worker/sonos.test.mjs (Slice 283: the anonymous Sonos music service, over a fake bucket and the real stories handler).
import test from "node:test";
import assert from "node:assert/strict";

import { canonicalRedirect, serveObject } from "./downloads.ts";
import { catalogToken, handleSonos, isSonosPath, languageName, trackOf } from "./sonos.ts";
import { escapeXml, parseSonosRequest, unescapeXml } from "./sonos-soap.ts";

const ID_A = "7d0e5a4e000040008000000000000001";
const ID_B = "7d0e5a4e000040008000000000000002";
const NS = "http://www.sonos.com/Services/1.1";

function metaOf(title, extra = {}) {
  return { schemaVersion: 1, title, language: "en", version: 2, added: "2026-11-01", ...extra };
}

function bookFiles(id, meta, listenLangs) {
  const folder = `dinoreader/stories/${id}/`;
  const files = [[`${folder}meta.json`, Buffer.from(JSON.stringify(meta))], [`${folder}story-v2.dino`, Buffer.from("pkg")], [`${folder}cover.jpg`, Buffer.from("jpg")]];
  for (const lang of listenLangs) {
    files.push([`${folder}audio-${lang}-v3.zip`, Buffer.from("zip")], [`${folder}listen-${lang}-v3.m4a`, Buffer.from("m4a")]);
  }
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

const bookA = metaOf("Goldilocks & Bears", { titles: { fa: "دخترک" }, author: "Anon", audio: { en: { version: 3, bytes: 5, listenSeconds: 600 }, fa: { version: 3, bytes: 5, listenSeconds: 540 } } });
const bookB = metaOf("No Track Book", { audio: { en: { version: 3, bytes: 5 } } });
const bookC = metaOf("Second", { audio: { en: { version: 3, bytes: 5, listenSeconds: 120 } } });
const ID_C = "7d0e5a4e000040008000000000000003";

function env(extra = []) {
  return {
    DOWNLOADS: bucketOf([
      ...bookFiles(ID_A, bookA, ["en", "fa"]),
      ...bookFiles(ID_B, bookB, []),
      ...bookFiles(ID_C, bookC, ["en"]),
      ...extra,
    ]),
  };
}

function envelope(action, inner = "") {
  return `<?xml version="1.0"?><s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Header><credentials xmlns="${NS}"><deviceId>XX</deviceId></credentials></s:Header><s:Body><ns:${action} xmlns:ns="${NS}">${inner}</ns:${action}></s:Body></s:Envelope>`;
}

async function call(action, inner = "", { headers = true, bucket = env() } = {}) {
  const init = { method: "POST", body: envelope(action, inner), headers: headers ? { SOAPAction: `"${NS}#${action}"` } : {} };
  const response = await handleSonos(new Request("https://dino.test/sonos", init), bucket, "", serveObject);
  return { response, text: await response.text() };
}

test("the request reader finds the action and the parameters in the body", () => {
  const xml = envelope("getMetadata", "<ns:id>lang:fa</ns:id><ns:index>2</ns:index><ns:count>5</ns:count><ns:recursive>true</ns:recursive>");
  assert.deepEqual(parseSonosRequest(xml), { action: "getMetadata", id: "lang:fa", index: 2, count: 5, recursive: true });
  assert.equal(parseSonosRequest("not xml"), null);
  assert.equal(unescapeXml(escapeXml(`a<&>"'b`)), `a<&>"'b`);
});

test("only the /sonos path is the service's", () => {
  assert.equal(isSonosPath("/sonos"), true);
  assert.equal(isSonosPath("/sonos/x"), false);
});

test("a GET is refused with Allow: POST", async () => {
  const response = await handleSonos(new Request("https://dino.test/sonos"), env(), "", serveObject);
  assert.equal(response.status, 405);
  assert.equal(response.headers.get("Allow"), "POST");
});

test("root lists the languages that have tracks, in their own names", async () => {
  const { response, text } = await call("getMetadata", "<id>root</id><index>0</index><count>100</count>");
  assert.equal(response.status, 200);
  assert.match(response.headers.get("Content-Type"), /^text\/xml/);
  assert.match(text, /<total>2<\/total>/);
  assert.match(text, /<id>lang:en<\/id>/);
  assert.match(text, /<title>English<\/title>/);
  assert.match(text, /<title>فارسی<\/title>/);
  assert.match(text, /<canPlay>false<\/canPlay>/);
  assert.doesNotMatch(text, /No Track Book/);
});

test("a language lists its books as tracks, title in that language, only books with a track", async () => {
  const { text } = await call("getMetadata", "<id>lang:en</id><index>0</index><count>10</count>");
  assert.match(text, /<total>2<\/total>/);
  assert.match(text, /<title>Goldilocks &amp; Bears<\/title>/);
  assert.match(text, new RegExp(`<id>track:en:${ID_A}</id>`));
  assert.match(text, /<mimeType>audio\/mp4<\/mimeType>/);
  assert.match(text, /<duration>600<\/duration>/);
  assert.match(text, new RegExp(`<albumArtURI>https://dino.test/api/stories/${ID_A}/cover\\?v=2</albumArtURI>`));
  const fa = (await call("getMetadata", "<id>lang:fa</id><index>0</index><count>10</count>")).text;
  assert.match(fa, /<title>دخترک<\/title>/);
  assert.match(fa, /<total>1<\/total>/);
});

test("paging returns the asked slice with the right index, count and total", async () => {
  const { text } = await call("getMetadata", "<id>lang:en</id><index>1</index><count>1</count>");
  assert.match(text, /<index>1<\/index><count>1<\/count><total>2<\/total>/);
  assert.equal([...text.matchAll(/<mediaMetadata>/g)].length, 1);
  const past = (await call("getMetadata", "<id>lang:en</id><index>9</index><count>5</count>")).text;
  assert.match(past, /<index>2<\/index><count>0<\/count><total>2<\/total>/);
});

test("a recursive root is the flat track list", async () => {
  const { text } = await call("getMetadata", "<id>root</id><index>0</index><count>50</count><recursive>true</recursive>");
  assert.match(text, /<total>3<\/total>/);
  assert.doesNotMatch(text, /<mediaCollection>/);
});

test("getMediaURI answers the track's https listen URL, getMediaMetadata its fields", async () => {
  const uri = await call("getMediaURI", `<id>track:en:${ID_A}</id>`);
  assert.match(uri.text, new RegExp(`<getMediaURIResult>https://dino.test/api/stories/${ID_A}/listen/en\\?v=3</getMediaURIResult>`));
  const meta = await call("getMediaMetadata", `<id>track:fa:${ID_A}</id>`);
  assert.match(meta.text, /<getMediaMetadataResult><id>track:fa:/);
  assert.match(meta.text, /<duration>540<\/duration>/);
  assert.match(meta.text, /<artist>Anon<\/artist>/);
});

test("getExtendedMetadata answers a track or a language folder", async () => {
  const track = await call("getExtendedMetadata", `<id>track:en:${ID_C}</id>`);
  assert.match(track.text, /<getExtendedMetadataResult><mediaMetadata>/);
  assert.match(track.text, /<artist>Dino Reader<\/artist>/);
  const folder = await call("getExtendedMetadata", "<id>lang:fa</id>");
  assert.match(folder.text, /<getExtendedMetadataResult><mediaCollection>/);
});

test("an unknown item is a Client.ItemNotFound fault with HTTP 500 and nothing else in the body", async () => {
  for (const [action, id] of [["getMetadata", "lang:xx"], ["getMediaURI", "track:en:nope"], ["getMediaMetadata", `track:de:${ID_A}`], ["getMediaURI", "track:en:a:b"], ["getExtendedMetadata", "bogus"]]) {
    const { response, text } = await call(action, `<id>${id}</id>`);
    assert.equal(response.status, 500, action);
    assert.match(text, /<faultcode>Client\.ItemNotFound<\/faultcode>/);
    assert.doesNotMatch(text, /Result>/);
  }
});

test("an unsupported call and a body that is not SOAP are faults", async () => {
  const other = await call("getAppLink");
  assert.equal(other.response.status, 500);
  assert.match(other.text, /Server\.ServiceUnknownError/);
  const junk = await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body: "junk" }), env(), "", serveObject);
  assert.equal(junk.status, 500);
  const huge = await handleSonos(new Request("https://dino.test/sonos", { method: "POST", body: "x".repeat(20000) }), env(), "", serveObject);
  assert.equal(huge.status, 500);
});

test("the size cap counts bytes: Content-Length over it, a multi-byte body and a stream past it are all refused", async () => {
  const send = (init) => handleSonos(new Request("https://dino.test/sonos", { method: "POST", ...init }), env(), "", serveObject);
  const declared = await send({ body: "x", headers: { "Content-Length": "999999" } });
  assert.match(await declared.text(), /Request too large/);
  const multiByte = await send({ body: "é".repeat(9000), headers: { SOAPAction: `"${NS}#getLastUpdate"` } });
  assert.match(await multiByte.text(), /Request too large/);
  const chunk = new TextEncoder().encode("y".repeat(8000));
  let pulls = 0;
  const stream = new ReadableStream({ pull(controller) { pulls += 1; controller.enqueue(chunk); } });
  const streamed = await send({ body: stream, duplex: "half" });
  assert.match(await streamed.text(), /Request too large/);
  assert.ok(pulls < 10, `stopped reading early (${pulls} pulls)`);
});

test("parameters come only from the action element in Body, and the SOAPAction header is never used", async () => {
  const decoy = `<s:Envelope xmlns:s="x"><s:Header><id>decoy</id><index>7</index></s:Header><s:Body><ns:getMetadata xmlns:ns="${NS}"><ns:id>real</ns:id></ns:getMetadata></s:Body></s:Envelope>`;
  const parsed = parseSonosRequest(decoy);
  assert.equal(parsed.id, "real");
  assert.equal(parsed.index, null);
  assert.equal(parseSonosRequest("<s:Envelope xmlns:s=\"x\"/>"), null);
  const empty = `<s:Envelope xmlns:s="x"><s:Header><id>decoy</id></s:Header><s:Body><getLastUpdate/></s:Body></s:Envelope>`;
  assert.equal(parseSonosRequest(empty).id, null);
  const headerOnly = new Request("https://dino.test/sonos", { method: "POST", body: "<s:Envelope xmlns:s=\"x\"><s:Body/></s:Envelope>", headers: { SOAPAction: `"${NS}#getLastUpdate"` } });
  assert.match(await (await handleSonos(headerOnly, env(), "", serveObject)).text(), /Not a SOAP call/);
});

test("a malformed catalogue entry is skipped and browsing still works", async () => {
  const pack = { version: 3, listenSeconds: 60, listenUrl: "/x.m4a" };
  const good = { id: ID_A, title: "T", author: null, coverUrl: null, audio: { en: pack } };
  assert.equal(trackOf(good, "en", "https://dino.test").storyId, ID_A);
  assert.equal(trackOf({ ...good, coverUrl: undefined }, "en", "https://dino.test").coverUrl, null);
  assert.equal(trackOf({ ...good, title: 5 }, "en", "https://dino.test"), null);
  assert.equal(trackOf({ ...good, id: undefined }, "en", "https://dino.test"), null);
  assert.equal(trackOf({ ...good, id: "bad id" }, "en", "https://dino.test"), null);
  assert.equal(trackOf({ ...good, audio: { en: null } }, "en", "https://dino.test"), null);
  assert.equal(trackOf({ ...good, audio: { en: { ...pack, listenSeconds: "9" } } }, "en", "https://dino.test"), null);
});

test("repeated calls reuse the stories handler's 60 s listing cache and do not re-list R2", async () => {
  const inner = env();
  let lists = 0;
  const bucket = { DOWNLOADS: { ...inner.DOWNLOADS, list: async (options) => (lists += 1, inner.DOWNLOADS.list(options)) } };
  await call("getLastUpdate", "", { bucket });
  await call("getMetadata", "<id>root</id>", { bucket });
  await call("getLastUpdate", "", { bucket });
  assert.equal(lists, 1);
});

test("characters XML cannot carry are dropped, so a stray control character cannot break a page", async () => {
  assert.equal(escapeXml("a\u0000b\u0008c\u000Bd￾e\ud800f\tg\nh"), "abcdef\tg\nh");
  const bad = metaOf("Bad\u0001 Title", { audio: { en: { version: 3, bytes: 5, listenSeconds: 60 } } });
  const bucket = { DOWNLOADS: bucketOf(bookFiles(ID_A, bad, ["en"])) };
  const { text } = await call("getMetadata", "<id>lang:en</id>", { bucket });
  assert.match(text, /<title>Bad Title<\/title>/);
  assert.doesNotMatch(text, /[\u0000-\u0008\u000B\u000C\u000E-\u001F]/);
});

test("the Sonos endpoint is never redirected, on either host or prefix", () => {
  for (const host of ["dinoreader.sirous.uk", "x.workers.dev", "readthestupidtext.sirous.uk"]) {
    for (const path of ["/sonos", "/dino/sonos"]) assert.equal(canonicalRedirect(host, path), null, `${host}${path}`);
  }
});

test("without the bucket the service is unavailable, as a fault", async () => {
  const { response, text } = await call("getLastUpdate", "", { bucket: {} });
  assert.equal(response.status, 500);
  assert.match(text, /Server\.ServiceUnavailable/);
});

test("getLastUpdate carries catalog, favorites and a poll interval, and the catalog token follows the tracks", async () => {
  const { text } = await call("getLastUpdate");
  assert.match(text, /<getLastUpdateResult><catalog>3-[0-9a-f]+<\/catalog><favorites>0<\/favorites><pollInterval>600<\/pollInterval>/);
  const one = { language: "en", storyId: "a", version: 1, seconds: 5 };
  assert.equal(catalogToken([one, { ...one, storyId: "b" }]), catalogToken([{ ...one, storyId: "b" }, one]));
  assert.notEqual(catalogToken([one]), catalogToken([{ ...one, version: 2 }]));
  assert.notEqual(catalogToken([one]), catalogToken([]));
  for (const change of [{ title: "T" }, { author: "A" }, { coverUrl: "https://c" }, { listenUrl: "https://l" }, { seconds: 6 }]) {
    assert.notEqual(catalogToken([one]), catalogToken([{ ...one, ...change }]), JSON.stringify(change));
  }
});

test("language names are the language's own; an unknown code shows in capitals", () => {
  assert.equal(languageName("de"), "Deutsch");
  assert.equal(languageName("xx"), "XX");
});

test("the service never logs", async () => {
  const seen = [];
  const original = { log: console.log, error: console.error, warn: console.warn };
  console.log = console.error = console.warn = (...args) => seen.push(args);
  try {
    await call("getMetadata", "<id>lang:xx</id>");
    await call("getMediaURI", `<id>track:en:${ID_A}</id>`);
  } finally {
    Object.assign(console, original);
  }
  assert.deepEqual(seen, []);
});
