# Slice 45 — Android: document scanner straight into the editor

*Planned.*

**Batch:** 9
**Decisions:** 64 (also 40, 43 stand until replaced)
**Depends on:** none (Android-only; touches no pipeline file)
**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/AppShell.xaml`, `src/ReadTheStupidText.Mobile/AppShell.xaml.cs`, `src/ReadTheStupidText.Mobile/ScanPage.*`, `src/ReadTheStupidText.Mobile/PendingScanResult.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/**`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `CLAUDE.md`
**Story issue:** —

## Goal

Tapping **Scan** opens the system document scanner at once: it finds the page
edges, crops, takes several pages, reorders them and can import from the
gallery. Every page is read in order and the text lands in the editor, ready to
play. No intermediate result screen, no camera permission.

## Steps

- [ ] Look up the ML Kit Document Scanner .NET binding (NuGet id and version
      confirmed via context7/NuGet at implementation) and add it to
      `ReadTheStupidText.Mobile.csproj` next to `Xamarin.Google.MLKit.TextRecognition`
      (line 105). Check the built APK for the new native/asset files (CLAUDE.md
      rule: unzip `obj/Debug/net10.0-android/android/bin/*.apk`, don't trust a
      clean build).
- [ ] Add `Platforms/Android/DocumentScanner.cs`: configure the scanner options
      (page limit ~20, gallery import on, JPEG result, full mode), launch its
      start-scan intent from the current activity and receive the result in
      `Platforms/Android/MainActivity.cs` through an activity-result callback
      bridged to a `TaskCompletionSource`. Expose it behind a small interface in
      `Mobile/` (e.g. `IDocumentScanner`, `Task<IReadOnlyList<string>> ScanAsync()`
      returning page image paths), registered in `MauiProgram.cs`.
- [ ] `ReaderPage.xaml.cs`: replace `OnScanTapped` (lines 82-83, currently
      `Shell.Current.GoToAsync(AppShell.ScanRoute)`) with: call the scanner, then
      OCR each returned page **in order** with `IImageTextExtractor`
      (`MlKitImageTextExtractor`, `Platforms/Android/MlKitImageTextExtractor.cs`),
      append to `TextEditor.Text` (blank line between pages, existing text kept),
      with `BusyIndicator` shown while OCR runs (as `OnFileTapped`, lines 107-125).
      User cancel = editor unchanged.
- [ ] Delete `ScanPage.xaml`, `ScanPage.xaml.cs`, `PendingScanResult.cs` and
      every reference: `MauiProgram.cs` lines 32 (`PendingScanResult`) and 35
      (`ScanPage`); `AppShell.xaml.cs` lines 6 and 12 (`ScanRoute`);
      `AppShell.xaml` comment (lines 10-13); `ReaderPage.xaml.cs` ctor parameter
      and field (lines 29, 33, 40), the `OnAppearing` block (lines 57-62) and
      the class summary (lines 9-18). Grep `Scan` once more for stragglers.
- [ ] **No Shell query hand-off:** the scan result goes straight into the editor
      from the page that launched the scanner. Do not reintroduce
      `[QueryProperty]` / `GoToAsync(dict)` (the replay bug that created
      `PendingScanResult`; see the MAUI Shell QueryProperty-replay memory note).
- [ ] Fallback when Google Play services or the scanner module is missing:
      catch the availability failure, tell the user why, and use the old
      single-photo path (`MediaPicker.Default.CapturePhotoAsync`, as in the
      deleted `ScanPage.OnCaptureTapped`). Keep `CAMERA` and the `IMAGE_CAPTURE`
      `<queries>` block in `AndroidManifest.xml` for this fallback only.
- [ ] `CLAUDE.md` Mobile bullet: note the scanner needs Play services and the
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
