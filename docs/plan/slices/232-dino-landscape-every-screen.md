### Slice 232 — Dino: a proper landscape design for every screen

*Part B merged as PR #724 (e260a3ba) (see *Part B: built* below); parts C-E planned (the slice stays open).* *Part A merged as PR #695 (0ccee639); parts C-E planned (the slice stays open).* From the user's user test on 2026-11-01 (finding 8: "the landscape mode sucks"). **Design done: Batch 44 mock, user answers recorded below** (corrected by the 2026-11-01 feedback loop; it said "Needs design by Claude Code"). Start after Slices 228 and 229 merge, so the mock draws their new header, pill and buttons.

**Touches:** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.Core/GalleryLayout.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.Core/ReadingLayoutRules.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/RecordViews.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastViews.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/IntroWizardPage.Art.cs`, `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/BackupPage.xaml`, `src/DinoReader.App/PageEditorPage.xaml`, `tests/DinoReader.Tests/**Layout*`, `src/DinoReader.Core/*Layout*.cs`, `src/DinoReader.App/GrownUpHeader.cs`, `src/DinoReader.App/ReadingTheme.cs`, `src/DinoReader.App/*Page*.cs` (Settings sub-pages), `docs/design/dino-reader/**`, `docs/plan/slices/232-dino-landscape-every-screen.md`

**Touches, part A (this claim, 2026-11-01):** `src/DinoReader.Core/ReadingLayoutRules.cs`, `src/DinoReader.Core/LandscapeLayout.cs` (new), `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/ReadingTheme.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/TransportRow.cs` (both added mid-build: the picture crop and the control row live there), `tests/DinoReader.Tests/*Layout*Tests.cs`, `tools/emulator/rotate.ps1` (new), `.maestro/dino/dino-landscape-reading*.yaml` (new), `docs/plan/slices/232-dino-landscape-every-screen.md`

**Touches, part B (claim 2026-11-01, night 2026-11-01-2355):** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/GrownUpHeader.cs`, `src/DinoReader.App/GrownUpMode.cs` (only if the rail needs its state), `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.Core/GalleryLayout.cs`, `src/DinoReader.Core/BookSheetPlan.cs`, `src/DinoReader.Core/LandscapeLayout.cs`, `tests/DinoReader.Tests/LandscapeLayoutTests.cs`, `tests/DinoReader.Tests/BookSheetPlanTests.cs`, `tests/DinoReader.Tests/GalleryLayout*Tests.cs`, `src/DinoReader.Core/ScrollAnchorRules.cs` and `tests/DinoReader.Tests/ScrollAnchorRulesTests.cs` (new, added at the medium fix round: the rotation anchor's pure rules), `.maestro/dino/dino-landscape-shelf*.yaml` (new), `docs/plan/slices/232-dino-landscape-every-screen.md`. `tools/emulator/rotate.ps1` is reused, not edited; rotation in flows is Maestro `setOrientation` as in part A (rotate.ps1 is the adb fallback).

**Part B design note (coordinator, claim):** the corner has no page of its own: it is `GalleryPage` in grown-up mode (`GrownUpHeader.cs`, `GrownUpMode.cs`). Every size comes from one pure call per layout pass on the window size: `LandscapeLayout.ShelfLayout` (sideways row on phone landscape, grid otherwise), `LandscapeLayout.ListPaneWidth`/`PinnedButton` for the corner rail; the page never recomputes widths or the 600 dp split. Rotation changes only layout, never the shelf's state (grown-up mode, scroll target book, open sheet). Book sheet in landscape: one centred card 580 dp (cover left, actions right), the same on tablets; its size is a pure function in `BookSheetPlan` with a unit test.

#### Parts (coordinator, claim 2026-11-01)

A premise check found the slice is about 14 screens on phone and tablet, too big for one build. It is split by screen group, in the mock's own order (reading screen first). Each later part is claimed in a later wave and reuses part A's Core functions and rotation helper.

- **A (this claim):** the Core layout functions from the Design section (`IsLandscape`, phone vs tablet by a 600 dp short side, `ShelfLayout`, `SplitLayout`, the reading-controls rule), with unit tests, in a new `LandscapeLayout.cs` (or next to `ReadingLayoutRules`). Also the reading screen, frames 113, 114, 115, 127 (tablet) and 132 (Farsi), and rotation keeping the reading position.
  - **Rotation in scripted checks:** a new `tools/emulator/rotate.ps1 -Serial <s> -To landscape|portrait` (adb `settings put system accelerometer_rotation 0` + `user_rotation 1|0`). It needs no app test hook, so `src/DinoReader.App/Testing/**` stays untouched (Slice 225 edits it this wave).
  - **The flow:** a Maestro flow that rotates portrait, landscape, portrait on a reading page. If Maestro's own orientation command works, use it (check the Maestro docs through `library-docs` first).
- **B:** shelf (kids' 110, tablet 125), grown-ups' corner (111, 126) and book sheet (112).
- **C:** Record (116, 129), Cast (119) and page editor (120).
- **D:** Settings and its sub-pages (118, 128), Store (117) and Backup (124).
- **E:** intro wizard (121, 130), gate (122) and first run (123). Rotation must keep the wizard step and gate progress.

#### Why

Nothing locks orientation (Android `MainActivity` has no `ScreenOrientation`; `Info.plist` allows portrait and both landscapes, plus upside-down on iPad), but only the reading screen has landscape layouts (`ReadingPage.xaml.cs:486-548`, `ReadingLayoutRules`). The shelf, book sheet, Record, Store, Settings, Cast, wizard, gate and first run have none (checked by search for orientation states; the pages themselves not opened one by one).

#### Decisions locked with the user (2026-11-01, RefineScope)

- A full landscape design for **every Dino screen**: shelf (kids' and grown-ups' corner), book sheet, reading screen (redo the current one), Record, Store, Settings and its sub-pages, Cast, page editor, intro wizard, gate, first run, Backup. **Phone and tablet**, Android and iOS.
- **No mirroring and no dark** (user, 2026-11-01: "we don't mirror", "Dark is out of scope for now"). Every screen is drawn and built light, the same layout in every language.

**User answers (2026-11-01):** the user accepted the default: build the undrawn variants (sub-pages, gate hold step, first-run, tablet frames) from the rules, with no extra frames unless review finds a gap. iOS gets a compile check in the night run; the VoiceOver / TestFlight / iPhone / iPad hand checks go to this slice's *Not yet verified* for the user to do later.

#### Design (Dino Reader Batch 44 mock, frames 110-132, added 2026-11-01)

Twenty-three frames in `docs/design/dino-reader/Dino Reader Batch 44 Landscape.dc.html` (README index has the line of each). Phone frames are 760 x 360 dp (drawn 772 x 372 with the bezel), tablet frames 1164 x 804 dp. The mock agrees with the plan, but covers fewer variants than the plan asks (see open points). Frames are real screens; the rule box at the top of the file is a text summary, not UI. It is drawn on top of Batch 43, so the pill, corner rail, Done and Continue are in.

- **Four rules for every screen.** Split, don't stretch (picture or list left, working part right). The header becomes a side rail where a page has a pinned button. Reading controls move to a right-hand rail on phones. Nothing floats over a picture except the small back-and-view chip.
- **Pure functions for Core (unit-tested).** `IsLandscape(w,h)`: w > h; phone vs tablet by the short side, under 600 dp is a phone. `ShelfLayout`: phone landscape is one sideways row, cover height = h - 120; otherwise a grid, columns = clamp(floor(w / 190), 2, 6). `SplitLayout`: left pane = picture fitted to full height, at most 45% of w; list panes 300 dp (phone) / 360 dp (tablet). `ReadingControls`: right rail when h < 480 dp, else a row under the text; play 68 / 84, previous and next 52 / 64. `PinnedButton`: in landscape a pinned bottom button moves to the bottom of the side rail. Rotation keeps the reading position, a recording in progress, wizard step and gate progress (state in view models, not layouts).
- **Kids' shelf (110).** Phone: one row of covers 192 x 256 dp, scrolling sideways, last cover cut off to say "more"; header 44 dp with the pill top right; left inset clears the camera cut-out; titles one line, ellipsed. Tablet (125): downward grid, five columns at 1164 dp (four on a 10-inch Android, three in portrait), covers about 200 dp wide, header with 52 dp targets.
- **Reading (113, 114, 115; 113 and 115 replace Batch 37 frames 92 and 93).** Picture + text: picture fitted whole at full height (no crop), capped at 45% of the width and centred in its column; controls leave the art for a right rail; text card in the middle at 22 px; the back / view-switch / flag chip is the only thing on the picture, top left. Picture only: picture fitted and centred, the chip on its own white at the left margin, the rail at the right. Text only: the card gets the freed 74 dp row, one more line at 26 px. Tablet (127): height is at least 480 dp, so controls are a row under the text (64 / 84), the title returns above the card, text 30 px.
- **Farsi (132).** The layout does not mirror (Slice 96); only the words read in their own direction; flag joins the chip; vowel marks hidden (Slice 154). **Night (131) is not built** (dark out of scope); the reading screen's existing bedtime colours are left as they are today.
- **Intro (121, 130).** Every step: picture in a 300 dp panel on the left, step bar, title, copy and buttons on the right; the two cards side by side; Skip and the main button share one row at the bottom. Tablet: same split, text block capped at 460 dp, vertically centred, buttons at the foot; tablet portrait stacks the picture above the copy.
- **Gate (122).** Question left, answers right at 64 dp; the hold step puts the ring where the answers were. Rotating keeps the step and the sum.
- **First run (123).** Welcome left, the 14 languages in a three-column grid right (scrolls after two rows), Continue pinned under the grid; download and Ready pages keep the split.
- **Corner (111, 126).** The ink header becomes an ink side rail: title, gear, the line, Add and Store, the coral way-back button pinned at the foot; books scroll in four columns. Tablet: rail 340 dp, Add and Store as full rows; tablet portrait (width 744 dp or more) uses this rail too, only phones use the portrait bar.
- **Book sheet (112).** A centred card 580 dp (cover left, actions right); Read now first and biggest, Record, Cast and Remove share one row. Tablets get the same card. The Cast picker sheet shows as a centred card too.
- **Record (116, 129).** Phone: three columns, picture fitted whole, words with the recordings line, rail with Done on top and the mic in the middle; the header sits in the words column; the button becomes the stop square in place when recording. Tablet: two columns, page left, header / words / line / big button stacked, shown mid-recording ("Recording · 0:14").
- **Store (117).** Two columns of slimmer cards (52 dp covers), sub-line joins the title row, Done/Continue top right. Tablet: three columns.
- **Settings (118, 128).** Two panes: list left (selected row tinted), sub-page right with its own title and Done; tapping a row swaps the right pane (no push). Done closes Settings as a whole; Back on the left does the same. Portrait keeps push-per-page. First open shows the first row. Tablet: right pane capped at 640 dp; tablet portrait (744 dp or more) keeps two panes.
- **Cast (119), page editor (120), Backup (124).** Cast: rows in a two-column grid, each with its sample play button. Page editor: picture left, text field right with page arrows; with the keyboard up the picture collapses to a 64 dp thumbnail so the field keeps three lines; Done saves. Backup: two cards side by side (copy in Slice 230).
- No new strings beyond Slices 228-230 (Backup card copy is Slice 230's).

**Order: reading screen first** (113-115), as the mock suggests: today its controls sit on the picture and the picture is cut to 40%. Then the rest.

**Decided: as drawn** (user, 2026-11-01, "do as the design did"): sideways row on phones, downward grid on tablets.

**Decided: light only, never mirrored** (user, 2026-11-01). No further frames needed for either.

**Open point: coverage gaps.** Not drawn: each Settings sub-page on its own (Reading speed, Narrator, Language, Animal sounds, About are only the right pane of 118), the gate hold step and first-run download page (described, not drawn), tablet frames for gate, first run, Cast, page editor, Backup and book sheet (described as "same card"/"same split"), tablet portrait frames. **Default:** build those from the rules above; add frames only if the review finds a gap.

Settled by the mock: tablet versus phone is split at a 600 dp short side; the corner rail applies to tablet portrait from 744 dp; Settings in landscape closes as a whole with Done.

#### Work

- [x] **Design (Claude Code):** mock done (Batch 44, 2026-11-01: `docs/design/dino-reader/Dino Reader Batch 44 Landscape.dc.html`, frames 110-130 and 132; 131 night not built). User answers recorded above.
- [x] Part A: the reading screen (built 2026-11-01, see *Part A: built* below).
- [x] Part B: shelf, grown-ups' corner and book sheet (built 2026-11-02, see *Part B: built* below).
- [ ] Build the rest (parts C-E), each with its layout rule tests.
- [ ] Rotation keeps state (reading position, recording in progress, wizard step, gate progress).
- [ ] Android and iOS built and checked together.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (layout rules).
- Scripted emulator flow per screen, rotating portrait → landscape → portrait, screenshot each, no clipped or overlapping controls; one tablet AVD. iOS: simulator or TestFlight on iPhone and iPad.

#### Part A: built (2026-11-01)

- `src/DinoReader.Core/LandscapeLayout.cs`: `IsLandscape`, `SizeOf` (600 dp short side), `ShelfLayout`, `PicturePaneWidth` (fitted to height, max 45%), `ListPaneWidth` (300/360), `ReadingControls` (rail under 480 dp tall), `ControlSizesFor`, `PinnedButton`, `TextCardLeft`, `SidewaysGap`. Unit tests in `LandscapeLayoutTests`; 1904 Dino tests pass.
- Reading screen: sideways picture + text shows the picture whole (AspectFit, real pixel aspect via `PageImageSize`) at full height, capped at 45%; text card in the middle (22 px on a phone, 30 px when 480 dp tall or more). Phone controls sit in a right rail (68/52, gap 12; `TransportRow` gained a Rail mode). Tablet controls are an 84/64 row under the card, aligned by `LandscapeLayout.TextCardLeft` for the held page (aspect cached per picture). Picture-only is fitted and centred; the back/view/flag chip is top-left. One window-based decision drives sideways, rail/row and font (`ReadingSurface.SetControls`). Portrait unchanged. Rotation keeps the reading position (`ReadingSurface.Adopt`).
- `tools/emulator/rotate.ps1` (`-To landscape|portrait`, saves/restores `accelerometer_rotation` under `<worktrees>/.emulator`). Flow `.maestro/dino/dino-landscape-reading.yaml` (Maestro `setOrientation`; expects `landscape=True page=1` and the upright line).
- Touches widened mid-build: `PictureTextPages.cs`, `TransportRow.cs`.

Changed while building (deviations from the mock):

- Tablet frame 127's title above the card is not built (the title lives in the floating header).
- Phone side margins are about 14 dp, not 34/26.
- The picture-only chip sits in the margin left of the picture, not on it.
- Farsi frame 132 needed no code (no mirroring).
- The first emulator run's flow expected `page=2`/`true` (0-based page, C# `True`); the flow was fixed.

From review (findings files in `D:/ReadTheStupidText-worktrees/reviews/`):

- `s232-high.md`: applied 1-4 and 9. Skipped 5-8 (cleanups: dead `compact` branches, duplicate gap constant, unused `SidewaysHeight`, Rail/Compact flags).
- `s232-medium.md`: applied 1-5 (5 answered: `Transport.xaml` already sets End on main). Skipped 6 (needs a device; the device run shows it), 7 (cleanup), 8 (killed-run only).
- `s232-check-medium.md`: 1, 2, 6 and 7 replaced by the pure `TextCardLeft` function after two strikes on the same report path; 8 fixed by moving the temp file. Skipped 3, 4, 5.
- `s232-check2-medium.md`: applied 1. Skipped 2 (relayout reads the picture size each time; later cached by check3 1), 3 (stale size on rotation; self-corrects on the next layout pass), 4 (dead `SidewaysHeight` and duplicated sizes: cleanup), 5 (layout rules not yet used by any screen: they are for parts B-E), 6 (the 0x0-card guard's redraw: MAUI lays out again with the real size), 7 (`VerticalOptions` End on upright rows after the base-class change: portrait flows passed; see Not yet verified), 8 (a short split-screen tablet window gets the phone rail: the 480 dp rule is by design). Reasons recorded by the round 4 feedback loop from the wave's chat.
- `s232-check3-medium.md`: applied 1 (aspect cache). Skipped 2 (self-corrects on the next layout) and 3 (low).
- `s232-check4-medium.md`: no findings.

Emulator RTST_Test, 2026-11-01: `dino-landscape-reading` PASS, `open-book` PASS, `default-view-per-book` PASS. Screenshots: `D:/ReadTheStupidText-worktrees/reviews/s232-shots/final-picture-text.png` and `final-picture-only.png` (picture whole, right rail, text not clipped).

Parts B-E unchanged (planned).

#### Part B: built (2026-11-02)

- Core: `LandscapeLayout.CornerLayout` / `CoverWidth` / `CornerColumnsFor` (rail = `ListPaneWidth`, 300 dp phone / 360 dp tablet; 2-5 columns of at least 100 dp from the width left; the rail also applies on a tablet in portrait from 744 dp). `BookSheetPlan.ShapeFor` (580 dp centred card in landscape, bottom sheet upright). `ScrollAnchorRules` (rotation anchor is a book id; swap generation; pure, tested).
- Kids' shelf: a sideways row on phone landscape (cover height = window height - 120), a grid otherwise. The list rebinds its `ItemsSource` when the layout kind flips (likely cause of the blank shelf after rotating on Android: the list kept its old layout; fixed, covers drawn 2 s after rotating on the emulator rerun).
- Grown-ups' corner: ink rail with header, line, Add and Store, a scrolling middle, and the way-back button pinned at the foot. Book sheet: a card in landscape, Remove on its own last row. Portrait Add/Store tiles are unchanged from main (52 dp, truncation); the two-line wrap applies only in the rail.
- Flow `.maestro/dino/dino-landscape-shelf.yaml` (`setOrientation`; whole-token log markers; waits for a book title after each rotation).
- Checks: `dotnet test tests/DinoReader.Tests` 1965 pass; Android build; iOS compile; flow lint 0.
- Emulator (1080x2400, 420 dp, about 914x411 dp sideways), 2026-11-02: first run `dino-landscape-reading` PASS, `open-book` PASS; the shelf flow failed on a hard-coded `columns=3` (flow bug) and showed a blank shelf 4 s after rotating (app bug, fixed); corner rail, sheet card and portrait shots good; the same book and open sheet were kept across rotation. Rerun on build 6f6bdc3797c8: `dino-landscape-shelf` PASS. Screenshots: `D:/ReadTheStupidText-worktrees/reviews/s232b-shots/`.

Changed while building (deviations from the mock):

- The card keeps its 48 dp close button.
- Tablet-portrait rail 340 dp became 360 dp (`ListPaneWidth`), so the design note's 300/360 now matches the code (the first build used 270/340).
- A 10-inch tablet in portrait gets 4 kids' columns where the mock text says 3.
- Phone rail text sizes were read from the mock.

From review (findings files in `D:/ReadTheStupidText-worktrees/reviews/`):

- `s232b-high.md`: applied 1, 2, 4, 5, 8, 9. Skipped 3 (full re-arrange on every size change: cleanup), 6 (title height kept at two lines in the sideways row: cosmetic), 7 (inline layout numbers: cleanup), 10 (cut-out inset added on top of `SafeAreaEdges`: needs a notched device).
- `s232b-medium.md`: applied 1, 3, 5, and 4 through the book-id anchor design (`ScrollAnchorRules`) after a second strike on the same area. Skipped 2 (low: re-arranging the sheet on a size change resets its scroll).
- `s232b-check-medium.md`: all skipped. Anchor edge cases 1-3 (400 ms swap timeout, a drag swallowed as the swap's own, mode toggle keeping an anchor) are left to the device run after a third pass on the area. 4 (rotating with the sheet open rebuilds the rows and can drop an in-flight Remove confirmation) is low, a follow-up. 5-10 low.
- `s232b-check2-medium.md`: skipped 1 and 2 (device-only scroll timing; the rerun showed the covers draw).

Reviewed findings read: 10/10, 5/5, 10/10, 2/2.

#### For CLAUDE.md (lifted 2026-11-01)

- Landscape reading-screen sizes and the rail-versus-row rule come from `src/DinoReader.Core/LandscapeLayout.cs` (one window-based decision via `ReadingSurface.SetControls`); do not recompute pane widths or the 480 dp rule in the pages. (Slice 232: docs/plan/slices/232-dino-landscape-every-screen.md)

#### For CLAUDE.md, part B (lifted 2026-11-02)

- Shelf, corner and book-sheet sizes come from `LandscapeLayout` / `BookSheetPlan` in `src/DinoReader.Core`; `GalleryPage` computes no widths. (Slice 232: docs/plan/slices/232-dino-landscape-every-screen.md)
- A `CollectionView` whose `ItemsLayout` kind changes must rebind its `ItemsSource` (`src/DinoReader.App/CLAUDE.md`); otherwise Android keeps the old layout and shows a blank shelf after rotating. (Slice 232: docs/plan/slices/232-dino-landscape-every-screen.md)

#### Not yet verified

- Part B: the same book stays in view after rotating a shelf with more than 4 books (the emulator shelf has 4, all on screen).
- Part B: tablet landscape and portrait (no tablet AVD); iPhone and iPad (iOS compile only).
- Part B: notch inset doubling with `SafeAreaEdges` (high 10).
- Part B: a 360 dp-tall phone, the card and Remove fit.
- Part B: rotating with the sheet open during a Remove confirmation.
- Part B: VoiceOver / TalkBack order in the rail.
- Tablet landscape (no tablet AVD): the controls row under the card and its left edge.
- iPhone and iPad landscape (iOS compile only).
- VoiceOver / TalkBack order in landscape.
- Picture-only chip placement against frame 114 (the user's eye).
- A window 600-700 dp on the short side choosing rail versus row.
- Windows head in a wide window.
- Portrait reading unchanged on iOS, Windows and tablets after `TransportRow`'s base class changed to `StackLayout` (shown only by two Android emulator flows).
- Phone side margins (about 14 dp, mock 34/26) and tablet frame 127's title against the mock (the user's eye).
