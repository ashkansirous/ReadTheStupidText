# Slice 49 — iOS reaches today's Android parity

*Built (wave 2026-10-21b), PR #196.*

**Batch:** 9
**Decisions:** 64, 65 (Apple Vision stand-in), 37-42
**Depends on:** Slice 48 (iOS target builds and reaches TestFlight)
**Touches:** `src/ReadTheStupidText.Mobile/Platforms/iOS/**`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/**`, `CLAUDE.md`
**Story issue:** —

## Goal

The iPhone app does what the Android app does today: type or paste, open a
`.txt`/`.pdf`/`.docx`, scan a document, pick a voice, change the speed, and hear
it read with the natural Supertonic voice (system voice as fallback).

## Steps

Android file -> iOS counterpart:

| Android | iOS |
| --- | --- |
| `Platforms/Android/AndroidSpeechReader.cs` | `Platforms/iOS/AvSpeechReader.cs` (from Slice 48, completed here) |
| `Platforms/Android/AndroidSupertonicSpeechReader.cs` | `Platforms/iOS/IosSupertonicSpeechReader.cs` (sherpa + `AVAudioPlayer`/`AVAudioEngine`) |
| `Platforms/Android/MlKitImageTextExtractor.cs` | `Platforms/iOS/VisionImageTextExtractor.cs` |
| Slice 45 `DocumentScanner` | `Platforms/iOS/VisionKitDocumentScanner.cs` |
| `MainActivity.cs` / `MainApplication.cs` | `AppDelegate.cs` (Slice 48) |

- [x] **Neural voice.** Port `AndroidSupertonicSpeechReader.cs` (596 lines) to
      iOS. The synthesis, chunking, ordered playback, voice swap and skip logic
      is pure C# over `SpeechTextChunker`/`ReadTimingTracker`; only the playback
      primitive (`Android.Media.MediaPlayer`, files written by `WriteWavFileAsync`,
      line 510) changes to an AVFoundation player with a position poll. Prefer
      extracting the shared logic into a platform-neutral base class under
      `Mobile/` (not `Platforms/`) so both readers share it; keep Android's
      behaviour unchanged (Slice 44 rule: shared files rebuild both platforms).
- [x] **Voice model on iOS.** Until Slice 50 removes bundling, the model stand-in
      is `MauiAsset` (csproj lines 58-67); `MobileVoiceModelService` extracts it
      to `AppDataDirectory` via `FileSystem.OpenAppPackageFileAsync`, which works
      on iOS unchanged. Condition the asset on iOS too. Check the `.ipa` size.
- [x] **Routing.** The shared `CompositeSpeechReader` (Slice 44 made it take
      `ISpeechReader`s) picks neural when `IVoiceModelService.IsReady`, else
      `AvSpeechReader`. Register both in `Platforms/iOS/`.
- [x] **File upload.** `ReaderPage.xaml.cs` lines 21-24 define
      `SupportedFileTypes` with only a `DevicePlatform.Android` entry; add
      `DevicePlatform.iOS` with the UTType strings for plain text, PDF and docx.
      The extractors are the shared `Documents` project, no change.
- [x] **Scanner + OCR.** `VisionKitDocumentScanner` presents
      `VNDocumentCameraViewController` and returns page images; implement
      `IDocumentScanner` (Slice 45) and `IImageTextExtractor` via Apple Vision
      (`VNRecognizeTextRequest`, accurate mode). This is a stand-in: Slice 55
      replaces it with PaddleOCR. Add the camera usage string
      (`NSCameraUsageDescription`) to `Info.plist`.
- [ ] **Settings and picker.** `MobilePreferencesSettingsStore` uses MAUI
      `Preferences` and `VoicePickerPage` uses `SupertonicVoiceTable`; both work
      as-is. Verify on device.
- [x] **Audio session.** Configure `AVAudioSession` (playback category) so audio
      continues with the silent switch on and routes correctly; handle interruptions.
- [ ] Real-device pass via TestFlight: type/paste, file (all three formats),
      scan 2 pages, voice change, speed change, skip, pause/resume, backgrounding.
- [ ] Update `CLAUDE.md` Mobile bullet (iOS files, AVSpeech fallback, Vision
      stand-in).

## Acceptance

- On an iPhone via TestFlight: every row of the Android feature list above works.
- Neural voice speaks; with the model absent the AVSpeech voice speaks instead.
- Android build and behaviour unchanged after extracting shared code.

## Risks / notes

- Slice 48's sherpa-onnx iOS native linking is the main risk; if it was deferred
  there, resolving it is step one here.
- Apple Vision OCR quality for Arabic/Farsi is limited; acceptable as a stand-in.
- Slice 47's head start and Slices 50-54 follow in Slice 55.

## Decisions taken during build

- **No shared base class for the readers.** `IosSupertonicSpeechReader` is a port of the Android reader with `AVAudioPlayer` as the playback primitive, not an extraction into a shared base. Slice 60 is editing the Android reader and `Application/Reading` on another branch, so extracting now would conflict and risk Android. Android is untouched. Folding the two together is a follow-up after both slices land.
- **The iOS reader lives in the app project, behind a seam.** sherpa-onnx's native framework is linked by `ReadTheStupidText.Mobile` (the app), and `Mobile.Shared.csproj` (the project file, as opposed to its `Platforms/iOS/**` folder, which is in Touches) is left alone. So the reader is `Mobile/Platforms/iOS/IosSupertonicSpeechReader.cs`, and `Mobile.Shared` gets only `IosNeuralReaderFactory` (a delegate the app registers in `MauiProgram.iOS.cs`) which `IOSPlatformServices` resolves. An iOS app that registers no factory (a future Dino Reader iOS build) keeps the system voice and `SupportsNeuralVoice` false, so it never downloads the pack. `SupportsTextScan` is now true only when the app registered an `IImageTextExtractor`.
- **sherpa-onnx on iOS: 1.13.8 shared framework, not the 1.13.3 Android pin.** The upstream xcframework release tag has no 1.13.3 iOS build (it starts at 1.13.4), and the 1.13.3 static iOS archive needs static-link P/Invoke plumbing. The iOS target therefore takes the managed `org.k2fsa.sherpa.onnx` 1.13.8 and the matching `SherpaOnnxC.xcframework` (shared dylib, onnxruntime linked in, about 30 MB) so the binding's C structs match the native library. It is separate from `AndroidSherpaPackaging.props` (Android stays 1.13.3). The csproj downloads the zip once into `obj/sherpa-ios/` with a SHA-256 check (`FetchSherpaIosFramework`), so no binary is committed. The managed binding calls `sherpa-onnx-c-api`, which does not exist on iOS, so `IosSherpaLibrary` installs a `NativeLibrary` DllImport resolver pointing at `Frameworks/SherpaOnnxC.framework/SherpaOnnxC`.
- **Voice model on iOS: nothing to condition.** The `MauiAsset` stand-in no longer exists (Slice 50 made the voice a downloaded pack), so there is no `.ipa` model to size-check; the pack download path in `MobileVoicePackService` is platform neutral and runs on iOS once `SupportsNeuralVoice` is true.
- **File upload: already done by Slice 48.** `ReaderPage.SupportedFileTypes` already has the iOS UTIs, so `ReaderPage.xaml.cs` and `MauiProgram.cs` are unchanged.
- **Apple Vision OCR reads lines in the order Vision returns them** (top to bottom) with language correction on and automatic language detection on iOS 16+; no re-sorting.
- **Audio.** One `IosAudioSession` helper (playback category, activate/deactivate, interruption observer) is used by both iOS readers. An interruption (call, alarm, Siri) pauses a playing read and resumes it only if the system says to and the pause was the interruption's; a pause the user made is never undone. `UIBackgroundModes` = `audio` was added to `Info.plist` so a read keeps going when the screen locks; this is an App Review item to watch.
- **Scan copy on a device without a document camera.** `ReaderPage` still says the scanner "needs Google Play services" when a scanner reports Unavailable. On iOS that only happens on the simulator (every iPhone supports the document camera). Slice 53 owns `ReaderPage`, so it is left alone here (see the follow-up below; medium-pass finding, skipped).

- **Follow-up, not done here (review finding 7):** `ReaderPage.xaml.cs` still shows Android-only scanner copy ("needs Google Play services"). A parallel slice is localizing `ReaderPage`, so the iOS-appropriate wording is left to it.

- **Follow-up, not done here:** the 16-bit PCM WAV encoder is duplicated between the Windows, Android and iOS Supertonic readers; extract a shared encoder once Slice 60 lands.

## Built (Slice 49)

Compiled for `net10.0-ios` on Windows against the reference assemblies (`dotnet build -f net10.0-ios -p:IncludeIos=true`). The Android build and 190 tests pass. The CI iOS build on the PR is the link proof. Nothing has run on a device.

**Final shape:**

- `IosSupertonicSpeechReader` (`Mobile/Platforms/iOS`): an `AVAudioPlayer` per chunk with a main-thread poll.
  - On natural end it goes through `FinishAsync`: release the player, delete the WAVs, deactivate the session.
  - Each generation tracks its WAVs; `DrainAsync` awaits and deletes them on any end.
  - `PauseNow`/`ResumeNow` are guarded by state. A pause between chunks is honoured. A false `Play()` is treated as a failure.
- sherpa-onnx on iOS: managed 1.13.8 plus `SherpaOnnxC.xcframework` 1.13.8, fetched into `obj/sherpa-ios/` with a SHA-256 check. The `AddSherpaIosFramework` target adds the `NativeReference` after the fetch. `IosSherpaLibrary` is the DllImport resolver.
- `VisionImageTextExtractor` (`VNRecognizeTextRequest`).
- `VisionKitDocumentScanner`: guarded present, JPEG save off the main thread, pages whose save fails are skipped.
- `Mobile.Shared/Platforms/iOS`:
  - `IosAudioSession`: owner-aware activate/deactivate, `NSError` checked, the interruption observer reads the options only on `Ended`.
  - `IosNeuralReaderFactory` seam.
  - `IOSPlatformServices` with optional constructor dependencies.
  - `AvSpeechReader` interruption handling.
- `Info.plist` has `UIBackgroundModes` = `audio`.

## From review (Slice 49)

- **High pass:** 10 findings, 9 fixed. Skipped: a shared WAV encoder (follow-up above).
- **Medium pass:** 10 findings, 9 fixed. Skipped: the Android-only scanner error copy in `ReaderPage` (follow-up above; Slice 53 owns that page).
- Known: `SpeakChunksAsync` in the iOS reader is over the 30-line guideline.

## Changed while building (Slice 49)

The "Audio" decision above describes the first cut of `IosAudioSession`. It is superseded in detail by the final shape: activation and deactivation are owner-aware, and the reader releases the session itself through `FinishAsync`. The sherpa-onnx pin and seam decisions stand as written.

#### For CLAUDE.md

- iOS neural reading uses sherpa-onnx 1.13.8 with `SherpaOnnxC.xcframework` 1.13.8, which differs from Android's 1.13.3 pin. Bump the managed package and the framework (URL and SHA-256 in `ReadTheStupidText.Mobile.csproj`) together. (Slice 49: docs/plan/slices/49-ios-parity.md)
- The iOS reader lives in `Mobile/Platforms/iOS`, behind `IosNeuralReaderFactory` in Mobile.Shared. An app without a factory, such as Dino Reader, keeps the system voice on iOS. (Slice 49: docs/plan/slices/49-ios-parity.md)
- The iOS audio session goes through `IosAudioSession` with an owner. Don't call `AVAudioSession` directly. (Slice 49: docs/plan/slices/49-ios-parity.md)

#### For README.md

The iOS app now has the neural voice (after the pack download), camera scanning with on-device text recognition, and background audio.

#### Not yet verified

- A real-device or TestFlight pass: neural voice after the pack, speed, skip, pause, interruptions, lock screen, scanning two pages, Vision OCR, and file open for txt, pdf and docx.
- The `.ipa` contains `Frameworks/SherpaOnnxC.framework`, correctly signed.
- App Review acceptance of background audio.
- Follow-ups: a shared WAV encoder; platform-neutral scanner error copy in `ReaderPage`; `SpeakChunksAsync` is over the 30-line guideline.
