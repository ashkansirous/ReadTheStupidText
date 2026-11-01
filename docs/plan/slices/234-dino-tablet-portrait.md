### Slice 234 — Dino: a proper design for tablets held upright

*Planned 2026-11-01.* The user asked for "better design on tablet". **Needs design by Claude Code** (an HTML mock, approved by the user, before any code). Start after Slice 232 merges: it reuses 232's phone-vs-tablet rules (`IsLandscape`, the 600 dp short-side split, `ShelfLayout`, `SplitLayout`).

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

**User answers (2026-11-01):** skip this slice tonight. It is parked until the user sees and approves its mock (Batch 45); status `parked: mock not drawn yet; waits for the user (skipped 2026-11-01)`.

#### Work

- [ ] **Design (Claude Code):** tablet-portrait mock (e.g. `Dino Reader Batch 45 Tablet.dc.html` in `docs/design/dino-reader/`), 804 × 1164 dp and a 10" Android size, for every screen above, on top of Batches 43 and 44. Rules as pure functions in Core where they can be. Publish as a private page; record the user's answer here.
- [ ] Core layout rules extended for tablet portrait, with unit tests (shelf columns, reading layout shape and text size, card widths).
- [ ] Each screen built from the rules; rotation keeps state.
- [ ] Android and iOS built and checked together.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (layout rules).
- Scripted flow on a tablet AVD in portrait, every screen, screenshot each: no clipped or tiny controls, shelf shows 3-4 columns. iOS: iPad simulator or TestFlight on an iPad.
