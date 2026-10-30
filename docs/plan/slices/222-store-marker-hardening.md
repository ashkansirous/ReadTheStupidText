### Slice 222 — Microsoft Store marker: findings of the rerun Slice 218 medium review

*parked: feedback loop, waits for the user.* Planned 2026-10-30 by the round 1 feedback loop ([note](../notes/2026-10-30-2100-feedback-loop-w1.md)). Slice 218 merged on an incomplete medium pass; the loop reran it on the merged diff (`050e076^1..050e076`) and it found the items below. None is proven yet: each is read from the code. The force_all run 41137137107 (in progress at 22:30 UTC) is the first real test of items 1-3. Check its `store-submit-mark` job first.

**Touches:** `.github/workflows/release.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `.github/CLAUDE.md`, `docs/plan/slices/222-store-marker-hardening.md`

#### Work

1. **The mark job's tag lookup may fail** (`release.yml:745`, medium): `store-submit-mark` reads `git/ref/tags/${RELEASE_TAG}`. If the tag does not exist yet (no `needs` on the job that creates it), or the run is Dino-only, the lookup returns 404, the job fails under `set -e`, no marker is written, and later releases submit again.
   - [ ] First read run 41137137107's `store-submit-mark` log. If it failed this way, make the job wait for the tag job (`needs`) and point the marker at the commit, not the tag. Add a test or an actionlint-visible dependency.
2. **A marker at another commit is never moved** (`release.yml:735`, medium): the existing-tag check only handles the same sha; a rerun of an old release could move the marker backwards, and a different sha leaves no new marker.
   - [ ] Move the marker forward only (never to an older commit). Test both cases in `ship-targets.test.mjs` or a workflow-level check.
3. **A Dino-only run with a pending Store change** (`ship-targets.mjs:372`, medium): the pending change turns `windows` on, but `rtst_version` may not be set in a Dino-only run, so the MSIX could ship with no matching `rtst-v` release, or the mark job tags nothing.
   - [ ] A test: a Dino-only diff with a pending Store change gives a consistent RTST version and release, or holds the Store change for the next RTST release.
4. **No first marker** (`ship-targets.mjs:363`, medium; same as the high pass's skipped 1): until a marker exists, the old release-diff rule applies. Today only the force_all run seeds it.
   - [ ] After item 1, confirm the force_all run created `rtst-store-v<x.y.z>`; if not, seed it once by hand-run dispatch (the user's call while live state is involved).
5. Low: `storeChangesFor` returns `console.log(...)` (undefined) as its "no data" signal (`ship-targets.mjs:397`). Return an explicit value.

Out of scope here: the review also flagged the duplicated pre-1.0 version rule in `worker/events.ts` and `UsageEligibility.cs` (Slice 219, outside this diff).

#### Verification

- `node --test .github/scripts/*.test.mjs` with the new cases.
- The next release after a Windows change creates exactly one marker at its commit.
