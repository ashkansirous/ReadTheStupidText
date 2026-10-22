# Slice 84 — Scan reads the page in the page's script, whatever the app language

*Built 2026-10-22 (wave 2026-10-22), PR #246; on-device checks not yet verified.*

**Batch:** 14
**Decisions:** 91
**Depends on:** Slice 83 (both edit the scan path in `ReaderPage.xaml.cs`; run after it). Slice 79
also edits `LineOrdering.cs`; this slice should not need to, but sequence after 79 if it does.
**Touches:** `src/ReadTheStupidText.Mobile.Shared/Ocr/LanguageImageTextExtractor.cs`, `src/ReadTheStupidText.Application/Images/Ocr/ScriptDetector.cs` (new), `src/ReadTheStupidText.Application/Images/Ocr/InstalledOcrCatalog.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `tests/ReadTheStupidText.Tests/ScriptDetectorTests.cs` (new), `tests/ReadTheStupidText.Tests/OcrLogicTests.cs`, `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`, `src/ReadTheStupidText.Application/Images/Ocr/OcrPackChoice.cs` (new), `src/ReadTheStupidText.Application/Images/Ocr/ImageText.cs`, `src/ReadTheStupidText.Application/Images/Ocr/OcrReading.cs`, `src/ReadTheStupidText.Application/Images/Ocr/OcrLanguages.cs`, `src/ReadTheStupidText.Mobile.Shared/Ocr/IBuiltInTextRecognizer.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MlKitImageTextExtractor.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MobileSharedServices.Android.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/VisionImageTextExtractor.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/MobileSharedServices.iOS.cs`
**Story issue:** —

## Goal

An English book scanned with the app in Farsi comes out as the English the page shows, in the
right order. The OCR model is chosen from the page's script; the app language plays no part.

## Why

Seen on a Pixel 10 Pro, 1.16.0, 2026-10-22, app in فارسی, scanning a Peter Rabbit page
("Squirrel Nutkin loves exploring / with his friend, Peter Rabbit."): the text came out as
"Peter Rabbit.,ith his friend", with letters dropped ("w", "'s") and word runs reversed.
`LanguageImageTextExtractor.ExtractTextAsync` picks the PaddleOCR pack from
`_settings.Language`, so the Farsi (Arabic-script) model read Latin text, and the RTL path
(`LineOrdering` right-to-left row order plus `RightToLeftText`'s reversal) flipped the runs.
The user: "The language of the app should not impact the content of the scan."

## Decisions locked with the user (2026-10-22)

- **Detect from the page.** The built-in recognizer (ML Kit Latin on Android, Vision on iOS)
  reads first. When its result is mostly Latin, that is the answer. When the page is mostly
  not Latin (few or low-confidence Latin characters), each installed OCR pack whose script
  could fit is tried, and the result with the most confident characters of its own script
  wins. RTL ordering applies only when the winning pack's script is right-to-left.
- OCR packs still download for the app language (that's the likely non-Latin script);
  Dino Reader (English-only today) shares the same extractor and gains the same rule.

## Steps

- [x] `ScriptDetector` (Application, pure): the share of Latin / Arabic / Cyrillic / CJK letters
      in a reading, and whether a reading counts as "mostly Latin"; unit-tested with the
      Nutkin line, a Farsi line, mixed lines with digits.
- [x] `LanguageImageTextExtractor`: the language argument becomes a hint, not the choice;
      `ExtractTextAsync` and `ExtractLinesAsync` run built-in first, then the installed packs
      only for non-Latin pages. `FallbackSource` still flags a pending pack.
- [x] `ReaderPage` stops passing the app language as the OCR language. (ReaderPage calls `IImageTextExtractor.ExtractTextAsync(path)` with no language; the extractor reads the app language only as the hint. `OcrPositionedTextExtractor` still passes it as that hint. Packs still download for the app language, per the decisions above.)
- [x] `OcrLogicTests`: an English page with the app in Farsi yields the Latin reading in
      left-to-right order.
- [x] `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`: OCR choice rule (page script, never app
      language).

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`
- Phone, app in فارسی: scan the Nutkin page; the text reads "Squirrel Nutkin loves exploring
  with his friend, Peter Rabbit. "Let's find food to make a big forest feast," said Nutkin."
  in order. Then scan a Farsi page in the same mode: it still reads correctly right-to-left.
- App in English: a Farsi page is read by the Farsi pack when it's installed.

*Changed while building:* the built-in recognizer runs first; a Latin verdict is final only at >=0.85 Latin share and >=12 letters, otherwise the installed non-Latin packs are tried (the loaded engine first, each at most once, early stop at a clear win). A pack wins only if its own-script letters exceed the built-in's total letters by `PackWinMargin` = 2; with a non-Latin hint, that one hint pack is always also tried. `ScriptDetector` is one table (Latin, Greek, Cyrillic, Hebrew, Arabic, Devanagari, Hangul, CJK; unknown = Unknown). The choice is a pure `OcrPackChoice` with tests; the fallback is keyed on the page script; ML Kit `FullText` keeps paragraph breaks; logging goes through `ISystemLog`; OOM propagates. OCR packs still download for the app language only (the user's decision).

*From review* (`reviews/84-review-1.md`, `84-review-2.md`): all fixed except review 2 #5 (pack results join `Lines`, the built-in uses `FullText`; accepted).

*Checks:* RTST tests 768 pass; Mobile and Dino Android and Dino Windows x64 build.

## Open question for the user

Should an English-app user scanning a Farsi page be offered the Farsi OCR pack? Today packs download for the app language only, so that page is read only if the Farsi pack is already installed.

#### For CLAUDE.md

- OCR choice follows the page, never the app language: `LanguageImageTextExtractor` reads with the built-in recognizer first; a mostly-Latin result of at least `OcrPackChoice.MinLatinLetters` letters is final, otherwise the installed non-Latin packs are tried via the pure `OcrPackChoice`, and right-to-left ordering applies only when the winning pack's script is. The language argument is only a hint. (Slice 84: docs/plan/slices/84-ocr-follows-the-page-not-the-app-language.md) Already added to `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md` on the branch.

#### Not yet verified

- On-device checks: the Nutkin page with the app in Farsi, a Farsi page, and an English app with a Farsi page. The emulator's ML Kit scanner fails, so it needs a Google-signed-in device.
