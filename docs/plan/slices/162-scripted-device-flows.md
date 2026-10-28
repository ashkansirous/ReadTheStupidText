# Slice 162 — Scripted device flows: Maestro plus Debug-only test hooks

*Built 2026-10-27 (wave 2026-10-27), not yet fully verified; see Not yet verified.* Batch 36 (fast device tests), RTST Mobile and Dino Reader, Android and iOS. Decision 165. After Slice 161.

**Touches:** `.maestro/rtst/**`, `.maestro/dino/**`, `.maestro/README.md` (all new),
`src/ReadTheStupidText.Mobile.Shared/Testing/**` (new, Debug only),
`src/ReadTheStupidText.Mobile.Shared/Platforms/Android/Testing/**` (new),
`src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/Testing/**` (new),
`src/ReadTheStupidText.Mobile.Shared/ReadTheStupidText.Mobile.Shared.csproj`,
`src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/DinoReader.App/MauiProgram.cs`,
`src/ReadTheStupidText.Mobile/**/*.xaml`, `src/DinoReader.App/**/*.xaml` (`AutomationId`s only),
`tools/emulator/run-flows.ps1` (new), `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`,
`tests/ReadTheStupidText.Tests/TestHookCommandTests.cs` (new) and its link in `ReadTheStupidText.Tests.csproj`,
`src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `src/DinoReader.App/DinoReader.App.csproj` (test-hook plist import), `tools/emulator/install-debug.ps1` (`--no-incremental`),
`tools/emulator/start-warm.ps1`, `stop.ps1`, `common.ps1`, `README.md` (no snapshots)

## Why

Device tests are an agent tapping one element at a time and reading the screen after each tap, which is slow and
uses a lot of tokens. A scripted flow runs the same checks in seconds and only reports what failed.

## Decisions (recommended, in the plan PR)

- **Maestro** for UI flows (YAML, runs on Android emulators and iOS simulators, needs only Java, and Java 21 is
  installed). Elements are found by MAUI `AutomationId`, never by position or translated text.
- **Debug-only test hooks** for state, so a flow never clicks through first-run screens to reach what it tests:
  skip first run, set the language, load a text or book, start or stop a read. On Android it is an `adb shell am
  broadcast` receiver. On iOS it is a Debug-only URL scheme (`rtst-test://`, `dino-test://`) that Maestro's `openLink`
  can call. Both call one shared `TestHookDispatcher` in `Mobile.Shared/Testing`. None of this is compiled into
  Release (`#if DEBUG` plus a csproj condition, checked by unzipping the Release APK).
- **Logcat markers:** the hooks and the read path log one line per state change under the tag `RTST-TEST`
  (`READ_START`, `CHUNK n`, `READ_END`, `PACK_READY`). Flows and scripts assert on those lines (`adb logcat -s`),
  not on screenshots.

## Work

- [ ] **Smallest end-to-end first:** one RTST flow, `.maestro/rtst/paste-and-read.yaml`. The skip-first-run hook
  runs, text is pasted, Read is tapped, and the flow waits for `READ_START`/`READ_END` in logcat.
  `tools/emulator/run-flows.ps1 -App rtst` runs it against the warm emulator (Slice 161) and prints pass/fail only.
- [ ] `TestHookDispatcher` (shared, unit-tested command parsing) plus the Android receiver and the iOS URL handler.
- [ ] `AutomationId`s on the controls the flows use (both apps).
- [ ] RTST flows: first run and language, speed change, a document open, Scan's photo fallback, voice pack states
  (fake pack-ready hook).
- [ ] Dino flows: the intro wizard, open a starter book, page turn, the cast page, the store list (offline-safe).
- [ ] iOS: the same flows are written platform-neutral (by `AutomationId`, hooks through `openLink`). Running them
  on the CI macOS simulator is a later step, because of the Actions quota (`.github/CLAUDE.md`). This slice only
  checks that the iOS hook compiles (`-p:IncludeIos=true`).
