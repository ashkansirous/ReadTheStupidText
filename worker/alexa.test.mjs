// Run: node --test worker/alexa.test.mjs (Slice 282: the Alexa kid skill's request checks and handlers).
// Certificates, keys and signed bodies are made here (a test CA, an intermediate and a leaf, built with a small DER writer),
// so every check runs offline with an injected root list, certificate fetch and clock.
import test from "node:test";
import assert from "node:assert/strict";
import { generateKeyPairSync, sign } from "node:crypto";

import { serveObject } from "./downloads.ts";
import { AMAZON_ROOT_CA_1, STARFIELD_SERVICES_ROOT_G2, TRUSTED_ROOTS } from "./alexa-roots.ts";
import { CLIP_FOLDER, findBook, findBooks, handleAlexa, parseToken, playedSeconds } from "./alexa.ts";
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
  const response = await handleAlexa(signed.request, extra.env ?? env(), "", serveObject, usage, options({ fetchChain: fetcherOf(CHAIN).fetchChain }), extra.services);
  const text = await response.text();
  return { status: response.status, json: text === "" ? null : JSON.parse(text), dataset };
}

const playing = (book, offset, version = 3, start = 0) => ({ context: { AudioPlayer: { token: `v1|${book}|en|${version}|${start}`, offsetInMilliseconds: offset, playerActivity: "PLAYING" } } });
const intent = (intentName, slots) => ({ type: "IntentRequest", intent: { name: intentName, slots } });

async function bookOrder() {
  const { json } = await call(intent("SearchIntent"));
  const speech = json.response.outputSpeech.ssml;
  return speech.indexOf("Goldilocks") < speech.indexOf("Little Red Hen") ? [BOOK_A, BOOK_C] : [BOOK_C, BOOK_A];
}

