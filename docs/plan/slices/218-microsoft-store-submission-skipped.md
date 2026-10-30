### Slice 218 — RTST Microsoft Store submission skipped Windows code changes

*In progress (wave 2026-10-30), draft PR.* Planned 2026-10-30. Found while checking where RTST 1.74.0+ is available.

**Touches:** `.github/scripts/ship-targets.mjs`, `.github/workflows/release.yml`, `.github/CLAUDE.md`, `tests/**` or `.github/scripts/*.test.*` (the ship-targets tests), `docs/plan/slices/218-microsoft-store-submission-skipped.md`

#### Why

- `store-submit` runs only when `windowsstore` is true, which `.github/CLAUDE.md` says covers App/Infrastructure/`store-submit.yml`/`windows.yml` changes.
- Slice 201 (PR #571) changed `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs` and `WindowsVoicePackService.cs`, yet the RTST `store-submit` job did not run in any release from rtst-v1.74.0 to rtst-v1.76.1 (release runs from 40986857439 on). So the Windows fix never went to the Microsoft Store, and Windows users stay on old versions (30-day usage: almost all 1.67.1).
- Not known yet: whether `ship-targets.mjs` missed the Infrastructure paths, whether the release that carried #571 was cut by a later merge whose own diff had no Windows paths (the release diffs from the last tag, or only the merge?), or something else.

#### Work

- [ ] Find the cause: run `ship-targets.mjs` against the diff of each release run from rtst-v1.73.2 to rtst-v1.76.1 and see what `windowsstore` it gives; read how the base of that diff is chosen.
- [ ] Fix so any release whose changes since the last **submitted** Windows version include Windows code submits (base the diff on the last Store-submitted tag, not the previous release).
- [ ] A test for the case: a core release followed by an Infrastructure change and then another core release.
- [ ] Then submit the current RTST Windows build once (`release.yml` `force_all`, standing practice for store releases) so Windows users get the fixes since 1.67.1. User, 2026-10-30 (run-all-waves start): yes, submit tonight once the fix merges; the force_all run also rebuilds Android (Play internal) and iOS (internal TestFlight), which is fine. Watch it until the Store submission is accepted.

#### Verification

- Fast: the ship-targets test.
- CI: the next release with a Windows change runs `store-submit`; Partner Center shows the new version.
