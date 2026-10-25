# Slice 132 — Dino: a remote switchboard and a forced update

*Planned 2026-10-25.*

**Batch:** 28 (Dino usage insights, Decisions 138-142)
**Touches:** `worker/stories.ts`, `worker/appconfig.ts` (new), `worker/index.ts`, `worker/*.test.ts`, `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `src/DinoReader.App/AppConfigClient.cs` (new), `src/DinoReader.App/UpdatePage.cs` (new), `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/GrownUpGate.cs`, `src/DinoReader.App/Platforms/Android/**`, `src/DinoReader.App/Platforms/iOS/**`, `src/DinoReader.App/Platforms/Windows/**`, `src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.Core/AppConfig.cs` (new), `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`, `CLAUDE.md`

## Why

The user wants the usage counts of Slices 133-134 to be adjustable without an app release (turn them
off, sample fewer), and a way to force an update when a change can only ship in a new version (a
telemetry or privacy fix, a broken store format). Today the app fetches nothing at start besides the
store list.

## Decisions

- **Decision 138: Dino fetches one small config from the Worker.** `GET /api/app-config` (Dino host only,
  plus its `/dino` twin in `run_worker_first`) returns per platform `minVersion` and `latestVersion`,
  and `telemetry: { enabled, sampleRate }`. The Worker reads it from the R2 object
  `dinoreader/config.json`, so a change is one `wrangler r2 object put`, no deploy, no app release.
  The app caches the last good copy; offline or on error it uses the cache, then safe defaults
  (telemetry off, no forced update). Short edge cache (5 minutes).
- **Decision 139: below `minVersion` the app shows a full-screen "Please update" page and nothing else.**
  Its button goes through the existing grown-up gate (`GrownUpGate`, Apple Kids category needs a
  parental gate before leaving the app) and then: Android, the Play in-app *immediate* update (confirm
  the binding package and API via `library-docs`); iOS, the App Store page; Windows, `StoreContext`
  update request. Below `latestVersion` only a quiet line in the grown-up settings, never a nag.
- The version compared is the app's own version (the `dino-v` line after Slice 130; same numbers today).
- Dino only; RTST gets no remote config.

## Work

- [ ] Worker: `appconfig.ts` reads and validates the R2 JSON (bad file = defaults), route + `/dino`
      twin, tests. Seed `config.json` with `minVersion` = current, telemetry off.
- [ ] Core: `AppConfig` parsing and the version comparison (pure, unit-tested).
- [ ] App: `AppConfigClient` fetch at start (non-blocking unless the cached config already forces an
      update), `UpdatePage`, the grown-up gate, per-platform update calls.
- [ ] Strings in all Dino languages; CLAUDE.md lines (config file location, how to force an update).

## Verification

- Worker tests: valid, missing and malformed config; Dino host only.
- Unit tests for version compare and parsing.
- Emulator: set `minVersion` above the installed build in R2, relaunch: the update page shows, the gate
  works, the Play link opens; set it back, the app opens normally. Offline launch opens normally.
