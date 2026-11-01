### Slice 232 — Dino: a proper landscape design for every screen

*Planned 2026-11-01.* From the user's user test on 2026-11-01 (finding 8: "the landscape mode sucks"). **Needs design by Claude Code** (an HTML mock, approved by the user, before any code). Start after Slices 228 and 229 merge, so the mock draws their new header, pill and buttons.

**Touches:** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.Core/GalleryLayout.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.Core/ReadingLayoutRules.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/RecordViews.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastViews.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/IntroWizardPage.Art.cs`, `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/BackupPage.xaml`, `src/DinoReader.App/PageEditorPage.xaml`, `tests/DinoReader.Tests/**Layout*`, `docs/design/dino-reader/**`, `docs/plan/slices/232-dino-landscape-every-screen.md`

#### Why

Nothing locks orientation (Android `MainActivity` has no `ScreenOrientation`; `Info.plist` allows portrait and both landscapes, plus upside-down on iPad), but only the reading screen has landscape layouts (`ReadingPage.xaml.cs:486-548`, `ReadingLayoutRules`). The shelf, book sheet, Record, Store, Settings, Cast, wizard, gate and first run have none (checked by search for orientation states; the pages themselves not opened one by one).

#### Decisions locked with the user (2026-11-01, RefineScope)

- A full landscape design for **every Dino screen**: shelf (kids' and grown-ups' corner), book sheet, reading screen (redo the current one), Record, Store, Settings and its sub-pages, Cast, page editor, intro wizard, gate, first run, Backup. **Phone and tablet**, Android and iOS.
- Open: the user has not yet said which landscape screen hurt most; ask at the design review. That screen is built first.

#### Work

- [ ] **Design (Claude Code):** landscape mock for each screen above, phone and tablet sizes, light and dark, right-to-left. Layout rules written as pure functions in Core where they can be (like `ReadingLayoutRules`), so they are unit-tested. Publish as a private page; record the user's answers and their "worst screen" here.
- [ ] Build the worst screen first, then the rest, each with its layout rule tests.
- [ ] Rotation keeps state (reading position, recording in progress, wizard step, gate progress).
- [ ] Android and iOS built and checked together.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (layout rules).
- Scripted emulator flow per screen, rotating portrait → landscape → portrait, screenshot each, no clipped or overlapping controls; one tablet AVD. iOS: simulator or TestFlight on iPhone and iPad.
