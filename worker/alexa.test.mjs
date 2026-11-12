// Run: node --test worker/alexa.test.mjs (Slice 282: the Alexa kid skill's request checks and handlers).
// Certificates, keys and signed bodies are made here (a test CA, an intermediate and a leaf, built with a small DER writer),
// so every check runs offline with an injected root list, certificate fetch and clock.
import test from "node:test";
import assert from "node:assert/strict";
import { generateKeyPairSync, sign } from "node:crypto";

import { serveObject } from "./downloads.ts";
import { AMAZON_ROOT_CA_1, STARFIELD_SERVICES_ROOT_G2, TRUSTED_ROOTS } from "./alexa-roots.ts";
import { findBook, findBooks, handleAlexa, parseToken, playedSeconds } from "./alexa.ts";
import { MAX_CACHED_CHAINS, isValidCertUrl, parseCertificate, parsePemChain, readCapped, verifyAlexaRequest } from "./alexa-verify.ts";
import { trackOf } from "./sonos.ts";

// ---- a tiny DER writer, enough for a certificate ----

const lengthOf = (n) => (n < 128 ? [n] : n < 256 ? [0x81, n] : [0x82, n >> 8, n & 255]);
const tlv = (tag, ...parts) => {
  const body = Buffer.concat(parts);
  return Buffer.concat([Buffer.from([tag, ...lengthOf(body.length)]), body]);
};
const seq = (...parts) => tlv(0x30, ...parts);
const oid = (hex) => tlv(0x06, Buffer.from(hex, "hex"));
const name = (cn) => seq(tlv(0x31, seq(oid("550403"), tlv(0x0c, Buffer.from(cn)))));
const utc = (date) => tlv(0x17, Buffer.from(date.toISOString().replace(/[-:T]/g, "").slice(2, 14) + "Z"));
const SHA256_RSA = seq(oid("2a864886f70d01010b"), tlv(0x05));

function extensions({ san, ca }) {
  const list = [];
  if (san !== undefined) list.push(seq(oid("551d11"), tlv(0x04, seq(...san.map((dns) => tlv(0x82, Buffer.from(dns)))))));
  if (ca !== undefined) list.push(seq(oid("551d13"), tlv(0x01, Buffer.from([0xff])), tlv(0x04, seq(ca ? tlv(0x01, Buffer.from([0xff])) : Buffer.alloc(0)))));
  return list.length === 0 ? Buffer.alloc(0) : tlv(0xa3, seq(...list));
}

function certificate({ subject, issuer, key, signer, from, until, san, ca, innerAlg = SHA256_RSA, outerAlg = SHA256_RSA }) {
  const spki = key.publicKey.export({ type: "spki", format: "der" });
  const tbs = seq(tlv(0xa0, tlv(0x02, Buffer.from([2]))), tlv(0x02, Buffer.from([1])), innerAlg, name(issuer), seq(utc(from), utc(until)), name(subject), spki, extensions({ san, ca }));
  const signature = sign("sha256", tbs, signer.privateKey);
  return seq(tbs, outerAlg, tlv(0x03, Buffer.concat([Buffer.from([0]), signature])));
}

const pem = (der) => `-----BEGIN CERTIFICATE-----\n${der.toString("base64").replace(/(.{64})/g, "$1\n")}\n-----END CERTIFICATE-----\n`;
const newKey = () => generateKeyPairSync("rsa", { modulusLength: 2048 });

const NOW = new Date("2026-11-07T12:00:00Z");
const YEAR = 365 * 24 * 3600 * 1000;
const BEFORE = new Date(NOW.getTime() - YEAR);
const AFTER = new Date(NOW.getTime() + YEAR);
const CERT_URL = "https://s3.amazonaws.com/echo.api/test-chain.pem";
const SKILL = "amzn1.ask.skill.test-1";

const rootKey = newKey();
const interKey = newKey();
const leafKey = newKey();
const otherKey = newKey();
const rootDer = certificate({ subject: "Test Root", issuer: "Test Root", key: rootKey, signer: rootKey, from: BEFORE, until: AFTER, ca: true });
const interDer = certificate({ subject: "Test Intermediate", issuer: "Test Root", key: interKey, signer: rootKey, from: BEFORE, until: AFTER, ca: true });
const leafOptions = { subject: "echo-api.amazon.com", issuer: "Test Intermediate", key: leafKey, signer: interKey, from: BEFORE, until: AFTER, san: ["echo-api.amazon.com"] };
const leafDer = certificate(leafOptions);
const ROOTS = [pem(rootDer)];
const CHAIN = pem(leafDer) + pem(interDer);

function fetcherOf(text) {
  const calls = [];
  const fetchChain = async (url) => {
    calls.push(url);
    return text;
  };
  return { fetchChain, calls };
}

function envelopeOf(request, extra = {}) {
  return { version: "1.0", session: { application: { applicationId: SKILL } }, context: { System: { application: { applicationId: SKILL } } }, request: { requestId: "r", timestamp: NOW.toISOString(), locale: "en-GB", ...request }, ...extra };
}

function signedRequest(envelope, { key = leafKey, url = CERT_URL, body = JSON.stringify(envelope), header = true } = {}) {
  const headers = { "Content-Type": "application/json", SignatureCertChainUrl: url };
  if (header) headers["Signature-256"] = sign("sha256", Buffer.from(body), key.privateKey).toString("base64");
  return { request: new Request("https://dinoreader.sirous.uk/alexa", { method: "POST", headers, body }), body: new TextEncoder().encode(body) };
}

function options(extra = {}) {
  return { now: NOW, roots: ROOTS, fetchChain: fetcherOf(CHAIN).fetchChain, cache: new Map(), skillId: SKILL, ...extra };
}

