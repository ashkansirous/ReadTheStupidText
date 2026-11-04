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

## Verification

- Dino APK size before and after (expect a few MB smaller); emulator flow `capture-reader-download.yaml` (first capture shows the download message, then text appears); RTST scanner flow still passes.
