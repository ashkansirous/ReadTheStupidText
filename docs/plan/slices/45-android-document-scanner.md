# Slice 45 — Android: document scanner straight into the editor

*Built (wave 2026-10-20d), PR #189. Not yet verified on a Google-signed-in device.*

**Batch:** 9
**Decisions:** 64 (also 40, 43 stand until replaced)
**Depends on:** none (Android-only; touches no pipeline file)
**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/AppShell.xaml`, `src/ReadTheStupidText.Mobile/AppShell.xaml.cs`, `src/ReadTheStupidText.Mobile/ScanPage.xaml` (deleted), `src/ReadTheStupidText.Mobile/ScanPage.xaml.cs` (deleted), `src/ReadTheStupidText.Mobile/PendingScanResult.cs` (deleted), `src/ReadTheStupidText.Mobile/IDocumentScanner.cs`, `src/ReadTheStupidText.Mobile/DocumentScannerPreparingException.cs`, `src/ReadTheStupidText.Mobile/DocumentScannerStartFailedException.cs`, `src/ReadTheStupidText.Mobile/DocumentScannerUnavailableException.cs`, `src/ReadTheStupidText.Mobile/ScannedImageFiles.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/MainActivity.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/MlKitImageTextExtractor.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/AndroidDocumentScanner.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/MauiProgram.Android.cs`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `src/ReadTheStupidText.Application/Images/ScannedText.cs`, `tests/ReadTheStupidText.Tests/ScannedTextTests.cs`
**Story issue:** —

## Goal

Tapping **Scan** opens the system document scanner at once: it finds the page
edges, crops, takes several pages, reorders them and can import from the
gallery. Every page is read in order and the text lands in the editor, ready to
play. No intermediate result screen, no camera permission.

## Steps

- [x] Look up the ML Kit Document Scanner .NET binding (NuGet id and version
      confirmed via context7/NuGet at implementation) and add it to
      `ReadTheStupidText.Mobile.csproj` next to `Xamarin.Google.MLKit.TextRecognition`
      (line 105). Check the built APK for the new native/asset files (CLAUDE.md
      rule: unzip `obj/Debug/net10.0-android/android/bin/*.apk`, don't trust a
      clean build).
- [x] Add `Platforms/Android/DocumentScanner.cs`: configure the scanner options
      (page limit ~20, gallery import on, JPEG result, full mode), launch its
      start-scan intent from the current activity and receive the result in
      `Platforms/Android/MainActivity.cs` through an activity-result callback
      bridged to a `TaskCompletionSource`. Expose it behind a small interface in
      `Mobile/` (e.g. `IDocumentScanner`, `Task<IReadOnlyList<string>> ScanAsync()`
      returning page image paths), registered in `MauiProgram.cs`.
- [x] `ReaderPage.xaml.cs`: replace `OnScanTapped` (lines 82-83, currently
      `Shell.Current.GoToAsync(AppShell.ScanRoute)`) with: call the scanner, then
      OCR each returned page **in order** with `IImageTextExtractor`
      (`MlKitImageTextExtractor`, `Platforms/Android/MlKitImageTextExtractor.cs`),
      append to `TextEditor.Text` (blank line between pages, existing text kept),
      with `BusyIndicator` shown while OCR runs (as `OnFileTapped`, lines 107-125).
      User cancel = editor unchanged.
- [x] Delete `ScanPage.xaml`, `ScanPage.xaml.cs`, `PendingScanResult.cs` and
      every reference: `MauiProgram.cs` lines 32 (`PendingScanResult`) and 35
      (`ScanPage`); `AppShell.xaml.cs` lines 6 and 12 (`ScanRoute`);
      `AppShell.xaml` comment (lines 10-13); `ReaderPage.xaml.cs` ctor parameter
      and field (lines 29, 33, 40), the `OnAppearing` block (lines 57-62) and
      the class summary (lines 9-18). Grep `Scan` once more for stragglers.
- [x] **No Shell query hand-off:** the scan result goes straight into the editor
      from the page that launched the scanner. Do not reintroduce
      `[QueryProperty]` / `GoToAsync(dict)` (the replay bug that created
      `PendingScanResult`; see the MAUI Shell QueryProperty-replay memory note).
- [x] Fallback when Google Play services or the scanner module is missing:
      catch the availability failure, tell the user why, and use the old
      single-photo path (`MediaPicker.Default.CapturePhotoAsync`, as in the
      deleted `ScanPage.OnCaptureTapped`). Keep `CAMERA` and the `IMAGE_CAPTURE`
      `<queries>` block in `AndroidManifest.xml` for this fallback only.
- [ ] `CLAUDE.md` Mobile bullet (lifted by the index pass after merge): note the scanner needs Play services and the
      fallback. (`MlKitImageTextExtractor`'s "no Play-services" remark is about
      the OCR model and stays true.)

## Acceptance

- On a Play-services device: Scan -> scanner opens immediately -> scan 3 pages
  (reorder one) -> editor shows all three pages' text in order -> Play reads it.
- Gallery import works; cancelling leaves the editor unchanged.
- Without Play services: Scan shows the message and the single-photo path still
  fills the editor.
- No `ScanPage` / `PendingScanResult` left in the build; opening the voice
  picker after a scan never replays old text into the editor.

## Risks / notes

- The scanner module downloads through Play services on first use; that is
  Google's on-device download, not ours (Decision 60 unaffected). Mention it in
  `PRIVACY.md` when Slice 50 edits it.
- OCR of many pages is slow: run pages sequentially and show progress.
- Latin-only OCR until Slice 52 lands.
- Batch 10 (Slice 63) needs the scanned page images kept - don't discard them after OCR when Dino Reader asks to keep them.

## Built

*Built 2026-10-20 (wave 2026-10-20d), PR #189. Verified: Android build, iOS compile (simulator dll produced), 190 tests; the APK's `lib/` has no new or wrong-ABI native libs. Emulator (Play services present, no Google account): tapping Scan launched Google's `DocumentScanningActivity` and the module download; the download failed because the emulator isn't signed in to Google ("Something went wrong"), and cancelling returned to the Reader with the editor unchanged, buttons re-enabled and no crash.*

ML Kit Document Scanner (`Xamarin.GooglePlayServices.MLKit.DocumentScanner` 116.0.0.4; the module is delivered by Google Play services, so no native libs in the APK; gallery import on, up to 20 pages, JPEG, full mode) sits behind `IDocumentScanner`, registered only in `Platforms/Android/MauiProgram.Android.cs`. `ReaderPage` takes it optionally, so iOS keeps Scan hidden. The result arrives through `MainActivity.OnActivityResult` (base called first) into `AndroidDocumentScanner`, which holds one atomically swapped pending request: a new Scan cancels a stale one, and request codes cycle 45000+256. The module is pre-warmed through `ModuleInstall` when `ReaderPage` appears.

Failures are typed. `Preparing`: "Scanner getting ready ... try again in a moment", no fallback. `StartFailed` (null activity): "Couldn't start the scanner - try again." `Unavailable` (no Play services): "Scanner unavailable", then a single `MediaPicker` photo fallback; a `PermissionException` shows "Grant camera access in Settings...".

Pages are OCR'd in order with one reused ML Kit recognizer, "Reading page i of N" shows in the header status, and successful pages are appended below the existing text (pure `ScannedText.AppendPages` in Application, 10 unit tests). Failed pages are reported ("Couldn't read page(s) 3, 9 - the rest was added."), page images are deleted after the pass, and the input buttons are disabled while scanning or OCR runs. `ScanPage` and `PendingScanResult` are removed (no Shell hand-off). csproj: scanner package, `Xamarin.GooglePlayServices.Tasks` >= 118.4.1.3, `AndroidX.Fragment.Ktx` pinned 1.9.0 (D8 duplicate-class fix).

*Changed while building:* the plan named `Platforms/Android/DocumentScanner.cs`; the class is `AndroidDocumentScanner.cs`, with typed exception files, `ScannedImageFiles.cs` and `Application/Images/ScannedText.cs` (plus `ScannedTextTests.cs`) added. Scan appends to the editor while Paste/File replace it (kept as specified). The `CLAUDE.md` step is left to the index pass.

*From review (high):* crash paths from `async void` (double tap, launch exceptions); first-use module download misreported as "needs Play services"; re-entrancy; camera permission message; one recognizer per page; leftover images; Uri flattening; progress display.

*From review (medium):* partial-failure scans discarded everything (fixed, good pages are kept); the pause/resume abandonment heuristic could drop real results (replaced by stale-request cancel on the next tap); wrapped exceptions; transient null activity; `MainActivity` BOM/CRLF churn; pure logic moved to Application with tests.

#### For CLAUDE.md

- Scan (Slice 45, Decision 64) opens the ML Kit Document Scanner (Play-services package, Android only, no camera permission) via `IDocumentScanner` (registered only in `Platforms/Android`). Results come through `MainActivity.OnActivityResult`; `ReaderPage` OCRs pages in order (one reused recognizer), appends them via `ScannedText.AppendPages` (Application), deletes the page images, and reports failed pages. First use triggers a Play-services module download (pre-warmed on Reader open). Without Play services: a single `MediaPicker` photo fallback, so `CAMERA` + `IMAGE_CAPTURE` `<queries>` stay in the manifest for that only. No `ScanPage`, no Shell hand-off. Gotchas: the scanner pulls `AndroidX.Fragment.Ktx` 1.8.8.1, which collides with Fragment 1.9.0 in D8, so the csproj pins Fragment.Ktx 1.9.0; `Xamarin.GooglePlayServices.Tasks` must stay >= 118.4.1.3. Testing a real scan needs a device/emulator signed in to Google. (Slice 45: docs/plan/slices/45-android-document-scanner.md)

#### For README.md

- Optional, one line: "Android: Scan opens Google's document scanner (multi-page); needs Google Play services."

#### Not yet verified

- A real multi-page scan on a Google-signed-in device: module download, edge detection, page order, gallery import, text appended and read aloud.
- The no-Play-services fallback on a device without Play services.
