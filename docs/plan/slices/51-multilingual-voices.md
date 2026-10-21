# Slice 51 — Multilingual voices (Supertonic, Piper Farsi, MeloTTS Chinese)

*Built (wave 2026-10-21b), PR #197.*

**Batch:** 9
**Decisions:** 63 (also 16, 60-62)
**Depends on:** Slice 50 (pack service, `packs.json`, first-run language)
**Touches:** `src/ReadTheStupidText.Domain/Reading/**`, `src/ReadTheStupidText.Application/Reading/**`, `src/ReadTheStupidText.Mobile/**`, `src/ReadTheStupidText.Infrastructure/Reading/NeuralVoiceCatalog.cs`, `.github/workflows/packs.yml`, `STORE.md`, `PRIVACY.md`, `CLAUDE.md`
**Story issue:** —

## Goal

Read text aloud in the user's language with a natural voice: English, French,
Spanish, Portuguese, Russian, Japanese and Arabic through Supertonic, Farsi
through a Piper voice, Chinese through MeloTTS.

## Steps

- [x] **Research (before code), via `library-docs` / context7
      `/k2-fsa/sherpa-onnx`:** Supertonic-3's multilingual config (how the
      language is chosen: a generation option vs separate model; speaker ids per
      language), the Piper VITS config for sherpa (`OfflineTtsVitsModelConfig`:
      model, tokens, data dir for espeak-ng), and MeloTTS zh_en. Record answers
      in this file.
- [x] **Farsi licence decision.** Confirm the licence of the `fa_IR` Piper
      voices (gyro vs amir), including the training dataset's terms, and pick one
      only if redistribution is allowed. If neither is, Farsi falls back to the
      phone voice; write the gap in `STORE.md`/plan outstanding work.
- [~] **Language in the engine contract.** `ISpeechReader.SpeakAsync(string text,
      int? activityId = null)` (`Application/Reading/ISpeechReader.cs`, line 46)
      has no language. Add a `string language` parameter (or a `SpeakRequest`
      record) and update every implementer: `Infrastructure/Reading/SpeechReader.cs`,
      `SupertonicSpeechReader.cs`, `CompositeSpeechReader.cs`, and the Mobile
      readers `AndroidSpeechReader`, `AndroidSupertonicSpeechReader`,
      `Mobile/CompositeSpeechReader.cs`, plus `ReadAloudService` callers
      (the single `_reader.SpeakAsync` call, line 560, inside `ReadEntryAsync`) and `ReaderPage.xaml.cs` line 159. Windows passes the
      stored language (default `en`) until Slice 56.
- [x] **Voice catalog per language.** `SupertonicVoiceTable` is a static English
      table of 10 voices with `VoiceInfo(Id, DisplayName, Language)`
      (`Domain/Reading/VoiceInfo.cs`; `SupertonicVoiceTable.cs` lines 28-33 hardcode
      `"en"`). Add an `IVoiceCatalogSource` fed by installed packs' `pack.json`
      (voices with id, display name, language, engine kind). Keep persisted ids
      stable (`supertonic:M1` etc.). Update `VoicePickerPage.xaml.cs` (lines
      32-37 enumerate `SupertonicVoiceTable.Voices`) to list voices for the
      current language, grouped by engine.
- [x] **Engine selector.** Today `Mobile/CompositeSpeechReader.cs` picks between
      one neural reader and one fallback on `IVoiceModelService.IsReady`
      (line 49), and `IVoiceModelService`/`VoiceModelPaths` assume a single model
      directory. Generalize: select by (language, voice) -> engine (Supertonic,
      Vits/Piper, Melo) -> installed pack -> else phone voice. One reader class
      per engine config kind sharing the chunk/playback loop (extract the base
      from Slice 49 if done, else from `AndroidSupertonicSpeechReader`).
- [x] **Packs.** Extend `packs.yml` to build and publish: the Supertonic pack
      (already in Slice 50, now carrying per-language voice metadata), the Piper
      `fa_IR` pack (model, tokens, espeak-ng-data, LICENSE, SOURCE link) and the
      MeloTTS `zh_en` pack. The GPL espeak-ng data lives **only** inside the Farsi
      pack zip, never in the repo, MSIX, AAB or `.ipa` (Decision 63).
- [ ] (left to the index pass, see *For CLAUDE.md* below) Update `CLAUDE.md`: rewrite the "Do **not** add GPL/LGPL components"
      rule (Versioning, license & releases) into the scoped exception; replace
      the "model ships inside the package" rule and the "Piper is GPL — do not
      use it" sentence; update the Supertonic voice-table notes (voices per
      language). `STORE.md` *Third-party components and licenses* gets Piper,
      espeak-ng (GPL-3.0, with source link) and MeloTTS entries. `PRIVACY.md`
      third-party list likewise.
