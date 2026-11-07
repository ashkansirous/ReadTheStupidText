# Slice 297 — Slice 267 follow-ups (read_voice count, layout-check leaks, wizard button grid)

*In progress (wave 2026-11-07), draft PR.* Planned 2026-11-07. Four items left by [Slice 267](267-dino-old-voice-and-wizard-buttons.md) (built, #826). Moved from plan.md's *Outstanding work*. Items 1 and 4 are app code (the same MAUI code on Android and iOS); items 2 and 3 are emulator tooling.

**Touches:** `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `tools/emulator/layout-check.ps1`, `tools/emulator/run-flows.ps1`, `tools/emulator/test-layout-check.ps1`, `tests/DinoReader.Tests/**`, `docs/plan/slices/297-slice-267-follow-ups.md`

## Work

- [ ] 1. Android and iOS: `read_voice` is counted before the read really starts (`ReadingSession.cs:570`; the flag `_voiceCountedFor` is at line 69, the report at line 625). Count it once the first piece plays, so a read that fails to start counts nothing. Test: a read that fails before audio sends no `read_voice`.
- [ ] 2. `layout-check.ps1` is dot-sourced by `run-flows.ps1`, so its `param()` and `$ErrorActionPreference` leak into the runner. Run it as a child script or move its functions to a module with no `param()`/preference side effects. Test in `test-layout-check.ps1`: the caller's `$ErrorActionPreference` is unchanged after the call.
- [ ] 3. The label-overlap check false-positives on scrim and tap-to-dismiss siblings. Skip full-screen scrim nodes (whole-window bounds, no text) as overlap partners. Test: a fixture tree with a scrim sibling passes; a real overlap still fails.
- [ ] 4. Android and iOS: `IntroWizardPage.PlaceButtons` run twice may leave stale `Grid.Row`/`Grid.Column` (unconfirmed). First confirm with a unit or layout test that a second call changes nothing; if it does leave stale values, reset them at the start of `PlaceButtons`.

## Verification

- Unit tests above green; the emulator layout-check flow passes on the wizard in portrait and landscape; iOS wizard checked on a simulator or TestFlight.
