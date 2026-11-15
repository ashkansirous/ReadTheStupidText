# Slice 387 — Dino: the shelf title "My stories" is cut to "My sto" by the Grown-ups button
**Product:** Dino

*Built (wave 2026-11-15), PR #1157.*

**Touches:** `src/Dino/DinoReader.App/ShelfHeader.cs` (the shelf header is built in code), `src/Dino/DinoReader.Core/ShelfTitleRules.cs`, `tests/Dino/DinoReader.Tests/ShelfTitleRulesTests.cs`, `docs/plan/slices/387-dino-shelf-title-cut-by-grownups-button.md`

## Why

The first iOS store-screenshot run on main (Slice 291, run 43650776897) shows the shelf title "My stories" (`Gallery_Title`) cut to "My sto" by the Grown-ups button on the iPhone 6.9" and 6.5" shots: `D:/ReadTheStupidText-worktrees/reviews/s291-shots/ios/dino-ios-sheet.png`.

## Work

- [x] 1. Find the header layout that lets the button cover the title, and fix it so the title shows whole or shortens with an ellipsis before the button, on iOS and Android alike (same app).
- [x] 2. Check a long title (the German and Farsi strings) and a large text size, so the fix does not move the cut. (English and German on the Android emulator at font 1.0 and 1.3; Farsi and the iPhone shot are under Not yet verified.)

## Verification

- A contact-sheet look at the shelf on an iPhone 6.9" simulator shot (`store-screenshots.yml`) and on the Android emulator: the title is not cut by the button.

## Changed while building

- Cause (likely, from code, not shown by a log): `ShelfHeader.Fit` measured the Grown-ups pill before it had a native view (Measure about 0 or only padding), chose Full, and the NoWrap title was hard-clipped; Fit re-ran only when the header's width changed.
- Fix: pure `ShelfTitleRules.PillWidth` (the laid-out width; the larger of it and a real measure; an estimate only when unmeasured), `Fit` re-runs on the pill's `HandlerChanged` and `SizeChanged`, the title uses `TailTruncation`, `TitleSlack` is 2.
- Tests: `DinoReader.Tests` 7267 passed.
- Emulator run 2026-11-15 (emulator-5554, build e182586fb5bb), PASS on all: English 1.0 title "My stories" [190,183][448,254] vs pill [739,155][1002,281]; English font 1.3 title [190,178][485,259] vs pill [680,155][1002,281]; German 1.0 and 1.3 pill "Für Erwachsene" at [650..] / [565..] with the title Hidden (a Fit outcome, never cut). Evidence: `D:/ReadTheStupidText-worktrees/reviews/s387-device/`.
- `cmd locale set-app-locales` does not change this app's language; only its own Language page does.

## Not yet verified

- Farsi strings on the shelf header (title whole or shortened, never cut by the pill).
- The iPhone 6.9" simulator shot: `store-screenshots.yml` run on the branch is pending; the result line is added when it reports.

## For CLAUDE.md

- Shelf header (`src/Dino/DinoReader.App/ShelfHeader.cs`): size the title room from the Grown-ups pill's laid-out width (`ShelfTitleRules.PillWidth`), never from one early `Measure`, which returns about 0 before the pill has a native view; `Fit` must re-run on the pill's `HandlerChanged`/`SizeChanged`. (Slice 387: docs/plan/slices/387-dino-shelf-title-cut-by-grownups-button.md)

## From review

- Skipped #1 (`reviews/s387-check.md`): "PillRoom calls WidthWithLabel (toggles IsVisible, re-measures) from the pill's own SizeChanged/HandlerChanged." - moot: the ShelfHeader Grown-ups pill never has LabelShown=false (only CornerShell's own Pill sets it, CornerShell.cs:183/280), so WidthWithLabel toggles true to true and the measured width is the shown width.
- Skipped #2 (`reviews/s387-check.md`): "a pill with label dropped (icon-only) still reserves full-label room, so the title can fall to Short/Hidden for no reason." - moot: the ShelfHeader pill never has LabelShown=false (CornerShell.cs:183/280), so it is never icon-only.
- Skipped #3 (`reviews/s387-check.md`): "misclassifies a real measure of an empty/very short label as unmeasured" - low.
- Skipped #4 (`reviews/s387-check.md`): "ignores OS font scale; under-estimates for Farsi/CJK/large text; Fit is not re-run unless pill width changes by more than 0.5." - low; the estimate is used only before the first layout, and the large-text case passed on the emulator at font 1.3.
- Skipped #5 (`reviews/s387-check.md`): "every pill event re-measures uncached before width arrives, and Fit can run twice in one pass" - low, redundant work only.
- Skipped #6 (`reviews/s387-check.md`): "an under-measured title (iOS rounding) shows an ellipsis instead of falling back to the short title; no test covers it." - the ellipsis is the slice's own ask (Work item 1, "shortens with an ellipsis").
- Skipped #7 (`reviews/s387-check.md`): "8-argument PillWidth with positional numeric args can be swapped silently" - low (style).
- Skipped #8 (`reviews/s387-check.md`): "only pure rules are tested; nothing exercises ShelfHeader.Fit with a real pill" - low; the real pill was checked on the emulator instead.

- Skipped #2 (`reviews/s387-high.md`): "The estimate ignores OS font scale and script width (UTF-16 length, fixed 0.6 em)." - moot: the estimate is used only before the pill's first layout and the laid-out width then replaces it; the German/Farsi/large-text case is the Not yet verified line.
- Skipped #2 (`reviews/s387-medium.md`): "fallback pill estimate uses labelLength*labelSize*0.6 (Latin widths). CJK labels are about 1 em per char, so the estimate is too small and the title still clips on first layout" - the estimate is used only before layout and then replaced by the real width.
- Skipped #3 (`reviews/s387-medium.md`): "HandlerChanged/SizeChanged on GrownUps call FitWhenPillChanged, which compares _lastPill" - low, redundant work only.
- Skipped #4 (`reviews/s387-medium.md`): "TitleSlack (2 dp) is added before comparing to Short; in a row where Short barely fits, Hidden wins and the icon stands alone." - low.
- Skipped #5 (`reviews/s387-medium.md`): "LineBreakMode changed NoWrap -> TailTruncation" - low; the ellipsis is the slice's own ask.
- Skipped #6 (`reviews/s387-medium.md`): "PillWidth takes 8 positional numeric args" - low (style).
- Skipped #7 (`reviews/s387-medium.md`): "PillRoom calls GrownUps.WidthWithLabel() (native measure) on every Fit and pill SizeChanged although PillWidth ignores it when Width>0" - low (perf); finding 1 now uses the measure when laid out, so it is no longer ignored.
- Skipped #4 (`reviews/s387-high.md`): "Fit is triggered from the pill's HandlerChanged and SizeChanged as well as the header's" - low, redundant work only; the 0.5 pt guard bounds the passes.
- Skipped #5 (`reviews/s387-high.md`): "Switching the title from NoWrap to TailTruncation hides residual under-estimates" - low; the ellipsis is the slice's own ask ("shortens with an ellipsis").
- Skipped #6 (`reviews/s387-high.md`): "The slack test only shows WithSlack adds 2; it does not exercise the clipping case it names." - low; WithSlack is gone, the test now pins the TitleSlack boundary.
- Skipped #8 (`reviews/s387-high.md`): "Slice is in progress but items are unticked, and the Touches line lists files not changed." - the docs pass ticks items and fixes Touches; the missing Not yet verified line is added above.
