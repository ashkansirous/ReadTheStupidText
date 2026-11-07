# Slice 310 — Dino Debug hook to force a voice-pack download failure

*In progress (wave 2026-11-07), draft PR.* Planned 2026-11-07. Testing gap: Dino has no Debug hook to force a pack failure; testers corrupt the `.partial` file by hand. It was "worth adding with Slice 187" and 187 shipped without it. Moved from plan.md's *Built but not verified*. Debug builds only; the hook must never exist in a Release build and never reach production analytics (`UsageEligibility` already blocks Debug).

**Touches:** `src/DinoReader.App/Testing/DinoTestCommands.cs`, `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookDispatcher.cs`, `src/DinoReader.App/Testing/DinoTestCommands.ShelfAudio.cs`, `src/DinoReader.App/AudioPackStore.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `tests/DinoReader.Tests/AudioPackStoreTests.cs`, `.maestro/dino/**`, `tools/emulator/**`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/310-dino-debug-hook-force-pack-failure.md`

## Work

- [ ] 1. Android and iOS: a Debug-only test command (same mechanism as the other `DinoTestCommands`) that makes the next pack download fail in a named way: `network` (connection drops mid-download), `corrupt` (bad hash at the end), `disk-full`. Compiled out of Release (`#if DEBUG` or the existing test-command gating, whichever the other commands use).
- [ ] 2. Use it in a scripted emulator flow that checks the failure message, Retry and the cleaned `.partial`; replace the by-hand `.partial` corruption in the unverified checks.
- [ ] 3. iOS: the same commands in the iOS Debug build; check with an iOS Debug build on a simulator or the iPhone (compile and one run).
- [ ] 4. One line in `src/DinoReader.App/CLAUDE.md` on the commands (only if an agent would get it wrong without it).

## Verification

- A unit test for the failure injection; the emulator flow passes on all three failure kinds; a Release build contains no such command (checked by a grep of the built assembly's strings or a build-time test).
