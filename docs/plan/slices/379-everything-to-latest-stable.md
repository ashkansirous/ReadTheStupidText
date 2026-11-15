# Slice 379 — Every library, SDK and toolchain to the latest stable version
**Product:** Shared

*Part A (items 1-6) built, merged as PR #1175 (04205ffc, 2026-11-15). Part B (items 7-9, .NET 11): in progress (wave 2026-11-15), draft PR.*

**Part B correction (coordinator, 2026-11-15):** part A's note that Xcode 27 is not on the runners is wrong. GitHub has an `xcode-27` image (runs-on `xcode-27`, macOS 27, Xcode 27.0/27.1/27.2; marked public preview), see runner-images issue 14404 and `images/macos/xcode-27-arm64-Readme.md`. Part B moves the iOS jobs to it, with the .NET 11 iOS workload, and corrects the `.github/CLAUDE.md` note.

**Touches:** `global.json`, `Directory.Build.props`, `src/**/*.csproj`, `tests/**/*.csproj`, `tools/**/*.csproj`, `package.json`, `package-lock.json`, `tools/dino/sound-words-test/requirements.txt`, `.github/workflows/*.yml`, `.github/dependabot.yml`, `.github/CLAUDE.md`, `src/Shared/AndroidRelease.props` (message text only), `src/Rtst/ReadTheStupidText.Mobile/CLAUDE.md` (workload-set text only), `src/Shared/SherpaOnnxVersion.props` (onnxruntime only, see decisions), `CLAUDE.md` (root, item 9 only), `README.md`, `STORE.md`, `src/Rtst/ReadTheStupidText.App/AGENTS.md` (.NET 11 version text only), `tools/CLAUDE.md` (API 37 note only), `docs/plan/slices/379-everything-to-latest-stable.md`, plus whatever source files a new version's breaking changes require (listed here when found)

## Why

The user, 2026-11-14: "if you did not add a part for updating all the libraries, dependencies and whatnot like dotnet, node, etc. to the latest stable add a slice for that as well." Slice 378 covers only the GitHub Actions and the warnings gate; this slice brings everything else current and keeps it current.

State on 2026-11-14 (read that day):

- **.NET:** `global.json` pins SDK `10.0.401` (`rollForward: latestPatch`); workflows pin `10.0.401` (iOS) and `10.0.402` (Android, Dino Windows). Latest releases (`releases-index.json`): .NET 11.0 SDK `11.0.100` (released 2026-11-10, active) and .NET 10.0 SDK `10.0.404`. `MauiVersion` 10.0.130 (`Directory.Build.props:7`).
- **Node:** workflows use `node-version: 22` (8) and `24` (1). Latest LTS on nodejs.org: v26.13.1. npm devDependencies: `typescript ^5.9.3`, `wrangler ^4.151.3`.
- **Others:** 57 `PackageReference` lines across the csproj files; `python-version: "3.12"` (one workflow) and `tools/dino/sound-words-test/requirements.txt`; `java-version: '17'`; Xcode 26.6 on `macos-26` (`.github/CLAUDE.md`, iOS toolchain pin).
- `.github/dependabot.yml` exists (Slice 378) with the github-actions ecosystem only; item 6 extends it.

## Decisions

- **Latest stable, as the user asked**, including .NET 11. Done in two parts so a blocked .NET 11 doesn't hold the rest: part A updates everything inside the current majors and every non-.NET tool; part B moves to .NET 11 (SDK, TFMs `net11.0*`, MAUI 11, workload sets, Windows App SDK if needed). If part B finds a hard blocker (a MAUI 11 or iOS workload that needs an Xcode the runners don't have, a library with no .NET 11 build), it records the blocker with its source link here and keeps the rest of part B going; nothing is quietly left on 10.
- **Existing hard rules stay:** sherpa-onnx stays on 1.x (root `CLAUDE.md`: "2.x stays out until a non-GPL phonemizer exists"); its version and onnxruntime's live only in `src/Shared/SherpaOnnxVersion.props`. No GPL/LGPL package (licence check for every new major). The SDK band, workload set and Xcode pins in `.github/CLAUDE.md` move together, as that file says.
- Android and iOS move together: every bump builds and runs on both, per head.
- Each library's breaking changes are read with `library-docs` (context7) before code changes.
- **Keeping current:** extend Slice 378's `.github/dependabot.yml` with `nuget` and `npm` (weekly, grouped minor/patch, majors separate).

