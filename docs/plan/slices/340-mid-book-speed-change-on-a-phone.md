# Slice 340: Listening page: Mid-book speed change on a phone

*Built 2026-11-09, merged as PR #985 (96a479d5); fix 1 (slowest speed 0.8x) was the only fix. Page: https://claude.ai/artifact/NWRNCx8GhfZneYoZ4Gt3NQ.* Clips: Dino Three Little Pigs page 1 at 1x, 0.6x and 1.2x, recorded with `scrcpy --audio-source=playback` over wireless adb. A mid-read speed change is no longer possible: reading stops when the page is left, so the page compares the three speeds instead.

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 150's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Live:** Slice 150: a mid-book speed change heard on a real phone; Windows head and iOS readers apply `SetSpeed` on the next sentence; native-speaker read ... [full text](notes/2026-10-30-outstanding-detail.md#o072) ([slice](slices/150-dino-slower-reading-speed.md))

**Touches:** `docs/plan/slices/340-mid-book-speed-change-on-a-phone.md`, `docs/plan/unverified.md`, `src/DinoReader.Core/ReadingSpeed.cs`, `src/DinoReader.App/ReadingSpeedPreference.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx` (`Speed_EndSlow`), `tests/DinoReader.Tests/ReadingSpeedTests.cs`, `tests/DinoReader.Tests/LocalizedUiSpeedLabelTests.cs`, `docs/plan/decisions.md`, `src/DinoReader.App/CLAUDE.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it: render it with the app's own engine and packs (the `voice-pick` skill's render path), or record it from the dev app on the S21 when rendering cannot reproduce it (speed changes, timing, a Sonos or store track). Note how each clip was made and its build or pack version.
- [x] Publish one private listening page through the repo's `voice-pick` agent: a play button per clip, the line of text it reads, and for each clip "sounds right" or "something is wrong" with a free-text box for what is wrong (and a timestamp when it helps). English and Farsi only.
- [x] When the user has answered, write every answer into this file, word for word (below).
- [ ] Each "something is wrong" becomes a fix item in this slice (cause, then fix, through the normal review gate). Every "sounds right" closes the check.
- [x] **Fix 1 (from the user's answer):** Dino's slowest reading speed becomes **0.8x**. Today `ReadingSpeed.Min` is 0.5 (`src/DinoReader.Core/ReadingSpeed.cs:11`), and the Reading speed slider (`SettingsRows.cs`, `Minimum = ReadingSpeed.Min`) goes down to it. Set the minimum to 0.8 so the slider and any stored or synced value clamp to 0.8 (`ReadingSpeed` already clamps), update the tests and any speed labels or presets below 0.8, on Android, iOS and the Windows head. Build after Slice 325 parts A-C merge (both touch the Settings rows).

## The user's answers (2026-11-09, Done pressed 00:01Z)

- **Speed 0.6x (slower): something is wrong:** "too slow. it should not be this slow. The slowest should be 0.8x"
- **Speed 1x: sounds right.**
- **Speed 1.2x (fastest allowed): sounds right.**

## From review

- Skipped #4 (`reviews/s340-high.md`): "a stored speed of 0.5-0.75 is only clamped on read, never re-saved" - clamp on read is enough, re-save not needed.
- Fix 1 is ticked for the code only (minimum, slider, `Speed_EndSlow` labels in 15 resx, tests, Decision 174); no device check has run.

## Not yet verified

- Fix 1 on a device: Settings > Reading speed slider runs 0.8x-1.2x with the left label "0.8x", on Android, iOS and the Windows head, and a book reads at 0.8x at the low end. Android phone emulator, 2026-11-09: `settings-reading-speed-row.yaml` passed and shows "0.8x · slow"; iOS, Windows and the 0.8x reading are still open.

**Found during the device run (not this slice's code):** `.maestro/dino/dino-landscape-settings-fa.yaml` fails at `assertVisible id: GrownUpDoneReadingSpeedPage`. That id no longer exists in `src/` since Slice 325 B removed the Settings Done buttons; `dino-landscape-settings-ar.yaml` and `dino-redesign-store-settings.yaml` reference it too. Likely stale flows from Slice 325; fix them with Slice 325 F (log: `D:\ReadTheStupidText-worktrees\reviews\s340-flows-run.log`).

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
