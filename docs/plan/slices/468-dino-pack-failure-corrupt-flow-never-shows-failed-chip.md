# Slice 468 — Dino: the pack-failure-corrupt flow never shows the failed chip
**Product:** Dino

**Touches:** `src/Dino/DinoReader.App/ShelfAudioPacks.cs`, `src/Dino/DinoReader.App/ShelfAudioPacksCard.cs`, `src/Dino/DinoReader.App/StoryLanguagesPage.cs`, `.maestro/dino/pack-failure-corrupt.yaml`, `.maestro/dino/pack-failure-network.yaml`, `.maestro/dino/pack-failure-disk-full.yaml` (widened at build: same below-the-fold assert), `.maestro/dino/side-language-audio.yaml`, `.maestro/dino/pack-cancel-stalled.yaml` (widened at review 1: same Swedish-row wait), `tests/Dino/DinoReader.Tests/**`, `src/Dino/DinoReader.App/Testing/DinoTestCommands.ShelfAudio.cs` (new partial, only if a test hook is needed; never `DinoTestCommands.cs`, which the parked s403 worktree edits), `docs/plan/unverified.md`

*Built 2026-11-27, PR #1454, device run 2 passed 5/5 on the emulator, not merged. Planned 2026-11-27 (index pass of wave 10) from Slice 463's follow-up F17.*

**Why.** `.maestro/dino/pack-failure-corrupt.yaml` FAILS on an `origin/main` build (9ea9d6bd, emulator-5554, 2026-11-27 about 21:3xZ) and on the Slice 463 branch: the app logs `failed:unzip` and `shelf audio sv: 0 of 1 done, 1 failed`, the Story languages page opens 8 s later, and `id: ShelfAudioFailed-sv` (`src/Dino/DinoReader.App/ShelfAudioPacksCard.cs:23`) never appears in 120 s. The chip state is in memory (`ShelfAudioPacks._states`, `ShelfAudioPacks.cs:275-283`, `StoryLanguagesPage.cs:296`). The last PASS found is 2026-11-08; the flow was last edited in Slice 418 (2026-11-19). Logs: `D:/ReadTheStupidText-worktrees/reviews/w10-device/main-baseline/` and `reviews/w10-device/s463f/`.

**Work.**
- [x] Find out whether the page reads a fresh `ShelfAudioPacks` (so the failed state is lost between the download and the page) or the chip is hidden from automation. **Cause:** neither. The state survives (log: `0 of 1 done, 1 failed`, `finished`, no later run; `_states[sv]` stays Idle with Failed=1). The Swedish row is the 12th and is clipped by the page's ScrollView viewport (ends y=2171; the tab bar starts at 2190) on a 1080x2400 screen: Maestro's saved hierarchy at the failing step shows `Svenska` bounds [2156..2171], 15 px high, so the chip was never on screen.
- [x] Fix it, with a unit test where the cause is in code. The cause is the flow, not code: no unit test. Confirmed by emulator run 2: the failed chip always worked, the Swedish row is clipped by the ScrollView viewport so the flows never saw it, and the flow-only fix passes. The three flows now use `scrollUntilVisible` on `ShelfAudioCard-sv` (the card exists in the failed state, so the failed chip is then on screen).
- [x] Make `.maestro/dino/pack-failure-corrupt.yaml` pass on the emulator. Done with `pack-failure-network`, `pack-failure-disk-full`, `side-language-audio` and `pack-cancel-stalled`: run 2 PASS 5/5 on emulator-5554, 351 s, logs `reviews/w11-device/s468-run2/` (run 1 logs: `reviews/w11-device/s468/`).

#### Not yet verified

None for this slice's flows: the emulator run passed (run 2, above), so the Slice 468 line was removed from `docs/plan/unverified.md`.

Follow-up (not in this slice): `.maestro/dino/onboarding-two-languages.yaml:38,42` scrolls to and taps `StoryLanguageRow-sv` on the onboarding page. Run 1 showed that id is not exposed on the Story languages page (the row's hit Button, `StoryLanguagesPage.cs:167`, is not exposed to automation); whether it is exposed on the onboarding page was not checked.

#### For CLAUDE.md

- A Maestro flow that waits for or taps something in the Swedish (12th) row of the Story languages page scrolls to `ShelfAudioCard-sv` first with `scrollUntilVisible`: the row is clipped by the page's ScrollView viewport on a 1080x2400 screen. Never scroll to `StoryLanguageRow-sv` there (its hit Button, `StoryLanguagesPage.cs:167`, is not exposed to automation). After Cancel the card is gone, so assert the text `Svenska`. (Slice 468: docs/plan/slices/468-dino-pack-failure-corrupt-flow-never-shows-failed-chip.md)

#### From review

- Skipped #2 (`reviews/pr-1454-high.md`): "the fix is not shown to work." - the emulator run is step 6 of the implementation gate, after review 2; it is planned (Not yet verified above), not skipped.
- Skipped #4 (`reviews/pr-1454-medium.md`): "Root cause and fix items ticked [x] from one hierarchy dump with no rerun." - the cause is proven by the saved log and hierarchy (both quoted in this file); the device item stays unticked until the emulator run.
- Skipped #5 (`reviews/pr-1454-medium.md`): "[session]-tagged check the same run can do (emulator reachable); run the five flows before merge and record the result." - the emulator run of the five flows is this wave's next step, after this fix; the unverified line goes when it passes.
- Applied (`reviews/pr-1454-high.md`): #1 (the same wait in `side-language-audio` and `pack-cancel-stalled` now scrolls too), #3 (the timeout nit; marked Fixed in the findings file), #4 (stale comments reworded), #5 ("passes" worded as an open check, now closed by run 2). Applied (`reviews/pr-1454-medium.md`): #1, #2, #3, #6. Check pass 1 (`reviews/pr-1454-check-1.md`): #1, #2 and #3 applied in these docs (device item ticked, `ShelfAudioCard-sv` named, run 2 cited).
- Skipped #4 (`reviews/pr-1454-check-1.md`): "post-Cancel `assertVisible: "Svenska"` is a text assert on a row that may sit clipped" - flake risk only; it passed in run 2 and check passes converge, so it is not reopened; kept as a follow-up: if that assert flakes, scroll to the row again before it.
- The skips above of high #2 and medium #4 and #5 were about the missing device run; run 2 has since passed 5/5.
- Device run 1 (emulator-5554, 2026-11-27): all five flows failed at `scrollUntilVisible StoryLanguageRow-sv` because the row's transparent hit Button is not exposed as a resource-id (the hierarchy held `ShelfAudioCard-sv`, `ShelfAudioNone-sv`, `ShelfAudioFailed-sv`). Fixed in the flows only: the scroll anchor is now `ShelfAudioCard-sv` (it exists in the failed, stalled and finished-with-None states; `ShelfAudioPacksCard.For` returns null once cancelled), and the two post-Cancel `assertVisible` checks use the text `Svenska` because the card is gone then. Assertions kept, none dropped.
