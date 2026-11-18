// Run: node --test "tools/dino/*.test.mjs" (Slice 410: the public-name check).
import test from "node:test";
import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { findHits, hitsIn, missingNameProblems, publicName, stringLiterals, submissionLines } from "./check-public-name.mjs";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const whole = (text) => [{ text, line: 1 }];

test("the public name is read from tools/dino/public-name.json", () => {
  assert.equal(publicName(root), "Dino: Kids Books & Stories");
});

test("string literals count, comments do not", () => {
  const source = '// Dino Reader here\nconst a = "Dino Reader";\n/* Dino Reader\n */ const b = `Dino Book Reader`;\nconst u = "https://x.test"; // Dino Reader\n';
  const literals = stringLiterals(source);
  assert.deepEqual(hitsIn(literals, "f.ts"), ['f.ts:2: "Dino Reader"', 'f.ts:4: "Dino Book Reader"']);
  assert.equal(literals.length, 3);
});

test("a regex literal holding a quote does not blind the scanner", () => {
  const literals = stringLiterals('const r = /"/g;\nconst a = "Dino Reader";\nconst s = text.replace(/[\'"]/g, "");\n');
  assert.deepEqual(hitsIn(literals, "f.ts"), ['f.ts:2: "Dino Reader"']);
});

test("a nested template literal is read, its expression strings too", () => {
  const literals = stringLiterals("const t = `a ${ cond ? `inner ${\"Dino Reader\"}` : 'x' } b`;\n");
  assert.equal(hitsIn(literals, "f.ts").length, 1);
});

test("only the Submission text and Store text sections of a slice are checked", () => {
  const md = "# Slice\nDino Reader in prose\n## Submission text\n- Dino Reader\n## Design\nDino Reader again\n### Store text\nx\n";
  assert.deepEqual(hitsIn(submissionLines(md), "s.md"), ['s.md:4: "Dino Reader"']);
});

test("the old name is found in any case, with any spacing and across a line break", () => {
  for (const text of ["dino reader", "DINO READER", "Dino  Reader", "Dino\nReader", "Dino\n   Reader", "Dino&nbsp;Reader"]) {
    assert.equal(hitsIn(whole(text), "f").length, 1, JSON.stringify(text));
  }
  assert.deepEqual(hitsIn(whole("a\nDino\nReader"), "f"), ['f:2: "Dino Reader"']);
});

test("the old name Dino Book Reader is a hit in any case, spacing and across a line break", () => {
  for (const text of ["Dino Book Reader", "dino book\nreader", "DINO  BOOK  READER", "Dino&nbsp;Book&nbsp;Reader"]) {
    assert.deepEqual(hitsIn(whole(text), "f").map((h) => h.replace(/^f:\d+: /, "")), ['"Dino Book Reader"'], JSON.stringify(text));
  }
});

test("the code names DinoReader / dinoreader.sirous.uk and the new name are not hits", () => {
  for (const text of ["DinoReader.App", "dinoreader.sirous.uk", "dino-reader/", "Dino: Kids Books & Stories", "Dino Bedtime Stories"]) {
    assert.deepEqual(hitsIn(whole(text), "f"), [], text);
  }
});

test("the real name is in the Sonos album constants and the privacy policy", () => {
  assert.deepEqual(missingNameProblems(root, publicName(root)), []);
  assert.equal(missingNameProblems(root, "Some Other Name").length, 3);
});

test("the English store-listing files are scanned, the language files wait for Slice 413", () => {
  const dir = mkdtempSync(join(tmpdir(), "public-name-"));
  try {
    for (const path of ["docs/store-listings/dino", "tools/dino"]) mkdirSync(join(dir, path), { recursive: true });
    writeFileSync(join(dir, "tools/dino/public-name.json"), JSON.stringify({ publicName: "X" }));
    writeFileSync(join(dir, "docs/store-listings/dino/play.md"), "Title: Dino Book Reader\n");
    writeFileSync(join(dir, "docs/store-listings/dino/de.md"), "Titel: Dino Book Reader\n");
    assert.deepEqual(findHits(dir), ['docs/store-listings/dino/play.md:1: "Dino Book Reader"']);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("the repo has no user-facing Dino Reader or Dino Book Reader", () => {
  assert.deepEqual(findHits(root), []);
});
