// Run: node --test worker/stories.test.mjs (node 22.18+/24 strip the types of stories.ts).
import test from "node:test";
import assert from "node:assert/strict";

import { GateAnswer, createPaidGate } from "./entitlements/gate.ts";
import { serveObject } from "./downloads.ts";
import { badRequest, compareEntries, handleStories, parseCreditsByLanguage, parseMeta, parseTags, parseTitles, toListItem } from "./stories.ts";

const baseMeta = { schemaVersion: 1, title: "The Town Mouse", language: "en" };

test("titles pass through trimmed, with codes lower-cased", () => {
  const titles = parseTitles({ fa: "  موش شهری ", "PT-br": "O Rato", de: "Die Stadtmaus" });
  assert.deepEqual(titles, { fa: "موش شهری", "pt-br": "O Rato", de: "Die Stadtmaus" });
});

test("bad codes, empty and over-long titles are dropped", () => {
  const titles = parseTitles({ fa: "x", "": "y", "not a code": "z", de: "   ", fr: "a".repeat(201) });
  assert.deepEqual(titles, { fa: "x" });
});

test("nothing valid, or not an object, is null", () => {
  assert.equal(parseTitles({ de: "" }), null);
  assert.equal(parseTitles(["a"]), null);
  assert.equal(parseTitles("fa"), null);
  assert.equal(parseTitles(undefined), null);
});

test("the count is capped", () => {
  const many = Object.fromEntries(Array.from({ length: 30 }, (_, i) => [`a${String.fromCharCode(97 + (i % 26))}`, `t${i}`]));
  assert.ok(Object.keys(parseTitles(many)).length <= 20);
});

test("a meta without titles still parses and lists titles as null", () => {
  const meta = parseMeta(baseMeta);
  assert.equal(meta.titles, null);
  const item = toListItem({ id: "7d0e5a4e-0000-4000-8000-000000000001", meta, version: 1, hasCover: false }, "");
  assert.equal(item.titles, null);
});

test("titles reach the list item", () => {
  const meta = parseMeta({ ...baseMeta, titles: { fa: "موش شهری" } });
  const item = toListItem({ id: "7d0e5a4e-0000-4000-8000-000000000001", meta, version: 2, hasCover: true }, "/dino");
  assert.deepEqual(item.titles, { fa: "موش شهری" });
});

test("credit lines pass through trimmed, with bad codes, blanks and over-200 lines dropped", () => {
  const credits = parseCreditsByLanguage({ DE: "  Nacherzählt · frei zum Teilen ", "": "y", fr: "   ", ja: "a".repeat(201), fa: "a".repeat(200) });
  assert.deepEqual(credits, { de: "Nacherzählt · frei zum Teilen", fa: "a".repeat(200) });
  assert.equal(parseCreditsByLanguage({ de: "" }), null);
  assert.equal(parseCreditsByLanguage(["a"]), null);
  assert.equal(parseCreditsByLanguage(undefined), null);
});

test("credit lines reach the list item, and a meta without them lists null", () => {
  const entry = (meta) => ({ id: "7d0e5a4e-0000-4000-8000-000000000001", meta, version: 2, hasCover: false });
  const withCredits = parseMeta({ ...baseMeta, credits: "Retold", creditsByLanguage: { fa: "بازگویی" } });
  assert.deepEqual(toListItem(entry(withCredits), "").creditsByLanguage, { fa: "بازگویی" });
  assert.equal(toListItem(entry(parseMeta(baseMeta)), "").creditsByLanguage, null);
});

// Slice 260: store tags and package size.
const storyEntry = (extra) => ({ id: "7d0e5a4e-0000-4000-8000-000000000001", meta: parseMeta({ ...baseMeta, ...extra }), version: 2, hasCover: false });

test("tags keep only the four known names, case-sensitive, deduplicated, in list order", () => {
  assert.deepEqual(parseTags(["New", "Hot", "Hot", "popular", "Sale", 7, "Popular"]), ["Hot", "Popular", "New"]);
  assert.deepEqual(parseTags("Hot"), []);
  assert.deepEqual(parseTags(undefined), []);
});

test("tags and bytes reach the list item; an older meta lists no tags and null bytes", () => {
  const item = toListItem(storyEntry({ tags: ["New", "Hot"], bytes: 9_000_000 }), "");
  assert.deepEqual(item.tags, ["Hot", "New"]);
  assert.equal(item.bytes, 9_000_000);
  const plain = toListItem(storyEntry({}), "");
  assert.deepEqual(plain.tags, []);
  assert.equal(plain.bytes, null);
});

