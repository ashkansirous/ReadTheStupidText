### Slice 218 — RTST Microsoft Store submission skipped Windows code changes

*Built 2026-10-30, merged (PR #630, 050e076); the force_all Store run (41137137107) submitted rtst-v1.77.0; Partner Center not yet verified.* Planned 2026-10-30. Found while checking where RTST 1.74.0+ is available.

**Touches:** `.github/scripts/ship-targets.mjs`, `.github/workflows/release.yml`, `.github/CLAUDE.md`, `tests/**` or `.github/scripts/*.test.*` (the ship-targets tests), `docs/plan/slices/218-microsoft-store-submission-skipped.md`

#### Why

- `store-submit` runs only when `windowsstore` is true, which `.github/CLAUDE.md` says covers App/Infrastructure/`store-submit.yml`/`windows.yml` changes.
- Slice 201 (PR #571) changed `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs` and `WindowsVoicePackService.cs`, yet the RTST `store-submit` job did not run in any release from rtst-v1.74.0 to rtst-v1.76.1 (release runs from 40986857439 on). So the Windows fix never went to the Microsoft Store, and Windows users stay on old versions (30-day usage: almost all 1.67.1).
- Not known yet: whether `ship-targets.mjs` missed the Infrastructure paths, whether the release that carried #571 was cut by a later merge whose own diff had no Windows paths (the release diffs from the last tag, or only the merge?), or something else.

#### Work

- [x] Find the cause: run `ship-targets.mjs` against the diff of each release run from rtst-v1.73.2 to rtst-v1.76.1 and see what `windowsstore` it gives; read how the base of that diff is chosen.
- [x] Fix so any release whose changes since the last **submitted** Windows version include Windows code submits (base the diff on the last Store-submitted tag, not the previous release).
- [x] A test for the case: a core release followed by an Infrastructure change and then another core release.
- [x] Then submit the current RTST Windows build once (`release.yml` `force_all`, standing practice for store releases) so Windows users get the fixes since 1.67.1. User, 2026-10-30 (run-all-waves start): yes, submit tonight once the fix merges; the force_all run also rebuilds Android (Play internal) and iOS (internal TestFlight), which is fine. Watch it until the Store submission is accepted.

*Store run (checked with `gh`, 2026-10-30 22:30 UTC):* the user approved the `force_all` run at the start of the run (the night's parked note is superseded). The interactive session started it at 22:10 UTC ([run 41136299215](https://github.com/ashkansirous/ReadTheStupidText/actions/runs/41136299215)); it ended `cancelled` with no jobs, because the push of the index PR #634 merge at 22:12 started release run 41136558112 in the same concurrency group. A second `force_all` dispatch ([41137137107](https://github.com/ashkansirous/ReadTheStupidText/actions/runs/41137137107), 22:17 UTC) was `in_progress` at 22:30 UTC; a running run is not cancelled by later merges (`cancel-in-progress: false`, only a *pending* run is replaced). Remaining: confirm it reaches `store-submit`, the first `rtst-store-v` marker tag and Partner Center showing the new version. Not waiting on a user decision. Found 2026-11-01 (round 2 feedback loop, not proven): `force_all` turns on every target (`.github/scripts/ship-targets.mjs`), and `release.yml` runs `store-submit-dino` when the Dino Windows target is on and `DINOREADER_STORE_APP_ID` is set (it is), so this run most likely also submitted Dino Reader for Windows, which only RTST was approved for. The run's jobs now answer 404; check Dino's Partner Center submission history. (Night rule since then: a dispatch a later merge would cancel waits for the end of the night, ClaudeSkills `run-all-waves`.)

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

- High: findings 2, 3 and 6 applied. The Dino half of 8 was applied, then removed as wrong (Dino's `dinostore` is the book upload; the coordinator asked for it without checking). Skipped:
  - 1: nothing seeds the first `rtst-store-v` marker; the force_all run does (see above).
  - 4: a Store backlog that never clears (submission keeps failing) makes every merge, Dino-only and Android-only too, rebuild the RTST MSIX and retry the Store. Not yet verified, see below.
  - 5: a run cancelled between `store-submit` and `store-submit-mark` can submit the same changes twice. Not yet verified, see below.
  - 7: the marker prefix is written in three places (`STORE_TAG_PREFIX`, `PRODUCTS.rtst.storePrefix`, the literal in `release.yml`); a rename would drift silently. Style, skipped.
  - 9: a missing blank line in the test file. Nit.
- Medium: **incomplete** (it read the diff directly and did not run the review fork); finding 3 applied (comments about "docs-only" releases); skipped 1 (no marker until force_all: the run above), 2 (a manual `store-submit.yml` retry creates no marker: Known behaviour above), 4 (`return console.log(...)` style). A fresh medium pass on the merged diff was run by the round 1 feedback loop ([note](../notes/2026-10-30-2100-feedback-loop-w1.md)).

Why users stayed on 1.67.1 is still unproven (1.73.2 was committed to Partner Center).

#### Verification

- Fast: the ship-targets test.
- CI: the next release with a Windows change runs `store-submit`; Partner Center shows the new version.

#### For CLAUDE.md (lifted 2026-10-30)

- Already edited in `.github/CLAUDE.md` in this PR (Slice 218: docs/plan/slices/218-microsoft-store-submission-skipped.md).

*Store run done (checked with `gh`, 2026-10-31 housekeeping):* run 41137137107 finished; `store-submit / submit`, `store-submit-mark`, `play-submit` and `testflight-submit` all succeeded (the run as a whole shows `cancelled`, but those jobs completed). The marker tag `rtst-store-v1.77.0` exists. The Microsoft Store submission of rtst-v1.77.0 is done; still open: Partner Center showing 1.77.0 once certification finishes.

#### Not yet verified

*actionlint on the new `store-submit-mark` job passed on PR #630 (run 41130690864), checked 2026-10-30.*

- ~~The first `rtst-store-v<x.y.z>` marker tag is created after the force_all run.~~ Done: `rtst-store-v1.77.0` (run 41137137107).
- Partner Center shows the new version, and why 1.67.1 stayed live after rtst-v1.73.2's "Submission commit success".
- High review 4: what a Store backlog that keeps failing costs (an RTST MSIX build on every merge); check the release runs after the first failed submission, if one happens.
- High review 5: no double submission after a run cancelled between `store-submit` and `store-submit-mark`.
- Dino Reader for Windows: read with `gh` by the round 4 feedback loop (2026-11-01, about 21:21). Release run 40992195327 (2026-10-30 02:31 UTC, a push run for PR #587) has `store-submit-dino / submit` green, so Dino was submitted that day. Whether the later `force_all` run 41137137107 also submitted Dino cannot be read (the API answers 404). Certification state was not checked; [Slice 242](242-round-4-leftovers-night-2026-11-01-0859.md) item 1 (parked) would read it.
