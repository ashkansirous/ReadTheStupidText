# Slice 353 — CI: keep the .NET 10 SDK on runners that carry .NET 11, and fix the Dino Windows VM test package

*In progress (wave 2026-11-12), draft PR.*

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

- [ ] 1. Pin the SDK the workflows build with to 10.0.402 (or the 10.0.4xx band) so a preinstalled .NET 11 SDK is
  never selected: query `library-docs` (context7: `actions/setup-dotnet`, `dotnet` SDK selection / `global.json`
  `rollForward`) first. Prefer a CI-only pin (e.g. a `global.json` written in the job, or `setup-dotnet`'s
  `global-json-file`) unless a repo-wide `global.json` `sdk` block is shown not to break local builds (check the SDKs
  installed on this machine with `dotnet --list-sdks`). Apply it to every workflow that installs a workload.
- [ ] 2. Make `FakePurchases.cs` compile in the `DINO_TEST_BOOKS` Release build (the type it calls must be compiled
  wherever `DINO_TEST_BOOKS` is defined), without shipping test hooks in a store build.
- [ ] 3. Rule in `.github/CLAUDE.md`: the SDK pin and why.

## Verification

- `android.yml` (and one other workload-installing workflow) green on a run whose "Set up job" shows image version
  20261111 or later.
- `tools/desktop-vm/Build-TestPackage.ps1 -App dino` builds; a store Release build still has no test hooks.
- Then rerun PR #1022's checks (Slice 352 is parked behind item 1).
