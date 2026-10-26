# Slice 132 — Dino: a remote switchboard and a forced update

*Built 2026-10-25 (PR #359), not yet verified on a device. Worker tests 59, DinoReader tests 1120.*

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

#### Design (Dino Reader Batch 26-28 mock, section 26-28, added 2026-10-25)

Frames 61 and 62 (bottom row, line 58 and 76 of `docs/design/dino-reader/Dino Reader Batch 26-28.dc.html`; row of frame 62 only). The mock agrees with the plan. All frames are 360x760 phones; the copy is marked a draft.

- **Please update (frame 61, `UpdatePage`).** Same layout as the first-run "ready" screen (18): background `#FFF6E6`, 150px `#ECE7FF` circle with the 120px app icon (radius 30) and a 44px `#5B57E8` download badge (white arrow, 4px `#FFF6E6` ring) at bottom-right. Title "Dino needs an update" (28px, 600). Body "Ask a grown-up to get the new version. Your books and recordings stay just as they are." (14.5px, `#5a5470`, max 280px). Button "Update Dino": 56px high, full width, `#FF7A59`, 5px `#E2583A` underlay, 17px 600. No close or skip. The button opens the grown-up gate first, then the per-platform update (Decision 139).
- **New version row (frame 62, grown-up settings).** Last row of the Version card: "A new version is ready", 14.5px, 600, `#5B57E8`, with a grey chevron (`#8a8399`), 50px row. It sits under a "Version" row (value "1.42.0", draft). It shows only below `latestVersion`; it opens the store, no popup, no badge.
- Placement in grown-up settings is the mock's own proposal (existing `SettingsRows` cards); no grown-up settings mock exists yet.

**New strings (all Dino locales):** "Dino needs an update", the body line, "Update Dino", "A new version is ready", "Version" (if not present).

**Open point: Store link from the settings row.** The plan says only "a quiet line"; the mock makes it a tappable row that opens the store. **Default:** tappable, same per-platform store target as the update page (no in-app update flow), no extra gate because grown-up mode is already gated.


## Work

- [x] Worker: `appconfig.ts` reads and validates the R2 JSON (bad file = defaults), route + `/dino`
      twin, tests. Seed `config.json` with `minVersion` = current, telemetry off. *(Seeding the R2 object is left to the coordinator after merge.)*
- [x] Core: `AppConfig` parsing and the version comparison (pure, unit-tested).
- [x] App: `AppConfigClient` fetch at start (non-blocking unless the cached config already forces an
      update), `UpdatePage`, the grown-up gate, per-platform update calls.
- [x] Strings in all Dino languages; CLAUDE.md lines (config file location, how to force an update).

## Verification

- Worker tests: valid, missing and malformed config; Dino host only.
- Unit tests for version compare and parsing.
- Emulator: set `minVersion` above the installed build in R2, relaunch: the update page shows, the gate
  works, the Play link opens; set it back, the app opens normally. Offline launch opens normally.

*Changed while building:*
- `GET /api/app-config` (and its `/dino` twin) reads R2 `dinoreader/config.json` on each request. A missing file returns defaults (no forced update, telemetry off). An unparseable file returns 503 with no-cache so clients keep their last good copy. `Cache-Control: max-age=300` is a client hint only; there is no edge cache.
- `AppConfig.TryParseServed` (Core) needs a JSON object with a numeric `schemaVersion`. A higher schema is accepted only when it still has a readable `minVersion`.
- `AppConfigClient` fetches at launch and on resume, at most every 15 minutes after a success and 1 minute after a failure. In-flight requests are de-duplicated and the body is compared before it is cached.
- `UpdatePage` also appears mid-session when a fetch raises `minVersion` (it pops modals and the stack first). Its button goes through `GrownUpGate`.
- Android uses the Play immediate in-app update (`Xamarin.Google.Android.Play.App.Update` 2.1.0.20; `OnActivityResult` clears a cancelled flow) with the Play page as fallback. Windows uses a `StoreContext` update (cancel does nothing, an error opens the Store page). iOS opens `apps.apple.com/app/id6825053435`.
- A Version card sits last in `LanguagePage` (grown-up settings): a Version row always, and "A new version is ready" below `latestVersion`. This is outside Touches, approved.
- `.github/workflows/site-checks.yml` now runs `worker/*.test.mjs` (outside Touches, approved). The worker tests are `.mjs`, not `.ts`.
- 5 new strings in all 14 resx files (machine drafts).

#### For CLAUDE.md (lifted 2026-10-25)

- Remote config: Dino `GET /api/app-config` reads R2 `dinoreader/config.json`; a missing or bad file means no forced update and telemetry off; change it with `wrangler r2 object put`. Root `CLAUDE.md` (Slice 132: docs/plan/slices/132-dino-remote-config-and-force-update.md)
- `AppConfigClient`, `UpdatePage` (gated button, no close), `StoreUpdater` per platform and the Version card: `src/DinoReader.App/CLAUDE.md` (Slice 132: docs/plan/slices/132-dino-remote-config-and-force-update.md)

#### Not yet verified

- R2 `config.json` seed (coordinator does it after merge).
- Emulator: below-`minVersion` shows `UpdatePage`; the gate; the Play page fallback.
- Emulator: the Version card and the "A new version is ready" row.
- Windows head and iOS builds after the last fixes.
- Play in-app update needs a Play-installed build with a higher versionCode.
- Native-speaker read of the 5 new strings.

#### Follow-up fix (2026-10-26)

The emulator run found the resume refresh fetching on the UI thread (`NetworkOnMainThreadException`, Android). `AppConfigClient` now fetches on the thread pool under one lock, raises `Changed` on the main thread, and retries a no-connection failure after 10 s, 30 s and 60 s. Emulator: Version card and "A new version is ready" row PASS (D:/ReadTheStupidText-worktrees/reviews/132-device.md). Still not verified: the forced-update page on a device (needs a temporary `minVersion` change in the live config).
