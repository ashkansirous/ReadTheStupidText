# Slice 67 — Dino Reader on iOS and Windows

*Planned.*

**Batch:** 10
**Decisions:** 72, 81
**Depends on:** Slices 48/49 (iOS target, parity) for iOS; Slices 58-62 for the app; Windows last (after 56, 57)
**Touches:** `src/DinoReader.App/**`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/ReadTheStupidText.Infrastructure/**`, `.github/workflows/build.yml`, `.github/scripts/**`, `STORE.md`, `CLAUDE.md`
**Story issue:** —

## Goal

Dino Reader ships on iOS (TestFlight) and Windows (Store, a separate product from Read The
Stupid Text) from the same project and the same version.

## Steps

- [ ] **iOS:** add `net10.0-ios` to `DinoReader.App` and finish `Mobile.Shared`'s
      `Platforms/iOS` (Slices 48/49): VisionKit scanner, PDFKit page renderer
      (`IPdfPageRenderer`, Slice 63), OCR with boxes, voice engine. A **second App ID
      `uk.sirous.dinoreader`** in the same Apple team plus its TestFlight app record (manual,
      user; guide step by step); its iOS pipeline job and secrets via `manage-secrets`.
      App icon: `docs/design/dino-reader/icons/ios/` (iOS 18 light/dark/tinted 1024s +
      `Contents.json`) goes in `Platforms/iOS/Resources/Assets.xcassets/AppIcon.appiconset/`;
      `legacy/` sizes only if the deployment target is below iOS 18.
- [ ] **Windows:** MAUI WinUI head in `DinoReader.App`, packaged as MSIX; segment reading
      from Slice 60 on the Windows reader; a `Windows.Data.Pdf` renderer for `IPdfPageRenderer`;
      no tray, no hotkey. A **separate Store listing** (name, id, identity): manual Partner
      Center reservation by the user, then the identity goes in its manifest; `STORE.md` gets a
      Dino Reader section. Visual assets: `docs/design/dino-reader/icons/windows/`
      (Square44/71/150/310, Wide310x150, StoreLogo, SplashScreen at all scales) wired
      into the package manifest. The `tray/` icons in that folder are unused: Dino
      Reader has no tray (Out of Scope, Batch 10).
- [ ] Pipeline: the `dinoreader` path group builds the iOS and Windows jobs only when Dino
      Reader or shared paths change; the Windows MSIX is self-contained like RTST's (Store cert
      10.2.4.1); one shared `v<x.y.z>` tag.
- [ ] `CLAUDE.md`: platform notes for the Dino Reader heads.

## Acceptance

- iOS: the TestFlight build of Dino Reader installs; typed and scanned stories read in four
  voices; picture-mode boxes follow.
- Windows: the MSIX installs and runs the same flows; Store submission accepted.
- A change under `src/DinoReader.App/**` rebuilds Dino Reader only, on all platforms.

## Risks / notes

- Windows last: it needs Slices 56/57 and the segment read on the Windows reader (Slice 60).
- A MAUI WinUI head next to the existing WinUI RTST App may clash in the `.slnx` `Platform`
  mapping; verify the build.