test("launch asks about one playable book and keeps listening; search lists the books that have an English track", async () => {
  const { status, json } = await call({ type: "LaunchRequest" });
  assert.equal(status, 200);
  assert.equal(json.response.shouldEndSession, false);
  assert.match(json.response.outputSpeech.ssml, /^<speak>Do you want to hear (Goldilocks &amp; Bears|The Little Red Hen)\? Or say: search\.<\/speak>$/);
  assert.ok(json.response.reprompt.outputSpeech.ssml);
  const searched = (await call(intent("SearchIntent"))).json.response.outputSpeech.ssml;
  assert.match(searched, /Goldilocks &amp; Bears/);
  assert.match(searched, /Little Red Hen/);
  assert.doesNotMatch(searched, /No Track Book/);
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

test("an unknown book name, or no name, says it does not know it and what to say instead of playing", async () => {
  for (const slots of [{ book: { name: "book", value: "dragons" } }, {}]) {
    const { json } = await call(intent("ReadBookIntent", slots));
    assert.equal(json.response.directives, undefined);
    assert.match(json.response.outputSpeech.ssml, /I don't know that one\. Say: search\./);
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
  const wrapped = (await call(intent("AMAZON.NextIntent"), { envelope: playing(second, 9000) })).json;
  assert.equal(parseToken(wrapped.response.directives[0].audioItem.stream.token).book, first, "review: next past the last book wraps, as after the clip");
  const previous = (await call(intent("AMAZON.PreviousIntent"), { envelope: playing(second, 9000) })).json;
  assert.equal(parseToken(previous.response.directives[0].audioItem.stream.token).book, first);
  const wrappedBack = (await call(intent("AMAZON.PreviousIntent"), { envelope: playing(first, 9000) })).json;
  assert.equal(parseToken(wrappedBack.response.directives[0].audioItem.stream.token).book, second, "review: previous before the first book wraps");
});

test("start over plays the current book from 0", async () => {
  const { json } = await call(intent("AMAZON.StartOverIntent"), { envelope: playing(BOOK_C, 70_000, 3, 10_000) });
  assert.equal(json.response.directives[0].audioItem.stream.offsetInMilliseconds, 0);
  assert.equal(parseToken(json.response.directives[0].audioItem.stream.token).book, BOOK_C);
});

test("help and an unknown intent say the one short line; a fallback says it does not know that and what to say", async () => {
  for (const request of [intent("AMAZON.HelpIntent"), intent("SomethingElseIntent")]) {
    assert.equal((await call(request)).json.response.outputSpeech.ssml, "<speak>Say a book's name, or say: search.</speak>");
  }
  assert.match((await call(intent("AMAZON.FallbackIntent"))).json.response.outputSpeech.ssml, /I don't know that one\. Say: search\./);
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
  for (const type of ["AudioPlayer.PlaybackStarted", "AudioPlayer.PlaybackFailed", "SessionEndedRequest", "System.ExceptionEncountered"]) {
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

// ---- Slice 457: matching, list, paid books, favourites, logging ----

const BOOK_P = "7d0e5a4e000040008000000000000004";
const BOOK_Q = "7d0e5a4e000040008000000000000005";
const USER = "amzn1.ask.account.TESTUSER1";
const SERVICES_OFF = { paidBooksEnabled: async () => false };

test("slice 457 matching: spelling slack, prefixes, number words, plurals, a whole title beats a partial one, equal books tie", () => {
  const tracks = [
    trackFor(BOOK_A, "Goldilocks and the Three Bears", 600),
    trackFor(BOOK_B, "The Lion and the Mouse", 300),
    trackFor(BOOK_C, "The Gingerbread Man", 120),
    trackFor(BOOK_P, "The Three Little Pigs", 120),
    trackFor(BOOK_Q, "Little Red Riding Hood", 120),
  ];
  for (const spoken of ["goldylock", "goldy", "goldilocks", "goldilocks and the 3 bears", "three bears", "3 bear", "goldilock and the three bear"]) {
    assert.deepEqual(findBooks(tracks, spoken).map((track) => track.storyId), [BOOK_A], spoken);
  }
  for (const spoken of ["the lion", "lion", "the lion and the mouse", "liyon"]) assert.deepEqual(findBooks(tracks, spoken).map((track) => track.storyId), [BOOK_B], spoken);
  for (const spoken of ["gingerbread", "gingerbread man", "gingerbred"]) assert.deepEqual(findBooks(tracks, spoken).map((track) => track.storyId), [BOOK_C], spoken);
  assert.deepEqual(findBooks(tracks, "three little pigs").map((track) => track.storyId), [BOOK_P]);
  assert.deepEqual(findBooks(tracks, "riding hood").map((track) => track.storyId), [BOOK_Q]);
  assert.deepEqual(findBooks(tracks, "three").map((track) => track.storyId).sort(), [BOOK_A, BOOK_P].sort(), "a tie names both");
  assert.deepEqual(findBooks(tracks, "dragons"), []);
  assert.deepEqual(findBooks(tracks, "xyz"), [], "short words need to be exact");
});

test("slice 457 matching: a word that is an exact match beats a spelling-slack match ('hood' is Riding Hood, not Good Enough)", () => {
  const tracks = [trackFor(BOOK_A, "Good Enough", 100), trackFor(BOOK_B, "Little Red Riding Hood", 100)];
  assert.deepEqual(findBooks(tracks, "hood").map((track) => track.storyId), [BOOK_B]);
});

function metaFor(title, access) {
  return { ...withTrack(300), title, ...(access === "paid" ? { access: "paid", productId: "dino.book.test" } : {}) };
}

/** Free: A "Goldilocks and the Three Bears", C "The Little Red Hen"; paid: P "The Lion and the Mouse"; B has no listening track. */
function shelf(extra = {}) {
  return {
    DOWNLOADS: bucketOf([
      ...bookFiles(BOOK_A, metaFor("Goldilocks and the Three Bears"), true),
      ...bookFiles(BOOK_B, metaOf("No Track Book", { audio: { en: { version: 3, bytes: 5 } } }), false),
      ...bookFiles(BOOK_C, metaFor("The Little Red Hen"), true),
      ...bookFiles(BOOK_P, metaFor("The Lion and the Mouse", "paid"), true),
    ]),
    ...extra,
  };
}

/** `told` is the alexa_subscription_told table (user id -> epoch ms); `missingTold` makes every query on it fail like a table that was never created. */
function fakeDb({ missingTold = false } = {}) {
  const rows = [];
  const told = new Map();
  const toldAnswer = (sql, args) => {
    const fail = () => { throw new Error("D1_ERROR: no such table: alexa_subscription_told"); };
    return {
      first: async () => (missingTold ? fail() : told.has(args[0]) ? { told_at: told.get(args[0]) } : null),
      run: async () => (missingTold ? fail() : (told.set(args[0], args[1]), {})),
    };
  };
  const answer = (sql, args) => ({
    all: async () => ({ results: /SELECT/.test(sql) ? rows.filter((row) => row.user_id === args[0]).map((row) => ({ book: row.book })) : [] }),
    run: async () => {
      if (/INSERT/.test(sql) && !rows.some((row) => row.user_id === args[0] && row.book === args[1])) rows.push({ user_id: args[0], book: args[1] });
      if (/DELETE/.test(sql)) rows.splice(0, rows.length, ...rows.filter((row) => !(row.user_id === args[0] && row.book === args[1])));
      return {};
    },
  });
  return { rows, told, prepare: (sql) => ({ bind: (...args) => (/alexa_subscription_told/.test(sql) ? toldAnswer(sql, args) : answer(sql, args)) }) };
}

const asUser = (envelope = {}) => ({ session: { application: { applicationId: SKILL }, user: { userId: USER } }, ...envelope });
const speechOf = (json) => json.response.outputSpeech.ssml;
const playsOf = (json) => (json.response.directives ?? []).filter((directive) => directive.type === "AudioPlayer.Play");

const wordCount = (ssml) => ssml.replace(/<[^>]+>/g, " ").trim().split(/\s+/).length;
const T0 = Date.UTC(2026, 10, 26, 12);
const DAY_MS = 24 * 60 * 60 * 1000;
const at = (ms) => ({ now: () => ms });

test("slice 457 decision 14: the launch is one short question about a random playable book, nothing else, and keeps the offered book", async () => {
  const seen = new Set();
  for (const random of [0, 0.4, 0.99]) {
    const { json } = await call({ type: "LaunchRequest" }, { env: shelf(), services: { random: () => random } });
    const speech = speechOf(json);
    assert.match(speech, /^<speak>Do you want to hear (Goldilocks and the Three Bears|The Little Red Hen)\? Or say: search\.<\/speak>$/);
    assert.ok(wordCount(speech) <= 15, `${wordCount(speech)} words`);
    assert.doesNotMatch(speech, /Lion and the Mouse|No Track Book|favourites|books|subscription|repeat/, "no paid or trackless book, no counts, no list, no end-of-story words");
    assert.ok([BOOK_A, BOOK_C].includes(json.sessionAttributes.offered));
    seen.add(json.sessionAttributes.offered);
    assert.equal(json.response.shouldEndSession, false);
    assert.equal(playsOf(json).length, 0);
  }
  assert.equal(seen.size, 2, "the book is random each time");
});

test("slice 457 decision 14: a title too long for five seconds is not offered while a shorter book exists", async () => {
  const files = [...bookFiles(BOOK_A, metaFor("The Extraordinarily Long Adventures of the Tiny Brave Little Bear"), true), ...bookFiles(BOOK_C, metaFor("The Little Red Hen"), true)];
  for (const random of [0, 0.5, 0.99]) {
    const { json } = await call({ type: "LaunchRequest" }, { env: { DOWNLOADS: bucketOf(files) }, services: { random: () => random } });
    assert.equal(json.sessionAttributes.offered, BOOK_C);
    assert.ok(wordCount(speechOf(json)) <= 15);
  }
});

test("slice 457 decision 14: 'yes' plays the offered book; with none offered, or one that no longer plays, it is a search", async () => {
  const { json } = await call(intent("AMAZON.YesIntent"), { env: shelf(), envelope: { session: { attributes: { offered: BOOK_C } } } });
  assert.deepEqual(tokenOfPlay(json), { book: BOOK_C, language: "en", version: 3, segmentStartMs: 0 });
  assert.equal(json.response.shouldEndSession, true);
  for (const envelope of [{}, { session: { attributes: { offered: BOOK_P } } }, { session: { attributes: { offered: 7 } } }]) {
    const searched = (await call(intent("AMAZON.YesIntent"), { env: shelf(), envelope })).json;
    assert.equal(playsOf(searched).length, 0);
    assert.match(speechOf(searched), /You have 2 books: .*Which one\?/);
  }
});

test("slice 457 decision 15: 'no', 'search' and every older list wording give the same search reply", async () => {
  const replies = [];
  for (const name of ["AMAZON.NoIntent", "SearchIntent", "ListBooksIntent", "MoreBooksIntent"]) {
    replies.push(speechOf((await call(intent(name), { env: shelf(), services: at(T0) })).json));
  }
  assert.equal(new Set(replies).size, 1);
  assert.equal(replies[0], "<speak>There is 1 more book with the subscription. You have 2 books: Goldilocks and the Three Bears, The Little Red Hen. Which one?</speak>");
});

test("slice 457 decision 15: the subscription line is said once a week per account, then again after 7 days; the books are always said", async () => {
  const db = fakeDb();
  const search = async (now, id = USER) => speechOf((await call(intent("SearchIntent"), { env: shelf({ STATS: db }), envelope: asUser({ session: { application: { applicationId: SKILL }, user: { userId: id } } }), services: at(now) })).json);
  assert.match(await search(T0), /^<speak>There is 1 more book with the subscription\. You have 2 books: /);
  assert.equal(db.told.get(USER), T0);
  const later = await search(T0 + 6 * DAY_MS);
  assert.doesNotMatch(later, /subscription/);
  assert.match(later, /^<speak>You have 2 books: Goldilocks and the Three Bears, The Little Red Hen\. Which one\?<\/speak>$/);
  assert.equal(db.told.get(USER), T0, "a search that did not say the line does not move the date");
  assert.match(await search(T0 + 7 * DAY_MS), /There is 1 more book with the subscription/);
  assert.equal(db.told.get(USER), T0 + 7 * DAY_MS);
  assert.match(await search(T0 + DAY_MS, "amzn1.ask.account.SOMEONEELSE"), /There is 1 more book with the subscription/, "another account is told on its own date");
  assert.deepEqual(Object.keys(Object.fromEntries(db.told)).sort(), ["amzn1.ask.account.SOMEONEELSE", USER].sort());
});

test("slice 457 decision 15: while the told table is missing, or there is no database or account, the line is said every time and nothing breaks", async () => {
  const missing = fakeDb({ missingTold: true });
  for (const extra of [{ env: shelf({ STATS: missing }), envelope: asUser() }, { env: shelf() }, { env: shelf({ STATS: fakeDb() }) }]) {
    for (let n = 0; n < 2; n++) {
      const { status, json } = await call(intent("SearchIntent"), { ...extra, services: at(T0) });
      assert.equal(status, 200);
      assert.match(speechOf(json), /There is 1 more book with the subscription\. You have 2 books/);
    }
  }
});

test("slice 457 decision 15: a failing told-table read skips the write, so one warning is logged per search, not two", async () => {
  const warnings = [];
  const original = console.warn;
  console.warn = (...args) => warnings.push(args.join(" "));
  try {
    const { status } = await call(intent("SearchIntent"), { env: shelf({ STATS: fakeDb({ missingTold: true }) }), envelope: asUser(), services: at(T0) });
    assert.equal(status, 200);
  } finally {
    console.warn = original;
  }
  assert.equal(warnings.length, 1, warnings.join("\n"));
});

test("slice 457 decision 15: no line when no book is locked; until Slice 415 every account counts as without the subscription", async () => {
  const none = speechOf((await call(intent("SearchIntent"), { env: manyBooks(3) })).json);
  assert.equal(none, "<speak>You have 3 books: Story Number 1, Story Number 2, Story Number 3. Which one?</speak>");
  assert.doesNotMatch(speechOf((await call(intent("SearchIntent"), { env: shelf(), services: at(T0) })).json), /Which story\?/);
});

test("slice 457 decision 15: counts grammar and every playable book is named, with a count that matches", async () => {
  assert.match(speechOf((await call(intent("SearchIntent"), { env: mixedBooks(3, 12) })).json), /^<speak>There are 12 more books with the subscription\. You have 3 books: /);
  const many = speechOf((await call(intent("SearchIntent"), { env: manyBooks(8) })).json);
  assert.match(many, /You have 8 books: /);
  assert.equal((many.match(/Story Number/g) ?? []).length, 8);
});

test("slice 457 decision 14: 'play it' as a book or title slot plays the offered book; with none offered it is the normal not-found", async () => {
  const offered = { session: { attributes: { offered: BOOK_C } } };
  for (const [name, slot] of [["ReadBookIntent", "book"], ["BookTitleIntent", "title"], ["ReadBookIntent", "book"]]) {
    for (const word of ["it", "that", "That One", "this one"]) {
      const { json } = await call(intent(name, { [slot]: { name: slot, value: word } }), { env: shelf(), envelope: offered });
      assert.deepEqual(tokenOfPlay(json), { book: BOOK_C, language: "en", version: 3, segmentStartMs: 0 }, `${name} ${word}`);
    }
  }
  const none = (await call(intent("ReadBookIntent", { book: { name: "book", value: "it" } }), { env: shelf() })).json;
  assert.match(speechOf(none), /I don't know that one\. Say: search\./);
});

test("slice 457 decision 14: every reply that keeps the session open carries the offered book, so a later 'yes' still plays it", async () => {
  const offered = { session: { attributes: { offered: BOOK_C } } };
  for (const name of ["AMAZON.FallbackIntent", "AMAZON.HelpIntent"]) {
    const { json } = await call(intent(name), { env: shelf(), envelope: offered });
    assert.equal(json.response.shouldEndSession, false, name);
    assert.equal(json.sessionAttributes.offered, BOOK_C, name);
  }
  const list = (await call(intent("SearchIntent"), { env: shelf(), envelope: offered })).json;
  assert.equal(list.response.shouldEndSession, false);
  assert.equal(list.sessionAttributes?.offered, undefined, "the search list drops the offer, so a following 'yes' searches again");
  const after = (await call(intent("AMAZON.YesIntent"), { env: shelf(), envelope: {} })).json;
  assert.match(speechOf(after), /You have 2 books/);
  const miss = (await call(intent("BookTitleIntent", { title: { name: "title", value: "dragons" } }), { env: shelf(), envelope: offered })).json;
  assert.equal(miss.sessionAttributes.offered, BOOK_C);
  const paid = (await call(intent("AMAZON.YesIntent"), { env: shelf(), envelope: { session: { attributes: { offered: "nope" } } } })).json;
  assert.equal(paid.sessionAttributes?.offered, undefined, "a 'yes' with an offer that no longer plays is a search, which drops it");
});

test("slice 457 decision 15: the subscription check may be async", async () => {
  const { json } = await call(intent("SearchIntent"), { env: shelf(), services: { subscribed: async () => false, ...at(T0) } });
  assert.match(speechOf(json), /You have 2 books: /);
});

function mixedBooks(free, paid) {
  const files = [];
  const add = (n, access) => files.push(...bookFiles(`7d0e5a4e0000400080000000000002${String(n).padStart(2, "0")}`, metaFor(`Story Number ${n}`, access), true));
  for (let n = 1; n <= free; n++) add(n);
  for (let n = free + 1; n <= free + paid; n++) add(n, "paid");
  return { DOWNLOADS: bucketOf(files) };
}

test("slice 457 decision 16: help, fallback and not-found are short and never name the paid book", async () => {
  const misses = [
    ["AMAZON.HelpIntent", {}, "Say a book's name, or say: search."],
    ["AMAZON.FallbackIntent", {}, "I don't know that one. Say: search."],
    ["BookTitleIntent", { title: { name: "title", value: "dragons" } }, "I don't know that one. Say: search."],
  ];
  for (const [name, slots, expected] of misses) {
    const json = (await call(intent(name, slots), { env: shelf() })).json;
    assert.equal(speechOf(json), `<speak>${expected}</speak>`, name);
    assert.ok(json.response.reprompt.outputSpeech.ssml.length < 80, `${name}: the reprompt is short`);
    assert.equal(json.response.shouldEndSession, false);
  }
});

test("slice 457 decision 15: an account with the subscription is asked for a name or a word, and no subscription line is said", async () => {
  const { json } = await call(intent("SearchIntent"), { env: shelf(), services: { subscribed: () => true, ...at(T0) } });
  assert.equal(speechOf(json), "<speak>Which story? Say a name, or a word like dragon.</speak>");
  assert.equal(json.response.shouldEndSession, false);
  assert.doesNotMatch(speechOf(json), /subscription|You have/);
});

test("slice 457 item 9: a word two books share ('mouse', a slot value with no book id) asks which book, naming both, the paid one too", async () => {
  const files = [...bookFiles(BOOK_P, metaFor("The Lion and the Mouse", "paid"), true), ...bookFiles(BOOK_A, metaFor("The Town Mouse and the Country Mouse"), true)];
  const slot = { title: { name: "title", value: "mouse", resolutions: { resolutionsPerAuthority: [{ status: { code: "ER_SUCCESS_MATCH" }, values: [{ value: { name: "mouse" } }] }] } } };
  const json = (await call(intent("BookTitleIntent", slot), { env: { DOWNLOADS: bucketOf(files) } })).json;
  assert.match(speechOf(json), /More than one book matches/);
  assert.match(speechOf(json), /Town Mouse and the Country Mouse/);
  assert.match(speechOf(json), /Lion and the Mouse/);
  assert.equal(playsOf(json).length, 0);
});

test("slice 457 decision 14: the launch never mentions favourites, even for an account that has some", async () => {
  const db = fakeDb();
  db.rows.push({ user_id: USER, book: BOOK_A });
  const speech = speechOf((await call({ type: "LaunchRequest" }, { env: shelf({ STATS: db }), envelope: asUser() })).json);
  assert.match(speech, /^<speak>Do you want to hear .*\? Or say: search\.<\/speak>$/);
  assert.doesNotMatch(speech, /favourite|shuffle|repeat/);
});

test("slice 457: a paid book is never sent as a Play while paid books are gated; it says it comes with the subscription and offers the free ones", async () => {
  for (const request of [intent("ReadBookIntent", { book: { name: "book", value: "lion and the mouse" } }), intent("BookTitleIntent", { title: { name: "title", value: "the lion" } })]) {
    const { json } = await call(request, { env: shelf() });
    assert.equal(playsOf(json).length, 0);
    assert.equal(speechOf(json), "<speak>The Lion and the Mouse comes with the subscription. Say: search.</speak>");
    assert.equal(json.response.shouldEndSession, false);
  }
});

test("slice 457: while the paid-books switch is off (the listen route answers without proof) a paid book plays like a free one", async () => {
  const { json } = await call(intent("BookTitleIntent", { title: { name: "title", value: "the lion" } }), { env: shelf(), services: SERVICES_OFF });
  assert.equal(parseToken(playsOf(json)[0].audioItem.stream.token).book, BOOK_P);
  assert.match(speechOf((await call(intent("SearchIntent"), { env: shelf(), services: SERVICES_OFF })).json), /You have 3 books.*Lion and the Mouse/);
  const broken = { paidBooksEnabled: async () => { throw new Error("config"); } };
  assert.equal(playsOf((await call(intent("BookTitleIntent", { title: { name: "title", value: "the lion" } }), { env: shelf(), services: broken })).json).length, 0, "an unreadable switch keeps paid books locked");
});

test("slice 457: no Play directive is ever built for a paid or trackless book, by any request, resume token or next/previous", async () => {
  const requests = [
    [{ type: "LaunchRequest" }, {}],
    [intent("ListBooksIntent"), {}],
    [intent("AMAZON.HelpIntent"), {}],
    [intent("AMAZON.ResumeIntent"), { envelope: playing(BOOK_P, 5000) }],
    [intent("AMAZON.ResumeIntent"), { envelope: playing(BOOK_B, 5000) }],
    [intent("AMAZON.StartOverIntent"), { envelope: playing(BOOK_P, 5000) }],
    [intent("ReadBookIntent", { book: { name: "book", value: "no track book" } }), {}],
    [intent("BookTitleIntent", { title: { name: "title", value: "lion" } }), {}],
  ];
  for (const [request, extra] of requests) {
    const { json } = await call(request, { env: shelf(), ...extra });
    assert.equal(playsOf(json).length, 0, JSON.stringify(request));
  }
  for (const direction of ["AMAZON.NextIntent", "AMAZON.PreviousIntent"]) {
    for (const from of [BOOK_A, BOOK_C]) {
      const { json } = await call(intent(direction), { env: shelf(), envelope: playing(from, 1) });
      for (const directive of playsOf(json)) assert.ok([BOOK_A, BOOK_C].includes(parseToken(directive.audioItem.stream.token).book));
    }
  }
});

test("slice 457: a bare title resolves through the slot's entity resolution (the book's id), else by what was heard", async () => {
  const resolved = { title: { name: "title", value: "whatever the ear heard", resolutions: { resolutionsPerAuthority: [{ status: { code: "ER_SUCCESS_MATCH" }, values: [{ value: { name: "The Little Red Hen", id: BOOK_C } }] }] } } };
  assert.equal(parseToken(playsOf((await call(intent("BookTitleIntent", resolved), { env: shelf() })).json)[0].audioItem.stream.token).book, BOOK_C);
  const noMatch = { title: { name: "title", value: "goldy", resolutions: { resolutionsPerAuthority: [{ status: { code: "ER_SUCCESS_NO_MATCH" } }] } } };
  assert.equal(parseToken(playsOf((await call(intent("BookTitleIntent", noMatch), { env: shelf() })).json)[0].audioItem.stream.token).book, BOOK_A);
});

test("slice 457: an ambiguous name names the books and asks which; a miss lists books and says what to say", async () => {
  const { json } = await call(intent("BookTitleIntent", { title: { name: "title", value: "number" } }), { env: manyBooks(3) });
  assert.match(speechOf(json), /More than one book matches/);
  assert.match(speechOf(json), /Story Number 1.*Story Number 2/);
  assert.equal(playsOf(json).length, 0);
  const miss = (await call(intent("BookTitleIntent", { title: { name: "title", value: "dragons" } }), { env: shelf() })).json;
  assert.match(speechOf(miss), /I don't know that one\. Say: search\./);
});

function manyBooks(count) {
  const files = [];
  for (let n = 1; n <= count; n++) files.push(...bookFiles(`7d0e5a4e0000400080000000000001${String(n).padStart(2, "0")}`, metaFor(`Story Number ${n}`), true));
  return { DOWNLOADS: bucketOf(files) };
}

test("slice 457 decision 16: help while a story plays adds the two words that need no skill name; paused or idle it does not", async () => {
  const idle = speechOf((await call(intent("AMAZON.HelpIntent"), { env: shelf() })).json);
  assert.equal(idle, "<speak>Say a book's name, or say: search.</speak>");
  const during = speechOf((await call(intent("AMAZON.HelpIntent"), { env: shelf(), envelope: playing(BOOK_A, 5000) })).json);
  assert.equal(during, "<speak>Say a book's name, or say: search. Or say: Alexa, repeat. Or: Alexa, next.</speak>");
  assert.ok(wordCount(during) <= 20);
});

test("slice 457 favourites: save the playing book, again, remove, list, and the account's id is the only thing stored", async () => {
  const db = fakeDb();
  const saved = (await call(intent("AddFavouriteIntent"), { env: shelf({ STATS: db }), envelope: { ...asUser(), ...playing(BOOK_A, 4000) } })).json;
  assert.match(speechOf(saved), /Saved to your favourites: Goldilocks and the Three Bears/);
  assert.deepEqual(db.rows, [{ user_id: USER, book: BOOK_A }]);
  const again = (await call(intent("AddFavouriteIntent"), { env: shelf({ STATS: db }), envelope: { ...asUser(), ...playing(BOOK_A, 4000) } })).json;
  assert.match(speechOf(again), /already in your favourites/);
  const named = (await call(intent("AddFavouriteIntent", { title: { name: "title", value: "red hen" } }), { env: shelf({ STATS: db }), envelope: asUser() })).json;
  assert.match(speechOf(named), /Saved to your favourites: The Little Red Hen/);
  const list = (await call(intent("ListFavouritesIntent"), { env: shelf({ STATS: db }), envelope: asUser() })).json;
  assert.match(speechOf(list), /Your favourites are Goldilocks and the Three Bears, The Little Red Hen/);
  const removed = (await call(intent("RemoveFavouriteIntent", { title: { name: "title", value: "goldy" } }), { env: shelf({ STATS: db }), envelope: asUser() })).json;
  assert.match(speechOf(removed), /Removed from your favourites: Goldilocks/);
  assert.deepEqual(db.rows, [{ user_id: USER, book: BOOK_C }]);
  assert.match(speechOf((await call(intent("RemoveFavouriteIntent", { title: { name: "title", value: "goldy" } }), { env: shelf({ STATS: db }), envelope: asUser() })).json), /is not in your favourites/);
  for (const row of db.rows) assert.deepEqual(Object.keys(row).sort(), ["book", "user_id"]);
});

test("slice 457 favourites: another account's favourites are never read, a paid book cannot be saved, and save with nothing playing asks which", async () => {
  const db = fakeDb();
  db.rows.push({ user_id: "amzn1.ask.account.OTHER", book: BOOK_C });
  assert.match(speechOf((await call(intent("ListFavouritesIntent"), { env: shelf({ STATS: db }), envelope: asUser() })).json), /no favourites yet/);
  const paid = (await call(intent("AddFavouriteIntent", { title: { name: "title", value: "lion" } }), { env: shelf({ STATS: db }), envelope: asUser() })).json;
  assert.match(speechOf(paid), /comes with the subscription/);
  assert.equal(db.rows.length, 1);
  assert.match(speechOf((await call(intent("AddFavouriteIntent"), { env: shelf({ STATS: db }), envelope: asUser() })).json), /Tell me which book to save/);
});

test("slice 457 favourites: the 51st is refused, and with no database or no account id they say they are not available", async () => {
  const db = fakeDb();
  for (let n = 0; n < 50; n++) db.rows.push({ user_id: USER, book: n.toString(16).padStart(32, "0") });
  assert.match(speechOf((await call(intent("AddFavouriteIntent"), { env: shelf({ STATS: db }), envelope: { ...asUser(), ...playing(BOOK_A, 1) } })).json), /favourites are full/);
  assert.equal(db.rows.length, 50);
  for (const extra of [{ env: shelf(), envelope: asUser() }, { env: shelf({ STATS: fakeDb() }) }]) {
    for (const name of ["AddFavouriteIntent", "PlayFavouritesIntent", "ListFavouritesIntent", "RemoveFavouriteIntent"]) {
      const { json } = await call(intent(name, { title: { name: "title", value: "red hen" } }), extra);
      assert.match(speechOf(json), /Favourites are not available/, name);
      assert.equal(playsOf(json).length, 0);
    }
  }
  const failing = { prepare: () => ({ bind: () => ({ all: async () => { throw new Error("d1"); }, run: async () => { throw new Error("d1"); } }) }) };
  assert.match(speechOf((await call(intent("PlayFavouritesIntent"), { env: shelf({ STATS: failing }), envelope: asUser() })).json), /Favourites are not available/);
  assert.equal((await call({ type: "LaunchRequest" }, { env: shelf({ STATS: failing }), envelope: asUser() })).status, 200, "a D1 failure never breaks the launch");
});

test("slice 457 favourites: play them in a row, next and previous walk them, the next one is queued behind the last, paid ones are skipped", async () => {
  const db = fakeDb();
  db.rows.push({ user_id: USER, book: BOOK_C }, { user_id: USER, book: BOOK_P }, { user_id: USER, book: BOOK_A });
  const started = (await call(intent("PlayFavouritesIntent"), { env: shelf({ STATS: db }), envelope: asUser() })).json;
  const firstToken = playsOf(started)[0].audioItem.stream.token;
  assert.deepEqual(parseToken(firstToken), { book: BOOK_C, language: "en", version: 3, segmentStartMs: 0, favourites: true });
  assert.equal(playsOf(started)[0].playBehavior, "REPLACE_ALL");
  const next = (await call(intent("AMAZON.NextIntent"), { env: shelf({ STATS: db }), envelope: { ...asUser(), context: { AudioPlayer: { token: firstToken, offsetInMilliseconds: 9 } } } })).json;
  assert.deepEqual(parseToken(playsOf(next)[0].audioItem.stream.token), { book: BOOK_A, language: "en", version: 3, segmentStartMs: 0, favourites: true });
  const back = (await call(intent("AMAZON.PreviousIntent"), { env: shelf({ STATS: db }), envelope: { ...asUser(), context: { AudioPlayer: { token: playsOf(next)[0].audioItem.stream.token, offsetInMilliseconds: 9 } } } })).json;
  assert.equal(parseToken(playsOf(back)[0].audioItem.stream.token).book, BOOK_C);
  const queued = (await call({ type: "AudioPlayer.PlaybackNearlyFinished", token: firstToken, offsetInMilliseconds: 290_000 }, { env: shelf({ STATS: db }), envelope: asUser() })).json;
  const [enqueue] = playsOf(queued);
  assert.equal(enqueue.playBehavior, "ENQUEUE");
  assert.equal(enqueue.audioItem.stream.expectedPreviousToken, firstToken);
  assert.equal(parseToken(enqueue.audioItem.stream.token).book, BOOK_A);
  assert.deepEqual(Object.keys(queued.response), ["directives"]);
  const last = (await call({ type: "AudioPlayer.PlaybackNearlyFinished", token: enqueue.audioItem.stream.token, offsetInMilliseconds: 290_000 }, { env: shelf({ STATS: db }), envelope: asUser() })).json;
  assert.match(playsOf(last)[0].audioItem.stream.url, /what-next-with-favourites\.m4a$/, "after the last favourite comes the clip (item 7)");
  const resumed = (await call(intent("AMAZON.ResumeIntent"), { env: shelf({ STATS: db }), envelope: { ...asUser(), context: { AudioPlayer: { token: firstToken, offsetInMilliseconds: 5000 } } } })).json;
  assert.equal(parseToken(playsOf(resumed)[0].audioItem.stream.token).favourites, true, "resuming a favourites book keeps it in the favourites row");
});

const nearlyFinished = (token) => ({ type: "AudioPlayer.PlaybackNearlyFinished", token, offsetInMilliseconds: 290_000 });
const clipTokenOfBook = (book) => `v1|${book}|en|3|0|c`;
const duringClip = (book, extra = {}) => ({ ...asUser(), context: { AudioPlayer: { token: clipTokenOfBook(book), offsetInMilliseconds: 4000, playerActivity: "PLAYING" } }, ...extra });

test("slice 457 item 7: a single book nearing its end queues the plain 'what next?' clip, its token names the book just played", async () => {
  const token = `v1|${BOOK_A}|en|3|0`;
  const { json } = await call(nearlyFinished(token), { env: shelf(), envelope: asUser() });
  assert.deepEqual(Object.keys(json.response), ["directives"], "directives only, no speech (the skill cannot speak after audio)");
  const [clip] = playsOf(json);
  assert.equal(clip.playBehavior, "ENQUEUE");
  assert.equal(clip.audioItem.stream.expectedPreviousToken, token);
  assert.equal(clip.audioItem.stream.url, "https://dinoreader.sirous.uk/dino/alexa-clips/what-next.m4a");
  assert.equal(clip.audioItem.stream.offsetInMilliseconds, 0);
  assert.deepEqual(parseToken(clip.audioItem.stream.token), { book: BOOK_A, language: "en", version: 3, segmentStartMs: 0, clip: true });
});

test("slice 457 item 7: an account with favourites gets the clip that offers a favourite; a database failure falls back to the plain clip", async () => {
  const db = fakeDb();
  db.rows.push({ user_id: USER, book: BOOK_C });
  const withFavourites = (await call(nearlyFinished(`v1|${BOOK_A}|en|3|0`), { env: shelf({ STATS: db }), envelope: asUser() })).json;
  assert.match(playsOf(withFavourites)[0].audioItem.stream.url, /\/what-next-with-favourites\.m4a$/);
  const unplayable = fakeDb();
  unplayable.rows.push({ user_id: USER, book: BOOK_B }); // saved, but its track does not play: no "shuffle" into silence
  const silent = (await call(nearlyFinished(`v1|${BOOK_A}|en|3|0`), { env: shelf({ STATS: unplayable }), envelope: asUser() })).json;
  assert.match(playsOf(silent)[0].audioItem.stream.url, /\/what-next\.m4a$/, "review: the clip follows the playable favourites, not the saved ids");
  const failing = { prepare: () => ({ bind: () => ({ all: async () => { throw new Error("d1"); } }) }) };
  const broken = (await call(nearlyFinished(`v1|${BOOK_A}|en|3|0`), { env: shelf({ STATS: failing }), envelope: asUser() })).json;
  assert.match(playsOf(broken)[0].audioItem.stream.url, /\/what-next\.m4a$/);
});

test("slice 457 item 7: favourites in a row keep going to the end, then the clip; the clip itself queues nothing and counts no listen", async () => {
  const db = fakeDb();
  db.rows.push({ user_id: USER, book: BOOK_C }, { user_id: USER, book: BOOK_A });
  const firstToken = `v1|${BOOK_C}|en|3|0|f`;
  const second = playsOf((await call(nearlyFinished(firstToken), { env: shelf({ STATS: db }), envelope: asUser() })).json)[0];
  assert.equal(second.audioItem.stream.url.includes("alexa-clips"), false, "the next favourite, not the clip, while favourites remain");
  const last = playsOf((await call(nearlyFinished(second.audioItem.stream.token), { env: shelf({ STATS: db }), envelope: asUser() })).json)[0];
  assert.match(last.audioItem.stream.url, /\/what-next-with-favourites\.m4a$/);
  assert.equal(last.audioItem.stream.expectedPreviousToken, second.audioItem.stream.token);
  assert.equal(parseToken(last.audioItem.stream.token).book, BOOK_A);
  const afterClip = (await call(nearlyFinished(last.audioItem.stream.token), { env: shelf({ STATS: db }), envelope: asUser() })).json;
  assert.deepEqual(afterClip, { version: "1.0", response: {} });
  const stopped = await call({ type: "AudioPlayer.PlaybackStopped", token: clipTokenOfBook(BOOK_A), offsetInMilliseconds: 4000 });
  assert.equal(stopped.dataset.points.length, 0, "the clip is no listen of the book");
});

test("slice 457 item 7: 'start over' during or after the clip replays the book just played, from the start, as a plain book", async () => {
  for (const intentName of ["AMAZON.StartOverIntent", "AMAZON.ResumeIntent"]) {
    const { json } = await call(intent(intentName), { env: shelf(), envelope: duringClip(BOOK_C) });
    assert.deepEqual(parseToken(playsOf(json)[0].audioItem.stream.token), { book: BOOK_C, language: "en", version: 3, segmentStartMs: 0 }, intentName);
    assert.equal(playsOf(json)[0].audioItem.stream.offsetInMilliseconds, 0);
  }
  const command = await call({ type: "PlaybackController.PlayCommandIssued" }, { env: shelf(), envelope: duringClip(BOOK_C) });
  assert.equal(parseToken(playsOf(command.json)[0].audioItem.stream.token).clip, undefined);
});

test("slice 457 item 7: 'next' from the clip plays a playable book other than the one just played, wrapping round; one book alone has no other", async () => {
  const fromA = (await call(intent("AMAZON.NextIntent"), { env: shelf(), envelope: duringClip(BOOK_A) })).json;
  assert.equal(parseToken(playsOf(fromA)[0].audioItem.stream.token).book, BOOK_C);
  const fromC = (await call(intent("AMAZON.NextIntent"), { env: shelf(), envelope: duringClip(BOOK_C) })).json;
  assert.equal(parseToken(playsOf(fromC)[0].audioItem.stream.token).book, BOOK_A, "the last book wraps to the first, never the same book");
  const back = (await call(intent("AMAZON.PreviousIntent"), { env: shelf(), envelope: duringClip(BOOK_A) })).json;
  assert.equal(parseToken(playsOf(back)[0].audioItem.stream.token).book, BOOK_C);
  const single = { DOWNLOADS: bucketOf(bookFiles(BOOK_A, metaFor("Goldilocks and the Three Bears"), true)) };
  const alone = (await call(intent("AMAZON.NextIntent"), { env: single, envelope: duringClip(BOOK_A) })).json;
  assert.equal(playsOf(alone).length, 0);
  assert.match(speechOf(alone), /last book/);
});

test("slice 457 item 7: 'play a favourite' plays one random favourite (not the one just played), alone, and says so when there are none", async () => {
  const db = fakeDb();
  assert.match(speechOf((await call(intent("PlayFavouriteIntent"), { env: shelf({ STATS: db }), envelope: asUser() })).json), /no favourites yet/);
  db.rows.push({ user_id: USER, book: BOOK_C }, { user_id: USER, book: BOOK_P }, { user_id: USER, book: BOOK_A });
  const pick = async (random, envelope = asUser()) => parseToken(playsOf((await call(intent("PlayFavouriteIntent"), { env: shelf({ STATS: db }), envelope, services: { random: () => random } })).json)[0].audioItem.stream.token);
  assert.equal((await pick(0)).book, BOOK_C);
  assert.equal((await pick(0.99)).book, BOOK_A, "the paid favourite is never a choice");
  assert.equal((await pick(0)).favourites, undefined, "one book, so the clip follows it");
  assert.equal((await pick(0, duringClip(BOOK_C))).book, BOOK_A, "not the book just played, when there is another");
  const only = fakeDb();
  only.rows.push({ user_id: USER, book: BOOK_C });
  const again = (await call(intent("PlayFavouriteIntent"), { env: shelf({ STATS: only }), envelope: duringClip(BOOK_C) })).json;
  assert.equal(parseToken(playsOf(again)[0].audioItem.stream.token).book, BOOK_C, "a single favourite plays again");
});

test("slice 457 item 7: both clips are served with the site, and both locales take 'for a favourite' and 'play a favourite'", async () => {
  const { existsSync, readFileSync } = await import("node:fs");
  for (const file of ["what-next.m4a", "what-next-with-favourites.m4a"]) assert.ok(existsSync(new URL(`../site/dino/alexa-clips/${file}`, import.meta.url)), file);
  for (const locale of ["en-GB", "en-US"]) {
    const model = JSON.parse(readFileSync(new URL(`../alexa/skill-package/interactionModels/custom/${locale}.json`, import.meta.url), "utf8"));
    const samples = model.interactionModel.languageModel.intents.find((entry) => entry.name === "PlayFavouriteIntent").samples;
    for (const sample of ["for a favourite", "play a favourite"]) assert.ok(samples.includes(sample), `${locale} ${sample}`);
  }
});

test("slice 457 favourites: none saved, or none that plays, is said plainly", async () => {
  const db = fakeDb();
  assert.match(speechOf((await call(intent("PlayFavouritesIntent"), { env: shelf({ STATS: db }), envelope: asUser() })).json), /no favourites yet/);
  db.rows.push({ user_id: USER, book: BOOK_P });
  const { json } = await call(intent("PlayFavouritesIntent"), { env: shelf({ STATS: db }), envelope: asUser() });
  assert.match(speechOf(json), /None of your favourites can play/);
  assert.equal(playsOf(json).length, 0);
});

test("slice 457 logs: intent and outcome only, never what was said, and no account id", async () => {
  const lines = [];
  const original = console.log;
  console.log = (...parts) => lines.push(parts.join(" "));
  try {
    await call(intent("ReadBookIntent", { book: { name: "book", value: "purple-secret-words" } }), { env: shelf(), envelope: asUser() });
    await call(intent("BookTitleIntent", { title: { name: "title", value: "goldy" } }), { env: shelf(), envelope: asUser() });
    await call(intent("BookTitleIntent", { title: { name: "title", value: "the lion" } }), { env: shelf(), envelope: asUser() });
    await call({ type: "LaunchRequest" }, { env: shelf(), envelope: asUser(), services: { random: () => 0 } });
    await call(intent("AddFavouriteIntent"), { env: shelf({ STATS: fakeDb() }), envelope: { ...asUser(), ...playing(BOOK_A, 1) } });
  } finally {
    console.log = original;
  }
  assert.deepEqual(lines, [
    "alexa intent=ReadBookIntent outcome=not_found",
    `alexa intent=BookTitleIntent outcome=play:${BOOK_A}`,
    `alexa intent=BookTitleIntent outcome=paid:${BOOK_P}`,
    `alexa intent=Launch outcome=offer:${BOOK_A}`,
    `alexa intent=AddFavouriteIntent outcome=favourite_added:${BOOK_A}`,
  ]);
  assert.ok(lines.every((line) => !/purple|TESTUSER|goldy/.test(line)));
});

test("slice 457: a skill package check of the new intents: both locales carry them, with the title slot type and one value per book", async () => {
  const { readFileSync } = await import("node:fs");
  for (const locale of ["en-GB", "en-US"]) {
    const model = JSON.parse(readFileSync(new URL(`../alexa/skill-package/interactionModels/custom/${locale}.json`, import.meta.url), "utf8")).interactionModel.languageModel;
    const names = model.intents.map((item) => item.name);
    for (const wanted of ["BookTitleIntent", "ListBooksIntent", "MoreBooksIntent", "AddFavouriteIntent", "PlayFavouritesIntent", "ListFavouritesIntent", "RemoveFavouriteIntent"]) assert.ok(names.includes(wanted), `${locale} ${wanted}`);
    for (const name of ["BookTitleIntent", "AddFavouriteIntent", "RemoveFavouriteIntent"]) assert.deepEqual(model.intents.find((item) => item.name === name).slots, [{ name: "title", type: "DINO_BOOK_TITLE" }]);
    assert.ok(model.types.some((type) => type.name === "DINO_BOOK_TITLE" && type.values.length > 0));
  }
});

test("slice 457 decisions 13-15: both models are called 'dino books', carry yes, no and one-word search, and no sample is said by two intents; the manifest says Dino Books", async () => {
  const { readFileSync } = await import("node:fs");
  const read = (path) => JSON.parse(readFileSync(new URL(`../alexa/skill-package/${path}`, import.meta.url), "utf8"));
  for (const locale of ["en-GB", "en-US"]) {
    const model = read(`interactionModels/custom/${locale}.json`).interactionModel.languageModel;
    assert.equal(model.invocationName, "dino books", locale);
    const byName = Object.fromEntries(model.intents.map((item) => [item.name, item]));
    assert.ok(byName["AMAZON.YesIntent"] && byName["AMAZON.NoIntent"] && byName.SearchIntent, locale);
    for (const word of ["search", "find", "find a book", "what books", "other books"]) assert.ok(byName.SearchIntent.samples.includes(word), `${locale} ${word}`);
    const owners = new Map();
    for (const item of model.intents) {
      for (const sample of item.samples ?? []) {
        const key = sample.toLowerCase();
        assert.ok(!owners.has(key) || owners.get(key) === item.name, `${locale}: "${sample}" is in ${owners.get(key)} and ${item.name}`);
        owners.set(key, item.name);
      }
    }
  }
  const manifest = JSON.stringify(read("skill.json"));
  assert.doesNotMatch(manifest, /Bedtime Stories/i);
  for (const locale of ["en-GB", "en-US"]) {
    const entry = read("skill.json").manifest.publishingInformation.locales[locale];
    assert.equal(entry.name, "Dino Books");
    assert.ok(entry.examplePhrases.includes("Alexa, open Dino Books"));
  }
});

// ---- Slice 457 review fixes (high pass) ----

test("slice 457 review 1: a whole title still finds its book when extra words came with it", () => {
  const tracks = [trackFor(BOOK_A, "Goldilocks and the Three Bears", 600), trackFor(BOOK_B, "The Lion and the Mouse", 300), trackFor(BOOK_C, "The Little Red Hen", 120)];
  for (const spoken of ["goldilocks and the three bears from dino", "read goldilocks and the three bears from dino please", "goldilocks and the three bears bedtime story"]) {
    assert.deepEqual(findBooks(tracks, spoken).map((track) => track.storyId), [BOOK_A], spoken);
  }
  assert.deepEqual(findBooks(tracks, "the lion and the mouse bedtime story").map((track) => track.storyId), [BOOK_B]);
  assert.deepEqual(findBooks(tracks, "bedtime story from dino"), [], "extra words alone find nothing");
});

test("slice 457 review 2: a D1 failure is logged by error name only, never the account, a book or the SQL", async () => {
  const lines = [];
  const original = console.warn;
  console.warn = (...args) => lines.push(args.join(" "));
  try {
    const broken = { prepare: () => { throw new Error(`D1_ERROR: no such table: alexa_favourites ${USER}`); } };
    const { json } = await call(intent("ListFavouritesIntent"), { env: shelf({ STATS: broken }), envelope: asUser() });
    assert.match(speechOf(json), /not available/);
  } finally {
    console.warn = original;
  }
  assert.deepEqual(lines, ["alexa favourites d1 error=Error table_missing=true"]);
});

test("slice 457 review 4: saving, listing or removing a favourite while a story plays carries on playing it from where it was", async () => {
  const db = fakeDb();
  db.rows.push({ user_id: USER, book: BOOK_C });
  const requests = [intent("AddFavouriteIntent"), intent("ListFavouritesIntent"), intent("RemoveFavouriteIntent", { title: { name: "title", value: "little red hen" } })];
  for (const request of requests) {
    const { json } = await call(request, { env: shelf({ STATS: db }), envelope: { ...asUser(), ...playing(BOOK_A, 5000) } });
    const [play] = playsOf(json);
    assert.ok(play, request.intent.name);
    assert.equal(parseToken(play.audioItem.stream.token).book, BOOK_A);
    assert.equal(play.audioItem.stream.offsetInMilliseconds, 5000);
    assert.equal(json.response.shouldEndSession, true);
  }
  const stopped = { context: { AudioPlayer: { ...playing(BOOK_A, 5000).context.AudioPlayer, playerActivity: "STOPPED" } } };
  assert.equal(playsOf((await call(intent("AddFavouriteIntent"), { env: shelf({ STATS: db }), envelope: { ...asUser(), ...stopped } })).json).length, 0, "a story the child stopped is not restarted");
  assert.equal(playsOf((await call(intent("ListFavouritesIntent"), { env: shelf({ STATS: db }), envelope: asUser() })).json).length, 0, "nothing playing: speech only");
});

test("slice 457 review 5: an ambiguous save or remove asks which, and the answer finishes that action instead of playing the book", async () => {
  const db = fakeDb();
  const store = () => manyBooks(3);
  for (const [name, pending] of [["AddFavouriteIntent", "save"], ["RemoveFavouriteIntent", "remove"]]) {
    const asked = (await call(intent(name, { title: { name: "title", value: "number" } }), { env: { ...store(), STATS: db }, envelope: asUser() })).json;
    assert.match(speechOf(asked), /More than one book matches/);
    assert.equal(asked.sessionAttributes.pending, pending);
    assert.equal(playsOf(asked).length, 0);
    const answered = (await call(intent("BookTitleIntent", { title: { name: "title", value: "story number 2" } }), { env: { ...store(), STATS: db }, envelope: asUser({ session: { application: { applicationId: SKILL }, user: { userId: USER }, attributes: asked.sessionAttributes } }) })).json;
    assert.equal(playsOf(answered).length, 0, `${name}: never plays`);
    assert.match(speechOf(answered), pending === "save" ? /Saved to your favourites: Story Number 2/ : /Removed from your favourites: Story Number 2/);
  }
  assert.equal(db.rows.length, 0, "saved by the first answer, removed by the second");
  const plain = (await call(intent("BookTitleIntent", { title: { name: "title", value: "story number 2" } }), { env: { ...store(), STATS: db }, envelope: asUser() })).json;
  assert.equal(playsOf(plain).length, 1, "with nothing pending a bare title still plays");
});

test("slice 457 review 3: a short name two books share is not resolved to one of them; the matcher asks which", async () => {
  const mice = { DOWNLOADS: bucketOf([...bookFiles(BOOK_A, metaFor("The Town Mouse and the Country Mouse"), true), ...bookFiles(BOOK_C, metaFor("The Lion and the Mouse"), true)]) };
  const { json } = await call(intent("BookTitleIntent", { title: { name: "title", value: "the mouse" } }), { env: mice });
  assert.match(speechOf(json), /More than one book matches/);
  assert.equal(playsOf(json).length, 0);
});

test("slice 457 review 8 and 9: the list page and pending attributes keep their names, and one objectAt serves both files", async () => {
  const { OFFERED_ATTRIBUTE, PENDING_ATTRIBUTE } = await import("./alexa.ts");
  const { objectAt } = await import("./object-path.ts");
  assert.equal(OFFERED_ATTRIBUTE, "offered");
  assert.equal(PENDING_ATTRIBUTE, "pending");
  assert.equal(objectAt({ a: { b: { c: 1 } } }, "a", "b", "c"), 1);
  assert.equal(objectAt({ a: 1 }, "a", "b"), undefined);
});

const tokenOfPlay = (json) => parseToken(playsOf(json)[0].audioItem.stream.token);

test("slice 457 decision 12: 'repeat' and 'loop' replay the book just played after the clip, and restart a book that is playing", async () => {
  for (const intentName of ["AMAZON.RepeatIntent", "AMAZON.LoopOnIntent", "AMAZON.StartOverIntent"]) {
    const after = (await call(intent(intentName), { env: shelf(), envelope: duringClip(BOOK_C) })).json;
    assert.deepEqual(tokenOfPlay(after), { book: BOOK_C, language: "en", version: 3, segmentStartMs: 0 }, intentName);
    const during = (await call(intent(intentName), { env: shelf(), envelope: { ...asUser(), ...playing(BOOK_A, 70_000) } })).json;
    assert.equal(playsOf(during)[0].audioItem.stream.offsetInMilliseconds, 0, intentName);
    assert.equal(tokenOfPlay(during).book, BOOK_A, intentName);
  }
});

test("slice 457 decision 12: 'shuffle' plays a random favourite, else a new playable book, never the one just played", async () => {
  const db = fakeDb();
  db.rows.push({ user_id: USER, book: BOOK_C }, { user_id: USER, book: BOOK_A });
  const favourite = await call(intent("AMAZON.ShuffleOnIntent"), { env: shelf({ STATS: db }), envelope: duringClip(BOOK_C), services: { random: () => 0 } });
  assert.equal(tokenOfPlay(favourite.json).book, BOOK_A, "a favourite other than the book just played");
  for (const env of [shelf({ STATS: fakeDb() }), shelf()]) {
    const fresh = (await call(intent("AMAZON.ShuffleOnIntent"), { env, envelope: duringClip(BOOK_A) })).json;
    assert.equal(tokenOfPlay(fresh).book, BOOK_C, "no favourites: a new playable book");
    assert.match(speechOf((await call(intent("AMAZON.ShuffleOnIntent"), { env, envelope: asUser() })).json), /nothing to continue/, "nothing played yet");
  }
});

test("slice 457 decision 12: 'loop off' and 'shuffle off' say nothing and leave a playing story playing", async () => {
  for (const intentName of ["AMAZON.LoopOffIntent", "AMAZON.ShuffleOffIntent"]) {
    const idle = (await call(intent(intentName), { env: shelf(), envelope: asUser() })).json;
    assert.equal(playsOf(idle).length, 0, intentName);
    assert.equal(idle.response.outputSpeech, undefined, intentName);
    const going = (await call(intent(intentName), { env: shelf(), envelope: { ...asUser(), ...playing(BOOK_A, 5000) } })).json;
    assert.equal(playsOf(going)[0].audioItem.stream.offsetInMilliseconds, 5000, intentName);
  }
});

test("slice 457 decision 12: both models carry the audio words, and no bare 'a favourite' sample", async () => {
  const { readFileSync } = await import("node:fs");
  for (const locale of ["en-GB", "en-US"]) {
    const intents = JSON.parse(readFileSync(new URL(`../alexa/skill-package/interactionModels/custom/${locale}.json`, import.meta.url), "utf8")).interactionModel.languageModel.intents;
    for (const wanted of ["Repeat", "LoopOn", "LoopOff", "ShuffleOn", "ShuffleOff"]) assert.ok(intents.some((entry) => entry.name === `AMAZON.${wanted}Intent`), `${locale} ${wanted}`);
    const samples = intents.find((entry) => entry.name === "PlayFavouriteIntent").samples;
    for (const bare of ["a favourite", "a favorite"]) assert.equal(samples.includes(bare), false, `${locale} ${bare}`);
    assert.ok(samples.includes("play a favourite"));
  }
});

test("slice 457 review 1: a favourites answer while the story is paused stays speech only", async () => {
  const paused = { context: { AudioPlayer: { ...playing(BOOK_A, 5000).context.AudioPlayer, playerActivity: "PAUSED" } } };
  const { json } = await call(intent("ListFavouritesIntent"), { env: shelf({ STATS: fakeDb() }), envelope: { ...asUser(), ...paused } });
  assert.equal(playsOf(json).length, 0);
  assert.ok(json.response.outputSpeech);
});

test("slice 457 review 2: the clip's own end reads no catalogue, a book's end still does", async () => {
  let lists = 0;
  const base = shelf();
  const counting = { ...base, DOWNLOADS: { ...base.DOWNLOADS, list: async (options) => { lists += 1; return base.DOWNLOADS.list(options); } } };
  await call({ type: "AudioPlayer.PlaybackFinished", token: clipTokenOfBook(BOOK_A), offsetInMilliseconds: 9000 }, { env: counting });
  await call({ type: "AudioPlayer.PlaybackStopped", token: clipTokenOfBook(BOOK_A), offsetInMilliseconds: 4000 }, { env: counting });
  assert.equal(lists, 0);
  await call({ type: "AudioPlayer.PlaybackFinished", token: `v1|${BOOK_A}|en|3|0`, offsetInMilliseconds: 9000 }, { env: counting });
  assert.ok(lists > 0);
});

test("slice 457 review 4: the clip path is served by the assets layer on both hosts (no run_worker_first pattern catches it)", async () => {
  const { readFileSync } = await import("node:fs");
  const path = `${CLIP_FOLDER}/what-next.m4a`;
  for (const file of ["wrangler.jsonc", "wrangler.workers-dev.jsonc"]) {
    const text = readFileSync(new URL(`../${file}`, import.meta.url), "utf8").split("\n").filter((line) => !line.trim().startsWith("//")).join("\n");
    const patterns = JSON.parse(text.replace(/,(\s*[}\]])/g, "$1")).assets.run_worker_first;
    const caught = patterns.filter((pattern) => (pattern.endsWith("*") ? path.startsWith(pattern.slice(0, -1)) : pattern === path));
    assert.deepEqual(caught, [], file);
  }
});
