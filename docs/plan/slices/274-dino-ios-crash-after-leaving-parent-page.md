# Slice 274 — Dino iPhone crash: leave the app from the grown-ups pages and come back

*Planned 2026-11-06.* Bug fix, Dino Reader, iOS first (Android checked for the same path). Reported by the user on 2026-11-06 ~00:10 UTC, testing the TestFlight build with Slice 270: "no crash on reading stories, but changing the app from parent page and coming back caused a crash."

**Touches:** `src/DinoReader.App/**` (expected: `App.xaml.cs` lifecycle, `GatePage.xaml.cs` and the grown-ups pages, `Platforms/iOS/**`), `src/ReadTheStupidText.Mobile.Shared/**` (only if the cause is in shared lifecycle or crash code), `src/ReadTheStupidText.Application/Crashes/**` (only if the cause is the rolling log), `tests/DinoReader.Tests/**`, `.maestro/dino/**`, `docs/plan/slices/274-dino-ios-crash-after-leaving-parent-page.md`. Narrow it once the cause is known.

## What we know (read 2026-11-06 00:15 UTC)

- `node tools/crashes/pull-crashes.mjs --app dino --from 2026-11-05 --kind managed,native` shows **no report of this crash**: the newest iOS report is the 2026-11-05 08:25 `FinishedPlaying` group that Slice 270 fixed. Reports saved in `D:/ReadTheStupidText-worktrees/reviews/ios-crash-1106/`. So either the app has not been opened again since (a managed report is sent on the next launch), or iOS killed the app in a way the managed handler does not see (watchdog, `0xdead10cc` on suspension, a native crash); native reports reach the store later (Slice 243/244).
- "Parent page" is the grown-ups area behind the gate (`GatePage.xaml.cs` and the pages it opens). The user switched to another app there and came back.
- Related open work: Slice 244 item 5 (`0xdead10cc`, iOS killed the app for a file held open across suspension; cause unproven, the rolling log writer is the one handle kept open) and Slice 243 (crash report gaps). Do not duplicate them: if the cause is the same, fix it here and note it in 244.

## Work

- [ ] 1. **Find the report.** Re-run `pull-crashes.mjs --app dino --from 2026-11-05 --kind managed,native` (it needs wrangler's login: `npm ci` at the repo root first if `npx --no-install wrangler` is missing). If nothing new: read Apple's crash data for the TestFlight build through the App Store Connect API (TestFlight crash feedback submissions and their crash logs; the shared Apple key in `D:/secrets`, see memory/`reference-apple-account-shared`; check the endpoint with `web-research` first). Also read `dino_usage_beta` around 2026-11-06 00:00-00:15 UTC (`analytics-query`) for the session's last events before the crash. Write what each source showed here.
- [ ] 2. **Find the cause** from the report: the stack (managed) or the termination reason (native: `0xdead10cc`, watchdog `0x8badf00d`, memory). Read the grown-ups pages' `OnAppearing`/`OnDisappearing` and the app's `OnSleep`/`OnResume`/`Window` lifecycle handlers for what runs on leaving and returning on these pages and not on the reading page. A cause read from code stays "likely" until a run or a report shows it.
- [ ] 3. **Fix it on iOS and Android** (same lifecycle code path on both; Android gets the same fix even if it does not crash there).
- [ ] 4. **A check**: an emulator flow that opens the grown-ups area, sends the app to the background (`adb shell input keyevent KEYCODE_HOME` or the Maestro equivalent), brings it back and asserts the page is alive; the iOS check goes to the user's iPhone on the next TestFlight build (`unverified.md`), and the crash store is read the next morning.

## Verification

- `dotnet test tests/DinoReader.Tests`; the new emulator flow passes (and fails on the old code if the bug also shows on Android).
- The user's iPhone, next TestFlight build: grown-ups page, switch app, come back: no crash; `pull-crashes.mjs` the next day shows no new report for that build.
