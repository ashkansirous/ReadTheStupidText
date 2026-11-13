// Run: node --test worker/catalogue.test.mjs
// Slice 355: the public books page (site/dino/books.html). Its pure part sits between the PURE-START and PURE-END
// markers in the page's script, so these tests run the very code the page ships.
import test from "node:test";
import assert from "node:assert/strict";
import vm from "node:vm";
import { readFileSync } from "node:fs";

// The 15 app languages (READING_LANGUAGES in events.ts, not exported; the store books carry the same 15).
const READING_LANGUAGES = ["en", "fa", "fr", "es", "pt", "zh", "ar", "ru", "ja", "da", "fi", "sv", "nb", "nl", "de"];

const page = readFileSync(new URL("../site/dino/books.html", import.meta.url), "utf8");

function loadPure() {
  const start = page.indexOf("// PURE-START");
  const end = page.indexOf("// PURE-END");
  assert.ok(start > 0 && end > start, "the page has its PURE-START and PURE-END markers");
  const body = page.slice(start, end);
  return vm.runInNewContext(`(function () { ${body}
    return { LANGUAGES, DEFAULT_PRICE, normalizeLanguage, pickLanguage, text, readConfig, priceLabel, titleFor, creditFor, isOwnCover, agesText }; })()`);
}

const pure = loadPure();
const BOOK_ID = "0123456789abcdef0123456789abcdef";
const free = { id: "a", title: "Town Mouse", access: "free" };
const paid = { id: "b", title: "Red Hen", access: "paid" };

test("switch off: every book shows Free, paid or not (as the apps do)", () => {
  for (const raw of [{ paidBooks: {} }, { paidBooks: { enabled: false } }, { paidBooks: { enabled: "true" } }, { paidBooks: { enabled: 1 } }]) {
    const config = pure.readConfig(raw);
    assert.equal(config.paidEnabled, false);
    assert.deepEqual({ ...pure.priceLabel(paid, config, "en") }, { free: true, label: "Free" });
    assert.deepEqual({ ...pure.priceLabel(free, config, "en") }, { free: true, label: "Free" });
  }
});

test("config unavailable (null): no price and no Free label at all, for free and paid books", () => {
  for (const raw of [null, undefined, "x", {}, { error: "x" }, { paidBooks: null }]) {
    const config = pure.readConfig(raw);
    assert.equal(config.known, false);
    assert.deepEqual({ ...pure.priceLabel(paid, config, "en") }, { free: false, label: "" });
    assert.deepEqual({ ...pure.priceLabel(free, config, "en") }, { free: false, label: "" });
  }
});

test("switch on: free books show Free and paid books show the configured price", () => {
  const config = pure.readConfig({ paidBooks: { enabled: true }, catalogue: { price: "  $1.49 / €1.49  " } });
  assert.deepEqual({ ...pure.priceLabel(free, config, "en") }, { free: true, label: "Free" });
  assert.deepEqual({ ...pure.priceLabel(paid, config, "en") }, { free: false, label: "$1.49 / €1.49" });
  assert.equal(pure.priceLabel(paid, config, "de").label, "$1.49 / €1.49");
  assert.equal(pure.priceLabel(free, config, "de").label, "Kostenlos");
});

test("switch on without a usable price falls back to the default price; a book with no access field is not paid", () => {
  const config = pure.readConfig({ paidBooks: { enabled: true }, catalogue: { price: "   " } });
  assert.equal(config.price, pure.DEFAULT_PRICE);
  assert.equal(pure.DEFAULT_PRICE, "$0.99 / €0.99 / £0.99");
  assert.equal(pure.priceLabel({ id: "c", title: "x" }, config, "en").free, true);
});

test("the page carries one set of texts for each of the app's languages, with every key filled", () => {
  assert.deepEqual(Object.keys(pure.LANGUAGES).sort(), [...READING_LANGUAGES].sort());
  const keys = Object.keys(pure.LANGUAGES.en.s).sort();
  for (const [code, language] of Object.entries(pure.LANGUAGES)) {
    assert.deepEqual(Object.keys(language.s).sort(), keys, code);
    for (const key of keys) assert.ok(language.s[key].trim().length > 0, `${code}.${key}`);
    assert.ok(language.name.length > 0, code);
  }
});

