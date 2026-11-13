# Slice 370 — Give the shared code a neutral name
**Product:** Shared

*Planned 2026-11-13.* Part of the tidy-by-product slices (366-371). User's choice, 2026-11-13 (item 5 of 5). Runs only when no other PR or worktree with changes is open (it touches about 400 files); after 366-369, before 371.

**Touches:** `src/ReadTheStupidText.Domain/**`, `src/ReadTheStupidText.Application/**`, `src/ReadTheStupidText.Documents/**`, `src/ReadTheStupidText.Infrastructure/**`, `src/ReadTheStupidText.Mobile.Shared/**`, every `using ReadTheStupidText.` in `src/**` and `tests/**`, `ReadTheStupidText.slnx`, `Directory.Build.props`, `.github/scripts/ship-targets.mjs`, `.github/workflows/*.yml` (paths), `tests/**/FallbackGuardBaseline.txt`, folder `CLAUDE.md` files that name these projects

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

- [ ] 1. Find every place a namespace or type name ends up in stored data or OS registration (the list above): grep for `typeof(...).FullName`, `nameof` used as keys, `[Activity`, `[Service`, `[BroadcastReceiver`, `Worker`, `[Register`, `TypeNameHandling`, `JsonDerivedType`, preference keys. Write each hit and its fix here before renaming.
- [ ] 2. Pin those names (explicit Android `Name`, string constants for keys).
- [ ] 3. Rename projects, folders (`git mv`), assembly names, root namespaces and every `namespace`/`using` line; `InternalsVisibleTo` lines; the `.slnx`.
- [ ] 4. Update path filters (`ship-targets.mjs`, workflow `paths:`), `FallbackGuardBaseline.txt` paths, folder `CLAUDE.md` names, root `CLAUDE.md` layer diagram.
- [ ] 5. Android and iOS for both apps built and tested in this slice.

## Verification

- All builds pass: RTST Windows x64, RTST Android, RTST iOS (CI), Dino Android, Dino iOS (CI), Dino Windows; all test projects pass with the same test count.
- `git grep -n "ReadTheStupidText\." -- src tests` shows only the RTST app projects, the RTST tests and the pinned strings from item 1.
- Upgrade test on the Android emulator for both apps: install the current release, set a setting, download a pack, then install the renamed build over it: setting kept, pack still found, app opens from the launcher icon. Same on the iOS simulator in CI if reachable, else a *Not yet verified* line.
- Android manifests of both apps before and after: the component names are identical (diff the merged `AndroidManifest.xml`).
