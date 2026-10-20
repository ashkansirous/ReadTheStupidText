# Slice 70 — Split the build pipeline into the Lets-Call-Mom shape

*In progress (wave 2026-10-20d), draft PR.*

**Batch:** 11 (Lets-Call-Mom parity)
**Depends on:** Slice 69 (the publish step moves with the Android workflow)
**Touches:** `.github/workflows/**`, `.github/scripts/**`, `GitVersion.yml`, `CLAUDE.md`, `README.md`, `docs/DEPLOYING.md`
**Story issue:** —

## Goal

`build.yml` is replaced by the structure Lets-Call-Mom uses. On a push to `main`, a
`release.yml` decides which platforms changed and calls one reusable workflow per platform.
Each platform workflow also runs on its own pull requests and by hand. The decision logic
lives in a unit-tested script, not in YAML path filters.

## Scope

- [ ] `.github/scripts/ship-targets.mjs` + `ship-targets.test.mjs`: from the changed paths
      and the semver bump, decide which targets to build (windows, android, dinoreader; ios
      once Slices 48/49 land). A major bump builds everything. Port from Lets-Call-Mom.
- [ ] `release.yml` (push to `main`): `version` job (GitVersion, unchanged rules) → `plan`
      job (ship-targets) → conditional `workflow_call` to each platform → draft-release /
      publish / `store-submit.yml`, as today.
- [ ] `windows.yml`, `android.yml` (now holding the Slice 69 R2 publish), `dinoreader.yml`,
      `tests.yml`: each `workflow_call` + `pull_request` (own paths) + `workflow_dispatch`.
- [ ] `release-scripts.yml`: runs the `.github/scripts/*.test.mjs` tests on PRs touching
      `.github/scripts/**`.
- [ ] Keep Decision 17's invariants: GitVersion trunk mode, `+semver` markers, the Android
      versionCode formula, `^v\d+\.\d+\.\d+$` tags, no `upload-artifact` for the ~1 GB packages,
      assets carried forward from the previous release.
- [ ] Keep `packs.yml` and `store-submit.yml` as they are, apart from the caller.
- [ ] Delete `build.yml` once one full release has gone through `release.yml`.

## Done when

- A PR touching only `src/ReadTheStupidText.App/**` runs only the Windows workflow.
- A merge to `main` with a patch bump builds only the changed platforms, and the draft
  release still carries the unchanged platform's assets forward.
- A `+semver: major` merge builds every target. (Write the marker only as a directive, never in prose.)
- `ship-targets` tests pass in CI.

## Notes

- Do this after Slice 69 so the publish step is written once, straight into `android.yml`.
- Reference: Lets-Call-Mom `.github/workflows/release.yml`, `.github/scripts/ship-targets.mjs`.