- [x] Tests: pure voice-selection logic (language -> engine/pack/fallback) in
      `tests/ReadTheStupidText.Tests`.

## Acceptance

- Change the language in settings: the voice picker lists that language's
  voices; with the pack installed, a sample reads natively (check each of the 9
  languages by ear; record which engine each uses).
- Farsi reads with the Piper voice, or the documented fallback is in place.
- With a pack missing, the phone voice reads and the picker says why.
- Unzipping the app package shows no espeak-ng data; it exists only in the
  downloaded Farsi pack.

## Risks / notes

- The GPL exception is deliberate and scoped; get the wording in `CLAUDE.md` exact.
- Arabic and Japanese quality in Supertonic needs a listening check.
- Changing `ISpeechReader` touches core code, so every platform rebuilds.

## Research answers (context7 `/k2-fsa/sherpa-onnx`, the sherpa-onnx repo's C# sources, 1.13.3)

- **Supertonic-3 language:** one multilingual model; the language is a *generation
  option*, not a separate model. `OfflineTtsGenerationConfig.Extra["lang"] = "<code>"`
  (31 codes incl. `en fr es pt ru ja ar`), passed through `OfflineTts.GenerateWithConfig`.
  The plain `Generate(text, speed, sid)` sends no `lang`, so before this slice every
  Android read was synthesized as the default language. Speakers are the same ten styles
  (`sid` 0..9) for every language.
- **Piper (VITS):** `config.Model.Vits.Model`, `.Tokens`, `.DataDir` (the `espeak-ng-data`
  folder); noise/length scales keep their defaults; single speaker `sid 0`.
- **MeloTTS `zh_en`:** `config.Model.Vits.Model`, `.Lexicon`, `.Tokens`, `.DictDir`
  (jieba `dict/`), plus `config.RuleFsts` = `date.fst,number.fst`; 1 female speaker,
  44.1 kHz, MIT. English words are spoken only if they are in `lexicon.txt`.

## Decisions taken during build

- **Farsi voice: `fa_IR-amir-medium` (int8, ~20 MB).** Its dataset licence is stated as
  CC0 (model card). `gyro`'s card says only "See URL" (not clearly redistributable), so it
  is rejected. Residual risk: amir is finetuned from the Piper US-English *lessac* voice;
  re-check the lessac dataset terms before a wide launch (written in `STORE.md`).
- **`ISpeechReader` is not changed** (Slice 60 owns `ISpeechReader.cs` and both
  `CompositeSpeechReader`s). The language reaches the engine through the stored language
  instead: `MobileVoiceModelService` resolves the installed pack for `ISettingsStore.Language`
  and hands the reader a richer `VoiceModelPaths` (engine kind, language, voices).
  `IVoiceModelService.IsReady` therefore means "a pack for the *current language* is
  installed", which is exactly the existing composite routing (neural, else phone voice).
  If a later slice wants `SpeakAsync(text, language)`, `VoiceModelPaths.Language` is the value.
- **No `IVoiceCatalogSource` interface:** the pure `InstalledVoiceCatalog.Resolve` over
  `IVoicePackService.GetInstalledVoicePacks()` does the job and is unit-tested; a second
  interface would only wrap it. `VoiceInfo` gained `Engine` and `SpeakerId` (defaults keep
  every old call site valid); persisted ids (`supertonic:M1`) are unchanged.
- **Supertonic pack lists no voices in `pack.json`**: its ten styles stay the single
  `SupertonicVoiceTable`, offered for every language the pack serves. Piper and Melo
  packs list theirs (`voices`, `model` keys in `pack.json`).
- **One engine instance, rebuilt on a pack change** *(superseded, see *Final shape*: engines are now ref-counted `BuiltEngine`s captured per read, not parked and disposed with the reader)*: `AndroidSupertonicSpeechReader`
  keeps its name and chunk/playback loop and builds a Supertonic, Piper or Melo
  `OfflineTts` from `VoiceModelPaths.Kind` (a second reader class would have duplicated
  the loop Slice 60 is editing). The previous engine is parked and disposed with the reader.
- **`PackConstants.CatalogUrl` still pins `packs-1`.** The new packs exist only after the
  `packs` workflow publishes `packs-2`; pointing the app at it before that would break the
  catalog fetch. After publishing, a one-line follow-up flips the pin and Persian/Chinese
  light up (until then those languages read with the phone voice, as in Slice 50).
- **Sentence splitting:** `SpeechTextChunker` now also breaks after the Arabic/Persian
  question mark and Urdu full stop and the CJK `。！？` when whitespace follows. Unspaced
  CJK text still has no break points (it synthesizes as one chunk): a follow-up, because
  `SplitWithRanges` maps chunks by whitespace-word count. *(Superseded, see *Final shape*: unspaced CJK now wraps by character count.)*
