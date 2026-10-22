# Slice 86 — RTST scan reads with line geometry

*Planned.*

**Batch:** 14
**Depends on:** Slices 76 and 85 (sequenced after the Mobile chain; all of it touches `ReaderPage.xaml.cs`).
**Touches:** `src/ReadTheStupidText.Application/Images/Ocr/ImageText.cs`, `src/ReadTheStupidText.Mobile.Shared/Ocr/LanguageImageTextExtractor.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/VisionImageTextExtractor.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Windows/WindowsOcrTextExtractor.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, the Windows OCR read path (`src/ReadTheStupidText.App`), `tests/ReadTheStupidText.Tests/**`
**Story issue:** —

## Goal

A scanned page in RTST (Mobile and Windows OCR) reads as its author meant it: line geometry, not only the plain-text rule, decides where a wrapped line continues and where a paragraph or heading starts.

## Why

Slice 76 deferred this. `ImageText` already carries `Lines` (`OcrLine` boxes), but the RTST read paths only use `ImageText.Text`, so headings use only the plain-text rule (see `docs/plan/slices/76-soft-line-break-reflow.md`, the scan-paths step).

## Steps

- [ ] Check that all three extractors (`LanguageImageTextExtractor`, `VisionImageTextExtractor`, `WindowsOcrTextExtractor`) fill `ImageText.Lines` consistently with `Text` (one line per `
`); fix any that do not. Find the RTST Windows desktop OCR read path (App/Infrastructure) and name it in Touches when building.
- [ ] Build the read units from `OcrReading.ReflowedText()` / `LineReflow.BreakMap(text, boxes)`; the displayed text keeps its `
`s.
- [ ] Wire this into `ReaderPage.xaml.cs` and the Windows OCR read path.
- [ ] Tests: geometry-driven paragraph and heading breaks, displayed text unchanged, plain-text fallback when there are no boxes.

## Verification

- Unit tests above pass.
- On a device, a scanned book page reads with no pause mid-sentence and a pause at each paragraph and heading (RTST Android, RTST Windows).
