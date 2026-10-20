# Slice 64 — Picture mode on real pages

*Planned.*

**Batch:** 10
**Decisions:** 78, 79
**Depends on:** Slices 62, 63, 45
**Touches:** `src/DinoReader.App/**`, `src/DinoReader.Core/**`, `tests/DinoReader.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

A story made from scanned pages or a PDF shows the **real page pictures** in picture mode,
with a highlight box over the lines of the sentence being spoken.

## Steps

- [ ] Reading screen (Slice 62): when a story has page images, picture mode uses them instead
      of generated pages; boxes come from `RangeToBoxMapper` (Slice 63).
- [ ] Fit the page image to the view keeping aspect ratio; draw the overlay in the image's own
      coordinate space so rotation (EXIF / portrait scans) and letterboxing stay correct.
- [ ] Multi-line and multi-page sentences: several boxes, continued on the next page.
- [ ] RTL pages: lines mapped in logical order (boxes are rectangles, so direction only
      matters for the mapper); verify with an Arabic/Farsi sample after Slice 52.
- [ ] Zoom-safe overlay: pinch-zoom and pan keep boxes aligned (normalized coordinates
      transformed with the image).
- [ ] Mixed stories: pages without an image get a generated page.

## Acceptance

- Scan a 3-page picture book: picture mode shows the three photos and the highlight follows
  every sentence including one that wraps lines; swipe and tap-to-read-from work as in 62.
- Rotate the phone and zoom: boxes stay on the text.
- An Arabic/Farsi scan highlights the right lines.

## Risks / notes

- OCR box drift on curved pages: pad boxes slightly; fall back to the text view if the mapper
  cannot place a sentence.