async function verifies(envelope, signOptions = {}, extra = {}) {
  const { request, body } = signedRequest(envelope, signOptions);
  return (await verifyAlexaRequest(request, body, options(extra))) !== null;
}

// ---- request checks ----

test("a request signed by the leaf of a chain that ends at a trusted root is accepted", async () => {
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" })), true);
});

test("a chain that sends its root too, or only the leaf for a root-signed leaf, is accepted", async () => {
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(CHAIN + pem(rootDer)).fetchChain }), true);
  const direct = certificate({ ...leafOptions, issuer: "Test Root", signer: rootKey });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(direct)).fetchChain }), true);
});

test("a signature made with another key, or over another body, is rejected", async () => {
  const envelope = envelopeOf({ type: "LaunchRequest" });
  assert.equal(await verifies(envelope, { key: otherKey }), false);
  const forged = signedRequest(envelope, { body: JSON.stringify(envelope) });
  const altered = new TextEncoder().encode(JSON.stringify(envelopeOf({ type: "LaunchRequest", requestId: "other" })));
  assert.equal(await verifyAlexaRequest(forged.request, altered, options()), null);
});

test("a request with no Signature-256 header, or one that is not base64, is rejected", async () => {
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), { header: false }), false);
  const { request, body } = signedRequest(envelopeOf({ type: "LaunchRequest" }));
  request.headers.set("Signature-256", "!!not base64!!");
  assert.equal(await verifyAlexaRequest(request, body, options()), null);
});

test("a stale or future timestamp (over 150 s) is rejected, 150 s is accepted", async () => {
  const at = (seconds) => envelopeOf({ type: "LaunchRequest", timestamp: new Date(NOW.getTime() + seconds * 1000).toISOString() });
  assert.equal(await verifies(at(-151)), false);
  assert.equal(await verifies(at(151)), false);
  assert.equal(await verifies(at(-150)), true);
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest", timestamp: "not a time" })), false);
});

test("certificate URLs: only https s3.amazonaws.com under /echo.api/, port 443 or none, no userinfo", () => {
  assert.equal(isValidCertUrl("https://s3.amazonaws.com/echo.api/echo-api-cert.pem"), true);
  assert.equal(isValidCertUrl("https://S3.AMAZONAWS.COM/echo.api/echo-api-cert.pem"), true);
  assert.equal(isValidCertUrl("https://s3.amazonaws.com:443/echo.api/echo-api-cert.pem"), true);
  for (const bad of [
    "http://s3.amazonaws.com/echo.api/c.pem",
    "https://s3.amazonaws.com.evil.test/echo.api/c.pem",
    "https://evil.test/echo.api/c.pem",
    "https://s3.amazonaws.com/echo.api.evil/c.pem",
    "https://s3.amazonaws.com/Echo.api/c.pem",
    "https://s3.amazonaws.com:8443/echo.api/c.pem",
    "https://s3.amazonaws.com/echo.api/../evil/c.pem",
    "https://s3.amazonaws.com/echo.api/%2e%2e/evil/c.pem",
    "https://user@s3.amazonaws.com/echo.api/c.pem",
    "https://s3.amazonaws.com@evil.test/echo.api/c.pem",
    "not a url",
  ]) {
    assert.equal(isValidCertUrl(bad), false, bad);
  }
});

test("a bad certificate URL never triggers a fetch", async () => {
  const fetcher = fetcherOf(CHAIN);
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), { url: "https://evil.test/echo.api/c.pem" }, { fetchChain: fetcher.fetchChain }), false);
  assert.equal(fetcher.calls.length, 0);
});

test("a leaf without echo-api.amazon.com among its names is rejected", async () => {
  const wrong = certificate({ ...leafOptions, san: ["example.com"] });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(wrong) + pem(interDer)).fetchChain }), false);
  const none = certificate({ ...leafOptions, san: undefined });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(none) + pem(interDer)).fetchChain }), false);
});

test("an expired or not yet valid leaf is rejected", async () => {
  const expired = certificate({ ...leafOptions, until: new Date(NOW.getTime() - 1000) });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(expired) + pem(interDer)).fetchChain }), false);
  const early = certificate({ ...leafOptions, from: new Date(NOW.getTime() + 1000) });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(early) + pem(interDer)).fetchChain }), false);
});

test("a chain that does not end at a bundled root is rejected", async () => {
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { roots: [AMAZON_ROOT_CA_1, STARFIELD_SERVICES_ROOT_G2] }), false);
  const strangerRoot = certificate({ subject: "Test Root", issuer: "Test Root", key: otherKey, signer: otherKey, from: BEFORE, until: AFTER, ca: true });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { roots: [pem(strangerRoot)] }), false, "same name, other key");
});

test("certificate hardening: inner and outer signature algorithms must match and be RSA-SHA256; an issuer must be a CA", async () => {
  const sha384 = seq(oid("2a864886f70d01010c"), tlv(0x05));
  const login = (der) => fetcherOf(pem(der) + pem(interDer)).fetchChain;
  const mismatch = certificate({ ...leafOptions, innerAlg: sha384 });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: login(mismatch) }), false, "inner differs from outer");
  const weak = certificate({ ...leafOptions, innerAlg: sha384, outerAlg: sha384 });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: login(weak) }), false, "not SHA-256");
  const direct = certificate({ ...leafOptions, issuer: "Test Root", signer: rootKey });
  const notCaRoot = certificate({ subject: "Test Root", issuer: "Test Root", key: rootKey, signer: rootKey, from: BEFORE, until: AFTER, ca: false });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(direct)).fetchChain }), true, "control: root-signed leaf");
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(direct)).fetchChain, roots: [pem(notCaRoot)] }), false, "root without CA flag");
});

