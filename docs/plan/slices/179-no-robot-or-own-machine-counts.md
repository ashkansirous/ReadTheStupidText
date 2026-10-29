# Slice 179 — No Play robot, VM or own-machine runs in the usage counts

*Built 2026-10-28 (PR #542), not yet verified on device or after deploy; see Not yet verified.* Both products, every head: Read The Stupid Text (Windows tray, Android, iOS) and Dino Reader (Android, iOS,
Windows). Follow-up to Slice 167, which did not stop all of it. Asked for by the user on 2026-10-28.

**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `src/ReadTheStupidText.Application/Usage/**`,
`src/ReadTheStupidText.Mobile.Shared/Usage/UsageGate.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/**` (only if a
platform helper is needed), `src/ReadTheStupidText.Infrastructure/Usage/WindowsUsage.cs`,
`tests/ReadTheStupidText.Tests/UsageEligibilityTests.cs`, `tools/desktop-vm/**`, `tools/emulator/**`, `tools/phone/**`,
`.claude/agents/analytics-query.md` (none in the repo: use the query notes in Slice 166's file),
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
- The Worker validates the device fields but never drops a count because of them (`worker/events.ts:59-67`), although
  `arch` and `platform` are already sent on every event (Slice 166).

## Decisions (defaults chosen; the user can overrule)

1. **The Worker drops Android and iOS events from `x64`/`x86` devices.** Real phones and tablets are ARM. This removes the
   robot from versions already in people's hands, with no app change. (A Chromebook running the Android app on Intel would be
   dropped too; accepted, it is rare.)
2. **Android also checks itself:** emulator signs in `Build.Fingerprint` (`generic/` prefix, `robo`; not `test-keys`, which real custom ROMs carry), `Build.Hardware`
   (`ranchu`, `goldfish`), `Build.Model`/`Product` (`sdk_gphone`, `Emulator`, `Android SDK built for`), an x86/x64 process architecture, and
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

7. **Why the per-platform signals differ (review 2, finding 10).** Android: the harnesses drop a marker file (adb can write the
   app's external files folder) and Play's pre-launch report is caught by Test Lab and the emulator/robot build strings; internal-track
   installs are counted because Android has no reliable receipt for them. iOS: the env var marks the simulator, and `sandboxReceipt`
   marks TestFlight and App Review installs. There is no iOS simulator harness yet, so no iOS launch-flag twin is built; when one is
   added it sets the env var. This asymmetry is a deliberate default, not a user exemption; the user can overrule it.

## Work

The first item alone fixes the robot for every shipped version.

- [x] Worker: drop `android`/`ios` events whose `device.arch` is `x64` or `x86` (return accepted, write nothing, so the app
      doesn't retry); tests in `events.test.mjs`.
- [x] `UsageEligibility`: add the "test install" and "non-store install" inputs; unit tests in `UsageEligibilityTests.cs`.
- [x] Android (`UsageGate`): the extra emulator/robot signals in decision 2, fail closed on the Test Lab read.
- [x] iOS (`UsageGate`): the `sandboxReceipt` check from decision 3.
- [x] Windows (`WindowsUsage` for RTST, `UsageGate` Windows path for Dino): Store signature only; read the off-switch env var. Note: `UsageGate` has no Windows branch today (header says Android/iOS only) and Dino Reader has no `Package.Current`/`SignatureKind` use, so this is a new Windows code path in `UsageGate` (or a shared helper in Infrastructure, reused by `WindowsUsage`).
- [x] Harnesses: `tools/desktop-vm`, `tools/emulator` and `tools/phone` set the off switch on every run.
- [x] Query notes: add the robot signature and the Windows caveat (decision 6).

## Verification

- Unit tests (`dotnet test tests/ReadTheStupidText.Tests/...` and the Dino suite) and the Worker tests pass.
- Worker: a local `wrangler dev` POST with an Android `x64` device is accepted and writes nothing; an `arm64` one writes.
- Android emulator and iOS simulator, Release build with a stamped version: a debug log line shows the gate says "don't
  count" and why. Same on the Windows VM with a CI MSIX.
- After the first release with it: an `analytics-query` run over the next 48 hours shows no Android/iOS `x64` rows and no
  Windows burst from a non-Store install.

*Changed while building:* the Windows path is a shared helper reused by `WindowsUsage` (RTST) and the Dino `UsageGate`; the new `UsageFacts` fields (`IsTestInstall`, `IsNonStoreInstall`) and their skip reasons were appended after `Unreadable`. `UsageEligibility.IsEmulatorBuild` uses narrow patterns only (`generic/` prefix, `sdk_gphone`, `goldfish`/`ranchu`/`emu`); test-keys custom ROMs count as real. Off switch: env `READTHESTUPIDTEXT_NO_USAGE=1`, or the Android marker file `no-usage` in the external files dir (a read error counts the install).

*From review:* two passes (`reviews/179-review1.md`, `179-review2.md`). Skipped on purpose: dropping x86 Chromebook installs is the slice decision, and iOS installs without a sandbox receipt still count (real users).

*Checks:* worker 90 tests, RTST and Dino suites, typecheck and App build green. Emulator: marker written and verified, gate detects the emulator with and without the marker, first run OK.

#### For CLAUDE.md (lifted 2026-10-29)

Root, usage paragraph: Robot and own-machine exclusion lives in `UsageEligibility.IsEmulatorBuild`, `UsageGate` and the `worker/events.ts` arch drop (Android/iOS `x64`/`x86`); every test harness must set the off switch (env `READTHESTUPIDTEXT_NO_USAGE=1`, or the Android `no-usage` marker file). (Slice 179: docs/plan/slices/179-no-robot-or-own-machine-counts.md)

#### Not yet verified

- Windows VM run with a CI MSIX.
- iOS `sandboxReceipt` on TestFlight.
- Windows Store-signature check on a Store build.
- Release build on an emulator without the marker (gate reason).
- Worker drop live after deploy (Workers Builds deploys on merge).
- iOS simulator harness off switch (`SIMCTL_CHILD_READTHESTUPIDTEXT_NO_USAGE=1`) when a harness is added.
