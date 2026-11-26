// Slice 457 decision 2: the Alexa skill's custom slot type `DINO_BOOK_TITLE`, made from the repo's own book metadata
// (content/dino/<stem>/book.json: id and title), so a bare book name ("goldilocks", "the lion", "gingerbread man") reaches the skill.
// Every book gets one value (id = the book's store id, which the Worker reads back from the slot's entity resolution) with its full title
// and short names as synonyms. The type is written into both English interaction models; `worker/alexa-book-names.test.mjs` fails when
// a repo book has no value or a model is out of date.
// Run from the repo root: node tools/dino/alexa-book-names.mjs --check   (exit 1 when a model is out of date)
//                         node tools/dino/alexa-book-names.mjs --write   (rewrites the slot type in both models)
import { existsSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export const SLOT_TYPE_NAME = "DINO_BOOK_TITLE";
export const BOOKS_FOLDER = "content/dino";
export const MODEL_FILES = ["alexa/skill-package/interactionModels/custom/en-US.json", "alexa/skill-package/interactionModels/custom/en-GB.json"];
const MIN_SHORT_LENGTH = 4;
const ARTICLES = new Set(["the", "a", "an"]);

/** Every book's id and English title, in folder order. */
export function loadBooks(root) {
  const folder = join(root, BOOKS_FOLDER);
  return readdirSync(folder)
    .map((stem) => join(folder, stem, "book.json"))
    .filter((file) => existsSync(file))
    .map((file) => JSON.parse(readFileSync(file, "utf8")))
    .map((book) => ({ id: book.id, title: book.title }));
}

/** A title as it is spoken: "&" is "and", punctuation is gone, spaces are single. */
export function spoken(title) {
  return title
    .replace(/&/g, " and ")
    .replace(/[^\p{L}\p{N}\s]/gu, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function withoutArticle(text) {
  const parts = text.split(" ");
  return ARTICLES.has(parts[0].toLowerCase()) ? parts.slice(1).join(" ") : text;
}

/** The ways a person says a book: without "the", and each side of "X and Y" ("Goldilocks", "three bears", "the lion"). */
export function shortNames(title) {
  const full = spoken(title);
  const bare = withoutArticle(full);
  const sides = full.split(/\s+and\s+/i).map((side) => withoutArticle(side));
  const parts = sides.length > 1 ? sides.filter((side) => side.length >= MIN_SHORT_LENGTH) : [];
  // "the" goes back only on a side that had it ("the lion"), never on a bare one ("freddy" is not "the freddy").
  const sidesWithArticle = full.split(/\s+and\s+/i).filter((side) => withoutArticle(side) !== side && withoutArticle(side).length >= MIN_SHORT_LENGTH);
  const names = [bare, ...parts, ...(sides.length > 1 ? sidesWithArticle : [])];
  return [...new Set(names.map((name) => name.toLowerCase()))].filter((name) => name !== full.toLowerCase());
}

/** How many books produce each short name (a book counts once). */
function claimsPerName(books) {
  const claims = new Map();
  for (const book of books) for (const name of new Set(shortNames(book.title))) claims.set(name, (claims.get(name) ?? 0) + 1);
  return claims;
}

/**
 * The slot type: one value per book. A short name claimed by two or more books is left out of all of them, and so is one that is another
 * book's title: Alexa would resolve it to a single book without asking, so the Worker's own matcher sees the bare word and asks which.
 */
export function buildSlotType(books) {
  const titles = new Set(books.map((book) => spoken(book.title).toLowerCase()));
  const claims = claimsPerName(books);
  const values = books.map((book) => {
    const value = spoken(book.title);
    const synonyms = shortNames(book.title).filter((name) => !titles.has(name) && claims.get(name) === 1);
    return { id: book.id, name: { value, synonyms } };
  });
  return { name: SLOT_TYPE_NAME, values };
}

/** The model text with the slot type in place (added when missing, replaced when there), in the repo's two-space JSON style. */
export function withSlotType(modelText, slotType) {
  const model = JSON.parse(modelText);
  const types = (model.interactionModel.languageModel.types ?? []).filter((type) => type.name !== SLOT_TYPE_NAME);
  model.interactionModel.languageModel.types = [...types, slotType];
  return `${JSON.stringify(model, null, 2)}\n`;
}

function run(root, mode) {
  const slotType = buildSlotType(loadBooks(root));
  let stale = 0;
  for (const file of MODEL_FILES) {
    const path = resolve(root, file);
    const current = readFileSync(path, "utf8");
    const next = withSlotType(current, slotType);
    if (next.replace(/\r\n/g, "\n") === current.replace(/\r\n/g, "\n")) continue;
    stale++;
    if (mode === "--write") writeFileSync(path, next);
    else console.error(`${file} is out of date: run node tools/dino/alexa-book-names.mjs --write`);
  }
  return mode === "--write" || stale === 0 ? 0 : 1;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const mode = process.argv.includes("--write") ? "--write" : "--check";
  process.exit(run(process.cwd(), mode));
}
