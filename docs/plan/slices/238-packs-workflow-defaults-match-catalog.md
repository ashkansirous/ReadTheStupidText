### Slice 238 — packs.yml version defaults match the live catalog

*In progress (wave 2026-11-01), draft PR.* Written 2026-11-01 by the round 3 feedback loop ([note](../notes/2026-11-01-0859-feedback-loop-w3.md)). Parked until 2026-11-01 by the round 3 feedback loop: waits for the user; moved back to planned by the night's final feedback loop.

**Touches:** `.github/workflows/packs.yml`, `.github/scripts/` (a new defaults check and its test), the one workflow that runs it (`.github/workflows/tests.yml` or the actionlint workflow; widened at claim, 2026-11-01 night), `.github/CLAUDE.md`, `docs/plan/slices/238-packs-workflow-defaults-match-catalog.md`

#### Why

`packs.yml`'s `workflow_dispatch` input `norwegian_pack_version` defaults to `'1'`, but the live packs release (packs-11, read 2026-11-01) ships `piper-nb-2.zip`. A packs run started with the defaults would stamp the Norwegian pack back to version 1, so phones that already hold version 2 might not see an update, or the catalog would go backwards. The wave 3 builder of Slice 220 reported the gap (wave 3 digest entry 264); nothing tracked it. The other `*_pack_version` defaults were not compared one by one.

#### Work

1. - [ ] Set every `*_pack_version` default in `packs.yml` to the version the newest `packs-<n>` release ships for that pack.
   Verify: list `gh release view <newest packs tag> --json assets` and compare each pack's `-<version>.zip` with the defaults; all match.
2. - [ ] A small check (script plus test, run in `tests.yml` or `actionlint`'s job when `packs.yml` changes) that fails when a default is lower than the version in the `packs.json` of the release the app pins (`PackConstants.CatalogUrl` names the release tag, packs-11 today; the versions live in that release's `packs.json`).
   Verify: lower one default in a test fixture and the check fails; the real file passes.
3. - [ ] `.github/CLAUDE.md`: one line saying a pack version bump changes the matching `packs.yml` default in the same PR.

#### Not yet verified

- What a lower version does on phones: Slice 220's record says a re-download is decided by `PackVersions.IsNewer` only, so a phone holding piper-nb 2 would most likely keep it and a fresh install would get 1; not checked on a device.
