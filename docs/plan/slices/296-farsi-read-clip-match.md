# Slice 296 — Why a Farsi read matched 5 of 78 pieces to clips

*Built 2026-11-07 (wave 2026-11-07), merged as PR #931 (e15ab256); iOS side not yet verified.* *Planned 2026-11-07.* Open question from [Slice 273](273-fallback-events-everywhere.md) parts B and C: on a Farsi read with a fixture `fa` pack only 5 of 78 pieces matched a clip, although only every third entry was corrupted. Either the fixture's keys differ from the text a Farsi read really speaks, or there is a real Farsi pre-made audio gap. Until this is answered, `fa` clip counts are not to be trusted. Moved from plan.md's *Outstanding work*. Applies to Android and iOS (shared reading code); the emulator and the iPhone read the same clips.

**Touches:** `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/AudioPackStore.cs`, `src/DinoReader.App/Testing/DinoTestCommands.ShelfAudio.cs`, `tests/DinoReader.Tests/AudioPackStoreTests.cs`, `src/DinoReader.App/Testing/DinoTestCommands.cs` (the fixture builder, work item 3), `.maestro/dino/usage-fallback-partial.yaml`, `tests/DinoReader.Tests/**` (new clip-key test), `docs/plan/slices/296-farsi-read-clip-match.md`

## Work

