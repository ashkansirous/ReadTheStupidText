// Run: node --test worker/alexa-book-names.test.mjs (Slice 457 decision 2: the Alexa title slot type is made from the repo's books).
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

import { GENERIC_WORDS, MODEL_FILES, SLOT_TYPE_NAME, SPOKEN_FORMS, buildSlotType, loadBooks, shortNames, spoken, titleWords, withSlotType } from "../tools/dino/alexa-book-names.mjs";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");

test("every book in the repo has a slot value carrying its store id, in both English models", () => {
  const books = loadBooks(ROOT);
  assert.ok(books.length >= 16, "the repo's books were read");
  for (const file of MODEL_FILES) {
    const type = JSON.parse(readFileSync(join(ROOT, file), "utf8")).interactionModel.languageModel.types.find((entry) => entry.name === SLOT_TYPE_NAME);
    assert.ok(type, file);
    for (const book of books) {
      const value = type.values.find((entry) => entry.id === book.id);
      assert.ok(value, `${file}: no slot value for the book "${book.title}" (${book.id}); run node tools/dino/alexa-book-names.mjs --write`);
      assert.equal(value.name.value, spoken(book.title));
    }
  }
});

test("the committed models are exactly what the generator writes (nothing hand-edited, nothing stale)", () => {
  const slotType = buildSlotType(loadBooks(ROOT));
  for (const file of MODEL_FILES) {
    const text = readFileSync(join(ROOT, file), "utf8").replace(/\r\n/g, "\n");
    assert.equal(withSlotType(text, slotType), text, `${file} is out of date: run node tools/dino/alexa-book-names.mjs --write`);
  }
});

test("the two English models carry the same slot type", () => {
  const [us, gb] = ["en-US", "en-GB"].map((locale) => JSON.parse(readFileSync(join(ROOT, `alexa/skill-package/interactionModels/custom/${locale}.json`), "utf8")).interactionModel.languageModel);
  assert.deepEqual(us.types, gb.types);
  assert.deepEqual(us.intents, gb.intents);
});

test("short names: the bare title, each side of 'X and Y', and 'the' forms; punctuation and '&' are spoken", () => {
  assert.deepEqual(shortNames("The Lion and the Mouse"), ["lion and the mouse", "lion", "mouse", "the lion", "the mouse"]);
  assert.deepEqual(shortNames("Goldilocks and the Three Bears"), ["goldilocks", "three bears", "the three bears"], "review: 'the' only where the title had it");
  assert.ok(!shortNames("Freddy and the Slipper").includes("the freddy"));
  assert.deepEqual(shortNames("The Gingerbread Man"), ["gingerbread man"]);
  assert.equal(spoken("X Marks the Treasure!"), "X Marks the Treasure");
  assert.equal(spoken("Goldilocks & the Three Bears"), "Goldilocks and the Three Bears");
});

test("a synonym belongs to one book only, and is never another book's title", () => {
  const type = buildSlotType([
    { id: "a", title: "The Town Mouse and the Country Mouse" },
    { id: "b", title: "The Lion and the Mouse" },
    { id: "c", title: "Mouse" },
  ]);
  const all = type.values.flatMap((value) => value.name.synonyms);
  assert.equal(new Set(all).size, all.length);
  assert.ok(!all.includes("mouse"), "'mouse' is a book of its own");
});

test("a short name two books both produce goes to neither book, so Alexa cannot silently pick one (Slice 457 review 3)", () => {
  const type = buildSlotType([
    { id: "a", title: "The Mouse and the Moon" },
    { id: "b", title: "The Lion and the Mouse" },
  ]);
  const all = type.values.filter((value) => value.id !== undefined).flatMap((value) => value.name.synonyms);
  for (const shared of ["mouse", "the mouse"]) assert.ok(!all.includes(shared), `${shared} is in no book's own value (it is the id-less shared value)`);
  assert.ok(all.includes("lion") && all.includes("moon"), "a name only one book produces is kept");
});

