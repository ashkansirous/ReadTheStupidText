# Slice 379 — Every library, SDK and toolchain to the latest stable version
**Product:** Shared

*Planned.*

**Touches:** `global.json`, `Directory.Build.props`, `src/**/*.csproj`, `tests/**/*.csproj`, `tools/**/*.csproj`, `package.json`, `package-lock.json`, `tools/dino/sound-words-test/requirements.txt`, `.github/workflows/*.yml`, `.github/dependabot.yml`, `.github/CLAUDE.md`, `src/Shared/SherpaOnnxVersion.props` (onnxruntime only, see decisions), `docs/plan/slices/379-everything-to-latest-stable.md`, plus whatever source files a new version's breaking changes require (listed here when found)

## Why

The user, 2026-11-14: "if you did not add a part for updating all the libraries, dependencies and whatnot like dotnet, node, etc. to the latest stable add a slice for that as well." Slice 378 covers only the GitHub Actions and the warnings gate; this slice brings everything else current and keeps it current.

State on 2026-11-14 (read that day):

- **.NET:** `global.json` pins SDK `10.0.401` (`rollForward: latestPatch`); workflows pin `10.0.401` (iOS) and `10.0.402` (Android, Dino Windows). Latest releases (`releases-index.json`): .NET 11.0 SDK `11.0.100` (released 2026-11-10, active) and .NET 10.0 SDK `10.0.404`. `MauiVersion` 10.0.130 (`Directory.Build.props:7`).
- **Node:** workflows use `node-version: 22` (8) and `24` (1). Latest LTS on nodejs.org: v26.13.1. npm devDependencies: `typescript ^5.9.3`, `wrangler ^4.151.3`.
- **Others:** 57 `PackageReference` lines across the csproj files; `python-version: "3.12"` (one workflow) and `tools/dino/sound-words-test/requirements.txt`; `java-version: '17'`; Xcode 26.6 on `macos-26` (`.github/CLAUDE.md`, iOS toolchain pin).
- No Dependabot or Renovate config.

## Decisions

- **Latest stable, as the user asked**, including .NET 11. Done in two parts so a blocked .NET 11 doesn't hold the rest: part A updates everything inside the current majors and every non-.NET tool; part B moves to .NET 11 (SDK, TFMs `net11.0*`, MAUI 11, workload sets, Windows App SDK if needed). If part B finds a hard blocker (a MAUI 11 or iOS workload that needs an Xcode the runners don't have, a library with no .NET 11 build), it records the blocker with its source link here and keeps the rest of part B going; nothing is quietly left on 10.
- **Existing hard rules stay:** sherpa-onnx stays on 1.x (root `CLAUDE.md`: "2.x stays out until a non-GPL phonemizer exists"); its version and onnxruntime's live only in `src/Shared/SherpaOnnxVersion.props`. No GPL/LGPL package (licence check for every new major). The SDK band, workload set and Xcode pins in `.github/CLAUDE.md` move together, as that file says.
- Android and iOS move together: every bump builds and runs on both, per head.
- Each library's breaking changes are read with `library-docs` (context7) before code changes.
- **Keeping current:** extend Slice 378's `.github/dependabot.yml` with `nuget` and `npm` (weekly, grouped minor/patch, majors separate).

## Work

Part A — current majors and tools:

- [ ] 1. Inventory: `dotnet list package --outdated` for the solution (and `--include-transitive` for vulnerable ones with `--vulnerable`), `npm outdated`, Python requirements, and every version pin in the workflows. Write the table (package, from, to, major?) into this file.
- [ ] 2. .NET 10 SDK to the latest 10.0.4xx patch in `global.json` and every workflow pin plus matching workload sets (Android, iOS, Dino Windows `SDK_VERSION`/`WORKLOAD_SET`).
- [ ] 3. NuGet packages to their latest stable versions that still support the current TFMs (sherpa-onnx inside 1.x only), fixing breaking changes.
- [ ] 4. Node to the latest LTS in every workflow; npm devDependencies to latest (`wrangler`, `typescript`), `npm run typecheck` green.
- [ ] 5. Python, Java and Android build tools: latest stable Python in the workflow, requirements to latest, Java to the version the current Android workload supports; Android `TargetSdkVersion` to the newest API Play accepts if it is behind.
- [ ] 6. Dependabot: add `nuget` and `npm` ecosystems.

Part B — .NET 11:

- [ ] 7. Check with `library-docs`: MAUI 11 release state, iOS/Android workload requirements (Xcode version vs. GitHub's macOS images), Windows App SDK on net11, sherpa-onnx 1.x and every other package's net11 support. Record the result here.
- [ ] 8. Move `global.json`, every TFM, `MauiVersion`, workload sets and workflow pins to .NET 11; fix breaking changes; Android and iOS heads of both apps plus the Windows tray and Dino Windows build.
- [ ] 9. Update the version facts in the root `CLAUDE.md` (Stack, ".NET 10") and `.github/CLAUDE.md` pins.

## Verification

- All heads build (Windows tray, Dino Windows, Dino and RTST Android and iOS) with Slice 378's warnings gate on; all three test suites pass; `npm run typecheck` passes; actionlint passes.
- Emulator flows for both apps (scripted, `device-test`) pass on the new versions; a TestFlight build of each app succeeds; the Windows tray reads text in the Hyper-V VM.
- The first release run after merge succeeds on every platform it ships (`ci-status`).

## Not yet verified

_(filled in when built)_
