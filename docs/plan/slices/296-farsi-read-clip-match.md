# Slice 296 — Why a Farsi read matched 5 of 78 pieces to clips

*In progress (wave 2026-11-07), draft PR.* *Planned 2026-11-07.* Open question from [Slice 273](273-fallback-events-everywhere.md) parts B and C: on a Farsi read with a fixture `fa` pack only 5 of 78 pieces matched a clip, although only every third entry was corrupted. Either the fixture's keys differ from the text a Farsi read really speaks, or there is a real Farsi pre-made audio gap. Until this is answered, `fa` clip counts are not to be trusted. Moved from plan.md's *Outstanding work*. Applies to Android and iOS (shared reading code); the emulator and the iPhone read the same clips.

**Touches:** `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/AudioPackStore.cs`, `src/DinoReader.App/Testing/DinoTestCommands.ShelfAudio.cs`, `tests/DinoReader.Tests/AudioPackStoreTests.cs`, `src/DinoReader.App/Testing/DinoTestCommands.cs` (the fixture builder, work item 3), `.maestro/dino/usage-fallback-partial.yaml`, `tests/DinoReader.Tests/**` (new clip-key test), `docs/plan/slices/296-farsi-read-clip-match.md`

## Work

- [ ] 1. Reproduce on the emulator with the fixture `fa` pack and read the clip-match events (`clip_missing` and the fallback events from Slice 273): list, per piece, the key the reader looked up and the keys in the pack.
- [ ] 2. Compare with the real store pack for one Farsi book (live `/api/stories` pack, not the fixture): how many pieces match.
- [ ] 3. If the fixture keys are wrong, fix the fixture builder. If the real pack has a gap, plan the missing audio (Slice 246's source folder holds the clips) and say how many pieces and which books.
- [ ] 4. Android and iOS: the same reader code serves both; confirm on the iOS side by log (TestFlight build, `clip_missing` rows for `fa`).

## Verification

- A written cause with the run that shows it; a Farsi read of a store book on the emulator reports every piece matched or names the missing ones.

## Wave note (coordinator, 2026-11-07)

- Premise check: the 5-of-78 run (Slice 273) used `install-audio-pack lang=fa miss=text_mismatch`, and the hook corrupts every third entry on purpose (`Testing/DinoTestCommands.cs` ~215). Test that first: compute, in a unit test, which fixture keys the reader's lookup would hit for the same book, with and without the corruption. If corruption alone gives about 52 matches, not 5, the gap is real and in key derivation (text normalisation of Farsi: ZWNJ, Arabic vs Persian yeh/kaf, digits, reflowed lines).
- Item 2 reads the live `/api/stories` pack read-only (download, no upload). Item 4 is an `analytics-query` read of `clip_missing` rows for `fa` (read-only). Neither changes live state.
- Night: no iPhone; the iOS side is by log only.
