# Slice 55 — iOS gets the Batch 9 features

*In progress (wave 2026-10-21b), draft PR.*

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

- [ ] **Head start (Slice 47).** Wire `IHeadStart`/the sentence cache into the
      iOS neural reader from Slice 49; triggers are the same shared `ReaderPage`
      code. Nothing new if the cache and triggers were built platform-neutral.
- [ ] **Packs (Slice 50).** Implement `IVoicePackService` on iOS (files under
      `FileSystem.AppDataDirectory`, mark the folder excluded from iCloud
      backup). Remove the iOS `MauiAsset` VoiceModel condition added in Slice 49.
      Confirm no ATS exception is needed (GitHub release URLs are https). iOS
      needs no entitlement for network (Decision 60). Verify the `.ipa` shrinks.
- [ ] **Voices (Slice 51).** The shared engine selector works if sherpa is linked
      on iOS (Slice 48/49); add the Piper and Melo engine configs and test each
      language on a device. AVSpeech remains the first-run voice.
- [ ] **OCR (Slice 52).** Reuse `PaddleOcrImageTextExtractor`; implement the
      image-decode seam on iOS (`UIImage`/`CGImage`) and the onnxruntime native
      packaging (iOS xcframework; verify by unzipping the `.ipa`). Retire
      `VisionImageTextExtractor` as the default; keep it as the fallback while
      the OCR pack downloads, as ML Kit Latin is on Android.
- [ ] **Localization and RTL (Slice 53).** The `.resx` files and culture handling
      are in the shared project; check `FlowDirection` mirroring on iOS and add
      localized `InfoPlist.strings` for `NSCameraUsageDescription`.
- [ ] **Translation (Slice 54), research step first.** Check for a usable .NET
      binding of the ML Kit iOS Translation/Language ID SDK (NuGet search, or a
      Swift/ObjC xcframework binding via `NativeReference`). If none: implement
      `ITranslator` on Apple's Translation framework (iOS 18+; confirm via
      context7/Apple docs), which has no Farsi; record the Farsi gap in
      `STORE.md` and plan outstanding work, hide the toggle for fa. Language ID can
      use `NLLanguageRecognizer` (Natural Language framework).
- [ ] TestFlight pass through every Batch 9 feature (list in Slices 47, 50-54
      Acceptance sections) on a real iPhone.
- [ ] `CLAUDE.md` Mobile bullet and `PRIVACY.md` updated for iOS (packs, ML Kit
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
