# Slice 263 — Dino: the page reader (text recognition) downloads on the first capture

*Planned 2026-11-04. Parked (night 2026-11-04-1441 wave 1, not claimed): on-demand reader mechanisms unproven, needs probe builds.* Dino Reader; Android and iOS alike. Read The Stupid Text shares the scanner code in Mobile.Shared and must keep working exactly as today.

**Re-scoped 2026-11-04 ~21:00 (main session; the user asked "why the fuck 263 is parked?"):** the original goal (leave the built-in Latin reader out) was dropped by the user ("I can live with 60MB instead of 55"), and both unproven mechanisms belonged to that goal. What is left is a broken feature: a captured Farsi, Arabic, Russian, Japanese or Chinese page gives "No text found" on Android, and Farsi or Arabic on iPhone (see *Measured and found* below). Fixing it is the obvious product choice, so this slice is now **downloadable page readers for those alphabets in Dino**, built on the path Read The Stupid Text already ships and uses: the PaddleOCR `ocr-*` packs (`docs/voice-packs.md`), `OcrPackInstaller` (`src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`), `ScanPackFlow`, and the shared `LanguageImageTextExtractor` fallback that already reads installed packs (`Mobile.Shared/Ocr/LanguageImageTextExtractor.cs:50-83`). No per-app build switch, no ML Kit on-demand variant. Status: planned.

**Work (re-scoped; replaces the list under *Work* below):**
- [ ] Share the pack installer: move what Dino needs of `OcrPackInstaller`/`ScanPackFlow` into Mobile.Shared (or reference it), Read The Stupid Text unchanged (its scanner tests and flows still pass).
- [ ] Dino: on a capture, when the page's language (the book's language in the wizard, else the family's languages, Slice 267's rule) needs an alphabet the built-in reader does not cover, download that `ocr-*` pack first with "Downloading the AI reader for <language>…" (progress, Cancel, offline message with Try again; design Batch 47 frames 170-171), then read the page. Android: Farsi/Arabic (`ocr-arabic`), Russian (`ocr-cyrillic`), Japanese/Chinese (`ocr-cjk`, 84 MB: say the size). iOS: Apple Vision covers Latin, ja, zh, ru on iOS 16+; download only for Farsi and Arabic, same packs and messages.
- [ ] A page still unread after that says so ("No text found on this page" stays only for a really empty page; a language with no reader says which language is not supported yet).
- [ ] Tests: unit tests for "which pack does this capture need"; emulator flow capturing a Farsi test page with the pack downloaded through the real installer (no hook), text appears. iOS compile in CI.

**Touches:** `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile/ScanPackFlow*.cs`, `src/ReadTheStupidText.Mobile/CapturePage.xaml.cs`, `src/ReadTheStupidText.Mobile.Shared/OcrPack*.cs` (new, shared installer), `src/DinoReader.App/MauiProgram.cs`, `src/ReadTheStupidText.Mobile.Shared/ReadTheStupidText.Mobile.Shared.csproj`, `src/ReadTheStupidText.Mobile.Shared/Ocr/**`, `src/ReadTheStupidText.Mobile.Shared/Platforms/**`, `src/DinoReader.App/ScanPages.cs`, `src/DinoReader.App/StoryWizardPage.xaml.cs`, `src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/capture-reader-download.yaml` (new), `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`, `docs/plan/slices/263-dino-text-reader-on-first-capture.md`.

## Decisions (user, 2026-11-04, RefineScope)

- "Voice to text" in the user's request means the reader that turns a photo of a page into words when a grown-up captures their own book (there is no speech recognition in the app).
- The app ships with **no page reader built in**. The first time someone captures a book, the app shows "Downloading the AI reader for <language>…" with progress and Cancel, then carries on. The same message is used for every language and alphabet.

