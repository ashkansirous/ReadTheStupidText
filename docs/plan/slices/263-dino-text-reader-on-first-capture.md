# Slice 263 — Dino: the page reader (text recognition) downloads on the first capture

*Planned 2026-11-04.* Dino Reader; Android and iOS alike. Read The Stupid Text shares the scanner code in Mobile.Shared and must keep working exactly as today.

**Touches:** `src/ReadTheStupidText.Mobile.Shared/ReadTheStupidText.Mobile.Shared.csproj`, `src/ReadTheStupidText.Mobile.Shared/Ocr/**`, `src/ReadTheStupidText.Mobile.Shared/Platforms/**`, `src/DinoReader.App/ScanPages.cs`, `src/DinoReader.App/StoryWizardPage.xaml.cs`, `src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/capture-reader-download.yaml` (new), `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`, `docs/plan/slices/263-dino-text-reader-on-first-capture.md`.

## Decisions (user, 2026-11-04, RefineScope)

- "Voice to text" in the user's request means the reader that turns a photo of a page into words when a grown-up captures their own book (there is no speech recognition in the app).
- The app ships with **no page reader built in**. The first time someone captures a book, the app shows "Downloading the AI reader for <language>…" with progress and Cancel, then carries on. The same message is used for every language and alphabet.

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

## Verification

- Dino APK size before and after (expect a few MB smaller); emulator flow `capture-reader-download.yaml` (first capture shows the download message, then text appears); RTST scanner flow still passes.
