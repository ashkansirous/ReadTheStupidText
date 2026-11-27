### Slice 407 — Dino: the premade-audio Farsi delivery fixture matches Good Enough's cast again
**Product:** Dino

*Built 2026-11-27 (wave 3 of night 2026-11-27-0154); merged as PR #1403 (b3df8fe8).*

**Touches:** `tools/dino/premade-audio/testdata/farsi-delivery-styles.json`, `tools/dino/premade-audio/test_premade_audio.py`

Why: `python -I -W error -m unittest discover -s tools/dino/premade-audio` is red on `main`. The one failure is
`AccentTests.test_every_casts_farsi_delivery_is_byte_identical_to_the_one_recorded_before_the_accent_moved` (`test_premade_audio.py`
about line 345): for `good-enough` the fixture pins only Hazelnut, while the committed `content/dino/good-enough/audio-cast.json` has Carrot and the
rest of the dialogue cast. Found 2026-11-18 by the Love from Seagull Island review (review-high finding 6, confirmed red at merge base
ed815c95, before that PR). The cast changed in 25609140 "Good Enough as dialogue" and the fixture was not updated. While it stays red, the loop
stops at good-enough, so every book after it in the loop (Love from Seagull Island included) is never checked, and every audio PR's test line
carries a known failure. User, 2026-11-18: "add the 2 findings to the plan to fix".

- [x] Read 25609140 and the Good Enough slice to confirm the dialogue cast is the intended one (not a stray edit).
- [x] Pin Good Enough's current Farsi deliveries in `farsi-delivery-styles.json`, generated from the cast the way the other entries were (the README's A8 note:
      entries for books added after A8 are copies of the current cast). Don't change any audio clip.
- [x] Make the test check every book even when one fails (collect the mismatches, fail once at the end with all of them), so one stale entry can't hide the others.

**Verification:** the premade-audio suite runs with no failure; a deliberate one-character change to two books' entries reports both books.

*Changed while building:* the good-enough entry pins Carrot, Hazelnut, Referee and Crowd (each cast style plus ", standard Tehrani accent"). The test collects every mismatching book and fails once, naming the style and the pinned versus actual value; a missing key counts as a mismatch. Verified: the suite gives 128 OK (was 1 failure); the two-books check was run and reported both books.

#### From review

- Applied high #1 (`reviews/pr-1403-high.md`): mismatch output now names style, pinned and actual values.
- Applied medium #1 (`reviews/pr-1403-medium.md`): a missing key is recorded as a mismatch instead of raising `KeyError`.

Follow-ups from `reviews/pr-1403-check1-medium.md` (nits after the second pass; check passes converge):

- Skipped #1 (`reviews/pr-1403-check1-medium.md`): "yields one mismatch per cast character" - a nit after the second pass; every book is still reported, only the order of the lines is off. Follow-up.
- Skipped #2 (`reviews/pr-1403-check1-medium.md`): "Test method holds all compare-and-collect logic" - a style nit after the second pass. Follow-up.
- Skipped #3 (`reviews/pr-1403-check1-medium.md`): "a malformed fixture raises AttributeError" - a nit after the second pass; the fixture is a committed file of dicts, so a malformed one fails the test loudly anyway. Follow-up.
- Skipped #4 (`reviews/pr-1403-check1-medium.md`): "contradict the ticked items" - done by this file's new status line.
- Skipped #2 (`reviews/pr-1403-high.md`): "The good-enough entry is a copy of the current cast, not a pre-A8 snapshot, yet the test name and docstring say "recorded before the accent moved"." - the test name is not wrong enough to rename a pinned test; the README A8 note already explains copies.
- Skipped #3 (`reviews/pr-1403-high.md`): "All work items are ticked but the slice's Verification line (deliberate one-character change to two books' entries, both reported) is not recorded as run, and the status line still says "In progress"." - the slice record is written by update-docs in slice mode, not by code. (The two-book check was run in review fix pass 2: both books reported, one as a missing key.)
