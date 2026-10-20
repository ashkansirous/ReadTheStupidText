# Batch 9 and 10 indexes, moved from plan.md

**Batch 9 — Mobile UX, OCR, translation, language packs and iOS.** Source:
the two 2026-10-20 phone scope docs (*mobile UX / OCR / translation* and *iOS +
per-platform pipeline*), folded into Decisions 59-71. Android gets the new
features first, iOS follows in parallel on the same MAUI project, and Windows
catches up last. Slices in this batch live in `docs/plan/slices/` (one file
each, with Steps, Acceptance and Risks); this section is only the index.
`plan.md` will be migrated to the index layout by `archive-docs` once PR #165
(the Batch 8 implementation) merges. Slices 44, 48, 50 and 56 edit the pipeline
and depend on that PR.

| # | Slice | Status | PR | Story | File |
| --- | --- | --- | --- | --- | --- |
| 43 | Store listing refresh (9 languages) | built | #174 | — | [43-store-listing-refresh.md](docs/plan/slices/43-store-listing-refresh.md) |
| 44 | Pipeline: iOS filter group, platform-neutral Mobile root | planned | — | — | [44-pipeline-ios-split.md](docs/plan/slices/44-pipeline-ios-split.md) |
| 45 | Android: document scanner into the editor | planned | — | — | [45-android-document-scanner.md](docs/plan/slices/45-android-document-scanner.md) |
| 46 | Play button loading state | planned | — | — | [46-play-loading-state.md](docs/plan/slices/46-play-loading-state.md) |
| 47 | Head start: sentence-level audio cache | planned | — | — | [47-head-start-sentence-cache.md](docs/plan/slices/47-head-start-sentence-cache.md) |
| 48 | iOS target and TestFlight from CI | planned | — | — | [48-ios-target-testflight.md](docs/plan/slices/48-ios-target-testflight.md) |
| 49 | iOS reaches Android parity | planned | — | — | [49-ios-parity.md](docs/plan/slices/49-ios-parity.md) |
| 50 | Voice packs, first-run language, slimmer app (Android) | built | #173 | — | [50-voice-packs-first-run-language.md](docs/plan/slices/50-voice-packs-first-run-language.md) |
| 51 | Multilingual voices (Supertonic, Piper fa, MeloTTS zh) | planned | — | — | [51-multilingual-voices.md](docs/plan/slices/51-multilingual-voices.md) |
| 52 | PaddleOCR per language (Android) | planned | — | — | [52-paddleocr-per-language.md](docs/plan/slices/52-paddleocr-per-language.md) |
| 53 | Android: localized UI and RTL | planned | — | — | [53-localized-ui-rtl.md](docs/plan/slices/53-localized-ui-rtl.md) |
| 54 | Android: on-device translation | planned | — | — | [54-on-device-translation.md](docs/plan/slices/54-on-device-translation.md) |
| 55 | iOS gets the Batch 9 features | planned | — | — | [55-ios-batch9-features.md](docs/plan/slices/55-ios-batch9-features.md) |
| 56 | Windows: packs, first-run language, head start | planned | — | — | [56-windows-packs-language-headstart.md](docs/plan/slices/56-windows-packs-language-headstart.md) |
| 57 | Windows: localized UI and translation | planned | — | — | [57-windows-localization-translation.md](docs/plan/slices/57-windows-localization-translation.md) |

Suggested order, in parallel lanes: **A** pipeline + iOS (44 -> 48 -> 49 -> 55);
**B** Android UX (45, 46, 47); **C** packs and languages (50 -> 51 -> 52, 53,
54); **Windows** last (56 -> 57); **43** any time.

**Batch 10 — Dino Reader.** Dino Reader is the second product in this repo (Decision 72): a kids' book
reader that gives each character a consistent, distinct voice. It has a gallery
behind a grown-up gate, a text mode and a picture mode that highlight the
spoken sentence, and per-character voices chosen by on-device rules. Sources:
the phone doc `2026-10-20-dino-reader.md` and a desk refine on 2026-10-20
(Decisions 73-81). Slices live in `docs/plan/slices/` (one file each); this
section is only the index.

| # | Slice | Status | PR | Story | File |
| --- | --- | --- | --- | --- | --- |
| 58 | Dino Reader skeleton: shared Mobile library, new app, pipeline | planned | — | — | [58-dino-reader-skeleton.md](docs/plan/slices/58-dino-reader-skeleton.md) |
| 59 | Dialogue parser and voice caster (pure) | built | #175 | — | [59-dialogue-parser-voice-caster.md](docs/plan/slices/59-dialogue-parser-voice-caster.md) |
| 60 | Reading with a voice per character | planned | — | — | [60-per-character-voice-reading.md](docs/plan/slices/60-per-character-voice-reading.md) |
| 61 | Gallery, stories on device, grown-up gate | planned | — | — | [61-gallery-grownup-gate.md](docs/plan/slices/61-gallery-grownup-gate.md) |
| 62 | Reading screen: text and picture modes | planned | — | — | [62-reading-screen-modes.md](docs/plan/slices/62-reading-screen-modes.md) |
| 63 | Positioned text extraction, kept page images | planned | — | — | [63-positioned-text-extraction.md](docs/plan/slices/63-positioned-text-extraction.md) |
| 64 | Picture mode on real pages | planned | — | — | [64-picture-mode-real-pages.md](docs/plan/slices/64-picture-mode-real-pages.md) |
| 65 | Cast screen | planned | — | — | [65-cast-screen.md](docs/plan/slices/65-cast-screen.md) |
| 66 | Dino Reader languages and translation | planned | — | — | [66-dino-reader-languages-translation.md](docs/plan/slices/66-dino-reader-languages-translation.md) |
| 67 | Dino Reader on iOS and Windows | planned | — | — | [67-dino-reader-ios-windows.md](docs/plan/slices/67-dino-reader-ios-windows.md) |

Suggested order, in lanes: **pure** 59 any time now (no overlap with Batch 9);
**app** 58 -> 60 -> 61 -> 62 -> 65 (58 needs PR #165 and Slice 44); **pictures**
63 (after 45 and 52) -> 64; **languages** 66 after Batch 9 lane C (50-54);
**platforms** 67 after 48/49, Windows last.
