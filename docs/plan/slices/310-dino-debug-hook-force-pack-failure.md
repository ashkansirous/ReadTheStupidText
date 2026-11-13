# Slice 310 — Dino Debug hook to force a voice-pack download failure
**Product:** Dino

*Built 2026-11-07 (wave 2026-11-07, PR #925, merged as 4cda30ce). Android emulator flows pass; iOS not yet verified.* Planned 2026-11-07. Testing gap: Dino has no Debug hook to force a pack failure; testers corrupt the `.partial` file by hand. It was "worth adding with Slice 187" and 187 shipped without it. Moved from plan.md's *Built but not verified*. Debug builds only; the hook must never exist in a Release build and never reach production analytics (`UsageEligibility` already blocks Debug).

**Touches:** `src/DinoReader.App/Testing/DinoTestCommands.cs`, `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookDispatcher.cs`, `src/DinoReader.App/Testing/DinoTestCommands.ShelfAudio.cs`, `src/DinoReader.App/AudioPackStore.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `tests/DinoReader.Tests/AudioPackStoreTests.cs`, `.maestro/dino/**`, `tools/emulator/**`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/Testing/CLAUDE.md`, `docs/plan/slices/310-dino-debug-hook-force-pack-failure.md`

## Work

- [x] 1. Android and iOS (shared MAUI code, no platform conditions): a Debug-only test command (same mechanism as the other `DinoTestCommands`) that makes the next pack download fail in a named way: `network` (connection drops mid-download), `corrupt` (bad hash at the end), `disk-full`. Compiled out of Release (`#if DEBUG` or the existing test-command gating, whichever the other commands use).
- [x] 2. Use it in a scripted emulator flow that checks the failure message, Retry and the cleaned `.partial`; replace the by-hand `.partial` corruption in the unverified checks.
- [ ] 3. iOS: the same commands in the iOS Debug build; check with an iOS Debug build on a simulator or the iPhone (compile and one run).
- [x] 4. One line in `src/DinoReader.App/CLAUDE.md` on the commands (only if an agent would get it wrong without it).

## Verification

- A unit test for the failure injection; the emulator flow passes on all three failure kinds; a Release build contains no such command (checked by a grep of the built assembly's strings or a build-time test).

*Changed while building:* Dino's pack path is `AudioPackStore`, not `VoicePackInstaller` (Dino has no phone voice; `NoSpeechEngineTests`), so `VoicePackInstaller` is untouched. The hook is `force-pack-failure mode=network|corrupt|disk-full|off` (`DinoTestCommands.ShelfAudio.cs`, wired in `DinoTestCommands.cs`); it sets `AudioPackStore.ForcedFailure` (volatile, read once per install) and the store wraps its fetcher in `FailingAudioPackFetcher`: `network` writes 1 KB then throws (`failed:network`), `corrupt` zeroes the last 64 bytes so unzip fails (`failed:unzip`), `disk-full` throws `IOException` (`failed:storage`). All under `#if DEBUG`. Debug log lines `Debug failure forced (<kind>)` and `temp files left none|yes` (only on forced attempts). Three flows `.maestro/dino/pack-failure-{network,corrupt,disk-full}.yaml`: force, wait for the failed line, restart (the process-wide switch clears), the next launch fetches the pack, proven by expect-log `bee62232085d4c1ab50cdf574530f473 pack in` and `shelf audio sv: 1 of 1 done,`. Markers must start after whitespace (`tools/emulator/common.ps1` `Test-LogMarker`), hence the book id prefix. The restart design replaced the in-session retry after the flow's tail was reopened three times. Hook documented in `src/DinoReader.App/Testing/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md`.

*Verified:* Release dll holds none of the strings (checked by hand, no build-time test). Device: run 1 (`reviews/wave40-device/s310/`) failed only on a marker starting with ": " (the hook itself worked: forced line, failed line, temp files none, next launch installed); run 2 (`reviews/wave40-device/s310-run2/`) all three PASS (~90 s each, build 740f56385095).

*From review:* high (`reviews/s310-high.md`): #1, 2, 5, 6 applied. medium (`reviews/s310-medium.md`): #1 applied. check1 (`reviews/s310-check1.md`): no findings. check2 (`reviews/s310-check2.md`): #1, 2 replaced by the restart design. The rest are listed one per line below.
- Skipped #3 (`reviews/s310-high.md`): "Release safety rests on one `#if DEBUG` per site and nothing tests it" (low; the builder searched the built Release DinoReader.App.dll and found none of the hook strings)
- Skipped #4 (`reviews/s310-high.md`): "scope wording): the hook only fails downloads through `AudioPackStore.InstallCoreAsync`" (Dino has no phone voice, `src/DinoReader.App/CLAUDE.md`, Slice 278B, and `NoSpeechEngineTests` forbids `VoicePackInstaller` in Dino, so `AudioPackStore` is Dino's pack path)
- Skipped #2 (`reviews/s310-medium.md`): "probe runs after the cleanup, so it nearly always reports none" (the probe exists to show the cleanup left nothing, so "none" is the expected result)
- Skipped #3 (`reviews/s310-medium.md`): "the real static ForcedFailure wiring in InstallCoreAsync" (low; unit tests cover FailingAudioPackFetcher and the device runs exercise the store wiring)
- Skipped #3 (`reviews/s310-check2.md`): "FailingAudioPackFetcher has an unreachable PackFailureInjection.None branch" (low; unit tests cover FailingAudioPackFetcher and the device runs exercise the store wiring)
- Skipped #4 (`reviews/s310-check2.md`): "probe checks only the two paths just deleted" (the probe exists to show the cleanup left nothing, so "none" is the expected result)
- Skipped #1 (`reviews/s310-check3.md`): "restart half has no explicit retry trigger" (speculative race on the launch resume; the device run passed all three)

#### For CLAUDE.md (lifted 2026-11-07)

- Dino's `force-pack-failure mode=network|corrupt|disk-full|off` Debug hook fails the next `AudioPackStore` install (not voice packs); it lasts one process, so a flow restarts to clear it. Expect-log markers must start after whitespace, so prefix the book id. Already written in `src/DinoReader.App/Testing/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md` (Slice 310: docs/plan/slices/310-dino-debug-hook-force-pack-failure.md).

#### Not yet verified

- iOS: the same hook and three flows on an iOS Debug build (no Mac or iPhone in the 2026-11-07 wave).
