# Slice 468 — Dino: the pack-failure-corrupt flow never shows the failed chip
**Product:** Dino

**Touches:** `src/Dino/DinoReader.App/ShelfAudioPacks.cs`, `src/Dino/DinoReader.App/ShelfAudioPacksCard.cs`, `src/Dino/DinoReader.App/StoryLanguagesPage.cs`, `.maestro/dino/pack-failure-corrupt.yaml`, `tests/Dino/DinoReader.Tests/**`, `src/Dino/DinoReader.App/Testing/DinoTestCommands.ShelfAudio.cs` (new partial, only if a test hook is needed; never `DinoTestCommands.cs`, which the parked s403 worktree edits), `docs/plan/unverified.md`

*In progress (wave 2026-11-27), draft PR. Planned 2026-11-27 (index pass of wave 10) from Slice 463's follow-up F17.*

**Why.** `.maestro/dino/pack-failure-corrupt.yaml` FAILS on an `origin/main` build (9ea9d6bd, emulator-5554, 2026-11-27 about 21:3xZ) and on the Slice 463 branch: the app logs `failed:unzip` and `shelf audio sv: 0 of 1 done, 1 failed`, the Story languages page opens 8 s later, and `id: ShelfAudioFailed-sv` (`src/Dino/DinoReader.App/ShelfAudioPacksCard.cs:23`) never appears in 120 s. The chip state is in memory (`ShelfAudioPacks._states`, `ShelfAudioPacks.cs:275-283`, `StoryLanguagesPage.cs:296`). The last PASS found is 2026-11-08; the flow was last edited in Slice 418 (2026-11-19). Logs: `D:/ReadTheStupidText-worktrees/reviews/w10-device/main-baseline/` and `reviews/w10-device/s463f/`.

**Work.**
- [ ] Find out whether the page reads a fresh `ShelfAudioPacks` (so the failed state is lost between the download and the page) or the chip is hidden from automation.
- [ ] Fix it, with a unit test where the cause is in code.
- [ ] Make `.maestro/dino/pack-failure-corrupt.yaml` pass on the emulator.

#### Not yet verified

(Nothing built yet.)
