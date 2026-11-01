### Slice 226 — Dino Settings rows and page text: Slice 205 review findings left open

*In progress (wave 2026-11-01), draft PR.* Written 2026-10-31 by the round 1 feedback loop ([note](../notes/2026-10-31-1301-feedback-loop-w1.md)) and parked; the user moved it to `planned` on 2026-11-01. Slice 205's record said "fixes applied", but a finding-by-finding check against origin/main (2026-10-31) found these still open.

**Touches:** `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/TextPages.cs`, `src/DinoReader.App/PictureTextPages.cs`, `.maestro/dino/` (a new Settings-row flow), `docs/plan/slices/226-dino-settings-rows-review-leftovers.md`

Shared MAUI code: every item is built and checked on Android and iOS alike (and the Windows head, where the Slice 205 fix came from).

#### Work

1. **Row children hidden from the accessibility tree** (Slice 205 high 4, medium 1 and 8; `SettingsRows.cs:122-125`): the full-row button hides every child, so an interactive `end` view (Switch, Picker) passed to `Row()` is unreachable, and text selectors (Maestro `text:`, uiautomator `text=`) no longer find the row title.
   - [ ] Hide only the labels the button's name already carries; leave interactive end views reachable; give the button the row title as text or a stable automation id. Verify: a Maestro flow taps the Reading speed row by its title and opens the page; a unit or UI test with a Switch end view still toggles it.
2. **NavRow's accessible name goes stale** (high 5 in part, medium 3; `SettingsRows.cs:110-111`): it is built once, so TalkBack, VoiceOver and UIA can announce an old value.
   - [ ] Refresh it when the value label changes, as `Row()` does. Verify: change a NavRow value in place and read the button's name (automation tree) afterwards.
3. **Column layout shifted for callers** (medium 4; `SettingsRows.cs:65`, `FirstColumn`): public `Row()`/`NavRow()` callers adding children by column index land one column off. No current caller does; keep it that way or document it.
   - [ ] Expose the content column through the helper (or a comment and a test) so a new caller cannot get it wrong. Verify: a test adds a child through the helper and finds it in the content column.
4. **Per-line description on every platform** (medium 7; `TextPages.cs:85`): a Windows UIA workaround builds a description for every label on every page on Android and iOS too; TalkBack and VoiceOver may read each line twice, and it allocates per line.
   - [ ] Limit it to the Windows head (or set it once in the label factory). Verify: on the Android emulator with TalkBack, a page's lines are read once (a scripted `uiautomator dump` shows no duplicate content description); iOS TestFlight hand check with VoiceOver.
5. **Picture-and-text page name** (medium 6; `PictureTextPages.cs:164-168`): the label's description is the raw page text slice (newlines and markers), set only in the draw path, so a recreated label without a redraw has no name.
   - [ ] Set a cleaned name where the label is created and update it on page change. Verify: a unit test of the name builder strips markers and newlines; the VM `dino-page-turn` check still passes.
6. **The Reading speed row on Android and iOS was never opened in a check** (Slice 205 high 9 and *Not yet verified*).
   - [ ] A `.maestro/dino` flow opens Settings, taps the Reading speed row and sees its page (item 1 makes it findable). Verify: the flow passes on the emulator.

#### Verification

- The verify line under each item.

#### Not yet verified

- Everything: planned only.

**User answers (2026-11-01):** iOS gets a compile check in the night run; the VoiceOver / TestFlight / iPhone / iPad hand checks go to this slice's *Not yet verified* for the user to do later.
