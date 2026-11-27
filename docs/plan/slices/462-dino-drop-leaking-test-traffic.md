# Slice 462 — Dino: drop the test and review traffic that still reaches dino_usage and Ninjalog
**Product:** Dino

*Built 2026-11-27 (wave 3 of night 2026-11-27-0154); merged as PR #1404 (e7151af5). Android Test Harness Mode is skipped as TestLab; groups A, C, D stay (owner Decision 2026-11-27). Shared tests 1745 pass, Worker tests 592 pass, Mobile.Shared builds for net11.0-android.*

*Planned 2026-11-27 from the 2026-11-26 usage analysis (Ninjalog `ashkan-sirous/dino-book-reader`, index `dino_usage`, 19-26 Nov).*

**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `worker/ninjalog.ts`, `worker/ninjalog.test.mjs`, `src/Shared/Sirous.Reading.Application/Usage/UsageEligibility.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/Usage/UsageGate.cs`, `tests/Shared/Shared.Tests/**`, `worker/CLAUDE.md`, `docs/analytics/ninjalog-queries.md`, `site/dino/privacy.html`

**Why:**
- About 20 of 53 first launches in 19-26 Nov were test or review traffic. They had US region, non-production builds, 1-second sessions, and Play pre-launch-style crawlers with 11-12 cancelled downloads. They make the raw funnel look far worse than it is: 21 of 27 real new users finish the intro on 1.136.0.
- What already filters, from the code (origin/main, 2026-11-27):
  - The client gate (`UsageGate`, `UsageEligibility`) drops debug builds, unstamped builds, emulators, Firebase Test Lab, test installs, non-Store Windows MSIX builds and TestFlight/App Review (sent to `dino_usage_beta`).
  - The Worker drops unstamped or pre-1.0 versions, and x64/x86 Android and iOS (`events.ts:172, 217-219, 352-363`). Windows is not covered by that arch rule.
  - Ninjalog gets only rows already written to `DINO_USAGE` (`ninjalog.ts:10`), so the same filters apply.
- So the leak is traffic these rules miss. Which rule misses it isn't known yet.

**Decisions (owner, 2026-11-27: "4- yes", to my recommendation to drop, not tag):**
- Leaking test traffic is dropped, not tagged.
- No install id, device id or new device field is added (Decision 140; the privacy page's "no install identifier"). Ninjalog is a third party, so nothing new goes there.
- If a case can't be told apart without a new field, it goes to `dino_usage_beta` (Slice 251's pattern), or stays, and is written down here.

**Open for the owner:** none.

**Waiting on the user:** Android drops Test Harness Mode devices; iOS has no such signal, so iOS keeps today's behaviour (TestFlight and App Review already go to `dino_usage_beta`; the iOS Groups A and C stay in `dino_usage`) unless you exempt it or name a signal. This default is recorded; it blocks nothing.

**Work:**
- [x] 1. Find what leaks. Use Ninjalog and `analytics-query` over 19-26 Nov for first-launch sessions under 2 s, sessions with 10 or more cancelled downloads, US non-production version strings, and Windows rows. Group them by platform, version, device class, arch, OS major and form factor. Write down which existing rule each group slips past.
  - **Result (live analysis over `dino_usage`, 19-26 Nov; Ninjalog not queried, it holds the same rows; 68 first-launch sessions).** Every row below already passed the client gate and the Worker's x64/x86 phone rule. "Cancels" is my reading (`audio_pack_download` with `cancel` in `detail`, plus `wait_cancelled`).
  - **Group A, sessions of 1-2 s (6):** all US, phone, arm64, 5-8 cores, 8+ GB, 2 events, 0 cancels. Android 1.133.2 x2, 1.133.0 (OS 16), 1.131.1 (OS 11); iOS 1.131.1, 1.133.1 (OS 27). Slips past the client gate (real-looking arm64, no emulator, Test Lab or install signal) and the Worker (stamped, at least 1.0, arm64). Only behaviour tells it apart (session length, no later events), which the Worker cannot see when it writes a batch.
  - **Group B, 10+ cancelled downloads (3, all Android):** US, OS 16, phone, arm64, 5-8 cores, 8+ GB; 1.136.3 (11 cancels), 1.133.3 (12), 1.133.0 (11); 102-110 events, 319-350 s. Looks like Play pre-launch or a testing device (inference). Slips past the arch rule (arm64) and the client gate (Test Lab setting not set, no emulator string). One signal the gate did not read: Android Test Harness Mode (`ActivityManager.isRunningInUserTestHarness()`).
  - **Group C, US "non-production" version strings:** iOS 1.136.0 (12 or more sessions), 1.133.1 (6), 1.136.5 (4), 1.136.3 (3), 1.133.3 (2), old strings 1.96.1-1.123.3 (1-5 events); Android 1.133.0, 1.133.3, 1.136.3 (the Group B sessions). Slips past the Worker's version rule (it only drops unstamped or pre-1.0) and, on iOS, the client's TestFlight/App Review diversion when the receipt is not sandbox. The live store version list was not checked, and 1.136.0 is the build the owner's 21-of-27 intro figure comes from, so "non-production" here is inference.
  - **Group D, Windows (8 first opens, 23 across ages):** 1.136.3.0, 1.136.5.0, 1.133.0.0, arm64 1.133.3.0; x64 desktop, OS 11. Nothing looks like test traffic; a VM or CI runner cannot be told from a PC by any row field, and a non-Store MSIX is already dropped on the client.
