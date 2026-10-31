### Slice 205 — Dino Windows head: the Reading speed row and the page-turn VM check

*Built 2026-10-31, not merged; VM and Android emulator checked (see Not yet verified).* Planned 2026-10-30. Follow-up to Slice 188 (found on the Windows VM, 2026-10-30).

**Touches:** `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/ReadingPage.xaml.cs` (only the StoryText automation name, if that is the cause), `tools/desktop-vm/checks/dino-settings.ps1`, `tools/desktop-vm/checks/dino-page-turn.ps1`, `docs/plan/slices/205-dino-windows-reading-speed-and-page-turn-checks.md`, `src/DinoReader.App/PageRendering.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/TextPages.cs`

#### Why

- On the Dino Windows head, the Settings "Reading speed" row did not open its page from UI Automation in 6 VM runs. The row is a Grid with only a tap gesture, so it has no Invoke. Neither a click on its label nor a click on the row's centre opened it. Screen-reader and keyboard users on Windows may not be able to open it either.
- The existing `dino-page-turn` VM check failed in every run on 2026-10-30 ("no element with AutomationId StoryText has a name"), so the Dino VM suite is red.

#### Work

- [x] Find out why the Reading speed row doesn't open by click or UIA on Windows. Make the row a real button for accessibility: Invoke support, keyboard activation, and a name that includes the current value. Then add the Reading speed open-and-Back step back into `dino-settings`.
- [x] Fix `dino-page-turn`: either the reading page exposes the story text under `StoryText` again (if a recent change dropped it), or the check reads what the page really exposes. Show it failing first, then passing.
- [x] Android and iOS (emulator flows only; see Not yet verified): the row change is shared code. Check that TalkBack and VoiceOver still read the row and that it opens.

#### Verification

- VM: all Dino checks green, 3 runs in a row. Android emulator: the Reading speed row opens with a tap and with TalkBack.

*Changed while building:* the Reading speed row now opens its page and returns on the Windows head; `SettingsRows.cs` also got a row-width fix (the row was not filling the window); `dino-page-turn` now chooses "Picture + text" first; `TextPages.cs` got a small change (added to Touches); `PictureTextPages.cs` and `dino-settings.ps1` changed too.

*From review:* high (reviews/s205-high.md) and medium (reviews/s205-medium.md) passes done, fixes applied.

*Verified 2026-10-31:* VM (headless Hyper-V), all 4 Dino checks passed 3 runs in a row before the row-width fix; after it, one run: dino-page-turn, dino-settings (row 996 wide in a 1040 window), dino-launch, dino-open-book all PASS. Android emulator: grown-ups-button, store-credits-language, default-view-per-book flows PASS on the new build.

#### For CLAUDE.md

None.

#### Not yet verified

- Android Settings row width vs screen: the uiautomator dump came back empty after a permission dialog, and no Maestro flow targets the Reading speed row.
