// Run: node --test worker/stories.test.mjs (node 22.18+/24 strip the types of stories.ts).
import test from "node:test";
import assert from "node:assert/strict";

import { parseMeta, parseTitles, toListItem } from "./stories.ts";

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