test("a broken link or a non-CA intermediate is rejected", async () => {
  const strangerInter = certificate({ subject: "Test Intermediate", issuer: "Test Root", key: interKey, signer: otherKey, from: BEFORE, until: AFTER, ca: true });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(leafDer) + pem(strangerInter)).fetchChain }), false);
  const notCa = certificate({ subject: "Test Intermediate", issuer: "Test Root", key: interKey, signer: rootKey, from: BEFORE, until: AFTER, ca: false });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(leafDer) + pem(notCa)).fetchChain }), false);
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf("junk").fetchChain }), false);
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: async () => null }), false);
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: async () => { throw new Error("network"); } }), false);
});

test("the skill id is enforced, and an empty or missing id refuses every request (fail closed)", async () => {
  const envelope = envelopeOf({ type: "LaunchRequest" });
  assert.equal(await verifies(envelope, {}, { skillId: SKILL }), true);
  assert.equal(await verifies(envelope, {}, { skillId: "amzn1.ask.skill.other" }), false);
  assert.equal(await verifies(envelope, {}, { skillId: "" }), false);
  assert.equal(await verifies(envelope, {}, { skillId: undefined }), false);
  const noId = envelopeOf({ type: "LaunchRequest" }, { session: undefined, context: {} });
  assert.equal(await verifies(noId, {}, { skillId: "" }), false);
  const contextOnly = envelopeOf({ type: "AudioPlayer.PlaybackStopped" }, { session: undefined });
  assert.equal(await verifies(contextOnly, {}, { skillId: SKILL }), true);
  assert.equal(await verifies({ ...contextOnly, context: {} }, {}, { skillId: SKILL }), false);
});

test("a leaf marked as a CA is rejected", async () => {
  const caLeaf = certificate({ ...leafOptions, ca: true });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(caLeaf) + pem(interDer)).fetchChain }), false);
  const plain = certificate({ ...leafOptions, ca: false });
  assert.equal(await verifies(envelopeOf({ type: "LaunchRequest" }), {}, { fetchChain: fetcherOf(pem(plain) + pem(interDer)).fetchChain }), true, "control: explicit non-CA leaf");
});

test("a stale, foreign-skill, unconfigured or unparsable request never costs a certificate fetch, and the claims are still checked after the signature", async () => {
  const fetcher = fetcherOf(CHAIN);
  const stale = envelopeOf({ type: "LaunchRequest", timestamp: new Date(NOW.getTime() - 151_000).toISOString() });
  assert.equal(await verifies(stale, {}, { fetchChain: fetcher.fetchChain }), false);
  const foreign = envelopeOf({ type: "LaunchRequest" });
  assert.equal(await verifies(foreign, {}, { fetchChain: fetcher.fetchChain, skillId: "amzn1.ask.skill.other" }), false);
  assert.equal(await verifies(foreign, {}, { fetchChain: fetcher.fetchChain, skillId: "" }), false);
  assert.equal(await verifies(foreign, { body: "not json" }, { fetchChain: fetcher.fetchChain }), false);
  assert.equal(fetcher.calls.length, 0);
  assert.equal(await verifies(foreign, {}, { fetchChain: fetcher.fetchChain }), true, "control: a good request does fetch");
  assert.equal(fetcher.calls.length, 1);
});

test("the certificate chain is fetched once per URL, then reused", async () => {
  const fetcher = fetcherOf(CHAIN);
  const shared = options({ fetchChain: fetcher.fetchChain });
  for (let count = 0; count < 3; count += 1) {
    const { request, body } = signedRequest(envelopeOf({ type: "LaunchRequest" }));
    assert.notEqual(await verifyAlexaRequest(request, body, shared), null);
  }
  assert.equal(fetcher.calls.length, 1);
});

test("the cache key is the normalised URL: a query string or case change never makes a new fetch or entry, and the fetch uses the clean URL", async () => {
  const fetcher = fetcherOf(CHAIN);
  const shared = options({ fetchChain: fetcher.fetchChain });
  for (const url of [CERT_URL, `${CERT_URL}?n=1`, `${CERT_URL}?n=2`, "https://S3.AMAZONAWS.COM:443/echo.api/x/../test-chain.pem?z"]) {
    const { request, body } = signedRequest(envelopeOf({ type: "LaunchRequest" }), { url });
    assert.notEqual(await verifyAlexaRequest(request, body, shared), null, url);
  }
  assert.deepEqual(fetcher.calls, [CERT_URL]);
  assert.equal(shared.cache.size, 1);
});

const chainUrl = (count) => `https://s3.amazonaws.com/echo.api/chain-${count}.pem`;

async function verifiesUrl(shared, url) {
  const { request, body } = signedRequest(envelopeOf({ type: "LaunchRequest" }), { url });
  return (await verifyAlexaRequest(request, body, shared)) !== null;
}

test("the cache is capped at 16 successes and evicts the least recently used", async () => {
  const fetcher = fetcherOf(CHAIN);
  const shared = options({ fetchChain: fetcher.fetchChain });
  for (let count = 0; count < MAX_CACHED_CHAINS; count += 1) await verifiesUrl(shared, chainUrl(count));
  await verifiesUrl(shared, chainUrl(0));
  await verifiesUrl(shared, chainUrl(99));
  assert.equal(shared.cache.size, MAX_CACHED_CHAINS);
  assert.equal(shared.cache.has(chainUrl(0)), true, "recently used entry kept");
  assert.equal(shared.cache.has(chainUrl(1)), false, "least recently used entry evicted");
});