test("Farsi and Arabic lay out right to left, every other language left to right", () => {
  for (const [code, language] of Object.entries(pure.LANGUAGES)) {
    assert.equal(language.dir, code === "fa" || code === "ar" ? "rtl" : "ltr", code);
  }
});

test("language choice: a request wins, then the browser's list in order, then English", () => {
  assert.equal(pure.pickLanguage("de", ["fr-FR"]), "de");
  assert.equal(pure.pickLanguage(null, ["fr-FR", "de"]), "fr");
  assert.equal(pure.pickLanguage("xx", ["sw", "es-MX", "de"]), "es");
  assert.equal(pure.pickLanguage(null, ["pt-BR"]), "pt");
  assert.equal(pure.pickLanguage(null, ["zh-Hans-CN"]), "zh");
  assert.equal(pure.pickLanguage("ar", []), "ar");
});

test("Norwegian tags (no, nn, nb-NO) all choose the nb texts", () => {
  for (const tag of ["no", "nn", "nb", "nb-NO", "NO-no"]) assert.equal(pure.pickLanguage(null, [tag]), "nb", tag);
});

test("fallback to English: nothing usable, or a bad request, gives English", () => {
  assert.equal(pure.pickLanguage(null, []), "en");
  assert.equal(pure.pickLanguage(undefined, undefined), "en");
  assert.equal(pure.pickLanguage("", ["sw", "tlh", 5, null]), "en");
  assert.equal(pure.pickLanguage("__proto__", ["constructor"]), "en");
  assert.equal(pure.normalizeLanguage("toString"), null);
});

test("a missing text falls back to the English one", () => {
  assert.equal(pure.text("de", "free"), "Kostenlos");
  assert.equal(pure.text("xx", "free"), "Free");
  assert.equal(pure.text("de", "noSuchKey"), undefined);
});

test("titles and credits use the translated entry, else the book's own", () => {
  const book = { title: "Town Mouse", titles: { fa: "T-fa", de: "  " }, credits: "Aesop", creditsByLanguage: { fa: "C-fa" } };
  assert.equal(pure.titleFor(book, "fa"), "T-fa");
  assert.equal(pure.titleFor(book, "de"), "Town Mouse");
  assert.equal(pure.titleFor(book, "fr"), "Town Mouse");
  assert.equal(pure.creditFor(book, "fa"), "C-fa");
  assert.equal(pure.creditFor(book, "fr"), "Aesop");
  assert.equal(pure.creditFor({ title: "x" }, "fr"), "");
  assert.equal(pure.titleFor({ titles: null, title: "x" }, "fa"), "x");
});

test("only the site's own cover path is fetched, on both prefixes", () => {
  assert.equal(pure.isOwnCover("", `/api/stories/${BOOK_ID}/cover?v=3`), true);
  assert.equal(pure.isOwnCover("/dino", `/dino/api/stories/${BOOK_ID}/cover?v=3`), true);
  assert.equal(pure.isOwnCover("", `/dino/api/stories/${BOOK_ID}/cover?v=3`), false);
  assert.equal(pure.isOwnCover("", `https://evil.example/api/stories/${BOOK_ID}/cover`), false);
  assert.equal(pure.isOwnCover("", null), false);
});

test("ages show as a range only when both ends are numbers", () => {
  assert.equal(pure.agesText({ ageMin: 3, ageMax: 9 }), "3–9");
  assert.equal(pure.agesText({ ageMin: 3, ageMax: null }), "");
  assert.equal(pure.agesText({}), "");
});

test("the page makes no request to another host and holds no external script, style or font", () => {
  assert.equal(/<script[^>]*\ssrc=/i.test(page), false);
  assert.equal(/<link[^>]*href="https?:/i.test(page), false);
  assert.equal(/@import|url\(\s*["']?https?:/i.test(page), false);
  const fetched = [...page.matchAll(/fetch\(([^)]*)\)/g)].map((m) => m[1]);
  assert.ok(fetched.length > 0);
  for (const call of fetched) assert.equal(/https?:/.test(call), false, call);
});

test("the page file uses LF line endings (a CRLF site file is refused at deploy)", () => {
  assert.equal(page.includes("\r"), false);
});
