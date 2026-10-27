# CLAUDE.md — Mobile (MAUI: Android and iOS)

Loaded when you work under `src/ReadTheStupidText.Mobile`. Shared platform code in
`src/ReadTheStupidText.Mobile.Shared` and the Dino Reader app have their own files. CI is in `.github/CLAUDE.md`.

> **Multi-platform (Batch 6):** the app is Android-first via **.NET MAUI**, with
> iOS/macOS later on the same stack. `Domain` and `Application` stay
> framework-agnostic and are reused **unchanged** by Mobile; only platform
> implementations are written. Android has none of Windows' auto-read triggers;
> its inputs are text entry, file upload and camera → on-device OCR → read.
> (Decisions 37-42 in `docs/plan/decisions.md`; note: docs/plan/notes/claude-md-history.md)

> **Language packs, iOS, translation (Batch 9):** on **Android and iOS no voice model is
> bundled** (Slice 50; supersedes Decisions 14 and 39 there): every voice downloads as a
> pack after the first-run language choice, from GitHub Releases tagged `packs-*`, and
> the system voice reads until it arrives. User data (text, images, scans,
> translations) stays **100% offline**; only packs and ML Kit models are downloaded.
> One **scoped GPL exception**: espeak-ng data (GPL-3.0) lives only inside the downloaded
> `piper-fa` and `piper-nb` packs (Slice 104), never in the repo or any package. **iOS** is the same MAUI project
> (`net10.0-ios`) shipped to TestFlight from CI. (Decisions 59-71,
> `docs/plan/slices/43-57`; note: docs/plan/notes/claude-md-history.md)

## Structure

- **Mobile** (`src/ReadTheStupidText.Mobile`, .NET MAUI, Decision 37) — the RTST
  app; references `Domain`/`Application`/`Documents`/`Mobile.Shared`. The shared
  platform code (engines, voice packs, `IPlatformServices`, per-OS
  `Platforms/` folders) lives in **`src/ReadTheStupidText.Mobile.Shared`**
  (namespace `ReadTheStupidText.Mobile.Shared`; `Application` grants it
  `InternalsVisibleTo`). OCR and the scanner stay RTST-only.
  **`src/DinoReader.App`** (`net10.0-android`, `uk.sirous.dinoreader`) references
  `Application`/`Domain`/`Mobile.Shared`. **Nothing outside `Platforms/`
  names an Android or iOS type:** engines come from `IPlatformServices` via the
  required `MauiProgram.RegisterPlatformServices` partial in each `Platforms/<OS>/`
  (`Platforms/iOS` exists since Slice 48; Slice 44:
  docs/plan/slices/44-pipeline-ios-split.md). It does **not** reuse
  `ReadAloudService` — that orchestrates Windows-only triggers (hotkey, UIA
  selection, clipboard) that don't exist on Android (Decision 38) — pages wire
  `ISpeechReader` + `PlaybackRate`/`SpeedPresets` directly instead. **Android
  minimum is API 28** (`SupportedOSPlatformVersion` 28.0, Decision 82) for
  every Android app here, Dino Reader included; a new MAUI project starts at
  the template's 21, so set it. Never lower it (Play automatic protection,
  enabled on our listing, rejects bundles below 24). Set it only in the
  csproj, never as a `<uses-sdk>` in `AndroidManifest.xml`. Guards for API 28 or
  older are dead code; 29+ guards are still needed.

## Gotchas

- Files under the app's own `Platforms/Android` use namespace `ReadTheStupidText.Mobile` (Mobile.Shared's use `ReadTheStupidText.Mobile.Shared`),
  *not* `...Platforms.Android` — a namespace segment literally named `Android`
  shadows the top-level `Android.*` binding namespace for unqualified lookups
  in that file.
- Any bare `Application` in Mobile code must be
  qualified as `Microsoft.Maui.Controls.Application` — the project also
  references a library literally named `ReadTheStupidText.Application`, which
  wins unqualified-name resolution from within the `ReadTheStupidText.Mobile`
  namespace.
- **sherpa-onnx on Android:** a plain `PackageReference` silently
  bundles wrong-OS `.so` files into `lib/arm64-v8a/`. The fix lives in
  `src/AndroidSherpaPackaging.props`, which **every** MAUI project linking the engine
  must import (all three: `Mobile.Shared`, RTST, `DinoReader.App`; `ExcludeAssets`
  does not flow through `ProjectReference`). The `AndroidNativeLibrary` items are
  staged only in app projects. `.github/scripts/check-android-natives.sh` checks
  every AAB/APK in CI; for any *other* native dependency, unzip the built package and
  check `lib/<abi>/` yourself. (Slice 58: docs/plan/slices/58-dino-reader-skeleton.md)