test("a failed fetch is never cached: it fails only that request and the next one fetches again", async () => {
  let healthy = false;
  const calls = [];
  const fetchChain = async (url) => {
    calls.push(url);
    return healthy ? CHAIN : null;
  };
  const shared = options({ fetchChain });
  assert.equal(await verifiesUrl(shared, CERT_URL), false);
  assert.equal(await verifiesUrl(shared, CERT_URL), false);
  assert.equal(calls.length, 2, "each failure fetches again");
  assert.equal(shared.cache.size, 0);
  healthy = true;
  assert.equal(await verifiesUrl(shared, CERT_URL), true, "the first success after a failure is accepted at once");
  assert.equal(calls.length, 3);
});

test("failing or varied URLs never evict or replace a verified entry", async () => {
  const good = fetcherOf(CHAIN);
  const shared = options({ fetchChain: good.fetchChain });
  assert.equal(await verifiesUrl(shared, CERT_URL), true);
  const before = shared.cache.get(CERT_URL);
  const junk = fetcherOf("junk");
  for (let count = 0; count < MAX_CACHED_CHAINS * 3; count += 1) {
    assert.equal(await verifiesUrl({ ...shared, fetchChain: junk.fetchChain }, chainUrl(count)), false);
  }
  assert.equal(shared.cache.get(CERT_URL), before);
  assert.equal(await verifiesUrl(shared, CERT_URL), true);
  assert.equal(good.calls.length, 1, "the verified entry still serves without a refetch");
});

test("a cached entry expires with the leaf's notAfter when that is sooner than an hour", async () => {
  const soon = new Date(NOW.getTime() + 600_000);
  const shortLeaf = certificate({ ...leafOptions, until: soon });
  const fetcher = fetcherOf(pem(shortLeaf) + pem(interDer));
  const shared = options({ fetchChain: fetcher.fetchChain });
  assert.equal(await verifiesUrl(shared, CERT_URL), true);
  assert.equal(shared.cache.get(CERT_URL).expires, soon.getTime());
});

test("readCapped stops reading once the limit is passed, whatever any length header says", async () => {
  let pulled = 0;
  const endless = new ReadableStream({
    pull(controller) {
      pulled += 1;
      controller.enqueue(new Uint8Array(1000));
      if (pulled > 10_000) controller.close();
    },
  });
  assert.equal(await readCapped(endless, 5000), null);
  assert.ok(pulled <= 10, `pulled ${pulled} chunks`);
  assert.deepEqual([...(await readCapped(new Response("abc").body, 3))], [97, 98, 99]);
  assert.equal(await readCapped(new Response("abcd").body, 3), null);
  assert.equal((await readCapped(null, 3)).length, 0);
});

test("a chunked body with no Content-Length that grows past 256 KB is refused without reading it all", async () => {
  let pulled = 0;
  const body = new ReadableStream({
    pull(controller) {
      pulled += 1;
      controller.enqueue(new Uint8Array(65_536));
      if (pulled > 1000) controller.close();
    },
  });
  const request = new Request("https://dinoreader.sirous.uk/alexa", { method: "POST", headers: { "Signature-256": "AAAA", SignatureCertChainUrl: CERT_URL }, body, duplex: "half" });
  assert.equal(request.headers.get("Content-Length"), null);
  assert.equal((await handleAlexa(request, env(), "", serveObject, undefined, options())).status, 400);
  assert.ok(pulled < 20, `pulled ${pulled} chunks`);
});

test("the bundled roots are Amazon Root CA 1 and Starfield Services G2 and parse", () => {
  const roots = TRUSTED_ROOTS.map((text) => parsePemChain(text)[0]);
  assert.equal(roots.length, 2);
  for (const root of roots) {
    assert.equal(root.isCa, true);
    assert.ok(root.notAfter > Date.parse("2037-01-01T00:00:00Z"));
    assert.deepEqual(root.issuer, root.subject, "self-signed");
  }
  assert.ok(Buffer.from(roots[0].subject).includes(Buffer.from("Amazon Root CA 1")));
  assert.throws(() => parseCertificate(new Uint8Array([1, 2, 3])));
});

// ---- tokens, names, seconds ----

const BOOK_A = "7d0e5a4e000040008000000000000001";
const BOOK_B = "7d0e5a4e000040008000000000000002";
const BOOK_C = "7d0e5a4e000040008000000000000003";

test("tokens parse only when every field is valid", () => {
  assert.deepEqual(parseToken(`v1|${BOOK_A}|en|3|1500`), { book: BOOK_A, language: "en", version: 3, segmentStartMs: 1500 });
  for (const bad of [undefined, 5, "", "v2|" + BOOK_A + "|en|3|0", `v1|${BOOK_A}|en|3`, `v1|${BOOK_A}|en|3|0|x`, `v1|nothex|en|3|0`, `v1|${BOOK_A}|fa|3|0`, `v1|${BOOK_A}|en|-1|0`, `v1|${BOOK_A}|en|3|-5`, `v1|${BOOK_A}|en|3|1.5`, `v1|${BOOK_A}|en|3|99999999999`, `v1|${BOOK_A}|en|x|0`]) {
    assert.equal(parseToken(bad), null, String(bad));
  }
});

function trackFor(id, title, seconds) {
  return trackOf({ id, title, author: null, titles: null, coverUrl: null, audio: { en: { version: 3, listenSeconds: seconds, listenUrl: `/api/stories/${id}/listen/en?v=3` } } }, "en", "https://dinoreader.sirous.uk");
}

test("a spoken book name finds the listed book: exact, inside, or most of the words", () => {
  const tracks = [trackFor(BOOK_A, "Goldilocks & the Three Bears", 600), trackFor(BOOK_C, "The Little Red Hen", 120)];
  assert.equal(findBook(tracks, "goldilocks and the three bears")?.storyId, BOOK_A);
  assert.equal(findBook(tracks, "Goldilocks")?.storyId, BOOK_A);
  assert.equal(findBook(tracks, "the little red hen story")?.storyId, BOOK_C);
  assert.equal(findBook(tracks, "red hen")?.storyId, BOOK_C);
  assert.equal(findBook(tracks, "dragons"), null);
  assert.equal(findBook(tracks, "   "), null);
});

