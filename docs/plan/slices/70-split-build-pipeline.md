# Slice 70 — Split the build pipeline into the Lets-Call-Mom shape

*Built (wave 2026-10-20d), PR #190. Not yet verified on GitHub: the first push to main after the merge is the first release through release.yml.*

**Batch:** 11 (Lets-Call-Mom parity)
**Depends on:** Slice 69 (the publish step moves with the Android workflow)
**Touches:** `.github/workflows/**`, `.github/scripts/**`, `GitVersion.yml`, `CLAUDE.md`, `README.md`, `docs/DEPLOYING.md`, `STORE.md`, `docs/voice-packs.md`, `.github/actionlint.yaml`
**Story issue:** —

## Goal

`build.yml` is replaced by the structure Lets-Call-Mom uses. On a push to `main`, a
`release.yml` decides which platforms changed and calls one reusable workflow per platform.
Each platform workflow also runs on its own pull requests and by hand. The decision logic
lives in a unit-tested script, not in YAML path filters.

## Scope

- [x] `.github/scripts/ship-targets.mjs` + `ship-targets.test.mjs`: from the changed paths
      and the semver bump, decide which targets to build (windows, android, dinoreader; ios
      once Slices 48/49 land). A major bump builds everything. Port from Lets-Call-Mom.
- [x] `release.yml` (push to `main`): `version` job (GitVersion, unchanged rules) → `plan`
      job (ship-targets) → conditional `workflow_call` to each platform → draft-release /
      publish / `store-submit.yml`, as today.
- [x] `windows.yml`, `android.yml` (now holding the Slice 69 R2 publish), `dinoreader.yml`,
      `tests.yml`: each `workflow_call` + `pull_request` (own paths) + `workflow_dispatch`.
- [x] `release-scripts.yml`: runs the `.github/scripts/*.test.mjs` tests on PRs touching
      `.github/scripts/**`.
- [x] Keep Decision 17's invariants: GitVersion trunk mode, `+semver` markers, the Android
      versionCode formula, `^v\d+\.\d+\.\d+$` tags, no `upload-artifact` for the ~1 GB packages,
      assets carried forward from the previous release.
- [x] Keep `packs.yml` and `store-submit.yml` as they are, apart from the caller.
- [x] Delete `build.yml` (done differently, see Changed while building).

## Done when

- A PR touching only `src/ReadTheStupidText.App/**` runs only the Windows workflow.
- A merge to `main` with a patch bump builds only the changed platforms, and the draft
  release still carries the unchanged platform's assets forward.
- A `+semver: major` merge builds every target. (Write the marker only as a directive, never in prose.)
- `ship-targets` tests pass in CI.

## Notes

- Do this after Slice 69 so the publish step is written once, straight into `android.yml`.
- Reference: Lets-Call-Mom `.github/workflows/release.yml`, `.github/scripts/ship-targets.mjs`.

*Changed while building:*

- `build.yml` was deleted in this PR, not after a first release: its `workflow_dispatch` fallback could not cut a release. `release.yml` gained `workflow_dispatch` with `force_all` (main only) instead.
- `publish-downloads` stays a `release.yml` job after `release-publish` (it reads the APK from the published release), not inside `android.yml`.
- The `plan` job was folded into `version` (a second full clone was wasteful). Job chain: version (GitVersion, Android versionCode, `ship-targets.mjs` -> windows/android/ios/dinoreader outputs) -> test (`tests.yml`; windows or android) -> release-draft (carry-forward, ios_signed, shippable) -> build-windows / build-android / build-ios via reusable `windows.yml` / `android.yml` / `ios.yml` -> release-publish -> store-submit, play-submit, testflight-submit, publish-downloads.
- iOS is included (unsigned compile when iOS changed; signed when proceeding and `ios_signed`). Toolchain pin (dotnet 10.0.401, Xcode 26.6, workload set 10.0.401) carried into `ios.yml`.
- Standalone workflows: `dinoreader.yml`, `site-checks.yml` (Worker typecheck/EOL/tests), `release-scripts.yml` (all script tests on Node 22 plus pinned actionlint 1.7.7 with `.github/actionlint.yaml` declaring macos-26; triggers on `.github/scripts/**` and `.github/workflows/**`).
- `ship-targets.mjs` encodes the old mapping (core -> all; App/Infrastructure -> windows; Mobile/Platforms/Android -> android; Mobile/Platforms/iOS -> ios; rest of Mobile -> android+ios; DinoReader -> dinoreader tests only; docs -> none; major bump or missing base tag -> all). `ship-targets.test.mjs` includes a parity test that every platform workflow's `pull_request.paths` equals the mapping. `dorny/paths-filter` and `path-filters.test.mjs` removed.
- `STORE.md`, `DEPLOYING.md` and `voice-packs.md` updated.

*From review:*

- High: no PR lint for `release.yml` (added an actionlint job); PR-path drift (parity test plus fixes); non-functional `build.yml` fallback (deleted, `force_all` dispatch added); docs; a second full clone (plan folded into version); duplicated test runs; two ship decisions (now one).
- Medium: `STORE.md` references; skip Windows/Android builds when nothing will publish.
- Accepted: called jobs keep `contents: write` (permissions can't be conditional; PRs are the owner's); `publish-downloads.mjs` edits don't re-publish (use `force_all`); iOS-only releases don't run the Windows unit suite (it can't be affected).

Verified: actionlint 0 issues on all workflows; 55 script tests pass.

#### For CLAUDE.md

- CI (Slice 70): `release.yml` is the only workflow on push to main (plus a main-only `workflow_dispatch` with `force_all`): version (GitVersion + versionCode + `.github/scripts/ship-targets.mjs`) -> test (`tests.yml`) -> release-draft -> `windows.yml` / `android.yml` / `ios.yml` (reusable; also run on their own PR paths, never signing or uploading on PRs) -> release-publish -> store-submit, play-submit, testflight-submit, publish-downloads. `ship-targets.mjs` holds the path -> platform mapping (Mobile/Platforms/Android -> Android, Mobile/Platforms/iOS -> iOS, rest of Mobile -> both; core paths incl. `release.yml`, `tests.yml`, `find-release.sh`, `ship-targets.mjs` -> all; major bump -> all); `ship-targets.test.mjs` checks every platform workflow's `pull_request.paths` matches it, so change them together. `release-scripts.yml` runs all script tests and actionlint (`.github/actionlint.yaml`) on PRs touching `.github/**`. Editing a core path ships every app. Never add `upload-artifact`. (Slice 70: docs/plan/slices/70-split-build-pipeline.md)
- This replaces the Batch 8 CI note's `build.yml`/dorny/test-filters wording, the core-path rule's `build.yml` mention, and the CLAUDE.md lines saying `build.yml` packages the MSIX, runs the test job or does it all.

#### For README.md

- Replace "build.yml is the single pipeline..." with one line: `release.yml` runs on push to main and calls a reusable workflow per platform; each platform workflow also runs on its own pull requests.

#### Not yet verified

- The first release through `release.yml` (next merge to main): check every job and that Windows/Android/iOS submissions still happen.
- A PR touching only one platform runs only that platform's workflow.
