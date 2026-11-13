# Slice 369 — One release workflow per product, sharing a reusable part

*In progress (wave 2026-11-13), draft PR.* Planned 2026-11-13. Part of the tidy-by-product slices (366-371). User's choice, 2026-11-13 (item 3 of 5).

**Touches:** `.github/workflows/release.yml`, `.github/workflows/release-rtst.yml` (new), `.github/workflows/release-dino.yml` (new), `.github/workflows/release-shared.yml` (new, `workflow_call`), `.github/workflows/dino-store.yml`, `.github/workflows/store-submit.yml`, `.github/workflows/testflight-external.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `.github/scripts/find-release.sh`, `.github/scripts/check-called-workflow-permissions.mjs`, `.github/CLAUDE.md`, `tools/**` scripts that name `release.yml`

## Why

`release.yml` is 992 lines and mentions Dino 189 times; one run computes both versions, builds both apps and submits both to the stores. A Dino-only fix and an RTST-only fix read, run and fail in the same file and the same run (e.g. run 42410230793: an RTST test failure skipped every RTST build while Dino shipped). Per product tags (`rtst-v`, `dino-v`) already exist (Slice 130, Decision 137).

## Rules this must keep (read `.github/CLAUDE.md` before starting)

- Only changed platforms are rebuilt and submitted (`ship-targets.mjs`); docs-only merges cut nothing.
- Actions storage and minutes quota: no build-output artifacts, Release assets only; path filters per platform.
- Wave merges carry the skip-CI marker and release in one batched `workflow_dispatch`; that batch must still release both products (one dispatch that starts both, or one per product, never a lost product).
- Versions never go backwards; Android `versionCode` keeps rising.
- iOS signing, Apple and Play secrets stay repo-level and are shared.
- A merge cancelling an in-flight release is fine; the two product workflows get separate concurrency groups so one product never cancels the other's run.

## Work

- [ ] 1. Map every job and step of `release.yml` to RTST, Dino or Shared (version calc, tests, packs check, each platform build, each store submit, GitHub Release, tag). Write the map into this file before changing anything.
- [ ] 2. `release-shared.yml` (`workflow_call`): the steps both need (checkout, GitVersion with the product's config, tests, `ship-targets` decision, GitHub Release creation with the product's tag prefix).
- [ ] 3. `release-rtst.yml` and `release-dino.yml`: push to `main` with their own path filters, plus `workflow_dispatch`; each calls the shared part and then only its own build and submit jobs. Shared code paths trigger both.
- [ ] 4. Retire `release.yml` (removed, not kept as a copy). Update every caller and reader: `dino-store.yml`, `store-submit.yml`, `testflight-external.yml`, `find-release.sh`, `check-called-workflow-permissions.mjs`, the `run-wave` batched release dispatch (ClaudeSkills) and `tools/` scripts that name `release.yml`.
- [ ] 5. `ship-targets.mjs` returns targets per product; `ship-targets.test.mjs` covers: Dino-only change runs only the Dino workflow's builds; tray-only change only RTST; shared engine change both; docs-only none.
- [ ] 6. `.github/CLAUDE.md`: replace the `release.yml` rules with the new file names.

## Verification

- `actionlint` and the `.github/scripts` tests pass.
- First merge after this slice: a Dino-only PR starts only `release-dino.yml` and cuts only a `dino-v` tag; the following RTST-only PR only `release-rtst.yml` and an `rtst-v` tag (read with `gh run list` and `gh release list`). Record both run ids here.
- One batched `workflow_dispatch` from a wave releases both products.
- No version goes backwards (compare the new tags with the last `rtst-v` and `dino-v`).
