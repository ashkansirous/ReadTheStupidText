# Slice 476: Dino: fix the Farsi (fa) premade-audio pack alignment (text_mismatch)

**Product:** Dino

*Planned 2026-11-28 by plan-cleanup, not started.*

**Why:** the Watch line "Dino fa pack text mismatch" fired on 2026-11-28. Query (`dino_usage`, `blob1='fallback'`, `blob9='premade_audio:text_mismatch'`, `blob3='fa'`, 7 days, token `CF_ANALYTICS_READ_TOKEN`, `docs/analytics/dino-voice-queries.md`) shows 13 rows on 1.136.0 (was 1 on 2026-11-25) plus 2 on 1.131.1; `premade_audio:clip_missing` 0. By session (MAX(double3) per `blob16`): 1.136.0 book `32d04b935a5649088293314cc648c825` in 4 sessions (47, 19, 18 and 18 times; latest 2026-11-28 10:16Z), book `af402c9974294fcb91a2c8a985c17558` once (1 time); 1.131.1 book `966400017c494fb5b045786f53852c9e` once (11 times). So the mismatch is on the current build, in repeated sessions, mostly one book.

**Work:**
- [ ] 1. Find why the fa pack for book `32d04b935a5649088293314cc648c825` (and `af402c99...`) does not line up with the book text: compare the pack's piece list with the text the app splits, in the same form the app uses (`AudioPackUsage`, premade-audio alignment code).
- [ ] 2. Fix the pack or the splitter so `read_voice` is `premade` for fa; add a test with the failing text.
- [ ] 3. After release, the same query shows no new fa `text_mismatch` rows on the fixed build (a Watch line, not a wait inside this slice).

**Touches:** Dino audio-pack alignment (pack build tooling, premade-audio matching in DinoReader.Core / Shared).
