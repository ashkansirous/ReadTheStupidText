### Slice 238 — packs.yml version defaults match the live catalog

*Built 2026-11-02, not merged; the new check has not run in CI yet.* Written 2026-11-01 by the round 3 feedback loop ([note](../notes/2026-11-01-0859-feedback-loop-w3.md)). Parked until 2026-11-01 by the round 3 feedback loop: waits for the user; moved back to planned by the night's final feedback loop.

**Touches:** `.github/workflows/packs.yml`, `.github/scripts/` (a new defaults check and its test), the one workflow that runs it (`.github/workflows/release-scripts.yml`, its `test` job; widened at claim, 2026-11-01 night; `tests.yml` not used because editing it ships every app; `src/ReadTheStupidText.Application/Reading/PackConstants.cs` is read by the check and added to that workflow's paths, not edited), `.github/CLAUDE.md`, `docs/plan/slices/238-packs-workflow-defaults-match-catalog.md`

#### Why

`packs.yml`'s `workflow_dispatch` input `norwegian_pack_version` defaults to `'1'`, but the live packs release (packs-11, read 2026-11-01) ships `piper-nb-2.zip`. A packs run started with the defaults would stamp the Norwegian pack back to version 1, so phones that already hold version 2 might not see an update, or the catalog would go backwards. The wave 3 builder of Slice 220 reported the gap (wave 3 digest entry 264); nothing tracked it. The other `*_pack_version` defaults were not compared one by one.

#### Work

1. - [x] Set every `*_pack_version` default in `packs.yml` to the version the newest `packs-<n>` release ships for that pack.
   Verify: list `gh release view <newest packs tag> --json assets` and compare each pack's `-<version>.zip` with the defaults; all match.
2. - [x] A small check (script plus test, run in `tests.yml` or `actionlint`'s job when `packs.yml` changes) that fails when a default is lower than the version in the `packs.json` of the release the app pins (`PackConstants.CatalogUrl` names the release tag, packs-11 today; the versions live in that release's `packs.json`).
   Verify: lower one default in a test fixture and the check fails; the real file passes.
3. - [x] `.github/CLAUDE.md`: one line saying a pack version bump changes the matching `packs.yml` default in the same PR.

*Changed while building:*

- packs-11 is the newest release; only `norwegian_pack_version` was wrong (`'1'` changed to `'2'`); the other defaults already matched.
- The check is `.github/scripts/check-packs-defaults.mjs` (+ `.test.mjs`). It fetches the `packs.json` of the release tag named by `PackConstants.CatalogUrl` (20 s timeout, 3 tries) and fails on: a default lower than the catalog version, an input with no matching pack, a catalog pack matched by no input, a non-numeric catalog version, or zero defaults read.
- It runs in `release-scripts.yml`'s `test` job (not `tests.yml`, which would ship every app). That workflow's `paths` now include `src/ReadTheStupidText.Application/Reading/PackConstants.cs`, so a CatalogUrl bump triggers it.
- Checks: `node --test .github/scripts/*.test.mjs` 223 pass; live run against packs-11: 0 problems; actionlint clean.

*From review:* (files read: `s238-high.md` 8/8, `s238-medium.md` 7/7, `s238-check-medium.md` 7/7)

- High 1 (PackConstants.cs missing from the workflow paths): applied.
- High 2 (catalog pack matched by no input): applied.
- High 3 (bare fetch, no timeout or retry): skipped as a separate item; covered later by the timeout and retry added for medium 1 and 2.
- High 4 (`process.exit` after fetch, claimed exit 127 on Windows): not reproduced by the reviewer; skipped.
- High 5 (non-numeric catalog version): applied.
- High 6 (zero defaults read passes): applied.
- High 7 (WORKFLOW_PATH/CONSTANTS_PATH resolve against cwd): skipped, low; CI runs from the repo root.
- High 8 (network step shares the `test` job): skipped, low.
- Medium 1 and 2 (no retry, no timeout): applied as a 20 s timeout and 3 tries. No soft-fail on fetch failure, so the check never passes without comparing.
- Medium 3-7 (comparison against older pinned catalog by design; trailing YAML comment in `default:`; mapped input with no catalog pack is a hard failure; shared default must be at least the max; cwd paths): skipped, low.
- Check-medium 1 (soft-fail on fetch failure): skipped; repeats the outage point, and a long GitHub outage still fails the job by design.
- Check-medium 2-6 (one-way check, retry on permanent errors, regex strictness, cwd paths, hard-coded prefixes): skipped, low.
- Check-medium 7 (Touches line missing PackConstants.cs): fixed.

#### For CLAUDE.md

- (`.github/CLAUDE.md`, already added in this slice) A pack version bump changes the matching `packs.yml` `*_pack_version` default in the same PR; `check-packs-defaults.mjs` in `release-scripts.yml` fails a default lower than the `packs.json` of the release `PackConstants.CatalogUrl` pins. (Slice 238: docs/plan/slices/238-packs-workflow-defaults-match-catalog.md)

#### Not yet verified

- The check's first run in CI on the PR.
- What a lower version does on phones: Slice 220's record says a re-download is decided by `PackVersions.IsNewer` only, so a phone holding piper-nb 2 would most likely keep it and a fresh install would get 1; not checked on a device.
