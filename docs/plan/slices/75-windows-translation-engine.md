# Slice 75 — Windows translation engine

*In progress (wave 2026-10-21), draft PR.*

**Batch:** 9
**Decisions:** 67, 68
**Depends on:** Slice 57 (localized UI and the inert translate-before-read flow)
**Touches:** `src/ReadTheStupidText.Infrastructure/Translation/**`, `src/ReadTheStupidText.Infrastructure/ReadTheStupidText.Infrastructure.csproj`, `src/ReadTheStupidText.App/**` (only to unhide the translate UI and DI wiring), `src/ReadTheStupidText.Application/Translation/**`, `src/ReadTheStupidText.Application/Reading/ReadAloudService.cs`, `.github/workflows/packs.yml`, `packs.json`, `tests/ReadTheStupidText.Tests/**`, `STORE.md`, `PRIVACY.md`
**Story issue:** —

## Goal

Make the hidden translate-before-read toggle on Windows work fully offline. Slice 57 shipped the flow (`ReadTranslator`, `TranslationFlow`, toggle, source picker, note, tray item) but `PackTranslator.IsAvailable` is false and `TranslateAsync` always raises `TranslationModelUnavailableException`, so the UI is hidden.

## Steps

- [x] **Translation research (before code).** Compare Opus-MT ONNX models
      (Helsinki-NLP/OPUS-MT, via onnxruntime, with pivot through English) with
      Bergamot (Firefox translation models, own native runtime): licence of
      engine and each pair's model, whether `fa` is covered in both directions,
      per-pair size (target < ~100 MB), CPU speed, and tokenizer needs
      (SentencePiece). Recommended default per Decision 67 is Opus-MT ONNX; record
      the decision and numbers here and in `plan.md`'s Decisions if it changes.
- [x] **Translation packs.** Ship translation models as packs (kind `translation`)
      through the Slice 50/56 pack service and `packs.yml`; `packs.json` lists
      `xx -> en` and `en -> xx` pairs for the user's language (pivot through English).
- [x] **A real `PackTranslator.TranslateAsync`** (ONNX inference plus SentencePiece), with
      `IsAvailable` true once the pair's pack is present, which unhides the toggle, source
      picker, note and tray item.
- [x] **Translate the first sentences first.** Translation adds latency before the first word and
      Slice 47's head start does not cover it, so translate the first sentences first to keep
      time-to-first-audio low.
- [x] `STORE.md` third-party notices for the chosen models; `PRIVACY.md` check (still 100% offline, only packs are downloaded).

## Research result (2026-10-21)

