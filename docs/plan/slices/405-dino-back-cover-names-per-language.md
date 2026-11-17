# Slice 405 — Dino: the back cover writes every name in the reading language (Elham Shafiei Gol, Ashkan Sirous, Aesop, Brothers Grimm ...)
**Product:** Dino

*Built 2026-11-17, reviewed (high and medium), checked on the Android emulator, not merged; PR to follow.*

**Touches:** `src/Dino/DinoReader.Core/BackCover.cs`, `src/Dino/DinoReader.Core/StoryJson.cs`, `src/Dino/DinoReader.App/BackCoverText.cs`, `tools/dino/DinoReader.BookBuilder/Books.cs`, `tools/dino/DinoReader.BookBuilder/BookCommands.cs`, `tools/dino/DinoReader.BookBuilder/BookHash.cs`, `content/dino/*/book.json`, `tests/Dino/DinoReader.Tests/BackCoverTests.cs`, `.claude/skills/create-dino-story/SKILL.md`, `.claude/agents/dino-story-writer.md`, `.claude/agents/dino-story-reviewer.md`

## Why

The user, 2026-11-17: the credits page in the books, "where it says the writers", is "being printed in english" in
the other languages; "add a slice to fix it".

Checked on `origin/main` the same day:

- The back-cover labels are translated: `BackCover_StoryBy`, `_OriginalStoryBy`, `_Traditional`, `_RetoldBy`,
  `_Illustration`, `_Date` and the months are in all 15 `AppResources*.resx` files (`BackCoverText.Lines`).
- The **names are not**: they come from the book's one `storyBy` string (`"Elham Shafiei Gol; Ashkan Sirous"`,
  `"Aesop"`, `"Brothers Grimm"`, `"Robert Southey"`, `"Charles Perrault; Brothers Grimm"`, `"Hans Christian Andersen"`)
  and the fixed `BackCover.Maker = "Ashkan Sirous"` (`src/Dino/DinoReader.Core/BackCover.cs:31`), so a Farsi back
  cover reads «داستان از Elham Shafiei Gol و Ashkan Sirous» and a German one "Originalgeschichte von Aesop".
- The store card already spells them per language: `creditsByLanguage` (live `/api/stories`, all 15 books, 14
  languages), e.g. Freddy `fa` «نوشتهٔ الهام شفیعی گل و اشکان سیروس», `ja` 「作：エルハム・シャフィエイ・ゴル、アシュカン・シルース」.

**Earlier rule this overrides:** Slice 237 (`docs/plan/slices/237-dino-back-cover-credits.md`, *Decided by me*):
"Names are never translated; a book may give transliterations per language if the writer finds the other books do so."
That was my default, not the user's decision; the user's report replaces it.

Two other ways a whole back cover shows in English, checked and kept as they are:

- The back cover follows the language of the page text (`ReadingPage.BackCoverOf`, `_translatedDisplay ? ReadingLanguage : story.Language`).
  A book shown in its own English text gets an English back cover. That matches the page above it (Slice 237).
- An app older than Slice 237 reads the English-only fallback page (`StoryJson.BackCoverPage`, `BackCoverEnglish.PageText`);
  Slice 237's user decision ("Old app versions may read the page ... until they update") stands.

Asked the user the same day which of the two they saw (only the names, or the whole page); if their answer is "the whole
page, on the current version", item 6 covers it.

## Decisions

- **Data in the book, not the app:** each `book.json` gets `namesByLanguage`: `{ "<lang>": { "<name as written>": "<spelling>" } }`,
  one entry for every name the back cover shows (each `storyBy` name, the illustrator, and the reteller `Ashkan Sirous`),
  for all 14 languages besides English. A new book with a new writer needs no app release.
- **Spellings:** take each name from the book's own `creditsByLanguage` where it is there (so card and back cover agree);
  otherwise the usual form in that language (de "Äsop", "Brüder Grimm"; fr "Ésope", "les frères Grimm"; fa «ازوپ»,
  «برادران گریم» ...). Latin-script languages keep a personal name as written unless the language has its own form.
- **A missing name falls back to the name as written,** never an empty line.
- **The builder refuses** a book with a back cover whose `namesByLanguage` misses a name or a language, so no new book
  ships with Latin names again (the open `feat/seagull-grannies` book included).
- Old apps are unchanged: the English fallback page stays as it is.

## Work

- [x] 1. Core: `BackCover` carries the per-language names; `BackCover.NameIn(name, language)` returns the spelling or the
      name as written; `StoryJson` reads and writes them in the back-cover file (`BackCoverFile`), old files without them still load.
- [x] 2. App: `BackCoverText.Lines` uses `NameIn` for Story by, Original story by, Retold by and Illustration, and joins
      several names with the language's own "and" (`BackCover_And`), as today. Android, iOS and the Windows head share this code.
- [x] 3. Builder: carries `namesByLanguage` into story.json, adds it to `BookHash`, and fails a book that misses a name or language.
- [x] 4. Content: `namesByLanguage` for all 15 books in all 14 languages; bump each book's version; upload every bumped
      store book right after the merge (store and app must match).