## Build and emulator

```bash
dotnet build src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj -f net10.0-android
```

Mobile.Shared adds a Windows target on every Windows host (Dino Reader's head), which needs the
`maui-windows` workload. Where only `maui-android` is installed, add `-p:IncludeWindows=false`.

Deploy to a device/emulator from Visual Studio (the **Mobile** project's Android
debug target), or `dotnet build -t:Run -f net10.0-android` with a device/emulator
already running. No AVD ships with this repo — set one up locally (or attach a
physical device) before relying on a slice that touches the Mobile project.

**iOS target (Slice 48, lifted 2026-10-21):** `Platforms/iOS` implements `IPlatformServices` (neural reading and Scan: see the iOS rules under Mobile voice packs). `net10.0-ios` is in the Mobile csproj only on macOS or with `-p:IncludeIos=true` (`-p:IncludeAndroid=false` for iOS-only); compile-check on Windows with `dotnet restore -p:IncludeIos=true` then `dotnet build -f net10.0-ios -p:IncludeIos=true --no-restore`. CI signing and TestFlight are in `.github/CLAUDE.md`. (Slice 48: docs/plan/slices/48-ios-target-testflight.md)

**Scan (Slice 45, lifted 2026-10-21):** Scan (Decision 64) uses the ML Kit Document Scanner (Android only, via `IDocumentScanner` registered only in `Platforms/Android`), now one page per Add (`ScanAsync(1)`; a scanner that can't limit pages, iOS, returns what it returns) from our own `CapturePage` (Slice 100, below). Without Play services it falls back to one `MediaPicker` photo, which is why the `CAMERA` + `IMAGE_CAPTURE` `<queries>` stay in the manifest. The csproj pins `AndroidX.Fragment.Ktx` 1.9.0 (the scanner's 1.8.8.1 collides with Fragment 1.9.0 in D8) and `Xamarin.GooglePlayServices.Tasks` must stay >= 118.4.1.3. A real scan needs a device signed in to Google. (Slice 45: docs/plan/slices/45-android-document-scanner.md; detail: docs/plan/notes/claude-md-history.md)

## Voice packs and first run (Batch 9)

- Mobile (Android and iOS) no longer bundles the voice model. Supertonic-3 is a downloaded pack `supertonic-3` (~145 MB) under `AppDataDirectory/packs/`; `MobileVoiceModelService` only locates and manifest-checks it. The APK/AAB has no `assets/VoiceModel` (unzip and check). (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- Voice packs (`docs/voice-packs.md`): `IVoicePackService` (Application) + `MobileVoicePackService` (Mobile); the pure decisions live in `PackRetryPolicy`/`PackCatalogParser`/`PackManifest`/`PackNaming`/`VoicePackSelector` (unit-tested). The pipeline runs off the UI thread and `VoicePackInstaller` marshals state to the main thread. An installed pack means zero network requests. `InvalidPack` is never retried. Offline detection on Android = `NetworkAccess.None` + Java exception type names. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- Until the pack is Ready, `CompositeSpeechReader` reads with the Android `TextToSpeech` "Phone voice" (`SystemVoice.Id`), also user-selectable. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- The app pins one `packs-<n>` release in `PackConstants.CatalogUrl` (how they are published and tag-filtered: `.github/CLAUDE.md`). (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- First-run language: `ISettingsStore.Language`; the app shows `LanguagePage` when it is null. Android declares `INTERNET` + `ACCESS_NETWORK_STATE`, used only for pack downloads; user data stays on the device. Android `minSdk` is 28 (Decision 82; see the API 28 rule above). (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- **Pack layout.** An installed pack lives in `packs/<id>/v<version>/` with an `active` file naming the
  folder; always resolve it through `PackVersionFolders.Resolve` (`src/ReadTheStupidText.Application/Reading/PackVersionFolders.cs`),
  never `packs/<id>/` directly, and never replace or delete the active folder (a live engine holds its files).
  A multi-model voice pack puts each model in `voices/<name>/` (`model.onnx` + `tokens.txt`), with one shared
  `espeak-ng-data` at the pack root. Phones update an installed pack only on an unmetered connection
  (`PackUpdatePolicy`). (Slice 89: docs/plan/slices/89-four-farsi-voices.md)
- **Engines are cached per model folder.** Readers get engines through `EngineCache`
  (`src/ReadTheStupidText.Application/Reading/EngineCache.cs`, capacity 4, keyed by model folder and pack
  version, reference counted). Acquire on the thread pool, never on the UI thread (it caused an ANR); the voice
  preview plays only when the reader is idle. (Slice 89: docs/plan/slices/89-four-farsi-voices.md)

## Rules from the Batch 9 slices (lifted 2026-10-21)

- Voices are per-language packs; `InstalledVoiceCatalog` resolves the one serving `ISettingsStore.Language` (Supertonic en/fr/es/pt/ru/ja/ar/da/fi/sv/nl/de, `piper-fa` fa, `melo-zh-en` zh). sherpa-onnx config types are structs: pass them by `ref` to helpers that fill them, or paths drop silently. `AndroidSupertonicSpeechReader` engines are `RefCountedTts`, captured per read. (Slice 51: docs/plan/slices/51-multilingual-voices.md)
- OCR on Android: PaddleOCR `kind: ocr` packs per language, ML Kit Latin as fallback; the pure pipeline is `Application/Images/Ocr`. `Microsoft.ML.OnnxRuntime.Managed` stays version-matched with sherpa's bundled `libonnxruntime.so` (both 1.24.3); bump together, never add a native onnxruntime package. (Slice 52: docs/plan/slices/52-paddleocr-per-language.md)
- UI strings live in `Mobile/Resources/Strings/AppResources*.resx` via the typed `AppResources`, never literals. A new key goes in the neutral file, every language file and `AppResources.cs` (`LocalizedUiTests` enforces it). `UiCulture.Apply` sets the culture (layout direction: see the next rule). (Slice 53: docs/plan/slices/53-localized-ui-rtl.md)
- The layout never mirrors: pages, shell and action sheets are `FlowDirection.LeftToRight` for every language. The back/skip arrows are the fixed `TransportGlyphs` constants, not resources. Text views take their direction: localised UI strings from the UI language, user/translated text from its own content (UI strings: `TextFlow.ForUi`; user, OCR and translated text: `TextFlow.ForContent`). Decision 92 supersedes the RTL-layout part of Decision 68. (Slice 85: docs/plan/slices/85-no-layout-mirroring.md)
- A scan whose page the built-in recognizer can't read (an `OcrSource.MlKitPackPending` result) runs `ScanPackFlow` (pure decisions: `PageLanguagePrompt`, `PagePackRetry` in Application): it asks which language the page is in (detected script first), downloads that pack via `OcrPackInstaller.EnsureForLanguageAsync` (joins an in-flight download of the same pack) with progress and Cancel, then re-reads only the pending pages. Offline, cancel, a capped Retry, or a page still pending keep the editor text. (Slice 87: docs/plan/slices/87-scan-downloads-the-page-language-pack.md)
- Phone-voice availability goes through `IPlatformServices.PhoneVoiceLanguagesAsync` (null = unknown, empty = none) and the shared `PlatformVoiceAvailability` cache; don't query the TTS engine from pages. Reading language (`VoiceRouting.ReadingLanguage`) is the translation target when the text was translated, else the text's own language (source picker choice, else the translate card's detection of this text; Play never runs a detector), and when that is unsure the main language of the text's script (the app language when it writes in that script); mixed text is read in one language. The pack for that language comes from `IReadingModelResolver.ModelForAsync` and travels with that one read as a `ReadVoiceScope` argument (`IReadScoped.SpeakScopedAsync`, never state on the reader; an app-language read takes the ordinary unscoped path, and a platform without `SupportsModelScope`, iOS, reads other languages with the phone voice); the app-language `IVoiceModelService`, the stored voice and the voice picker are never changed by a one-off read; the "no voice" block applies to that language alone. `AndroidSpeechReader` must `SetLanguage` before every read and check the result; never rely on `Locale.Default` set at init. (Slices 82, 90: docs/plan/slices/82-phone-voice-reads-the-texts-language.md, docs/plan/slices/90-untranslated-text-reads-in-its-own-language.md)
- Translation: `Application/Translation` (`TranslationFlow`, pure) with ML Kit (`MlKitTranslator` in `Mobile.Shared/Platforms/Android` and `Platforms/iOS`, both platforms since Slice 160), run only from `ReviewPage` (Slice 101, below): the text is replaced by its translation and the main page shows and reads that, so what is on screen is what is heard (this reverses Slice 54's "editor keeps the original" for the phone; `ReviewTextState` keeps the original for **Show original**). Text never leaves the device. (Slice 54: docs/plan/slices/54-on-device-translation.md; Slice 101: docs/plan/slices/101-rtst-mobile-review-and-translate.md)
- Android sentence audio is cached by `SentenceAudioCache` (`CacheDirectory/sentences`); never play cached clips directly, join them into a temp file. `IHeadStart` prepares the first chunks, skipped while translation is on. Windows keeps `SpeechTextChunker.HeadPolicy.Default` until Slice 56. (Slice 47: docs/plan/slices/47-head-start-sentence-cache.md)
- iOS neural reading uses sherpa-onnx 1.13.8 + `SherpaOnnxC.xcframework` 1.13.8 (Android pins 1.13.3); bump package and framework (URL and SHA-256 in the Mobile csproj) together. The reader sits behind `IosNeuralReaderFactory` (apps without one, like Dino Reader, keep the system voice); the audio session goes through `IosAudioSession`, never `AVAudioSession` directly. (Slice 49: docs/plan/slices/49-ios-parity.md)
- Multi-voice reading is `ISpeechReader.SpeakSegmentsAsync` (native only where `SupportsSegments`; else one voice). `ChunkChanged` may fire off the UI thread: marshal. (Slice 60: docs/plan/slices/60-per-character-voice-reading.md)

- Release Android builds are R8-obfuscated (`src/AndroidRelease.props`, keep rules in `src/AndroidProguard.cfg`) and need
  .NET SDK/workload set 10.0.402 (Android SDK >= 36.1.118); the build errors otherwise. A `NoSuchFieldError` or
  `LinkageError` in Release only means R8 renamed a member a binding reads over JNI: add a keep rule, clean-rebuild (delete
  obj/bin Release), and smoke-test the Release APK on the emulator with `adb install -r --abi arm64-v8a`. Release ships
  arm64-v8a only. (Slice 72: docs/plan/slices/72-android-deps-r8-obfuscation.md)
- Pages draw edge to edge on Android 15+. Set `SafeAreaEdges="None"` on the page/root and `Container` on the header,
  sheet or body that must clear the bars; never set status/navigation bar colours in code (deprecated).
  (Slice 73: docs/plan/slices/73-edge-to-edge-safe-areas.md)
- Emulator testing goes through `tools/emulator/` (`start-warm.ps1`, `install-debug.ps1 -App rtst|dino`, `stop.ps1`; headless, `emulator-5554`, one cold boot (~45 s) per session then kept running; snapshots are off because they broke flows; never a window) with an **android-x64** Debug build; arm64 is only for the Release smoke test. `AndroidSherpaPackaging.props` fetches sherpa-onnx's x86_64 `libsherpa-onnx-c-api.so` + `libonnxruntime.so` (pinned URL and SHA-256, into `artifacts/`) for Debug + `android-x64` only; Release stays arm64-v8a. Bump them with the NuGet package. (Slice 161: docs/plan/slices/161-warm-headless-x64-emulator.md)
- Debug builds installed with `adb` (emulator) need `-p:EmbedAssembliesIntoApk=true` and a `-p:RuntimeIdentifier` (`android-x64` for the emulator; `android-arm64` for a phone) (plus `-p:IncludeWindows=false`). A plain Debug build uses Fast Deployment and dies on launch with "No assemblies found" after `adb install`. (Slice 81: docs/plan/slices/81-language-change-starts-voice-download.md)
- Phone overhaul (Batch 19, Decisions 103-105; main page and Settings built by Slice 99): the main page holds only the gear, the read-only text with its highlight and Edit link, **Text from pictures**, **Text from a file** and the player; language, voice and speed live on `SettingsPage`. Don't add controls back to `ReaderPage`. The main page reads the text as it is (no translate card or switch; the language detector still runs quietly for the voice); `TextEditor` is only its hidden text holder, and Edit, long-press Paste (offered on the text), **Text from a file** and the capture wizard's Done all open `ReviewPage` (route `AppShell.ReviewRoute`) (`LongPressBehavior`: a `GestureDetector` on Android, a `UILongPressGestureRecognizer` on iOS, in `Platforms/<OS>/`; neither consumes the touch), the icons are the mock's SVG paths drawn as unscaled `Path`s (`Aspect=None`, sized with `Scale`), the old `Translate_*` strings are gone (the page's are `Review_*`), and `EditorDraft.RegisterRebuildHook` lets `SettingsPage` ask the live reader to stop and keep its text before a language change rebuilds the shell. Multi-page capture is our own `CapturePage` (Slice 100, route `AppShell.CaptureRoute`): the camera opens on entry, each Add is one page, `CapturedPageList` (Application, pure) holds the pages and each is OCRed in the background in arrival order (`ScanPackFlow` per page, never while a camera is in front), Done joins them with `ScannedText.JoinPages` and opens `ReviewPage` in place of the wizard (`GoToAsync("../review")`), naming unread pages in its banner, Back asks "Discard these pages?" once a page exists, and the pictures are deleted when the wizard ends. Hand-offs between pages go through the `ReviewHandOff` singleton (Application/Translation; request to the page, result back, each taken once; never a Shell query parameter: it replays). `ReviewPage` (wizard page 2) is almost all editable text box; its checkbox shows only when `ILanguageDetector` finds a language other than the app's and `ITranslator.SupportsPair` says this phone can translate it (Android and iOS; the pure rules are `ReviewDecisions`; iOS simulator builds have no translator, so the card hides there), is ticked by default and there is no source picker; the big button reads **Translate and read** or **Read**; a missing ML Kit model downloads behind a card (ML Kit reports no percentage, so the bar sweeps) and a failed translation reads the original after one short alert. Read hands `ReviewResult` back and `ReaderPage.OnAppearing` shows the text and starts reading it; Back changes nothing. The `TranslateEnabled` setting is no longer read by the phone (Windows still uses it). (Slices 99-101: docs/plan/slices/99-rtst-mobile-minimal-main-page.md, 100-rtst-mobile-page-capture-wizard.md, 101-rtst-mobile-review-and-translate.md)
- The phone's Batch 19 screens (Slices 99-101) are the mock `docs/design/rtst-phone/RTST Phone Batch 19.dc.html`, and Batch 29's (Slices 137-139: wait overlay, counts switch, first-run line) are `docs/design/rtst-phone/RTST Batch 29.dc.html` (index in that folder's README): read visual values (colours, sizes, copy, layout) from it, never guess them, and never call `DesignSync` for them.
- Anonymous usage counts (Slice 137, Decisions 143-145): pages call `RtstUsage.Record` (`Mobile.Shared/Usage`, built on the `UsageQueueSender` and `RemoteConfigClient` Dino shares); it never throws and does nothing while the Settings switch or the remote config (`readthestupidtext/config.json`, off until the plan's *Store privacy declarations* are ticked) says off. The events are the closed set in `Application/Usage` and `worker/events.ts` (`RTST_EVENT_RULES`; `UsageTests` fails when they drift): a new count means a new name in both, never free text, an id or the read text. The first-sound clock is `ReaderPage.Usage.cs`; the model-download wait is timed apart only where the translator is an `IModelPreparer` (ML Kit). (Slice 137: docs/plan/slices/137-rtst-phone-usage-counts.md)
- RTST waits use `Mobile.Shared/Waiting` through `ReaderPage.Wait.cs`; colours live in `Colors.xaml` / `ThemeColors.cs`, no hex literals. (Slice 138: docs/plan/slices/138-rtst-phone-wait-indicator.md)
- Multi-page capture is our own `CapturePage`, not Google's multi-page UI: each Add is one page (`ScanAsync(1)`). Text goes back to `ReaderPage` through the `CaptureHandOff` singleton, not query properties. Clean up cached images in `OnNavigatedFrom`, not on backgrounding. (Slice 100: docs/plan/slices/100-rtst-mobile-page-capture-wizard.md)
- `ReviewPage` hand-off: `ReviewHandOff` values clear on read, and `ReaderPage` guards against re-entry and pauses reading when it opens the page. Don't read the hand-off twice. (Slice 101: docs/plan/slices/101-rtst-mobile-review-and-translate.md)
- Phone translation is a per-text choice on `ReviewPage`, never translate-on-speak. Language detection always routes the voice; the translator only gates the checkbox. `TranslateEnabled` stays in `ISettingsStore` for Windows only. (Slice 101: docs/plan/slices/101-rtst-mobile-review-and-translate.md)
- `ReviewPage` sets soft input to `AdjustResize` and must restore `Pan` on leaving. (Slice 101: docs/plan/slices/101-rtst-mobile-review-and-translate.md)
- Early start (`ReaderPage.EarlyStart.cs`, `ReviewPage.xaml.cs`) records no usage of its own and shows no overlay. It may only cancel preparations it started itself. (Slice 139: docs/plan/slices/139-rtst-phone-early-start.md)
- A head start completes normally only when its clips are stored (`HeadStartRun`); the read's first chunk follows the early head through `FirstChunkHints`, never global state. (Slice 139: docs/plan/slices/139-rtst-phone-early-start.md)
