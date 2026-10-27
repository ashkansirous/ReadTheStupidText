# Slice 162 — Scripted device flows: Maestro plus Debug-only test hooks

*Planned.* Batch 36 (fast device tests), RTST Mobile and Dino Reader, Android and iOS. Decision 165. After Slice 161.

**Touches:** `.maestro/rtst/**`, `.maestro/dino/**`, `.maestro/README.md` (all new),
`src/ReadTheStupidText.Mobile.Shared/Testing/**` (new, Debug only),
`src/ReadTheStupidText.Mobile.Shared/Platforms/Android/Testing/**` (new),
`src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/Testing/**` (new),
`src/ReadTheStupidText.Mobile.Shared/ReadTheStupidText.Mobile.Shared.csproj`,
`src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/DinoReader.App/MauiProgram.cs`,
`src/ReadTheStupidText.Mobile/**/*.xaml`, `src/DinoReader.App/**/*.xaml` (`AutomationId`s only),
`tools/emulator/run-flows.ps1`, `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`

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

## Verification

- `run-flows.ps1 -App rtst` and `-App dino` pass on the warm emulator with no window on screen.
- Removing an `AutomationId` makes the flow fail with a clear message.
- The Release APK/AAB has no `TestHook` types (`unzip` plus a type search) and no `RTST-TEST` strings.
- The iOS compile check passes with the hook in.