- [x] 5. Writer and reviewer docs (`create-dino-story`, `dino-story-writer`, `dino-story-reviewer`): a new
      book writes `namesByLanguage` with the translations, and the reviewer checks the back cover's names in two languages.
- [ ] 6. Only if the user says the whole page was English on the current version: find out why (the book's language,
      translation fallback events in `dino_usage`, the app version) before changing anything.

## Verification

- Unit (`tests/Dino/DinoReader.Tests/BackCoverTests.cs`): `NameIn` finds a spelling, falls back to the written name;
  a story.json with names round-trips; one without them still loads; the builder refuses a book missing a language.
- Android emulator: a store book's back cover in Farsi and in German shows the names from `namesByLanguage`
  (read from the `BACKCOVER` log line and a screenshot).
- iOS simulator: the same back cover in Farsi and Japanese.
- Store: every bumped book's version on `/api/stories` equals its `book.json` after the upload.

## Built

*Changed while building:*

- `namesByLanguage` is in all 15 `book.json` files (14 languages). `BackCover.NameIn` / `JoinSpelled`: when a spelled name already holds the language's "and", the names are joined with the list separator instead (new resx keys `BackCover_AndWord` and `BackCover_List` in all 15 languages). `BackCoverText` is now an instance with a lookup seam, tested by `BackCoverTextTests` (fa, de, ja).
- The builder refuses a missing name or language, and a Latin-only spelling in ar, fa, ja, zh, ru. The names are part of `BookHash`.
- 4 starters stamped (good-enough 10, three-little-pigs 16, gingerbread-man 13, tortoise-and-hare 13) and their `testbooks/*.dino` rebuilt. The 11 store books take their version at upload (a store book needs no stamp, Slice 239).
- Spellings: German "den Brüdern Grimm" and Spanish "los hermanos Grimm" fit "von/de {0}"; French and Portuguese "Jacob et/e Wilhelm Grimm".
- Tests: DinoReader.Tests 4615 passed, 2 skipped; Shared.Tests 1722 passed; builder check "All 15 books are current"; Android build 0 errors.
- Device (Android emulator, three-little-pigs back cover; evidence in `D:/ReadTheStupidText-worktrees/reviews/s405-device/`): Farsi PASS («یک قصهٔ قدیمی», «بازنویسی برای کودکان توسط اشکان سیروس», «تصویرگری: اشکان سیروس»); German PASS (German labels, Latin names).

*From review:*

- Read: `reviews/s405-high.md` findings 7/7, `reviews/s405-medium.md` findings 9/9.
- Applied (per the builder's report): high #1 (fr/pt Grimm "and", via `JoinSpelled`), #2 (`BackCoverTextTests`), #6 (items ticked; store books need no stamp), #7 (script check).
- Medium #1 is not a defect: store books need no stamp (high #6 correction).
- Skipped #5 (`reviews/s405-high.md`): "JoinNames is public and now takes" the cover and a language code - public signature left as is.
- Skipped #2 (`reviews/s405-medium.md`): "glued forms (Arabic و, Japanese と, Chinese 和) are never detected" and a bare "e"/"y" name - no current book hits it; follow-up.
- Skipped #6 (`reviews/s405-medium.md`): "script-letter check passes any spelling holding one letter of the script" - follow-up.
- Skipped #3, #4, #5, #7, #8 (`reviews/s405-medium.md`): "hash uses only NamesByLanguage Count", "lookup is ordinal and case-sensitive", "duplicates the Lines() rule", "lost static (public API shrink)", "resx re-parsed with XDocument.Load" - all rated low, left.
- Applied high #3 (`NameIn` does one lookup, no "!") and #4 (`BackCover` equality compares the names by content, with a test).
- Medium #9 not a defect: the round-trip and old-file test exists (`BackCoverTests.Names_by_language_round_trip_through_story_json_and_an_old_file_without_them_still_loads`).

*Open items:*

- Farsi reteller line wraps inside the name («...توسط اشکان» / «سیروس») and the gap looks larger than the normal wrapped-line spacing. Keep a name on one line (non-breaking spaces) and check the gap. Not fixed in this slice.
- After the merge (coordinator): upload all 15 bumped books to the store and compare `/api/stories` versions with `book.json`.
- Item 6 stays open, conditional on the user's answer.

#### For CLAUDE.md

- Dino back cover names: every name the back cover shows needs a `namesByLanguage` entry for all 14 non-English languages in the book's `book.json`; the builder refuses a missing name or language and a Latin-only spelling in ar, fa, ja, zh, ru. A spelling that already holds the language's "and" is joined with the list separator (`BackCover.JoinSpelled`). (Slice 405: docs/plan/slices/405-dino-back-cover-names-per-language.md)

#### Not yet verified

- **Device:** iOS simulator back cover in Farsi and Japanese (no Mac here; `.github/workflows/dinoreader-ios-sim.yml` is workflow_dispatch only and has no back-cover flow).
- **You:** native read of the spellings chosen without a store-card source: ar «الأخوان غريم»; Perrault, Southey and Andersen in ar, fa, ja, ru, zh; ru «Эзоп».
