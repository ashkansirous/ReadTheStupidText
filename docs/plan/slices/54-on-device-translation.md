# Slice 54 — Android: on-device translation

*Built (wave 2026-10-21b), PR #199.*

**Batch:** 9
**Decisions:** 67, 68 (target language), 60
**Depends on:** Slice 50 (language setting); Slice 51 helps (voice per language) but is not required
**Touches:** `src/ReadTheStupidText.Application/Translation/**`, `src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs`, `src/ReadTheStupidText.Infrastructure/Settings/LocalSettingsStore.cs`, `src/ReadTheStupidText.Mobile.Shared/MobilePreferencesSettingsStore.cs`, `src/ReadTheStupidText.Application/Settings/ISettingsStore.cs`, `src/ReadTheStupidText.Mobile/**` (`ReaderPage.xaml`, `ReaderPage.xaml.cs`, `MauiProgram.cs`, `ReadTheStupidText.Mobile.csproj`, `MobilePreferencesSettingsStore.cs`, `Platforms/Android/**`), `tests/ReadTheStupidText.Tests/**`, `PRIVACY.md`, `site/privacy.html`, `CLAUDE.md`
**Story issue:** —

## Goal

Paste or scan text in another language and have it read in the user's own
language. A source dropdown shows the detected language (and can be overridden),
a "Translate to my language" toggle (off by default) controls it, and what gets
read is the translated text. Everything runs on the phone.

## Steps

- [x] **Interfaces first** in `Application/Translation/` so iOS and Windows reuse
      the flow: `ILanguageDetector` (`Task<DetectedLanguage?> DetectAsync(string text)`,
      returns BCP-47 tag + confidence), `ITranslator` (`Task<string> TranslateAsync(string text,
      string source, string target, CancellationToken)`, `IReadOnlyList<string>
      SupportedLanguages`, `Task<bool> IsModelReadyAsync(...)`), and a pure
      `TranslationFlow` that decides: skip when off, skip when source == target,
      chunk long text by paragraph/sentence (reuse `SpeechTextChunker` splitting),
      translate pieces, rejoin.
- [x] Android bindings: ML Kit **Language Identification** and **Translation**
      NuGet packages (ids and versions confirmed via context7/NuGet at
      implementation) in `ReadTheStupidText.Mobile.csproj` beside the existing
      `Xamarin.Google.MLKit.*` references (lines 105-106). Unzip the built `.apk`
      to confirm what ships. Implement `MlKitLanguageDetector` and
      `MlKitTranslator` in `Platforms/Android/`; the translator downloads a
      language model on first use (ML Kit's own on-device download, wifi-only
      option) and reports progress.
- [x] **Farsi check (research step).** Confirm in the ML Kit Translation
      language list whether Persian (`fa`) is supported, and which of the 9
      languages are. Record the supported set here. Unsupported pairs: hide the
      toggle for that language and say why (a gap, not a silent no-op). ML Kit
      translates via English as pivot for many pairs; the reader is told
      translation is casual quality.
- [x] `ISettingsStore`: add `TranslateEnabled` (default false) and
      `TranslateSourceLanguage` (nullable = auto); implement in
      `MobilePreferencesSettingsStore.cs` (keys beside lines 17-23) and the
      Windows `LocalSettingsStore` (Slice 57 wires the UI there).
- [x] **UI** on `ReaderPage.xaml`: a source-language `Picker` (Auto + the
      supported languages, in their own names) and a "Translate to <my language>"
      `Switch`, placed under the editor card. When text changes (reuse Slice 47's
      debounce), run detection and set the dropdown to the result unless the user
      overrode it; the user's override sticks until the text is cleared.
- [x] **Read path.** In `OnPlayPauseTapped` (`ReaderPage.xaml.cs`, lines
      136-160), when the toggle is on and source != target, translate first
      (with the Slice 46 spinner covering the wait), then `SpeakAsync` the
      translated text. `_lastSpokenText` (line 158) must compare against the
      source text so Resume still works. Show the translated text read-only
      beneath the editor, or replace nothing in the editor (the editor keeps the
      original).
