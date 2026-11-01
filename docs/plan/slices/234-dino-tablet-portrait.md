### Slice 234 — Dino: a proper design for tablets held upright

*Planned 2026-11-01.* The user asked for "better design on tablet". **Mock drawn 2026-11-01 (Batch 45), added by the user in Claude Design, so it counts as approved**. Start after Slice 232 merges: it reuses 232's phone-vs-tablet rules (`IsLandscape`, the 600 dp short-side split, `ShelfLayout`, `SplitLayout`).

**Touches:** `src/DinoReader.Core/GalleryLayout.cs`, `src/DinoReader.Core/ReadingLayoutRules.cs`, `src/DinoReader.Core/*Layout*.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/RecordViews.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/BackupPage.xaml`, `src/DinoReader.App/PageEditorPage.xaml`, `src/DinoReader.App/GrownUpHeader.cs`, `tests/DinoReader.Tests/**Layout*`, `docs/design/dino-reader/**`, `docs/plan/slices/234-dino-tablet-portrait.md`

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
