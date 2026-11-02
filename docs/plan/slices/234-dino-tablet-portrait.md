### Slice 234 — Dino: a proper design for tablets held upright

*In progress (wave 2026-11-02), parts B and C draft PRs; part A built 2026-11-02, merged as PR #736 (b54e32ff).* *Planned 2026-11-01.* The user asked for "better design on tablet". **Mock drawn 2026-11-01 (Batch 45), added by the user in Claude Design, so it counts as approved**. Start after Slice 232 merges: it reuses 232's phone-vs-tablet rules (`IsLandscape`, the 600 dp short-side split, `ShelfLayout`, `SplitLayout`).

**Touches:** `src/DinoReader.Core/GalleryLayout.cs`, `src/DinoReader.Core/ReadingLayoutRules.cs`, `src/DinoReader.Core/*Layout*.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/RecordViews.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/BackupPage.xaml`, `src/DinoReader.App/PageEditorPage.xaml`, `src/DinoReader.App/GrownUpHeader.cs`, `tests/DinoReader.Tests/**Layout*`, `docs/design/dino-reader/**`, `docs/plan/slices/234-dino-tablet-portrait.md`

#### Parts (coordinator, claim 2026-11-02)

A premise check (2026-11-02, after Slice 232 merged) found about 14 screens, too big for one build, and that some tablet-upright behaviour already exists from 232: the corner rail at 744 dp (`LandscapeLayout.CornerLayout`, rail 360 dp), Settings two panes at 744 dp (`SettingsLayout.TwoPanes`), the intro stacking picture above copy with copy capped at 460 (`OnboardingLayout.Intro`). Split in the mock's order:

- **A (this claim):** Core `IsTabletPortrait` and the tablet-upright shelf rule; the shelf (133) and the reading screen stacked (134, 135, 150, and picture only as described).
- **B:** centred cards: gate (137), first run (138), book sheet (136), intro (141); corner rail at 300 dp with Store solid (139) and Settings rows showing their value (140).
- **C:** Record (142), Store and Cast (143, 144), page editor (145), Backup (146).

**Touches, part A (claim 2026-11-02, night 2026-11-01-2355 wave 4):** `src/DinoReader.Core/LandscapeLayout.cs` (`IsTabletPortrait`, the shelf rule), `src/DinoReader.Core/ReadingLayoutRules.cs`, `src/DinoReader.Core/GalleryLayout.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/ReadingTheme.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/TransportRow.cs`, `tests/DinoReader.Tests/*Layout*Tests.cs`, `tests/DinoReader.Tests/ReadingLayoutRules*Tests.cs`, `.maestro/dino/dino-tablet-upright-shelf*.yaml`, `.maestro/dino/dino-tablet-upright-reading*.yaml` (new), `docs/plan/slices/234-dino-tablet-portrait.md`. Read-only: `src/DinoReader.Core/SettingsLayout.cs`, `src/DinoReader.Core/OnboardingLayout.cs`, `src/DinoReader.Core/BookSheetPlan.cs`, `tools/emulator/**`.

**Part A design note (coordinator, claim):** same pattern as Slice 232: each page overrides `OnSizeAllocated`, stores the size, makes one pure Core call per layout pass, and never rebuilds views on a size change. Rules, each a pure unit-tested function: `LandscapeLayout.IsTabletPortrait(w,h)` = h >= w and w >= 600 dp. Shelf: when tablet upright, columns = clamp(floor((w - 80) / 170), 3, 6), side padding 40 dp, titles up to two lines, header targets 52 dp; phone upright (2 columns) and every landscape rule (190 divisor, sideways row) unchanged. Reading: a new `Stacked` shape from `ReadingLayoutRules.For` when tablet upright: picture panel 48% of the height, art fitted whole and centred (no crop), chip at the panel's top left; text card below at 32 sp; controls row 64 / 84 / 64 dp under the card; the 30% card cap does not apply. Text only: one tall card at 36 sp, chip inside its top left, Play in the same place. Picture only: art fitted in the full height above the same row. Phone upright and all landscape reading unchanged. Rotation keeps the reading position and the shelf state (the existing 232 mechanisms; a shape flip re-places views, never rebuilds). Device: RTST_Tablet (800 x 1280 dp, frame 150) upright; the 804 x 1164 frames are checked by the unit rules.

