### Slice 226 — Dino Settings rows and page text: Slice 205 review findings left open

*Built 2026-11-01 (wave 7, night 2026-11-01-0859), PR #668, not merged; device-checked on the Android emulator and the desktop VM, not on TalkBack or iOS.* Written 2026-10-31 by the round 1 feedback loop ([note](../notes/2026-10-31-1301-feedback-loop-w1.md)) and parked; the user moved it to `planned` on 2026-11-01. Slice 205's record said "fixes applied", but a finding-by-finding check against origin/main (2026-10-31) found these still open.

**Touches:** `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/TextPages.cs`, `src/DinoReader.App/PictureTextPages.cs`, `.maestro/dino/` (a new Settings-row flow), `docs/plan/slices/226-dino-settings-rows-review-leftovers.md`

Shared MAUI code: every item is built and checked on Android and iOS alike (and the Windows head, where the Slice 205 fix came from).

#### Work

1. **Row children hidden from the accessibility tree** (Slice 205 high 4, medium 1 and 8; `SettingsRows.cs:122-125`): the full-row button hides every child, so an interactive `end` view (Switch, Picker) passed to `Row()` is unreachable, and text selectors (Maestro `text:`, uiautomator `text=`) no longer find the row title.
   - [x] Hide only the labels the button's name already carries; leave interactive end views reachable; give the button the row title as text or a stable automation id. Verify: a Maestro flow taps the Reading speed row by its title and opens the page; a unit or UI test with a Switch end view still toggles it.
   - Built: `AddRowButton` hides only Label/IconView/Border/BoxView children, so interactive end views stay reachable, and the button spans only the padding and title columns. The button carries an AutomationId (`NavRow` takes an optional `automationId`; Reading speed uses `SettingsReadingSpeedRow`) instead of title text, because title text drew twice on hover/press on Windows. The Switch-end toggle test was not written (no current caller).
2. **NavRow's accessible name goes stale** (high 5 in part, medium 3; `SettingsRows.cs:110-111`): it is built once, so TalkBack, VoiceOver and UIA can announce an old value.
   - [x] Refresh it when the value label changes, as `Row()` does. Verify: change a NavRow value in place and read the button's name (automation tree) afterwards.
3. **Column layout shifted for callers** (medium 4; `SettingsRows.cs:65`, `FirstColumn`): public `Row()`/`NavRow()` callers adding children by column index land one column off. No current caller does; keep it that way or document it.
   - [x] (no unit test: `DinoReader.Tests` cannot reference MAUI code and Core is outside Touches) Expose the content column through the helper (or a comment and a test) so a new caller cannot get it wrong. Verify: a test adds a child through the helper and finds it in the content column.
4. **Per-line description on every platform** (medium 7; `TextPages.cs:85`): a Windows UIA workaround builds a description for every label on every page on Android and iOS too; TalkBack and VoiceOver may read each line twice, and it allocates per line.
   - [x] Limit it to the Windows head (or set it once in the label factory). Verify: on the Android emulator with TalkBack, a page's lines are read once (a scripted `uiautomator dump` shows no duplicate content description); iOS TestFlight hand check with VoiceOver.
5. **Picture-and-text page name** (medium 6; `PictureTextPages.cs:164-168`): the label's description is the raw page text slice (newlines and markers), set only in the draw path, so a recreated label without a redraw has no name.
   - [x] (no unit test, same reason as item 3) Set a cleaned name where the label is created and update it on page change. Verify: a unit test of the name builder strips markers and newlines; the VM `dino-page-turn` check still passes.
6. **The Reading speed row on Android and iOS was never opened in a check** (Slice 205 high 9 and *Not yet verified*).
   - [x] A `.maestro/dino` flow opens Settings, taps the Reading speed row and sees its page (item 1 makes it findable). Verify: the flow passes on the emulator.

#### Verification

- The verify line under each item.

*Changed while building:*

- Item 1: the row button carries an AutomationId, not the title text (title text drew twice on hover/press on Windows).
- Item 2: `NavRow` refreshes the button's accessible name when its value label (or the tag's label) changes, through a helper shared with `Row()`.
- Item 3: public `SettingsRows.AddInRow(row, child, slot)`; slot 0 is the title column, an out-of-range slot throws. No unit test (`DinoReader.Tests` cannot reference MAUI; Core is outside Touches), so the verify line's test was not written.
- Item 4: the per-line description is set only on the Windows head (`DevicePlatform.WinUI`).
- Item 5: the page label is named where it is created and on draw, via `AccessibleName` (collapses newlines and whitespace; page text has no markers). No unit test, same reason as item 3.
- Item 6: new flow `.maestro/dino/settings-reading-speed-row.yaml` (taps by id; dismisses the optional voices note only when visible).

*From review* (`reviews/s226-high.md`, `reviews/s226-medium.md`):

- High: applied 1, 2, 3. Skipped 4 (page text has no markers), 5 (performance nit), 6 and 7 (no current caller), 8 (belongs to the device stage).
- Medium: applied 5; 1 checked, no caller puts a control in a Border. Skipped 2, 3, 4, 6, 7, 8, 9.

*Device results:* Android emulator flow passed (108.9 s). Settings dump: button `SettingsReadingSpeedRow`, content-desc "Reading speed: 0.8x". Reading page dump: text-only lines have no content-desc; a picture+text page label has content-desc equal to its words with newlines collapsed (on Android content-desc replaces the text for TalkBack, so it should read once; not heard with TalkBack). Desktop VM `Invoke-DesktopTest -App dino`: 4/4 pass including `dino-page-turn`; a throw-away assertion found no CR/LF in StoryText UIA names (not kept in the check).

#### Not yet verified

- TalkBack / VoiceOver hearing each reading page once.
- iPhone / iPad / VoiceOver / TestFlight hand checks (the user's answer: they do these later).
- A Switch-end row still toggling (no current caller).

#### For CLAUDE.md

- `src/DinoReader.App/CLAUDE.md`: a full-row settings button (`SettingsRows.cs`) hides only its Label/IconView/Border/BoxView children and carries an AutomationId, not title text (title text draws twice on Windows hover/press); add children to a row only through `SettingsRows.AddInRow(row, child, slot)` (slot 0 = title column). (Slice 226: docs/plan/slices/226-dino-settings-rows-review-leftovers.md)
- `src/DinoReader.App/CLAUDE.md`: the per-line UIA description in `TextPages.cs` is Windows-head only (`DevicePlatform.WinUI`); name a picture+text page label through `PictureTextPages.AccessibleName`. (Slice 226: docs/plan/slices/226-dino-settings-rows-review-leftovers.md)

**User answers (2026-11-01):** iOS gets a compile check in the night run; the VoiceOver / TestFlight / iPhone / iPad hand checks go to this slice's *Not yet verified* for the user to do later.
