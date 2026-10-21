# Slice 67 — Dino Reader on iOS and Windows

*In progress (wave 2026-10-21), draft PR.*

**Batch:** 10
**Decisions:** 72, 81
**Depends on:** Slices 48/49 (iOS target, parity) for iOS; Slices 58-62 for the app; Windows last (after 56, 57)
**Touches:** `src/DinoReader.App/**`, `ReadTheStupidText.slnx`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/ReadTheStupidText.Infrastructure/**`, `.github/workflows/**` (the Dino iOS and Windows jobs, ship-targets path groups), `.github/scripts/**`, `.github/CLAUDE.md`, `STORE.md`, `CLAUDE.md`
**Story issue:** —

## Goal

Dino Reader ships on iOS (TestFlight) and Windows (Store, a separate product from Read The
Stupid Text) from the same project and the same version.

## Steps

- [x] **iOS:** add `net10.0-ios` to `DinoReader.App` and finish `Mobile.Shared`'s
      `Platforms/iOS` (Slices 48/49): VisionKit scanner, PDFKit page renderer
      (`IPdfPageRenderer`, Slice 63), OCR with boxes, voice engine. A **second App ID
      `uk.sirous.dinoreader`** in the same Apple team plus its TestFlight app record (manual,
      user; guide step by step); its iOS pipeline job and secrets via `manage-secrets`.
      App icon: `docs/design/dino-reader/icons/ios/` (iOS 18 light/dark/tinted 1024s +
      `Contents.json`) goes in `Platforms/iOS/Resources/Assets.xcassets/AppIcon.appiconset/`;
      `legacy/` sizes only if the deployment target is below iOS 18.
- [x] **Windows:** MAUI WinUI head in `DinoReader.App`, packaged as MSIX; segment reading
      from Slice 60 on the Windows reader; a `Windows.Data.Pdf` renderer for `IPdfPageRenderer`;
      no tray, no hotkey. A **separate Store listing** (name, id, identity): manual Partner
      Center reservation by the user, then the identity goes in its manifest; `STORE.md` gets a
      Dino Reader section. Visual assets: `docs/design/dino-reader/icons/windows/`
      (Square44/71/150/310, Wide310x150, StoreLogo, SplashScreen at all scales) wired
      into the package manifest. The `tray/` icons in that folder are unused: Dino
      Reader has no tray (Out of Scope, Batch 10).
- [x] Pipeline (iOS part): `dinoios` target + `dinoreader-ios.yml` + `testflight-submit-dino`.
- [x] Pipeline: the `dinoreader` path group builds the iOS and Windows jobs only when Dino
      Reader or shared paths change; the Windows MSIX is self-contained like RTST's (Store cert
      10.2.4.1); one shared `v<x.y.z>` tag.
- [x] `CLAUDE.md`: platform notes for the Dino Reader heads.

## Acceptance

- iOS: the TestFlight build of Dino Reader installs; typed and scanned stories read in four
  voices; picture-mode boxes follow.
- Windows: the MSIX installs and runs the same flows; Store submission accepted.
- A change under `src/DinoReader.App/**` rebuilds Dino Reader only, on all platforms.

## Risks / notes

- Windows last: it needs Slices 56/57 and the segment read on the Windows reader (Slice 60).
- A MAUI WinUI head next to the existing WinUI RTST App may clash in the `.slnx` `Platform`
  mapping; verify the build.

## Built notes

*Changed while building:*

- **iOS:** `net10.0-ios` on `DinoReader.App` (only on macOS or with `-p:IncludeIos=true`; floor iOS 15). The Book-hug asset catalog is copied from `docs/design/dino-reader/icons/ios/`. `Mobile.Shared/Platforms/iOS` is now the single copy of the sherpa engine, VisionKit scanner, Vision OCR with boxes, PDFKit renderer and image keeper; RTST Mobile's duplicates were removed. `SherpaIosVersion.props` drives every iOS sherpa version.
- **iOS CI:** `dinoreader-ios.yml`, plus `release.yml` jobs `build-dinoios` and `testflight-submit-dino` (bundle `uk.sirous.dinoreader`, team `J4TBWA5379`, ASC app `6825053435`). It reuses the team ASC and cert secrets plus `DINOREADER_IOS_PROVISIONING_PROFILE_BASE64` (set 2026-10-21), and skips cleanly if any secret is missing. IPA carry-forward patterns are per app.
- **Windows:** a MAUI WinUI head (`net10.0-windows10.0.19041.0`, MSIX, self-contained, unsigned) with icons from `docs/design/dino-reader/icons/windows/`. `Mobile.Shared/Platforms/Windows` sits over Infrastructure: `WindowsNeuralReader` via `SupertonicSpeechReader` segment reads (a voice per character on the ChunkFeed model, merged with Slice 75 streaming), `Windows.Media.Ocr` with boxes (`OcrSource.PlatformFallback` when there is no OS language), a `Windows.Data.Pdf` renderer and an image keeper, both sharing `WindowsImageScale`. The Scan tile is dimmed and inert. `ScreenReader` detects Narrator via SPI or the Narrator process.
- **Windows CI:** `dinoreader-windows.yml`, plus `release.yml` `build-dinowin` (the `test` job now also runs for dinowin) and `store-submit-dino`, skipped while the repo variable `DINOREADER_STORE_APP_ID` is empty. `store-submit.yml` takes `package`/`product_id` inputs.
- **Package identity is a placeholder:** Name `AshkanSirous.DinoReader`, RTST's Publisher, a generated PhoneProductId, with a TODO comment in the manifest.
- **Fixes along the way:** Dino's Android `MainActivity` now forwards `OnActivityResult` to the document scanner (it used to hang after cancel); stream text appended before the feed exists is buffered (a Slice 75 bug); `OnnxRuntime.Managed` is pinned per TFM (1.24.4 Windows, 1.24.3 Android/iOS); the language picker re-enables if a restart fails.

*From review:* two passes plus a focused merge review (9 + 7 + 3 findings, all fixed). One low finding accepted: a short gap at a character/narrator boundary after a mid-read picker change.

*Checks:* Dino Windows publish, Android, iOS compile on Windows, RTST App, `.slnx`; tests 655 RTST / 353 Dino, node 83.

*Desktop acceptance:* BLOCKED on 2026-10-21 because the Windows session was locked. Only launch was verified (My stories and the grown-up gate visible).

#### For CLAUDE.md

Into `src/DinoReader.App/CLAUDE.md`, and one line for the root:

- Dino Reader builds for Android, iOS (`-p:IncludeIos=true` on Windows, compile only) and Windows (MAUI WinUI, MSIX via `dotnet publish -f net10.0-windows10.0.19041.0 -p:IncludeAndroid=false -p:Platform=x64`). On a machine with only maui-android, pass `-p:IncludeWindows=false`. Platform code lives once in `Mobile.Shared/Platforms/<OS>`; RTST and Dino share it. The Windows package identity is a placeholder until the Partner Center values are filled in (`STORE.md`); `store-submit-dino` runs only when `DINOREADER_STORE_APP_ID` is set. (Slice 67: docs/plan/slices/67-dino-reader-ios-windows.md)

#### Not yet verified

- The TestFlight build installs and reads in four voices, with picture-mode boxes (CI/macOS plus a device).
- The Windows desktop flows: typed story, voices, PDF picture mode, Windows OCR.
- The real Partner Center identity (Store ID, Identity Name, Publisher) set in the manifest, plus the `DINOREADER_STORE_APP_ID` variable, then a Store submission.
