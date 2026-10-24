# Slice 106 — Windows: offline translation packs for the five new languages

*In progress (wave 2026-10-24), draft PR.* Planned 2026-10-24, from the user's request to add Danish, Finnish, Swedish, Norwegian and Dutch
(with Slice 103).

**Batch:** 21 (five more languages)
**Depends on:** Slice 103, Slice 75 (Windows translation engine, Opus-MT packs pivoting through English)
**Touches:** `.github/workflows/packs.yml`, `src/ReadTheStupidText.Infrastructure/Translation/TranslationPacks.cs`, `tests/ReadTheStupidText.Tests/**`, `src/ReadTheStupidText.Infrastructure/CLAUDE.md`

## Why

Windows translates offline with Opus-MT packs, one per direction, pivoting through English
(`TranslationPacks.IntoEnglish`/`FromEnglish`; `packs.yml` `TRANSLATION_PAIRS`). Without packs for the new
languages, "Translate" silently does nothing on Windows for exactly those users. Android uses ML Kit,
which already covers them.

## Decisions (2026-10-24, agreed with the user)

- **Decision 110: ten more Windows translation packs.** `da-en en-da fi-en en-fi sv-en en-sv nb-en en-nb
  nl-en en-nl`. They're built the same way as today's pairs from the Helsinki-NLP Opus-MT models.
  Where no direct Norwegian model exists, use the Scandinavian multi-target model with the Norwegian target
  token. The adapter maps `nb` to the model's own code. Each pack carries its model licence (CC-BY 4.0) as
  today.
- Add the five to `IntoEnglish` and `FromEnglish`; between two new languages (e.g. sv to nl) it pivots
  through English, as other pairs do.
- **Not changed:** the missing `en-ja` pair noted while planning is a separate gap, recorded in
  *Outstanding work*, not fixed here.

## Work

- [ ] 1. **Smallest end-to-end: Swedish.** `sv-en` and `en-sv` built and released; Swedish text in English
  UI translates and reads on Windows.
- [ ] 2. The other eight packs, including the Norwegian model choice (verify the model card first).
- [ ] 3. Tests for the pair tables and the `nb` mapping; rule in `src/ReadTheStupidText.Infrastructure/CLAUDE.md`.

## Verification

- CI: `packs.yml` publishes all ten packs; the catalog lists them.
- Windows head: with the UI in Dutch, an English paragraph translated to Dutch reads in Dutch; a Finnish
  paragraph with the UI in English reads in English; a Norwegian paragraph translates to English.
