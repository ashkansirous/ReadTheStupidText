# Slice 345: RTST: the Debug stall hook must block the reader's first-chunk wait

*Built 2026-11-09, merged as PR #987 (30bc0f3e); emulator flow passes, iOS and Windows not run.* Planned 2026-11-08. From [Slice 320](320-device-check-tooling-from-2026-11-08-run.md): the Debug `StallHook` holds `StallReporter`'s watch, not the reader's first-chunk wait. In the emulator flow `stall-return-overlay` chunk 0 therefore still synthesizes and plays, and the "Still getting the voice ready" overlay never shows. The flow failed twice on 2026-11-08: `STALL hold begins`, then `CHUNK_SYNTH 0 end` and `CHUNK_AUDIO 0`, and no `STALL hold released`. The cause is likely, not proven.

**Touches:** `src/ReadTheStupidText.Application/Reading/StallReporter.cs`, the Debug StallHook, the three readers (Android, iOS, Windows), `.maestro/rtst/stall-return-overlay.yaml`

## Items

- [x] Make the Debug-only stall hook block the reader's wait on Android, iOS and Windows (built 2026-11-09). Finding: the hook already blocked the reader's await (it sits inside `WatchAsync`; the CHUNK_SYNTH/CHUNK_AUDIO markers come from the synthesis finishing, not from playback). The likely reason the overlay never showed: chunk 0's wait is the page's "Preparing the voice" wait, which owns the overlay, so `BeginStallWait` is skipped. The hook now holds the read's second chunk (`holdable: index == 1` in the three readers), and the flow types two sentences so a second chunk exists. Unit-tested in `StallHookTests`; cause still unproven until the emulator run.
- [x] Rerun `stall-return-overlay` on the emulator: the overlay shows, then `STALL hold released` is logged. Passed twice on emulator-5554, 2026-11-09 (125 s and 86 s; `STALL hold released` in the kept logcat, `D:\ReadTheStupidText-worktrees\reviews\s345-run`). Not run on iOS.

## From review

- Fixed (`reviews/s345-high.md` #1-#5): a read that plays to its natural end without a second chunk disarms the request (`StallReporter.ReadPlayedToEnd`); all three readers ask `StallReporter.IsHoldable(index)`; the split test uses `SpeechChunkPlanner.ForText` with the readers' head policies and with and without a first-chunk hint.
- Kept open #6 (`reviews/s345-high.md`): "keep the slice's 'rerun on the emulator' item open (cause is still unproven)" - the hold now begins only after chunk 0 has played, so the flow's 15 s `WaitLine` depends on chunk 0's synthesis and playback time on a slow emulator; the index is absolute, so after a skip-back or voice-swap restart the hold can fire at a different point. Both are Debug-only and only an emulator run settles them.

- Fixed (`reviews/s345-medium.md` #1, #2, #3, #6): a read takes a claim on the armed request when it starts (`StallReporter.ClaimArmedRequest`); only its natural end (token not cancelled, same point in all three readers) disarms it (`ReadPlayedToEnd(claim)`), so a request armed after the read started survives. Tests added for both.
- Skipped #4, #5 (`reviews/s345-medium.md`): "holdability by absolute index 1" / "only Supertonic readers disarm; a stopped/replaced read leaves request armed up to 2 min" - Debug-only, request lapses after 2 min.

## Verification

`stall-return-overlay` passes on the Android emulator; the iOS and Windows hook paths compile and have a unit test where the reader can be built without a device.

#### For CLAUDE.md (lifted 2026-11-09)

- The RTST Debug stall hook holds the read's second chunk (`StallReporter.HoldableChunkIndex`), so a flow that uses it must type text that splits into two chunks. (Slice 345: docs/plan/slices/345-rtst-stall-hook-blocks-reader.md)

#### Not yet verified

- **Device:** `stall-return-overlay` on iOS and the Windows head (the hook edits in the iOS and Windows readers are not compiled or run here; only Android emulator ran, twice, 2026-11-09).
