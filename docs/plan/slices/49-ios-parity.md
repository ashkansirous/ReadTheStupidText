# Slice 49 — iOS reaches today's Android parity

*Planned.*

**Batch:** 9
**Decisions:** 64, 65 (Apple Vision stand-in), 37-42
**Depends on:** Slice 48 (iOS target builds and reaches TestFlight)
**Touches:** `src/ReadTheStupidText.Mobile/Platforms/iOS/**`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `CLAUDE.md`
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

- [ ] **Neural voice.** Port `AndroidSupertonicSpeechReader.cs` (596 lines) to
      iOS. The synthesis, chunking, ordered playback, voice swap and skip logic
      is pure C# over `SpeechTextChunker`/`ReadTimingTracker`; only the playback
      primitive (`Android.Media.MediaPlayer`, files written by `WriteWavFileAsync`,
      line 510) changes to an AVFoundation player with a position poll. Prefer
      extracting the shared logic into a platform-neutral base class under
      `Mobile/` (not `Platforms/`) so both readers share it; keep Android's
      behaviour unchanged (Slice 44 rule: shared files rebuild both platforms).
- [ ] **Voice model on iOS.** Until Slice 50 removes bundling, the model stand-in
      is `MauiAsset` (csproj lines 58-67); `MobileVoiceModelService` extracts it
      to `AppDataDirectory` via `FileSystem.OpenAppPackageFileAsync`, which works
      on iOS unchanged. Condition the asset on iOS too. Check the `.ipa` size.
- [ ] **Routing.** The shared `CompositeSpeechReader` (Slice 44 made it take
      `ISpeechReader`s) picks neural when `IVoiceModelService.IsReady`, else
      `AvSpeechReader`. Register both in `Platforms/iOS/`.
- [ ] **File upload.** `ReaderPage.xaml.cs` lines 21-24 define
      `SupportedFileTypes` with only a `DevicePlatform.Android` entry; add
      `DevicePlatform.iOS` with the UTType strings for plain text, PDF and docx.
      The extractors are the shared `Documents` project, no change.
- [ ] **Scanner + OCR.** `VisionKitDocumentScanner` presents
      `VNDocumentCameraViewController` and returns page images; implement
      `IDocumentScanner` (Slice 45) and `IImageTextExtractor` via Apple Vision
      (`VNRecognizeTextRequest`, accurate mode). This is a stand-in: Slice 55
      replaces it with PaddleOCR. Add the camera usage string
      (`NSCameraUsageDescription`) to `Info.plist`.
- [ ] **Settings and picker.** `MobilePreferencesSettingsStore` uses MAUI
      `Preferences` and `VoicePickerPage` uses `SupertonicVoiceTable`; both work
      as-is. Verify on device.
- [ ] **Audio session.** Configure `AVAudioSession` (playback category) so audio
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
