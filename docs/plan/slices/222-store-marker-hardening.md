### Slice 222 — Microsoft Store marker: findings of the rerun Slice 218 medium review

*Built 2026-11-01 (wave 7, night 2026-11-01-0859), PR #667, not merged; the guards are not yet verified on a real store-submit run.* Written 2026-10-30 by the round 1 feedback loop ([note](../notes/2026-10-30-2100-feedback-loop-w1.md)) and parked; the user moved it to `planned` on 2026-11-01. Run 41137137107 finished and created `rtst-store-v1.77.0`. Slice 218 merged on an incomplete medium pass; the loop reran it on the merged diff (`050e076^1..050e076`) and it found the items below. None is proven yet: each is read from the code, and the reviewer wrote that it did not verify 1-4 and 7 itself; it also said its fork partly read the wrong base (two out-of-diff findings were dropped), so recheck each against `050e076^1..050e076` before fixing. The force_all run 41137137107 (in progress at 22:30 UTC) is the first real test of items 1-3. Check its `store-submit-mark` job first.

**Touches:** `.github/workflows/release.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `.github/CLAUDE.md`, `docs/plan/slices/222-store-marker-hardening.md`

#### Work

1. **The mark job's tag lookup may fail** (`release.yml:745`, medium): `store-submit-mark` reads `git/ref/tags/${RELEASE_TAG}`. The reviewer said the tag may not exist yet; but `store-submit-mark` needs `store-submit`, which needs `release-publish-rtst` (the job that creates the tag), so that half is probably wrong. A Dino-only run is the open case: if the lookup returns 404 there, the job fails under `set -e`, no marker is written, and later releases submit again.
   - [x] First read run 41137137107's `store-submit-mark` log. If it failed, find why from the log (not from this text), and point the marker at the commit rather than the tag if the tag lookup is the cause. Add a test or an actionlint-visible dependency.
2. **A marker at another commit is never moved** (`release.yml:735`, medium): the existing-tag check only handles the same sha; a rerun of an old release could move the marker backwards, and a different sha leaves no new marker.
   - [x] Move the marker forward only (never to an older commit). Test both cases in `ship-targets.test.mjs` or a workflow-level check.
3. **A Dino-only run with a pending Store change** (`ship-targets.mjs:372`, medium, unproven): the pending change turns `windows` on, but the reviewer thinks `rtst_version` may not be set in a Dino-only run, so the MSIX could ship with no matching `rtst-v` release, or the mark job tags nothing.
   - [x] A test: a Dino-only diff with a pending Store change gives a consistent RTST version and release, or holds the Store change for the next RTST release.
4. **No first marker** (`ship-targets.mjs:363`, medium; same as the high pass's skipped 1): until a marker exists, the old release-diff rule applies. Today only the force_all run seeds it.
   - [x] After item 1, confirm the force_all run created `rtst-store-v<x.y.z>`; if not, seed it once by hand-run dispatch (the user's call while live state is involved).

**User answers (2026-11-01):** item 4 is moot: the tags `rtst-store-v1.77.0` and `rtst-store-v1.79.0` exist on origin, so no hand-seeded dispatch is needed.
5. Low: `storeChangesFor` returns `console.log(...)` (undefined) as its "no data" signal (`ship-targets.mjs:397`). Return an explicit value.

Out of scope here: the review also flagged the duplicated pre-1.0 version rule in `worker/events.ts` and `UsageEligibility.cs` (Slice 219, outside this diff).

#### Verification

- `node --test .github/scripts/*.test.mjs` with the new cases.
- The next release after a Windows change creates exactly one marker at its commit.

#### Recheck result (2026-11-01, against `050e076^1..050e076`)

- Run 41137137107: `gh run view` and the jobs API both return 404 (id unknown to the repo, or the run was expired), so its `store-submit-mark` log could not be read. Evidence instead: `rtst-store-v1.77.0` and `rtst-store-v1.79.0` exist and point at the same commit as `rtst-v1.77.0` / `rtst-v1.79.0`, so the mark job ran twice and its tag lookup worked.
- Item 1 does not hold: `store-submit` needs `release-publish-rtst.outputs.released == 'true'`, which publishes (and so creates) `rtst-v<version>` before the mark job runs. A Dino-only run with no pending Store change has `windows` false, so `store-submit` and the mark job are skipped. Not changed.
- Item 2 holds in a narrow form (a rerun finding a marker at another sha failed on the POST with 422; an older version's marker could be added). Fixed in the `store-submit-mark` step: an existing marker at another sha is left alone with a warning, and an older version never adds a marker. `newestAppTag` already takes the highest version, so the base never goes back. Stale reruns were already stopped earlier by `release-publish-rtst`'s stale-draft check.
- Item 3 does not hold: GitVersion computes `rtst_version` for every run (commits since the last `rtst-v` tag), so a Dino-only run with a pending Store change gets draft, MSIX, release `rtst-v<next>` and a matching marker. Test added to pin the flags.
- Item 4 moot (user). Item 5 fixed: `storeChangesFor` returns an explicit `undefined`.

*Changed while building:* items 1 and 3 did not hold and item 4 is moot, so only items 2 and 5 changed code. In `store-submit-mark`: an existing marker at another sha is left alone with a warning; a marker older than the newest is not added; `matching-refs` is read with `--paginate`; the lookup treats only `Not Found` / 404 as "absent" (verified live: `gh` prints `Not Found (HTTP 404)`) and fails the job on any other API error; a failed POST re-reads the ref and counts the same sha as success.

*From review* (`reviews/s222-high.md`, `reviews/s222-medium.md`):
- High 1 (no `--paginate` on matching-refs), 2 (API errors read as "not found"), 3 (concurrent create race), 7 (rule missing from `.github/CLAUDE.md`): applied.
- High 4 (Dino-only test title/assertion wording), 5 (workflow-text test uses regexes), 6 (`sort -V` vs strict parser on non-`x.y.z` refs), 8 (`storeChangesFor` style, two statements on one line): skipped, test wording/style or non-GitVersion tags that this repo does not create.
- Medium 1 (older rerun exits 0 with a warning and writes no marker): skipped, intended behaviour.
- Medium 2 (`Not Found` matched as text in `2>&1` output): skipped, edge case; the text match was kept after the live check above.
- Medium 3 (prerelease-shaped marker tag ignored by the newest-wins guard): skipped, marker tags are always `x.y.z`.
- Medium 4 (workflow-guard test is regex-based), 5 (Dino-only test mutates the plan by hand), 6 (duplicated log-and-return in `storeChangesFor`): skipped, style.

*Checks:* `node --test .github/scripts` 183/183, actionlint clean. No device stage (CI only).

#### For CLAUDE.md

- Store marker is forward-only: `rtst-store-v<x.y.z>` is never moved or hand-seeded, and a release older than the newest marker never adds one. Already written into `.github/CLAUDE.md`'s Store-flag rule (Slice 222: docs/plan/slices/222-store-marker-hardening.md).

#### Not yet verified

- The marker guards' behaviour on the next real store-submit run (the first on main after this merges).
