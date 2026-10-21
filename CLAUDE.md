# CLAUDE.md — ReadTheStupidText

Project-specific guidance for agents working in this repo. This complements
the user's global instructions (which always apply).

## What this is

**ReadTheStupidText** is a Windows 11 tray utility that reads selected/copied text aloud
at 1x–2x speed. It is a **WinUI 3 + Windows App SDK** desktop app, packaged as
**MSIX** and targeted at the **Microsoft Store**. It is *not* a classic UWP app
(the sandbox cannot do tray icons, global hotkeys, or cross-app text reads).

> **Multi-platform (Batch 6):** the app is Android-first via **.NET MAUI**, with
> iOS/macOS later on the same stack. `Domain` and `Application` stay
> framework-agnostic and are reused **unchanged** by Mobile; only platform
> implementations are written. Android has none of Windows' auto-read triggers;
> its inputs are text entry, file upload and camera → on-device OCR → read.
> (Decisions 37-42 in `docs/plan/decisions.md`; note: docs/plan/notes/claude-md-history.md)

> **Reading text box + disk-backed audio (Batch 7):** a toggleable floating window
> shows the chunk being read (4th icon in the panel's `CONTROLS` row). Synthesized
> audio is a temp WAV per chunk under `TemporaryFolder\audio\<id>\`; the chunk
> map used by Skip (Decision 32) and the read-through timer (Decision 33) carries
> each chunk's file path and source-text range, so extend it, don't duplicate it.
> **Stacked PRs:** open them against `main`, or retarget each one after its base
> merges (PRs #154-#156 once merged into each other instead). (Decisions 45-51;
> note: docs/plan/notes/claude-md-history.md)

> **CI (Batch 8, Slice 70):** zero `upload-artifact` (the account's storage is 500 MB
> shared across repos): build jobs upload straight to a **draft GitHub Release**, which
> `release-publish` publishes once every affected build succeeds; repo artifact
> retention stays at 1 day as a safety net (Slice 42). `release.yml` is the only
> workflow on push to `main` (plus a main-only `workflow_dispatch` with `force_all`):
> version (GitVersion + versionCode + `.github/scripts/ship-targets.mjs`) -> test
> (`tests.yml`) -> release-draft -> `windows.yml` / `android.yml` / `ios.yml` (reusable;
> they also run on their own PR paths, never signing or uploading on PRs) ->
> release-publish -> store-submit, play-submit, testflight-submit, publish-downloads.
> `ship-targets.mjs` holds the path -> platform mapping (`Mobile/Platforms/Android` ->
> Android, `Mobile/Platforms/iOS` -> iOS, rest of `Mobile` -> both; `App`/`Infrastructure`
> -> Windows; core paths -> all; a major bump -> all). `ship-targets.test.mjs` checks
> every platform workflow's `pull_request.paths` matches it, so change them together.
> `release-scripts.yml` runs all script tests and actionlint (`.github/actionlint.yaml`)
> on PRs touching `.github/**`. An untouched platform is neither built nor submitted;
> its assets are carried forward. The iOS PR check builds Debug (simulator); only main does the signed Release (PR #206). When adding a project or shared build file, add its
> path to `ship-targets.mjs`. Never add `actions/upload-artifact` for build outputs.
> (Decisions 52-58, 69; Slice 44: docs/plan/slices/44-pipeline-ios-split.md;
> Slice 70: docs/plan/slices/70-split-build-pipeline.md)

> **Merging a change to a core path ships every app.** The core paths in
> `ship-targets.mjs` include `.github/workflows/release.yml`, `tests.yml`,
> `.github/scripts/find-release.sh`, `ship-targets.mjs`, `*.slnx`, `GitVersion.yml` and
> `global.json`, so a PR that edits any of them (even a Dino Reader-only PR adding a
> project to the solution) builds all platforms, cuts a version and submits to the
> Microsoft Store, Google Play and TestFlight on merge. Expect that, or batch such
> edits. (Slice 70: docs/plan/slices/70-split-build-pipeline.md)

> **iOS toolchain pin:** `ios.yml` pins dotnet 10.0.401 + workload set 10.0.401
> (Microsoft.iOS.Sdk 26.5.10318) + Xcode 26.6, because `macos-26` has no Xcode 27.
> Bump all three together. (Slice 48: docs/plan/slices/48-ios-target-testflight.md)

> **iOS target (Slice 48, lifted 2026-10-21):** `Platforms/iOS` implements `IPlatformServices` (neural reading and Scan: see the iOS rules under Mobile voice packs). `net10.0-ios` is in the Mobile csproj only on macOS or with `-p:IncludeIos=true` (`-p:IncludeAndroid=false` for iOS-only); compile-check on Windows with `dotnet restore -p:IncludeIos=true` then `dotnet build -f net10.0-ios -p:IncludeIos=true --no-restore`. `ios.yml` compiles unsigned on PRs; on main with the six iOS secrets it signs (`.github/scripts/ios-signing.sh`, identity by SHA-1), attaches the `.ipa` to the draft and `testflight-submit` uploads it after `release-publish`. Signing scripts must run on macOS bash 3.2. (Slice 48: docs/plan/slices/48-ios-target-testflight.md)

> **Scan (Slice 45, lifted 2026-10-21):** Scan (Decision 64) uses the ML Kit Document Scanner (Android only, via `IDocumentScanner` registered only in `Platforms/Android`); `ReaderPage` OCRs the pages in order and appends them via `ScannedText.AppendPages`. Without Play services it falls back to one `MediaPicker` photo, which is why the `CAMERA` + `IMAGE_CAPTURE` `<queries>` stay in the manifest. No `ScanPage`, no Shell hand-off. The csproj pins `AndroidX.Fragment.Ktx` 1.9.0 (the scanner's 1.8.8.1 collides with Fragment 1.9.0 in D8) and `Xamarin.GooglePlayServices.Tasks` must stay >= 118.4.1.3. A real scan needs a device signed in to Google. (Slice 45: docs/plan/slices/45-android-document-scanner.md; detail: docs/plan/notes/claude-md-history.md)

> **Dino CI rule:** the `dinoreader` paths run only the `test-dinoreader`
> job (ubuntu), never the version, release or store jobs. Keep Dino paths out of
> the core paths in `ship-targets.mjs`. (Slice 59: docs/plan/slices/59-dialogue-parser-voice-caster.md)

> **Language packs, iOS, translation (Batch 9):** on **Android and iOS no voice model is
> bundled** (Slice 50; supersedes Decisions 14 and 39 there): every voice downloads as a
> pack after the first-run language choice, from GitHub Releases tagged `packs-*`, and
> the system voice reads until it arrives. **Windows still bundles the model until
> Slice 56 lands** (see the Windows narrator notes). User data (text, images, scans,
> translations) stays **100% offline**; only packs and ML Kit models are downloaded.
> One **scoped GPL exception**: espeak-ng data (GPL-3.0) lives only inside the downloaded
> `piper-fa` pack, never in the repo or any package. **iOS** is the same MAUI project
> (`net10.0-ios`) shipped to TestFlight from CI. (Decisions 59-71,
> `docs/plan/slices/43-57`; note: docs/plan/notes/claude-md-history.md)

> **Dino Reader (Batch 10):** a second product in this repo, a kids' book reader
> with a voice per character: a second MAUI app, `src/DinoReader.App`, sharing
> `src/ReadTheStupidText.Mobile.Shared` plus Domain/Application/Documents. The pure
> `src/DinoReader.Core` holds the dialogue segmenter, speaker attributor and voice
> caster (no framework deps). One shared version (`v<x.y.z>`) covers both products;
> each has its own path-filter target (`dinoapp` in `ship-targets`, with
> `dinoreader-android.yml`; assets `DinoReader-<v>.aab`/`.apk`; `DINOREADER_*` secrets,
> never the RTST ones; without all five a Dino-only change cuts no release).
> (Decisions 72-82, `docs/plan/slices/58-67`)
> Its icons (all platforms, "Book hug" design) live in
> `docs/design/dino-reader/icons/`: copy from there, never redraw or regenerate them.
> Its screens are the mock `docs/design/dino-reader/Dino Reader v2.dc.html` (index in
> that folder's README): read visual values (colours, sizes, copy, layout) from it,
> never guess them, and never call `DesignSync` for them.

> **Naming:** the user-facing **product display name is "Read The Stupid Text"** (with
> spaces) — shown in the manifest `DisplayName`s, tray tooltip, control-panel header,
> and window titles. The **repo, package id (`ReadTheStupidText`), namespaces, assembly,
> and the StartupTask `TaskId`** are internal identifiers and stay `ReadTheStupidText`
> — do not "rename" those. Store identity (ID `9NGT1BN1H92V`) is wired into
> `Package.appxmanifest`; see Decision 23 in `docs/plan/decisions.md` and `STORE.md`.

Read [`plan.md`](plan.md) before starting work — it defines the vertical slices
and their GitHub issue numbers. Tick slices off in `plan.md` as they complete. The numbered **Decisions** live in `docs/plan/decisions.md` ("Decision NN" anywhere in this file refers to it); each slice's full record is under `docs/plan/slices/`.

## Stack & conventions

- **.NET 10**, **C#**, **WinUI 3 / Windows App SDK 1.8**.
- Solution file is `ReadTheStupidText.slnx` (the XML `slnx` format — not `.sln`).
- Always confirm Windows App SDK / WinUI / `H.NotifyIcon` APIs and versions via
  **context7** before writing code against them — versions move fast.

## Layered structure (one-direction dependencies)

```
App  →  Application  →  Domain
Infrastructure  →  Application / Domain
Mobile  →  Application / Domain / Documents
Documents  →  Application
DinoReader.Core (standalone, no deps)
```

- **Domain** (`net10.0`) — entities, value objects, enums. No framework deps.
  Reading speed is the **`PlaybackRate`** value object — a decimal multiplier
  (0.5–2.0, snapped to 0.05 steps), *not* an enum: the user picks a continuous
  rate, with `SpeedPresets` exposing the common stops (1/1.25/1.5/1.75/2) for the
  native tray menu. Genuinely closed sets (reading state, etc.) stay `enum`s.
  Also `SupertonicVoiceTable`/`SupertonicFiles` (Slice 31) — pure voice-catalog
  data, moved here from Infrastructure so Mobile can reuse it too.
- **Application** (`net10.0`) — use cases / orchestration, interfaces. Also
  `SpeechTextChunker`/`ReadTimingTracker` (both `internal`, Slice 31) — pure
  chunking/timing logic shared by every `ISpeechReader` implementation on
  every platform (`InternalsVisibleTo` covers `Tests`, `Infrastructure`, and
  `Mobile`).
- **Documents** (`net10.0`) — `PlainTextExtractor`/`PdfTextExtractor`/
  `DocxTextExtractor`/`CompositeDocumentTextExtractor` (Slice 33). A portable
  library, not Infrastructure or Application: they need PdfPig/
  DocumentFormat.OpenXml (so not Application, which stays library-free) but
  have no Windows-only dependency (so not stuck in Infrastructure) — both
  Infrastructure (Windows) and Mobile (Android) reference this project and
  share the exact same extractors.
- **DinoReader.Core** (`net10.0`, `src/DinoReader.Core`) — the pure dialogue library
  for Dino Reader: `StorySegmenter`, `SpeakerAttributor`, `GenderGuesser`,
  `VoiceCaster` (Decision 75). No package or project references (not Domain, not
  Application); voices come in as plain `VoiceOption(id, gender)`. Every language
  table lives in `LanguageRules` (`LanguageRules.English` today): a new language adds
  a rules instance and never touches the algorithms. Returns data only, no
  user-facing copy. Paragraphs are blank-line separated; a single newline is never a
  sentence or paragraph boundary (wrapped PDF/OCR text). (Slice 59: docs/plan/slices/59-dialogue-parser-voice-caster.md)
- **Infrastructure** (`net10.0-windows`) — speech engines (local neural
  **Supertonic-3** via sherpa-onnx, plus a WinRT `SpeechSynthesis` fallback),
  all played through `MediaPlayer`; every platform downloads its voice as a pack
  (Slice 50; Windows since Slice 56, no model in the MSIX).
  Clipboard, UI Automation, startup task, OS integration.
- **App** (`net10.0-windows`, WinUI single-project MSIX) — UI, tray, DI wiring.
- **Mobile** (`src/ReadTheStupidText.Mobile`, .NET MAUI, Decision 37) — the RTST
  app; references `Domain`/`Application`/`Documents`/`Mobile.Shared`. The shared
  platform code (engines, voice packs, `IPlatformServices`, per-OS
  `Platforms/` folders) lives in **`src/ReadTheStupidText.Mobile.Shared`**
  (namespace still `ReadTheStupidText.Mobile`; `Application` grants it
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
  **Gotcha:**
  files under `Platforms/Android` use namespace `ReadTheStupidText.Mobile`,
  *not* `...Platforms.Android` — a namespace segment literally named `Android`
  shadows the top-level `Android.*` binding namespace for unqualified lookups
  in that file. Similarly, any bare `Application` in Mobile code must be
  qualified as `Microsoft.Maui.Controls.Application` — the project also
  references a library literally named `ReadTheStupidText.Application`, which
  wins unqualified-name resolution from within the `ReadTheStupidText.Mobile`
  namespace. **Gotcha (sherpa-onnx on Android):** a plain `PackageReference` silently
  bundles wrong-OS `.so` files into `lib/arm64-v8a/`. The fix lives in
  `src/AndroidSherpaPackaging.props`, which **every** MAUI project linking the engine
  must import (all three: `Mobile.Shared`, RTST, `DinoReader.App`; `ExcludeAssets`
  does not flow through `ProjectReference`). The `AndroidNativeLibrary` items are
  staged only in app projects. `.github/scripts/check-android-natives.sh` checks
  every AAB/APK in CI; for any *other* native dependency, unzip the built package and
  check `lib/<abi>/` yourself. (Slice 58: docs/plan/slices/58-dino-reader-skeleton.md)

**Mobile voice packs and first run (Batch 9):**

- Mobile (Android and iOS) no longer bundles the voice model. Supertonic-3 is a downloaded pack `supertonic-3` (~145 MB) under `AppDataDirectory/packs/`; `MobileVoiceModelService` only locates and manifest-checks it. The APK/AAB has no `assets/VoiceModel` (unzip and check). Windows still bundles `VoiceModel/` until Slice 56. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- Voice packs (`docs/voice-packs.md`): `IVoicePackService` (Application) + `MobileVoicePackService` (Mobile); the pure decisions live in `PackRetryPolicy`/`PackCatalogParser`/`PackManifest`/`PackNaming`/`VoicePackSelector` (unit-tested). The pipeline runs off the UI thread and `VoicePackInstaller` marshals state to the main thread. An installed pack means zero network requests. `InvalidPack` is never retried. Offline detection on Android = `NetworkAccess.None` + Java exception type names. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- Until the pack is Ready, `CompositeSpeechReader` reads with the Android `TextToSpeech` "Phone voice" (`SystemVoice.Id`), also user-selectable. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- `packs-<n>` releases hold packs + `packs.json`, published by the manual `packs.yml` with `--latest=false`; the app pins one in `PackConstants.CatalogUrl`. Every app-release tag scan (`release.yml` release-publish, `.github/scripts/find-release.sh`) must filter to `^v[0-9]+\.[0-9]+\.[0-9]+$`. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- First-run language: `ISettingsStore.Language`; the app shows `LanguagePage` when it is null. Android declares `INTERNET` + `ACCESS_NETWORK_STATE`, used only for pack downloads; user data stays on the device. Android `minSdk` is 28 (Decision 82; see the API 28 rule above). (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)

**Mobile rules from the Batch 9/10 slices (lifted 2026-10-21):**

- Voices are per-language packs; `InstalledVoiceCatalog` resolves the one serving `ISettingsStore.Language` (Supertonic en/fr/es/pt/ru/ja/ar, `piper-fa` fa, `melo-zh-en` zh). sherpa-onnx config types are structs: pass them by `ref` to helpers that fill them, or paths drop silently. `AndroidSupertonicSpeechReader` engines are `RefCountedTts`, captured per read. (Slice 51: docs/plan/slices/51-multilingual-voices.md)
- OCR on Android: PaddleOCR `kind: ocr` packs per language, ML Kit Latin as fallback; the pure pipeline is `Application/Images/Ocr`. `Microsoft.ML.OnnxRuntime.Managed` stays version-matched with sherpa's bundled `libonnxruntime.so` (both 1.24.3); bump together, never add a native onnxruntime package. (Slice 52: docs/plan/slices/52-paddleocr-per-language.md)
- UI strings live in `Mobile/Resources/Strings/AppResources*.resx` via the typed `AppResources`, never literals. A new key goes in the neutral file, all eight language files and `AppResources.cs` (`LocalizedUiTests` enforces it). `UiCulture.Apply` sets the culture; RTL pages set `FlowDirection`, paddings use `UiFlowDirection.Mirror`. (Slice 53: docs/plan/slices/53-localized-ui-rtl.md)
- Translation: `Application/Translation` (`TranslationFlow`, pure) with ML Kit in `Platforms/Android`. The translated text is read; the editor and resume identity keep the original. Text never leaves the device. (Slice 54: docs/plan/slices/54-on-device-translation.md)
- Android sentence audio is cached by `SentenceAudioCache` (`CacheDirectory/sentences`); never play cached clips directly, join them into a temp file. `IHeadStart` prepares the first chunks, skipped while translation is on. Windows keeps `SpeechTextChunker.HeadPolicy.Default` until Slice 56. (Slice 47: docs/plan/slices/47-head-start-sentence-cache.md)
- iOS neural reading uses sherpa-onnx 1.13.8 + `SherpaOnnxC.xcframework` 1.13.8 (Android pins 1.13.3); bump package and framework (URL and SHA-256 in the Mobile csproj) together. The reader sits behind `IosNeuralReaderFactory` (apps without one, like Dino Reader, keep the system voice); the audio session goes through `IosAudioSession`, never `AVAudioSession` directly. (Slice 49: docs/plan/slices/49-ios-parity.md)
- Multi-voice reading is `ISpeechReader.SpeakSegmentsAsync` (native only where `SupportsSegments`; else one voice). `ChunkChanged` may fire off the UI thread: marshal. (Slice 60: docs/plan/slices/60-per-character-voice-reading.md)
- Dino Reader: stories are `AppDataDirectory/stories/<guid>/story.json` via `FileStoryLibrary`, images only through `TryResolveFile`; grown-up pages implement `IGrownUpPage`, and app-launched activities are wrapped in `ExternalActivityGuard`; tokens come from `DinoTokens`. (Slice 61: docs/plan/slices/61-gallery-grownup-gate.md)
- Dino Reader reading: `StorySentences` and `SegmentSentenceSplitter` map `ChunkChanged` 1:1 to sentences; `FlowPager`/`PageComposer` (`DinoReader.Core`) paginate off the UI thread; story text ignores the font scale. (Slice 62: docs/plan/slices/62-reading-screen-modes.md)
- Dino Reader cast: edits go only through `CastEditor` and pin voices via `CastMember.Edited`; merges live in `Story.Merges`, applied each read by `CastMerges` in `StoryVoicing`, never in stored text; an unknown speaker gets the narrator voice (`VoiceCaster.VoiceForLine`). (Slice 65: docs/plan/slices/65-cast-screen.md)
- Scanner/OCR stack (ML Kit, PaddleOCR, document scanner, PDF renderer, page-image keeper) lives in `Mobile.Shared/Platforms/Android`, registered once in `MobileSharedServices.Android.cs`; apps must not register their own and must forward `OnActivityResult` to `AndroidDocumentScanner.HandleActivityResult`. Positioned text uses `NormalizedRect` (0..1, top-left); `RangeToBoxMapper` maps story ranges to lines. Translation stays in the RTST app. (Slice 63: docs/plan/slices/63-positioned-text-extraction.md)
- OCR shared by Android and iOS is `Mobile.Shared/Ocr/` behind `IBuiltInTextRecognizer`. iOS ships its own `Microsoft.ML.OnnxRuntime` (sherpa's xcframework links it statically, no `OrtGetApiBase`); no translator on iOS, so the translate card stays hidden. (Slice 55: docs/plan/slices/55-ios-batch9-features.md)
- Dino languages: rules per language in `RuleSets` (cached per `AppLanguage`, `LanguageRules.For(code)`); only `ReadingPage` switches `SpokenLanguage`, never the cast screen; translation stays hidden until an `ITranslator` is registered in Dino's DI. (Slice 66: docs/plan/slices/66-dino-reader-languages-translation.md)
- Windows packs (Slice 56): the MSIX holds no voice model and declares `internetClient` (STORE.md, PRIVACY.md and `site/privacy.html` say so). Packs live in `LocalCacheFolder\packs\<id>`, never `LocalFolder` (cloud-backed). `PackVoiceModelService` and `WindowsVoicePackService` are Windows copies of the Mobile ones, `VoicePackSetup` (Application) the MAUI-free twin of `VoicePackInstaller`: change both sides together. `windows.yml` has no LFS; keep `RemoveWinMlOnnxRuntimeDuplicate`. (Slice 56: docs/plan/slices/56-windows-packs-language-headstart.md)
- Windows head start (Slice 56): `SupertonicSpeechReader` implements `IHeadStart`, backed by `SentenceAudioCache` in `LocalCacheFolder\sentences`; chunks are joined into the per-read play file, so `DeleteRead` and the startup sweep never touch cached clips. `ReadAloudService.BeginAutoRead` prepares after an 80 ms stable wait (`HeadStartStableMs`); hotkey and Play reads do not. Reads are owned by `ActiveRead`/`ReadOwnership`. (Slice 56: docs/plan/slices/56-windows-packs-language-headstart.md)

Keep `App.xaml.cs` thin: provider/DI wiring and window bootstrap only. Real
logic lives in Application/Infrastructure; XAML views stay free of business
logic.

## Build & run

```bash
dotnet build src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64
dotnet run   --project src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64
```

Builds are **per-architecture** — always pass `-p:Platform=x64` (or `arm64`).
Building the **app project** is the quickest way to build the whole graph.

The solution (`ReadTheStupidText.slnx`) also builds directly — e.g.
`dotnet build ReadTheStupidText.slnx -p:Platform=x64` — and opens in Visual
Studio. The class libraries are `AnyCPU` and the app is `x86;x64;ARM64`; the
`.slnx` bridges the gap with explicit `<Configurations>` (platforms only, no
`Any CPU`) and a per-app `<Platform Solution="Debug|x64" Project="x64" />`
mapping, plus a `<Deploy Solution="Debug|x64" />` rule so F5 deploys the
package. Debug through the **(Package)** profile — there is no unpackaged
profile, because running unpackaged fails with `REGDB_E_CLASSNOTREG` (no
package identity).

**Mobile (Android):**

```bash
dotnet build src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj -f net10.0-android
```

Deploy to a device/emulator from Visual Studio (the **Mobile** project's Android
debug target), or `dotnet build -t:Run -f net10.0-android` with a device/emulator
already running. No AVD ships with this repo — set one up locally (or attach a
physical device) before relying on a slice that touches the Mobile project.

CI/packaging (Slice 5, Slice 70): `.github/workflows/windows.yml` (called by `release.yml`) packages the single-project
MSIX (x64 + ARM64, unsigned — the Store re-signs; uploaded to the Release, never
as an Actions artifact — Batch 8) and checks out LFS for
the voice model; see `STORE.md` for capability justification, licenses, and the
Store/deployment details. The app is **live in the Store** (`9NGT1BN1H92V`);
`store-submit.yml` pushes updates (bundles both arches → one `.msixbundle` →
msstore CLI) once the Partner Center secrets are set. The **Release** MSIX is
built **.NET self-contained** (`<SelfContained>true</SelfContained>`, non-Debug
only) so the .NET runtime ships inside the package — this fixed Store cert
**10.2.4.1** (undisclosed dependency on .NET); the Windows App SDK runtime stays
framework-dependent (Store-delivered). Debug stays framework-dependent for a fast
inner loop. Trimming stays off (`PublishTrimmed=false`) — packaged WinUI apps
aren't trimmed.

**Android downloads.** The Worker serves `/download`, `/download/android/latest` and
`/download/android/<x.y.z>` from the private R2 bucket `readthestupidtext-downloads` (binding
`DOWNLOADS`; `worker/downloads.ts`). The `publish-downloads` job copies each released APK there
after `release-publish` (no signing secrets; uses `CLOUDFLARE_API_TOKEN`/`CLOUDFLARE_ACCOUNT_ID`).
The key layout lives only in `worker/download-keys.mjs`; an already-published version is skipped and
the manifest is written last. Windows stays Store-only. A deploy needs the bucket to exist (wrangler
fails on a missing R2 binding). (Slice 69: docs/plan/slices/69-apk-downloads-on-r2.md)

**Play loading state.** `ReadAloudService.IsGeneratingAudio` is true from `GeneratingAudio` until the
first `Playing`; `GeneratingChanged` is deduped but not ordered, so handlers re-read it. After
`SpeakAsync` returns a read gets `FirstAudioGrace` (8 s) to reach `Playing` before being marked
Failed (the WinRT fallback returns before playback starts), so any new `ISpeechReader` must await the
whole read or raise `Playing` within that grace. Play does nothing while generating. The control
panel overlays a `ProgressRing`; Mobile `ReaderPage` tracks a per-read token and overlays an
`ActivityIndicator`. (Slice 46: docs/plan/slices/46-play-loading-state.md)

**Landing site.** `readthestupidtext.sirous.uk` is the Cloudflare Worker `readthestupidtext`
(`wrangler.jsonc`, `worker/index.ts`) serving `site/` (not `docs/`); GitHub Pages is off. Deploy with
`npm run deploy` (refuses CRLF site files; `npm run typecheck` keeps `wrangler.jsonc` and
`wrangler.workers-dev.jsonc` in sync) manually when needed. Workers Builds deploys `main` only (preview-branch builds are off); deploy
command `npm run deploy`, watch paths `worker/*`, `site/*`, `wrangler*.jsonc`, `package*.json`. A Worker custom domain can't replace an existing DNS
record: delete it first (wrangler has no DNS scope). Privacy policy edits touch both `PRIVACY.md` and
`site/privacy.html`. Don't add `docs/CNAME` or a GitHub Actions deploy. See `docs/DEPLOYING.md`.
(Slice 68: docs/plan/slices/68-landing-site-on-worker.md)

## Tests

Unit tests live in `tests/ReadTheStupidText.Tests` (xUnit v3, `net10.0-windows`),
covering the **pure** logic only — `PlaybackRate`, `SpeechTextChunker`,
`SupertonicVoiceTable`, `ActivityLog`, and the `LocalSettingsStore` settings
migration (via the pure `ResolveAutoReadFlag`; internals are exposed through
`InternalsVisibleTo`). The WinUI/WinRT/native paths (readers, monitors, windows)
are **not** unit-tested — they need package identity / a UI host; verify those by
running the app (VS **(Package)** profile). Run the suite with:

```bash
dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj
```

Dino Reader's pure library has its own suite (xUnit v3, plain `net10.0`, runs on any OS):

```bash
dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj
```

The `tests.yml` workflow (called by `release.yml`) runs them on every push/PR that
touches app code (docs-only changes skip it) and **gates** the build and release jobs,
so a failing test blocks the release.

## Versioning, license & releases (Batch 2 — Slices 11–16)

- **License is MIT** (Decision 16 in `docs/plan/decisions.md`). Anyone may use/extend/sell it
  provided the copyright notice is kept. Do **not** add GPL/LGPL components (the
  Supertonic/sherpa stack was chosen to keep this clean — see `STORE.md`).
- **Every merge to `main` that touches app code ships a version** (Decision 17;
  docs-only merges cut none, and only the changed platforms are rebuilt and
  submitted — Batch 8), derived by
  **GitVersion** from git history — **git tags are the source of truth**. The
  single `release.yml` run does it all: GitVersion computes the next SemVer, the
  MSIX is packaged with that version stamped into `Package.appxmanifest`
  `Version` (`x.y.z.0` — the Store needs revision `0`) **at build time** (not
  committed), then a `v<x.y.z>` tag + GitHub Release are cut at the merge commit.
  No PAT, no commit-back. **`main` defaults to a patch bump.** To bump higher,
  put a token in a commit message (highest since the last tag wins): **`+semver:
  minor`** for a feature, **`+semver: major`** for a breaking change, `+semver:
  none` to skip. Config: `GitVersion.yml`. So: write a normal commit and add
  `+semver: minor`/`major` when the change warrants it; otherwise it's a patch.
- **Signing:** CI stays **unsigned**; the Microsoft Store re-signs on publish
  (Decision 18). A domain (e.g. `sirous.uk`) cannot sign code. **Azure Trusted
  Signing** is the documented upgrade for trusted sideloaded MSIX — see
  `STORE.md`.

## Code quality (project-specific reminders)

- Genuinely closed sets (reading state, etc.) are `enum`s — stringify only at
  the UI boundary. Reading speed is **not** closed: it's the `PlaybackRate`
  value object (decimal, range/step enforced in the type).
- No magic strings for settings keys / manifest names — use named constants.
- The backend/infrastructure returns structured data; the UI composes any
  user-facing copy.
- Every method does one thing; 3–30 lines.

## Out of scope (v1)

Voice *tuning* beyond playback rate (pitch/volume/SSML), a **persistent/dockable**
settings window (tabs, taskbar presence, hotkey-remap UI), and non-Store
distribution as the primary channel. See `scope.md`.

**Console / non-UIA limitation (by design, not a bug).** The console (Windows
Terminal, PowerShell, `cmd`) and some editors (e.g. Visual Studio's code editor)
expose **no UI Automation text selection**, so *selecting* text in them raises no
OS event — there is nothing to auto-read on. Reading from them therefore requires a
**copy** (auto-read-on-copy via the clipboard listener, gated by the **Auto-read
on copy** toggle) or the **global hotkey** (`Ctrl+Win+R`, copies then reads). Windows
Terminal's `copyOnSelect` makes selecting auto-copy, which then triggers auto-read.
Detecting a bare console *selection* is **out of scope** — Windows provides no API
for it. When a request asks to "read on selection in the terminal," the answer is
copy / `copyOnSelect` / hotkey, not a new selection-detection mechanism.

Note (Windows; Android/iOS use downloaded packs): the narrator voice is a **local neural voice** (Slice 9, Decision 14) — the
**sherpa-onnx** runtime (Apache-2.0) running the **Supertonic-3** model
(Apache-2.0, `sherpa-onnx-supertonic-3-tts-int8-2026-05-11`), via
`org.k2fsa.sherpa.onnx`. The model (~145 MB) is a **downloaded pack** (Slice 56,
see the Windows rules under Mobile rules below; `VoiceModel/` stays in the repo only as the source for `packs.yml`); the picker offers the pack's
voices plus a labelled "Windows voice". The Supertonic voices (`SupertonicVoiceTable`, the fixed 10-style F1–F5/M1–M5 set in
sorted sid order, modelled as `VoiceInfo` records). The built-in WinRT
`SpeechSynthesis` voice is the first-run voice until the pack lands and the fallback
if the pack is missing; it is selectable as "Windows voice" (Slice 56). Narrator's
"Natural"/neural voices are unreachable by a Store app, so we bring our own
engine. **Piper is GPL — do not use it; Kokoro was rejected** (Chinese-focused,
no English male voice in the latest, ships GPL-adjacent espeak data); Supertonic
is English-first, Apache-2.0, espeak-free, and Store-safe. Note the build-time
`onnxruntime.dll` dedupe in the App `.csproj` (WinML and sherpa both ship one; we
keep sherpa's).

Note: a **left-click tray control panel** is **in scope** (Slice 8, see
Decision 12 in `docs/plan/decisions.md`) — a borderless, always-on-top `AppWindow`, **pinned
above all windows** (it stays open until the ✕ or another tray click; it does
*not* light-dismiss), sized to its content. It is *not* a persistent settings
window: it's transient and every control maps to an existing service. The
right-click `MenuFlyout` stays (Quit lives there only). Rich controls (slider,
`ComboBox`) **cannot** go in H.NotifyIcon's `PopupMenu` — that's why the panel
is a real window, not a flyout (same native-menu limit as Decision 11).

As of **Slice 13 (Decisions 20–21)** the panel is the **"Media Card"** design
(`design_handoff_tray_panel/`): a brand-gradient header (glyph watermark, `NOW
READING` eyebrow, title, a live 5-bar **waveform** + dynamic status text, and a
transport row = 40px play/pause circle + **live progress bar** + speed pill) over
a compact Fluent body and a `Ctrl+Win+R` hotkey footer. **Compact rev (post-Slice
13):** the body no longer stacks full-width labelled rows — the **fine 0.05 speed
slider is hidden in the header** and only revealed when the speed pill (now a
`ToggleButton` carrying a flipping chevron) is tapped (the panel re-fits its height
on reveal). **Design refresh (post-Slice 13):** revealing the speed control now
shows the slider **plus a row of six quick-preset chips** (0.5x / 1x / 1.25x / 1.5x
/ 1.75x / 2x, `SpeedPresetStyle`); tapping a chip drives the slider, and the chip
matching the current rate is highlighted (solid-white fill + blue text) from code
(`ApplyPresetVisual`/`UpdatePresetHighlight`) — slider and chips stay in sync. The
body is a single **voice picker** row (the `ComboBox` now **stretches full width** +
a 34px **activity-log button** that opens `ActivityLogWindow` via the panel's
`ActivityLogRequested` event) above a `CONTROLS` row of **three square icon
toggles** — auto-read on selection, auto-read on copy, launch-at-startup — each a
styled `ToggleButton` (off = card fill + muted icon, on = accent fill + white icon
via its `CheckStates`), now **70px tall (radius 11)** with **larger stroked line
icons**, and a hover tooltip naming the control and its on/off state. The toggle
icons are **stroked `Path` shapes** (the design's source SVGs: a select-frame and
stacked sheets, each enclosing a two-wave sound glyph; launch-at-startup is a
**rocket** — Segoe Fluent has no rocket glyph, and the design deliberately avoids
the power symbol); their stroke colour is the toggle's icon colour, set in code via
`ApplyToggleVisual`. Built with
native WinUI Fluent controls + light/dark `ThemeDictionaries` from the design
tokens (the HTML is the visual source of truth, not shipped code); it **keeps** the
pinned-topmost behavior (no click-away/Esc dismiss). The progress bar is driven by
`ISpeechReader.ProgressChanged` (a 0..1 read-through fraction from `MediaPlayer`
position + chunk index, weighting chunks equally) and is **display-only** — true
scrubbing/seek is out of scope (Decision 21); the status line is composed in the
View from `ReadAloudService.CurrentReadWindow`/`CurrentReadTrigger`.

**Read-through timer (Slice 25, Decision 33).** A `TimerText` label under the
transport row shows `elapsed/total` as `mm:ss/mm:ss` (`--:--` while the total is
unknown), driven by `ISpeechReader.TimingChanged` (a new `ReadTiming(TimeSpan
Elapsed, TimeSpan? Total)` in `Application.Reading`, formatted by the pure
`ReadTimingFormatter`). Unlike the progress bar's equal-chunk-weighting estimate,
elapsed/total use each chunk's *real* synthesized duration, tracked by
`Infrastructure.Reading.ReadTimingTracker` — a pure state machine (unit-tested
without the WinRT engine) shared by both `ISpeechReader` implementations. A
chunk's duration is known the instant its audio is generated, not when it plays,
so `Total` can appear well before the read reaches it; `TimingChanged` fires
immediately whenever `Total` newly becomes known (bypassing the ~1s throttle used
for ordinary ticks) and resets to `00:00/--:--` on the same Idle transition that
zeroes the progress bar.

The panel is **draggable by its gradient header** and **remembers where it was left**
(Slice 24, Decision 31): pointer-press/move/release handlers on `HeaderBorder` move the
borderless `AppWindow` by the raw screen-cursor delta (`GetCursorPos`, not
element-relative coords, which would jitter as the window moves), and the final position
is persisted as `ISettingsStore.PanelPosition` (the new `PanelPosition(X, Y)` record,
device pixels, two `PanelX`/`PanelY` keys). `PositionPanel` opens the panel at that saved
spot — **clamped to the work area** (`ClampToWorkArea`, via `DisplayArea.GetFromPoint`) so
a now-offscreen/rearranged-monitor point is pulled back on-screen — and only falls back to
the default bottom-right pin when the user has never moved it. Child controls (buttons,
slider, pill) mark their pointer input handled, so a drag only starts on the header's empty
areas. It still **keeps** the pinned-topmost / no-light-dismiss behavior — only header-drag
+ remembered position is added, not min/max/resize chrome.

Note: read activity flows through **`IActivityLog`** (Application — an in-memory,
observable ring buffer; `ActivityEntry`/`ActivityState`/`ActivityTrigger`/
`WindowSource` in Domain — plus **`ActivityReason`** for *why* an entry deviated).
`ReadAloudService` opens an entry per intercepted text and transitions it
(pending→**generatingAudio**→reading→read, or ignored/interrupted/failed), tagging
each deviation with an `ActivityReason` (`NewSelection`/`Deselected`/`Error`) shown
in the log's **Reason** column. `GeneratingAudio` ("Generating audio") covers the
synthesis wait before any audio plays and flips to `reading` on the reader's first
`Playing` transition. Each entry also records its **`ActivityTrigger`** (auto-read /
hotkey / manual / **clipboard**) in the **Trigger** column and the originating
foreground window (`WindowSource` = app + title, via `IForegroundWindow`) in the
**Source** column. `ActivityLogWindow` (a normal resizable window, opened from the
right-click tray menu) renders it live.

Note: alongside the in-memory log, the app writes **two daily diagnostic files to
disk** (Slice 21, Decision 27) under the package **TemporaryFolder** (`logs\`): a
**system log** (`system-YYYYMMDD.log`) via **Serilog** (rolling file, Info/Debug/
Warning/Error, fixed levels — not user-configurable) carrying every id-correlated
action and exception, and an **input log** (`input-YYYYMMDD.log`) — a TSV the
`ActivityInputLog` writer appends **one row per activity-state transition** to
(append-only; it never rewrites a row), columns = the Activity-Log grid plus the
entry **id**, so the two files **join on the id**. Both store **redacted** text only
(the Slice 20 sanitizer runs first); raw text never touches disk. Serilog's rolling
sink stamps the day as `YYYYMMDD` and can't prefix it, so the files are
`system-…`/`input-…` rather than the `yyyy-MM-dd-…` order in Decision 27. The
abstractions are **`ISystemLog`** + **`ILogFolder`** (Application); `LogPaths`
(resolves the folder + a **7-day** startup retention sweep), `SerilogSystemLog`,
`ActivityInputLog` and the pure `InputLogRow` formatter live in Infrastructure.
`ActivityLogWindow` has an **Open logs** button (`Launcher.LaunchFolderAsync`). The
former `UiaSelectionMonitor` `Debug.WriteLine` traces now go through `ISystemLog`.
This is local-only (consistent with the "we collect nothing" stance) — no network,
no telemetry export.

**Three auto-read/read paths feed the log:** the **UIA selection monitor**
(`ISelectionMonitor`, gated by the **Auto-read on selection** toggle), the
**clipboard monitor** (`IClipboardMonitor` = `ClipboardFormatListener`, a Win32
`WM_CLIPBOARDUPDATE` listener on the tray window's handle — the path for the
**console**/Windows Terminal and other apps with no UIA text selection; gated by
the **Auto-read on copy** toggle), and the global **hotkey** (always on). The two
auto-read gates are independent `ISettingsStore` flags (`AutoReadOnSelection` /
`AutoReadOnCopy`, both default on; an old single `IsEnabled=false` migrates to
both off — Decision 22), each surfaced as a `ToggleSwitch` in the control panel
and the right-click menu. `ReadAloudService` de-dupes across them via
`_lastTriggeredText` so the hotkey's own copy (clipboard echo) and a
copy-on-select duplicate of a UIA selection don't read twice. The UIA monitor
emits **`SelectionCleared`** only on a *genuine* empty selection (deselect) so an
in-progress read is interrupted — a **transient cross-process read failure** is
kept distinct (`SelectionOutcome.Unavailable`) and left silent, so a selection
event re-fired mid-selection no longer falsely ignores/interrupts the read. The
log is the diagnostic for "selection does nothing": no entry ⇒ no UIA selection
event *and* nothing copied (with auto-read on, copying in the console now reads;
the hotkey is the always-available fallback). See Slice 10 / Decision 15 in
`docs/plan/decisions.md`.