- [x] Failure handling: model not downloaded and offline -> message, read the
      original text; never block reading.
- [x] Tests (`tests/ReadTheStupidText.Tests`): `TranslationFlow` skip rules,
      chunk/rejoin, with fake `ITranslator`/`ILanguageDetector`.
- [x] `PRIVACY.md` / `site/privacy.html`: translation is on-device; ML Kit model
      downloads come from Google. `CLAUDE.md`: the Application-level interfaces (left to `update-docs`, not edited on this branch).

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

## Built (implementation notes)

**Farsi check result.** ML Kit Translation (Xamarin.Google.MLKit.Translate 117.0.3.9) supports
Persian (`fa`). All nine app languages are supported: en, fa, fr, es, pt, zh, ar, ru, ja (59 languages
in all; non-English pairs pivot through English). Language Identification (Xamarin.Google.MLKit.Language.Id
117.0.6.9) reports BCP-47 tags (`zh`, `ja-Latn`, `und` for unknown); `TranslationFlow.NormalizeTag` keeps
the primary subtag. If a target language were ever unsupported, the controls hide and a note says so
(`ShowTargetUnsupported`); none of the nine triggers it today.

**APK native-library check** (Debug build, x86_64 AVD ABI, `obj/Debug/net10.0-android/android/bin/*.apk`
unzipped): the new libs are `lib/x86_64/libtranslate_jni.so` (17.4 MB) and
`lib/x86_64/liblanguage_id_l2c_jni.so` (1.3 MB), both ML Kit's own. No wrong-OS libraries; the other `.so`
files are the .NET runtime and the existing OCR pipeline. No translation model ships in the APK: language
models are fetched by ML Kit at runtime. The language-id model is bundled in its native library. Expect
roughly the same two libraries per ABI in the arm64 Release.

## Decisions taken during build

- **Packages:** the bundled `Xamarin.Google.MLKit.Language.Id` (not the Play-services variant) so detection
  works with no Play services; `Xamarin.Google.MLKit.Translate` for translation.
- **No Wi-Fi-only download.** The model is fetched when the user turns translation on and plays, so
  requiring Wi-Fi would make that tap fail on mobile data. One constant (`MlKitTranslator.RequireWifi`)
  switches it. ML Kit exposes no download progress, so the UI shows an indeterminate spinner and a
  "Downloading the translation model" note instead of a percentage.
- **Source dropdown** lists the nine app languages (own names) first, then the other ML Kit languages
  (names from the system), so a German text is detected and translated too. Item 0 is "Auto-detect";
  detection selects the detected language in the picker without recording an override. Picking a language
  records `TranslateSourceLanguage`; picking Auto or clearing the editor clears it. The override is also
  cleared at startup when the editor is empty.
- **Debounce:** the reader has no Slice 47 debounce to reuse, so detection uses its own 600 ms
  `Task.Delay` + cancellation.
- **Chunking:** *(superseded, see "Final shape" below)* paragraphs split on blank lines, each split with
  `SpeechTextChunker.Split` (no first-chunk bias), pieces rejoined with a space inside a paragraph and a blank
  line between paragraphs.
- **Translated text** is shown read-only (up to four lines) under the toggle; the editor keeps the original.
- **Failure:** missing model/offline shows an alert and the original is read; any other translation error
  likewise. Playback is never blocked.
- `ShouldTranslate` skips when the detected or chosen source is unknown (no confident detection), not just
  when it equals the target.
- Strings: 11 `Translate_*` keys, machine-translated into the eight languages and flagged for a
  native-speaker read. *(Superseded: 12 keys in the final shape.)*
- Files outside the Touches line that had to change: `src/ReadTheStupidText.Infrastructure/Settings/LocalSettingsStore.cs`
  (the spec asks for the Windows store implementation; the interface change would not compile without it).
  `TextSanitizerTests.cs` and `ReadAloudServiceGeneratingTests.cs` fakes got the two new properties.

