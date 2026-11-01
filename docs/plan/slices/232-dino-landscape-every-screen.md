### Slice 232 — Dino: a proper landscape design for every screen

*Planned 2026-11-01.* From the user's user test on 2026-11-01 (finding 8: "the landscape mode sucks"). **Design done: Batch 44 mock, user answers recorded below** (corrected by the 2026-11-01 feedback loop; it said "Needs design by Claude Code"). Start after Slices 228 and 229 merge, so the mock draws their new header, pill and buttons.

**Touches:** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.Core/GalleryLayout.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.Core/ReadingLayoutRules.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/RecordViews.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastViews.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/IntroWizardPage.Art.cs`, `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/BackupPage.xaml`, `src/DinoReader.App/PageEditorPage.xaml`, `tests/DinoReader.Tests/**Layout*`, `src/DinoReader.Core/*Layout*.cs`, `src/DinoReader.App/GrownUpHeader.cs`, `src/DinoReader.App/ReadingTheme.cs`, `src/DinoReader.App/*Page*.cs` (Settings sub-pages), `docs/design/dino-reader/**`, `docs/plan/slices/232-dino-landscape-every-screen.md`

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
- [ ] Build the reading screen first, then the rest, each with its layout rule tests.
- [ ] Rotation keeps state (reading position, recording in progress, wizard step, gate progress).
- [ ] Android and iOS built and checked together.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (layout rules).
- Scripted emulator flow per screen, rotating portrait → landscape → portrait, screenshot each, no clipped or overlapping controls; one tablet AVD. iOS: simulator or TestFlight on iPhone and iPad.
