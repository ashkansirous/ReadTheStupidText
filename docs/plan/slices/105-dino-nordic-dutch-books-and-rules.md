# Slice 105 — Dino: story rules and book translations for the five new languages

*In progress (wave 2026-10-24), draft PR.* Planned 2026-10-24, from the user's request to add Danish, Finnish, Swedish, Norwegian and Dutch
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

- [ ] 1. **Smallest end-to-end: Swedish.** `SwedishRules` with tests (a short Swedish dialogue casts two
  named speakers with the right genders); the Pigs' Swedish table; rebuilt Pigs package reads in Swedish.
- [ ] 2. The other four rule sets with tests.
- [ ] 3. All four starters in all five languages; the coverage test reads `UiLanguage.Supported`; rebuild.
- [ ] 4. The store stories' tables (if Slice 102 has merged), and the adoption key bump.
- [ ] 5. Rule in `src/DinoReader.Core/CLAUDE.md` (written at planning; check it still matches).

## Verification

- Unit: each rule set casts speakers from a sample paragraph in its language; the coverage test is green
  across fourteen languages; `BundledTranslations` lookups for the new codes.
- Emulator (device-test): with Svenska picked, the Three Little Pigs read in Swedish with the wolf and pigs
  in different voices; a typed Danish story with `sagde` tags casts its speakers.
