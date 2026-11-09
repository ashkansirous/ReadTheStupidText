# Slice 340: Listening page: Mid-book speed change on a phone

*Planned: answered 2026-11-09; fix 1 (slowest speed 0.8x) to build after Slice 325 A-C. Page: https://claude.ai/artifact/NWRNCx8GhfZneYoZ4Gt3NQ.* Clips: Dino Three Little Pigs page 1 at 1x, 0.6x and 1.2x, recorded with `scrcpy --audio-source=playback` over wireless adb. A mid-read speed change is no longer possible: reading stops when the page is left, so the page compares the three speeds instead.

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 150's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Live:** Slice 150: a mid-book speed change heard on a real phone; Windows head and iOS readers apply `SetSpeed` on the next sentence; native-speaker read ... [full text](notes/2026-10-30-outstanding-detail.md#o072) ([slice](slices/150-dino-slower-reading-speed.md))

**Touches:** `docs/plan/slices/340-mid-book-speed-change-on-a-phone.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it: render it with the app's own engine and packs (the `voice-pick` skill's render path), or record it from the dev app on the S21 when rendering cannot reproduce it (speed changes, timing, a Sonos or store track). Note how each clip was made and its build or pack version.
- [x] Publish one private listening page through the repo's `voice-pick` agent: a play button per clip, the line of text it reads, and for each clip "sounds right" or "something is wrong" with a free-text box for what is wrong (and a timestamp when it helps). English and Farsi only.
- [x] When the user has answered, write every answer into this file, word for word (below).
- [ ] Each "something is wrong" becomes a fix item in this slice (cause, then fix, through the normal review gate). Every "sounds right" closes the check.
- [ ] **Fix 1 (from the user's answer):** Dino's slowest reading speed becomes **0.8x**. Today `ReadingSpeed.Min` is 0.5 (`src/DinoReader.Core/ReadingSpeed.cs:11`), and the Reading speed slider (`SettingsRows.cs`, `Minimum = ReadingSpeed.Min`) goes down to it. Set the minimum to 0.8 so the slider and any stored or synced value clamp to 0.8 (`ReadingSpeed` already clamps), update the tests and any speed labels or presets below 0.8, on Android, iOS and the Windows head. Build after Slice 325 parts A-C merge (both touch the Settings rows).

## The user's answers (2026-11-09, Done pressed 00:01Z)

- **Speed 0.6x (slower): something is wrong:** "too slow. it should not be this slow. The slowest should be 0.8x"
- **Speed 1x: sounds right.**
- **Speed 1.2x (fastest allowed): sounds right.**

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
