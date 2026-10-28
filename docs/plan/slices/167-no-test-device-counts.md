### Slice 167 — Usage counts: don't count test devices (both apps)

*Built 2026-10-27.* Not yet verified: Release build on an emulator (crashed at startup in MAUI before any usage code, x64 only; store builds are arm64), the emulator and Test Lab rules on a device (unit tests only), the RTST tray on the desktop, an iOS simulator run; the first store rollout showing no test rows (query via the API).

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Usage/*`, `src/ReadTheStupidText.Application/Usage/*`, `src/ReadTheStupidText.Infrastructure/Usage/*`, `src/DinoReader.App/UsageCounter.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/DinoReader.App/MauiProgram.cs`, `tests/ReadTheStupidText.Tests/UsageServiceTests.cs`, `tests/ReadTheStupidText.Tests/UsageTests.cs`, `worker/events.ts`, `worker/events.test.mjs`, `tools/dino-usage-report.ps1`, `tools/rtst-usage-report.ps1`, `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html`

Planned 2026-10-27, the evening counting with device context (Slice 166) went live. On its first day `dino_usage`
held about 450 counts and none came from a real user: Google Play's pre-launch report robot (country US, versions
not yet rolled out, e.g. 1.71.1 on an x64 Android 11 emulator and an arm64 Android 15 phone) and our own local
builds (version `1.0`, country GB: emulator device tests from this PC). Nothing in the apps stops a test build from
counting: no debug check, no emulator check, and the remote switch is on for every build.

**Decisions (settled 2026-10-27; the user asked for it, the details are the agent's call):**

- A count is never sent when the build or device is a test one:
  1. **Debug build** (`#if DEBUG`), every head: Android, iOS, Windows (RTST tray, Dino's Windows head).
  2. **Unstamped build**: the app's display version is the project default `1.0` (CI stamps every shipped build
     with the GitVersion number, so `1.0` means built on a desk). Every head.
  3. **Emulator or simulator**: MAUI `DeviceInfo.DeviceType == DeviceType.Virtual` (Android emulator, iOS
     simulator) — one check, both platforms.
  4. **Android Firebase Test Lab** (what Play's pre-launch report runs on): the system setting
     `firebase.test.lab` is `"true"` (Google's documented check; confirm via library-docs/web before coding). iOS
     has no pre-launch robot, so there is no twin to build; (3) covers the simulator.
     The Test Lab check is Android-only because iOS has no pre-launch robot; the simulator check covers iOS.
  5. The emulator rule applies on Android and iOS only, never Windows (a real user in a Windows VM counts).
     Unstamped means blank, `1.0`, `1.0.0`, `1.0.0.0`, `0.0.0`, `0.0.0.0` in the app, the Worker and both reports.
     A gate fact that cannot be read means not counted, except the Test Lab setting (absent is normal: not a test).
- **Dry run instead of silence:** on a test build or device the sender still builds each batch but writes it to
  the debug log (the existing DEBUG batch line, marked `not sent: <reason>`) instead of posting it, so device tests
  can still check what a batch would hold. Nothing reaches `/api/events`.
- Internal testers on Play's internal track and TestFlight still count: they are real people on real phones, and
  the device cannot tell an internal-track install from a store one. The reports can filter them by version.
- One pure decision, unit-tested: `UsageEligibility` (Application) takes the facts (is debug, display version,
  is virtual, is test lab) and answers counted / not counted with the reason. Each head gathers the facts once at
  start. The reason goes to a Debug line only.
- The Worker is a backstop only (new global rule, analytics record real production users): it drops, and does not write, any count whose
  app version is exactly the unstamped default `1.0`, on both the Dino and RTST paths. It cannot tell an emulator from a phone, so the apps
  carry the rest.
- Report scripts leave out rows already stored from test builds: version `1.0`, and Android `x64` rows (only
  emulators are x64 Android). Analytics Engine cannot delete rows.
- The scripts read the token from the secrets store entry `CF_ANALYTICS_READ_TOKEN`
  (`D:\secrets\readthestupidtext\secrets.env`, Account Analytics: Read only) when `-Token` and
  `CLOUDFLARE_API_TOKEN` are not given, and default `-AccountId` to the account that owns the Worker.
- Privacy pages: one sentence saying test and development builds send nothing.

**Work:**

- [x] `UsageEligibility` in `src/ReadTheStupidText.Application/Usage/` plus unit tests for each rule and their mix.
- [x] Android: facts gathered in Mobile.Shared (Test Lab setting read through `Android.Provider.Settings.System`),
      the shared queue sender (`UsageQueueSender`) refuses to queue or send when not counted. Both apps.
- [x] iOS twin: the same facts (debug, version, simulator) through the same Mobile.Shared code path. Both apps.
- [x] Windows: RTST tray (`UsageService`) and Dino's Windows head skip counting for debug and unstamped builds.
- [x] Report scripts: test-row filter, token from the secrets store, default account id.
- [x] `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html`: the one sentence.

**Verification:**

- Unit tests for `UsageEligibility` (each rule alone, all clear, several at once).
- Android emulator (Release build): no `POST /api/events` leaves the app (DEBUG log line in `UsageQueueSender`
  absent, or logcat shows the skip reason); the in-app switch still shows and works.
- iOS: compiles for `net10.0-ios`; simulator run if one is available.
- Windows: the RTST tray Debug build logs the skip reason and sends nothing.
- After the next store rollout: `dino_usage` rows from the new version, none `x64` Android, none version `1.0`,
  queried through the API with the new token.