**Decision: Opus-MT ONNX on onnxruntime (Decision 67's default stands); Bergamot rejected.**

- **Bergamot:** engine and Mozilla models are MPL-2.0, but the engine is a native C++ library with
  no .NET binding (we would build and ship x64 + ARM64 DLLs in CI), and Persian is a Firefox
  "development" language (Nightly-only models). More build risk for no coverage gain.
- **Chosen packs:** the Xenova int8 conversions with the merged decoder and key/value cache, 13 packs: to English
  from fr, es, zh, ar, ru, ja, pt; from English to fr, es, pt, zh, ar, ru. About 110 MB per leg. Target tokens:
  pt uses the ROMANCE model with `>>pt<<`, zh `>>cmn_Hans<<`, ar `>>ara<<`. Licence CC-BY-4.0.
- **Opus-MT (Helsinki-NLP, Marian):** pre-converted int8 ONNX exists in the `Xenova/opus-mt-*` repos
  (`encoder_model_quantized.onnx` ~50 MB + `decoder_model_quantized.onnx` ~57 MB, no-cache decoder).
  Licence: CC-BY-4.0 per OPUS-MT (the Hugging Face cards say Apache-2.0; we ship the stricter notice). Weights are
  Marian transformer-base; tokenizer is a SentencePiece unigram `source.spm` plus a shared `vocab.json`.
- **Per leg (encoder + decoder int8):** fr-en/en-fr 107 MB, es 113, zh 113, ar 110, ru 110, ja-en 108,
  pt via `opus-mt-ROMANCE-en` / `en-ROMANCE` 113 (target token `>>pt<<`). Slightly over the ~100 MB
  target; a pair not touching English needs two legs (about 215-225 MB).
- **Coverage:** into English: fr, es, pt, zh, ar, ru, ja (7). From English: fr, es, pt, zh (`>>cmn_Hans<<`
  on `en-zh`), ar (`>>ara<<` on `en-ar`), ru (6). **Unsupported, so `SupportsPair` is false:**
  - **Persian both ways.** No `opus-mt-fa-en`/`en-fa`; the multilingual `mul-en`/`en-mul` score
    Tatoeba BLEU 7.5 (fas->eng) and 6.9 (eng->fas), unusable. Firefox/Bergamot has fa only in development.
  - **English -> Japanese.** `opus-mt-en-jap` is Bible-trained and `en-mul` scores BLEU 0.0 for jpn.
    `Helsinki-NLP/opus-tatoeba-en-ja` (Apache-2.0, BLEU 15.2) has no ONNX conversion: a follow-up could
    export and quantise it in `packs.yml` (needs optimum in CI).
- **Engine:** `Microsoft.ML.OnnxRuntime.Managed` 1.24.4 (managed only, paired with sherpa's native `onnxruntime.dll` 1.24.4,
  so no second `onnxruntime.dll` and the App `RemoveWinMlOnnxRuntimeDuplicate` target is unchanged),
  `Microsoft.ML.Tokenizers` 2.0.1 `SentencePieceTokenizer` for `source.spm`, then `vocab.json` for ids.
  Greedy decoding (beam search not needed for casual quality). Real run, fr->en: "Le chat dort sur le
  canapé. Il fait beau aujourd'hui." -> "The cat sleeps on the couch. It's beautiful today."
- **Pack layout (`kind: translation`, id `translation-<from>-<to>`, catalogue `language` = `<from>-<to>`):**
  `encoder.onnx`, `decoder.onnx`, `source.spm`, `vocab.json`, `translation.json` (decoding ids and target
  token, copied from `config.json`), `LICENSE`, `SOURCE`, `pack.json`.

## Speed and streaming (2026-10-21)

- **Measured** (fr->en, 208-word paragraph, 16 cores, Debug build, warm): no-cache decoder first sentence
  0.65 s, whole text 6.8 s; merged decoder with key/value cache (now the pack's `decoder.onnx`) 0.4 s and 4.7 s.
  Thread count barely matters (1 thread 5.7 s, 8 threads slower): the 59k-wide output projection per token
  dominates, so batching sentences would be the next lever, not threads. Speech of 208 words lasts about 80 s,
  so with streaming the whole text finishes translating long before it is spoken.
- **Streaming:** `ISpeechReader.SupportsStreaming/SpeakStreamAsync/AppendText/CompleteStream` (defaults: off).
  The Supertonic reader's chunk list became a `ChunkFeed` (growable, completes on `CompleteStream`); the
  playback loop waits on the feed, a pump starts synthesis of each appended chunk at once, and
  `ReadTimingTracker.Extend/SetOpen` keep the total unknown until the stream closes. `ReadAloudService` (partial
  `.Streaming.cs`) translates `TranslationBlocks` (a one or two sentence head, then ~600 char blocks) with a 20 s
  budget per block; a failed block and the rest are read in the original. The WinRT fallback does not stream:
  it gets the whole translated text as before.
- **Packs on demand:** `TranslationPackInstaller` downloads missing legs through `IVoicePackService`; the toggle
  fetches `en -> ui`, a read needing another leg starts it in the background and reads the original
  (`TranslationOutcome.PackDownloading`). `IsAvailable` means the engine can serve the UI language.

## Built (2026-10-21)

- **Pipeline:** `PackTranslator` / `MarianEngine` / `MarianTokenizer` / `GreedyDecoder`. Legs load lazily, reload when
  the pack changes on disk, and dispose waits for in-flight inference.
- **Packs:** `TranslationPackInstaller` (toggle on downloads `translation-en-<ui>`; another source leg downloads in
  the background while the original is read, `PackDownloading`). `packs.yml` builds the 13 packs from one
  `TRANSLATION_PAIRS` list with sha256 pins; `packs-3` also re-ships every `packs-2` voice and OCR pack at version 1.
  `PackConstants.CatalogUrl` now points to `packs-3` for all platforms.
- **Streaming:** `ISpeechReader.SpeakStreamAsync/AppendText/CompleteStream`, `ChunkFeed`, `ReadTimingTracker.Extend/SetOpen`,
  `ReadText.Extends`, `TranslationBlocks` (head of 1-2 sentences, then ~600-char blocks, 20 s budget per block, original
  from the first failed block). `CompositeSpeechReader` falls back to the whole text through the Windows voice.
  `ReadChunk.MatchesSource` guards against stale chunks.
- **Shared helpers:** one sentence splitter, `TranslationFlow.SplitSentences`; `ScriptLanguageDetector` is stricter for
  Cyrillic and Arabic script.
- **Speed:** 208 words fr->en, first sentence 0.4 s, whole text 4.7 s.

*Changed beyond Touches (accepted):* `Application/Reading` (`ISpeechReader`, `ReadText`, `ReadChunk`, `PackKind`,
`PackConstants`, `ReadAloudService.Streaming.cs`), `Infrastructure/Reading` (`SupertonicSpeechReader`,
`CompositeSpeechReader`, `ChunkFeed`, `ReadTimingTracker`), `site/privacy.html`, `docs/voice-packs.md`.

*From review:* two passes (10 + 3 findings fixed; the rest belonged to already-merged code). Tests: 653 pass, including
the real-model end-to-end tests for fr->en and en->pt.

#### For CLAUDE.md

Into `src/ReadTheStupidText.Infrastructure/CLAUDE.md`:

- Windows translation is Opus-MT ONNX in `translation-<from>-<to>` packs, pivoting through English. `PackTranslator`
  loads legs lazily and must dispose under its gate. Translated reads stream: the head of 1-2 sentences speaks while later
  blocks are appended through `ISpeechReader.AppendText`; extend `ReadTimingTracker` inside `ChunkFeed`'s lock. Each block
  has a 20 s budget, and failure falls back to the original from that block on. fa and ja targets are unsupported, so the
  toggle stays hidden. `PackConstants.CatalogUrl` is shared by every platform: a new `packs-N` release must re-ship every
  existing pack at the same versions. (Slice 75: docs/plan/slices/75-windows-translation-engine.md)

#### For README.md

Windows can translate what it reads into your language, fully offline (fr/es/pt/zh/ar/ru, plus to English from ja).

#### Not yet verified

- Desktop acceptance (blocked: the session was locked) of toggle -> pack download -> translated read streaming.
- `packs-3` contents after the workflow run.
- The Android/iOS apps still find their packs in `packs-3`.

## Notes

- Auto-read must not stall: the 20 s budget and fall-back to the original text from Slice 57 stay.
- Split out of Slice 57 on 2026-10-21; the exact original wording is in `docs/plan/slices/57-windows-localization-translation.md` (steps marked "-> Slice 75").
