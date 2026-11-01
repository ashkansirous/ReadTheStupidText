### Slice 229 — Dino: a Done/Continue button, top right, on every grown-up page

*Built (wave 2026-11-01); merged as PR #683 (1c4bd76).* Planned 2026-11-01. From the user's user test on 2026-11-01 (finding 3). **Design done: the user approved the Batch 43 mock (see User answers)** (corrected by the 2026-11-01 feedback loop; it said "Needs design by Claude Code").

**Touches:** `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/PageEditorPage.xaml`, `src/DinoReader.App/PageEditorPage.xaml.cs`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/BackupPage.xaml`, `src/DinoReader.App/BackupPage.xaml.cs`, `src/DinoReader.App/ReadingSpeedPage.cs`, `src/DinoReader.App/*Page*.cs` (the other Settings sub-pages: language, narrator, animal sounds, about), `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.App/GrownUpHeader.cs`, `src/DinoReader.App/*Page*.xaml` (sub-page XAML: LanguagePage, NarratorLanguagesPage, StoryLanguagesPage, UpdatePage, PageTextPage; widened at claim), `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/App.xaml.cs`, `docs/design/dino-reader/**`, `.maestro/dino/settings-done-button.yaml`, `docs/plan/slices/229-dino-subpage-done-button.md`

#### Why

Testers opened the Store from the intro wizard, added books, then did not know how to go on. Every grown-up page is pushed with `Navigation.PushAsync` and only Back leaves it; the wizard moves on only when the page pops (`IntroWizardPage.cs` ~151, `_launchedStep` then `AdvanceAsync` in `OnAppearing`).

#### Decisions locked with the user (2026-11-01, RefineScope)

- **Every grown-up page** (every `IGrownUpPage`: Store, Record, Cast, Add/Edit, Settings and each Settings sub-page) gets one main button **at the top, on the right** of the page header (user's change to the proposal).
- It reads **"Continue"** when the page was opened from the intro wizard (and takes the wizard to its next step) and **"Done"** otherwise (back to where the page was opened from). Back keeps working as today.
- On pages with unsaved edits (page editor, cast), Done saves first, the same as Back does today, not a separate Save button. Check each page's Back behaviour while building; if one discards on Back, Done saves there and the slice notes it.
- Android and iOS together; the Windows head gets the same button.

**User answers (2026-11-01):** the user approved the Batch 43 mock (frames 104-108). Build to it. iOS gets a compile check in the night run; the VoiceOver / TestFlight / iPhone / iPad hand checks go to this slice's *Not yet verified* for the user to do later.

#### Design (Dino Reader Batch 43 mock, frames 104-109, added 2026-11-01)

Six frames in `docs/design/dino-reader/Dino Reader Batch 43 Grown-ups Corner.dc.html` (file lines 227-329; portrait phone, 360 x 760 dp). The mock agrees with the decisions above, with one placement detail (Done on a sub-page) and one question (right to left). The mock is a separate file from Slice 228's, not "the Slice 228 mock file".

- **Button (all frames).** Top right of the header, 44 dp tall, radius 22, fill `#5B57E8` (brand indigo, not coral: coral stays the child-side colour), white 15 px/600. "Done": padding 0 20, text only (105, 106). "Continue": padding 0 12 0 18 plus a chevron after the word, gap 4 (104, 108). The label is set by whoever pushed the page (`launchedFromIntro`), never guessed.
- **Header row (104-106, 108).** Status bar 28 dp, then padding 8 12 10 6: a 44 x 44 back button, title 20 px/600 with an optional 12 px `#8a8399` sub-line ("Picked and cast for Dino" on Store, "The Three Little Pigs · page 5 of 16" on Record), button at the right. One shared control, `GrownUpHeader` (back, title, optional sub-line, Done/Continue), replaces each page's own header row.
- **Store from the intro (104).** Continue pops the page and moves the wizard on, as Back does; a download in progress keeps going in the background.
- **Settings (105).** Done goes back to where Settings was opened from (the corner). On a sub-page (Reading speed, Narrator, ...) Done goes up one level to Settings, same as Back, so Done never skips pages.
- **Deeper pages (108).** From the intro's Add step, deeper pages (Scan, page editor) show Done; only the page the wizard pushed shows Continue. Continue always means "back to the intro, next step".
- **Record (106).** Done top right; a recording in progress is stopped and kept first, like Back. Continue when opened from the intro's record step.
- **Never mirrored (107 is not built).** User, 2026-11-01: "never mirror, never! not even in farsi. not even in the header." Done stays top right and Back top left in every language, Farsi and Arabic too (Slice 96). Only the words themselves read in their own direction.
- **Dark (109): out of scope** (user, 2026-11-01). Not built.
- **Strings (all locales).** "Done", "Continue" (check which exist). Built as `Common_Continue` in all 15 resx files.

**Decided: never mirror** (user, 2026-11-01). The earlier plan line "Done on the left in Farsi and Arabic" was my mistake, written without checking Slice 96.

**Decided: light only** (user, 2026-11-01).

**Decided: the Add/Edit wizard hides the header Done/Continue pill on the Pages and Cover steps** (chosen by the wave coordinator 2026-11-01). Nothing is saved there yet, so a pill that left would drop the parent's pictures, which is a bug. Back keeps today's behaviour. On the Text step the pill saves, like the bottom Save.

**Decided: Back on the page-text editor (PageTextPage) keeps today's discard-on-back; only Done saves** (chosen by the wave coordinator 2026-11-01). Header Back, system Back and iOS swipe-back all leave without saving, as before this slice; the old Save button became the header Done. Reasons: save-on-back deleted a page silently when the text was blank, and discard-on-back is the pre-slice behaviour, kept until the user decides. The wizard's last-step header Back (leave and clean up the draft) was already like that on main (`OnLeaveTapped` -> `LeaveAsync`), so it is unchanged.

#### Waiting on the user

- PageText Back: keep discard-on-back (today) or save-on-back?

Settled by the mock: Done is a filled indigo pill; Done on a sub-page goes up one level (the slice only said "back to where the page was opened from"). In landscape Settings (Slice 232, frame 118) Done closes Settings as a whole.

#### Work

- [x] **Design (Claude Code):** mock done (Batch 43, 2026-11-01: `docs/design/dino-reader/Dino Reader Batch 43 Grown-ups Corner.dc.html`, frames 104-106 and 108; 107 right-to-left and 109 dark are not built). User answers recorded above.
- [x] One shared header control (or helper) with the button, so each page adds it in one line; the label comes from whether the wizard launched the page.
- [x] Add it to every `IGrownUpPage` and every Settings sub-page.
- [x] Wizard: Continue pops the page and advances, same path as Back today.
- [x] Strings: `Common_Continue` added to all 15 `AppResources*.resx` (values copied from `FirstRun_Continue`).
- [ ] Android and iOS built and checked together (Android emulator checked; iOS hand checks and Windows head not done, see Not yet verified).

*Changed while building:*

- New `src/DinoReader.App/GrownUpHeader.cs`: back, title (optional sub-line, one line with ellipsis), indigo Done/Continue pill on the right. Light only, never mirrored. A transparent `Button` sits over the pill for screen readers. The Done `AutomationId` is set once, in the constructor (`SettingsSubPage`: `GrownUpDone<PageType>`).
- On it: Settings and its sub-pages, Store, Language, Backup, page editor, Add/Edit wizard, Record, Cast, page text. Done pops one level; Continue (with chevron) when launched from the intro (Store, Record, Add).
- Strings: 15 resx files carry `Common_Continue`, not 13.
- Add/Edit wizard: pill hidden on the Pages and Cover steps, shown on Text where it saves (coordinator decision, above).
- PageText: Back keeps discard-on-back, Done saves. A save-on-back version was built and reverted because it deleted a page silently on blank text. The Waiting on the user line stays open.
- Emulator found a crash opening Settings (the Done `AutomationId` was set twice); fixed by setting it once in the constructor.
- New flow `.maestro/dino/settings-done-button.yaml`.

*From review:* read from the five findings files in `D:/ReadTheStupidText-worktrees/reviews/` (outside the repo); applied/skipped is the coordinator's decision.

`s229-high.md` (9 findings):
1. Skipped: the pill is hidden on the Add/Edit wizard's Pages and Cover steps (finding: intro Add step has no Continue on two of three steps). A decision recorded above; the reviewer's note says to triage it as accepted.
2. Applied, later reverted: PageText Back next to a Done that saves, with no warning that Back discards. The save-on-back fix was reverted (it deleted a page on blank text); Back discards as on main, only Done saves.
3. Skipped (low): `DoneAsync` is a one-line wrapper over `SaveAsync` in `StoryWizardPage.xaml.cs`.
4. Applied: the pill was a Border with a tap gesture and no button role; a transparent `Button` now sits over it.
5. Applied: title had no line limit and wrapped next to a wide Continue pill; now one line with ellipsis.
6. Applied: `GrownUpDone` AutomationId was shared by stacked pages; `SettingsSubPage` ids are now `GrownUpDone<PageType>`.
7. Skipped (low): `SettingsSubPage` builds a throwaway `GrownUpHeader` that the real one replaces.
8. Skipped (low): `LaunchedFromIntro` is copy-pasted, and set-only on Store and the wizard.
9. Skipped (low): the Farsi/Arabic sub-line is not given its own script direction or font.

`s229-medium.md` (7 findings):
1. Applied, later reverted: header Back saved while system Back discarded. Superseded by the revert above.
2. Skipped (low): `DoneAsync` wrapper again (same as high 3).
3. Applied: `ButtonVisible` was true until the wizard session existed, so Done could throw a NullReferenceException at `_session!.Cleanup()`.
4. Skipped (low): shared `GrownUpDone` id on stacked pages (the Settings sub-page ids were already fixed under high 6). Checked on the emulator.
5. Skipped (low): `BuildButton` rebuilds the label row on each Continue toggle; the Button text may be announced twice. Checked on the emulator.
6. Skipped (low): ad-hoc `LaunchedFromIntro` setters (same as high 8). Checked on the emulator.
7. Skipped (low): page editor header changed Grid to `GrownUpHeader` and the pill height 38 to 44; crop-stage layout not verified (listed in Not yet verified). Checked on the emulator.

`s229-medium-check.md` (7 findings):
1. Resolved by the Back revert: Back saving deleted a blank text page without confirmation.
2. Resolved by the Back revert: `OnBackButtonPressed` fire-and-forget save, and iOS swipe-back skipping it.
3. Not changed, pre-existing on main: on the wizard's last step header Back discards the draft next to a saving Done (`OnLeaveTapped` -> `LeaveAsync`).
4. Skipped (low): `LaunchedFromIntro` set-only / read once.
5. Skipped (low): `DoneAsync` wrapper.
6. Skipped (low): throwaway `_header` in `SettingsPage`; Done equals Back on sub-pages.
7. Skipped (low): `RecordPage` Done and Back are identical `PopAsync`; the finding asks to verify that an in-progress recording is stopped on leave.

`s229-medium-check2.md` (6 findings, all low, none applied):
1. Garbled comment in `PageTextPage.cs` after the revert.
2. Label plus transparent Button may be announced twice; the Button inset may shrink the tap area.
3. Title forced to one line with ellipsis on all pages; long German/Finnish titles are cut at large font scale.
4. Page editor header about 6 dp taller than its layout was built for (unverified on device).
5. `BuildButton` relies on `Children.Count > 1`.
6. Per-page Done id applies only to `SettingsSubPage`; Store, Cast, Record and others share `GrownUpDone`.

`s229-medium-check3.md`: no findings (Done AutomationId set once in the constructor; matches the Maestro ids).

*Device (Android emulator):* settings-done-button and grown-ups-button PASS on rerun after the crash fix; corner-read-now, record-page-shows-words and store-credits-language PASS earlier. Sub-page header screenshot, nothing clipped: `D:/ReadTheStupidText-worktrees/reviews/s229-subpage.png`.

#### Verification

- Scripted emulator flow: intro wizard, Get more books, Store shows Continue top right, tap it, wizard is on the next step; from the grown-ups' corner, Settings shows Done, tap it, back on the corner. iOS: same flow on simulator or TestFlight.
- Arabic or Farsi: Done is still top right; nothing in the header moves.

#### For CLAUDE.md (lifted 2026-11-01)

Place in `src/DinoReader.App/CLAUDE.md` (it already has uncommitted edits in this branch; the index pass checks they match).

- Every grown-up page uses `GrownUpHeader.cs` (back, title, Done/Continue pill); never build a page's own header row. Light only, never mirrored, in Farsi and Arabic too. Set the Done `AutomationId` once, in the constructor (setting it twice crashed Settings on open). (Slice 229: docs/plan/slices/229-dino-subpage-done-button.md)
- Done pops one level; Continue only when the intro wizard pushed the page. Back on `PageTextPage` discards and only Done saves; do not make Back save (it deleted a page on blank text). (Slice 229: docs/plan/slices/229-dino-subpage-done-button.md)

#### For README.md (lifted 2026-11-01, nothing to lift)

Nothing user-facing for the README.

#### Not yet verified

- Page editor header over the crop stage (44 dp pill vs 38).
- Intro-wizard Continue flow (no hook to start the intro at that step).
- Farsi/Arabic: Done stays top right.
- Windows head not built.
- iOS VoiceOver, iPhone and iPad hand checks.