test("stop words never pick a book, and two titles that fit are ambiguous and listed", async () => {
  const tracks = [trackFor(BOOK_A, "The Little Red Hen", 600), trackFor(BOOK_C, "Little Red Riding Hood", 120)];
  for (const spoken of ["the", "a", "the story", "please read", "and"]) assert.deepEqual(findBooks(tracks, spoken), [], spoken);
  assert.deepEqual(findBooks(tracks, "little red").map((track) => track.storyId).sort(), [BOOK_A, BOOK_C].sort());
  assert.equal(findBook(tracks, "little red"), null);
  assert.equal(findBook(tracks, "red riding hood")?.storyId, BOOK_C);
  assert.equal(findBook(tracks, "the little red hen")?.storyId, BOOK_A, "exact beats the longer title that also contains the words");
  const two = {
    DOWNLOADS: bucketOf([
      ...bookFiles(BOOK_A, { ...withTrack(600), title: "The Little Red Hen" }, true),
      ...bookFiles(BOOK_C, { ...withTrack(120), title: "Little Red Riding Hood" }, true),
    ]),
  };
  const { json } = await call(intent("ReadBookIntent", { book: { name: "book", value: "little red" } }), { env: two });
  const named = json.response.outputSpeech.ssml;
  assert.equal(json.response.directives, undefined, "an ambiguous name plays nothing");
  assert.match(named, /More than one book matches/);
  assert.match(named, /Red Hen/);
  assert.match(named, /Riding Hood/);
});

test("played seconds are the offset minus the segment start, clamped; no offset gives the rest of the book as an estimate", () => {
  const track = trackFor(BOOK_A, "A", 600);
  const token = { book: BOOK_A, language: "en", version: 3, segmentStartMs: 60_000 };
  assert.deepEqual(playedSeconds(token, track, 100_000), { seconds: 40, detail: "reported" });
  assert.deepEqual(playedSeconds(token, track, 10_000), { seconds: 0, detail: "reported" });
  assert.deepEqual(playedSeconds(token, track, 9_000_000), { seconds: 600, detail: "reported" });
  assert.deepEqual(playedSeconds(token, track, undefined), { seconds: 540, detail: "estimated" });
});

// ---- handlers, over a fake bucket and the real stories handler ----

function metaOf(title, extra = {}) {
  return { schemaVersion: 1, title, language: "en", version: 2, added: "2026-11-01", ...extra };
}

