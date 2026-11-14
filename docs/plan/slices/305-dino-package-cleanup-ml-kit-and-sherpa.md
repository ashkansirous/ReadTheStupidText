# Slice 305 — Dino package cleanup: ML Kit, sherpa-onnx and onnxruntime out of the Dino packages
**Product:** Dino

*Built 2026-11-07 (wave 3, night 2026-11-07-1412, PR #920), merged as PR #920 (d2ac88fd). Android checked on the emulator; iOS and Windows compiled only. Planned 2026-11-07.* From [Slice 278](278-dino-store-books-only.md) A4 and B4. ML Kit (Translate, TextRecognition, DocumentScanner; iOS MLKitTranslate) still reach the Dino Android and iOS packages through Mobile.Shared/Documents, though Dino no longer calls them. The sherpa-onnx/onnxruntime natives and the shared speech registrations also still reach Dino through Mobile.Shared; Dino's csproj keeps its three sherpa/onnxruntime imports (`AndroidSherpaPackaging.props`, `IosSherpaPackaging.props`, the WinML target) to neutralise them. Moved from plan.md's *Outstanding work*. Android and iOS in the same slice (repo rule): the Dino Windows head is part of it too because of the WinML target.

**Touches:** `src/DinoReader.App/DinoReader.App.csproj`, `src/Sirous.Reading.Mobile.Shared/Sirous.Reading.Mobile.Shared.csproj`, `src/Sirous.Reading.Mobile.Shared/MobileSharedServices.cs`, `src/Sirous.Reading.Mobile.Shared/DocumentScannerRegistration.cs`, `src/AndroidSherpaPackaging.props`, `src/Sirous.Reading.Mobile.Shared/IosSherpaPackaging.props`, `src/SherpaOnnxVersion.props`, `src/DinoReader.App/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `.github/scripts/check-sherpa-pins.mjs`, `.github/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/305-dino-package-cleanup-ml-kit-and-sherpa.md`, `src/Sirous.Reading.Mobile.Shared/Platforms/**`, `src/DinoReader.App/Platforms/**`, `src/Sirous.Reading.Documents/Sirous.Reading.Documents.csproj` (widened at claim: MainActivity scanner call, manifest ML Kit provider, per-platform registrations), `.github/workflows/dinoreader-android.yml`, `.github/scripts/check-android-natives.sh`, `tests/DinoReader.Tests/**` (widened mid-build: Dino's natives check, a switch test; no other live slice touches them)

## Work

- [x] 1. Read how the packages get ML Kit and the sherpa natives (project and package references, the packaging props), and write the map in the slice file (Dino csproj lines, Mobile.Shared lines). (Map below.)
- [x] 2. Move ML Kit, sherpa-onnx, onnxruntime and the speech registrations behind per-app switches (an `IncludeSpeechEngines` / `IncludeMlKit` property on Mobile.Shared that Dino sets false, RTST true), so Dino's packages no longer contain them. (Android and iOS; the Windows head keeps Infrastructure, see Built.)
- [x] 3. Drop Dino's three neutralising imports once nothing needs neutralising. `check-sherpa-pins.mjs` must still pass (versions stay in `SherpaOnnxVersion.props` only). (Two imports dropped; the WinML target stays for the Windows head.)
- [x] 4. Android, iOS and the Windows head: build and publish all three Dino packages; compare package sizes before and after (APK, IPA, MSIX) and list the native libraries in each; RTST's three packages must be unchanged in content. (Android APK compared: Dino 47.1 MB to 23.2 MB, RTST unchanged. Dino Android/iOS/Windows x64 and RTST Android/iOS compile with 0 errors; no IPA or MSIX built, see *Not yet verified*.)
- [x] 5. Dino still runs: emulator flows (shelf, read a store book, record) and an iOS TestFlight look; the Windows VM check for the Windows head only if its project files changed. (Emulator flows pass; the iOS TestFlight look and the Windows VM are in *Not yet verified*.)

## Map (item 1) and what was built

- Before: Dino got ML Kit, sherpa and onnxruntime only through `Mobile.Shared` (its own PackageReferences, `AndroidSherpaPackaging.props` import, the iOS sherpa block) and, on Windows, through `Infrastructure`. NuGet restore does not see a ProjectReference's `AdditionalProperties`, so a build property alone cannot drop a package from Dino's restore graph: `Mobile.Shared` now declares those packages with `PrivateAssets="all"` (so they do not flow) and `ReadTheStupidText.Mobile` declares ML Kit and `Microsoft.ML.OnnxRuntime.Managed` itself (versions in `SherpaOnnxVersion.props`, `MlKit*Version`); the sherpa refs from the props get `PrivateAssets` through a `PackageReference Update` in `Mobile.Shared.csproj` (a `PrivateAssets` inside the props itself dropped `sherpa-onnx.dll` from the RTST APK).
- Switches: `IncludeSpeechEngines` and `IncludeMlKit` (default true in `Mobile.Shared.csproj`, set false in `DinoReader.App.csproj`, true in `ReadTheStupidText.Mobile.csproj`, passed through the ProjectReference). They define `NO_SPEECH_ENGINES` / `NO_MLKIT`, drop the engine and ML Kit sources (targets `DropSpeechEngineSources`, `DropMlKitSources`, `DropIosMlKitTranslator`) and the registrations behind them. The switches are honoured only for the android and ios TFMs (through `SharedSpeechEngines` / `SharedMlKit`); every other target, including the Dino Windows head, is unchanged from main. The lean variant builds into `obj/lean` and `bin/lean`. With the engines off, `ISpeechReader` is the phone's own voice (Android `TextToSpeech`, iOS AVSpeech, Windows WinRT) because the Debug test hooks resolve one.
- Dino: `AndroidSherpaPackaging.props` and `IosSherpaPackaging.props` imports removed, `MainActivity` no longer forwards to `AndroidDocumentScanner`, the manifest no longer removes `MlKitInitProvider`.
- Dino Windows keeps `Infrastructure` (its `LogPaths`/`SerilogSystemLog` back `ISystemLog`), so sherpa and the WinML target stay there until that logging leaves Infrastructure.
- Android Release APK (arm64, unsigned-publish output, `dotnet publish -f net10.0-android -c Release -p:AndroidPackageFormat=apk`): Dino 47.10 MB to 23.2 MB; removed `libsherpa-onnx-c-api.so`, `libonnxruntime.so`, `libmlkit_google_ocr_pipeline.so`, `libtranslate_jni.so` and the ML Kit, Play-services and `sherpa-onnx`/`Microsoft.ML.OnnxRuntime` assemblies. RTST APK 45,388,674 bytes before and after, same file list (only `libassembly-store.so` differs by 8 bytes).
- `dinoreader-android.yml` now runs `check-android-natives.sh --none` on the Dino packages.
- Not done: the Dino Windows head still ships the engine and the WinML target (logging lives in Infrastructure); follow-up below.

**Changed while building:** Touches widened mid-build to `.github/workflows/dinoreader-android.yml`, `.github/scripts/check-android-natives.sh` and `tests/DinoReader.Tests/**` (Dino's natives check, a switch test).

**From review:** (files: `reviews/s305-high.md`, `reviews/s305-medium.md`, `reviews/s305-check1.md`)
- High: applied 1, 2, 3, 4, 5, 8.
- Skipped #6 (`reviews/s305-high.md`): "an RTST-only sherpa packaging edit triggers and ships a Dino Android/iOS release with identical content" (extra path-filter triggers are harmless; listed as a follow-up).
- Skipped #7 (`reviews/s305-high.md`): "the rule exists in two projects" (duplication of the simulator condition, low).
- Medium: applied 1 (one rule for the switches, android/ios only) and 8. #2 checked, no behaviour change: Dino never read `SupportsNeuralVoice`/`SupportsTextScan` (IPlatformServices.cs:96, VoicePackInstaller.cs:283/485/531, RTST ReaderPage.xaml.cs:74, from the fixer's report).
- Skipped #3 (`reviews/s305-medium.md`): "two places must agree" (low; an XML test guards it).
- Skipped #4 (`reviews/s305-medium.md`): "Tests only parse csproj XML" (low).
- Skipped #5 (`reviews/s305-medium.md`): "tools assuming obj" lookups may miss (low; CI's PR builds exercise it).
- Skipped #6 (`reviews/s305-medium.md`): "MlKitInitProvider removal dropped" (low).
- Skipped #7 (`reviews/s305-medium.md`): "Path filters may still list" the sherpa props (low, unconfirmed; harmless).
- Check pass 1: #1 applied (this file's target name); #2 (iOS lean variant not compiled) covered by the Dino and RTST iOS compiles below.

**Checks:** Dino tests 3869 pass, RTST tests 2153 pass, check-sherpa-pins 0, actionlint 0; compiles Dino Android/iOS/Windows x64 and RTST Android/iOS 0 errors.

**Device** (emulator-5554, key 0aecd5c35f98): Dino first-run, open-book, record-page-shows-words and premade-audio-plays pass; RTST paste-and-read passes; no DllNotFound/TypeLoad/sherpa/onnx/mlkit lines in logcat.

**Follow-ups:** remove the engine from the Dino Windows head (move logging out of Infrastructure); `ship-targets` and `dinoreader*.yml` path filters still list the sherpa props (harmless).

## Verification

- No `libsherpa-onnx*`, `libonnxruntime*` or ML Kit module in the Dino Android package and iOS bundle (listed from the built artifact); RTST's lists unchanged; Dino's sizes drop; fast tests and device flows green.

#### For CLAUDE.md (lifted 2026-11-07)

(`src/DinoReader.App/CLAUDE.md` and `.github/CLAUDE.md` are already edited in this diff. The block below belongs in `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`, outside this slice's Touches, so the index pass lifts it.)

- `IncludeSpeechEngines` and `IncludeMlKit` on `ReadTheStupidText.Mobile.Shared.csproj` (Dino false, RTST true) drop the sherpa/onnxruntime and ML Kit sources, packages and registrations. They apply only to the android and ios TFMs (`SharedSpeechEngines` / `SharedMlKit`); the Dino Windows head is unchanged. Packages Mobile.Shared marks `PrivateAssets="all"` must be re-declared in `ReadTheStupidText.Mobile.csproj` or RTST loses them. The lean variant builds into `obj/lean` and `bin/lean`. (Slice 305: docs/plan/slices/305-dino-package-cleanup-ml-kit-and-sherpa.md)

#### Not yet verified

- iOS TestFlight look of Dino and its IPA size (iOS only compiled).
- Windows VM run of the Dino Windows head (unchanged by design; 6.5 GB free).
- A parallel build of both apps in one msbuild invocation.
