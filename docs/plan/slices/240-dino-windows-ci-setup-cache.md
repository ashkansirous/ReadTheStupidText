# Slice 240 — Dino Reader Windows CI: cache the SDK, workload and NuGet packages

*In progress.* Planned 2026-11-01, from the user: "fix the caching problem for windows build and setup, specially installing maui and all the rest".

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
- [x] `actions/cache/restore` + `actions/cache/save`: after a restore, validate (`dotnet.exe` exists, `dotnet --version` is the pin, `dotnet workload list` shows maui-windows, output captured before matching). Validation also needs `packs\` to hold a `Microsoft.Maui*` folder, and the cache key carries the runner image (`ImageOS`/`ImageVersion`, set by hosted runner images; a warning shows if unset). Invalid or miss: wipe, `setup-dotnet`, install, check `packs\` is non-empty with a Microsoft.Maui pack (fails before any save otherwise), and after a bad exact hit delete the broken entry (`gh cache delete <key>`, job has `actions: write`, a delete failure only warns), then save under the exact key right after the install (before any build).
- [x] NuGet cache at `~/.nuget/packages`, key per matrix arch, hashing only the csproj/props files the Dino Windows build restores, restore-keys prefix per arch; saved after a good publish on an exact miss.
- [x] x64 + ARM64 matrix and the MSIX publish step unchanged.
- [x] actionlint 1.7.7 clean.

## Verification (needs the PR's CI runs)

- [ ] Run 1 (cache miss): record step times for "Set up .NET 10", "Install the .NET MAUI Windows workload", the publish step; confirm `dotnet workload list` shows maui-windows and the MSIX builds. Read the `packs\` listing: did iOS/Android/wasm packs get pulled; check the cache saved a dotnet root.
- [ ] Run 2 (cache hit, rerun the same PR): the SDK and workload steps take seconds; the build and packaging pass.
- [ ] Before: setup 240-300 s per job. After (hit): expected under 60 s including cache restore.

Not yet verified: whether the workload installs as files (not MSIs) into the setup-dotnet root on a hosted runner (read from the SDK source, not run); if it does not, the pack check after install (non-empty `packs\` with a Microsoft.Maui folder) fails before the save and the run errors loudly instead of caching a hollow root. Also not run: `gh cache delete` of a cache entry created on another ref (a PR run restoring main's entry) may be refused; that only warns and the save then fails harmlessly.