- [x] 1. Reproduce on the emulator with the fixture `fa` pack and read the clip-match events (`clip_missing` and the fallback events from Slice 273): list, per piece, the key the reader looked up and the keys in the pack. (Done as counts per run, see the finding; no per-piece list was needed because 52 of 78 equals the corruption alone.)
- [x] 2. Compare with the real store pack for one Farsi book (live `/api/stories` pack, not the fixture): how many pieces match. (78 of 78.)
- [x] 3. If the fixture keys are wrong, fix the fixture builder. If the real pack has a gap, plan the missing audio (Slice 246's source folder holds the clips) and say how many pieces and which books. (Neither: the fixture is right and the live pack has no gap for this book; nothing to fix.)
- [ ] 4. Android and iOS: the same reader code serves both; confirm on the iOS side by log (TestFlight build, `clip_missing` rows for `fa`).

## Verification

- A written cause with the run that shows it; a Farsi read of a store book on the emulator reports every piece matched or names the missing ones.

## Wave note (coordinator, 2026-11-07)

- Premise check: the 5-of-78 run (Slice 273) used `install-audio-pack lang=fa miss=text_mismatch`, and the hook corrupts every third entry on purpose (`Testing/DinoTestCommands.cs`, `WriteFixturePackAsync`, the `i % MismatchEvery == 0` rule at ~307). Test that first: compute, in a unit test, which fixture keys the reader's lookup would hit for the same book, with and without the corruption. If corruption alone gives about 52 matches, not 5, the gap is real and in key derivation (text normalisation of Farsi: ZWNJ, Arabic vs Persian yeh/kaf, digits, reflowed lines).
- Item 2 reads the live `/api/stories` pack read-only (download, no upload). Item 4 is an `analytics-query` read of `clip_missing` rows for `fa` (read-only). Neither changes live state.
- Night: no iPhone; the iOS side is by log only.

## Finding (2026-11-07)

**Cause:** the 5 of 78 does not reproduce on `main`. Corruption alone gives 52 of 78; there is no Farsi clip gap for this book. Slice 273's 5 most likely was the fallback counter, not a clip-match count: in run 1 below the line `[Usage] fallback premade_audio:text_mismatch times=N counted` counts up once per silent piece reached while playing (times=1 at 20:35:04 up to times=5 at 20:35:18, when the flow stopped), while the same run logged `52 of 78 pieces have a clip`. So 5 silent pieces were reached before the flow ended. This is likely, not proven: it matches the run 1 log (`reviews/wave41-s296/run1-logcat.log`), but the Slice 273 run itself was not re-read. `fa` clip counts can be trusted again for this book.

### Emulator run (Dino Debug build `0ac339e41410`, emulator-5554, book `three-little-pigs`, `fa`; logs in `D:/ReadTheStupidText-worktrees/reviews/wave41-s296/`)

- Run 1, flow `usage-fallback-partial` (`install-audio-pack lang=fa miss=text_mismatch`): logged `fa v1: 52 of 78 pieces have a clip` and `read voice partial (52 pieces by clip, 26 with no clip)`, with `miss=text_mismatch`. Exactly the corruption's 26 of 78 missed.
- Run 2, fixture with no `miss`: `78 of 78`, no miss.
- Run 3, live pack via `fetch-audio-pack id=three-little-pigs lang=fa` (v2): `78 of 78`, `read voice premade`.
- Item 4 (iOS): production analytics for the last 30 days show no iOS `fa` reads, so there is nothing to read by log; the iOS check stays under *Not yet verified*.

### Offline model (no device)

Numbers for `three-little-pigs` in Farsi (78 spoken pieces, the 78 of the Slice 273 run); the emulator run above confirmed them. These numbers come from a test model, not from a run of the hook or the app: the test (`FarsiClipKeyTests`) keeps its own small copy of the hook's pack-writing rule (one entry per spoken piece, every third hash corrupted; it names the hook lines it mirrors, and the app's code is unchanged) and calls the Core steps the reading session makes (`StoryPieces.Split`, `BundledTranslations.Compose`, `StoryTranslation.TranslateJoinedAsync`), then looks each piece up with the reader's `InstalledPack.Find` over real files. `StoryVoicing` and `StoryTranslator` are MAUI-side (service provider) and are not reachable from `tests/DinoReader.Tests`, so their steps are called one by one, not as a whole, and the hook's own text ordering is not exercised. The model is not the proof; the emulator run above is.
- Reader's lookup (pack entry at the piece's spoken position, confirmed by the hash of the plain translated piece, as `StoryVoicing.PremadeSource.PathOf` does) over the fixture builder's keys, in the test model: 78 of 78 with a complete pack; 52 of 78 with `miss=text_mismatch` (26 corrupted: entries 0, 3, ..., 75), no file-missing misses. So, in the model, the corruption alone never gives 5.
- The live `/api/stories` fa pack (v2, 78 entries, 1.6 MB, read-only GET, kept in the worktree's untracked `tmp/live/`): the model's reader pieces hash to the pack's entries 78 of 78, same positions (manifest only: clip files are created empty in the test, so a missing file on a device is not covered). So the real store pack shows no Farsi gap for this book.
- Tests: `tests/DinoReader.Tests/FarsiClipKeyTests.cs` (three: complete pack 78 of 78; text-mismatch pack 52 of 78 with 26 text-mismatch misses; the live pack, skipped unless `DINO_LIVE_FA_MANIFEST` names a downloaded manifest, so CI does not run it).
- The live-pack test is skipped unless `DINO_LIVE_FA_MANIFEST` names a downloaded manifest, on purpose; the emulator run 3 recorded the live result. The model pins 78 and 52 as exact counts.

Production analytics, last 30 days (analytics-query, 2026-11-07): 15 fa reads, all Android, none iOS; 13 premade with no miss, 1 partial (book af402c99...), 1 device:clip_missing (book 1ca50c69...); 5 premade_audio:clip_missing fallback rows with book none; too few to show a real fa gap.

### Changed while building

- App code is unchanged: an earlier refactor of the hook into Core was reverted. The only code change is the new `tests/DinoReader.Tests/FarsiClipKeyTests.cs` (model test pinning 78 and 52; the live-pack test runs only with `DINO_LIVE_FA_MANIFEST`).
- Tests: `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` 3886 passed, 2 skipped, 0 failed.
- Device: the emulator runs as written under *Emulator run*.

### From review

Files: `reviews/s296-high.md`, `reviews/s296-medium.md`, `reviews/s296-check1.md`, `reviews/s296-check2.md`.

- High: all findings applied except #7.
- Skipped #7 (`reviews/s296-high.md`): "Touches lists files this change does not edit"
- Medium (`reviews/s296-medium.md`): #1, 2, 6, 7 and 8 resolved by reverting the app code (the hook refactor into Core); #4 applied; #3 and #5 listed next.
- Skipped #3 (`reviews/s296-medium.md`): "Tests are circular (both sides built from the same Core calls)"
- Skipped #5 (`reviews/s296-medium.md`): "Expected corrupted count (spoken + 2) / 3 mirrors the production"
- Check 1: all applied except #1, 3, 4 and 7.
- Skipped #1 (`reviews/s296-check1.md`): "fixture built from reader's own texts; matching tests pass by construction"
- Skipped #3 (`reviews/s296-check1.md`): "model uses clip offset 0; mid-book read offset (a candidate cause) is untested"
- Skipped #4 (`reviews/s296-check1.md`): "live-pack test skipped without env var"
- Skipped #7 (`reviews/s296-check1.md`): "Touches lists DinoTestCommands.cs, usage-fallback-partial.yaml, AudioPackStoreTests.cs, none changed"
- Check 2: #1 applied (this file's cause paragraph, rewritten from the run 1 log).
- Skipped #2 (`reviews/s296-check2.md`): "runs 2 and 3 used throwaway flows tmp-s296-nomiss/live not in the diff"
- Skipped #3 (`reviews/s296-check2.md`): "asserts only totals (52 of 78, 26); wrong pieces missed would still pass"
- Skipped #4 (`reviews/s296-check2.md`): "only offset-0 read modelled; mid-book ClipOffsetAsync path unverified"

### Not yet verified

- iOS: a Farsi read on an iPhone reports its clip count (no iOS `fa` reads in production in 30 days, so no log to read yet).
- Mid-book read start (`ClipOffsetAsync`) is not modelled or run; only reads from the start are covered.
- Runs 2 and 3 used throwaway flows not in the repo; only run 1's build label (`0ac339e41410`) is proven.
