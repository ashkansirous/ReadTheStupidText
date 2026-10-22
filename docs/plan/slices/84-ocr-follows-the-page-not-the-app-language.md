# Slice 84 — Scan reads the page in the page's script, whatever the app language

*Planned.*

**Batch:** 14
**Decisions:** 91
**Depends on:** Slice 83 (both edit the scan path in `ReaderPage.xaml.cs`; run after it). Slice 79
also edits `LineOrdering.cs`; this slice should not need to, but sequence after 79 if it does.
**Touches:** `src/ReadTheStupidText.Mobile.Shared/Ocr/LanguageImageTextExtractor.cs`, `src/ReadTheStupidText.Application/Images/Ocr/ScriptDetector.cs` (new), `src/ReadTheStupidText.Application/Images/Ocr/InstalledOcrCatalog.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `tests/ReadTheStupidText.Tests/ScriptDetectorTests.cs` (new), `tests/ReadTheStupidText.Tests/OcrLogicTests.cs`, `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`
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

- [ ] `ScriptDetector` (Application, pure): the share of Latin / Arabic / Cyrillic / CJK letters
      in a reading, and whether a reading counts as "mostly Latin"; unit-tested with the
      Nutkin line, a Farsi line, mixed lines with digits.
- [ ] `LanguageImageTextExtractor`: the language argument becomes a hint, not the choice;
      `ExtractTextAsync` and `ExtractLinesAsync` run built-in first, then the installed packs
      only for non-Latin pages. `FallbackSource` still flags a pending pack.
- [ ] `ReaderPage` stops passing the app language as the OCR language.
- [ ] `OcrLogicTests`: an English page with the app in Farsi yields the Latin reading in
      left-to-right order.
- [ ] `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`: OCR choice rule (page script, never app
      language).

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`
- Phone, app in فارسی: scan the Nutkin page; the text reads "Squirrel Nutkin loves exploring
  with his friend, Peter Rabbit. "Let's find food to make a big forest feast," said Nutkin."
  in order. Then scan a Farsi page in the same mode: it still reads correctly right-to-left.
- App in English: a Farsi page is read by the Farsi pack when it's installed.
