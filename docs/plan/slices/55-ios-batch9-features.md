# Slice 55 — iOS gets the Batch 9 features

*Built (wave 2026-10-21b), PR #207.*

**Batch:** 9
**Decisions:** 61-68 (iOS side), 69, 70
**Depends on:** Slice 49 (iOS parity); Slices 47, 50, 51, 52, 53, 54 (the Android versions it ports); PR #165 via Slice 44
**Touches:** `src/ReadTheStupidText.Mobile/Platforms/iOS/**`, `src/ReadTheStupidText.Mobile/**` (shared pages and services), `src/ReadTheStupidText.Application/**`, `STORE.md`, `PRIVACY.md`, `CLAUDE.md`
**Story issue:** —

## Goal

The iPhone app catches up with Android: language packs and first-run language,
PaddleOCR in every supported language, a translated and right-to-left UI,
on-device translation and the sentence head start.

## Steps

- [x] **Head start (Slice 47).** Wire `IHeadStart`/the sentence cache into the
      iOS neural reader from Slice 49; triggers are the same shared `ReaderPage`
      code. Nothing new if the cache and triggers were built platform-neutral.
- [x] **Packs (Slice 50).** Implement `IVoicePackService` on iOS (files under
      `FileSystem.AppDataDirectory`, mark the folder excluded from iCloud
      backup). Remove the iOS `MauiAsset` VoiceModel condition added in Slice 49.
      Confirm no ATS exception is needed (GitHub release URLs are https). iOS
      needs no entitlement for network (Decision 60). Verify the `.ipa` shrinks.
- [x] **Voices (Slice 51).** The shared engine selector works if sherpa is linked
      on iOS (Slice 48/49); add the Piper and Melo engine configs and test each
      language on a device. AVSpeech remains the first-run voice.
- [x] **OCR (Slice 52).** Reuse `PaddleOcrImageTextExtractor`; implement the
      image-decode seam on iOS (`UIImage`/`CGImage`) and the onnxruntime native
      packaging (iOS xcframework; verify by unzipping the `.ipa`). Retire
      `VisionImageTextExtractor` as the default; keep it as the fallback while
      the OCR pack downloads, as ML Kit Latin is on Android.
- [x] **Localization and RTL (Slice 53).** The `.resx` files and culture handling
      are in the shared project; check `FlowDirection` mirroring on iOS and add
      localized `InfoPlist.strings` for `NSCameraUsageDescription`.
- [ ] **Translation (Slice 54): not shipped on iOS.** Research was done and nothing verifiable was
      found (Apple's Translation framework is Swift-only with no binding; the only ML Kit binding on NuGet
      is an unverifiable single-maintainer package; see Decisions below), so there is no `ITranslator` on
      iOS and the translate card stays hidden. Only the language detector (`NLLanguageRecognizer`) is
      done. Original step, kept for reference: **research step first.** Check for a usable .NET
      binding of the ML Kit iOS Translation/Language ID SDK (NuGet search, or a
      Swift/ObjC xcframework binding via `NativeReference`). If none: implement
      `ITranslator` on Apple's Translation framework (iOS 18+; confirm via
      context7/Apple docs), which has no Farsi; record the Farsi gap in
      `STORE.md` and plan outstanding work, hide the toggle for fa. Language ID can
      use `NLLanguageRecognizer` (Natural Language framework).
- [ ] TestFlight pass (needs a real iPhone; not done here) through every Batch 9 feature (list in Slices 47, 50-54
      Acceptance sections) on a real iPhone.
- [ ] `CLAUDE.md` Mobile bullet (left to `update-docs`; `PRIVACY.md` and `STORE.md` are done) and `PRIVACY.md` updated for iOS (packs, ML Kit
      model downloads or Apple Translation).

## Acceptance

- The Acceptance checks of Slices 47, 50, 51, 52, 53 and 54 pass on an iPhone
  via TestFlight, or each failure is recorded as a named gap.
- Android is unaffected (shared-file changes are re-tested there).

## Risks / notes

- Two onnxruntime copies (sherpa's and OCR's) on iOS: link one if possible.
- Apple Translation needs a newer iOS minimum than the app's baseline; if so
  gate the toggle by OS version.
- This slice is large; split by feature (head start, packs, OCR, translation) at
  claim time if needed, one file each.

## Decisions taken during build

