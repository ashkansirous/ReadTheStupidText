# CLAUDE.md — CI and release pipeline (.github)

Loaded when you edit workflows or scripts. Complements the root `CLAUDE.md`.

> **CI (Batch 8, Slice 70):** zero `upload-artifact` (the account's storage is 500 MB
> shared across repos, billed as monthly GB-hours; one run's MSIX + AAB/APK is ~1 GB, and
> deleting artifacts doesn't refund it): build jobs upload straight to a **draft GitHub
> Release** (Release assets don't count toward the quota), which `release-publish`
> publishes (creating the tag) once every affected build succeeds; repo artifact
> retention stays at 1 day as a safety net (Slice 42). `release.yml` is the only
> workflow on push to `main` (plus a main-only `workflow_dispatch` with `force_all`):
> version (GitVersion + versionCode + `.github/scripts/ship-targets.mjs`) -> test
> (`tests.yml`) -> release-draft -> `windows.yml` / `android.yml` / `ios.yml` (reusable;
> they also run on their own PR paths, never signing or uploading on PRs) ->
> release-publish -> store-submit, play-submit, testflight-submit, publish-downloads.
> `ship-targets.mjs` holds the path -> platform mapping (`Mobile/Platforms/Android` ->
> Android, `Mobile/Platforms/iOS` -> iOS, rest of `Mobile` -> both; `App`/`Infrastructure`
> -> Windows; core paths -> all; a major bump -> all). `ship-targets.test.mjs` checks
> every platform workflow's `pull_request.paths` matches it, so change them together.
> `release-scripts.yml` runs all script tests and actionlint (`.github/actionlint.yaml`)
> on PRs touching `.github/**`. An untouched platform is neither built nor submitted;
> its assets are carried forward. The iOS PR check builds Debug (simulator); only main does the signed Release (PR #206). When adding a project or shared build file, add its
> path to `ship-targets.mjs`. Never add `actions/upload-artifact` for build outputs.
> (Decisions 52-58, 69; Slice 44: docs/plan/slices/44-pipeline-ios-split.md;
> Slice 70: docs/plan/slices/70-split-build-pipeline.md)

> **Merging a change to a core path ships every app.** The core paths in
> `ship-targets.mjs` include `.github/workflows/release.yml`, `tests.yml`,
> `.github/scripts/find-release.sh`, `ship-targets.mjs`, `*.slnx`, `GitVersion.yml` and
> `global.json`, so a PR that edits any of them (even a Dino Reader-only PR adding a
> project to the solution) builds all platforms, cuts a version and submits to the
> Microsoft Store, Google Play and TestFlight on merge. Expect that, or batch such
> edits. (Slice 70: docs/plan/slices/70-split-build-pipeline.md)

> **Dino CI rule:** the `dinoreader` paths run only the `test-dinoreader`
> job (ubuntu), never the version, release or store jobs. Keep Dino paths out of
> the core paths in `ship-targets.mjs`. (Slice 59: docs/plan/slices/59-dialogue-parser-voice-caster.md)

> **iOS toolchain pin:** `ios.yml` pins dotnet 10.0.401 + workload set 10.0.401
> (Microsoft.iOS.Sdk 26.5.10318) + Xcode 26.6, because `macos-26` has no Xcode 27.
> Bump all three together. (Slice 48: docs/plan/slices/48-ios-target-testflight.md)

> **iOS signing (Slice 48, lifted 2026-10-21):** `ios.yml` compiles unsigned on PRs; on main with the six iOS secrets it signs (`.github/scripts/ios-signing.sh`, identity by SHA-1), attaches the `.ipa` to the draft and `testflight-submit` uploads it after `release-publish`. Signing scripts must run on macOS bash 3.2. (Slice 48: docs/plan/slices/48-ios-target-testflight.md)

## Windows packaging and Store

`.github/workflows/windows.yml` (called by `release.yml`, Slice 5, Slice 70) packages the single-project
MSIX (x64 + ARM64, unsigned — the Store re-signs; uploaded to the Release, never
as an Actions artifact — Batch 8); it has no LFS checkout (Slice 56). See `STORE.md` for capability justification, licenses, and the
Store/deployment details. The app is **live in the Store** (`9NGT1BN1H92V`);
`store-submit.yml` pushes updates (bundles both arches → one `.msixbundle` →
msstore CLI) once the Partner Center secrets are set.

## Packs releases

`packs-<n>` releases hold packs + `packs.json`, published by the manual `packs.yml` with `--latest=false`. Every app-release tag scan (`release.yml` release-publish, `.github/scripts/find-release.sh`) must filter to `^v[0-9]+\.[0-9]+\.[0-9]+$`. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)

## Dino Reader pipeline

Dino Reader has its own path-filter target (`dinoapp` in `ship-targets`, with
`dinoreader-android.yml`; assets `DinoReader-<v>.aab`/`.apk`; `DINOREADER_*` secrets,
never the RTST ones; without all five a Dino-only change cuts no release). One shared
version (`v<x.y.z>`) covers both products. (Decisions 72-82, `docs/plan/slices/58-67`)

## Android downloads

The Worker serves `/download`, `/download/android/latest` and
`/download/android/<x.y.z>` from the private R2 bucket `readthestupidtext-downloads` (binding
`DOWNLOADS`; `worker/downloads.ts`). The `publish-downloads` job copies each released APK there
after `release-publish` (no signing secrets; uses `CLOUDFLARE_API_TOKEN`/`CLOUDFLARE_ACCOUNT_ID`).
The key layout lives only in `worker/download-keys.mjs`; an already-published version is skipped and
the manifest is written last. Windows stays Store-only. A deploy needs the bucket to exist (wrangler
fails on a missing R2 binding). (Slice 69: docs/plan/slices/69-apk-downloads-on-r2.md)
