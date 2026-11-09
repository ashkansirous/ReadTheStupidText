# Slice 339: Listening page: Real phone audio timing (Slice 165)

*Dropped 2026-11-09.* The user asked "why are we testing the speed for rtst? whats the point of 339?". Slice 165's script (`tools/phone/audio-timing.ps1`) already measures Read The Stupid Text's timing on a real phone (time to first audio, gaps between chunks, total time at 1x and 2x) and flags a gap over its limit; only "does the voice sound right" was left for the ear, and the page did not ask that well. The listening page (https://claude.ai/artifact/R94Dnv6FB3JEuaSj3Lbmiq) is withdrawn unanswered. The measured 1x gaps (1.1-1.5 s between pieces on the S21) go to `docs/plan/unverified.md` as a Device check: run `audio-timing.ps1 -App rtst` on the S21 and compare against its own gap limit.

*Was parked: the listening page is published; waits for the user's answers.* Clips: RTST English Supertonic on the S21, recorded with `scrcpy --audio-source=playback` over wireless adb; the 1x gaps measured 1.1-1.5 s between pieces.

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 165's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Live:** Slice 165: the user's listening check on the phone (Slice 163 and the Slice 175/176 gaps are closed) ([slice](slices/165-real-phone-audio-timing-checks.md))

**Touches:** `docs/plan/slices/339-real-phone-audio-timing.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it: render it with the app's own engine and packs (the `voice-pick` skill's render path), or record it from the dev app on the S21 when rendering cannot reproduce it (speed changes, timing, a Sonos or store track). Note how each clip was made and its build or pack version.
- [x] Publish one private listening page through the repo's `voice-pick` agent: a play button per clip, the line of text it reads, and for each clip "sounds right" or "something is wrong" with a free-text box for what is wrong (and a timestamp when it helps). English and Farsi only.
- [ ] When the user has answered, write every answer into this file, word for word.
- [ ] Each "something is wrong" becomes a fix item in this slice (cause, then fix, through the normal review gate). Every "sounds right" closes the check.

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
