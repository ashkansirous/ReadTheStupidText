# Slice 63 — Positioned text extraction and kept page images

*Planned.*

**Batch:** 10
**Decisions:** 78
**Depends on:** Slice 45 (scanner), Slice 52 (PaddleOCR), Slice 58 (shared library)
**Touches:** `src/ReadTheStupidText.Application/Images/**`, `src/ReadTheStupidText.Documents/**`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/ReadTheStupidText.Mobile/**`, `src/StoryReader.Core/**`, `tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

Every source of a story yields lines with their position on the page, and the page images
are kept, so picture mode can box the spoken sentence over the real page. The text-only API
stays for Read The Stupid Text.

## Steps

- [ ] Application types: `PositionedPage(int Index, string? ImagePath, IReadOnlyList<TextLine>
      Lines)` and `TextLine(string Text, NormalizedRect Box)` with `NormalizedRect` in 0..1
      (left, top, width, height). New `IPositionedTextExtractor` next to `IImageTextExtractor`
      (`Application/Images/IImageTextExtractor.cs:12`); `ExtractTextAsync(string)` unchanged.
- [ ] ML Kit: `MlKitImageTextExtractor.cs:35` reads only the text and discards geometry; keep
      the line `BoundingBox` and normalize by the image size.
- [ ] PaddleOCR (Slice 52): detection already produces boxes; keep them. Slice 52's interface
      step must return lines + boxes and derive the text-only call from it (note added to 52).
- [ ] PDF: `PdfTextExtractor` uses `page.Text` only (`Documents/PdfTextExtractor.cs:29`); add a
      positioned variant using PdfPig `GetWords()` grouped into lines (baseline y, then x,
      RTL aware); no text layer -> OCR of the rendered page.
- [ ] PDF page rendering: Android `PdfRenderer` first, behind `IPdfPageRenderer`. iOS (PDFKit)
      and Windows (`Windows.Data.Pdf`) renderers are noted for Slices 67/49, not built here.
- [ ] Scanner keeps page images: the old scan flow discards them (`ScanPage.xaml.cs:60-61,78`)
      and Slice 45 must not either. Story Reader copies the scanner's page images into the
      story folder; Read The Stupid Text still deletes them.
- [ ] Pure `RangeToBoxMapper` (StoryReader.Core): from the page's line texts in reading order
      and a text source range, return the line boxes covering it (whole line boxed even when
      the range is partial). Unit-tested incl. a sentence over two lines and over two pages.
- [ ] Tests for normalization, line grouping and the mapper.

## Acceptance

- A scanned page, a PDF with a text layer and a PDF without one each produce lines whose
  boxes land on the right text when drawn over the image (visual check, recorded).
- Existing `ExtractTextAsync` callers and tests are unchanged and green.
- Mapper tests: a sentence over 3 lines returns 3 boxes in order.

## Risks / notes

- Line grouping can merge columns; accept for single-column kids' books.
- Kept images cost storage: JPEG, long edge capped around 1600 px.
