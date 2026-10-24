# Slice 105 — Dino: story rules and book translations for the five new languages

*Built 2026-10-24 (PR #292), not yet verified on the emulator, in the R2 store, or by a native read of the translations.* Planned 2026-10-24, from the user's request to add Danish, Finnish, Swedish, Norwegian and Dutch
(with Slice 103).

**Batch:** 21 (five more languages)
**Depends on:** Slice 103 (the languages exist), Slice 95 (bundled translations), Slice 102 (store stories; their translations join whichever lands second)
**Touches:** `src/DinoReader.Core/RuleSets.cs`, `src/DinoReader.Core/{Danish,Finnish,Swedish,Norwegian,Dutch}Rules.cs` (new), `tests/DinoReader.Tests/**Rules*Tests.cs`, `tests/DinoReader.Tests/Starters/translations/*.{da,fi,sv,nb,nl}.json` (new), `tests/DinoReader.Tests/StarterStoryTests.cs`, `tests/DinoReader.Tests/StarterBuilder.cs`, `src/DinoReader.App/Resources/Raw/starters/*.dino`, `src/DinoReader.Core/CLAUDE.md`

## Why

Dino's dialogue parser has a rule set per language (`RuleSets.cs`: speech verbs, pronouns, gender) and
falls back to English rules for any other language. So a scanned Danish book would find no speakers.
The Slice 95 rule says every book Dino ships carries every app language. With five more languages,
the four starters (and the ten store stories) need five more translation tables each.

## Decisions (2026-10-24, agreed with the user)

- **Decision 109: Dino speaks the five new languages fully.** Each gets a `LanguageRules`. It covers speech
  verbs before and after the name (da `sagde`, sv `sa`, nb `sa`, fi `sanoi`, nl `zei`, plus the common
  ask/shout/whisper verbs), quote marks (»…«, „…", ”…”, – dialogue dashes), and he/she pronouns for gender
  (da/nb `han`/`hun`, sv `han`/`hon`, nl `hij`/`zij`, fi has no gendered pronoun, so gender comes from the
  name or stays unknown). These follow the shape of `FrenchRules` and `SpanishRules`.
- **Claude translates every shipped book** into da, fi, sv, nb and nl
  (`Starters/translations/<stem>.<lang>.json`). The coverage test takes its language list from
  `UiLanguage.Supported`, so a missing table fails the build. The store stories (Slice 102) get the same:
  whichever of 102 and 105 merges second adds the other's tables.
- The four starter packages are rebuilt. Existing installs adopt the new tables through the Slice 95
  one-time adoption (bump its pref key per story).

## Work

- [x] 1. **Smallest end-to-end: Swedish.** `SwedishRules` with tests (a short Swedish dialogue casts two
  named speakers with the right genders); the Pigs' Swedish table; rebuilt Pigs package reads in Swedish.
- [x] 2. The other four rule sets with tests.
- [x] 3. All four starters in all five languages; the coverage test reads `UiLanguage.Supported`; rebuild.
- [x] 4. The store stories' tables (if Slice 102 has merged), and the adoption key bump.
- [x] 5. Rule in `src/DinoReader.Core/CLAUDE.md` (written at planning; check it still matches).

## Verification

- Unit: each rule set casts speakers from a sample paragraph in its language; the coverage test is green
  across fourteen languages; `BundledTranslations` lookups for the new codes.
- Emulator (device-test): with Svenska picked, the Three Little Pigs read in Swedish with the wolf and pigs
  in different voices; a typed Danish story with `sagde` tags casts its speakers.

*Changed while building:* the five `LanguageRules` (da, fi, sv, nb, nl) are registered in `RuleSets`. A new `DashDialogue` (flag `DashTagAfterQuote`) attributes "– Hei, sanoi Pekka." for these five; a first-person tag ("sa jag") ends the quote with no named speaker. Known limit: a tag before a closing dash ("– Hej, sa Peter – och gick.") is voiced as part of the quote. Dutch `ze` is no longer counted as female (ambiguous with "they"); plural speech verbs added, false verbs removed; Danish accepts «…». 70 translation tables (4 starters and 10 store stories x 5 languages), written by Claude with no native review; the coverage tests use `UiLanguage.Supported`; the four starter packages are rebuilt. `BundledTranslations.Adopt` merges only the missing language tables into a stored copy; the adoption key is v2 and an attempt is recorded even if it fails. StoreTexts are at Version 2 and story-v2 artifacts are built; the coordinator uploads them to R2 after merge.

*From review:* high pass, 9 findings applied. Medium pass, 6 applied (2, 6, 7, 8 skipped).

#### Emulator (2026-10-24, RTST_Test)

- Choosing Svenska on first run gives a Swedish UI, and The Three Little Pigs' story.json carries the `sv` table with Translate on.
- A typed Danish story with `sagde` tags is detected as Dansk, and the cast finds Lars (boy) and Mille (girl).
- Swedish reading could not be checked. The pinned packs-4 Supertonic pack doesn't declare sv yet, so the phone voice is used. The emulator's Google TTS accepted a synthesis request and never called back, so the highlight stayed on sentence 1. origin/main behaves the same, so this is not a Slice 105 regression. One ANR was seen once and couldn't be reproduced in three runs; the main thread was idle.
- The upgrade from 1.33.0 couldn't be tested: a debug-signed build can't install over the release-signed one.

#### Not yet verified

- Swedish and Danish reading after the packs-5 release (Supertonic sv/da/fi/nl), on a real phone. Also check that the phone-voice fallback doesn't hang silently when the device has no voice for the language.
- The upgrade from 1.33.0 with a release-signed build: the new language tables are adopted into the existing starters.

- Emulator result (Svenska read of the Three Little Pigs, typed Danish story) to follow.
- Store story-v2 upload to R2 (done by the coordinator after merge).
- A native read of the translations (no native review of the 70 tables).
