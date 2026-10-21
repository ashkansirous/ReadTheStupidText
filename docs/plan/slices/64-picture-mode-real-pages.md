# Slice 64 — Picture mode on real pages

*Built 2026-10-21 (PR #218, merge 53cc881); not yet verified, see below.*

**Batch:** 10
**Decisions:** 78, 79
**Depends on:** Slices 62, 63, 45
**Touches:** `src/DinoReader.App/**`, `src/DinoReader.Core/**`, `tests/DinoReader.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

A story made from scanned pages or a PDF shows the **real page pictures** in picture mode,
with a highlight box over the lines of the sentence being spoken.

## Steps

- [x] Reading screen (Slice 62): when a story has page images, picture mode uses them instead
      of generated pages; boxes come from `RangeToBoxMapper` (Slice 63).
- [x] Fit the page image to the view keeping aspect ratio; draw the overlay in the image's own
      coordinate space so rotation (EXIF / portrait scans) and letterboxing stay correct.
- [x] Multi-line and multi-page sentences: several boxes, continued on the next page.
- [ ] RTL pages: lines mapped in logical order (boxes are rectangles, so direction only
      matters for the mapper); verify with an Arabic/Farsi sample after Slice 52.
- [x] Zoom-safe overlay: pinch-zoom and pan keep boxes aligned (normalized coordinates
      transformed with the image).
- [x] Mixed stories: pages without an image get a generated page.

#### Design (Dino Reader v2 mock, screens 2, 13, added 2026-10-20)

Source: `docs/design/dino-reader/Dino Reader v2.dc.html`. Two frames (screen 2 lines 28-30; tablet 96-107). The mock agrees with the plan (box over the lines of the spoken sentence, wrapping across lines, a character’s line in her own voice). Bar, pager and transport are in Slice 62 Design; tokens in Slice 58. The striped "scanned illustration" panel stands in for the user’s photo, not UI.

- **Page frame (screen 2).** Card margin 6/14/0, radius 6, fill `#f7f1e3`, shadow `0 6px 18px rgba(43,37,64,.18)` plus inset 1px `rgba(0,0,0,.05)`, padding 18/18/16. Not tilted: the refreshed mock has no rotation anywhere, and its intro (line 21) says "Pages are never tilted, so highlight boxes always sit on the words."
- **Highlight on a real page.** Each spoken line gets fill `rgba(255,214,0,.42)`, inset 2px outline `#F5B800`, radius 9, extended 6px sideways. A wrapped sentence shows two stacked boxes.
- **Tablet (screen 13).** Scanned double-page spread at full height; boxes in the image’s own coordinates "so pinch-zoom and rotation keep them on the text". Controls in a 140px side column (Slice 62).
- The mode choice is remembered per story.

**Settled by the mock: page tilt.** No tilt; the overlay is drawn in the image’s own coordinate space.

## Acceptance

- Scan a 3-page picture book: picture mode shows the three photos and the highlight follows
  every sentence including one that wraps lines; swipe and tap-to-read-from work as in 62.
- Rotate the phone and zoom: boxes stay on the text.
- An Arabic/Farsi scan highlights the right lines.

## Risks / notes

- OCR box drift on curved pages: pad boxes slightly; fall back to the text view if the mapper
  cannot place a sentence.

## Built

- **Model.** `StoryPage` gained optional `Lines` (`PageLine(Text, NormalizedRect Box)`), stored in the story JSON. Old files load unchanged; small box overshoots clamp, and a truly off-page line is dropped without losing the page picture.
- **Core.** `ScanLayout` matches lines to story text once, tolerating whitespace, newlines and end-of-line hyphens. It precomputes page ranges and answers boxes per sentence (multi-line, and across pages), the page of a position, sentences per page, tap-to-sentence, and carried-over sentence tails on generated pages (with real sentence indexes).
- **App rendering.** `ScanPages` (an `IPageRenderer`) draws a real page only when it has a picture, matched lines and a readable image size; otherwise a generated page. `ZoomablePage`: gestures sit on an untransformed outer container and only an inner layout is scaled. Pinch goes up to 4x and snaps to 1 below `ZoomSnapThreshold` 1.1. Pan is active only while zoomed, swipes are suppressed while zoomed, and it has its own tap and swipe forwarding. `PageImageSize` reads the header size with EXIF 5-8 swap.
- **Import.** The Scan and Photos tiles now work (`IDocumentScanner`, `MediaPicker.PickPhotosAsync` with permission catches), with per-page OCR via `OcrPositionedTextExtractor` (a failed page is skipped). PDF goes through `RenderedPdfTextExtractor`: a text-layer PDF takes its story text from the normal extractor and uses positioned lines only for boxes, and its pictures render lazily at save. Each import gets its own `import-<guid>` folder, and temp files are deleted on save, replace, back, navigate-away and failure. Save keeps pictures (`IPageImageKeeper`, `page-N.jpg`) only while the text is unedited, and cleans up the story folder on failure.

*Changed while building (beyond Touches, accepted):* `src/ReadTheStupidText.Documents/PdfTextExtractor.cs` now uses PdfPig `ContentOrderTextExtractor.GetText` (context7 `/uglytoad/pdfpig`) instead of `page.Text`, which glued lines together (`bag.And`). This affects RTST PDF reading too, for the better. Test: `tests/ReadTheStupidText.Tests/PdfTextExtractorTests.cs`.

*From review:* two passes (10 + 10 findings, all fixed). Checks: DinoReader.Tests 353, RTST tests 511, Android builds for DinoReader.App and Mobile.

*Emulator acceptance (2026-10-21, AVD RTST_Test):* PDF import with flowing text, picture mode with real pages, highlight following speech across lines and pages, pinch, pan while zoomed, swipe at scale 1, tap-to-read and Photos import all PASS, with no crash. Fixed after acceptance: the missing PDF line spaces, the "1 page read" count, and the zoom snap back to 1.

*Not done:* RTL step (no Arabic/Farsi scan to test), card shadow (shared card has no shadow support), tablet double-page layout (Slice 62 chrome), and the `PageTextPage` edit flow doesn't carry pictures. Editing imported text saves a text-only story.

#### For CLAUDE.md (lifted 2026-10-21)

In `src/DinoReader.App/CLAUDE.md`: Picture mode on real pages: `ScanLayout` (Core) maps sentences to line boxes; `ScanPages`/`ZoomablePage` (App) render them. Boxes are in the picture's own 0..1 coordinates, so zoom and EXIF rotation keep them on the text. Gestures go on the untransformed outer view; only the inner layout is scaled. Imports keep pictures only while the text is unedited; temp files live in a per-import folder and must be deleted on every exit path. (Slice 64: docs/plan/slices/64-picture-mode-real-pages.md)

#### For README.md (lifted 2026-10-21)

Dino Reader shows the real scanned or PDF pages in picture mode, with the spoken sentence highlighted.

#### Not yet verified

- A real camera scan of a picture book on a physical device.
- An Arabic or Farsi scan highlights the right lines.
- The denied-permission alert on Photos.
- A 1.1 snap threshold feels right on a real phone.
