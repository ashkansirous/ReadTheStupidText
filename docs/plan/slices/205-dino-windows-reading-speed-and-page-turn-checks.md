### Slice 205 — Dino Windows head: the Reading speed row and the page-turn VM check

*Planned 2026-10-30.* Follow-up to Slice 188 (found on the Windows VM, 2026-10-30).

**Touches:** `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/ReadingPage.xaml.cs` (only the StoryText automation name, if that is the cause), `tools/desktop-vm/checks/dino-settings.ps1`, `tools/desktop-vm/checks/dino-page-turn.ps1`, `docs/plan/slices/205-dino-windows-reading-speed-and-page-turn-checks.md`.

#### Why

- On the Dino Windows head, the Settings "Reading speed" row did not open its page from UI Automation in 6 VM runs. The row is a Grid with only a tap gesture, so it has no Invoke. Neither a click on its label nor a click on the row's centre opened it. Screen-reader and keyboard users on Windows may not be able to open it either.
- The existing `dino-page-turn` VM check failed in every run on 2026-10-30 ("no element with AutomationId StoryText has a name"), so the Dino VM suite is red.

#### Work

- [ ] Find out why the Reading speed row doesn't open by click or UIA on Windows. Make the row a real button for accessibility: Invoke support, keyboard activation, and a name that includes the current value. Then add the Reading speed open-and-Back step back into `dino-settings`.
- [ ] Fix `dino-page-turn`: either the reading page exposes the story text under `StoryText` again (if a recent change dropped it), or the check reads what the page really exposes. Show it failing first, then passing.
- [ ] Android and iOS: the row change is shared code. Check that TalkBack and VoiceOver still read the row and that it opens.

#### Verification

- VM: all Dino checks green, 3 runs in a row. Android emulator: the Reading speed row opens with a tap and with TalkBack.
