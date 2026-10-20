# Slice 54 — Android: on-device translation

*Planned.*

**Batch:** 9
**Decisions:** 67, 68 (target language), 60
**Depends on:** Slice 50 (language setting); Slice 51 helps (voice per language) but is not required
**Touches:** `src/ReadTheStupidText.Application/Translation/**`, `src/ReadTheStupidText.Application/Settings/ISettingsStore.cs`, `src/ReadTheStupidText.Mobile/**` (`ReaderPage.xaml`, `ReaderPage.xaml.cs`, `MauiProgram.cs`, `ReadTheStupidText.Mobile.csproj`, `MobilePreferencesSettingsStore.cs`, `Platforms/Android/**`), `tests/ReadTheStupidText.Tests/**`, `PRIVACY.md`, `docs/privacy.html`, `CLAUDE.md`
**Story issue:** —

## Goal

Paste or scan text in another language and have it read in the user's own
language. A source dropdown shows the detected language (and can be overridden),
a "Translate to my language" toggle (off by default) controls it, and what gets
read is the translated text. Everything runs on the phone.

## Steps

- [ ] **Interfaces first** in `Application/Translation/` so iOS and Windows reuse
      the flow: `ILanguageDetector` (`Task<DetectedLanguage?> DetectAsync(string text)`,
      returns BCP-47 tag + confidence), `ITranslator` (`Task<string> TranslateAsync(string text,
      string source, string target, CancellationToken)`, `IReadOnlyList<string>
      SupportedLanguages`, `Task<bool> IsModelReadyAsync(...)`), and a pure
      `TranslationFlow` that decides: skip when off, skip when source == target,
      chunk long text by paragraph/sentence (reuse `SpeechTextChunker` splitting),
      translate pieces, rejoin.
- [ ] Android bindings: ML Kit **Language Identification** and **Translation**
      NuGet packages (ids and versions confirmed via context7/NuGet at
      implementation) in `ReadTheStupidText.Mobile.csproj` beside the existing
      `Xamarin.Google.MLKit.*` references (lines 105-106). Unzip the built `.apk`
      to confirm what ships. Implement `MlKitLanguageDetector` and
      `MlKitTranslator` in `Platforms/Android/`; the translator downloads a
      language model on first use (ML Kit's own on-device download, wifi-only
      option) and reports progress.
- [ ] **Farsi check (research step).** Confirm in the ML Kit Translation
      language list whether Persian (`fa`) is supported, and which of the 9
      languages are. Record the supported set here. Unsupported pairs: hide the
      toggle for that language and say why (a gap, not a silent no-op). ML Kit
      translates via English as pivot for many pairs; the reader is told
      translation is casual quality.
- [ ] `ISettingsStore`: add `TranslateEnabled` (default false) and
      `TranslateSourceLanguage` (nullable = auto); implement in
      `MobilePreferencesSettingsStore.cs` (keys beside lines 17-23) and the
      Windows `LocalSettingsStore` (Slice 57 wires the UI there).
- [ ] **UI** on `ReaderPage.xaml`: a source-language `Picker` (Auto + the
      supported languages, in their own names) and a "Translate to <my language>"
      `Switch`, placed under the editor card. When text changes (reuse Slice 47's
      debounce), run detection and set the dropdown to the result unless the user
      overrode it; the user's override sticks until the text is cleared.
- [ ] **Read path.** In `OnPlayPauseTapped` (`ReaderPage.xaml.cs`, lines
      136-160), when the toggle is on and source != target, translate first
      (with the Slice 46 spinner covering the wait), then `SpeakAsync` the
      translated text. `_lastSpokenText` (line 158) must compare against the
      source text so Resume still works. Show the translated text read-only
      beneath the editor, or replace nothing in the editor (the editor keeps the
      original).
- [ ] Failure handling: model not downloaded and offline -> message, read the
      original text; never block reading.
- [ ] Tests (`tests/ReadTheStupidText.Tests`): `TranslationFlow` skip rules,
      chunk/rejoin, with fake `ITranslator`/`ILanguageDetector`.
- [ ] `PRIVACY.md` / `docs/privacy.html`: translation is on-device; ML Kit model
      downloads come from Google. `CLAUDE.md`: the Application-level interfaces.

## Acceptance

- Paste French text: dropdown auto-sets to French; with the toggle on and
  language set to English, Play reads English; with it off, reads French.
- Overriding the dropdown to a wrong language is respected.
- Airplane mode after the model is cached: still translates. First use offline:
  clear message and the original is read.
- A 5,000-word text translates without blocking the UI.

## Risks / notes

- ML Kit's model download is Google's network use, not ours; disclose it.
- Translation quality is casual; do not claim otherwise in the listing.
- Core-filter files (`Application/**`, `Tests/**`) rebuild all platforms; expected.
