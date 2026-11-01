# Slice 240 — Dino Reader Windows CI: cache the SDK, workload and NuGet packages

*Built 2026-11-01, merged as PR #702 (4e94d766, 21:22 UTC) plus hotfix PR #704 (880ef8a4, 21:44 UTC); the cache hit on a run on `main` is not yet verified.* Planned 2026-11-01, from the user: "fix the caching problem for windows build and setup, specially installing maui and all the rest".

**Touches:** `.github/workflows/dinoreader-windows.yml`, `.github/CLAUDE.md`, `docs/plan/slices/240-dino-windows-ci-setup-cache.md`

## Why

Each `dinoreader-windows` job (x64 and ARM64) spends about 240-300 s on setup before its 3-minute build:
"Set up .NET 10" (`10.0.x` downloads a fresh 300 MB 10.0.403 every run) 28-40 s; "Install the .NET MAUI Windows
workload" (`dotnet workload install maui-windows`) 198-250 s, installing iOS, MacCatalyst, Android, AOT and
Emscripten packs through MSIs that an `-p:IncludeAndroid=false` MSIX build does not use; NuGet restore 30-47 s per
project, uncached.

## Work

- [x] Pin the SDK to 10.0.402 and the workload set to 10.0.402, as job `env` (`SDK_VERSION`, `WORKLOAD_SET`); they must match each other.
- [x] No `userlocal` marker and no `DOTNET_CLI_HOME`: `setup-dotnet` installs the SDK into a job-local `DOTNET_INSTALL_DIR` (no `installertype\msi` marker there, so workloads install as files into that root), then `maui-windows` is installed with the pinned set into the same root. The whole root is cached.
- [x] `actions/cache/restore` + `actions/cache/save`: after a restore, validate (`dotnet.exe` exists, `dotnet --version` is the pin, `dotnet workload list` shows maui-windows, output captured before matching). Validation also needs `packs\` to hold a `Microsoft.Maui*` folder, and the cache key carries the runner OS image (`ImageOS`) but not `ImageVersion`: on 2026-11-01 the two jobs of one run got images 20261021 and 20261030, so a per-version key missed. Run 1 (miss, 2026-11-01): workload install 19-21 s (was 198-250 s) and .NET setup 20-23 s, file-based packs confirmed. The first rerun's hit failed validation because a backslash Windows path was used in a glob (fixed with `cygpath -u`). Invalid or miss: wipe, `setup-dotnet`, install, check `packs\` is non-empty with a Microsoft.Maui pack (fails before any save otherwise), and after a bad exact hit delete the broken entry (`gh cache delete <key>`, job has `actions: write`, a delete failure only warns), then save under the exact key right after the install (before any build).
- [x] NuGet cache at `~/.nuget/packages`, key per matrix arch, hashing only the csproj/props files the Dino Windows build restores, restore-keys prefix per arch; saved after a good publish on an exact miss.
- [x] x64 + ARM64 matrix and the MSIX publish step unchanged.
- [x] actionlint 1.7.7 clean.

## Verification (needs the PR's CI runs)

- [x] Run 1 (cache miss, PR #702 run 41349281630): MSIX packaged on both architectures; workload install 19-21 s (was 198-250 s), .NET setup 20-23 s (the file-based workload install ran), and the cache was saved.
- [x] Run 2 (cache hit, rerun of the same PR, run 41350317511): both jobs restored the cache in 7 s and validated it in 1 s, with no setup or workload install; the x64 job took about 3.4 min (was about 9).
- [x] Before: setup 240-300 s per job. After (hit): restore 7 s plus validation 1 s.

Not yet verified: a cache hit for a run on `main` (no release run on `main` has succeeded since #702; PR #708's pull-request run 41354709874 missed the cache, because GitHub gives a branch only its own caches and main's, and main had saved none; release run 41356358486 built both Dino Windows architectures and saved main's caches at 22:32-22:35 (`gh cache list --ref refs/heads/main`), so the next run can show the hit). Also not run: `gh cache delete` of a cache entry created on another ref (a PR run restoring main's entry) may be refused; that only warns and the save then fails harmlessly. Also not yet verified: a release run on `main` with the Store MSIX build pinned to SDK 10.0.402 (was floating `10.0.x`) packages both architectures and uploads them to the draft release; only PR runs have used the pin (final feedback loop, 2026-11-01).

## Skipped review findings

- Medium review (`s240-medium.md`) 3, matrix race on the SDK key: on a cold cache both legs install and save the same key and one save fails, hidden by `continue-on-error`; accepted as harmless after the first run (PR #702: "accepted").
- Medium 5, duplicate pins (`SDK_VERSION` and `WORKLOAD_SET` are two literals that must match): accepted; the install step already fails on a version mismatch.
- Medium 6, NuGet cache saved only on an exact miss, so packages outside the hashed files are re-downloaded each run: accepted; watch the cache quota.
- Hotfix high review (`s240b-high.md`) 2, `DOTNET_DIR_KEY` has no `matrix.platform`, so x64 and ARM64 share one SDK cache entry and a bad exact hit can make one leg delete the other's fresh entry: accepted (PR #704: "predates this change and is low risk"; unconfirmed in practice).
