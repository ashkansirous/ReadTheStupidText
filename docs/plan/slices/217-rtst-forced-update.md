### Slice 217 — Read The Stupid Text: forced update from the remote config

*Planned 2026-10-30.* Port of Dino Reader's forced update (Slice 132) to Read The Stupid Text, on Android, iOS and the Windows tray app.

**Touches:** `src/ReadTheStupidText.Application/Usage/UsageConfig.cs` (or a new `Application/AppConfig/*` shared with Dino's `DinoReader.Core/AppConfig.cs`), `src/ReadTheStupidText.Mobile.Shared/Usage/RemoteConfigClient.cs`, `src/ReadTheStupidText.Mobile/App.xaml.cs`, `src/ReadTheStupidText.Mobile/UpdatePage*.cs` (new), `src/ReadTheStupidText.App/**` (tray: config client and the update prompt), `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/ReadTheStupidText.App/Strings/**`, `tests/ReadTheStupidText.Tests/**`, `.maestro/rtst/**`, `docs/plan/slices/217-rtst-forced-update.md`

#### Why

- RTST has no way to make old builds update. Its phone app reads `/api/app-config` only for the telemetry switch (`UsageConfig.cs:12`, `RemoteConfigClient.cs:28`); the Windows tray app reads nothing. Only Dino has the forced update (`DinoReader.App/AppConfigClient.cs:47`, `UpdatePage.cs`, `DinoReader.Core/AppConfig.cs:109`).
- Found 2026-10-30: every RTST release up to rtst-v1.73.2 still downloads the broken Norwegian pack from `packs-9` and older (Slice 201). Nobody uses Norwegian on those versions (30-day usage counts: no `nb` reading language), so the user decided to leave it for now; this slice is what will let us push users off a bad version next time.
- Real users are on old versions (30 days: Android mostly 1.57-1.71, iOS mostly 1.69, Windows 1.67.1; one event from 1.74+).

#### Decisions (2026-10-30)

- Same config file and shape as Dino, already served by the worker (`worker/appconfig.ts`, key `readthestupidtext/config.json`): `{"android":{"minVersion","latestVersion"},"ios":{...},"windows":{...},"telemetry":{...}}`. A missing or bad file or field means no forced update (as Dino).
- Same behaviour as Dino: installed version below `minVersion` shows a full-screen "Update needed" page with a button to the platform's store page (Play, App Store, Microsoft Store) and no way past it; below `latestVersion` only a dismissible "Update available" note. Share the pure parts (version compare, config parse) with Dino instead of copying them.
- Windows tray: the check runs at start and once a day; a forced update opens the control panel on the update notice and pauses reading until updated.
- Debug builds and unstamped versions never force an update (as Slice 167's rule for counts), so tests are not blocked.
- **Do not set `minVersion` in the live file until store builds above it are live on that platform** (see Slice 218); setting it is a separate, user-approved step.

#### Work

- [ ] Shared pure config + compare (move from `DinoReader.Core/AppConfig.cs` to Application if it fits; unit tests for compare and parse).
- [ ] Android and iOS: the client reads the platform keys; update page and note; store links.
- [ ] Windows tray: client, notice in the control panel, store link.
- [ ] Strings in every RTST locale (machine drafts marked).
- [ ] Maestro flow on the emulator with a test config (Debug points at a test key, never the live file).

#### Verification

- Fast: unit tests.
- Emulator: forced and soft update with a test config.
- Windows VM: tray notice with a test config.
- iOS: compile in CI; TestFlight hand check.
