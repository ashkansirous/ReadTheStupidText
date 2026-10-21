# Slice 63 — Positioned text extraction and kept page images

*Built (wave 2026-10-21b), PR #205.* Not yet verified on a device (see the end of this file).

**Batch:** 10
**Decisions:** 78
**Depends on:** Slice 45 (scanner), Slice 52 (PaddleOCR), Slice 58 (shared library)
**Touches:** `src/ReadTheStupidText.Application/Images/**`, `src/ReadTheStupidText.Documents/**`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/ReadTheStupidText.Mobile/**`, `src/DinoReader.Core/**`, `tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

Every source of a story yields lines with their position on the page, and the page images
are kept, so picture mode can box the spoken sentence over the real page. The text-only API
stays for Read The Stupid Text.

## Steps

- [x] Application types: `PositionedPage(int Index, string? ImagePath, IReadOnlyList<TextLine>
      Lines)` and `TextLine(string Text, NormalizedRect Box)` with `NormalizedRect` in 0..1
      (left, top, width, height). New `IPositionedTextExtractor` next to `IImageTextExtractor`
      (`Application/Images/IImageTextExtractor.cs:12`); `ExtractTextAsync(string)` unchanged.
- [x] ML Kit: `MlKitImageTextExtractor.cs:35` reads only the text and discards geometry; keep
      the line `BoundingBox` and normalize by the image size.
- [x] PaddleOCR (Slice 52): detection already produces boxes; keep them. Slice 52's interface
      step must return lines + boxes and derive the text-only call from it (note added to 52).
- [x] PDF: `PdfTextExtractor` uses `page.Text` only (`Documents/PdfTextExtractor.cs:29`); add a
      positioned variant using PdfPig `GetWords()` grouped into lines (baseline y, then x,
      RTL aware); no text layer -> OCR of the rendered page.
- [x] PDF page rendering: Android `PdfRenderer` first, behind `IPdfPageRenderer`. iOS (PDFKit)
      and Windows (`Windows.Data.Pdf`) renderers are noted for Slices 67/49, not built here.
- [x] Scanner keeps page images: the old scan flow discards them (`ScanPage.xaml.cs:60-61,78`)
      and Slice 45 must not either. Dino Reader copies the scanner's page images into the
      story folder; Read The Stupid Text still deletes them.
- [x] Pure `RangeToBoxMapper` (DinoReader.Core): from the page's line texts in reading order
      and a text source range, return the line boxes covering it (whole line boxed even when
      the range is partial). Unit-tested incl. a sentence over two lines and over two pages.
- [x] Tests for normalization, line grouping and the mapper.

## Acceptance

- A scanned page, a PDF with a text layer and a PDF without one each produce lines whose
  boxes land on the right text when drawn over the image (visual check, recorded).
- Existing `ExtractTextAsync` callers and tests are unchanged and green.
- Mapper tests: a sentence over 3 lines returns 3 boxes in order.

## Risks / notes

- Line grouping can merge columns; accept for single-column kids' books.
- Kept images cost storage: JPEG, long edge capped around 1600 px.

## Build status: first pass (2026-10-21)

*Superseded: the split below is history. Slice 52 was merged from main and the second pass finished the work.*

Slice 52's implementation (`c230d27`) was not on this branch (cut from its claim commit) and
merging feature branches was ruled out, so the work is split. Slice 52 lands on main first; then
`origin/main` is merged into this branch for the second pass.

**Done (first pass, independent of Slice 52):**
- `Application/Images`: `NormalizedRect` (with `FromBox` clamping and `Union`), `TextLine`,
  `PositionedPage`, `IPositionedTextExtractor` (`ExtractPagesAsync(path)`), `IPdfPageRenderer`
  (interface only).
- `Documents`: `PdfPositionedTextExtractor` (PdfPig `GetWords()`, y flipped to top-left origin),
  pure `WordLineGrouper` (row grouping by vertical centre, RTL-aware ordering), `PositionedWord`.
- `DinoReader.Core`: `RangeToBoxMapper` + `LineRef`. It returns line references (page, line), not
  boxes, so Core stays free of the Application types; the caller looks the boxes up.
- Tests: `PositionedTextTests` (RTST suite), `RangeToBoxMapperTests` (Dino suite).

**Second pass (Slice 52 merged from main; done):**
- [x] Line boxes kept end to end. `ImageText` now carries the pixel size of the image the boxes are in
      (`Width`, `Height`); `PaddleOcrEngine` reports the detection-image size, `MlKitImageTextExtractor`
      the size of the `InputImage` ML Kit decoded (swapped for a quarter-turn rotation). Pure `OcrPageMapper` normalises them and
      `OcrPositionedTextExtractor` (Application) is the image `IPositionedTextExtractor`.
- [x] PDF page with no text layer: `RenderedPdfTextExtractor` (Documents) wraps
      `PdfPositionedTextExtractor` and OCRs the rendered image of any page that has no text layer.
      Rendering is on demand: extraction renders only those pages, in one renderer session
      (`IPdfPageRenderer.RenderPagesAsync` takes a set of `PdfPageTarget`s and opens the document once).
      A page with a text layer gets no image during extraction; a caller that wants page images
      (Slice 64 picture mode) calls `RenderedPdfTextExtractor.RenderPagesAsync(pdfPath, pageIndexes)`
      for the pages it shows, so a 200-page PDF is never rendered eagerly. A page that fails to render
      keeps its text-layer result.
- [x] `AndroidPdfPageRenderer` (Android `PdfRenderer`, white background, long edge 1600, JPEG).
- [x] Scanner and OCR moved into `Mobile.Shared`: `IDocumentScanner` + its three exceptions,
      `ScannedImageFiles` (now public), and under `Platforms/Android` the document scanner, ML Kit and
      PaddleOCR extractors, the onnx session, pixel loader and OCR page. The ML Kit, onnxruntime-managed
      and Fragment.Ktx package references moved with them. Registered once in
      `MobileSharedServices.Android.cs`; RTST's `MauiProgram.Android.cs` keeps only the OCR pack installer,
      language detection and translation. iOS files and registrations were left alone (Slice 55).
- [x] Page images are kept through `IPageImageKeeper` (`AndroidPageImageKeeper`: upright JPEG, long edge
      1600). RTST's Reader page is untouched and still deletes pages after OCR.
- [x] APK check (arm64 builds of both apps, unzipped): see Decisions below.
- [ ] Follow-up, open (after Slices 61/62/65/66 merge): wire Dino's dimmed "Scan pages" and "Photos" tiles and
      its PDF import to the shared scanner, keeper, `OcrPositionedTextExtractor` and
      `RenderedPdfTextExtractor`; Dino's own `MainActivity.OnActivityResult` must call
      `AndroidDocumentScanner.HandleActivityResult` (the scanner returns only through it).
      `src/DinoReader.App` is not touched by this slice.

## Decisions taken during build

- The mapper works on line texts and a story range, and returns whole lines (as `LineRef`). Each line must
  start at the cursor past only whitespace (a trailing hyphen also matches a joined word); lines that do not
  are skipped without moving the cursor, so a dropped page number cannot derail the lines after it.
- PDF boxes: PdfPig 0.1.16 already reports word coordinates relative to the visible box's lower-left corner
  (CropBox, else MediaBox) and `Page.Width/Height` are that box's size, so the extractor must not subtract the
  corner again (review finding 4 checked and not applied; a theory test with a shifted MediaBox and a CropBox pins it).
- Android image locations (path, `file://`, `content://`) are opened, sized and EXIF-read in one helper,
  `AndroidImageSource`; the EXIF is read from the stream, so content-URI photos rotate correctly.
- PDF page rotation is not applied; the page cap (200) matches `PdfTextExtractor`.
- **Dino now carries ML Kit OCR.** The previous rule ("Dino Reader ships no OCR") could not stand once
  Dino scans pages. The scanner and OCR live in `Mobile.Shared`, so Dino's arm64 APK gains
  `libmlkit_google_ocr_pipeline.so` (11 MB), `libarc.bin.so` and the `assets/mlkit-google-ocr-models/`
  Latin models. The language OCR packs are still downloaded, not bundled. Translation and language-id
  stay RTST-only (Dino's APK has no `libtranslate_jni.so` or `liblanguage_id_l2c_jni.so`).
- APK check (arm64 Debug builds, unzipped): in both apps `lib/arm64-v8a/` has exactly one
  `libonnxruntime.so` (25,831,632 bytes, identical to the sherpa android-arm64 package's) and one
  `libsherpa-onnx-c-api.so`; no linux, win or osx native libraries; the managed onnxruntime binding added
  no second runtime. Release/AAB sizes were not measured.
- ML Kit boxes: context7 docs for `InputImage.fromFilePath` do not say whether it reads EXIF, pre-rotates the
  bitmap, or what `getWidth`/`getHeight`/`getRotationDegrees` report, so `MlKitImageTextExtractor.ExtractLinesAsync`
  no longer uses it: it decodes the image, turns it upright from the EXIF orientation (the same
  `ImageOrientation` the PaddleOCR loader and page keeper use, mirrored values included) and passes
  `InputImage.FromBitmap(upright, 0)`. The frame is exactly that bitmap's size (long edge capped at 4096).
  PaddleOCR already reports the upright detection image size, so both engines and the keeper agree.
  The ML Kit document scanner "detects rotation to show documents upright" per its docs, so its pages are
  taken to carry no EXIF rotation; if one did, this would rotate it. Needs the device check below.
- Review fixes: `RangeToBoxMapper` resyncs by a bounded forward search (2 x line length + 64) after a line
  that is not at the cursor, unless the next line matches at the cursor (dropped page number); a line found
  nowhere is skipped. `RenderedPdfTextExtractor` catches OCR failure per page. `WordLineGrouper` joins a word
  to a row when it overlaps the row's median band by 40% of the smaller height. The PDF renderer writes each
  page to a temp file and moves it into place. `ScannedImageFiles` documents that kept images (IPageImageKeeper)
  live in a different folder and are written before the sources are deleted; the keeper rejects destination == source.
- Not fixed here: Dino's `MainActivity` scanner result forwarding (finding 2) - `src/DinoReader.App` belongs to
  Slice 66 and Dino does not call the scanner yet, so it goes with the Dino scan-tile wiring follow-up. The
  APK check (finding 8) was already done above.
- Page images are written as JPEG at quality 85 with the long edge capped at 1600 (the slice's risk note).
- iOS and Windows PDF renderers, and a Vision/PaddleOCR-on-iOS positioned extractor, are not built
  (Slices 55, 67, 49).

## Device checks still needed

- Draw the boxes over a scanned page, a photographed page (EXIF-rotated), a PDF with a text layer and a
  PDF without one, and confirm they land on the right text (ML Kit and PaddleOCR).
- An EXIF-rotated gallery photo through ML Kit (boxes use the upright bitmap) and a scanner page (no rotation applied twice).
- RTL line order on an Arabic or Farsi page.
- RTST scan flow unchanged after the move (scan, OCR progress, text in the editor, pages deleted).

## From review

*High pass:* 10 findings, 8 fixed. One was not a bug: PdfPig coordinates are already relative to the visible
box, proved by fixtures (see Decisions). One-line methods were skipped as the repo convention.

*Medium pass:* 9 findings, 7 fixed. Skipped: Dino `MainActivity` scanner-result forwarding (follow-up with the
Dino scan wiring) and the APK check (done in part 2).

## Verification

- 461 RTST and 229 Dino tests pass.
- Android builds of both apps and the Windows x64 build pass.
- APK check: one `libonnxruntime.so` per app at the matching size; Dino now carries ML Kit OCR; RTST content unchanged.
- A code check confirmed RTST still registers exactly one of each OCR/scanner service, still routes the scanner
  result, and still has translation and the OCR pack installer.

#### For CLAUDE.md (lifted 2026-10-21)

- The scanner and OCR stack (ML Kit, PaddleOCR runner, document scanner, PDF page renderer, page-image keeper)
  lives in `Mobile.Shared/Platforms/Android` and is registered once in `MobileSharedServices.Android.cs`. Apps
  must not register their own. (Slice 63: docs/plan/slices/63-positioned-text-extraction.md)
- An app using the scanner must forward `OnActivityResult` to `AndroidDocumentScanner.HandleActivityResult`.
  (Slice 63: docs/plan/slices/63-positioned-text-extraction.md)
- Positioned text uses `NormalizedRect` (0..1, top-left origin). `RangeToBoxMapper` maps story ranges to page
  lines. (Slice 63: docs/plan/slices/63-positioned-text-extraction.md)
- Translation and language-id stay in the RTST app. (Slice 63: docs/plan/slices/63-positioned-text-extraction.md)

#### Not yet verified

- Draw boxes over a scanned page, an EXIF-rotated photo, and PDFs with and without a text layer, with both ML Kit and PaddleOCR.
- RTL line order on a real Arabic/Farsi page.
- The RTST scan flow unchanged on a Google-signed-in device.
- Follow-up: wire Dino's Scan, Photos and PDF import to the shared services, including `MainActivity.OnActivityResult`.