- [ ] Rule in `Mobile.Shared/CLAUDE.md`: new screens get `AutomationId`s, device checks start as a Maestro flow,
  and hooks stay Debug-only. Point `device-test` at `run-flows.ps1` (ClaudeSkills PR).

## Built / changed notes

- `start-warm.ps1` always cold-boots with `-no-snapshot-load -no-snapshot-save` (~45 s, once per session; a running emulator is reused). Cause: after a quick-boot snapshot resume 3 of 5 flows failed on stale app/system state; after a cold boot all 5 pass. The existing snapshot under `~/.android` is left alone.

- Built: Maestro (installed under `D:\ReadTheStupidText-worktrees\.tools\maestro`) flows `.maestro/rtst` (first-run, paste-and-read, speed) and `.maestro/dino` (first-run, open-book); `tools/emulator/run-flows.ps1` (whole-token log markers); Debug-only hooks in `Mobile.Shared/Testing` with Android and iOS twins (commands `skip-first-run`, `stop-read`, `state`; Android: launch args or the per-app broadcast `uk.sirous.rtst-test.TEST_HOOK` / `uk.sirous.dino-test.TEST_HOOK`; iOS: `rtst-test://` / `dino-test://` via a `TestHooks.props` plist merge, Debug only); markers `READ_START`, `CHUNK n`, `READ_END`, `PACK_READY`, `PAGE n`, `HOOK`, `STATE`; `AutomationId`s in both apps; `TestHookCommandTests` (5); `install-debug.ps1` uses `--no-incremental`.
- Cold-boot times: rtst 34 / 54 / 37 s, dino 39 / 58 s. This supersedes Slice 161's 7 s warm-start claim.
- From review: high pass 14 findings (applied 1, 2, 4, 6, 8, 14; 12 moved to Follow-up; rest low, Debug-only); medium pass 11 (applied 1, 2, 3, 9; rest low/info).
- Checks: RTST 1363 pass / 2 skip, Dino 1550 pass; iOS compile of both apps; Release build hook-free (no `TestHook` strings).

## Verification

- `run-flows.ps1 -App rtst` and `-App dino` pass on the warm emulator with no window on screen.
- Removing an `AutomationId` makes the flow fail with a clear message.
- The Release APK/AAB has no `TestHook` types (`unzip` plus a type search) and no `RTST-TEST` strings.
- The iOS compile check passes with the hook in.

## Follow-up (flows not built yet)

- Dino: starting the voice pack from the skip-first-run hook (`SpokenLanguage.Use`) crashes the Debug x86_64 emulator build (Mono SIGSEGV loading `System.Reflection.Emit.ILGeneration.dll`, a worker thread, ~1 s after the hook). The hook therefore sets the languages only. Find out whether a real first run crashes the same way.
- A CI check that the Release build has no hook types: search the built assembly for `TestHookDispatcher` and `RTST-TEST` in the Release workflows (today only the manual acceptance bullet above).
- iOS hook delivery is unverified: launch arguments as `NSUserDefaults` and the `OpenUrl`/`OnActivated` lifecycle have only been compile-checked, never run on a simulator or device.

- Document open (system file picker), Scan's photo fallback (camera), a fake pack-ready hook, Dino's intro wizard and the Dino store list (grown-up gate).

#### For CLAUDE.md (lifted 2026-10-27)

- Device acceptance for the MAUI apps starts with `tools/emulator/run-flows.ps1 -App rtst|dino` (Maestro flows in `.maestro/`), not hand-tapping; new screens get `AutomationId`s and hooks stay Debug-only. The emulator always cold-boots (~45 s, reused while running); never rely on snapshots. (Slice 162: docs/plan/slices/162-scripted-device-flows.md)
- Follow-up (ClaudeSkills): the `device-test` agent should run the flows first.

#### Not yet verified

- iOS hooks and the `rtst-test://` / `dino-test://` URL scheme have never run (no Mac); compile-checked only.
- Dino `skip-first-run` leaves out the voice pack start (Debug x86_64 SIGSEGV in Reflection.Emit via `SpokenLanguage.Use`); check whether a real Dino first run crashes on the x64 emulator, and the grown-up hand-over.
