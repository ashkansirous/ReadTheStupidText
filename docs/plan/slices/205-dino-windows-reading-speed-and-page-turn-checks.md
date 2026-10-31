### Slice 205 — Dino Windows head: the Reading speed row and the page-turn VM check

*Built 2026-10-31, merged as PR #645 (6a997ce); VM and Android emulator checked (see Not yet verified).* Planned 2026-10-30. Follow-up to Slice 188 (found on the Windows VM, 2026-10-30).

**Touches:** `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/ReadingPage.xaml.cs` (only the StoryText automation name, if that is the cause), `tools/desktop-vm/checks/dino-settings.ps1`, `tools/desktop-vm/checks/dino-page-turn.ps1`, `docs/plan/slices/205-dino-windows-reading-speed-and-page-turn-checks.md`, `src/DinoReader.App/PageRendering.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/TextPages.cs`

#### Why

- On the Dino Windows head, the Settings "Reading speed" row did not open its page from UI Automation in 6 VM runs. The row is a Grid with only a tap gesture, so it has no Invoke. Neither a click on its label nor a click on the row's centre opened it. Screen-reader and keyboard users on Windows may not be able to open it either.
- The existing `dino-page-turn` VM check failed in every run on 2026-10-30 ("no element with AutomationId StoryText has a name"), so the Dino VM suite is red.

#### Work

- [x] Find out why the Reading speed row doesn't open by click or UIA on Windows. Make the row a real button for accessibility: Invoke support, keyboard activation, and a name that includes the current value. Then add the Reading speed open-and-Back step back into `dino-settings`.
- [x] Fix `dino-page-turn`: either the reading page exposes the story text under `StoryText` again (if a recent change dropped it), or the check reads what the page really exposes. Show it failing first, then passing.
- [ ] Android and iOS: the row change is shared code. Check that TalkBack and VoiceOver still read the row and that it opens. (Unticked 2026-10-31 by the feedback loop: the emulator ran three flows that do not touch this row; see Not yet verified.)

#### Verification

- VM: all Dino checks green, 3 runs in a row. Android emulator: the Reading speed row opens with a tap and with TalkBack.

*Changed while building:* the Reading speed row now opens its page and returns on the Windows head; `SettingsRows.cs` also got a row-width fix (the row was not filling the window); `dino-page-turn` now chooses "Picture + text" first; `TextPages.cs` got a small change (added to Touches); `PictureTextPages.cs` and `dino-settings.ps1` changed too.

*From review:* high (reviews/s205-high.md) and medium (reviews/s205-medium.md) passes done. Corrected 2026-10-31 by the feedback loop (checked finding by finding against origin/main; the earlier line said "fixes applied"): high 7 of 10 applied (1, 2, 3, 6, 7, 8, 10), 5 in part, 4 and 9 open; medium 2 of 8 applied (2, 5). Still open on main, now Slice 226: no Android or iOS tap or TalkBack check of the row (high 9; the emulator flows did not touch it); all row children hidden from the accessibility tree, so an interactive `end` view is unreachable and text selectors miss the row title (high 4, medium 1 and 8, `SettingsRows.cs:122-125`); NavRow's accessible name is set once and goes stale (high 5 in part, medium 3, `SettingsRows.cs:110-111`); public Row()/NavRow() columns shifted by `FirstColumn` (medium 4); `PictureTextPages.cs` sets the raw page text as the name, only in the draw path (medium 6); the per-line description at `TextPages.cs:85` runs on every platform and may make TalkBack and VoiceOver read each line twice (medium 7). Medium 2 (button chrome) and 5 (page-turn check cap, now 120 taps) were checked as applied (`review-status-205-214.md` in the night folder; its own "applied" for high 9 was wrong and is corrected here).

*Verified 2026-10-31:* VM (headless Hyper-V), all 4 Dino checks passed 3 runs in a row before the row-width fix; after it, one run: dino-page-turn, dino-settings (row 996 wide in a 1040 window), dino-launch, dino-open-book all PASS. Android emulator: grown-ups-button, store-credits-language, default-view-per-book flows PASS on the new build.

#### For CLAUDE.md (lifted 2026-10-31)

None.

#### Not yet verified

- Android Settings row width vs screen: the uiautomator dump came back empty after a permission dialog, and no Maestro flow targets the Reading speed row.
- Android and iOS: the Reading speed row (and every Settings row) opens with a tap and with TalkBack / VoiceOver; never run (the emulator flows did not touch the row). The "Verification" line above was the plan, not a result.
- TalkBack and VoiceOver may read each text line twice (the per-line description, medium 7): not checked; fix in Slice 226.