const synonymsOf = (type, title) => type.values.find((value) => value.name.value === title).name.synonyms;

test("slice 457 item 9: every distinctive single title word is a synonym; stop words and words two books share are not", () => {
  const type = buildSlotType(loadBooks(ROOT));
  for (const [title, words] of [
    ["Goldilocks and the Three Bears", ["goldilocks", "bears"]],
    ["The Gingerbread Man", ["gingerbread"]],
    ["The Three Little Pigs", ["pigs"]],
    ["X Marks the Treasure", ["treasure"]],
    ["Love from Seagull Island", ["seagull"]],
    ["Freddy and the Slipper", ["freddy"]],
    ["The Ugly Duckling", ["duckling"]],
  ]) for (const word of words) assert.ok(synonymsOf(type, title).includes(word), `${title}: ${word}`);
  const all = type.values.flatMap((value) => value.name.synonyms);
  for (const word of ["the", "and", "from", ...GENERIC_WORDS]) assert.ok(!all.includes(word), `stop or generic word: ${word}`);
  assert.deepEqual(titleWords("The Lion and the Mouse"), ["lion", "mouse"]);
  assert.deepEqual(titleWords("Good Enough"), [], "generic words are kept only inside longer names");
  assert.ok(synonymsOf(type, "Little Red Riding Hood").includes("red riding hood"));
});

test("slice 457 item 9: a word two books share is its own slot value with no book id, so it reaches BookTitleIntent and the Worker asks which", () => {
  const type = buildSlotType(loadBooks(ROOT));
  const mouse = type.values.find((value) => value.name.value === "mouse");
  assert.ok(mouse && mouse.id === undefined, "'mouse' has a value without an id");
  assert.deepEqual(mouse.name.synonyms, ["the mouse"]);
  assert.deepEqual(type.values.filter((value) => value.id === undefined).map((value) => value.name.value), ["mouse"], "only mouse is shared among today's books");
  const all = type.values.flatMap((value) => [value.name.value, ...value.name.synonyms]);
  assert.equal(all.filter((name) => name === "mouse").length, 1);
});

test("every SPOKEN_FORMS key is a real book's spoken title with non-empty forms", () => {
  const books = loadBooks(ROOT);
  const titleSet = new Set(books.map((book) => spoken(book.title).toLowerCase()));
  for (const [key, forms] of Object.entries(SPOKEN_FORMS)) {
    assert.ok(titleSet.has(key), `SPOKEN_FORMS: "${key}" matches no book title`);
    assert.ok(forms.length > 0 && forms.every((form) => form.trim() !== ""), key);
  }
});

test("slice 457 item 9: Goldilocks answers to goldy, goldie, goldylock and goldylocks", () => {
  const synonyms = synonymsOf(buildSlotType(loadBooks(ROOT)), "Goldilocks and the Three Bears");
  for (const form of ["goldy", "goldie", "goldylock", "goldylocks"]) assert.ok(synonyms.includes(form), form);
});

test("slice 457 item 9: a spoken form another book also produces is dropped, and the bare-title intent takes a single word", () => {
  const type = buildSlotType([
    { id: "a", title: "Goldilocks and the Three Bears" },
    { id: "b", title: "Goldy the Fish" },
  ]);
  assert.ok(!type.values.flatMap((value) => value.name.synonyms).includes("goldy"));
  const model = JSON.parse(readFileSync(join(ROOT, MODEL_FILES[0]), "utf8")).interactionModel.languageModel;
  assert.ok(model.intents.find((entry) => entry.name === "BookTitleIntent").samples.includes("{title}"));
  for (const entry of model.intents.filter((other) => other.name !== "BookTitleIntent")) assert.ok(!(entry.samples ?? []).includes("{title}"), entry.name);
});

test("in the committed models no two books share a synonym", () => {
  const all = buildSlotType(loadBooks(ROOT)).values.flatMap((value) => value.name.synonyms);
  assert.equal(new Set(all).size, all.length);
});