- [ ] 2. Close each gap at the cheapest correct place. That's the client gate when the device can tell (for example Play pre-launch's test-lab flag or robot UI automation). It's the Worker when the row shows it (for example an unreleased version string, or the arch rule extended to Windows if the leak is there). Each rule gets a test.
  - **Built (Android, Group B):** the gate now also treats Android Test Harness Mode as a test device: `UsageEligibility.IsTestLabDevice(setting, inUserTestHarness)` is true for the `firebase.test.lab` setting or `ActivityManager.isRunningInUserTestHarness()` (API 29, public; guarded by an API 29 check, the app's minimum is 28). It is read with the Test Lab setting in `UsageGate.IsTestLab`, once at start before the first event, and reports as `TestLab` (so crash reports skip it too, as for Test Lab). The Test Lab setting read still fails closed; the harness flag has its own try, and a throw there counts as not a harness (real users stay counted; no fallback count can be sent from the gate, because the usage queue asks the gate first). A Debug line names both signals. Tests: `UsageEligibilityTests` (setting alone, harness alone, neither, `"True"` is not `"true"`, and a harness device is skipped as `TestLab`). The Android head builds (`net11.0-android`); whether Play's pre-launch devices set Test Harness Mode is not proven, so the next window's Group B count decides.
  - **Not built, no signal tells it apart (this slice's Decisions, owner 2026-11-27: no new device field):** Group A (needs session length, which the Worker never sees; dropping it would also drop real users who open and close the app at once); Group C (version strings cannot be told from live releases without a release list, and 1.136.0 is a real build; dropping by inference would lose real users); Group D (no row field separates a VM or runner from a PC; the non-Store MSIX rule already covers CI builds). These stay in `dino_usage`; the verification query reads them as the remaining share. iOS: App Review and TestFlight already go to `dino_usage_beta` through the sandbox receipt, simulators are `DeviceType.Virtual`, and iOS has no public test-farm flag, so there is no iOS twin of the Android rule; no Worker rule was added because no field already on the row separates test traffic.
- [ ] 3. Write the rules into `worker/CLAUDE.md` and `docs/analytics/ninjalog-queries.md`. Update `site/dino/privacy.html` only if what is kept changes.

**Verification:**
- Worker and Shared tests are green.
- Ninjalog, after release, for a 7-day window: the queries in `docs/analytics/ninjalog-queries.md`, section "Test-looking sessions (Slice 462)" (first sessions `installAge=first`; cancels are `wait_cancelled` plus `audio_pack_download` with `detail=cancelled`). Test-looking sessions (under 2 s, or 10 or more cancelled downloads; the queries do not measure version strings) should be under 5% of first sessions (the one denominator: first sessions seen in any event, Sonos and Alexa out), against about 38% (20 of 53) in 19-26 Nov.

#### Not yet verified

- [cloud] or [session] After the release that ships this slice, the next analytics window (7 days of Ninjalog): the share of test-looking first sessions (third query in `docs/analytics/ninjalog-queries.md`, "Test-looking sessions") is under 5%, and Group B (10 or more cancels) is gone. Unproven that Play's pre-launch devices set Test Harness Mode.
No device run is planned for the Test Harness read: a Debug build skips usage at the debug check before the harness check, and `adb shell cmd testharness enable` wipes the device's user data, so a device run cannot show `Reason=TestLab`. The gate's harness read calls an Android API the net11.0 test project cannot run; only the pure helper `IsTestLabDevice` is unit tested, and the 7-day query line above is the proof. The release build is also blind to whether the flag fired or the read threw (the diagnostic is Debug-only).

#### From review

- Skipped #8 (`reviews/pr-1404-high.md`): "tests/Shared/Shared.Tests/UsageEligibilityTests.cs:136, severity nit: the tests cover only the pure `IsTestLabDevice` helper." - the gate's harness read calls an Android API that the net11.0 test project cannot run; the pure helper carries the rule, and the 7-day query line above is the proof of the read.
- Skipped #1 (`reviews/pr-1404-medium.md`): "src/Shared/Sirous.Reading.Mobile.Shared/Usage/UsageGate.cs:79 (IsInUserTestHarness call), severity nit: confirm `ActivityManager.IsRunningInUserTestHarness` binds as the call written (static property vs method) in the net11.0-android binding." - the Android build of Mobile.Shared for net11.0-android had 0 errors in the build and fix rounds, so the compiler confirmed the shape.
- Skipped #2 (`reviews/pr-1404-check1-medium.md`): "the harness diagnostic is Debug-only." - nits after the second pass, since check passes converge; the blind spot is stated under *Not yet verified*.
- Skipped #4 (`reviews/pr-1404-check1-medium.md`): "session-less rows fall into one bucket in all three queries." - nits after the second pass, since check passes converge.
- Skipped #5 (`reviews/pr-1404-check1-medium.md`): "work items 2 and 3 are still unticked though built" - nits after the second pass, since check passes converge; the iOS gap is the *Waiting on the user* line.
- Skipped #6 (`reviews/pr-1404-check1-medium.md`): "the setting key `firebase.test.lab` stays in UsageGate" - nits after the second pass, since check passes converge.
- Skipped #7 (`reviews/pr-1404-check1-medium.md`): "confirm crash reports are meant to drop for it too" - nits after the second pass, since check passes converge.
