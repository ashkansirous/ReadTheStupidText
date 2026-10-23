# Slice 95 — Dino translation works: bundled translations for every shipped book, live translation on Android

*Planned.* 2026-10-23, from the user's report "the translation does not work. When a book's language is
English and someone selects Farsi, the book should be translated so it can be read in Farsi. For
store-provided books, attach all available languages. Generate the translations yourself."

**Batch:** 17 (Dino Reader content, part 2)
**Depends on:** Slice 66 (translate after attribution), Slice 91 (starters), Slice 92 (store), Slice 94
**Touches:** `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/BundledTranslations.cs` (new), `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/MlKitTranslator.cs` (moved), `src/ReadTheStupidText.Mobile/Platforms/Android/MauiProgram.Android.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MlKitTranslator.cs` (new), `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MobileSharedServices.Android.cs`, `src/ReadTheStupidText.Mobile.Shared/*.csproj`, `src/ReadTheStupidText.Mobile/*.csproj`, `tests/DinoReader.Tests/StarterBuilder.cs`, `tests/DinoReader.Tests/Starters/translations/**` (new), `tests/DinoReader.Tests/StarterStoryTests.cs`, `tests/DinoReader.Tests/StoryTranslationTests.cs`, `src/DinoReader.App/Resources/Raw/starters/*.dino`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.Core/CLAUDE.md`

## Why (root cause)

Dino Reader registers no `ITranslator`: the ML Kit translator lives in the RTST Mobile project only
(`StoryTranslator` says so). So `StoryTranslator.Applies` is always false and every story is read in its
own language, whatever language the grown-up picked.

## Decisions (2026-10-23)

- **Bundled translations travel with the story.** `Story.Translations` (optional): language code to a
  table of source piece text to translated text, keyed by the exact text of each attributed piece (the
  segmenter's output, before translation, as `StoryTranslation` sees it). It lives in `story.json`, so a
  `.dino` carries it. The package `schemaVersion` stays 1: the field is optional and an older app ignores it.
- **Every book Dino ships carries all eight other languages** (fa, fr, es, pt, zh, ar, ru, ja for an
  English book): the four starters now, and every store upload from now on (an upload rule in
  `src/DinoReader.App/CLAUDE.md`). Claude writes the translations as checked-in files
  (`tests/DinoReader.Tests/Starters/translations/<starter>.<lang>.json`). The builder dumps the pieces of
  each starter, merges the translations into `story.json`, and a test fails when any piece of any starter
  has no translation in any language. So a text edit without new translations can't ship.
- **Which language:** "my language" is the one the grown-up picked (`LanguageChoice`, the screens'
  language, `StoryTranslator.MyLanguage`). A bundled translation applies when that language differs from
  the story's. Shipped books set `TranslateToMyLanguage = true`, so picking Farsi reads an English starter
  in Farsi with no extra switch. A grown-up can still turn it off per story.
- **Lookup order per piece:** the bundled table, then the on-device engine, then the source text. A book
  with a full table translates on every platform, offline and with no engine.
- **Live translation on Android:** `MlKitTranslator` moves to `Mobile.Shared/Platforms/Android` and both
  apps register it, so a scanned book with "Translate to my language" on is translated by ML Kit (model
  downloaded on first use; a missing model still reads the story as it is, as today).
- **What stays the same (Decision 74):** speakers are worked out in the source language; the voice speaks
  the translation in each character's voice; the highlight and the text on screen stay on the source
  sentence. The voice for "my language" downloads as in Slice 81.
- **Out of scope:** live translation on Windows and iOS (Windows' `PackTranslator` needs RTST's
  translation packs; iOS has no engine, see `MauiProgram.iOS.cs`). Those platforms translate bundled
  books only. Showing translated text on screen.

## Work

- [ ] 1. **Smallest end-to-end: one starter in Farsi.** `Story.Translations` and its JSON, the
  `StoryTranslator` lookup order and `Applies` (true with a bundled table for my language and the story
  switch on), a builder dump of the Pigs' pieces, a Farsi table, rebuilt package. Test: the Pigs read in
  Farsi with the same characters and source ranges, with no engine.
- [ ] 2. **All languages, all starters.** Translations for the four starters (Pigs, Gingerbread Man,
  Tortoise and Hare, Good Enough) into all eight languages; the coverage test; `TranslateToMyLanguage` on
  in the builder; rebuild the four packages.
- [ ] 3. **ML Kit for Dino on Android.** Move `MlKitTranslator` to Mobile.Shared, register it in both apps'
  Android services, drop the "no engine" note from `StoryTranslator`. Test on the emulator with a typed
  English story and Farsi chosen.
- [ ] 4. Rules into `src/DinoReader.App/CLAUDE.md` and `src/DinoReader.Core/CLAUDE.md` (done at planning,
  check they still match what was built).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (lookup order, coverage, round-trip of
  `Translations` through a `.dino`).
- Emulator: pick Farsi in the first run (or Settings), open each starter: it is read in Farsi, each
  character in its own voice, picture-mode boxes on the English line. Back to English: read in English.
  A typed English story with "Translate to my language" on and Farsi picked is read in Farsi through ML Kit.
