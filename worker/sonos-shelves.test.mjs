// Run: node --test worker/sonos-shelves.test.mjs (Slice 437: the shelf rules, the ids, the words and the Accept-Language reader; no I/O).
import test from "node:test";
import assert from "node:assert/strict";

import { Shelf, bookSubtitle, languageNameIn, languageSubtitle, localDigits, shelfSubtitle, shelfTitle, storiesLabel } from "./sonos-labels.ts";
import { otherLanguages, parseShelfId, shelfId, shelfTracks, shelvesOf, sortAlphabetically } from "./sonos-shelves.ts";
import { acceptedLanguages } from "./sonos-soap.ts";

const LANGUAGES = ["en", "ar", "da", "de", "es", "fa", "fi", "fr", "ja", "nb", "nl", "pt", "ru", "sv", "zh"];

function book(storyId, extra = {}) {
  return { language: "en", storyId, title: storyId, author: null, seconds: 300, version: 1, coverUrl: null, squareCoverUrl: null, listenUrl: "u", tags: [], ageMin: null, ageMax: null, added: null, paid: false, ...extra };
}

const idsOf = (tracks) => tracks.map((track) => track.storyId);

test("Bedtime is 600 s or less, shortest first; 601 s is out", () => {
  const books = [book("b", { seconds: 600 }), book("a", { seconds: 120 }), book("c", { seconds: 601 }), book("d", { seconds: 120, title: "Aardvark" })];
  assert.deepEqual(idsOf(shelfTracks(books, Shelf.Bedtime, "en")), ["a", "d", "b"], "equal lengths keep A-Z order");
});

test("New is the New tag, newest first; no date sorts last", () => {
  const books = [book("old", { tags: ["New"], added: "2026-01-01" }), book("new", { tags: ["Hot", "New"], added: "2026-11-01" }), book("undated", { tags: ["New"] }), book("plain", { added: "2026-12-01" })];
  assert.deepEqual(idsOf(shelfTracks(books, Shelf.New, "en")), ["new", "old", "undated"]);
});

test("age shelves: ageMin <= 5 and ageMax >= 6; a 3-9 book is in both; no range is in neither; 6-8 is only older", () => {
  const books = [book("young", { ageMin: 3, ageMax: 5 }), book("both", { ageMin: 3, ageMax: 9 }), book("older", { ageMin: 6, ageMax: 8 }), book("none")];
  assert.deepEqual(idsOf(shelfTracks(books, Shelf.Ages35, "en")), ["both", "young"]);
  assert.deepEqual(idsOf(shelfTracks(books, Shelf.Ages69, "en")), ["both", "older"]);
  assert.deepEqual(idsOf(shelfTracks(books, Shelf.All, "en")), ["both", "none", "older", "young"]);
});

test("Free leaves paid books out; an empty shelf is not in the shelf list", () => {
  const books = [book("f"), book("p", { paid: true })];
  assert.deepEqual(idsOf(shelfTracks(books, Shelf.Free, "en")), ["f"]);
  assert.deepEqual(shelvesOf(books, "en").map((row) => row.shelf), [Shelf.Bedtime, Shelf.Free, Shelf.All]);
  assert.deepEqual(shelvesOf([], "en"), []);
});

test("A-Z: English ignores a leading The (any case), Farsi follows its own alphabet, ties keep one order", () => {
  const english = [book("1", { title: "Zoo" }), book("2", { title: "The Apple" }), book("3", { title: "the Moon" }), book("4", { title: "Bear" })];
  assert.deepEqual(sortAlphabetically(english, "en").map((track) => track.title), ["The Apple", "Bear", "the Moon", "Zoo"]);
  const farsi = [book("1", { title: "پ" }), book("2", { title: "ب" }), book("3", { title: "آ" }), book("4", { title: "ا" })];
  assert.deepEqual(sortAlphabetically(farsi, "fa").map((track) => track.title), ["آ", "ا", "ب", "پ"]);
  const ties = [book("b", { title: "Same" }), book("a", { title: "Same" })];
  assert.deepEqual(idsOf(sortAlphabetically(ties, "en")), ["a", "b"]);
  assert.equal(sortAlphabetically([book("x")], "not a language!").length, 1);
});

test("ids carry the language and round-trip; anything else is not a shelf id", () => {
  assert.equal(shelfId("fa", Shelf.Bedtime), "lang:fa/bedtime");
  assert.deepEqual(parseShelfId("lang:fa"), { language: "fa", shelf: null, firstLanguages: [] });
  assert.deepEqual(parseShelfId("lang:fa/age-3-5"), { language: "fa", shelf: "age-3-5", firstLanguages: [] });
  assert.deepEqual(parseShelfId(shelfId("fa", Shelf.Others, ["en", "de"])), { language: "fa", shelf: "others", firstLanguages: ["en", "de"] });
  for (const bad of [null, "root", "track:en:x", "lang:", "lang:en/zzz", "lang:en/all/en", "lang:en/others/a b", "lang:en/others/a/b", "lang:a b"]) assert.equal(parseShelfId(bad), null, String(bad));
});