test("bytes that are not a positive whole number are dropped", () => {
  for (const bad of [0, -5, 1.5, "9", null, true]) assert.equal(toListItem(storyEntry({ bytes: bad }), "").bytes, null);
});

// Slice 357: an error answer is its status alone; only a 400 speaks.
const PAID_ID = "7d0e5a4e000040008000000000000001";
const PAID_PRODUCT = "uk.sirous.dinoreader.book.goldilocks";
function oneBookBucket() {
  const folder = `dinoreader/stories/${PAID_ID}/`;
  const meta = { schemaVersion: 1, title: "Goldilocks", language: "en", version: 1, access: "paid", productId: PAID_PRODUCT };
  const objects = new Map([[`${folder}meta.json`, Buffer.from(JSON.stringify(meta))], [`${folder}story-v1.dino`, Buffer.from("pkg")]]);
  return {
    async list({ prefix }) { return { objects: [...objects.keys()].filter((key) => key.startsWith(prefix)).map((key) => ({ key })), truncated: false }; },
    async head(key) { const body = objects.get(key); return body ? { size: body.length, httpEtag: '"e"' } : null; },
    async get(key) {
      const body = objects.get(key);
      return body ? { size: body.length, httpEtag: '"e"', body: new Response(body).body, json: async () => JSON.parse(body.toString()) } : null;
    },
  };
}
const callStore = (path, gate, bucket = oneBookBucket(), headers = {}) => handleStories(new Request(`https://x.test${path}`, { headers }), { DOWNLOADS: bucket }, "", serveObject, gate);
const DOWNLOAD = `/api/stories/${PAID_ID}/download?v=1`;

test("a refused paid file is 402, 403 or 429 with no body; a store that cannot be asked is 503, not 403", async () => {
  const expected = [[GateAnswer.NoProof, 402], [GateAnswer.BadProof, 403], [GateAnswer.RateLimited, 429], [GateAnswer.CannotVerify, 503]];
  for (const [answer, status] of expected) {
    const response = await callStore(DOWNLOAD, async () => answer);
    assert.equal(response.status, status, answer);
    assert.equal(await response.text(), "", `${answer} has no body`);
  }
});

test("a missing store secret is 503 with no body, never owned", async () => {
  const gate = createPaidGate({ enabled: async () => true, env: {}, fetcher: async () => new Response("{}") });
  const noProof = await callStore(DOWNLOAD, gate);
  const proofed = await callStore(DOWNLOAD, gate, oneBookBucket(), { "X-Purchase-Proof": `android:${PAID_PRODUCT}:t` });
  assert.equal(noProof.status, 402);
  assert.equal(proofed.status, 503);
  assert.equal(await proofed.text(), "");
});

test("an unknown book, a bad id and an unreadable bucket answer 404 or 503 with no body", async () => {
  const missing = await callStore(`/api/stories/${"0".repeat(32)}/download?v=1`, undefined);
  assert.equal(missing.status, 404);
  assert.equal(await missing.text(), "");
  const badId = await callStore("/api/stories/not-an-id/download", undefined);
  assert.equal(badId.status, 404);
  assert.equal(await badId.text(), "");
  const broken = { async list() { throw new Error("r2 down"); }, async head() { return null; }, async get() { return null; } };
  const down = await callStore("/api/stories", undefined, broken);
  assert.equal(down.status, 503);
  assert.equal(await down.text(), "");
});

test("a 429 for the Windows ticket and a 404 for none have no body", async () => {
  const call = (ticket) => handleStories(new Request("https://x.test/api/stories/windows-ticket"), { DOWNLOADS: oneBookBucket() }, "", serveObject, undefined, ticket);
  const limited = await call(async () => ({ limited: true }));
  assert.equal(limited.status, 429);
  assert.equal(await limited.text(), "");
  const none = await call(async () => ({ none: true }));
  assert.equal(none.status, 404);
  assert.equal(await none.text(), "");
});

test("a 400 carries the one message, in English, as JSON", async () => {
  const response = badRequest("The book id is not valid.");
  assert.equal(response.status, 400);
  assert.deepEqual(await response.json(), { message: "The book id is not valid." });
});

test("tagged books list first, then newest first by added, then by title", () => {
  const a = { ...storyEntry({ title: "A", added: "2026-01-01" }) };
  const b = { ...storyEntry({ title: "B", added: "2026-03-01" }) };
  const c = { ...storyEntry({ title: "C", added: "2026-02-01", tags: ["Recommended"] }) };
  const d = { ...storyEntry({ title: "D", added: "2025-12-01", tags: ["New"] }) };
  assert.deepEqual([a, b, c, d].sort(compareEntries).map((e) => e.meta.title), ["C", "D", "B", "A"]);
});