**Touches, part B (claim 2026-11-02, night 2026-11-01-2355 wave 5):** `src/DinoReader.Core/LandscapeLayout.cs`, `src/DinoReader.Core/SettingsLayout.cs`, `src/DinoReader.Core/OnboardingLayout.cs`, `src/DinoReader.Core/BookSheetPlan.cs`, `src/DinoReader.Core/TabletCardLayout.cs` (new), `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/IntroWizardPage.Art.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/GrownUpHeader.cs`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/SettingsRows.cs`, `tests/DinoReader.Tests/TabletCardLayoutTests.cs` (new), `tests/DinoReader.Tests/LandscapeLayout*Tests.cs`, `tests/DinoReader.Tests/SettingsLayout*Tests.cs`, `tests/DinoReader.Tests/OnboardingLayout*Tests.cs`, `tests/DinoReader.Tests/BookSheetPlan*Tests.cs`, `.maestro/dino/dino-tablet-upright-cards*.yaml` (new), `docs/plan/slices/234-dino-tablet-portrait.md`. Read-only: `tools/emulator/**`, every part C file.

**Part B design note (coordinator, claim):** same pattern as parts A and 232 (store the size in `OnSizeAllocated`, one pure Core call per layout pass, never rebuild views on a size change). Everything here applies only when `LandscapeLayout.IsTabletPortrait(w,h)`; phone upright and every landscape layout stay as they are (tests pin that). New Core `TabletCardLayout.CardWidth(w, kind)` = min(w - 80, max) for the gate, first run and book sheet; take each max from the drawn frames 137, 138, 136 (the mock text says 560 / 520 / 620 but also "a 560 dp card" for the sheet: the frame's drawn width wins; write which you took in the slice file). Gate: answers 80 dp, hold ring 160 dp. First run: all 14 languages in two columns, no scroll. Book sheet: cover on top, tap outside closes. Intro (141): step 101b panel above the copy, buttons in one row at the foot, other steps swap only panel and copy, keep 101b's copy. Corner (139): at w >= 744 tablet upright the rail is 300 dp (change only the tablet-upright corner and Settings list widths through a new named rule; `ListPaneWidth`'s other callers keep their values unless the mock frame shows otherwise), Store solid indigo in the rail, books in three columns; below 744 the phone header bar stays. Settings (140): two panes, 300 dp list plus detail, each row shows its value under its name (values already exist via `SettingsRows.SetNavValue`; match the frame's look), a row swaps the right pane, Done closes. Part C runs in parallel and owns Record, Store, Cast, editor, Backup: do not edit their files, and do not change `SettingsLayout.StoreColumns`, `SettingsLayout.BackupSideBySide` or `LandscapeLayout.CastColumns`.

#### Why (read from code on origin/main, 2026-11-01; not run on a tablet)

Nothing in Dino adapts to a tablet held upright. No `OnIdiom`, `DeviceInfo.Idiom` or width breakpoints anywhere in `src/DinoReader.App` or `DinoReader.Core`. The shelf grid is a fixed `GridItemsLayout Span="2"` (`GalleryPage.xaml:136`), so a 10-11" tablet shows two huge covers. `ReadingLayoutRules.For` (`ReadingLayoutRules.cs:16-20`) returns `Upright` whenever width <= height, so the reading screen uses the phone layout, and the text size is a portrait constant (`ReadingPage.xaml.cs:542, 984`). The gate and first-run boxes are capped narrow (`GatePage.xaml:29` 260, `FirstRunPage.xaml:100` 290) and float small. Slice 232 lists tablet-portrait rules for the shelf, intro, corner and Settings but draws none, and says nothing about the reading screen upright.

#### Decisions locked with the user (2026-11-01, RefineScope)

- **Dino Reader only**, Android and iOS (iPad) together.
- **Tablet held upright** = short side 600 dp or more, portrait. Every screen gets a tablet layout:
  - **Shelf:** 3-4 columns (the rule from `ShelfLayout`, not a fixed span).
  - **Reading:** picture on top, bigger text below, controls in a row under the text.
  - **Sheets, gate, first run:** centred cards sized for a tablet, not phone-width boxes.
  - **Grown-ups' corner and Settings:** the side rail and two panes as Slice 232 describes for 744 dp and wider.
  - **Intro:** picture above the copy (Slice 232).
  - Record, Store, Cast, page editor, Backup: wider cards and columns from the same rules.
- **Light only, never mirrored** in any language (user, 2026-11-01; Slice 96).

**User answers (2026-11-01):** skip this slice tonight. It is parked until the user sees and approves its mock (Batch 45); status `parked: waits for the user's approval of the mock`. Parked until 2026-11-01 by the user: skipped tonight, the user added the mock on 2026-11-01, so the slice is ready to build after 232.

#### Design (Dino Reader Batch 45 mock, frames 133-146 and 150, added 2026-11-01)

Fifteen frames in `docs/design/dino-reader/Dino Reader Batch 45 Tablet Upright.dc.html` (README index has the line of each). Tablet frames are 804 x 1164 dp (iPad Air, 1x); frame 150 is a 10" Android at 800 x 1280. Drawn on top of Batches 43 and 44. The user added the mock on 2026-11-01; it counts as approved. The mock agrees with the plan, with the open points below. Frames are real screens; the rules box and the "For the review" box at the top of the file are text, not UI. Frame numbers are not in order in the file (136 Book sheet sits after 139).

- **Shelf (133):** 4 columns at 804 dp, covers about 166 dp, side padding 40 dp, header targets 52 dp. Rule: `columns = clamp(floor((w - 80) / 170), 3, 6)`; 3 on small 600-680 dp tablets. Titles wrap to two lines. The fourth row peeks so it reads as "scroll for more".
- **Reading (134, 135, 150):** stacked. Picture panel is 48% of the height, art fitted whole and centred (3:4 art leaves cream margins), chip at the panel's top left; text card below at 32 sp (phone 22); controls row 64 / 84 / 64 dp. The 30% card cap from Batch 25 does not apply. Text only (135): one tall card at 36 sp, chip inside its top left; Play stays in the same place in all views. Picture only is not drawn. Frame 150: same rules on 16:10, nothing else changes.
- **Gate (137), first run (138), book sheet (136):** centred cards, `min(w - 80, 560 / 520 / 620)`. Gate answers grow to 80 dp, hold ring 160 dp. First run: all 14 languages in two columns, no scroll. The book sheet is a 560 dp card with the cover on top; a tap outside closes it.
- **Intro (141):** step 101b, picture panel above the copy, buttons in one row at the foot; other steps swap only panel and copy. The copy in the frame is illustrative; keep 101b's.
- **Corner (139):** 300 dp ink rail at 744 dp and wider, Store solid indigo, books in three columns. Below 744 dp the phone header bar comes back. **Settings (140):** two panes (300 dp list plus detail), each row shows its value under its name, a row swaps the right pane, Done closes.
- **Record (142):** Done header, page picture, words at 30 sp, recordings line, mic centred at the foot. **Store (143) and Cast (144):** two columns; Store covers 90 dp with a full-width action button; the Cast picker opens as a centred card like 136. **Page editor (145):** picture on top with Change picture, field below; keyboard up collapses the picture to a 120 dp strip so the field keeps five lines. **Backup (146):** the two cards side by side at the top, not stretched.
- **Core rules the mock lists:** `IsTabletPortrait(w,h)` = h >= w and w >= 600 dp; `ShelfLayout` as above; `ReadingLayout.Stacked`; `CardWidth`; `SidePanes` at w >= 744.
- **Light only, never mirrored** (Slice 96), as the plan says.
- **New strings:** none beyond existing ones (the Farsi labels in frame 149 belong to Slice 237).

**Open point: shelf column divisor.** The plan says 3-4 columns "from the `ShelfLayout` rule"; Batch 44's rule divides by 190, which gives 3 at 804 dp. The mock uses 170 for tablets so an iPad Air gets 4. **Default:** take the mock's 170 for tablet portrait, clamp 3 to 6.

**Open point: picture crop.** The mock fits the art whole in the 48% panel (cream margins for 3:4 art) and asks in its review box whether to crop to fill the width instead. **Default:** fit whole, as drawn.

**Open point: picture only, upright.** Described in text (art fits whole in the full height above the same row) but not drawn. **Default:** build it as described.

Settled by the mock, left open in the plan: reading panel 48% of the height, text 32 sp (36 sp text only), controls row 64 / 84 / 64, card widths 560 / 520 / 620. Unit-test these numbers.

#### Work

- [x] **Design, added by the user in Claude Design 2026-11-01 (counts as approved):** tablet-portrait mock (`Dino Reader Batch 45 Tablet Upright.dc.html` in `docs/design/dino-reader/`), 804 × 1164 dp and a 10" Android size, for every screen above, on top of Batches 43 and 44. Rules as pure functions in Core where they can be.
- [ ] Core layout rules extended for tablet portrait, with unit tests (shelf columns, reading layout shape and text size, card widths).
- [ ] Each screen built from the rules; rotation keeps state.
- [ ] Android and iOS built and checked together.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (layout rules).
- Scripted flow on a tablet AVD in portrait, every screen, screenshot each: no clipped or tiny controls, shelf shows 3-4 columns. iOS: iPad simulator or TestFlight on an iPad.

#### Part A: built (2026-11-02)

Part A covers the tablet-upright shelf and the reading page. The Work items above stay unticked because parts B-C are open.

- **Core:** `LandscapeLayout.IsTabletPortrait` (h >= w and w >= 600). Tablet-upright shelf columns are clamp(floor((w-80)/170), 3, 6) with 40 dp side padding (`ShelfShape.TabletUpright`, `SidePadding`). `ReadingLayout.Stacked` comes from `ReadingLayoutRules.For` on a tablet held upright for picture+text books and for text mode; `StackedFits` falls back to Upright on a too-short window (no real window hits it). The picture panel is 48% of (surface + 104 dp controls band); the card keeps 120 dp. Text is 32 sp (36 sp for text only).
- **App:** shelf header has 52 dp targets (language button included) and a 28 title. Stacked reading is a beige picture panel (art fitted whole) over a white scrolling card, with a `TransportRow.Wide` row of 64/84/64 dp and `ReadingTheme.PicturePanel`. Grown-up mode keeps Slice 232's corner rail (tabletUpright is False there by design, GalleryPage.xaml.cs:395-398).
- **Choices:** picture mode of books that are not picture+text keeps the upright layout. Text-only uses TextPages' card (no 28 dp corner or shadow; TextPages is outside Touches, part B/C may add it). No book title in Stacked (mock frames 134/135 show none).
- **Tests:** 2101 pass; Android build, iOS compile and lint-flows pass.
- **From review** (all three files read in full; the review files live outside the repo). The coordinator's brief gave one reason for high findings 3-8 together (low, polish or intended); no per-finding reason beyond that was recorded.
  - `s234a-review-high.md` (8 findings, all low):
    - 1 empty white text card on a page with no picture and no text (PictureTextPages.cs:178): skipped (low).
    - 2 language button stays 48 dp beside 52 dp header targets (GalleryPage.xaml.cs:498): applied.
    - 3 OnModeChanged calls SetMode before UpdateHeaderLayout (ReadingPage.xaml.cs:1178): skipped (low).
    - 4 stacked text sizes in three places (ReadingPage.xaml.cs:639): skipped (polish).
    - 5 TextStyleFor private, Stacked row untested (ReadingPage.xaml.cs:636): skipped (low).
    - 6 PicturePanel hard-codes "#F0E8D8" instead of a DinoTokens entry (ReadingTheme.cs:43): skipped (polish).
    - 7 new one-line methods against the no-one-liner convention (ReadingLayoutRules.cs:557): skipped (polish; the review called it optional).
    - 8 no book title left in Stacked (ReadingPage.xaml.cs:731): skipped, intended: mock frames 134/135 show none and a test pins it.
  - `s234a-review-medium.md` (6 findings, all low or low-medium):
    - 1 IsTabletPortrait is width-only, so a narrow Windows-head window gets the tablet layout: skipped (the mock's rule, same as Slice 235's WindowLayout).
    - 2 StackedPictureHeight returns 0 on a small surface (ReadingLayoutRules.cs:99-103): applied.
    - 3 ApplyHeaderShape rewrites phone sizes on every shape change, may override rail/corner sizing: skipped (not reproducible: grown-up mode never uses tablet sizes).
    - 4 language button width/corner radius may stay at 48 dp: skipped (unconfirmed).
    - 5 TransportRow.Wide ordering against Compact/_rail: skipped (Stacked never sets Compact or the rail).
    - 6 no test for the StackedPictureHeight zero case or the width==height boundary: applied.
  - `s234a-review-check-medium.md` (3 findings, all low, no blockers), all skipped:
    - 1 StackedFits uses a 104 dp band, the real Wide row may be about 122 dp: skipped (the guard is never reached by a real window); the short-window case stays untested.
    - 2 picture-only pages in Stacked have no panel treatment: skipped (the picture-only panel is not drawn in the mock).
    - 3 gear resets to 44 dp in the grown-up corner on a tablet upright: skipped (the corner gear is part B).
- **Device (RTST_Tablet, build 87e00636951d, cold boot after a System UI ANR on the warm snapshot):** dino-tablet-upright-shelf, dino-tablet-upright-reading and dino-landscape-reading-tablet PASS. dino-landscape-store FAIL: the flow expects the phone's `store columns=2`, the tablet logs 3 by design (SettingsLayout.StoreColumns), and this diff touches no Store file, so it is a phone-only flow. Upright shelf shot: 4 columns, nothing clipped (`D:/ReadTheStupidText-worktrees/reviews/s234a-shots/shelf-with-anr-dialog.png`).

#### Part B: built (2026-11-02)

Part B covers the cards (gate, first run, book sheet, intro), the grown-up corner and Settings on a tablet held upright. Phone and landscape values are unchanged.

- **Core:** new `TabletCardLayout`: `CardWidth` = min(w-80, max) with max 520 for the gate, 620 for first run, 560 for the book sheet (taken from the drawn frames, which differ from the mock text's 560/520/620); `CardMaxHeight` = h - 80; `ScrollAreaHeight`, `GateFixedBelow`. `LandscapeLayout.SideListWidth` is 300 on an upright tablet (`ListPaneWidth` unchanged elsewhere); the corner gets a `TabletUpright` flag and 3 columns. `SettingsLayout.ValueUnderName`. `OnboardingLayout` and `BookSheetPlan` gained card fields.
- **App:** the gate is a centred 520 card with 80 dp keys and a 160 dp ring; the hold ring and Back sit outside the card's ScrollView (always vertical), so a scroll cannot cancel a hold. First run is one 620 card with the 15 languages in two columns. The book sheet is a 560 card with the cover on top. The intro has a cream panel above the copy and its buttons in one row. In the corner, Store is solid indigo and the gear 48 dp. Settings has a 300 list with the value under the name (`SettingsRows.StackNavValue`, tag rows too).
- **Choices:** `GrownUpHeader` keeps 44 dp Back/Done (mock 48). Below 744 the phone corner header stays. No chevrons while stacked. Sideways keeps 360.
- **Tests:** 2158 pass; Android build, iOS compile and flow lint pass.
- **From review** (all four files read in full; the files live in `D:/ReadTheStupidText-worktrees/reviews/`, outside the repo):
  - `s234b-review-high.md` (9 findings):
    - 1 gate card has no height cap or scroll on a square window (GatePage.xaml.cs:105): applied.
    - 2 StyleCopy restyles only the first two labels (IntroWizardPage.cs:253): applied.
    - 3 hard-coded #F0E8D8 duplicates ReadingTheme.PicturePanel (IntroWizardPage.cs:39): applied.
    - 4 card constants repeated across four pages (TabletCardLayout.cs:16): skipped (polish).
    - 5 upright tablet fixed at 3 columns (LandscapeLayout.cs:142): skipped, intended three columns.
    - 6 StackNavValue skips tag rows (SettingsRows.cs:47): applied.
    - 7 StackNavValue forces text alignment, ignoring FlowDirection (SettingsRows.cs:59): skipped, the layout never mirrored Slice 96.
    - 8 dead `_shape?.KeyHeight` lookup in BuildKeyPad (GatePage.xaml.cs:349): skipped (nit).
    - 9 first-run card has no height cap or edge margin (FirstRunPage.xaml.cs:188): applied.
  - `s234b-review-medium.md` (11 findings):
    - 1 first-run card fills its cap instead of sizing to content (FirstRunPage.xaml.cs:213): applied.
    - 2 StyleCopy finds heading and line by position (IntroWizardPage.cs:311): skipped (low).
    - 3 StackNavValue picks children by position, StackTag duplicates code (SettingsRows.cs:40): skipped (low).
    - 4 stacked rows cannot grow for a wrapping value (SettingsRows.cs:56): applied.
    - 5 SheetGrid in a ScrollView may measure infinite height (GatePage.xaml.cs:134): applied.
    - 6 hold ring inside the ScrollView loses the hold on a drift or drag (GatePage.xaml.cs:131/242): applied (ring and Back moved outside the scroll).
    - 7 book sheet header left out of `_cardRows` (BookSheet.cs:106): skipped (low).
    - 8 BookSheetMaxHeight pass-through and repeated card constants (TabletCardLayout.cs:62): skipped (low).
    - 9 language button hard-coded height 52 (FirstRunPage.xaml.cs:268): applied.
    - 10 two sources for the key max height 64 (GatePage.xaml.cs:149): skipped (low).
    - 11 intro panel colour stays light in dark mode (IntroWizardPage.cs:416): skipped (light only).
  - `s234b-review-check-medium.md` (1 finding): the ScrollView started with Orientation Neither and could never scroll (GatePage.xaml.cs:182/133): applied, it is always vertical.
  - `s234b-review-check2-medium.md` (2 findings, no blocker, both low):
    - 1 `GateFixedBelow` is about 12 dp short of the real fixed height (TabletCardLayout.cs:75): skipped (low).
    - 2 reparenting the ring and Back mid-hold on a card/non-card switch (GatePage.xaml.cs:143-165): skipped (low).
- **Device (RTST_Tablet, build 6edc9b530403, `hide_error_dialogs` on):** dino-tablet-upright-cards-gate, -firstrun, -corner and -intro PASS (`D:/ReadTheStupidText-worktrees/reviews/s234b-run2.log`). The first run was lost to a System UI ANR dialog (`s234b-run1.log`).

#### Not yet verified (part B)

- Cards, corner and Settings against frames 136-141 by eye (the flows take no screenshots).
- dino-tablet-upright-shelf and dino-landscape-reading-tablet regression not rerun after part B.
- A drifting finger on the gate hold.
- A short tablet window (cards cap and scroll).
- iPad (compile only).
- TalkBack order.

#### For tools/CLAUDE.md (lift later)

- Tablet AVD runs set `settings put global hide_error_dialogs 1` and send CLOSE_SYSTEM_DIALOGS before the flows, and set it back after. A System UI ANR on RTST_Tablet lost two runs on 2026-11-02 (Slice 234 part B: docs/plan/slices/234-dino-tablet-portrait.md).

#### Not yet verified

- Stacked reading page against frames 134, 135 and 150 by eye (no screenshot taken).
- Shelf spacing and header against frame 133.
- dino-landscape-store is phone-only: mark it as such or give it a tablet variant (tooling follow-up).
- iPad (compile only).
- TalkBack order.
- Picture only, upright.
