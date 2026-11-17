# Slice 405 — Dino: the back cover writes every name in the reading language (Elham Shafiei Gol, Ashkan Sirous, Aesop, Brothers Grimm ...)
**Product:** Dino

*Planned 2026-11-17 from the user's report; not built yet.*

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

- [ ] 1. Core: `BackCover` carries the per-language names; `BackCover.NameIn(name, language)` returns the spelling or the
      name as written; `StoryJson` reads and writes them in the back-cover file (`BackCoverFile`), old files without them still load.
- [ ] 2. App: `BackCoverText.Lines` uses `NameIn` for Story by, Original story by, Retold by and Illustration, and joins
      several names with the language's own "and" (`BackCover_And`), as today. Android, iOS and the Windows head share this code.
- [ ] 3. Builder: carries `namesByLanguage` into story.json, adds it to `BookHash`, and fails a book that misses a name or language.
- [ ] 4. Content: `namesByLanguage` for all 15 books in all 14 languages; bump each book's version; upload every bumped
      store book right after the merge (store and app must match).
- [ ] 5. Writer and reviewer docs (`create-dino-story`, `dino-story-writer`, `dino-story-reviewer`): a new
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
