# Slice 106 — Windows: offline translation packs for the five new languages

*Built 2026-10-24 (PR #281), not yet verified: no `packs.yml` run and no Windows run yet.* Planned 2026-10-24, from the user's request to add Danish, Finnish, Swedish, Norwegian and Dutch
(with Slice 103).

**Batch:** 21 (five more languages)
**Depends on:** Slice 103, Slice 75 (Windows translation engine, Opus-MT packs pivoting through English)
**Touches:** `.github/workflows/packs.yml`, `src/ReadTheStupidText.Infrastructure/Translation/TranslationPacks.cs`, `tests/ReadTheStupidText.Tests/**`, `src/ReadTheStupidText.Infrastructure/CLAUDE.md`, `docs/voice-packs.md`

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
- **Rollout (2026-10-24, review of Slice 106):** the new packs reach users only after a `packs.yml` run and a
  `PackConstants.CatalogUrl` pin bump; the coordinator does that once, after Slices 104 and 106 have both merged.
- **Not changed:** the missing `en-ja` pair noted while planning is a separate gap, recorded in
  *Outstanding work*, not fixed here.

## Work

- [x] 1. **Smallest end-to-end: Swedish.** `sv-en` and `en-sv` built and released; Swedish text in English
  UI translates and reads on Windows.
- [x] 2. The other eight packs, including the Norwegian model choice (verify the model card first).
- [x] 3. Tests for the pair tables and the `nb` mapping; rule in `src/ReadTheStupidText.Infrastructure/CLAUDE.md`.

## Verification

- CI: `packs.yml` publishes all ten packs; the catalog lists them.
- Windows head: with the UI in Dutch, an English paragraph translated to Dutch reads in Dutch; a Finnish
  paragraph with the UI in English reads in English; a Norwegian paragraph translates to English.

## Changed while building

- `packs.yml` ships ten new pairs. `da`/`fi`/`sv`/`nl` in both directions use `Xenova/opus-mt-<pair>` int8.
  Norwegian uses `TigreGotico/opus-mt-gmq-en-onnx` (nb-en) and `TigreGotico/opus-mt-en-gmq-onnx` (en-nb, with the
  `>>nob<<` target token). The nb merged decoder is effectively fp32, about 390 MB per pack.
- Each table row has 11 columns including layout and licence; the column count is checked. The licence is written
  per pair (nb: apache-2.0, the others CC-BY-4.0). `maxLength` falls back to `max_position_embeddings`, and the job
  fails if both are null.
- `TranslationPacks` adds `da fi sv nb nl`. `TranslationFlow.NormalizeTag` maps `no`/`no-NO` to `nb`. A test ties
  the C# legs to `TRANSLATION_PAIRS` and the table rows.

## From review

- High pass: 9 applied; #8 handled by the rollout step above.
- Medium pass: 6 applied; #4 skipped because the sha pins were verified by download.

#### For CLAUDE.md

- `.github/workflows/packs.yml`: a new translation pair needs its `TRANSLATION_PAIRS` entry, an 11-column table row
  (layout and per-pair licence) and a matching `TranslationPacks` leg; a test ties the three together, and `maxLength`
  must resolve (`max_position_embeddings` fallback) or the job fails. New packs reach users only after a `packs.yml`
  run and the `PackConstants` pin bump. (Slice 106: docs/plan/slices/106-windows-nordic-dutch-translation-packs.md)
- `TranslationFlow.NormalizeTag` maps `no`/`no-NO` to `nb`. (Slice 106: docs/plan/slices/106-windows-nordic-dutch-translation-packs.md)

#### Not yet verified

- The `packs.yml` run that publishes the ten packs.
- Norwegian on a Windows head via `MarianEngine` (third-party conversion, never run).
- en to da/fi/sv/nl/nb and back on Windows.
