# Slice 353 — CI: keep the .NET 10 SDK on runners that carry .NET 11, and fix the Dino Windows VM test package

*Built 2026-11-12 (night wave 3), PR #1024, not merged. CI proof on the new runner image and the Dino VM package build are not yet verified (see the end of this file).*

**Touches:** `.github/workflows/*.yml`, `.github/CLAUDE.md`, `global.json` (only if the fix pins the SDK there),
`src/DinoReader.App/Testing/FakePurchases.cs`, `src/ReadTheStupidText.Mobile.Shared/Testing/**`,
`src/ReadTheStupidText.Mobile.Shared/ReadTheStupidText.Mobile.Shared.csproj`, `tools/desktop-vm/**`,
`docs/plan/slices/353-ci-sdk-pin-and-vm-test-package.md`

## Why

Two build breaks found while shipping Slice 352 (PR #1022), neither caused by that slice:

1. **`android.yml` fails at "Install the .NET MAUI Android workload"** on GitHub's new runner image
   `ubuntu-26.04` version 20261111.196.1 (three attempts of run 43282120681, 2026-11-12 18:40-18:46Z):
   `Workload installation failed: Workload pack 'Microsoft.NET.Runtime.Emscripten.Node.net10' in manifest
   'microsoft.net.workload.emscripten.net10' [/usr/share/dotnet/sdk-manifests/11.0.100/...] conflicts with manifest
   'microsoft.net.workload.emscripten.current' [/usr/share/dotnet/sdk-manifests/10.0.100/.../10.0.113/...]`.
   `dinoreader-android.yml`, with the same steps (`setup-dotnet` 10.0.402, `dotnet workload install maui-android
   --version 10.0.402`), passed on the older image 20261101.186.1 a minute earlier. So the image now carries a .NET 11
   SDK, and with no SDK pin in `global.json` (it holds only the test runner) the workload install likely runs under
   SDK 11. Likely, not proven: the fix must show it on the new image. As the rollout completes, every workflow that
   installs a workload (Android, iOS, Windows, both apps) can break the same way, including releases from `main`.
2. **The Dino Windows VM test package does not build on `main`:** `tools/desktop-vm/Build-TestPackage.ps1 -App dino`
   fails in the Release publish with `src/DinoReader.App/Testing/FakePurchases.cs(56,9)` and `(62,9)`: CS0234,
   `Testing` does not exist in namespace `ReadTheStupidText.Mobile.Shared` (Slice 346, PR #1016, under
   `#if DINO_TEST_BOOKS`; `Mobile.Shared.Testing.TestHookLog` is likely compiled only in Debug). Log:
   `D:/ReadTheStupidText-worktrees/reviews/s352-win-build.log`. No Dino desktop VM check can run until it builds.

## Work

- [x] 1. Pin the SDK the workflows build with to 10.0.402 (or the 10.0.4xx band) so a preinstalled .NET 11 SDK is
  never selected: query `library-docs` (context7: `actions/setup-dotnet`, `dotnet` SDK selection / `global.json`
  `rollForward`) first. Prefer a CI-only pin (e.g. a `global.json` written in the job, or `setup-dotnet`'s
  `global-json-file`) unless a repo-wide `global.json` `sdk` block is shown not to break local builds (check the SDKs
  installed on this machine with `dotnet --list-sdks`). Apply it to every workflow that installs a workload.
- [x] 2. Make `FakePurchases.cs` compile in the `DINO_TEST_BOOKS` Release build (the type it calls must be compiled
  wherever `DINO_TEST_BOOKS` is defined), without shipping test hooks in a store build.
- [x] 3. Rule in `.github/CLAUDE.md`: the SDK pin and why.

## Verification

- `android.yml` (and one other workload-installing workflow) green on a run whose "Set up job" shows image version
  20261111 or later.
- `tools/desktop-vm/Build-TestPackage.ps1 -App dino` builds; a store Release build still has no test hooks.
- Then rerun PR #1022's checks (Slice 352 is parked behind item 1).

*Changed while building:*

- `global.json` now holds an `sdk` block, version 10.0.401 with `rollForward` `latestPatch`, beside the test-runner block.
  Five workflows (`dino-store`, `dinoreader`, `tests`, `tools-selftests`, `windows`) use `setup-dotnet` with
  `global-json-file`. The workflows that install a workload (`android`, `dinoreader-android`, `ios`, `dinoreader-ios`,
  `windows`) log `dotnet --version` before the workload install, so a wrong SDK shows in the log.
- `dinoreader-windows.yml` installs its SDK into its own `DOTNET_ROOT`; `SDK_VERSION` and `WORKLOAD_SET` there are
  hand-synced with the pin (rule in `.github/CLAUDE.md`).
- `FakePurchases.cs`: the `WriteMarker` body is under `#if DEBUG`. Windows has no `TestHookLog` sink, so Release VM
  packages write no `[Purchases]` markers. `DinoTestBooks` Release publish: 0 errors. The store Release build has no
  `FakePurchases` and no `TestHookLog`.

*Tests:* RTST 2221 pass, Dino 6939 pass, actionlint 0 findings.

*From review:* high pass `reviews/s353-high.md` (6 findings), medium pass `reviews/s353-medium.md` (3), re-check
`reviews/s353-check1.md` (1 new, 5 repeats). All read in full.

- Applied high #3 (`reviews/s353-high.md`): the `dinoreader-windows.yml` `SDK_VERSION`/`WORKLOAD_SET` hand-sync is now in the `.github/CLAUDE.md` note.
- Applied medium #3 (`reviews/s353-medium.md`): the `dotnet --version` log step in `windows.yml`.
- Applied medium #2 (`reviews/s353-medium.md`) as a doc note only (fallback): the Release VM package writes no `[Purchases]` markers; recorded under Changed while building.
- Skipped #1 (`reviews/s353-high.md`): "jobs with no setup-dotnet" need a 10.0.4xx SDK preinstalled for `dotnet tool install GitVersion.Tool`; the runner images list 10.0.403, and the 4xx band is the pin's intent.
- Skipped #2 (`reviews/s353-high.md`): "pin 10.0.401 but android.yml" and others install 10.0.402; on hosted runners latestPatch picks the preinstalled 10.0.403 anyway, the extra download is harmless.
- Skipped #4 (`reviews/s353-high.md`): "the repo's usual form" is the conditional attribute, versus the repo's `[Conditional("DEBUG")]` form; the result is identical in Release and Debug, a style point.
- Skipped #5 (`reviews/s353-high.md`): "the Release DINO_TEST_BOOKS (VM) package writes no" `[Purchases]` markers; no script reads them today (see the doc note on medium #2).
- Skipped #6 (`reviews/s353-high.md`): "global.json is a release-trigger path", so merging runs all builds; informational, expected and noted in `.github/CLAUDE.md`.
- Skipped #1 (`reviews/s353-medium.md`): "two SDK pins" 10.0.401 and 10.0.402; same reason as high #2, both are in the 10.0.4xx band.
- Skipped #1 (`reviews/s353-check1.md`): "any host with no 10.0.4xx SDK" fails every dotnet command; that is the pin's intent.
- Skipped #2-#6 (`reviews/s353-check1.md`): "REPEAT (medium #1 / high #2)" and the other repeats, each handled under the original finding above.
- The builder's context7 line was not given to the reviewers; the caller confirms it separately.

#### For CLAUDE.md

- `global.json` pins the SDK (10.0.401, `rollForward` `latestPatch`) because runner images can carry a .NET 11 SDK
  and a workload install under it fails with manifest conflicts; every workflow reads it via `setup-dotnet`
  `global-json-file`, and workload jobs log `dotnet --version` first. Any host needs a 10.0.4xx SDK. Moving the band
  also means editing the literal 10.0.401/10.0.402 installs in `android`, `ios`, `dinoreader-android` and
  `dinoreader-ios`, and `SDK_VERSION`/`WORKLOAD_SET` in `dinoreader-windows.yml` (own `DOTNET_ROOT`, outside the repo's
  `global.json`). (Slice 353: docs/plan/slices/353-ci-sdk-pin-and-vm-test-package.md)
- Release Dino VM test packages (`DINO_TEST_BOOKS`) write no `[Purchases]` markers: `FakePurchases.WriteMarker` is
  `#if DEBUG` and Windows has no `TestHookLog` sink; a VM check must not expect them. (Slice 353: docs/plan/slices/353-ci-sdk-pin-and-vm-test-package.md)

#### Not yet verified

- `Build-TestPackage.ps1 -App dino` result: pending (to be added when run).
- `android.yml` and one other workload workflow green on runner image 20261111 or later (proved by this PR's own CI or the first main release).
- Dino VM checks of Slice 352 (desktop), once the package builds.
- Rerun PR #1022's checks after merge.
