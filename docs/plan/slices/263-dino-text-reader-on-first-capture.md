# Slice 263 — Dino: both apps about 50-60 MB; drop ML Kit translation from iPhone, download what is needed on first use
**Product:** Shared

**Dropped 2026-11-05 by the user: "lets keep ml kit for now."** Google ML Kit translation stays in the iPhone app (about 82 MB after Slice 261's starter removal; Android about 47 MB). Reason on record: Apple allows downloading data after install, never program code, so the ML Kit engine cannot be fetched later; the routes without it either lose Farsi (Apple Translation, Farsi unconfirmed, iOS 18 minimum) or need a 630 MB model download (M2M100). Reopen only if the user asks. The text below is the record.

*Planned 2026-11-04. Rewritten 2026-11-04 ~21:05 to the user's actual goal* (the user: "what I asked you to change was to download whatever you need when you need the function the first time ... and the ml kit removal from IOS to bring the package size down to 50ish MB for both android and ios. I am fine with both being 60ish as well"). The earlier text below (an Android page-reader goal) was a misreading by the main session and is kept only as the record. Dino Reader, Android and iOS. Read The Stupid Text keeps working exactly as today.

> **Park note (moved back to planned by the final feedback loop of night 2026-11-04-1441, user rule 2026-11-01):** night 2026-11-04-1441 wave 3 parked this slice because it waits for the user's choice of iPhone translation route (see *Choices for the user*). A wave that picks it up before the user has chosen does no build: it records "still waiting" here and moves on.

## Measured (released builds, 2026-11-04)

- **iPhone `DinoReader-1.101.0.ipa`, 94.5 MB download:** the app binary 32.3 MB; **`TranslateMLKit.framework` 24.5 MB** (Google ML Kit translation, used to translate a parent's own book on the phone); `SherpaOnnxC.framework` (voices) 9.4 MB; **the four starter books 12.5 MB** (three-little-pigs 3.6, good-enough 3.1, tortoise-and-hare 3.0, gingerbread-man 2.8); PdfPig and OpenXml (reading PDF and Word files) about 2.3 MB; the rest .NET. The iPhone has **no** page-reader (text recognition) files: it uses Apple Vision, part of iOS.
- **Android `DinoReader-1.101.0.apk`, 60.3 MB:** ML Kit text recognition 5.2 MB (kept, user: fine at 60 MB); the starters about 13 MB.

## Target and how it is reached

| | Today | After Slice 261 (no starters) | After this slice |
|---|---|---|---|
| Android | 60.3 MB | about 47 MB | about 47 MB |
| iPhone | 94.5 MB | about 82 MB | **about 57 MB** |

- Starter books out: Slice 261 (in progress). Book audio in the family's 1-3 languages only, downloaded with the book: Slice 267's rule. Both already planned.
- **This slice: the iPhone stops shipping Google ML Kit translation (24.5 MB).** A parent's own book is translated on the phone with Apple's built-in Translation framework instead, whose language models iOS downloads the first time they are needed. Apple does not allow downloading program code after install, so the ML Kit framework itself cannot be "downloaded later"; replacing it is the way to drop it.

## Work

- [ ] iOS: an `ITranslator` on Apple's Translation framework (on-device; models downloaded on first use with the app's wait note "Getting the <language> translation…", Cancel); `TranslateMLKit` and `MLKitBinding` removed from the iOS build. How to call it from .NET (a small Swift wrapper bound into the app, or an existing binding) and the minimum iOS version it needs are read from the docs first (a library-docs check was started 2026-11-04 ~21:05; its answer is written here). A language Apple does not translate (check Farsi) says so in the book, as Android does without a pack.
  *Docs check 2026-11-04 ~21:10 (library-docs agent; Apple's pages did not load, so the Apple facts are from memory and UNCONFIRMED):* .NET for iOS has no binding for Apple's Translation framework (checked in github.com/dotnet/macios); it is Swift-only, so it needs a small Swift wrapper built on a Mac and bound with Native Library Interop (learn.microsoft.com/dotnet/communitytoolkit/maui/native-library-interop). Apple Translation needs iOS 18, and iOS 26 for a session without SwiftUI (unconfirmed); our minimum iOS is 15.5 (`DinoReader.App.csproj:37`, `Mobile.Shared.csproj:35`). **Apple Translation probably has no Farsi** (also possibly no da, fi, nb, sv) — unconfirmed; if true, this route loses own-book Farsi translation on iPhone.
  *Second route (in the repo already):* downloadable translation model packs on the onnxruntime the app already ships, like `translation-en-ja` (ElanMT, Decision 171): drop ML Kit on iOS and fetch a model per language pair on first use (data, which Apple allows). Not checked: a usable model for every pair, Farsi first.
  - [ ] Before building: confirm Farsi in Apple Translation's supported list, and check which pairs have an openly licensed model for the second route (no GPL; Slice 16 licence rule). Pick the route that keeps Farsi and every app language, write why here.
- [ ] Android: unchanged (ML Kit translation stays; its models already download on first use).
- [ ] Measure the iPhone and Android downloads after Slice 261 and this slice; write both numbers here. Target: about 50-60 MB each.
- [ ] Tests: unit tests for the translator choice; iOS compile in CI; Android flows unchanged.

## Waits for the user: how the iPhone translates Farsi without ML Kit (night 2026-11-04-1441 wave 3, not claimed)

Wave 3 research (`reviews/s263-translation-route-research.md`) and a second lookup by the starter session (2026-11-04 ~23:40 UTC, web pages read, nothing downloaded):

- **Apple Translation:** needs iOS 18 (our minimum is 15.5; a session without SwiftUI needs iOS 26). Apple's pages give no language list (the app is told to ask `LanguageAvailability` at run time), so Farsi is **not confirmed either way**; the consumer Translate app has no Persian (weak evidence).
- **Meta M2M100 418M:** MIT licence, covers Farsi and every app language in one model. The smallest ONNX export (Xenova, quantized) is about **627 MB** (encoder 288 + decoder 339), read from the file list. Speed and memory on a phone are not measured.
- **Helsinki Opus-MT `opus-mt-en-iir`:** Apache-2.0, English to Persian only. Persian to English was not found (`opus-mt-fa-en` and `opus-mt-en-fa` returned 401). Size not shown.
- **Not usable:** NLLB-200 and the persiannlp mT5 Farsi models (non-commercial); MADLAD-400 (smallest 3B, Farsi not named).

Choices for the user:
1. **Keep ML Kit on the iPhone.** About 82 MB after Slice 261; Farsi translation stays. Android about 47 MB.
2. **Drop ML Kit, use Apple Translation.** iPhone about 57 MB, minimum iOS raised to 18; Farsi own-book translation on iPhone only if Apple supports it (unconfirmed; a probe on an iOS 18 device would tell).
3. **Drop ML Kit, download M2M100 the first time a grown-up translates their own book.** iPhone about 57 MB, Farsi kept, but a download of about 630 MB once, and phone speed unmeasured.
4. Another route the user names.

Parked by night 2026-11-04-1441 wave 3 (not claimed): waits for the user's pick from the list above; housekeeping 3 set the plan.md row to `parked`.

## Not this slice (found 2026-11-04, waits for the user)

- Capturing a Farsi, Arabic, Russian, Japanese or Chinese page reads no text on Android (Farsi and Arabic also on iPhone); see *Measured and found* below. Fixing it means downloadable page readers (the `ocr-*` packs Read The Stupid Text uses). Not asked for; recorded so it is not lost.

**Touches:** `src/Sirous.Reading.Mobile.Shared/Sirous.Reading.Mobile.Shared.csproj`, `src/Sirous.Reading.Mobile.Shared/Ocr/**`, `src/Sirous.Reading.Mobile.Shared/Platforms/**`, `src/DinoReader.App/ScanPages.cs`, `src/DinoReader.App/StoryWizardPage.xaml.cs`, `src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/capture-reader-download.yaml` (new), `src/Sirous.Reading.Mobile.Shared/CLAUDE.md`, `docs/plan/slices/263-dino-text-reader-on-first-capture.md`.

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