The neural reader (`SupertonicSpeechReader`) **chunks** long text
(`SpeechTextChunker`, paragraph→sentence→word) and synthesizes the chunks
**concurrently** while playing them **in order**, so a long read starts
speaking after the first chunk. The **first** chunk is biased toward a single
sentence (Slice 18, Decision 25) so time-to-first-audio is a short synthesis, not a
whole paragraph; later chunks keep the ~200-char paragraph→sentence→word split. A
superseded/stopped read is torn down via a
**generation counter + `CancellationToken`** (`ISpeechReader.Stop()`), so stale
synthesis can never reach the shared `MediaPlayer`; `read` is marked only on the
reader's **`Completed`** (natural end), never on a stop-induced idle.

**Latency instrumentation + adaptive threading (Slice 22, Decision 30).** The reader
takes `ISystemLog` and writes per-read Debug lines so a slow read is attributable:
`split … into K chunk(s) in X ms (threads T, concurrency C)`, a per-chunk
`chunk i/K (n chars): generate X ms, wav Y ms`, and a one-shot `first audio after X
ms` (on the first chunk's `MediaOpened`). Each is stamped with the activity-log
**id**, threaded in via a new optional `ISpeechReader.SpeakAsync(text, activityId)`
param (the WinRT fallback ignores it), so the lines join the input log. The sherpa-onnx
knobs are **adaptive to `Environment.ProcessorCount`** (no longer fixed at threads 2 /
degree 3): `SynthesisThreads = clamp(cores/2, 2, 4)` (`config.Model.NumThreads`,
confirmed via context7 `/k2-fsa/sherpa-onnx`) and `MaxSynthesisConcurrency =
clamp(cores/threads, 2, 4)` — latency-first (more ONNX threads shorten the single
first-chunk synthesis the user waits on), sized so `threads * concurrency` fits the
cores without oversubscribing.

**Voice swap continues the current read (Slice 23, Decision 29).** Changing the voice
actor **mid-read** no longer waits for the next read. The playback loop
(`SpeakChunksAsync`) takes the speaker as a **parameter** (not the mutable field), so a
change can't half-apply to chunks already queued; the switch happens cleanly at the
**next chunk boundary**. `SetVoice` just records the selection (`_speakerId`); when the
current chunk finishes, the loop notices `_speakerId` differs from the speaker it was
started with and restarts at the **next** chunk in the new voice (`BeginGeneration`
cancels the old-voice synthesis already in flight for upcoming chunks). So the current
chunk finishes in the old voice — nothing already heard is **repeated** and no unheard
text is **skipped** — and earlier chunks are never re-synthesized. A single-chunk read
(or a change during the last chunk) just applies to the next read. Driven from
`ReadAloudService.SetVoice` (neural-only via `CompositeSpeechReader`).

To kill the cold-start stall on the **first** read (Slice 17, Decision 24), the
engine is **warmed at startup**: once `IVoiceModelService` locates the model,
`ReadAloudService` calls `ISpeechReader.WarmUpAsync()`, which (on the neural
reader) builds the `OfflineTts` and runs one **discarded** tiny synthesis on a
background thread to JIT the ONNX graph. The lazy `EnsureTts()` stays as the
safety net for a read that arrives before warm-up finishes; the build is
double-checked under a lock so the eager warm-up and a first read can't each load
the ~145 MB model. The WinRT fallback's `WarmUpAsync()` is a no-op.

Auto-read uses an **adaptive settle** (Slice 18, Decision 25) rather than a flat
delay: a lone select (click/double-click) reads after a short **150 ms** baseline,
but while events keep arriving within `BurstGapMs` (a live drag) each one extends
the wait to **500 ms**, so a drag still collapses to one read. The
swapped-`_selectionCts` supersede makes any late extra read harmless. Both auto-read
paths (UIA selection + clipboard copy) share this debounce in `ReadAloudService`.

**Local-only timing diagnostics (Slice 19, Decision 26).** Each `ActivityEntry`
carries two nullable diagnostics — **`TimeToFirstAudio`** (entry created → the
reader's first `Playing` transition) and **`SynthesisDuration`** (GeneratingAudio →
first `Playing`, the metric Slice 17's warm-up shrinks). `ReadAloudService` measures
them with monotonic `Stopwatch` timestamps and calls `IActivityLog.RecordTiming` at
the first-Playing transition; `ActivityLogWindow` shows them as the **First audio**
and **Synth** columns (`TimingText` formats ms/s, blank until measured). This stays
**in-memory, cleared on restart, never transmitted** — no third-party SDK, no
network, no privacy-policy change (consistent with the "we collect nothing" stance).
For *dev-time* tuning only, you may optionally instrument the read pipeline with
**OpenTelemetry** `Activity`/`Meter` and attach a **local Aspire dashboard** (an OTLP
viewer run on the dev machine) — **not shipped** in the MSIX; .NET Aspire was
rejected as a redistributable mechanism (it orchestrates *distributed* apps at dev
time and would reintroduce telemetry-export/cost concerns). See `STORE.md`.
