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

Tray control panel (Slices 8, 13, 24, 25; docs/plan/slices/08-tray-control-panel-window.md, 13-media-card-control-panel-redesign-media.md, 24-draggable-position-persisted-control-panel.md, 25-read-through-timer.md): a left-click panel, a borderless always-on-top `AppWindow` pinned above all windows (closes only on the X or another tray click, never light-dismisses), sized to its content. It is transient, not a settings window; the right-click `MenuFlyout` stays (Quit lives only there). Rich controls (slider, `ComboBox`) cannot go in H.NotifyIcon's `PopupMenu`, which is why it is a real window. It is the "Media Card" design (`design_handoff_tray_panel/`, the HTML is the visual source of truth): gradient header (waveform, status, play/pause, display-only progress bar from `ISpeechReader.ProgressChanged`, speed pill that reveals the 0.05 slider plus six preset chips kept in sync via `ApplyPresetVisual`/`UpdatePresetHighlight`), a full-width voice `ComboBox` + activity-log button (`ActivityLogRequested`), and three 70px stroked-icon `ToggleButton`s (auto-read selection, auto-read copy, launch at startup; icon colour set in `ApplyToggleVisual`). Seek/scrubbing is out of scope. `TimerText` shows `mm:ss/mm:ss` from `ISpeechReader.TimingChanged` (`ReadTiming`, `ReadTimingFormatter`); elapsed/total use real synthesized chunk durations from `ReadTimingTracker` (pure, shared by both readers); `Total` fires immediately when newly known, and resets on Idle. The header drags the window by raw `GetCursorPos` delta; the position persists as `ISettingsStore.PanelPosition` (`PanelX`/`PanelY`), is clamped to the work area (`ClampToWorkArea`) on open, and defaults to bottom-right. Child controls mark pointer input handled so only empty header areas drag.

Activity log and diagnostics (Slices 10, 19, 21; docs/plan/slices/10-live-activity-log-auto-read-fix.md, 19-local-only-timing-diagnostics.md, 21-daily-on-disk-logs-open-logs.md): `IActivityLog` (Application, in-memory ring buffer) holds `ActivityEntry` (state pending, generatingAudio, reading, read, ignored, interrupted, failed; `ActivityTrigger`; `ActivityReason` NewSelection/Deselected/Error; `WindowSource` via `IForegroundWindow`), rendered by `ActivityLogWindow`. Two daily files under the package TemporaryFolder `logs\`: `system-YYYYMMDD.log` (Serilog, fixed levels) and `input-YYYYMMDD.log` (TSV, one append-only row per state transition, `ActivityInputLog`/`InputLogRow`). They join on the entry id and hold only redacted text (Slice 20 sanitizer runs first; raw text never touches disk). Abstractions `ISystemLog` + `ILogFolder`; `LogPaths` does a 7-day startup sweep; `ActivityLogWindow` has an Open logs button. `ActivityEntry.TimeToFirstAudio` and `SynthesisDuration` are in-memory only, never transmitted (no SDK, no network, no privacy-policy change). Dev-time OpenTelemetry plus a local Aspire dashboard is optional and never shipped; Aspire was rejected as a redistributable mechanism (see `STORE.md`).

Three read paths (Slices 10, 12; docs/plan/slices/10-live-activity-log-auto-read-fix.md): the UIA selection monitor (`ISelectionMonitor`, toggle Auto-read on selection), the clipboard monitor (`IClipboardMonitor`, `ClipboardFormatListener`/`WM_CLIPBOARDUPDATE`, toggle Auto-read on copy; the path for console and other no-UIA apps) and the always-on hotkey. The two gates are independent `ISettingsStore` flags (`AutoReadOnSelection`/`AutoReadOnCopy`, default on; an old `IsEnabled=false` migrates to both off), shown in the panel and the right-click menu. `ReadAloudService` de-dupes via `_lastTriggeredText` (hotkey clipboard echo, copy-on-select duplicates). The UIA monitor emits `SelectionCleared` only on a genuine empty selection; a transient cross-process failure is `SelectionOutcome.Unavailable` and stays silent. Diagnosing "selection does nothing": no log entry means no UIA event and nothing copied.

Neural reader (Slices 17, 18, 22, 23; docs/plan/slices/17-warm-the-neural-engine-at-startup.md, 18-adaptive-settle-delay-smaller-first-chunk.md, 22-read-latency-instrumentation-low-risk-tuning.md, 23-voice-swap-continues-the-current-read.md): `SupertonicSpeechReader` chunks text (`SpeechTextChunker`, paragraph then sentence then word, ~200 chars; the first chunk biased to one sentence) and synthesizes chunks concurrently, playing them in order. A stale read is torn down by a generation counter + `CancellationToken` (`ISpeechReader.Stop()`) so stale synthesis never reaches the shared `MediaPlayer`; `read` is marked only on `Completed`, never on a stop-induced idle. Per-read Debug lines (split, per-chunk generate/wav ms, first audio) carry the activity id via the optional `SpeakAsync(text, activityId)` param. Sherpa knobs adapt to `Environment.ProcessorCount`: `SynthesisThreads = clamp(cores/2, 2, 4)`, `MaxSynthesisConcurrency = clamp(cores/threads, 2, 4)`. A mid-read voice change applies at the next chunk boundary: `SpeakChunksAsync` takes the speaker as a parameter, `SetVoice` records `_speakerId`, the loop restarts at the next chunk (`BeginGeneration` cancels old-voice synthesis); nothing is repeated or skipped. Startup warm-up: `ReadAloudService` calls `ISpeechReader.WarmUpAsync()` once the model is located (builds `OfflineTts`, runs one discarded synthesis); `EnsureTts()` is double-checked under a lock as the safety net; WinRT's is a no-op. Auto-read settle is adaptive: 150 ms for a lone select, extended to 500 ms while events arrive within `BurstGapMs`; the swapped `_selectionCts` makes late reads harmless; both auto-read paths share it in `ReadAloudService`.
