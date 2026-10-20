# Slice 51 — Multilingual voices (Supertonic, Piper Farsi, MeloTTS Chinese)

*Planned.*

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

- [ ] **Research (before code), via `library-docs` / context7
      `/k2-fsa/sherpa-onnx`:** Supertonic-3's multilingual config (how the
      language is chosen: a generation option vs separate model; speaker ids per
      language), the Piper VITS config for sherpa (`OfflineTtsVitsModelConfig`:
      model, tokens, data dir for espeak-ng), and MeloTTS zh_en. Record answers
      in this file.
- [ ] **Farsi licence decision.** Confirm the licence of the `fa_IR` Piper
      voices (gyro vs amir), including the training dataset's terms, and pick one
      only if redistribution is allowed. If neither is, Farsi falls back to the
      phone voice; write the gap in `STORE.md`/plan outstanding work.
- [ ] **Language in the engine contract.** `ISpeechReader.SpeakAsync(string text,
      int? activityId = null)` (`Application/Reading/ISpeechReader.cs`, line 46)
      has no language. Add a `string language` parameter (or a `SpeakRequest`
      record) and update every implementer: `Infrastructure/Reading/SpeechReader.cs`,
      `SupertonicSpeechReader.cs`, `CompositeSpeechReader.cs`, and the Mobile
      readers `AndroidSpeechReader`, `AndroidSupertonicSpeechReader`,
      `Mobile/CompositeSpeechReader.cs`, plus `ReadAloudService` callers
      (the single `_reader.SpeakAsync` call, line 560, inside `ReadEntryAsync`) and `ReaderPage.xaml.cs` line 159. Windows passes the
      stored language (default `en`) until Slice 56.
- [ ] **Voice catalog per language.** `SupertonicVoiceTable` is a static English
      table of 10 voices with `VoiceInfo(Id, DisplayName, Language)`
      (`Domain/Reading/VoiceInfo.cs`; `SupertonicVoiceTable.cs` lines 28-33 hardcode
      `"en"`). Add an `IVoiceCatalogSource` fed by installed packs' `pack.json`
      (voices with id, display name, language, engine kind). Keep persisted ids
      stable (`supertonic:M1` etc.). Update `VoicePickerPage.xaml.cs` (lines
      32-37 enumerate `SupertonicVoiceTable.Voices`) to list voices for the
      current language, grouped by engine.
- [ ] **Engine selector.** Today `Mobile/CompositeSpeechReader.cs` picks between
      one neural reader and one fallback on `IVoiceModelService.IsReady`
      (line 49), and `IVoiceModelService`/`VoiceModelPaths` assume a single model
      directory. Generalize: select by (language, voice) -> engine (Supertonic,
      Vits/Piper, Melo) -> installed pack -> else phone voice. One reader class
      per engine config kind sharing the chunk/playback loop (extract the base
      from Slice 49 if done, else from `AndroidSupertonicSpeechReader`).
- [ ] **Packs.** Extend `packs.yml` to build and publish: the Supertonic pack
      (already in Slice 50, now carrying per-language voice metadata), the Piper
      `fa_IR` pack (model, tokens, espeak-ng-data, LICENSE, SOURCE link) and the
      MeloTTS `zh_en` pack. The GPL espeak-ng data lives **only** inside the Farsi
      pack zip, never in the repo, MSIX, AAB or `.ipa` (Decision 63).
- [ ] Update `CLAUDE.md`: rewrite the "Do **not** add GPL/LGPL components"
      rule (Versioning, license & releases) into the scoped exception; replace
      the "model ships inside the package" rule and the "Piper is GPL — do not
      use it" sentence; update the Supertonic voice-table notes (voices per
      language). `STORE.md` *Third-party components and licenses* gets Piper,
      espeak-ng (GPL-3.0, with source link) and MeloTTS entries. `PRIVACY.md`
      third-party list likewise.
- [ ] Tests: pure voice-selection logic (language -> engine/pack/fallback) in
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
