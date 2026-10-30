### Slice 197 — Dino: a "Download now" tap right after the mobile-data check is never dropped

*Built (wave 2026-10-30), PR #583.* *Planned 2026-10-29.* Dino Reader (Android, iOS and the Windows head share the code). Gap left in Slice 115 (*Not yet verified*).

**Touches:** `src/DinoReader.App/StorytellerDefault.cs`, `src/DinoReader.App/StorytellerViews.cs`, `src/DinoReader.App/Testing/DinoTestCommands.cs` (Debug hook only, if one is needed to force the mobile-data state), `.maestro/dino/**`, `tests/DinoReader.Tests/**`, `docs/plan/slices/197-dino-download-now-tap-dropped.md`.

#### Why

On mobile data the storyteller pack waits for Wi-Fi and shows a "Download now" pill (Slice 115). A tap that lands right after the mobile-data check can be lost: the download never starts and the card stays. A parent taps, nothing happens, and they think the app is broken.

#### Decisions

- Reproduce on the emulator first (cellular-only network via `adb emu network` / `svc wifi disable`, or a Debug hook that forces the metered state). If it doesn't reproduce in a scripted flow, record that and stop with no code change.
- The fix makes the tap a state the downloader reads (a "user allowed mobile data" flag set before the network check runs), not a race between the check and the tap. Any wait over 500 ms after the tap shows the existing progress line.

#### State design (coordinator, 2026-10-30, before claim)

- **One owner:** `StorytellerDefault` (`_running` :53, `_mobileDataRequested` :54, `_downloadNowChosen` :57). Likely drop window (from code, not yet reproduced): a "Download now" tap while a run holds `_running` sets `_mobileDataRequested`, but `RunAsync` reads it only at the WaitingForWifi check (:210); a tap after that check and before `_running` clears (:150) is erased by the reset.
- **Rule, by construction:** the tap first records consent in `_downloadNowChosen` (lasts until that download completes or is cancelled, as Slice 182 decided). When a run ends, before it releases `_running`, it checks consent: if the run ended WaitingForWifi and consent is now given, it runs again once at once. Consent and the run guard live in the MAUI-free `StorytellerConsent` under one lock: the tap records consent and takes the guard in one step; the run ending checks consent and releases the guard in one step, so no timing window remains. Consent is cleared on Done, on the already-installed path and on Cancel; it is kept across a failure retry of the same download (Slice 182).
- **Interrupts:** notification Cancel stays final (`PackCancelledByNotificationException`, consent cleared); app killed: consent is not saved (a fresh start asks again); offline: as Slice 182 (waits, resumes).
- First reproduce on the emulator (a metered network, then the tap during the check), and keep the repro as the scripted check.

#### Work

- [ ] Scripted repro flow (Maestro or `run-flows.ps1`): metered network, open grown-up mode, tap "Download now" within 200 ms of the card appearing, assert the download starts (Debug log marker). (`.maestro/dino/download-now-tap.yaml`, written, not yet run on a device: a smoke check only: a timing race of microseconds cannot be reproduced reliably by Maestro, so it passes on old and new code; the concurrent interleaving unit test in `StorytellerConsentTests` is the proof)
- [x] Fix in `StorytellerDefault` so the tap is never lost; the pure decision (allowed? metered? state) moves to a unit-tested method.
- [x] Unit tests for the decision: tap before check, tap during check, tap after check, Wi-Fi arriving after the tap.
- [x] Android and iOS: same shared code; iOS compile check here (no Mac), TestFlight check listed under *Not yet verified*.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Emulator: the smoke flow passes (a tap during the Wi-Fi card starts the download); the race itself is proved by the unit test, not the flow.
- iOS: compile here; TestFlight on a cellular-only iPhone later.

#### Verified (2026-10-30)
- Emulator (Wi-Fi off, cellular on): `.maestro/dino/download-now-tap.yaml` PASS in 65.5 s (`HOOK download-now consent recorded`, `HOOK storyteller download started`); the Kokoro pack downloaded over mobile data to the end. The race itself is proven by the 2000-round concurrent unit test (`StorytellerConsentTests`), not by the flow.

#### Not yet verified
- No stale "Downloading" status after the download ends (screen unreadable on that run).
- Notification Cancel during the download, then nothing restarts (download finished in about a minute first).
- iOS TestFlight on a cellular-only iPhone.
