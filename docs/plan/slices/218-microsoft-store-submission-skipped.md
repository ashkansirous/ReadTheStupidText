### Slice 218 — RTST Microsoft Store submission skipped Windows code changes

*In progress (wave 2026-10-30), draft PR.* Planned 2026-10-30. Found while checking where RTST 1.74.0+ is available.

**Touches:** `.github/scripts/ship-targets.mjs`, `.github/workflows/release.yml`, `.github/CLAUDE.md`, `tests/**` or `.github/scripts/*.test.*` (the ship-targets tests), `docs/plan/slices/218-microsoft-store-submission-skipped.md`

#### Why

- `store-submit` runs only when `windowsstore` is true, which `.github/CLAUDE.md` says covers App/Infrastructure/`store-submit.yml`/`windows.yml` changes.
- Slice 201 (PR #571) changed `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs` and `WindowsVoicePackService.cs`, yet the RTST `store-submit` job did not run in any release from rtst-v1.74.0 to rtst-v1.76.1 (release runs from 40986857439 on). So the Windows fix never went to the Microsoft Store, and Windows users stay on old versions (30-day usage: almost all 1.67.1).
- Not known yet: whether `ship-targets.mjs` missed the Infrastructure paths, whether the release that carried #571 was cut by a later merge whose own diff had no Windows paths (the release diffs from the last tag, or only the merge?), or something else.

#### Work

- [x] Find the cause: run `ship-targets.mjs` against the diff of each release run from rtst-v1.73.2 to rtst-v1.76.1 and see what `windowsstore` it gives; read how the base of that diff is chosen.
- [x] Fix so any release whose changes since the last **submitted** Windows version include Windows code submits (base the diff on the last Store-submitted tag, not the previous release).
- [x] A test for the case: a core release followed by an Infrastructure change and then another core release.
- [ ] Then submit the current RTST Windows build once (`release.yml` `force_all`, standing practice for store releases) so Windows users get the fixes since 1.67.1. User, 2026-10-30 (run-all-waves start): yes, submit tonight once the fix merges; the force_all run also rebuilds Android (Play internal) and iOS (internal TestFlight), which is fine. Watch it until the Store submission is accepted.

*Built 2026-10-30, not yet merged. The force_all submission above is still to do after the merge.*

*Update 2026-10-30 (checked with `gh`):* the user approved the `force_all` run at the start of the run (the night's parked note is superseded). The interactive session started it at 22:10 UTC ([run 41136299215](https://github.com/ashkansirous/ReadTheStupidText/actions/runs/41136299215)); it ended `cancelled` with no jobs, because the push of the index PR #634 merge at 22:12 started release run 41136558112 in the same concurrency group. So the Store submission has not happened yet. Remaining: rerun `release.yml` with `force_all` when no merge is about to land, then confirm the first `rtst-store-v` marker tag and Partner Center showing the new version. Not waiting on a user decision.

**Cause found (from release run logs):** releases rtst-v1.74.0 to rtst-v1.76.1 were core-only diffs, so `windowsstore=false` by design. #571 shipped in rtst-v1.73.2 (run 40981417896) and `store-submit` printed "Submission commit success". Why Windows users stay on 1.67.1 is **not proven**: certification may have stalled or failed after the commit, or commit does not mean published. Partner Center has not been read yet.

**Latent bug fixed:** the Store flag was measured from the last release tag, so a Windows change in a cancelled or failed release was never submitted later.

*Changed while building:*

- `ship-targets.mjs` now measures the Store flag from the newest `rtst-store-v<x.y.z>` marker tag. It falls back to the old release diff when there is no marker or the diff is unreadable. A pending Store change also turns `windows` on.
- New `release.yml` job `store-submit-mark` (`contents: write`, idempotent) creates the marker tag after `store-submit` succeeds.
- Dino unchanged: `dinostore` is the book upload, and `store-submit-dino` stays gated on `dinowin`.
- Tests: `node --test .github/scripts/*.test.mjs`, 180 pass, including git-fixture cases.
- Known behaviour: a manual `store-submit.yml` retry creates no marker, so the next release resubmits.
- The force_all run after merge seeds the first marker.

*From review:*

- High: findings 2, 3 and 6 applied. The Dino half of 8 was applied, then removed as wrong. 1, 4, 5, 7 and 9 skipped.
- Medium (read the diff directly, no review fork): finding 3 applied; 1, 2 and 4 skipped.

**Waiting on the user (2026-10-30):** the force_all release run (the Store submission the user approved) was denied by the night's permission check. Run `release.yml` with `force_all`, then confirm the first `rtst-store-v` marker tag and that Partner Center shows the new version. Why users stayed on 1.67.1 is still unproven (1.73.2 was committed to Partner Center).

#### Verification

- Fast: the ship-targets test.
- CI: the next release with a Windows change runs `store-submit`; Partner Center shows the new version.

#### For CLAUDE.md (lifted 2026-10-30)

- Already edited in `.github/CLAUDE.md` in this PR (Slice 218: docs/plan/slices/218-microsoft-store-submission-skipped.md).

#### Not yet verified

- actionlint on the new `store-submit-mark` job (CI runs it).
- The first `rtst-store-v<x.y.z>` marker tag is created after the force_all run.
- Partner Center shows the new version, and why 1.67.1 stayed live after rtst-v1.73.2's "Submission commit success".
