# Slice 179 — No Play robot, VM or own-machine runs in the usage counts

*Planned.* Both products, every head: Read The Stupid Text (Windows tray, Android, iOS) and Dino Reader (Android, iOS,
Windows). Follow-up to Slice 167, which did not stop all of it. Asked for by the user on 2026-10-28.

**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `src/ReadTheStupidText.Application/Usage/**`,
`src/ReadTheStupidText.Mobile.Shared/Usage/UsageGate.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/**` (only if a
platform helper is needed), `src/ReadTheStupidText.Infrastructure/Usage/WindowsUsage.cs`,
`tests/ReadTheStupidText.Tests/UsageEligibilityTests.cs`, `tools/desktop-vm/**`, `tools/emulator/**`, `tools/phone/**`,
`.claude/agents/analytics-query.md` (if the repo has one; else the query notes in Slice 166's file),
`docs/plan/slices/179-no-robot-or-own-machine-counts.md`

## Why

An analytics check on 2026-10-28 (from Slice 167's merge, 2026-10-27 20:58Z, to 14:30Z the next day) found test traffic
still arriving from builds that contain the Slice 167 fix:

- **Play's pre-launch robot:** 9 Dino and 16 RTST rows, all Android, arch `x64`, US, Android 11, in pairs seconds after each
  new version (Dino 1.71.3-1.73.2, RTST 1.67.3-1.69.0). Almost all `app_opened`.
- **One Windows machine:** 94 RTST opens on 1.67.1 in 20 minutes overnight, a stamped version, most likely the user's own
  run of a CI or test MSIX.

Why they get through (checked in code on origin/main, 2026-10-28):

- The Android gate only checks MAUI `DeviceType.Virtual` (`UsageGate.cs:45`) and the `firebase.test.lab` system setting
  (`UsageGate.cs:57`). Play's robot is evidently neither, and a failed setting read counts the device (`UsageGate.cs:59-63`).
- iOS checks only the simulator.
- Windows passes `isDebug`, the package version and a hard-coded `false` for emulator and Test Lab (`WindowsUsage.cs:25-37`).
  Any Release MSIX with a real version counts, wherever it runs: the host, the Hyper-V test VM, a sideload.
- No harness can switch counting off: no env var, launch argument or marker file is read.
- The Worker validates the device fields but never drops a count because of them (`worker/events.ts:48-50`), although
  `arch` and `platform` are already sent on every event (Slice 166).

## Decisions (defaults chosen; the user can overrule)

1. **The Worker drops Android and iOS events from `x64`/`x86` devices.** Real phones and tablets are ARM. This removes the
   robot from versions already in people's hands, with no app change. (A Chromebook running the Android app on Intel would be
   dropped too; accepted, it is rare.)
2. **Android also checks itself:** emulator signs in `Build.Fingerprint` (`generic`, `robo`, `test-keys`), `Build.Hardware`
   (`ranchu`, `goldfish`), `Build.Model`/`Product` (`sdk`, `Emulator`), an x86/x64 process architecture, and
   `ActivityManager.IsUserAMonkey()`. A failed Test Lab read now means **don't count** (fail closed): one lost count is
   cheaper than a robot row.
3. **iOS:** besides the simulator, a build installed from TestFlight or used by App Review (its receipt file is
   `sandboxReceipt`) does not count. Only App Store installs count. TestFlight testers are the user's own circle; losing their
   counts is accepted. Android has no reliable equivalent for internal-track installs; the robot rules above cover it.
4. **Windows counts only Store-signed installs:** `Package.Current.SignatureKind == Store` and not `IsDevelopmentMode`, for
   the RTST tray app and Dino's Windows head. A CI MSIX, a local build, a VM test install or a sideload sends nothing.
5. **Off switch for our harnesses:** `UsageEligibility` gets a "test install" input. Set by the env var
   `READTHESTUPIDTEXT_NO_USAGE=1` on Windows and by a launch extra / marker file on Android and iOS. `tools/desktop-vm`,
   `tools/emulator` and `tools/phone` always set it, even for Release runs.
6. **Rows already stored** are not deleted (Analytics Engine can't). The `analytics-query` notes get the robot signature
   (Android/iOS `x64`/`x86`) and the "before Slice 179 Windows rows may be own runs" caveat, so reports keep filtering them.

## Work

The first item alone fixes the robot for every shipped version.

- [ ] Worker: drop `android`/`ios` events whose `device.arch` is `x64` or `x86` (return accepted, write nothing, so the app
      doesn't retry); tests in `events.test.mjs`.
- [ ] `UsageEligibility`: add the "test install" and "non-store install" inputs; unit tests in `UsageEligibilityTests.cs`.
- [ ] Android (`UsageGate`): the extra emulator/robot signals in decision 2, fail closed on the Test Lab read.
- [ ] iOS (`UsageGate`): the `sandboxReceipt` check from decision 3.
- [ ] Windows (`WindowsUsage` for RTST, `UsageGate` Windows path for Dino): Store signature only; read the off-switch env var.
- [ ] Harnesses: `tools/desktop-vm`, `tools/emulator` and `tools/phone` set the off switch on every run.
- [ ] Query notes: add the robot signature and the Windows caveat (decision 6).

## Verification

- Unit tests (`dotnet test tests/ReadTheStupidText.Tests/...` and the Dino suite) and the Worker tests pass.
- Worker: a local `wrangler dev` POST with an Android `x64` device is accepted and writes nothing; an `arm64` one writes.
- Android emulator and iOS simulator, Release build with a stamped version: a debug log line shows the gate says "don't
  count" and why. Same on the Windows VM with a CI MSIX.
- After the first release with it: an `analytics-query` run over the next 48 hours shows no Android/iOS `x64` rows and no
  Windows burst from a non-Store install.
