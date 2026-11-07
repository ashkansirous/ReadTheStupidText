# Slice 305 — Dino package cleanup: ML Kit, sherpa-onnx and onnxruntime out of the Dino packages

*Planned 2026-11-07.* From [Slice 278](278-dino-store-books-only.md) A4 and B4. ML Kit (Translate, TextRecognition, DocumentScanner; iOS MLKitTranslate) still reach the Dino Android and iOS packages through Mobile.Shared/Documents, though Dino no longer calls them. The sherpa-onnx/onnxruntime natives and the shared speech registrations also still reach Dino through Mobile.Shared; Dino's csproj keeps its three sherpa/onnxruntime imports (`AndroidSherpaPackaging.props`, `IosSherpaPackaging.props`, the WinML target) to neutralise them. Moved from plan.md's *Outstanding work*. Android and iOS in the same slice (repo rule): the Dino Windows head is part of it too because of the WinML target.

**Touches:** `src/DinoReader.App/DinoReader.App.csproj`, `src/ReadTheStupidText.Mobile.Shared/ReadTheStupidText.Mobile.Shared.csproj`, `src/ReadTheStupidText.Mobile.Shared/MobileSharedServices.cs`, `src/ReadTheStupidText.Mobile.Shared/DocumentScannerRegistration.cs`, `src/AndroidSherpaPackaging.props`, `src/ReadTheStupidText.Mobile.Shared/IosSherpaPackaging.props`, `src/SherpaOnnxVersion.props`, `src/DinoReader.App/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `.github/scripts/check-sherpa-pins.mjs`, `.github/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/305-dino-package-cleanup-ml-kit-and-sherpa.md`

## Work

- [ ] 1. Read how the packages get ML Kit and the sherpa natives (project and package references, the packaging props), and write the map in the slice file (Dino csproj lines, Mobile.Shared lines).
- [ ] 2. Move ML Kit, sherpa-onnx, onnxruntime and the speech registrations behind per-app switches (an `IncludeSpeechEngines` / `IncludeMlKit` property on Mobile.Shared that Dino sets false, RTST true), so Dino's packages no longer contain them.
- [ ] 3. Drop Dino's three neutralising imports once nothing needs neutralising. `check-sherpa-pins.mjs` must still pass (versions stay in `SherpaOnnxVersion.props` only).
- [ ] 4. Android, iOS and the Windows head: build and publish all three Dino packages; compare package sizes before and after (APK, IPA, MSIX) and list the native libraries in each; RTST's three packages must be unchanged in content.
- [ ] 5. Dino still runs: emulator flows (shelf, read a store book, record) and an iOS TestFlight look; the Windows VM check for the Windows head only if its project files changed.

## Verification

- No `libsherpa-onnx*`, `libonnxruntime*` or ML Kit module in the Dino Android package and iOS bundle (listed from the built artifact); RTST's lists unchanged; Dino's sizes drop; fast tests and device flows green.