function bookFiles(id, meta, withTrack) {
  const folder = `dinoreader/stories/${id}/`;
  const files = [[`${folder}meta.json`, Buffer.from(JSON.stringify(meta))], [`${folder}story-v2.dino`, Buffer.from("pkg")], [`${folder}cover.jpg`, Buffer.from("jpg")]];
  if (withTrack) files.push([`${folder}audio-en-v3.zip`, Buffer.from("zip")], [`${folder}listen-en-v3.m4a`, Buffer.from("m4a")]);
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

const withTrack = (seconds) => metaOf("x", { audio: { en: { version: 3, bytes: 5, listenSeconds: seconds } } });

function env(extra = {}) {
  return {
    DOWNLOADS: bucketOf([
      ...bookFiles(BOOK_A, { ...withTrack(600), title: "Goldilocks & Bears", author: "Anon" }, true),
      ...bookFiles(BOOK_B, metaOf("No Track Book", { audio: { en: { version: 3, bytes: 5 } } }), false),
      ...bookFiles(BOOK_C, { ...withTrack(120), title: "The Little Red Hen" }, true),
    ]),
    ...extra,
  };
}

function datasetOf() {
  const points = [];
  return { points, writeDataPoint: (point) => points.push(point) };
}

async function call(request, extra = {}) {
  const dataset = extra.dataset ?? datasetOf();
  const usage = { dataset, telemetryEnabled: async () => extra.telemetry ?? true };
  const signed = signedRequest(envelopeOf(request, extra.envelope ?? {}));
  const response = await handleAlexa(signed.request, extra.env ?? env(), "", serveObject, usage, options({ fetchChain: fetcherOf(CHAIN).fetchChain }));
  const text = await response.text();
  return { status: response.status, json: text === "" ? null : JSON.parse(text), dataset };
}

const playing = (book, offset, version = 3, start = 0) => ({ context: { AudioPlayer: { token: `v1|${book}|en|${version}|${start}`, offsetInMilliseconds: offset, playerActivity: "PLAYING" } } });
const intent = (intentName, slots) => ({ type: "IntentRequest", intent: { name: intentName, slots } });

async function bookOrder() {
  const { json } = await call({ type: "LaunchRequest" });
  const speech = json.response.outputSpeech.ssml;
  return speech.indexOf("Goldilocks") < speech.indexOf("Little Red Hen") ? [BOOK_A, BOOK_C] : [BOOK_C, BOOK_A];
}

test("launch greets and lists the books that have an English track, and keeps listening", async () => {
  const { status, json } = await call({ type: "LaunchRequest" });
  assert.equal(status, 200);
  assert.equal(json.response.shouldEndSession, false);
  assert.match(json.response.outputSpeech.ssml, /Goldilocks &amp; Bears/);
  assert.match(json.response.outputSpeech.ssml, /Little Red Hen/);
  assert.doesNotMatch(json.response.outputSpeech.ssml, /No Track Book/);
  assert.ok(json.response.reprompt.outputSpeech.ssml);
});

test("launch with no books says so and ends the session", async () => {
  const { json } = await call({ type: "LaunchRequest" }, { env: { DOWNLOADS: bucketOf([]) } });
  assert.equal(json.response.shouldEndSession, true);
  assert.match(json.response.outputSpeech.ssml, /no books/);
});

test("reading a book plays its track from the start with a token carrying book, language, version and segment start", async () => {
  const { json } = await call(intent("ReadBookIntent", { book: { name: "book", value: "goldilocks" } }));
  const [directive] = json.response.directives;
  assert.equal(directive.type, "AudioPlayer.Play");
  assert.equal(directive.playBehavior, "REPLACE_ALL");
  assert.match(directive.audioItem.stream.url, new RegExp(`^https://dinoreader\\.sirous\\.uk/api/stories/${BOOK_A}/listen/en\\?v=3$`));
  assert.equal(directive.audioItem.stream.offsetInMilliseconds, 0);
  assert.deepEqual(parseToken(directive.audioItem.stream.token), { book: BOOK_A, language: "en", version: 3, segmentStartMs: 0 });
  assert.equal(directive.audioItem.metadata.title, "Goldilocks & Bears");
  assert.equal(json.response.shouldEndSession, true);
});

test("an unknown book name, or no name, lists the books instead of playing", async () => {
  for (const slots of [{ book: { name: "book", value: "dragons" } }, {}]) {
    const { json } = await call(intent("ReadBookIntent", slots));
    assert.equal(json.response.directives, undefined);
    assert.match(json.response.outputSpeech.ssml, /could not find that book/);
    assert.equal(json.response.shouldEndSession, false);
  }
});

test("pause stops with no speech and stores nothing; stop says goodbye", async () => {
  const pause = (await call(intent("AMAZON.PauseIntent"), { envelope: playing(BOOK_A, 5000) })).json;
  assert.deepEqual(pause.response.directives, [{ type: "AudioPlayer.Stop" }]);
  assert.equal(pause.response.outputSpeech, undefined);
  const stop = (await call(intent("AMAZON.StopIntent"), { envelope: playing(BOOK_A, 5000) })).json;
  assert.deepEqual(stop.response.directives, [{ type: "AudioPlayer.Stop" }]);
  assert.match(stop.response.outputSpeech.ssml, /Goodbye/);
});

test("resume plays the same book at the offset Alexa sent, with a new token starting there", async () => {
  const { json } = await call(intent("AMAZON.ResumeIntent"), { envelope: playing(BOOK_A, 61_234) });
  const stream = json.response.directives[0].audioItem.stream;
  assert.equal(stream.offsetInMilliseconds, 61_234);
  assert.deepEqual(parseToken(stream.token), { book: BOOK_A, language: "en", version: 3, segmentStartMs: 61_234 });
});

test("resume at or past the end starts the book over, and with a foreign, stale or missing token lists the books", async () => {
  for (const offset of [99_999_999, 600_000, 600_001]) {
    const past = (await call(intent("AMAZON.ResumeIntent"), { envelope: playing(BOOK_A, offset) })).json;
    assert.equal(past.response.directives[0].audioItem.stream.offsetInMilliseconds, 0, String(offset));
  }
  const nearEnd = (await call(intent("AMAZON.ResumeIntent"), { envelope: playing(BOOK_A, 599_999) })).json;
  assert.equal(nearEnd.response.directives[0].audioItem.stream.offsetInMilliseconds, 599_999);
  for (const envelope of [{ context: { AudioPlayer: { token: "garbage", offsetInMilliseconds: 5 } } }, playing("7d0e5a4e000040008000000000000009", 5), { context: {} }]) {
    const { json } = await call(intent("AMAZON.ResumeIntent"), { envelope });
    assert.equal(json.response.directives, undefined);
    assert.match(json.response.outputSpeech.ssml, /nothing to continue/);
  }
});

test("resume from a token whose version differs from the listed track starts the book at 0, and go home ends the session like stop", async () => {
  const { json } = await call(intent("AMAZON.ResumeIntent"), { envelope: playing(BOOK_A, 61_234, 2) });
  const stream = json.response.directives[0].audioItem.stream;
  assert.equal(stream.offsetInMilliseconds, 0);
  assert.equal(parseToken(stream.token).version, 3);
  const home = (await call(intent("AMAZON.NavigateHomeIntent"), { envelope: playing(BOOK_A, 5000) })).json;
  assert.deepEqual(home.response.directives, [{ type: "AudioPlayer.Stop" }]);
  assert.equal(home.response.shouldEndSession, true);
});

test("next and previous move through the live list; past either end they say so and play nothing", async () => {
  const [first, second] = await bookOrder();
  const next = (await call(intent("AMAZON.NextIntent"), { envelope: playing(first, 9000) })).json;
  assert.equal(parseToken(next.response.directives[0].audioItem.stream.token).book, second);
  assert.equal(next.response.directives[0].audioItem.stream.offsetInMilliseconds, 0);
  const last = (await call(intent("AMAZON.NextIntent"), { envelope: playing(second, 9000) })).json;
  assert.equal(last.response.directives, undefined);
  assert.match(last.response.outputSpeech.ssml, /last book/);
  assert.equal(last.response.shouldEndSession, true, "the voice answer past the last book speaks and ends the session");
  const previous = (await call(intent("AMAZON.PreviousIntent"), { envelope: playing(second, 9000) })).json;
  assert.equal(parseToken(previous.response.directives[0].audioItem.stream.token).book, first);
  const none = (await call(intent("AMAZON.PreviousIntent"), { envelope: playing(first, 9000) })).json;
  assert.match(none.response.outputSpeech.ssml, /first book/);
  assert.equal(none.response.shouldEndSession, true);
});

test("start over plays the current book from 0", async () => {
  const { json } = await call(intent("AMAZON.StartOverIntent"), { envelope: playing(BOOK_C, 70_000, 3, 10_000) });
  assert.equal(json.response.directives[0].audioItem.stream.offsetInMilliseconds, 0);
  assert.equal(parseToken(json.response.directives[0].audioItem.stream.token).book, BOOK_C);
});

test("help and an unknown intent list the books", async () => {
  for (const request of [intent("AMAZON.HelpIntent"), intent("AMAZON.FallbackIntent"), intent("SomethingElseIntent")]) {
    assert.match((await call(request)).json.response.outputSpeech.ssml, /Goldilocks/);
  }
});

test("playback commands answer with directives only, never speech", async () => {
  const next = (await call({ type: "PlaybackController.NextCommandIssued" }, { envelope: playing(BOOK_A, 1000) })).json;
  assert.equal(next.response.outputSpeech, undefined);
  const pause = (await call({ type: "PlaybackController.PauseCommandIssued" }, { envelope: playing(BOOK_A, 1000) })).json;
  assert.deepEqual(pause.response.directives, [{ type: "AudioPlayer.Stop" }]);
  const nothing = (await call({ type: "PlaybackController.PlayCommandIssued" }, { envelope: { context: {} } })).json;
  assert.equal(nothing.response.outputSpeech, undefined);
  const last = (await call({ type: "PlaybackController.NextCommandIssued" }, { envelope: playing((await bookOrder())[1], 1000) })).json;
  for (const answer of [next, pause, nothing, last]) {
    // Amazon: PlaybackController answers hold AudioPlayer directives only; outputSpeech, card, reprompt and the like are an error
    assert.deepEqual(Object.keys(answer.response).filter((key) => key !== "directives"), [], JSON.stringify(answer));
    assert.ok((answer.response.directives ?? []).every((directive) => directive.type.startsWith("AudioPlayer.")));
  }
});

test("playback events and the session end answer with an empty response", async () => {
  for (const type of ["AudioPlayer.PlaybackStarted", "AudioPlayer.PlaybackNearlyFinished", "AudioPlayer.PlaybackFailed", "SessionEndedRequest", "System.ExceptionEncountered"]) {
    const { status, json, dataset } = await call({ type, token: `v1|${BOOK_A}|en|3|0`, offsetInMilliseconds: 5000 });
    assert.equal(status, 200);
    assert.deepEqual(json, { version: "1.0", response: {} });
    assert.equal(dataset.points.length, 0, type);
  }
});

test("a stopped segment is one alexa row: book, language, reported seconds since the token's start, no household id", async () => {
  const { json, dataset } = await call({ type: "AudioPlayer.PlaybackStopped", token: `v1|${BOOK_A}|en|3|60000`, offsetInMilliseconds: 100_000 });
  assert.deepEqual(json, { version: "1.0", response: {} });
  assert.equal(dataset.points.length, 1);
  const point = dataset.points[0];
  assert.match(point.blobs[7], /^\d{4}-\d{2}-\d{2}$/);
  assert.deepEqual([...point.blobs.slice(0, 7), point.blobs[8]], ["book_listened", BOOK_A, "en", "shelf", "no", "", "alexa", "reported"]);
  assert.deepEqual(point.doubles, [1, -1, -1, -1, 40, -1]); // Slice 351: double6 (seconds since launch) is -1 for a speaker row
  assert.equal(point.blobs[15], "");
});

test("a finished segment with no offset is an estimate of the rest of the book; a paused-and-resumed book makes two rows", async () => {
  const finished = await call({ type: "AudioPlayer.PlaybackFinished", token: `v1|${BOOK_A}|en|3|60000` });
  assert.equal(finished.dataset.points[0].blobs[8], "estimated");
  assert.equal(finished.dataset.points[0].doubles[4], 540);
  const dataset = datasetOf();
  await call({ type: "AudioPlayer.PlaybackStopped", token: `v1|${BOOK_A}|en|3|0`, offsetInMilliseconds: 30_000 }, { dataset });
  await call({ type: "AudioPlayer.PlaybackStopped", token: `v1|${BOOK_A}|en|3|30000`, offsetInMilliseconds: 50_000 }, { dataset });
  assert.deepEqual(dataset.points.map((point) => point.doubles[4]), [30, 20]);
});

test("a book missing from a loaded listing is still counted from its token; a bad token or telemetry off counts nothing", async () => {
  const bad = await call({ type: "AudioPlayer.PlaybackStopped", token: "garbage", offsetInMilliseconds: 5000 });
  assert.equal(bad.dataset.points.length, 0);
  const unlisted = await call({ type: "AudioPlayer.PlaybackStopped", token: `v1|${"7d0e5a4e000040008000000000000009"}|en|3|0`, offsetInMilliseconds: 5000 });
  assert.equal(unlisted.dataset.points.length, 1);
  assert.equal(unlisted.dataset.points[0].doubles[4], 5);
  const off = await call({ type: "AudioPlayer.PlaybackStopped", token: `v1|${BOOK_A}|en|3|0`, offsetInMilliseconds: 5000 }, { telemetry: false });
  assert.equal(off.dataset.points.length, 0);
});

test("when the catalogue cannot be read the skill apologises (speech requests) or answers empty (events)", async () => {
  const broken = { DOWNLOADS: { list: async () => { throw new Error("r2"); } } };
  const launch = await call({ type: "LaunchRequest" }, { env: broken });
  assert.match(launch.json.response.outputSpeech.ssml, /cannot get the books/);
  const event = await call({ type: "AudioPlayer.PlaybackStopped", token: `v1|${BOOK_A}|en|3|0`, offsetInMilliseconds: 5 }, { env: broken });
  assert.deepEqual(event.json, { version: "1.0", response: {} });
});

test("an unreadable catalogue still records the segment from its token: seconds clamped at 0 and at a cap, detail reported", async () => {
  const broken = { DOWNLOADS: { list: async () => { throw new Error("r2"); } } };
  const rows = async (token, offset) => (await call({ type: "AudioPlayer.PlaybackStopped", token, offsetInMilliseconds: offset }, { env: broken })).dataset.points;
  const [point] = await rows(`v1|${BOOK_A}|en|3|60000`, 100_000);
  assert.deepEqual([...point.blobs.slice(0, 3), point.blobs[8]], ["book_listened", BOOK_A, "en", "reported"]);
  assert.equal(point.doubles[4], 40);
  assert.equal((await rows(`v1|${BOOK_A}|en|3|60000`, 10_000))[0].doubles[4], 0);
  assert.ok((await rows(`v1|${BOOK_A}|en|3|0`, 99_999_999_999))[0].doubles[4] <= 86_400);
  const unknownLength = await call({ type: "AudioPlayer.PlaybackFinished", token: `v1|${BOOK_A}|en|3|60000` }, { env: broken });
  assert.equal(unknownLength.dataset.points[0].doubles[4], 0);
  assert.equal(unknownLength.dataset.points[0].blobs[8], "estimated");
  const speech = await call({ type: "LaunchRequest" }, { env: broken });
  assert.equal(speech.dataset.points.length, 0);
});

test("the endpoint is POST only and refuses unsigned, forged and foreign-skill requests with a bare 400", async () => {
  const get = await handleAlexa(new Request("https://dinoreader.sirous.uk/alexa"), env(), "", serveObject);
  assert.equal(get.status, 405);
  assert.equal(get.headers.get("Allow"), "POST");
  const unsigned = new Request("https://dinoreader.sirous.uk/alexa", { method: "POST", body: "{}" });
  assert.equal((await handleAlexa(unsigned, env(), "", serveObject, undefined, options())).status, 400);
  const forged = signedRequest(envelopeOf({ type: "LaunchRequest" }), { key: otherKey });
  const forgedResponse = await handleAlexa(forged.request, env(), "", serveObject, undefined, options());
  assert.equal(forgedResponse.status, 400);
  assert.equal(await forgedResponse.text(), "");
  const foreign = signedRequest(envelopeOf({ type: "LaunchRequest" }));
  assert.equal((await handleAlexa(foreign.request, env({ ALEXA_SKILL_ID: "amzn1.ask.skill.other" }), "", serveObject, undefined, { ...options(), skillId: undefined })).status, 400);
  const ours = signedRequest(envelopeOf({ type: "LaunchRequest" }));
  assert.equal((await handleAlexa(ours.request, env({ ALEXA_SKILL_ID: SKILL }), "", serveObject, undefined, { ...options(), skillId: undefined })).status, 200);
  const unconfigured = signedRequest(envelopeOf({ type: "LaunchRequest" }));
  assert.equal((await handleAlexa(unconfigured.request, env({ ALEXA_SKILL_ID: "" }), "", serveObject, undefined, { ...options(), skillId: undefined })).status, 400, "no ALEXA_SKILL_ID configured: every request refused");
});

test("the skill package matches the Worker: endpoint, AudioPlayer, kid skill, and the intents and slot the handlers read", async () => {
  const { readFileSync } = await import("node:fs");
  const read = (path) => JSON.parse(readFileSync(new URL(`../alexa/skill-package/${path}`, import.meta.url), "utf8"));
  const manifest = read("skill.json").manifest;
  assert.equal(manifest.apis.custom.endpoint.uri, "https://dinoreader.sirous.uk/alexa");
  assert.deepEqual(manifest.apis.custom.interfaces, [{ type: "AUDIO_PLAYER" }]);
  assert.equal(manifest.privacyAndCompliance.isChildDirected, true);
  // Amazon's skill-manifest reference: a child-directed skill's category is one of these four (KIDS is not a value at all)
  assert.ok(["CHILDRENS_EDUCATION_AND_REFERENCE", "CHILDRENS_GAMES", "CHILDRENS_MUSIC_AND_AUDIO", "CHILDRENS_NOVELTY_AND_HUMOR"].includes(manifest.publishingInformation.category));
  for (const [locale, info] of Object.entries(manifest.publishingInformation.locales)) {
    for (const [field, size] of [["smallIconUri", 108], ["largeIconUri", 512]]) {
      const png = readFileSync(new URL(`../alexa/skill-package/${info[field].replace("file://", "")}`, import.meta.url));
      assert.equal(png.subarray(1, 4).toString(), "PNG", `${locale} ${field}`);
      assert.deepEqual([png.readUInt32BE(16), png.readUInt32BE(20)], [size, size], `${locale} ${field} size`);
    }
  }
  assert.equal(manifest.privacyAndCompliance.usesPersonalInfo, false);
  for (const locale of ["en-GB", "en-US"]) {
    const model = read(`interactionModels/custom/${locale}.json`).interactionModel.languageModel;
    const names = model.intents.map((item) => item.name);
    for (const wanted of ["ReadBookIntent", "AMAZON.PauseIntent", "AMAZON.ResumeIntent", "AMAZON.StopIntent", "AMAZON.NextIntent", "AMAZON.PreviousIntent", "AMAZON.StartOverIntent"]) {
      assert.ok(names.includes(wanted), `${locale} ${wanted}`);
    }
    const read_book = model.intents.find((item) => item.name === "ReadBookIntent");
    assert.deepEqual(read_book.slots, [{ name: "book", type: "AMAZON.SearchQuery" }]);
    assert.ok(read_book.samples.every((sample) => sample.includes("{book}")));
    assert.ok(manifest.publishingInformation.locales[locale] !== undefined);
  }
});