## Final shape (after review)

- **Application/Translation:** `ILanguageDetector` (takes a `CancellationToken`), `ITranslator`,
  `DetectedLanguage`, `TranslationModelUnavailableException`, and a pure `TranslationFlow`:
  - skip rules: off, same language, unknown source, unsupported pair;
  - per-paragraph sequential translation; paragraphs over 2,000 characters are split at `. ! ? ؟ ۔ 。 ！ ？`,
    reusing `SpeechTextChunker.SplitParagraphs` (now `internal`);
  - real errors surface as themselves;
  - `DetectionSample` (first 1,000 characters) and `Prefix` (the 600-character preview).
- **Settings:** `TranslateEnabled` (default off) and `TranslateSourceLanguage` (null means auto) in
  `ISettingsStore`, `MobilePreferencesSettingsStore` and the Windows `LocalSettingsStore`.
- **Android packages:** `Xamarin.Google.MLKit.Language.Id` 117.0.6.9 (bundled model, no Play services) and
  `Xamarin.Google.MLKit.Translate` 117.0.3.9.
- **`MlKitLanguageDetector` / `MlKitTranslator`:** one shared download per language pair; a cancelled caller
  stops waiting; when neither side is English the English pivot model is required too. Both are registered in
  `MauiProgram.Android.cs`.
- **ReaderPage translate card** (`ReaderPage.Translation.cs`): debounced detection; Play awaits a fresh
  detection if needed; a source picker whose manual override clears when the text is replaced or emptied; the
  toggle; a read-only preview; a "Downloading the translation model" note per read id; failure or an empty
  result shows an alert and reads the original; toggle and source changes force a fresh read rather than a resume.
- **Strings:** 12 `Translate_*` keys in nine languages (machine-translated).
- **Privacy:** `PRIVACY.md` and `site/privacy.html` say detection and translation are on-device, the model
  download comes from Google, and no text is sent.
- **Time to first audio:** the whole text is translated before reading, since `SpeakAsync` takes one text.
  Streaming into the reader is a follow-up.

## From review

- High pass: 10 findings, all fixed. The worst: the detector and translator were never registered in DI, so
  the card never appeared (the emulator caught this too).
- Medium pass: 9 findings fixed, 1 skipped (partial-class size).

## Emulator results (RTST_Test, no Google account, 2026-10-21)

- French text is detected as Français.
- With the toggle on, the first Play shows "Downloading the translation model…". The model downloaded without a
  Google account, the text translated to "Hello everyone. I am very happy to see you today in the garden.", and
  the English was read.
- After replacing the text with Spanish, it was detected as Español, the Spanish model downloaded, and the
  English translation was read.
- The toggle state persisted across a restart.

#### For CLAUDE.md (lifted 2026-10-21)

- On-device translation lives in `Application/Translation` (`TranslationFlow`, pure). Android implements it with
  ML Kit in `Platforms/Android`, registered in `MauiProgram.Android.cs`. (Slice 54:
  docs/plan/slices/54-on-device-translation.md)
- Translated text is what gets read, but the editor and the resume identity keep the original. (Slice 54:
  docs/plan/slices/54-on-device-translation.md)
- User text never leaves the device. Only ML Kit model downloads use the network. (Slice 54:
  docs/plan/slices/54-on-device-translation.md)

#### For README.md (lifted 2026-10-21)

The Android app can translate pasted or scanned text into your language before reading it, on the phone. Turn on
"Translate to <language>" on the reader.

#### Not yet verified

- An offline translation with a cached model.
- An offline first use, which should show the alert and read the original.
- A 5,000-word text, for responsiveness.
- The translate card in RTL (fa, ar).
- A native-speaker read of the new strings.
- Streaming translation into the reader (follow-up).
- iOS has no detector/translator registered, so the card stays hidden there.
