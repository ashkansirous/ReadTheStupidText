# Slice 330: Listening page: Store listening track: page gaps audible
**Product:** Dino

*Verified 2026-11-13 by measurement, no listen needed.* The user asked why they should listen to Three Little Pigs again (they had checked its lines on 2026-11-08 and 2026-11-09, Slices 334 and 335); a page-gap check can be measured, so it was. ffmpeg `silencedetect` (-40 dB) on the live tracks `listen/en?v=2` (290.05 s) and `listen/fa?v=3` (318.61 s), 16 pages each, yardstick `ListenTrackPlan.PageGapMs` 900 ms (Slice 281):
- English: 15 page gaps, min 0.95 s, median 1.01 s, max 1.07 s; longest silence inside a page 0.70 s (a sentence pause).
- Farsi: 15 page gaps, min 0.99 s, median 1.02 s, max 1.40 s (171.0-172.28 s, speech resumes right after); longest silence inside a page 0.62 s.
- No missing (< 0.3 s) or overlong (> 2.5 s) gap; speech resumes within 10 ms of every gap end; peaks +0.61 dB (en) and +0.29 dB (fa) from the AAC encode, no flat-topped samples. Check closed; the listening page https://claude.ai/artifact/NoAfafbFzyhrZRD9iQ5bS4 is not needed.

*Earlier: parked 2026-11-13 (index pass): needs the user's ear; headless night sessions have no Artifact tool to publish the listening page.*

*Park note (moved back to planned by the 2026-11-12-1337 feedback loop, user answer 2026-11-01): it was parked: needs the user's ear; run from an interactive session (headless night sessions have no Artifact tool to publish the listening page). A night wave that hits the same blocker parks it again with that reason.*

*Parked (night 2026-11-08-1703 wave 1): needs the user's ear; run from an interactive session (headless night sessions have no Artifact tool to publish the listening page).*

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 281's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Live:** Play one track end to end (page gaps audible).

**Touches:** `docs/plan/slices/330-store-listening-track-page-gaps-audible.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

*Struck by Slice 393 item 16 (2026-11-18): the four items and the Verification line below describe the listening page, which the measurement above replaced (the user asked why they should listen again). They are struck, not ticked, because no page was built and no answer was recorded. **Caveat:** the measuring agent used ffmpeg `silencedetect` at -40 dB, which finds a silence but cannot tell a clip (a page cut short) from a fast fade-out into the gap; the gap lengths and the "speech resumes within 10 ms" reading say nothing was cut, but a clipped page start or end is not proven absent. If a user ever reports a cut page, run the listening page then.*

- [~] ~~Make the audio exactly as the app plays it: render it with the app's own engine and packs (the `voice-pick` skill's render path), or record it from the dev app on the S21 when rendering cannot reproduce it (speed changes, timing, a Sonos or store track). Note how each clip was made and its build or pack version.~~ (replaced by the measurement of the live tracks)
- [~] ~~Publish one private listening page through the repo's `voice-pick` agent: a play button per clip, the line of text it reads, and for each clip "sounds right" or "something is wrong" with a free-text box for what is wrong (and a timestamp when it helps). English and Farsi only.~~ (not needed, see the measurement)
- [~] ~~When the user has answered, write every answer into this file, word for word.~~ (no page, no answers)
- [~] ~~Each "something is wrong" becomes a fix item in this slice (cause, then fix, through the normal review gate). Every "sounds right" closes the check.~~ (nothing was marked wrong)

## Verification

~~The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.~~ Closed by measurement (2026-11-13) with the caveat above.