- **Windows keeps the English Supertonic model** and is untouched until Slice 56.

## Final shape (as built)

*Changed while building:* the build decisions above held, except where marked superseded.

- **Selection:** `InstalledVoiceCatalog` resolves the installed pack serving `ISettingsStore.Language`. `PackManifest` gained optional `model` and `voices` keys (no `model` means Supertonic).
- **`MobileVoiceModelService`:** `Paths` is cached and its getter does no file-system access. A background scan is triggered by `ILanguageChangeNotifier.LanguageChanged` (raised by `MobilePreferencesSettingsStore`). `RefreshUntilCurrent` loops until the scanned language matches the stored one. `ReadyChanged` fires on every readiness or model change.
- **`AndroidSupertonicSpeechReader`:** holds an immutable `BuiltEngine(RefCountedTts, VoiceModelPaths)` captured per read; engines are ref-counted and disposed when the last holder releases them. It builds Supertonic configs (`Extra["lang"]` via `GenerateWithConfig`), Piper/VITS configs (espeak-ng data dir) and MeloTTS configs (phone, date and number rule FSTs). It warms up on the first ready, observes and disposes abandoned generations, and `RequireModelFiles` checks model files before construction. `NeuralEngineUnavailableException` makes the composite fall back to the phone voice.
- **Voice memory per engine family:** `VoiceChoices` and `NeuralVoiceMemory`, keys `LastNeuralVoiceId.<kind>`. Picking a Farsi voice no longer overwrites the English one.
- **`SpeechTextChunker`:** breaks after fullwidth 。！？ with no whitespace, keeping trailing closers; wraps unspaced CJK by character count; `SplitWithRanges` matches characters and throws on a mismatch.
- **`VoicePickerPage`:** lists the current language's voices, refreshes live, uses shared Styles.
- **`packs.yml`:** builds `supertonic-3`, `piper-fa` (espeak-ng GPL data inside that zip only, with its LICENSE and SOURCE) and `melo-zh-en` into one `packs-<n>` release. The files lists exclude only `pack.json`, and optional docs are guarded.

*From review:* high pass 10 findings, medium pass 10 findings, all fixed.

*Bug found on the emulator, fixed:* the config helpers took `OfflineTtsConfig` (a struct) by value, so the model paths were dropped. The native engine was null ("tts is nullptr") and every English read failed. The helpers now take it by `ref`, and `RequireModelFiles` guards construction. **Lesson:** sherpa-onnx C# configs are structs; never pass them by value to a helper that fills them in.

*Emulator results (RTST_Test, 2026-10-21):* with the language stored as `fa` and no Farsi pack, the voice row shows "Phone voice" (the correct fallback). With `en` it shows "Momonga". An English read plays after the fix. The timer bug (elapsed > total) is pre-existing on main and not part of this slice.

#### For CLAUDE.md

- Voices download as packs per language. The installed pack serving `ISettingsStore.Language` is resolved by `InstalledVoiceCatalog`. Supertonic covers en/fr/es/pt/ru/ja/ar, Piper (`piper-fa`) covers fa, MeloTTS (`melo-zh-en`) covers zh. (Slice 51: docs/plan/slices/51-multilingual-voices.md)
- sherpa-onnx config types (`OfflineTtsConfig` etc.) are structs: pass them by `ref` to any helper that fills them in, or the paths are silently dropped and the engine is null. (Slice 51: docs/plan/slices/51-multilingual-voices.md)
- Engine lifecycle in `AndroidSupertonicSpeechReader`: `BuiltEngine`/`RefCountedTts`, captured per read, disposed when the last holder releases. (Slice 51: docs/plan/slices/51-multilingual-voices.md)
- espeak-ng GPL data exists only inside the downloaded `piper-fa` pack, never in the repo or an app package. (Slice 51: docs/plan/slices/51-multilingual-voices.md)
- `PackConstants.CatalogUrl` pins the packs release. (Slice 51: docs/plan/slices/51-multilingual-voices.md)

#### For README.md

Android reads with natural voices in nine languages: Supertonic for most, Piper for Farsi and MeloTTS for Chinese. Each downloads as a pack for the chosen language.

#### Not yet verified

- The packs workflow run with `release_number=2` and all three versions set to `1`, then a one-line PR flipping `PackConstants.CatalogUrl` to `packs-2`. Until then Farsi and Chinese read with the phone voice.
- Farsi and Chinese reads heard on a device, after packs-2.
- Arabic and Japanese Supertonic quality, by ear.
- French spoken as French (the `lang` option), by ear.
- `fa_IR-amir-medium` dataset licence terms (derived from lessac) re-checked before a wide launch.
