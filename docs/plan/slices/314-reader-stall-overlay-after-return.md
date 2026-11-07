# Slice 314 — Restore the "Still getting the voice ready" overlay when the person returns to the reader during a stalled read

*Built 2026-11-07 (wave 2026-11-07, PR #927, merged as 882e01ce); the stall-return case is not yet verified on Android or iOS.* Planned 2026-11-07. Follow-up to [Slice 304](304-slice-202-rereview-and-initwait-test.md) review 6 (`reviews/s304-202-medium.md`): `ReaderPage.OnAppearing` re-subscribes `StallChanged`, but `ISpeechReader` exposes no stalled state, so a stalled read is silent with no Cancel after the person visits Settings or Review and comes back. Android and iOS both (shared `ReaderPage`).

**Touches:** `src/ReadTheStupidText.Application/**` (`ISpeechReader`), `src/ReadTheStupidText.Mobile.Shared/**` (the phone readers and `CompositeSpeechReader`), `src/ReadTheStupidText.Infrastructure/Reading/**` (Windows readers), `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/314-reader-stall-overlay-after-return.md`

## Design (coordinator, wave 2026-11-07)

- **One owner of the state:** each chunking reader's existing `StallReporter`. Its `IsStalled` (`Application/Reading/StallReporter.cs`) is already true only while a chunk wait is open, and `WatchAsync`'s `finally` closes the wait on stop, cancel, failure and the read finishing. So the state is derived, never stored a second time and never reset by hand.
- `ISpeechReader` gets `bool IsStalled => false;` (a default member next to `StallChanged`, like the event's default), so readers that cannot know (the phone's own voice, WinRT) need no change.
- Supertonic readers (Windows `SupertonicSpeechReader`, `AndroidSupertonicSpeechReader`, `IosSupertonicSpeechReader`): `public bool IsStalled => _stall.IsStalled;`.
- Wrappers forward: both `CompositeSpeechReader`s return `_active?.IsStalled ?? false` (the same reader `ForwardStall` accepts a stall start from); `WindowsNeuralReader` returns `_inner.IsStalled`.
- `ReaderPage.OnAppearing`, right after `_reader.StallChanged += OnReaderStallChanged`: `OnReaderStallChanged(_reader, _reader.IsStalled)`. The handler is idempotent (Begin does nothing while a wait is active; End only ends a Stalled wait), so an event racing the read does no harm; the overlay keeps its 500 ms rule.
- Interrupts: page away while the read stops, finishes or errors: `IsStalled` is already false on return, so nothing shows. Read still stalled: overlay with Cancel. Stall ends while away: the false event was missed, but the read returns false, so nothing stale shows.
- Test seams: `StallReporter.IsStalled` across a watch and a cancelled watch; a composite with a fake inner reader forwards `IsStalled` from the active reader only.

## Work

- [x] 1. Add a stalled state to `ISpeechReader` (a property next to the `StallChanged` event) and set it in every implementation, cleared when the stall ends or the read stops.
- [x] 2. `ReaderPage.OnAppearing` reads that state after re-subscribing and shows the overlay (with Cancel) when the read is stalled.
- [x] 3. Test: the state is true between a stall start and its end, and false after stop; the page restores the overlay from the state (a pure seam if the page cannot be built in tests).

## Verification

- Unit tests above. Device: start a read that stalls, open Settings, come back: the overlay and Cancel are shown (Android and iOS).
- Run 2026-11-07: RTST tests 2183 pass (new `StallReporter` cancel tests); Mobile Android, App x64 and iOS compile with 0 errors. Emulator (build 8c98cb65973f): paste-and-read PASS; sentence-highlight FAIL twice (missing `SENTENCE 0 1`): the read ran on fallback speech (`[Usage] fallback speech:neural_not_ready`, READ_START 19:56:36, supertonic-3 pack done 19:56:39), the known fresh-install failure already in `docs/plan/unverified.md` (Slice 215 line), not this slice's code path. Logs: `reviews/wave40-device/s314/`.

*Changed while building:* built as the Design section, plus one change found in review. `StallReporter.WatchAsync(pending, token)` ends the watch at whichever comes first, the chunk ready or the read's token cancelled (`Interlocked` release, once), so Stop or a superseding read ends the stall at once; all three Supertonic readers pass their per-generation `_synthCts` token. `ReaderPage.RestoreStallWait` (`ReaderPage.Wait.cs`) runs on the main thread and syncs from `IsStalled` alone: stalled begins the stall wait, otherwise a Stalled-stage wait is ended. Composite readers return `_active.IsStalled`.

*From review:* high pass (`reviews/s314-high.md`) 5 findings read, medium (`reviews/s314-medium.md`) 5/5, check 1 (`reviews/s314-check1.md`) 2/2, check 2 (`reviews/s314-check2.md`) 4/4.
- Applied high #1 (overlay over a stopped read; first fixed with an Idle guard, which check 1 #1 showed hid the overlay while the first chunk generates, so it was replaced by the token design above), high #2, high #5, medium #1, check 1 #1.
- Skipped #3 (`reviews/s314-high.md`): "Promised forwarding tests are missing." The test project references only Domain, Application and Infrastructure, and the Infrastructure composite takes sealed readers.
- Skipped #4 (`reviews/s314-high.md`): "IsStalled covers only the active reader." It matches the forwarding rule `ForwardStall` already uses.
- Skipped #2 (`reviews/s314-medium.md`): "queued StallChanged(true) handlers marshal separately" (low, pre-existing narrow race).
- Skipped #3 (`reviews/s314-medium.md`): "if _inner type does not override IsStalled" (low).
- Skipped #4 (`reviews/s314-medium.md`): "nothing enforces it" (low).
- Skipped #5 (`reviews/s314-medium.md`): "Composite delegation, RestoreStallWait and the Idle guard are untested." (low).
- Skipped #2 (`reviews/s314-check1.md`): "IsStalled reports only the active reader" (same as the skipped high #4).
- Skipped #1 (`reviews/s314-check2.md`): "token already cancelled on entry raises Changed(true) then Changed(false) inline" (low).
- Skipped #2 (`reviews/s314-check2.md`): "Release runs inside Cancel()" (low, not a defect).
- Skipped #3 (`reviews/s314-check2.md`): "await pending after cancel may return a value" (low, pre-existing).

#### For CLAUDE.md (lifted 2026-11-07)

- A reader that raises `StallChanged` must also return `IsStalled` (`ISpeechReader`, `Application/Reading/ISpeechReader.cs`): the default is false, which hides the stall from `ReaderPage.RestoreStallWait` after the person returns to the reader. Pass the read's per-generation token to `StallReporter.WatchAsync` so Stop or a superseding read ends the stall. (Slice 314: docs/plan/slices/314-reader-stall-overlay-after-return.md)

#### Not yet verified

- The stall-return case itself on Android and iOS (start a stalled read, open Settings, come back: overlay and Cancel shown); no Debug hook forces a stall.
- The iOS stall watcher on device.
