# Slice 297 — Slice 267 follow-ups (read_voice count, layout-check leaks, wizard button grid)
**Product:** Shared

*Built 2026-11-07 (wave 2026-11-07, PR #926, merged as 8abf2632); iOS wizard and read_voice on iOS not yet verified.* Planned 2026-11-07. Four items left by [Slice 267](267-dino-old-voice-and-wizard-buttons.md) (built, #826). Moved from plan.md's *Outstanding work*. Items 1 and 4 are app code (the same MAUI code on Android and iOS); items 2 and 3 are emulator tooling.

**Touches:** `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/PendingVoiceCount.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `tools/emulator/layout-check.ps1`, `tools/emulator/layout-check.lib.ps1`, `tools/emulator/run-flows.ps1`, `tools/emulator/test-layout-check.ps1`, `tests/DinoReader.Tests/**`, `docs/plan/slices/297-slice-267-follow-ups.md`

## Work

- [x] 1. Android and iOS: `read_voice` is counted before the read really starts (`ReadingSession.cs:570`; the flag `_voiceCountedFor` is at line 69, the report at line 625). Count it once the first piece plays, so a read that fails to start counts nothing. Test: a read that fails before audio sends no `read_voice`.
- [x] 2. `layout-check.ps1` is dot-sourced by `run-flows.ps1`, so its `param()` and `$ErrorActionPreference` leak into the runner. Run it as a child script or move its functions to a module with no `param()`/preference side effects. Test in `test-layout-check.ps1`: the caller's `$ErrorActionPreference` is unchanged after the call.
- [x] 3. The label-overlap check false-positives on scrim and tap-to-dismiss siblings. Skip full-screen scrim nodes (whole-window bounds, no text) as overlap partners. Test: a fixture tree with a scrim sibling passes; a real overlap still fails.
- [x] 4. Android and iOS: `IntroWizardPage.PlaceButtons` run twice may leave stale `Grid.Row`/`Grid.Column` (unconfirmed). First confirm with a unit or layout test that a second call changes nothing; if it does leave stale values, reset them at the start of `PlaceButtons`.

## Verification

- Unit tests above green; the emulator layout-check flow passes on the wizard in portrait and landscape; iOS wizard checked on a simulator or TestFlight.

*Changed while building:*
- Item 1: `read_voice` is counted at the first premade clip, through the new `PendingVoiceCount.cs` (a pending record per read id, holding the reading language at start time; cleared on stop, fail, finish and new read; the book is claimed only after `ReportVoiceUsed`; the count runs on the main thread). `VoiceUsed` now carries the language and `ReadingPage.OnVoiceUsed` uses it. The demo flag is not carried: the session has no demo notion, so the page reads `_demoRead` while subscribed.
- Item 2: the pure functions moved to `tools/emulator/layout-check.lib.ps1`; `layout-check.ps1` is a thin CLI with `param()`; `run-flows.ps1` and the test dot-source the lib. A malformed dump exits 2.
- Item 3: a full-window clickable with no text is ignored as an overlap partner only when it is earlier in dump order.
- Item 4: no code change (`PlaceButtons` resets rows and columns); pinned by a source test.

*From review* (files in `D:/ReadTheStupidText-worktrees/reviews/`): high 2, 3, 4, 5, 7, 8 applied; medium 2, 3, 6 applied; check1 1, 2, 3 applied; check2 1-5 all low, none applied.
- Partly applied high #8 (`reviews/s297-high.md`): "carry reading language (and demo flag) in the pending record" - the language is carried; the demo flag is not (the fixer left it out: the session has no demo notion, so the page reads `_demoRead` while subscribed). A read whose demo state changes between the first clip and the count is classified by the page's value at that moment (check 2 #4).
- Skipped #1 (`reviews/s297-high.md`): "make VoiceToCount a sealed class or use ReferenceEquals" - low; "harmless today since ReadId is unique" (the same review file).
- Skipped #6 (`reviews/s297-high.md`): "test only counts" `SetCell(` substrings - low; item 4 needed no code change (`PlaceButtons` resets rows and columns), and the test only pins that.
- Skipped #1 (`reviews/s297-medium.md`): "under-reported until a later piece plays" - the count is deliberately tied to the first played clip (item 1), so a book whose clips all fail counts nothing.
- Skipped #4 (`reviews/s297-medium.md`): "tests assert on source-text positions" - low; accepted, the behaviour of the pending record is unit-tested separately.
- Skipped #5 (`reviews/s297-medium.md`): "Intended tradeoff, low risk" - the review's own verdict on the scrim rule (item 3).
- Skipped #4 (`reviews/s297-check1.md`): "odd layouts may hide overlaps" - low; the same accepted tradeoff as medium #5.
- Skipped #5 (`reviews/s297-check1.md`): "Get-LayoutProblems throws on a malformed dump" - low-medium; no reason was recorded when it was left (feedback loop 2026-11-07-1412, entry 1098), so it stays open for the next layout-check change.
- Skipped #1 (`reviews/s297-check2.md`): "arguably right; add a Volatile.Read" - the clip did play, so counting the read is arguably right.
- Skipped #2 (`reviews/s297-check2.md`): "stale record can linger until the next Clear" - Take is keyed by the read id, so a stale record is never counted wrongly.
- Skipped #3 (`reviews/s297-check2.md`): "safe today because main-thread only" - not a defect now.
- Skipped #4 (`reviews/s297-check2.md`): "could misclassify a demo read if it changed in between" - low; the known demo-flag limit in the first line above.
- Skipped #5 (`reviews/s297-check2.md`): "no test drives Take + Stop + hop" - all check 2 findings are low and optional (the review's wording).

*Verified:* DinoReader.Tests 3875 pass; `test-layout-check.ps1` passes on pwsh and Windows PowerShell 5.1; Android Debug and iOS compile 0 errors. Emulator (build e84ff515add9): `premade-audio-plays` PASS with the log "[AudioPack] first clip plays, the voice is counted (book d81e..., reading en)" right after the clip start; `first-run` PASS; `wizard-layout-record-large-font` (layout-check) PASS. Logs in `reviews/wave40-device/s297/`.

#### For CLAUDE.md (lifted 2026-11-07)

None.

#### Not yet verified

- iOS wizard layout (no Mac or iPhone tonight).
- `read_voice` counted at the first premade clip on iOS.
- Dino Windows head not built.
