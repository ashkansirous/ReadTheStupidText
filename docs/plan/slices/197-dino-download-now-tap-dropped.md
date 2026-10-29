### Slice 197 — Dino: a "Download now" tap right after the mobile-data check is never dropped

*Planned 2026-10-29.* Dino Reader (Android, iOS and the Windows head share the code). Gap left in Slice 115 (*Not yet verified*).

**Touches:** `src/DinoReader.App/StorytellerDefault.cs`, `src/DinoReader.App/StorytellerViews.cs`, `src/DinoReader.App/Testing/DinoTestCommands.cs` (Debug hook only, if one is needed to force the mobile-data state), `.maestro/dino/**`, `tests/DinoReader.Tests/**`, `docs/plan/slices/197-dino-download-now-tap-dropped.md`.

#### Why

On mobile data the storyteller pack waits for Wi-Fi and shows a "Download now" pill (Slice 115). A tap that lands right after the mobile-data check can be lost: the download never starts and the card stays. A parent taps, nothing happens, and they think the app is broken.

#### Decisions

- Reproduce on the emulator first (cellular-only network via `adb emu network` / `svc wifi disable`, or a Debug hook that forces the metered state). If it doesn't reproduce in a scripted flow, record that and stop with no code change.
- The fix makes the tap a state the downloader reads (a "user allowed mobile data" flag set before the network check runs), not a race between the check and the tap. Any wait over 500 ms after the tap shows the existing progress line.

#### Work

- [ ] Scripted repro flow (Maestro or `run-flows.ps1`): metered network, open grown-up mode, tap "Download now" within 200 ms of the card appearing, assert the download starts (Debug log marker).
- [ ] Fix in `StorytellerDefault` so the tap is never lost; the pure decision (allowed? metered? state) moves to a unit-tested method.
- [ ] Unit tests for the decision: tap before check, tap during check, tap after check, Wi-Fi arriving after the tap.
- [ ] Android and iOS: same shared code; iOS compile check here (no Mac), TestFlight check listed under *Not yet verified*.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Emulator: the repro flow fails before the fix and passes after it, three runs in a row.
- iOS: compile here; TestFlight on a cellular-only iPhone later.
