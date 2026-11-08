# Slice 345: RTST: the Debug stall hook must block the reader's first-chunk wait

*Planned 2026-11-08.* From [Slice 320](320-device-check-tooling-from-2026-11-08-run.md): the Debug `StallHook` holds `StallReporter`'s watch, not the reader's first-chunk wait. In the emulator flow `stall-return-overlay` chunk 0 therefore still synthesizes and plays, and the "Still getting the voice ready" overlay never shows. The flow failed twice on 2026-11-08: `STALL hold begins`, then `CHUNK_SYNTH 0 end` and `CHUNK_AUDIO 0`, and no `STALL hold released`. The cause is likely, not proven.

**Touches:** `src/ReadTheStupidText.Application/Reading/StallReporter.cs`, the Debug StallHook, the three readers (Android, iOS, Windows), `.maestro/rtst/stall-return-overlay.yaml`

## Items

- [ ] Make the Debug-only stall hook block the reader's first-chunk wait on Android, iOS and Windows, so the reader really stalls while the hook holds.
- [ ] Rerun `stall-return-overlay` on the emulator: the overlay shows, then `STALL hold released` is logged.

## Verification

`stall-return-overlay` passes on the Android emulator; the iOS and Windows hook paths compile and have a unit test where the reader can be built without a device.