**User, 2026-11-04 (after the measurement below): "I can live with 60MB instead of 55."** Keep the built-in Latin reader on Android; leaving it out (this slice's original goal) is dropped. The iPhone build carries no page-reader files (`DinoReader-1.101.0.ipa`, 94.5 MB: 0 text-recognition files; Apple Vision is part of iOS), so nothing changes there. Still open, not decided by the user: whether this slice becomes downloadable readers for the alphabets Dino cannot read (gap below). Of the iPhone download, `TranslateMLKit.framework` is 24.5 MB and `SherpaOnnxC.framework` 9.4 MB (compressed, measured the same day).

## Measured and found 2026-11-04 (after the night 2026-11-04-1441 wave 1 parked this slice)

- **Size of the built-in reader, measured in the released `DinoReader-1.101.0.apk` (60.3 MB, arm64):** ML Kit's text-recognition files (`assets/mlkit-google-ocr-models/**` plus `lib/arm64-v8a/libmlkit_google_ocr_pipeline.so`, 11.06 MB on its own) are 12.0 MB unpacked and **5.2 MB of the APK download**. Leaving them out saves about 5 MB of the 60 MB download.
- **Gap, from reading the code (not run on a device):** Dino on Android reads only Latin-script pages. A captured Farsi, Arabic, Russian, Japanese or Chinese page gives "No text found on this page" (`Wizard_NoText`, `TextStep.xaml.cs:111`) or garbled Latin letters, with no "not supported" note: `LanguageImageTextExtractor.cs:50-83` falls back to PaddleOCR `ocr-*` packs only when one is installed, and `src/DinoReader.App` has no pack installer (`OcrPackInstaller` is RTST-only). iOS: Apple Vision covers Latin plus ja, zh, ru on iOS 16+ (`VisionImageTextExtractor.cs:21-27,43-50`), not Farsi or Arabic. Windows: `Windows.Media.Ocr` for the book's language if installed, else a profile-language fallback (`WindowsOcrTextExtractor.cs:45-57`). No slice, test or unverified line recorded this before.

## Facts to check first (not checked yet)

- Android today bundles ML Kit's Latin text recognition (a few MB, `Mobile.Shared.csproj:49-66`); other alphabets already come as downloadable PaddleOCR packs (`docs/voice-packs.md`). ML Kit has a Play-services variant that downloads its model on demand: confirm with `library-docs` (context7) before switching.
- iOS: which text recognition Dino uses and whether it adds to the app size (not checked on 2026-11-04). If it is Apple's built-in Vision, nothing downloads on iPhone and the message is not shown there; say so in this file.
- Whether the bundled reader can be left out for Dino only (Read The Stupid Text keeps its current reader unless the user says otherwise).

## Work

- [ ] Android: Dino uses the on-demand reader; first capture downloads it with the message, Cancel stops it and returns to the wizard; offline says so with Try again.
- [ ] iOS: the twin of the above, or a written reason it needs nothing (built-in reader), in this file.
- [ ] Strings: the message in all 15 languages.
- [ ] Read The Stupid Text: unchanged; its build and scanner tests still pass.

#### Design (Dino Reader Batch 47 Onboarding mock, frames 170-171, added 2026-11-04)

Source: `docs/design/dino-reader/Dino Reader Batch 47 Onboarding.dc.html`, 2 frames at 360x760: 170 download (line 241), 171 offline (line 264). Both are the shared wait overlay over the "New book" wizard (`Sand` page, back arrow, title 20/600, a 2-column grid of page thumbnails 10 dp gap, radius 14, the grey box is an empty slot). The mock agrees with the plan's message and Cancel.

- **Download (170).** Overlay `Sand` at 92%; 130 dp hopping Dino (`DinoWait`, Slice 143); "Downloading the AI reader for English…" 17/600 balanced (language name changes only); sub-line "Only the first time. It turns your photos into words." 13 `Body`; 12 dp bar (`BrandTint` track, `Brand` fill, 38%), "Downloading…" left and percent right 13.5/600; Cancel (44 high, white, 2 dp `#e3dccd` border). When it finishes it goes straight on to "Reading the pictures…" with no extra tap. Cancel goes back to the wizard and keeps the pages taken.
- **Offline (171).** Dino icon 110 dp with a 40 dp white badge (3 dp `Sand` border); "Connect to the internet to get the AI reader" 17/600; "Your pages are kept. Dino needs the reader once to turn them into words." 13 `Body`, max 260; "Try again" 52 high, `Coral`, 16/600; Cancel below. Try again resumes where it stopped; Cancel returns to the wizard, the next capture tries again. Typed books and PDFs with text never see it.
- **Files.** `ScanPages.cs`, `StoryWizardPage.xaml.cs`, `Mobile.Shared/Ocr/**` (progress callback), `AppResources*.resx`.
- **New strings (15 locales).** "Downloading the AI reader for {0}…", "Only the first time. It turns your photos into words.", "Connect to the internet to get the AI reader", "Your pages are kept. Dino needs the reader once to turn them into words."

**Open point: iPhone (mock question 4).** If iOS uses Apple's built-in reader, nothing downloads there and 170-171 are Android only. Slice already says to check first; this is not "Android only" by choice. **Default:** check first, write the answer in this file; if built in, ship no screen on iOS.
**Open point: wait stage.** `WaitStage` has a fixed list; a download-with-bar stage for the reader may need a new stage. **Default:** reuse the download stage with a language-name line.

## Parked (night 2026-11-04-1441 wave 1, not claimed)

Design depends on mechanisms nobody has proven, plus one product gap. Code check on origin/main: Dino recognises page text through `OcrPositionedTextExtractor` (src/DinoReader.App/MauiProgram.cs:129) over the shared `LanguageImageTextExtractor` with the bundled ML Kit Latin model (Mobile.Shared.csproj:60, Android) or Apple Vision (iOS, built in, nothing to download). Dino has no OCR pack download at all (`OcrPackInstaller` and `ScanPackFlow` are RTST-only), so "every alphabet downloads on first capture" means a new Dino pack path too. Leaving ML Kit out for Dino only needs a per-app MSBuild switch on a shared library that restores once (two separate builds of Mobile.Shared), unproven. The on-demand variant `Xamarin.GooglePlayServices.MLKit.Text.Recognition` 119.0.1.9 exists, but whether `ModuleInstallClient` is bound in .NET, how to cancel it, and what happens without Play services (emulator) are unconfirmed. Next: a probe build of the per-app switch and a ModuleInstall probe on the emulator, then a written design.

## Verification

- Dino APK size before and after (expect a few MB smaller); emulator flow `capture-reader-download.yaml` (first capture shows the download message, then text appears); RTST scanner flow still passes.
