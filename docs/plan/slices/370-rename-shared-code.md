# Slice 370 — Give the shared code a neutral name
**Product:** Shared

*Built 2026-11-13 (wave 2026-11-13), merged as PR #1104 (2fc6ac4f); iOS simulator upgrade test, Release/R8/aab builds and CI iOS builds on main not yet verified.* Planned 2026-11-13. Part of the tidy-by-product slices (366-371). User's choice, 2026-11-13 (item 5 of 5). Runs only when no other PR or worktree with changes is open (it touches about 400 files); after 366-369, before 371.

**Touches:** `src/Sirous.Reading.Domain/**`, `src/Sirous.Reading.Application/**`, `src/Sirous.Reading.Documents/**`, `src/Sirous.Reading.Infrastructure/**`, `src/Sirous.Reading.Mobile.Shared/**` (renamed from the `src/ReadTheStupidText.*` folders of the same names), every `using ReadTheStupidText.` in `src/**` and `tests/**`, `ReadTheStupidText.slnx`, `Directory.Build.props`, `.github/scripts/ship-targets.mjs`, `.github/workflows/*.yml` (paths), `tests/**/FallbackGuardBaseline.txt`, folder `CLAUDE.md` files that name these projects; root `CLAUDE.md` and `README.md` (layer diagram, paths); `src/AndroidSherpaPackaging.props` and other `src/*.props` naming these paths; every `ProjectReference` in `src/**` and `tests/**` csproj files; `.github/scripts/*`; `tools/DinoReader.BookBuilder/**`, `tools/desktop-vm/**`, `tools/test-test-targets.ps1`, `tools/test-targets.ps1` (widened at claim, 2026-11-13, from `git grep` of the project names), `tools/crashes/pull-crashes.mjs` and its test (widened in review, 2026-11-13: crash grouping treats `Sirous.Reading.X` as `ReadTheStupidText.X`)

## Why

The engine both apps ship is named after one of them. Dino's code imports `ReadTheStupidText.*` in 94 files, and Mobile.Shared's namespace equals the RTST phone app's (`ReadTheStupidText.Mobile`, noted 2026-10-25, Slice 131).

## Decision (2026-11-13)

The new name is `Sirous.Reading.Domain`, `.Application`, `.Documents`, `.Infrastructure`, `.Mobile.Shared` (owner name plus what the code does; no product name). Taken without asking: the user said not to be asked about choices that block nothing ("what the fuck do you need from me?! what the hell the the point of planning and running on auto mode if you are going to stop all the shit every fucking hour!?"). Do not ask at build start.

## What must not change (users would lose data or apps would break)