- **Reader.** `IosSupertonicSpeechReader` is now a port of the whole Android reader, split into
  four partial files under `Mobile/Platforms/iOS`: the read flow, `.Synthesis` (sentence cache,
  engine acquire), `.Playback` (`AVAudioPlayer`, poll loop, interruptions) and `.HeadStart`. It
  implements `IHeadStart`, so the shared `CompositeSpeechReader` and `ReaderPage` need no change
  (Slice 47). It also takes over Slice 60's `SpeakSegmentsAsync` and per-chunk `ChunkChanged`, which the
  iOS reader had missed. The engine ref-counting and the Supertonic, Piper and Melo configs are in
  `IosTtsEngine.cs`; the structs are filled through `ref` helpers exactly as on Android. It still
  throws `NeuralEngineUnavailableException` when the model went away (the old iOS reader returned
  silently). The logic stays a copy of the Android reader, not a shared base (same reason as Slice 49).
- **Packs.** `MobileVoicePackService` was already platform-neutral and the `MauiAsset` stand-in was gone
  (Slice 49), so only the backup flag was added: `IosPackStorage` marks `AppDataDirectory/packs` as
  excluded from backup when `IOSPlatformServices` is created. Its folder name duplicates the private
  constant in `MobileVoicePackService` (that file is outside this slice's Touches). No ATS exception or
  entitlement (pack URLs are HTTPS; Decision 60).
- **OCR.** The detection image and the recognition crops come from ImageIO
  (`CGImageSource.CreateThumbnail` with the transform applied, so EXIF orientation is handled, capped at
  4096 px) and `CGBitmapContext` (`IosPixelImageLoader`, `IosOcrPage`, `CgImagePixels`). Crops are cut
  from that upright full-resolution image, sampled by the shared `CropSampling`. `IosLanguageImageTextExtractor`
  mirrors Android's: PaddleOCR from the installed pack, Apple Vision as the fallback (and for a pack that
  cannot run); `VisionImageTextExtractor` now also returns line boxes for `IOcrLineExtractor`. The
  fallback reports `OcrSource.MlKit` / `MlKitPackPending` (the enum docs now say "the platform's built-in
  recognizer"), so Slice 63 sees one meaning on both platforms.
- **Two onnxruntime copies (conservative choice).** The sherpa iOS framework links onnxruntime statically
  and does not export `OrtGetApiBase`, so OCR cannot reuse it. iOS references the full
  `Microsoft.ML.OnnxRuntime` 1.24.3 (static xcframework, same version as Android's managed pin). Expect
  roughly 30-50 MB more in the app. Checking the unzipped `.ipa` and that both copies load is a TestFlight item.
- **Shared OCR sources** (superseded: moved to Mobile.Shared/Ocr/ in the review fixes). `OnnxOcrInference.cs` and `PaddleOcrEngine.cs` are platform-neutral but sit in
  `Platforms/Android`. The Mobile csproj compiles them for iOS from there in the `AddIosOcrSources` target.
  **When Slice 63 moves them to Mobile.Shared, delete that target** or the build breaks on the missing path.
- **Localization.** `.resx` and `FlowDirection` handling are shared and untouched. Added
  `Platforms/iOS/Resources/<lang>.lproj/InfoPlist.strings` for all nine languages (camera, photo library,
  photo add and microphone prompts; `zh` is `zh-Hans.lproj`) and `CFBundleLocalizations` plus
  `CFBundleDevelopmentRegion` in `Info.plist`. The translations are mine and have not been reviewed by a
  native speaker. RTL mirroring on iOS is unverified (device item).
- **Translation: not shipped on iOS.** Research: Apple's Translation framework (iOS 18+; the programmatic
  `TranslationSession(installedSource:target:)` is newer) is Swift-only, with no Objective-C header and no
  binding in `dotnet/macios`, and its supported pairs have no Farsi; language packs download through a
  SwiftUI `translationTask`. The only .NET ML Kit iOS binding on NuGet is a single-maintainer package
  (`EngageSolutionsGroup.Apple.MLKitTranslate` 8.1.0, net9.0-ios18.0, about 50 MB, broken on the arm64
  simulator) that cannot be verified without a Mac. Both were rejected as unverifiable here. Result: no
  `ITranslator` on iOS, so `ReaderPage` keeps the translate card hidden (it needs both services). The
  `NLLanguageRecognizer`-based `NlLanguageDetector` is registered and ready. The gap is recorded in
  `STORE.md`; the Farsi hiding the spec mentions never comes into play until a translator exists.
  Follow-up options: a small Swift wrapper xcframework (iOS 18 gate) built in CI, or evaluate the ML Kit
  package on a Mac.
- **Docs.** `PRIVACY.md` and `STORE.md` updated for iOS (packs, OCR, backup flag, Apple Vision, the
  translation gap). `CLAUDE.md`, `README.md` and `plan.md` untouched.
- **Application (additive).** `ImageResizer.FitWithin`, `OcrBox.FromNormalizedBottomLeft` (pure helpers
  the iOS OCR path uses, unit-tested in `IosOcrGeometryTests`) and the `OcrSource` doc comments.

- **Review fixes.** The platform-neutral OCR code (`OnnxOcrInference`, `PaddleOcrEngine` and one
  `LanguageImageTextExtractor`) now lives in `Mobile.Shared/Ocr/` and compiles for Android and iOS, over a
  small `IBuiltInTextRecognizer` (`Covers(language)`, `ExtractTextAsync`, `ExtractLinesAsync` returning
  lines plus the frame size): ML Kit on Android, Vision on iOS. Vision covers Latin, ja, zh and ru, so
  only ar and fa are flagged `MlKitPackPending` on iOS. The iOS page decodes the detection image at the
  detector's size and the 4096 px upright image only on the first crop.
- **Follow-up (not done): one shared TTS engine.** `IosTtsEngine` and the Android engine wrapper do
  nearly the same sherpa-onnx work (config, generate, voice tables) behind different native bindings;
  fold them into a shared engine in `Mobile.Shared` once both bindings can be verified side by side.

## Key shape

- `Mobile.Shared/Ocr/` holds `LanguageImageTextExtractor`, `IBuiltInTextRecognizer`, `OnnxOcrInference`
  and `PaddleOcrEngine`, shared by Android (ML Kit) and iOS (Vision; covers ja/zh/ru on iOS 16+).
- `NlLanguageDetector` returns primary subtags.
- Pack backup exclusion runs in `AppDelegate.FinishedLaunching`.
- Translation is NOT shipped on iOS.

## Review notes

- High pass: 10 findings. 9 fixed; the shared TTS engine is a follow-up.
- Medium pass: 8 findings. 7 fixed; the two onnxruntime copies on iOS are a known decision that needs a
  device check.

## Verification

The iOS compile, both Android builds, and 463 + 229 tests pass. Nothing has run on a device.

## Not verified (needs TestFlight / a device)

- A full TestFlight/device pass.
- iOS only compiles here. Nothing below has run on a device or simulator.
- Neural read with a Supertonic, a Piper (Farsi) and a Melo (Chinese) pack, including espeak data loading
  and the structs-by-ref configs on sherpa 1.13.8; the head start (second Play of the same text starts at
  once); skip, voice swap mid-read and an interruption (call) with the new reader.
- Pack download, the backup exclusion (check the packs folder is flagged) and a smaller `.ipa`.
- PaddleOCR: the app links two onnxruntime copies without a symbol clash and launches; real scans in ar,
  fa, ru, zh, ja, fr (orientation, reading order, RTL, memory with the 84 MB CJK pack); the Vision fallback
  while a pack downloads.
- All nine languages: UI strings, RTL mirroring (Shell, pickers, alerts), localized permission prompts
  (camera, photo library) in each language.
- App Store validation of the privacy manifest now that onnxruntime is linked into the executable (it may
  need extra required-reason API entries in `PrivacyInfo.xcprivacy`).
- `NLLanguageRecognizer` output (`zh-Hans` and similar tags are normalised by `TranslationFlow`).

#### For CLAUDE.md (lifted 2026-10-21)

- The OCR pipeline shared by Android and iOS lives in `Mobile.Shared/Ocr/` behind `IBuiltInTextRecognizer`
  (ML Kit on Android, Vision on iOS). (Slice 55: docs/plan/slices/55-ios-batch9-features.md)
- iOS ships its own onnxruntime (`Microsoft.ML.OnnxRuntime`), because sherpa's xcframework links it
  statically and does not export `OrtGetApiBase`. (Slice 55: docs/plan/slices/55-ios-batch9-features.md)
- Translation isn't available on iOS (no .NET binding for Apple's Translation framework), so the
  translate card stays hidden there. (Slice 55: docs/plan/slices/55-ios-batch9-features.md)

#### For README.md (lifted 2026-10-21)

iOS now has the natural voices in nine languages, the head start, on-device text recognition per
language, and localised permission prompts. Translation is Android-only for now.
