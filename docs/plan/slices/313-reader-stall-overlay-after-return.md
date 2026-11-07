# Slice 313 — Restore the "Still getting the voice ready" overlay when the person returns to the reader during a stalled read

*Planned 2026-11-07.* Follow-up to [Slice 304](304-slice-202-rereview-and-initwait-test.md) review 6 (`reviews/s304-202-medium.md`): `ReaderPage.OnAppearing` re-subscribes `StallChanged`, but `ISpeechReader` exposes no stalled state, so a stalled read is silent with no Cancel after the person visits Settings or Review and comes back. Android and iOS both (shared `ReaderPage`).

**Touches:** `src/ReadTheStupidText.Application/**` (`ISpeechReader`), the `ISpeechReader` implementations (find them with a grep; Mobile and Infrastructure), `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/313-reader-stall-overlay-after-return.md`

## Work

- [ ] 1. Add a stalled state to `ISpeechReader` (a property next to the `StallChanged` event) and set it in every implementation, cleared when the stall ends or the read stops.
- [ ] 2. `ReaderPage.OnAppearing` reads that state after re-subscribing and shows the overlay (with Cancel) when the read is stalled.
- [ ] 3. Test: the state is true between a stall start and its end, and false after stop; the page restores the overlay from the state (a pure seam if the page cannot be built in tests).

## Verification

- Unit tests above. Device: start a read that stalls, open Settings, come back: the overlay and Cancel are shown (Android and iOS).
