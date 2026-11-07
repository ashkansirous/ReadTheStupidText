# Slice 314 — Restore the "Still getting the voice ready" overlay when the person returns to the reader during a stalled read

*In progress (wave 2026-11-07), draft PR.* Planned 2026-11-07. Follow-up to [Slice 304](304-slice-202-rereview-and-initwait-test.md) review 6 (`reviews/s304-202-medium.md`): `ReaderPage.OnAppearing` re-subscribes `StallChanged`, but `ISpeechReader` exposes no stalled state, so a stalled read is silent with no Cancel after the person visits Settings or Review and comes back. Android and iOS both (shared `ReaderPage`).

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

- [ ] 1. Add a stalled state to `ISpeechReader` (a property next to the `StallChanged` event) and set it in every implementation, cleared when the stall ends or the read stops.
- [ ] 2. `ReaderPage.OnAppearing` reads that state after re-subscribing and shows the overlay (with Cancel) when the read is stalled.
- [ ] 3. Test: the state is true between a stall start and its end, and false after stop; the page restores the overlay from the state (a pure seam if the page cannot be built in tests).

## Verification

- Unit tests above. Device: start a read that stalls, open Settings, come back: the overlay and Cancel are shown (Android and iOS).
