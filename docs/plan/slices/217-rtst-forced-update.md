### Slice 217 — Read The Stupid Text: forced update from the remote config

*In progress (wave 2026-10-30), draft PR.* Port of Dino Reader's forced update (Slice 132) to Read The Stupid Text, on Android, iOS and the Windows tray app.

**Touches:** `src/ReadTheStupidText.Application/Usage/UsageConfig.cs` (or a new `Application/AppConfig/*` shared with Dino's `DinoReader.Core/AppConfig.cs`), `src/ReadTheStupidText.Mobile.Shared/Usage/RemoteConfigClient.cs`, `src/ReadTheStupidText.Mobile/App.xaml.cs`, `src/ReadTheStupidText.Mobile/UpdatePage*.cs` (new), `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/ReaderPage*.cs` (R15 card; Slice 215 adds one AutomationId to ReaderPage.xaml, accepted overlap), `src/ReadTheStupidText.App/**` (tray: config client and the update prompt), `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/ReadTheStupidText.App/Strings/**`, `tests/ReadTheStupidText.Tests/**`, `.maestro/rtst/**`, `docs/plan/slices/217-rtst-forced-update.md`

#### Why

- RTST has no way to make old builds update. Its phone app reads `/api/app-config` only for the telemetry switch (`UsageConfig.cs:12`, `RemoteConfigClient.cs:28`); the Windows tray app reads nothing. Only Dino has the forced update (`DinoReader.App/AppConfigClient.cs:47`, `UpdatePage.cs`, `DinoReader.Core/AppConfig.cs:109`).
- Found 2026-10-30: every RTST release up to rtst-v1.73.2 still downloads the broken Norwegian pack from `packs-9` and older (Slice 201). Nobody uses Norwegian on those versions (usage counts: no `nb` reading language; corrected 2026-10-30: those counts covered only about 3 days and exact `nb`/`no` codes), so the user decided to leave it for now; this slice is what will let us push users off a bad version next time.
- Real users are on old versions (30 days: Android mostly 1.57-1.71, iOS mostly 1.69, Windows 1.67.1; one event from 1.74+).

#### Decisions (2026-10-30)

- Same config file and shape as Dino, already served by the worker (`worker/appconfig.ts`, key `readthestupidtext/config.json`): `{"android":{"minVersion","latestVersion"},"ios":{...},"windows":{...},"telemetry":{...}}`. A missing or bad file or field means no forced update (as Dino).
- Same behaviour as Dino: installed version below `minVersion` shows a full-screen "Update needed" page with a button to the platform's store page (Play, App Store, Microsoft Store) and no way past it; below `latestVersion` only a dismissible "Update available" note. Share the pure parts (version compare, config parse) with Dino instead of copying them.
- Windows tray: the check runs at start and once a day; a forced update opens the control panel on the update notice and pauses reading until updated.
- Debug builds and unstamped versions never force an update (as Slice 167's rule for counts), so tests are not blocked.
- **Do not set `minVersion` in the live file until store builds above it are live on that platform** (see Slice 218); setting it is a separate, user-approved step.

#### Design (Batch 42 Updates and Paused Downloads mock, frames R14, R15, W3, W4, added 2026-10-30)

Four frames in `docs/design/rtst-phone/Batch 42 Updates and Paused Downloads.dc.html` (R14 line 27, R15 line 46, W3 line 73, W4 line 98; notes under each frame). The mock agrees with the plan on the two states and adds behaviour, listed in the open points. R14/R15 are 360x760 phones (top bar 30px `#5B57E8`, header 64px gradient `#5B57E8` to `#3B82F6`, glyph watermark 12%). W3/W4 show only the changed parts of the 344px control panel on a 380px backdrop. All copy is marked a draft. Icons are inline SVG strokes (a down arrow with a base line, and a close cross), not Phosphor; use the app's existing icon set for the same shapes.

- **Update needed, phone (R14; Android and iOS, same page).** Replaces the whole app: header bar only, no back, close or skip; Android back closes the app. Centred: 132px circle `rgba(91,87,232,.10)` with the 96px app tile (radius 24) and a 42px `#5B57E8` badge (white download arrow, 4px `#f4f5f9` ring) at bottom-right; title "Update needed" (24px, 600); body "This version has a problem that's been fixed. Get the new one to keep reading. Your settings and voices stay as they are." (15px, `rgba(0,0,0,.62)`, max 280px); button at the bottom, full width, 60px, radius 16, the header gradient, 17px 600, shadow `0 4px 14px rgba(91,87,232,.32)`. Button text "Open Google Play" (iOS: "Open the App Store"). No grown-up gate (Dino's frame 61 has one).
- **Update available, phone (R15; Android and iOS).** A one-line card above the text box on the main page, pushing the box down: white, 1px `rgba(91,87,232,.28)` border, radius 16, padding 6x4x6x14, gap 10. Left the 22px `#4A46D6` arrow icon, "A new version is ready" (15px, 600), a "Get it" button (44px high, radius 12, `rgba(91,87,232,.10)` fill, `#4A46D6` text, 15px 600) and a 44x44 close cross (14px, `rgba(0,0,0,.5)`). "Get it" opens the store page; the cross hides the card until the next `latestVersion`, remembered per version. Never shown while reading or on first run. Settings (R10) gets no row.
- **Update needed, Windows (W3).** Panel header status line reads "Reading paused". Body card (padding 14, radius 8, `rgba(255,255,255,.6)`): 32px `#3B6FE3` circle with the white arrow, title "Update needed" (14px, 600), text "This version has a problem that's been fixed. Reading starts again once you update." (12.5px), button "Open Microsoft Store" (32px, radius 5, `#3B6FE3`, 13px 600). The transport row below is at 35% opacity and unclickable. The panel opens by itself; the toggles are left out of the frame but also grey. Hotkey and auto-read do nothing except reopen the panel here. Closing is allowed; it returns on the next read try. The tray icon stays. The button opens `ms-windows-store://pdp/` (product id).
- **Update available, Windows (W4).** Status line "Ready". A bar under the header: padding 6x6x6x12, radius 8, `rgba(59,111,227,.08)` fill, 1px `rgba(59,111,227,.22)` border; 16px `#3B6FE3` arrow, "A new version is ready" (13px), "Get it" (28px high, radius 5, white `.72` fill, 12.5px) and a close cross. Shown only when the user opens the panel; no toast, never opens the panel; reading is normal. The cross hides it for that version.
- **Copy and checks.** Debug, unstamped builds, a missing or bad config, and an offline start with no saved config never force an update.

**New strings (every RTST locale on the phone, `AppResources*.resx`; Windows `Strings/**`):** "Update needed", the R14 body, "Open Google Play", "Open the App Store", "A new version is ready", "Get it", the W3 body, "Open Microsoft Store", "Reading paused" (check whether it exists already).

**Open point: no grown-up gate.** Plan says "same behaviour as Dino" (line 16); Dino's page opens a gate before the store. The mock says RTST has no gate. **Default:** no gate.

**Open point: R15 dismissal and no Settings row.** Plan says only "dismissible" (line 16). The mock remembers the dismissal per `latestVersion` and gives Settings (R10) no row, unlike Dino's frame 62. **Default:** accept both; Settings stays as it is.

**Open point: offline start.** The mock says an offline start with no saved config means no forced update. The plan does not mention caching the config (`RemoteConfigClient.cs` reads it live). **Default:** save the last good config on the device and use it offline; none saved means no force.

**Open point: Windows panel behaviour.** Plan line 18 says the panel opens on the update notice and reading pauses. The mock adds: the panel opens by itself, transport and toggles grey out, the hotkey and auto-read only reopen the panel, closing is allowed but it returns on the next read try, and the "Update available" bar never opens the panel or toasts. **Default:** build it as the mock shows; it needs the read path in `ReadAloudService` to check the forced-update state.

**Open point: touches.** R15 edits the phone main page and the W3/W4 panel XAML. The Touches line lists `App.xaml.cs` and `UpdatePage*` only on the phone. **Default:** add the main page files under `src/ReadTheStupidText.Mobile/` when claiming.

**Platform parity:** R14 and R15 serve Android and iOS from one page (only the button text differs); the Windows frames are the desktop head, not covered by the parity rule.

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
