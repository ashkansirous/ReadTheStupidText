# Slice 86 — RTST scan reads with line geometry

*Built 2026-10-22 (wave 2026-10-22), PR #248, not yet verified on a device.*

**Batch:** 14
**Depends on:** Slices 76 and 85 (sequenced after the Mobile chain; all of it touches `ReaderPage.xaml.cs`).
**Touches:** `src/ReadTheStupidText.Application/Images/Ocr/ImageText.cs`, `src/ReadTheStupidText.Mobile.Shared/Ocr/LanguageImageTextExtractor.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/VisionImageTextExtractor.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Windows/WindowsOcrTextExtractor.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, the Windows OCR read path (`src/ReadTheStupidText.App`), `tests/ReadTheStupidText.Tests/**`, `src/ReadTheStupidText.Application/Reading/ScanReadingText.cs`, `src/ReadTheStupidText.Application/Reading/LineReflow.cs`, `tests/ReadTheStupidText.Tests/ImageTextReadingTests.cs`
**Story issue:** —

## Goal

A scanned page in RTST (Mobile and Windows OCR) reads as its author meant it: line geometry, not only the plain-text rule, decides where a wrapped line continues and where a paragraph or heading starts.

## Why

Slice 76 deferred this. `ImageText` already carries `Lines` (`OcrLine` boxes), but the RTST read paths only use `ImageText.Text`, so headings use only the plain-text rule (see `docs/plan/slices/76-soft-line-break-reflow.md`, the scan-paths step).

## Steps

- [x] Check that all three extractors (`LanguageImageTextExtractor`, `VisionImageTextExtractor`, `WindowsOcrTextExtractor`) fill `ImageText.Lines` consistently with `Text` (one line per `
`); fix any that do not. Find the RTST Windows desktop OCR read path (App/Infrastructure) and name it in Touches when building.
- [x] Build the read units from `OcrReading.ReflowedText()` / `LineReflow.BreakMap(text, boxes)`; the displayed text keeps its `
`s.
- [x] Wire this into `ReaderPage.xaml.cs` and the Windows OCR read path.
- [x] Tests: geometry-driven paragraph and heading breaks, displayed text unchanged, plain-text fallback when there are no boxes.

## Verification

- Unit tests above pass.
- On a device, a scanned book page reads with no pause mid-sentence and a pause at each paragraph and heading (RTST Android, RTST Windows).

*Changed while building:* geometry is carried in the text, not in a break map passed to `ISpeechReader`. `ScanReadingText.From(ImageText)` adds one blank line only at geometry heading starts and ends (consecutive headings included), keeping every `
`, so the displayed text gains those blank lines. It aligns ML Kit `FullText` to `Lines` by trimmed text, skips whitespace-only and box-less lines, and falls back to `Text` on a real mismatch. `LanguageImageTextExtractor` and `WindowsOcrTextExtractor` return it from `ExtractTextAsync`; `ReaderPage` is unchanged and Dino `ExtractLinesAsync` is untouched. `LineBreakMap` gains `HeadingPairs` (Dino ignores it). The geometry heading text-shape check was already in place (Slice 79).

*From review:* (`reviews/86-review-1.md`) fixed 1, 2, 3, 5; 4 by design. (`reviews/86-review-2.md`) fixed 2-5 (5 partly: `OcrLine.Box` stays non-nullable, confined to `BoxOf`); 1 skipped (no logger at that layer).

*Checks:* RTST 809 pass / 2 skipped, Dino 410; Mobile and Dino Android builds.

#### For CLAUDE.md

In `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`: RTST image-text extractors return `ScanReadingText.From(...)` from `ExtractTextAsync` (geometry headings become blank-line breaks); `ExtractLinesAsync` stays raw for Dino. (Slice 86: docs/plan/slices/86-rtst-scan-line-geometry.md)

#### Not yet verified

- A scanned page with a heading reads the heading as its own unit on a Google-signed-in Android device and in the Windows Mobile build (the emulator's ML Kit scanner fails).