- App identities: RTST and Dino Android package ids, iOS bundle ids, MSIX identities, Store IDs, the `StartupTask` id. The two apps' own projects are not renamed.
- The pack manifest URL (`PackConstants.cs:12`, this repo's releases) and the Worker URLs: strings, unchanged.
- Anything saved under a type or namespace name: settings keys, preference files, cached files, serialized JSON with type names, crash grouping keys. Item 1 finds them.
- Android component names: MAUI names Java wrappers from the .NET namespace, so an `[Activity]`, `[Service]`, `[BroadcastReceiver]` or WorkManager worker moved to a new namespace gets a new Android class name. Launcher shortcuts, scheduled jobs and the manifest's media service would break on update. Every such class in the renamed projects gets an explicit `Name = "..."` with its current Java name before the rename.
- iOS: background task ids and `NSUserDefaults` keys keep their strings.

## Work

- [x] 1. Find every place a namespace or type name ends up in stored data or OS registration (the list above): grep for `typeof(...).FullName`, `nameof` used as keys, `[Activity`, `[Service`, `[BroadcastReceiver`, `Worker`, `[Register`, `TypeNameHandling`, `JsonDerivedType`, preference keys. Write each hit and its fix here before renaming.
  **Item 1 findings (2026-11-13, from the merged Debug `acw-map.txt` and `AndroidManifest.xml` of both apps built before the rename, saved in `reviews/s370-before/`):**
  - Only five Java-wrapped types live in the renamed projects, all in `Mobile.Shared/Platforms/Android` (current wrapper hashes `crc646cc3464eb102334b`, `crc64f08c6de59cd7ad33`, `crc6497a5e39d1ddaca20`): `PackDownloadService` (the one manifest component, in both apps' manifests), `AndroidSpeechReader` and its nested `ProgressListener`, `AndroidLifecycleTrace.MemoryCallbacks`, `TestHooks.HookReceiver` (a runtime-registered receiver, Debug hooks). The apps' own `MainActivity`/`MainApplication` stay in unchanged namespaces and assemblies, so their names do not move.
  - Stored data: no `typeof(...).FullName`, `TypeNameHandling`, `JsonDerivedType`, type-name preference key, `[Register]` or `Worker` in the five projects. `FullName`/`GetType().FullName` only goes into crash report text (`CrashReportBuilder`, `LogLineFormatter`): exception type names of our own exceptions change in the stored report (`exceptionType`, stack, log tail). Only `tools/crashes/pull-crashes.mjs` groups on them (the Worker keeps the type as an `app_crashes` blob, which no query in the repo reads); see the Decision below. iOS: the NSUrlSession background id is `BundleIdentifier + ".packs"`, a string; the NSObject subclasses (`IosMetricSubscriber`, `IosBackgroundDownloader`, `SynthesizerDelegate`) are not referenced by their ObjC name; the app delegates keep their `[Register("AppDelegate")]`.
  - Decision (review pass 1, 2026-11-13): one owner of the old/new name equivalence: the reader. The app stores the true names (`CrashReportBuilder` writes `error.GetType().FullName`, as before the rename; the stack and log tail carry the new names too). `tools/crashes/pull-crashes.mjs` `normalizeRenamed` treats `Sirous.Reading.{Domain,Application,Documents,Infrastructure,Mobile.Shared}` and `ReadTheStupidText.*` as one group, applied to the stored type and the top frame; the RTST app's own `ReadTheStupidText.Mobile`/`.App` names stay distinct. Tests: `tools/crashes/pull-crashes.test.mjs`. A future renamed project must be added to that regex.
  - Strings that contain `ReadTheStupidText` and stay as they are: the pack manifest URL (`PackConstants.cs`), `ReadTheStupidTextStartup` (`StartupTask` id), the `ReadTheStupidText.slnx` file name (tests find the repo root by it), the RTST app project names.
- [x] 2. Pin those names (explicit Android `Name`, string constants for keys). Done: `[Service(Name = ...)]` on `PackDownloadService`, `[Android.Runtime.Register("<old jni name>")]` on the other four.
- [x] 3. Rename projects, folders (`git mv`), assembly names, root namespaces and every `namespace`/`using` line; `InternalsVisibleTo` lines; the `.slnx`.
- [x] 4. Update path filters (`ship-targets.mjs`, workflow `paths:`), `FallbackGuardBaseline.txt` paths, folder `CLAUDE.md` names, root `CLAUDE.md` layer diagram. (`FallbackGuardBaseline.txt` has no entries, on `main` too: only its two comment lines, so no paths needed renaming.)
- [x] 5. Android and iOS for both apps built and tested in this slice. Builds with 0 errors: Android RTST and Dino, iOS compile of both (`-p:IncludeIos=true`), Dino Windows x64 Release, RTST App x64, BookBuilder. Tests, same totals as `main`: Shared.Tests 1677, ReadTheStupidText.Tests 630, DinoReader.Tests 7169; `.github/scripts` node tests 356; `pull-crashes` 14/14; `tools/test-test-targets.ps1` passes (pwsh and Windows PowerShell 5.1). Android upgrade test on emulator-5554 (old `49b19487` debug installed, then the renamed debug with `install -r`): both apps PASS (setting kept, pack/stories found, launcher opens, no crash, `dumpsys` components identical); evidence in `reviews/s370-device/`. Dino's setting was set through the test hooks (its onboarding has no skip); Dino shows onboarding step 5 after relaunch on the old and the new build alike (the skip-first-run hook does not persist), so it is not a regression.

*Changed while building:* the five Android Java wrappers are pinned (`PackDownloadService` with `Name =`, the other four with `[Register]`); merged manifests of both apps are byte-identical before and after and the `acw-map` Java names are unchanged (`reviews/s370-before`, `reviews/s370-after`). Crash grouping has one owner, the reader (`tools/crashes/pull-crashes.mjs` `normalizeRenamed`); the app stores the true new names.

## Verification

- All builds pass: RTST Windows x64, RTST Android, RTST iOS (CI), Dino Android, Dino iOS (CI), Dino Windows; all test projects pass with the same test count.
- `git grep -n "ReadTheStupidText\." -- src tests` shows only the RTST app projects, the RTST tests and the pinned strings from item 1.
- Upgrade test on the Android emulator for both apps: install the current release, set a setting, download a pack, then install the renamed build over it: setting kept, pack still found, app opens from the launcher icon. Same on the iOS simulator in CI if reachable, else a *Not yet verified* line.
- Android manifests of both apps before and after: the component names are identical (diff the merged `AndroidManifest.xml`).

## From review

Findings files read: `reviews/s370-high.md` 7/7, `s370-medium.md` 7/7, `s370-check1.md` 8/8, `s370-check2.md` 3/3, `s370-check3.md` 6/6. Applied (every finding not listed in the lines below): high #1, #4, #5, #6, #7; medium #1, #4, #5; check1 #1-#7; check2 #1, #2; check3 none (all six are the lines below).

- Skipped #2 (`reviews/s370-high.md`): "Pinned Java names are hand-typed literals with no automated guard." - the names were verified once against the merged Debug acw-map and manifests of both apps (item 1); a source-scan guard is a separate hardening, not this rename.
- Skipped #3 (`reviews/s370-high.md`): "Item 5 unchecked; Verification not complete." - item 5 and the Verification runs are this slice's own open work, tracked by the unchecked box, not a code fix.
- Skipped #2 (`reviews/s370-medium.md`): "Item 5 (Android/iOS builds, tests) unchecked" - the builds and the emulator upgrade test are the slice's open item 5 and Verification, run later.
- Skipped #3 (`reviews/s370-medium.md`): "have no test or CI guard." - same as high #2: verified once by diff; a guard test is separate hardening.
- Skipped #6 (`reviews/s370-medium.md`): "may now bind to the new namespace" - covered by the full builds of every head in the open item 5; no code change known to be needed.
- Skipped #7 (`reviews/s370-medium.md`): "Old Slice 15 review files still cite" - those are historical review files outside the repo's tracked source; the verification grep covers `src tests` only.
- Skipped #8 (`reviews/s370-check1.md`): "with only an italic superseded note" - the superseded note is enough; history stays as written.
- Skipped #3 (`reviews/s370-check2.md`): "group key shows the old name while the newest report shows the new name; headings name types absent from current code. Show the current name or both." - cosmetic heading; groups still merge.
- Skipped #1 (`reviews/s370-check3.md`): "key now displays retired ReadTheStupidText.* names for post-rename crashes." - cosmetic key display; display and grouping share one string by design.
- Skipped #2 (`reviews/s370-check3.md`): "turns missing exceptionType from" - harmless key text; hand-built or stub reports only, the Worker's `typeOrOther` prevents it.
- Skipped #3 (`reviews/s370-check3.md`): "are not normalized; unverified whether they ever carry Sirous.Reading.* names." - speculative; native frames carry no managed type names.
- Skipped #4 (`reviews/s370-check3.md`): "lookbehind refuses Foo.Sirous.Reading.X on purpose" - intentional, info only.
- Skipped #5 (`reviews/s370-check3.md`): "no tests for null exceptionType, native frame with renamed name, or mere mention in a path." - test gaps on a dev tool; not worth more tests here.
- Skipped #6 (`reviews/s370-check3.md`): "hard-coded five-project list can drift from real projects; no guard test against src/Sirous.Reading.* dirs." - a guard on a dev tool; the Decision line above says to add a future project to the regex.

## Not yet verified

- iOS simulator upgrade test (old build, setting, pack, install the renamed build over it): no Mac available.
- Release, R8 and aab builds of both Android apps (only Debug manifests and builds were compared).
- CI iOS builds on `main` after the merge (only compile checks ran locally).

#### For CLAUDE.md (lifted 2026-11-13)

- Root `CLAUDE.md`: the layer diagram uses the `Sirous.Reading.{Domain,Application,Documents,Infrastructure,Mobile.Shared}` names. (Slice 370: docs/plan/slices/370-rename-shared-code.md)
- A namespace rename changes the Android crc64 Java wrapper names MAUI generates, which breaks launcher shortcuts, jobs and the manifest service on update: pin the old name with `Name =` or `[Android.Runtime.Register("...")]` first, and diff the merged `AndroidManifest.xml` and `acw-map.txt` before and after. (Slice 370: docs/plan/slices/370-rename-shared-code.md)
