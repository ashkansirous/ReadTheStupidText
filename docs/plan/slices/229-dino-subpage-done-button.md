### Slice 229 — Dino: a Done/Continue button, top right, on every grown-up page

*Planned 2026-11-01.* From the user's user test on 2026-11-01 (finding 3). **Needs design by Claude Code** (an HTML mock, approved by the user, before any code).

**Touches:** `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/PageEditorPage.xaml`, `src/DinoReader.App/PageEditorPage.xaml.cs`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/BackupPage.xaml`, `src/DinoReader.App/BackupPage.xaml.cs`, `src/DinoReader.App/ReadingSpeedPage.cs`, `src/DinoReader.App/*Page*.cs` (the other Settings sub-pages: language, narrator, animal sounds, about), `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `docs/design/dino-reader/**`, `docs/plan/slices/229-dino-subpage-done-button.md`

#### Why

Testers opened the Store from the intro wizard, added books, then did not know how to go on. Every grown-up page is pushed with `Navigation.PushAsync` and only Back leaves it; the wizard moves on only when the page pops (`IntroWizardPage.cs` ~151, `_launchedStep` then `AdvanceAsync` in `OnAppearing`).

#### Decisions locked with the user (2026-11-01, RefineScope)

- **Every grown-up page** (every `IGrownUpPage`: Store, Record, Cast, Add/Edit, Settings and each Settings sub-page) gets one main button **at the top, on the right** of the page header (user's change to the proposal).
- It reads **"Continue"** when the page was opened from the intro wizard (and takes the wizard to its next step) and **"Done"** otherwise (back to where the page was opened from). Back keeps working as today.
- On pages with unsaved edits (page editor, cast), Done saves first, the same as Back does today, not a separate Save button. Check each page's Back behaviour while building; if one discards on Back, Done saves there and the slice notes it.
- Android and iOS together; the Windows head gets the same button.

#### Work

- [ ] **Design (Claude Code):** in the Slice 228 mock file, the header with the top-right Done/Continue on Store, Record and Settings (light and dark, right-to-left for Arabic/Farsi: the button moves to the top left there).
- [ ] One shared header control (or helper) with the button, so each page adds it in one line; the label comes from whether the wizard launched the page.
- [ ] Add it to every `IGrownUpPage` and every Settings sub-page.
- [ ] Wizard: Continue pops the page and advances, same path as Back today.
- [ ] Strings in all 13 languages.
- [ ] Android and iOS built and checked together.

#### Verification

- Scripted emulator flow: intro wizard, Get more books, Store shows Continue top right, tap it, wizard is on the next step; from the grown-ups' corner, Settings shows Done, tap it, back on the corner. iOS: same flow on simulator or TestFlight.
- Arabic or Farsi: the button sits on the left.
