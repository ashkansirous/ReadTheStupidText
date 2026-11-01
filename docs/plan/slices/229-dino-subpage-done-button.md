### Slice 229 — Dino: a Done/Continue button, top right, on every grown-up page

*Planned 2026-11-01.* From the user's user test on 2026-11-01 (finding 3). **Needs design by Claude Code** (an HTML mock, approved by the user, before any code).

**Touches:** `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/PageEditorPage.xaml`, `src/DinoReader.App/PageEditorPage.xaml.cs`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/BackupPage.xaml`, `src/DinoReader.App/BackupPage.xaml.cs`, `src/DinoReader.App/ReadingSpeedPage.cs`, `src/DinoReader.App/*Page*.cs` (the other Settings sub-pages: language, narrator, animal sounds, about), `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.App/GrownUpHeader.cs`, `src/DinoReader.App/App.xaml.cs`, `docs/design/dino-reader/**`, `docs/plan/slices/229-dino-subpage-done-button.md`

#### Why

Testers opened the Store from the intro wizard, added books, then did not know how to go on. Every grown-up page is pushed with `Navigation.PushAsync` and only Back leaves it; the wizard moves on only when the page pops (`IntroWizardPage.cs` ~151, `_launchedStep` then `AdvanceAsync` in `OnAppearing`).

#### Decisions locked with the user (2026-11-01, RefineScope)

- **Every grown-up page** (every `IGrownUpPage`: Store, Record, Cast, Add/Edit, Settings and each Settings sub-page) gets one main button **at the top, on the right** of the page header (user's change to the proposal).
- It reads **"Continue"** when the page was opened from the intro wizard (and takes the wizard to its next step) and **"Done"** otherwise (back to where the page was opened from). Back keeps working as today.
- On pages with unsaved edits (page editor, cast), Done saves first, the same as Back does today, not a separate Save button. Check each page's Back behaviour while building; if one discards on Back, Done saves there and the slice notes it.
- Android and iOS together; the Windows head gets the same button.

#### Design (Dino Reader Batch 43 mock, frames 104-109, added 2026-11-01)

Six frames in `docs/design/dino-reader/Dino Reader Batch 43 Grown-ups Corner.dc.html` (file lines 227-329; portrait phone, 360 x 760 dp). The mock agrees with the decisions above, with one placement detail (Done on a sub-page) and one question (right to left). The mock is a separate file from Slice 228's, not "the Slice 228 mock file".

- **Button (all frames).** Top right of the header, 44 dp tall, radius 22, fill `#5B57E8` (brand indigo, not coral: coral stays the child-side colour), white 15 px/600. "Done": padding 0 20, text only (105, 106). "Continue": padding 0 12 0 18 plus a chevron after the word, gap 4 (104, 108). The label is set by whoever pushed the page (`launchedFromIntro`), never guessed.
- **Header row (104-106, 108).** Status bar 28 dp, then padding 8 12 10 6: a 44 x 44 back button, title 20 px/600 with an optional 12 px `#8a8399` sub-line ("Picked and cast for Dino" on Store, "The Three Little Pigs · page 5 of 16" on Record), button at the right. One shared control, `GrownUpHeader` (back, title, optional sub-line, Done/Continue), replaces each page's own header row.
- **Store from the intro (104).** Continue pops the page and moves the wizard on, as Back does; a download in progress keeps going in the background.
- **Settings (105).** Done goes back to where Settings was opened from (the corner). On a sub-page (Reading speed, Narrator, ...) Done goes up one level to Settings, same as Back, so Done never skips pages.
- **Deeper pages (108).** From the intro's Add step, deeper pages (Scan, page editor) show Done; only the page the wizard pushed shows Continue. Continue always means "back to the intro, next step".
- **Record (106).** Done top right; a recording in progress is stopped and kept first, like Back. Continue when opened from the intro's record step.
- **Right to left (107).** In Farsi and Arabic the header mirrors: back on the right pointing right, Done on the left; rows below do not mirror. Draft strings for a native read: title "تنظیمات", Done "تمام".
- **Dark (109).** Page `#1E1B2E`, Continue `#A9A2FF` on `#1E1B2E` (7:1), cards `#2A2640`, "On the shelf" green lifted to `#7FD48A` (the one colour that is not already a `Night*` token). Same theme-pin caveat as Slice 228.
- **Strings (both locales, 13 languages).** "Done", "Continue" (check which exist); Farsi/Arabic "تمام" is a draft.

**Open point: right to left.** Slice 229 says Done moves to the top left in Farsi and Arabic; Slice 96 says the layout never mirrors. The mock follows this slice (header only, 107) and asks the user. **Default:** mirror the header only, as drawn.

**Open point: dark.** As in Slice 228, 109 needs the theme pin lifted (`App.xaml.cs:30`). **Default:** keep the pin, light only.

Settled by the mock: Done is a filled indigo pill; Done on a sub-page goes up one level (the slice only said "back to where the page was opened from"). In landscape Settings (Slice 232, frame 118) Done closes Settings as a whole.

#### Work

- [x] **Design (Claude Code):** mock done (Batch 43, 2026-11-01: `docs/design/dino-reader/Dino Reader Batch 43 Grown-ups Corner.dc.html`, frames 104-109). Remaining: the user's approval and their answers to the right-to-left and dark questions. Original ask: in the Slice 228 mock file, the header with the top-right Done/Continue on Store, Record and Settings (light and dark, right-to-left for Arabic/Farsi: the button moves to the top left there).
- [ ] One shared header control (or helper) with the button, so each page adds it in one line; the label comes from whether the wizard launched the page.
- [ ] Add it to every `IGrownUpPage` and every Settings sub-page.
- [ ] Wizard: Continue pops the page and advances, same path as Back today.
- [ ] Strings in all 13 languages.
- [ ] Android and iOS built and checked together.

#### Verification

- Scripted emulator flow: intro wizard, Get more books, Store shows Continue top right, tap it, wizard is on the next step; from the grown-ups' corner, Settings shows Done, tap it, back on the corner. iOS: same flow on simulator or TestFlight.
- Arabic or Farsi: the button sits on the left.