test("Other languages: header languages first (only ones that exist), then by name in the parent's language, never the parent's own", () => {
  const name = (code) => ({ de: "German", fa: "Persian", ar: "Arabic", fr: "French" })[code] ?? code;
  assert.deepEqual(otherLanguages(["ar", "de", "en", "fa", "fr"], "en", [], name), ["ar", "fr", "de", "fa"]);
  assert.deepEqual(otherLanguages(["ar", "de", "en", "fa", "fr"], "en", ["fr", "xx", "en"], name), ["fr", "ar", "de", "fa"], "header language first, the rest by English name");
});

test("Accept-Language: q order, region dropped, q=0 and * left out, duplicates once, nb aliases, junk is empty (Slice 437 A2)", () => {
  assert.deepEqual(acceptedLanguages("fa-IR, en-GB;q=0.8"), ["fa", "en"]);
  assert.deepEqual(acceptedLanguages("en;q=0.5, de;q=0.9, fr"), ["fr", "de", "en"]);
  assert.deepEqual(acceptedLanguages("zh-Hans-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7"), ["zh", "en"]);
  assert.deepEqual(acceptedLanguages("de;q=0, *;q=0.5, FA"), ["fa"]);
  assert.deepEqual(acceptedLanguages("nb-NO, no;q=0.9, nn;q=0.8"), ["nb"]);
  assert.deepEqual(acceptedLanguages(null), []);
  assert.deepEqual(acceptedLanguages(""), []);
  assert.deepEqual(acceptedLanguages(";;, ,q=1"), []);
});

test("words from the mock: English and Farsi", () => {
  assert.equal(shelfTitle("en", Shelf.Bedtime), "Bedtime: 10 minutes or less");
  assert.equal(shelfSubtitle("en", Shelf.Bedtime, 6), "6 stories · shortest first · press play for all");
  assert.equal(shelfSubtitle("en", Shelf.Bedtime, 1), "1 story · shortest first · press play for all");
  assert.equal(shelfTitle("en", Shelf.Ages35), "Ages 3–5");
  assert.equal(shelfTitle("en", Shelf.All), "All stories, A–Z");
  assert.equal(shelfSubtitle("en", Shelf.Others, 14), "14 languages");
  assert.equal(shelfTitle("fa", Shelf.Bedtime), "قصه‌های شب: ۱۰ دقیقه یا کمتر");
  assert.equal(shelfSubtitle("fa", Shelf.Bedtime, 6), "۶ داستان · کوتاه‌ترها اول · پخش همه");
  assert.equal(shelfTitle("fa", Shelf.Ages35), "۳ تا ۵ سال");
  assert.equal(shelfTitle("fa", Shelf.Ages69), "۶ تا ۹ سال");
  assert.equal(shelfTitle("fa", Shelf.All), "همه‌ی داستان‌ها، الفبایی");
  assert.equal(shelfSubtitle("fa", Shelf.Others, 14), "۱۴ زبان");
  assert.equal(languageSubtitle("en", "fa", 8), "انگلیسی · ۸ داستان");
  assert.equal(languageSubtitle("de", "en", 8), "German · 8 stories");
});

test("book subtitle: ages, whole minutes (at least 1), then Free or In the Dino app; no ages, no age part", () => {
  assert.equal(bookSubtitle(book("a", { ageMin: 3, ageMax: 5, seconds: 360 }), "en"), "Ages 3–5 · 6 min · Free");
  assert.equal(bookSubtitle(book("a", { ageMin: 3, ageMax: 5, seconds: 360, paid: true }), "en"), "Ages 3–5 · 6 min · In the Dino app");
  assert.equal(bookSubtitle(book("a", { seconds: 10 }), "en"), "1 min · Free");
  assert.equal(bookSubtitle(book("a", { ageMin: 3, ageMax: 5, seconds: 360, paid: true }), "fa"), "۳ تا ۵ سال · ۶ دقیقه · در برنامه‌ی دینو");
});

test("fa and ar use their own digits; every other language keeps ASCII", () => {
  assert.equal(localDigits("0123456789", "fa"), "۰۱۲۳۴۵۶۷۸۹");
  assert.equal(localDigits("0123456789", "ar"), "٠١٢٣٤٥٦٧٨٩");
  assert.equal(localDigits("0123456789", "de"), "0123456789");
  assert.equal(storiesLabel("ar", 12), "القصص: ١٢");
  assert.match(shelfTitle("ar", Shelf.Bedtime), /١٠/);
});

test("every one of the 15 languages words every shelf and the book line, with no leftover placeholder", () => {
  for (const language of LANGUAGES) {
    for (const shelf of Object.values(Shelf)) {
      const words = [shelfTitle(language, shelf), shelfSubtitle(language, shelf, 7)];
      for (const text of words) assert.ok(text.length > 0 && !/[{}]/.test(text), `${language} ${shelf}: ${text}`);
    }
    const line = bookSubtitle(book("a", { ageMin: 3, ageMax: 5, seconds: 400, paid: true }), language);
    assert.ok(!/[{}]/.test(line) && line.split(" · ").length === 3, `${language}: ${line}`);
    assert.ok(languageNameIn("de", language).length > 0);
  }
  assert.equal(shelfTitle("xx", Shelf.Free), "Free stories", "an unknown language reads English");
});
