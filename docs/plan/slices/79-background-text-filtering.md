# Slice 79 — Background text: drop signs and titles from the story, with a manual override

*In progress (wave 2026-10-22), draft PR.*

**Batch:** 13
**Decisions:** 86
**Depends on:** Slice 76 (`LineReflow` and its geometry rule), Slice 78 (the wizard's Text step hosts the override)
**Touches:** `src/ReadTheStupidText.Application/Images/Ocr/TextRegions.cs` (new), `src/ReadTheStupidText.Application/Images/Ocr/RegionClass.cs` (new), `src/ReadTheStupidText.Application/Images/Ocr/OcrReading.cs`, `src/ReadTheStupidText.Application/Images/Ocr/LineOrdering.cs`, `src/DinoReader.Core/StoryPage.cs` (or `Story.cs` where `StoryPage` lives), `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/ImportedText.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/TextStep.xaml`, `src/DinoReader.App/TextStep.xaml.cs`, `src/DinoReader.App/PageTextPage.cs`, `src/DinoReader.App/RegionOverlay.cs` (new), `tests/ReadTheStupidText.Tests/TextRegionsTests.cs` (new), `tests/ReadTheStupidText.Tests/OcrLayoutTests.cs`, `tests/DinoReader.Tests/StorySegmenterTests.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/TextPages.cs`, `src/ReadTheStupidText.Application/Reading/LineClass.cs`, `src/ReadTheStupidText.Application/Reading/LineReflow.cs`, `tests/ReadTheStupidText.Tests/LineReflowTests.cs`
**Story issue:** —

## Goal

A picture-book page's sign, banner or wall text ("BRIGHT STADIUM", image 2) is not read as
part of the story. The pipeline keeps the page's dominant text block and marks the rest as
background; in the wizard's Text step a grown-up sees the dropped regions greyed on the page
and taps any region to include or exclude it.

## Why

OCR returns every line it finds and the only filter is PaddleOCR's `MinConfidence`;
`StoryImport.WithText` keeps every line of every page. Picture books put story text in one
block of uniform size and decorate the illustration with larger, isolated lettering — size
and distance from the main block separate them reliably; a manual override catches the
rest.

## Decisions locked with the user (2026-10-22)

- **Heuristic by default, manual override always available** (not manual-only, not
  heuristic-only).
- The classifier lives in Application next to `LineOrdering` so RTST's scan path can use it
  later; **RTST exposes no UI for it in this slice** (out of scope), Dino does.
- Stored per page, so the choice survives edits and the reading screen never reads an
  excluded region.

## The rule (`TextRegions.Classify(OcrReading) → IReadOnlyList<TextRegion>`)

1. Group `OcrLine`s into regions: lines whose vertical gap ≤ 1.0× the page's median line
   height and whose horizontal extents overlap (or are centre-aligned within 0.5× median
   height) join one region.
2. Per region compute median line height and line count.
3. The **main region** is the one with the most characters whose median line height is
   within 0.75–1.35× the page-wide median line height.
4. Every other region is **background** if its median line height is ≥ 1.5× or ≤ 0.6× the
   main region's, **or** it has ≤ 2 lines and its distance from the main region is ≥ 3×
   median line height. Otherwise it is **story** too (a second paragraph block).
5. Pages with one region keep it; pages whose only region fails the size test still keep it
   (a page is never emptied by the heuristic).

Thresholds are named constants, tuned on the fixtures below.

## Steps

- [ ] `TextRegion(IReadOnlyList<OcrLine> Lines, NormalizedRect Bounds, RegionClass Class)`
      and `TextRegions.Classify` in `Application/Images/Ocr`; pure, with tests on: the
      Bright Stadium layout (sign dropped, 4-line story kept), a two-paragraph page (both
      kept), a page with a page number at the foot (dropped), a single-region page
      (kept), a title page with one big line (kept — rule 5).
- [ ] `StoryPage` gains `Regions` (bounds + class + an `Included` override, nullable so
      old JSON loads with "follow the heuristic"); `PageText`/`TextOf` compose only from
      included regions. `StoryJson` round-trip test.
- [ ] `StoryImport.WithText` builds pages from `TextRegions.Classify`, not from raw lines.
- [ ] `RegionOverlay` in the wizard's Text step page card and in `PageTextPage`: the page
      image with each region outlined — included regions in ink, excluded greyed with a
      hatched fill — tap toggles `Included`; the text below re-composes live. One line of
      helper copy: "Tap a box to read it or leave it out."
- [ ] Reading screen: `RangeToBoxMapper` input is the included lines only (excluded regions
      never get a highlight because they are never in the text).
- [ ] Re-running OCR on a page (Slice 78 rotate/crop) resets that page's overrides.
- [ ] `StorySegmenterTests`: a story built from the Bright Stadium fixture has no
      "BRIGHT STADIUM" segment.

## Taken over from Slice 76 (wave 2026-10-22)

- [ ] Dino: feed each page's `Lines` boxes into `StorySentences.Build(..., LineBreakMap)` / `StorySegmenter.Split(text, LineBreakMap)` (via `LineReflow.BreakMap(text, boxes)`), in `StoryVoicing`, `ReadingPage` and `TextPages`, so the geometry rule (height, gap) applies to scanned pages. Plain-text pages keep the plain-text rule.
- [ ] Geometry heading needs a text-shape check too (Slice 76 review 2 #2): a line is a heading only if it also looks like one (short, no terminal punctuation, not starting lowercase), not on box height or gap alone.
- The RTST Mobile/Windows scan text path (ReaderPage, image-text extractors) is **not** here: it is Slice 86, sequenced after the Mobile chain 81-85.

## Verification

- `dotnet test` on both suites green, with the five layout fixtures.
- Emulator (`device-test`): import the Bright Stadium page as a photo → Text step shows the
  sign greyed, the paragraph in ink; the composed text starts "Carrot and Hazelnut…". Tap
  the sign → it joins the text; tap again → gone. Save, read: the sign is not spoken; boxes
  land on the paragraph lines. A page with two paragraphs keeps both.
