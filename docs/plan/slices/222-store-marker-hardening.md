### Slice 222 — Microsoft Store marker: findings of the rerun Slice 218 medium review

*Planned.* Written 2026-10-30 by the round 1 feedback loop ([note](../notes/2026-10-30-2100-feedback-loop-w1.md)) and parked; the user moved it to `planned` on 2026-11-01. Run 41137137107 finished and created `rtst-store-v1.77.0`. Slice 218 merged on an incomplete medium pass; the loop reran it on the merged diff (`050e076^1..050e076`) and it found the items below. None is proven yet: each is read from the code, and the reviewer wrote that it did not verify 1-4 and 7 itself; it also said its fork partly read the wrong base (two out-of-diff findings were dropped), so recheck each against `050e076^1..050e076` before fixing. The force_all run 41137137107 (in progress at 22:30 UTC) is the first real test of items 1-3. Check its `store-submit-mark` job first.

**Touches:** `.github/workflows/release.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `.github/CLAUDE.md`, `docs/plan/slices/222-store-marker-hardening.md`

#### Work

1. **The mark job's tag lookup may fail** (`release.yml:745`, medium): `store-submit-mark` reads `git/ref/tags/${RELEASE_TAG}`. The reviewer said the tag may not exist yet; but `store-submit-mark` needs `store-submit`, which needs `release-publish-rtst` (the job that creates the tag), so that half is probably wrong. A Dino-only run is the open case: if the lookup returns 404 there, the job fails under `set -e`, no marker is written, and later releases submit again.
   - [ ] First read run 41137137107's `store-submit-mark` log. If it failed, find why from the log (not from this text), and point the marker at the commit rather than the tag if the tag lookup is the cause. Add a test or an actionlint-visible dependency.
2. **A marker at another commit is never moved** (`release.yml:735`, medium): the existing-tag check only handles the same sha; a rerun of an old release could move the marker backwards, and a different sha leaves no new marker.
   - [ ] Move the marker forward only (never to an older commit). Test both cases in `ship-targets.test.mjs` or a workflow-level check.
3. **A Dino-only run with a pending Store change** (`ship-targets.mjs:372`, medium, unproven): the pending change turns `windows` on, but the reviewer thinks `rtst_version` may not be set in a Dino-only run, so the MSIX could ship with no matching `rtst-v` release, or the mark job tags nothing.
   - [ ] A test: a Dino-only diff with a pending Store change gives a consistent RTST version and release, or holds the Store change for the next RTST release.
4. **No first marker** (`ship-targets.mjs:363`, medium; same as the high pass's skipped 1): until a marker exists, the old release-diff rule applies. Today only the force_all run seeds it.
   - [ ] After item 1, confirm the force_all run created `rtst-store-v<x.y.z>`; if not, seed it once by hand-run dispatch (the user's call while live state is involved).

**User answers (2026-11-01):** item 4 is moot: the tags `rtst-store-v1.77.0` and `rtst-store-v1.79.0` exist on origin, so no hand-seeded dispatch is needed.
5. Low: `storeChangesFor` returns `console.log(...)` (undefined) as its "no data" signal (`ship-targets.mjs:397`). Return an explicit value.

Out of scope here: the review also flagged the duplicated pre-1.0 version rule in `worker/events.ts` and `UsageEligibility.cs` (Slice 219, outside this diff).

#### Verification

- `node --test .github/scripts/*.test.mjs` with the new cases.
- The next release after a Windows change creates exactly one marker at its commit.
