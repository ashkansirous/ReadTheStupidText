# Slice 79 — Background text: drop signs and titles from the story, with a manual override

*Built 2026-10-22 (wave 2026-10-22), PR #240; not yet verified on a real picture-book scan.

**Batch:** 13
**Decisions:** 86
**Depends on:** Slice 76 (`LineReflow` and its geometry rule), Slice 78 (the wizard's Text step hosts the override)
**Touches:** `src/ReadTheStupidText.Application/Images/Ocr/TextRegions.cs` (new), `src/ReadTheStupidText.Application/Images/Ocr/RegionClass.cs` (new), `src/ReadTheStupidText.Application/Images/Ocr/OcrReading.cs`, `src/ReadTheStupidText.Application/Images/Ocr/LineOrdering.cs`, `src/DinoReader.Core/StoryPage.cs` (or `Story.cs` where `StoryPage` lives), `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/ImportedText.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/TextStep.xaml`, `src/DinoReader.App/TextStep.xaml.cs`, `src/DinoReader.App/PageTextPage.cs`, `src/DinoReader.App/RegionOverlay.cs` (new), `tests/ReadTheStupidText.Tests/TextRegionsTests.cs` (new), `tests/ReadTheStupidText.Tests/OcrLayoutTests.cs`, `tests/DinoReader.Tests/StorySegmenterTests.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/TextPages.cs`, `src/ReadTheStupidText.Application/Reading/LineClass.cs`, `src/ReadTheStupidText.Application/Reading/LineReflow.cs`, `tests/ReadTheStupidText.Tests/LineReflowTests.cs`, `src/DinoReader.App/RegionPicker.cs` (new), `src/DinoReader.Core/ScanLayout.cs`, `src/DinoReader.Core/PageDraft.cs`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryDraft.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx` and `AppResources.cs` (one `Regions_` block after the `Wizard_` keys)
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

- [x] `TextRegion(IReadOnlyList<OcrLine> Lines, NormalizedRect Bounds, RegionClass Class)`
      and `TextRegions.Classify` in `Application/Images/Ocr`; pure, with tests on: the
      Bright Stadium layout (sign dropped, 4-line story kept), a two-paragraph page (both
      kept), a page with a page number at the foot (dropped), a single-region page
      (kept), a title page with one big line (kept — rule 5).
- [x] `StoryPage` gains `Regions` (bounds + class + an `Included` override, nullable so
      old JSON loads with "follow the heuristic"); `PageText`/`TextOf` compose only from
      included regions. `StoryJson` round-trip test.
- [x] `StoryImport.WithText` builds pages from `TextRegions.Classify`, not from raw lines.
- [x] `RegionOverlay` in the wizard's Text step page card and in `PageTextPage`: the page
      image with each region outlined — included regions in ink, excluded greyed with a
      hatched fill — tap toggles `Included`; the text below re-composes live. One line of
      helper copy: "Tap a box to read it or leave it out."
- [x] Reading screen: `RangeToBoxMapper` input is the included lines only (excluded regions
      never get a highlight because they are never in the text).
- [x] Re-running OCR on a page (Slice 78 rotate/crop) resets that page's overrides.
- [x] `StorySegmenterTests`: a story built from the Bright Stadium fixture has no
      "BRIGHT STADIUM" segment.

## Taken over from Slice 76 (wave 2026-10-22)

- [x] Dino: feed each page's `Lines` boxes into `StorySentences.Build(..., LineBreakMap)` / `StorySegmenter.Split(text, LineBreakMap)` (via `LineReflow.BreakMap(text, boxes)`), in `StoryVoicing`, `ReadingPage` and `TextPages`, so the geometry rule (height, gap) applies to scanned pages. Plain-text pages keep the plain-text rule.
- [x] Geometry heading needs a text-shape check too (Slice 76 review 2 #2): a line is a heading only if it also looks like one (short, no terminal punctuation, not starting lowercase), not on box height or gap alone.
- The RTST Mobile/Windows scan text path (ReaderPage, image-text extractors) is **not** here: it is Slice 86, sequenced after the Mobile chain 81-85.

## Verification

- `dotnet test` on both suites green, with the five layout fixtures.
- Emulator (`device-test`): import the Bright Stadium page as a photo → Text step shows the
  sign greyed, the paragraph in ink; the composed text starts "Carrot and Hazelnut…". Tap
  the sign → it joins the text; tap again → gone. Save, read: the sign is not spoken; boxes
  land on the paragraph lines. A page with two paragraphs keeps both.

## Changed while building

- `TextRegions`/`RegionClass` follow the rule with named constants; boxes are aspect-corrected. `PageRegion` and `StoryPage.Regions` (null = follow the heuristic, so old JSON loads); damaged lines drop with an index remap.
- Page text is composed from included lines only; `ScanLayout` maps boxes from included lines only.
- `Story.BreakMap` uses geometry only when the included lines match the text after whitespace normalisation, else the plain-text rule; cached per `Story`. `CastPage.cs` calls `story.BreakMap()` (done on this branch after Slice 80 merged).
- Geometry heading also needs a text shape (at most 60 chars, not starting lowercase).
- `RegionOverlay` (`AspectFit.ContentRect` mapping, smallest box on top, semantics) in `TextStep` and `PageTextPage`; toggling over typed text asks Replace/Keep; `PageTextPage` Back cancels. 7 `Regions_` strings in 9 locales.

## From review

- Review 1 (`reviews/79-review-1.md`): fixed 1-3 and 5-12; 4 is the CastPage item, done after Slice 80.
- Review 2 (`reviews/79-review-2.md`): fixed 2-6 and 8-10; 1 matches the rule (manual override is the remedy); 7 intended.

## Checks

Dino 405 and RTST 697 tests pass; Dino Android, Dino Windows x64 and RTST App x64 build. Emulator (RTST_Test): Bright Stadium sign and page number greyed, paragraph kept; tap toggles live; two paragraphs kept; save and read only included text with correct highlight; Replace/Keep prompt; Back cancels; outlines sit on the glyphs. PASS.

#### For CLAUDE.md

- `src/DinoReader.App/CLAUDE.md`: page text is composed from included regions only (`StoryPage.Regions`; null = heuristic); a region toggle never silently replaces typed text. (Slice 79: docs/plan/slices/79-background-text-filtering.md)
- `src/DinoReader.Core/CLAUDE.md`: `Story.BreakMap` uses geometry only when the included lines match the text. (Slice 79: docs/plan/slices/79-background-text-filtering.md)

#### Not yet verified

- A real picture-book scan on a device.
- Windows overlay taps.
