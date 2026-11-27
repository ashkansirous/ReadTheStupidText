### Slice 407 — Dino: the premade-audio Farsi delivery fixture matches Good Enough's cast again
**Product:** Dino

*Plan-cleanup check 2026-11-25: keep open. All three own-work items are unbuilt. Live: `python -I -W error -m unittest discover -s tools/dino/premade-audio` at origin/main 1a8bf725 gives 1 failure of 128 (good-enough pins only Hazelnut; the cast has Carrot and more); fixture and test untouched since 45089bff. No waits, no outside events, no side items to move.*

*Planned.*

**Touches:** `tools/dino/premade-audio/testdata/farsi-delivery-styles.json`, `tools/dino/premade-audio/test_premade_audio.py`

Why: `python -I -W error -m unittest discover -s tools/dino/premade-audio` is red on `main`. The one failure is
`AccentTests.test_every_casts_farsi_delivery_is_byte_identical_to_the_one_recorded_before_the_accent_moved` (`test_premade_audio.py`
about line 345): for `good-enough` the fixture pins only Hazelnut, while the committed `content/dino/good-enough/audio-cast.json` has Carrot and the
rest of the dialogue cast. Found 2026-11-18 by the Love from Seagull Island review (review-high finding 6, confirmed red at merge base
ed815c95, before that PR). The cast changed in 25609140 "Good Enough as dialogue" and the fixture was not updated. While it stays red, the loop
stops at good-enough, so every book after it in the loop (Love from Seagull Island included) is never checked, and every audio PR's test line
carries a known failure. User, 2026-11-18: "add the 2 findings to the plan to fix".

- [ ] Read 25609140 and the Good Enough slice to confirm the dialogue cast is the intended one (not a stray edit).
- [ ] Pin Good Enough's current Farsi deliveries in `farsi-delivery-styles.json`, generated from the cast the way the other entries were (the README's A8 note:
      entries for books added after A8 are copies of the current cast). Don't change any audio clip.
- [ ] Make the test check every book even when one fails (collect the mismatches, fail once at the end with all of them), so one stale entry can't hide the others.

**Verification:** the premade-audio suite runs with no failure; a deliberate one-character change to two books' entries reports both books.