## Work

Part A — current majors and tools:

- [x] 1. Inventory (table below): `dotnet list package --outdated` for the solution (and `--include-transitive` for vulnerable ones with `--vulnerable`), `npm outdated`, Python requirements, and every version pin in the workflows. Write the table (package, from, to, major?) into this file.
- [x] 2. (iOS workload set and Xcode stay, see inventory) .NET 10 SDK to the latest 10.0.4xx patch in `global.json` and every workflow pin plus matching workload sets (Android, iOS, Dino Windows `SDK_VERSION`/`WORKLOAD_SET`).
- [x] 3. NuGet packages to their latest stable versions that still support the current TFMs (sherpa-onnx inside 1.x only), fixing breaking changes.
- [x] 4. Node to the latest LTS in every workflow; npm devDependencies to latest (`wrangler`, `typescript`), `npm run typecheck` green.
- [x] 5. Python, Java and Android build tools: latest stable Python in the workflow, requirements to latest, Java to the version the current Android workload supports; Android `TargetSdkVersion` to the newest API Play accepts if it is behind.
- [x] 6. Dependabot: add `nuget` and `npm` ecosystems.

Part A inventory (built 2026-11-15; no extra source files needed a change, the Touches line held):

| Item | From | To | Major? | Note |
| --- | --- | --- | --- | --- |
| .NET SDK (Android, Dino Windows `setup-dotnet`) | 10.0.402 | 10.0.404 | no | latest 10.0 SDK; `global.json` stays at the floor `10.0.401` with `rollForward: latestPatch` (any installed 10.0.4xx at or above it), so a machine on 10.0.402 builds and 10.0.404 rolls forward |
| .NET SDK (iOS `setup-dotnet`, store-screenshots) | 10.0.401 | unchanged | - | kept exactly as before: an iOS SDK 10.0.404 + workload set 10.0.401 + Xcode 26.6 pairing was never run on `macos-26`; unchanged until Xcode 27 is on the runners (review #1 and #2) |
| Android and Dino Windows workload set | 10.0.402 | 10.0.404 | no | Android SDK still 36.1.118 (the `AndroidRelease.props` check holds) |
| iOS workload set and Xcode | 10.0.401, Xcode 26.6 | unchanged | - | blocker: every workload set after 10.0.401 carries iOS SDK 27.x (10.0.402 = 27.0.10722, 10.0.404 = 27.2.10000, needs Xcode 27), and the `macos-26` image lists Xcode 26.6 at most (https://github.com/actions/runner-images/blob/main/images/macos/macos-26-arm64-Readme.md, read 2026-11-15). Move the iOS pins with Part B or when the image has Xcode 27 |
| Microsoft.Extensions.DependencyInjection / Logging.Debug | 10.0.9 / 10.0.0 | 10.0.15 | no | 11.0.0 is Part B |
| Microsoft.WindowsAppSDK | 2.1.3 | 2.6.5 | minor | App builds; notes: 2.4.0 moves unpackaged `LocalSettings` registry path (App is MSIX, not affected) |
| Microsoft.Windows.SDK.BuildTools / .WinApp | 10.0.28000.1839 / 0.3.1 | 10.0.28000.2957 / 0.8.0 | 0.x | App builds |
| Serilog | 4.3.1 | 4.4.0 | no | |
| Microsoft.ML.Tokenizers | 2.0.1 | 3.0.0 | yes | no source change; Marian tokenizer tests pass |
| xunit.v3.mtp-v2 (3 test projects) | 3.2.2 | 4.0.3 | yes | no source change; MTP v2 default; 1722 + 628 + 4511 pass |
| Xamarin.AndroidX.Fragment / Fragment.Ktx | 1.9.0 | 1.9.1 | no | moved together (the D8 duplicate-class pin); Release R8 builds of Dino and RTST Mobile: see Review fixes below |
| Xamarin.Google.MLKit.TextRecognition | 116.0.1.8 | 116.0.1.9 | no | |
| OnnxRuntime (managed) | 1.28.0 | unchanged | - | held to sherpa-onnx 1.13.8's bundled onnxruntime 1.28.2 (props comment); latest 1.32 would not match the native library |
| sherpa-onnx | 1.13.8 | unchanged | - | latest 1.x; 2.x stays out (root CLAUDE.md) |
| MauiVersion | 10.0.130 | unchanged | - | latest 10.x; 11.x is Part B |
| H.NotifyIcon.WinUI, DocumentFormat.OpenXml, PdfPig, Serilog.Sinks.File, BillingClient, Play.App.Update, AppCompat, Material, MLKit others, OnnxRuntime.Extensions.Dummy | - | already latest | - | |
| Node (workflows) | 22 / 24 | 26 (LTS 26.13.1) | yes | all `setup-node` steps; local run was Node 24.13.1 (script tests 429 pass, worker tests 383 pass) |
| typescript | 5.9.3 | 7.0.2 | yes | `npm run typecheck` passes with the existing tsconfig |
| wrangler | 4.151.3 | 4.162.0 | no | `wrangler.jsonc` has no workflows/artifacts keys |
| Python (`packs.yml` quantiser) | 3.12 | 3.13 | no | 3.14/3.15 are available, but the quantiser's onnxruntime 1.23.2 has wheels only up to cp313; `sound-words-test/requirements.txt` already latest (sherpa-onnx pinned by the props rule, numpy unpinned) |
| Java (Maestro in two iOS simulator workflows) | 17 | 21 | no | .NET Android workflows use the runner's JDK; local JDK 21 builds Android |
| Android target API | 36 | unchanged | - | already the newest Play accepts |
| Dependabot | github-actions | + nuget, npm | - | sherpa-onnx and OnnxRuntime ignored (held by the props file) |

*Changed while building (part A):* `global.json` keeps a floor of SDK 10.0.401 (not 10.0.404), and the iOS workflows' SDK and workload pins stay at main's 10.0.401 with Xcode 26.6 until Xcode 27 is on `macos-26`. Dependabot uses `directories` globs and an `androidx-fragment` group so Fragment and Fragment.Ktx land in one PR.

*From review:* reviews `reviews/pr-1175-high.md` (8 findings; the file's verdict line says 7 plus 1 environment blocker) and `reviews/pr-1175-medium.md` (1 finding), all read.
- Applied #1-#5 of `reviews/pr-1175-high.md`: `global.json` floor 10.0.401; iOS SDK pins back to main's 10.0.401 / Xcode 26.6 until Xcode 27 on macos-26; stale 10.0.402 texts in `AndroidRelease.props` and the Mobile `CLAUDE.md`; Dependabot `directories` globs and the androidx-fragment group; Fragment/Fragment.Ktx comments and a Release R8 build (RTST Mobile and Dino Android Release, 0 errors).
- Skipped #6 (`reviews/pr-1175-high.md`): "scripts only run on Node 24.13.1" - checked by the PR's CI runs, not run locally on Node 26.
- Skipped #7 (`reviews/pr-1175-high.md`): "not shown to package in CI form" - checked by the RTST App Release x64 MSIX build (one msix, one onnxruntime.dll); ARM64 stays under Not yet verified.
- Skipped #8 (`reviews/pr-1175-high.md`): "verified only by Windows unit tests" - low severity; recorded under Not yet verified (Microsoft.ML.Tokenizers 3.0.0 on mobile Release).
- Skipped #1 (`reviews/pr-1175-medium.md`): "may get no Dependabot PRs" - low severity; checked after the first weekly Dependabot cycle, listed under Not yet verified.

*Verified for part A:* Release builds RTST Mobile and Dino Android (R8, 0 errors) and RTST App Release x64 MSIX (one msix, one onnxruntime.dll). Emulator emulator-5554, build 90f93c61710d: RTST first-run, paste-and-read, Dino first-run, open-book, premade-audio-plays all PASS (`reviews/s379-flow-rtst.log`, `reviews/s379-flow-dino.log`). Tests pass: Shared 1722, RTST 628, Dino 4511.

Part B — .NET 11:

- [x] 7. Check with `library-docs`: MAUI 11 release state, iOS/Android workload requirements (Xcode version vs. GitHub's macOS images), Windows App SDK on net11, sherpa-onnx 1.x and every other package's net11 support. Record the result here.
- [~] 8. Move `global.json`, every TFM, `MauiVersion`, workload sets and workflow pins to .NET 11; fix breaking changes; Android and iOS heads of both apps plus the Windows tray and Dino Windows build. (Done and built except the two Android builds: blocker below.)
- [x] 9. Update the version facts in the root `CLAUDE.md` (Stack, ".NET 10") and `.github/CLAUDE.md` pins.

Part B results (built 2026-11-15, SDK 11.0.100 installed into the worktree's ignored `tmp/dotnet`):

- **Item 7 findings.** SDK 11.0.100; `dotnet workload install ... --version 11.0.100` resolves workload set 11.0.100 (iOS manifest 27.2.11039, Android 37.2.29, MAUI 11.0.0); `Microsoft.Maui.Controls` 11.0.10 is the latest 11.x; Extensions 11.0.0; Android min API 24 (the apps' `SupportedOSPlatformVersion` 28 is above it); Windows App SDK 2.6.5 (part A) builds on `net11.0-windows` for the tray and Dino; sherpa-onnx 1.13.8 / OnnxRuntime 1.28.0 / Microsoft.ML.Tokenizers 3.0.0 / Serilog 4.4.0 / xunit.v3 4.0.3 restore and run on net11 (all tests pass). Runners: GitHub's `xcode-27` image (Xcode 27.2 at `/Applications/Xcode_27.2.app`, default 27.1, only .NET 10 SDKs, public preview; https://github.com/actions/runner-images/blob/main/images/macos/xcode-27-arm64-Readme.md). The compatibility page is https://learn.microsoft.com/en-us/dotnet/core/compatibility/11 (the `/11.0` URL 404s) and the MAUI notes are https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-11. The docs do not say whether iOS 15.5 stays a valid minimum on MAUI 11 (page lists 18.2 as supported): the iOS compile-only builds on Windows pass with 15.5.
- **Moved:** `global.json` 11.0.100; every `net10.0*` TFM to `net11.0*` (11 csproj plus tests and tools); `MauiVersion` 11.0.10; Extensions packages 11.0.0; all workflows (`setup-dotnet` 11.0.100, workload set 11.0.100 for Android, iOS and Dino Windows `SDK_VERSION`/`WORKLOAD_SET`, iOS jobs `runs-on: xcode-27` with Xcode 27.2, step names "Set up .NET 11"); an `InstallAndroidDependencies` step in both Android workflows (API 37 `android.jar`, XA5207 otherwise).
- **Opt-outs:** Windows CollectionView2 off for Dino (`UseWindowsCollectionView2Handler=false` in `DinoReader.App.csproj`, windows block): Dino's gallery and store lists were built on the old handler. No switch exists for CoreCLR on Android/iOS (Mono is gone on .NET 11), handler-based Shell on Android, or handler NavigationPage/TabbedPage on iOS (Dino uses NavigationPage): these come in as the new default and need checks on a device (listed below).
- **CollectionView2 opt-out checked (review fix 9):** the Dino Windows x64 Debug build's `bin/x64/Debug/net11.0-windows10.0.19041.0/win-x64/DinoReader.App.runtimeconfig.json` carries `"Microsoft.Maui.RuntimeFeature.IsWindowsCollectionView2HandlerEnabled": false`, so the property reaches the runtime.
- **Mono is not an option on .NET 11 (checked in review, 2026-11-15):** `UseMonoRuntime` still exists, but the .NET 11 SDK stops `UseMonoRuntime=true` on `net11.0-android`/`-ios` with NETSDK1242 (`_CheckForUnsupportedMonoMobileRuntime` in `Microsoft.NET.Sdk.TargetFrameworkInference.targets`; the MAUI repo's UI-testing guide says the same, context7 `/dotnet/maui`). So both apps move to CoreCLR with this slice; the only way back to Mono is a .NET 10 build. Review finding 1 (pin Mono) was not applied for that reason.
- **Extra files edited outside the Touches line (each hardcodes the TFM or label):** `.github/actionlint.yaml` (`xcode-27` label), `.github/scripts/compile-dino-test-package.sh`, `tools/desktop-vm/Build-TestPackage.ps1`, `tools/emulator/install-debug.ps1`, `tools/phone/common.ps1`, `tools/test-targets.ps1`, `tools/test-test-targets.ps1`, `src/Dino/DinoReader.App/CLAUDE.md`, `src/Dino/DinoReader.Core/CLAUDE.md`, `src/Shared/Sirous.Reading.Mobile.Shared/CLAUDE.md`, `tools/desktop-vm/README.md` (TFM text only). No C# source needed a change. README.md, STORE.md and `src/Rtst/ReadTheStupidText.App/AGENTS.md` now say .NET 11 (review fix).
- **Checks:** builds pass (0 errors) for DinoReader.Core, RTST App x64 Debug and Release, Dino Windows x64 Debug and Release, RTST Mobile and Dino iOS compile-only (`-p:IncludeIos=true`, `net11.0-ios`); tests Shared 1722, RTST 628, Dino 4511 pass; `npm run typecheck`, actionlint 1.7.12, `.github/scripts` tests (429), `test-test-targets.ps1` pass. New warning: CsWinRT1032 (collection expressions on WinRT-facing code, from the new SDK), not in the warnings-as-errors list.
- **Blocker (Android builds not run):** `net11.0-android` compiles against API 37, and the machine's Android SDK only has `platforms/android-36`; the fix is `dotnet build <proj> -t:InstallAndroidDependencies -f net11.0-android -p:AndroidSdkDirectory=<sdk> -p:AcceptAndroidSDKLicenses=true`, which installs into the user's Android SDK and accepts its licences, and the permission check denied it for this agent (a copy into tmp also needs `AcceptAndroidSDKLicenses`). RTST Mobile and Dino Android Debug and Release (R8) builds are therefore NOT RUN; the CI workflows now run that step themselves. Source: https://aka.ms/xa5207.

## Verification

- All heads build (Windows tray, Dino Windows, Dino and RTST Android and iOS) with Slice 378's warnings gate on; all three test suites pass; `npm run typecheck` passes; actionlint passes.
- Emulator flows for both apps (scripted, `device-test`) pass on the new versions; a TestFlight build of each app succeeds; the Windows tray reads text in the Hyper-V VM.
- The first release run after merge succeeds on every platform it ships (`ci-status`).

## Not yet verified

- iOS jobs on CI on `xcode-27` (SDK 11.0.100, workload set 11.0.100, Xcode 27.2): `ios.yml` and `dinoreader-ios.yml` on the branch.
- Android Debug and Release (R8) builds of both apps on .NET 11 (API 37 platform step in CI), then both apps on an emulator under CoreCLR and handler-based Shell.
- Dino's per-page iOS status-bar icon colour (`StatusBarPageViewController`, Slice 325 A4) on the handler-based NavigationPage: check on an iOS simulator or device that the icons still follow each page.
- iOS NavigationPage/TabbedPage handlers (Dino), Windows CollectionView2 (opted out for Dino, so unchanged).
- Node 26 in the release scripts (first release run).
- Dependabot nuget/npm PRs, including versions held in `.props` files, after the first weekly cycle.
- ARM64 MSIX.
- Windows tray read in the Hyper-V VM.
- TestFlight build of each app.
- RTST Scan flow.

#### For CLAUDE.md

Already edited on the branch: the SDK floor and iOS pin note in `.github/CLAUDE.md` (Slice 379: docs/plan/slices/379-everything-to-latest-stable.md).
