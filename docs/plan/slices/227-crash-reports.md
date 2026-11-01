### Slice 227 — Crash and error reports from the phone apps (Dino Reader and RTST, Android and iOS)

*Built 2026-10-31, merged as PR #654 (b8c420d); Worker deployed, not yet verified on iOS.* Planned 2026-10-31. From a friend's iPhone crashing Dino Reader several times on 2026-10-31 and nothing in our data showing why.

**Touches:** `src/ReadTheStupidText.Application/Crashes/**` (new), `src/ReadTheStupidText.Application/Usage/UsageEligibility.cs`, `src/ReadTheStupidText.Mobile.Shared/Crashes/**` (new), `src/ReadTheStupidText.Mobile.Shared/Usage/UsageGate.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSystemLog.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSystemLog.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MobileSharedServices.Android.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/MobileSharedServices.iOS.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/Crashes/**` (new), `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/Crashes/**` (new), `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/DinoLog.cs`, `src/DinoReader.App/Platforms/iOS/PrivacyInfo.xcprivacy`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/App.xaml.cs`, `src/ReadTheStupidText.Mobile/Platforms/iOS/PrivacyInfo.xcprivacy`, `worker/crashes.ts` (new), `worker/crashes.test.mjs` (new), `worker/index.ts`, `worker/download-keys.mjs`, `worker/CLAUDE.md`, `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `tools/crashes/**` (new), `tests/ReadTheStupidText.Tests/Crashes/**` (new), `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html`, `STORE.md`, `docs/plan/slices/227-crash-reports.md`

**Migrations:** none

#### Why

- **Nothing records a crash.** `dino_usage`/`rtst_usage` take 17 closed event names, none a crash (`worker/events.ts`). `IosSystemLog` writes to the console only and `AndroidSystemLog` to logcat only ("nothing is written to disk or sent anywhere"); both are gone after a crash. No `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` or `MarshalManagedException` handler exists. The only logs ever decided (Decisions 26, 27) are the Windows tray app's local files.
- **Apple has almost nothing either** (checked 2026-10-31 through the App Store Connect API, key "LCM CI", App Manager): no diagnostic signatures for any build (1.81.1 empty, the rest 404, no 401/403), and one TestFlight crash submission, from 2026-10-28 on build 1.73.1 (see item 9). TestFlight sends a crash to us only when the tester taps Share in TestFlight's prompt, and Dino has never been submitted to the App Store, so Xcode Organizer data does not exist.
- **Likely leak, not proven:** Dino is TestFlight-only on iOS, yet `dino_usage` holds 27 iOS rows from 2026-10-30/31 (1.81.1, 1.82.0, 1.83.0). `UsageGate.IsNonStoreInstall` drops a build whose `AppStoreReceiptUrl` ends in `sandboxReceipt`; either that check misses TestFlight on iOS 26/27 or the rows come from somewhere else. Item 1 settles it, and the crash gate needs a reliable TestFlight test anyway.

#### Decisions locked with the user (2026-10-31, RefineScope)

1. **Our own pipe.** The apps post crash reports to our Worker, which keeps each report in R2 and writes one count to a new Analytics Engine dataset. No third-party SDK. Plus tooling that pulls Apple's and Google's own crash data.
2. **What is captured:** unhandled crashes (type, message, stack) and the errors the app already logs, through a small rolling log file on the phone (last few hundred lines). The tail goes out with the report on the next launch.
3. **TestFlight installs send crash reports**, tagged `testflight`, kept out of the usage counts. This is the user's exception to the production-only rule, for crash reports only. Debug builds, unstamped versions, emulators/simulators, Firebase Test Lab, robots and our harnesses' off switch still send nothing.
4. **Both MAUI apps** (Dino Reader and RTST mobile), **Android and iOS**, built once in `Mobile.Shared`.

Settled here (planning): the reports follow the user's existing "anonymous usage" switch, which is on by default (one switch, label updated to mention crash reports), and a new remote-config key `crashReports.enabled` (default off). As with Decision 145, it is switched on only after the store privacy forms say "Crash data" (item 8). No identifier of any kind: the Worker names the R2 object with its own random id.

#### Work

- [x] **1. TestFlight test that works (gate).** Check which signal tells App Store, TestFlight and Xcode installs apart on iOS 26/27 (StoreKit 2 `AppTransaction.environment` vs. the receipt URL; context7/Apple docs first). Put one tested `InstallChannel` (`store`, `testflight`, `dev`) in `UsageEligibility`/`UsageGate`. Usage counts keep dropping `testflight`. A new `CrashEligibility` allows `store` and `testflight` and drops everything else the usage gate drops. Android: `store` for a Play install (installer package), otherwise `dev`. Unit tests for both rules. Re-query `dino_usage` iOS rows after the fix ships to confirm the leak closed.
- [x] **2. Smallest end to end: Dino, a managed crash, Android and iOS.**
  - Application: a `CrashReport` record (app `dino`/`rtst`, platform, version, channel, kind, exception type, message capped at 1 KB, stack capped at 16 KB, log tail, UTC minute, the same coarse device buckets as Slice 166) and a pure `CrashReportJson` writer/parser with unit tests.
  - Mobile.Shared `CrashCapture`: on `AppDomain.CurrentDomain.UnhandledException` only (review pass 2: Android's `UnhandledExceptionRaiser` also fires for exceptions that are handled afterwards, so it is not hooked), synchronously write one small pending report file into app data (type, message, stack, time, kind; no log tail, no log write, no lock, no preference read, no network) and let the crash continue. Never throws. (Review pass 1: iOS has no `MarshalManagedException` hook, because it also fires for exceptions that are caught later; iOS relies on `AppDomain.UnhandledException`, MetricKit and the run marker. The sender attaches the log tail at the next launch from the rolling file, read at start-up before the new run logs anything; only `managed` reports get it.)
  - `CrashReportSender`: on the next launch, off the UI thread and after the first screen, post each pending file to `POST /api/crash` on the app's host (no redirects, like `UsageQueueSender`). Delete it on 2xx or 400/413/422 (`CrashSendPolicy`); keep it on 404/405 (Worker not deployed yet), 408, 429, 5xx and network failure; the switch is read again before every report, and the previous run's tail goes to the newest pending managed report only; at most 10 pending files, the oldest dropped first.
  - Worker `worker/crashes.ts`: `POST /api/crash` on both hosts, plus the `/dino` twin and `run_worker_first` entries. Closed lists for app/platform/kind/channel, the version pattern and the unstamped and pre-1.0 drop from `events.ts`, and the Android/iOS x64/x86 drop. Body at most 64 KB, strings capped, answered 204 when `crashReports.enabled` is off. It writes the JSON to R2 `crashes/<app>/<yyyy-mm-dd>/<random id>.json` and one point to a new Analytics Engine dataset `app_crashes` (app, platform, version, channel, kind, exception type, country, device buckets). Bindings go in both wrangler configs. A rate limit per connection, with the IP never stored (context7 for the Workers rate-limit binding). `worker/crashes.test.mjs` under node.
  - A Debug-only test hook that throws on demand, for the device check.
- [x] **3. The log tail.** A `RollingLogFile` (two files of 128 KB in app data, swapped when full; one line per call, flushed on Warning/Error) decorates `AndroidSystemLog`/`IosSystemLog` in DI and in `DinoLog.Resolve`. A crash report takes its last 300 lines (max 32 KB). The file is written, and the run marker kept, only while crash reporting could send (an eligible build and the usage switch on); when the switch turns off or the build may not send, the log files, the pending reports and the marker are deleted (Review pass 1). Exception messages in log lines and reports have every quoted piece of text (`'...'`, `"..."`, `«...»`, `“...”`) replaced by `<text>`, file paths by `<path>`, and are cut to 200 characters. Audit every `ISystemLog` call in Mobile.Shared, Dino and RTST mobile for user content (typed or read text, book titles, file names, OCR text) and remove or reduce what is found. A unit test feeds a known user string through the read paths' logging and asserts it never reaches the file. `TaskScheduler.UnobservedTaskException` is logged as an Error line (in the tail), not a crash.
- [x] **4. Crashes the managed hooks never see.** A run marker is written when the app comes to the front and removed when it goes to the background. If it is still there on the next launch and no pending report was written, the app sends an `unclean_exit` report with the log tail (catches native crashes in sherpa-onnx/onnxruntime and memory kills). One crash gives one report (Review pass 1): Android drops an exit-reason `native` report within 2 minutes of a managed report and writes no `unclean_exit` when a managed report or an exit reason explains the marker; on iOS the app notes each managed or `unclean_exit` report in a small ledger (`crashes/reported.txt`, 3 days) and a MetricKit crash diagnostic whose period covers an unclaimed entry is dropped (MetricKit gives only the period, so the match is by count). While the switch is off the exit-reason watermark still moves on and MetricKit payloads are discarded.
  - iOS: an `MXMetricManager` subscriber sends each `MXCrashDiagnostic` (and hang diagnostics) as kind `native`, with the call-stack JSON capped.
  - Android: `ActivityManager.GetHistoricalProcessExitReasons` (API 30+) sends each new `REASON_CRASH_NATIVE`, `REASON_ANR` and `REASON_LOW_MEMORY` since the last one seen, with its description and capped trace (a tombstone is capped to 9 KB before base64, so the stored text fits the stack cap). A low-memory kill is reported only when the process was in the foreground or visible; background kills are ignored.
- [x] **5. RTST mobile.** The same `CrashCapture`/`RollingLogFile`/sender wiring in `src/ReadTheStupidText.Mobile` with app `rtst`, on Android and iOS.
- [x] **6. Reading them: `tools/crashes/`.**
  - `pull-crashes.mjs`: lists and downloads R2 crash objects for an app and date range, groups them by exception type and top frame, and prints counts plus the newest log tail per group. It reads with a read-only R2 token from `D:\secrets`, created and recorded with `manage-secrets`.
  - `pull-apple.py`: App Store Connect `diagnosticSignatures` + logs and TestFlight `betaFeedbackCrashSubmissions` + crash logs for both apps (from the 2026-10-31 scratch script).
  - Google Play: the Play Developer Reporting API (`vitals.errors` issues and reports) for both apps, if the existing Play service account has the reporting scope; if it does not, record that and stop.
  - A `worker/CLAUDE.md` rule and a line in the `analytics-query` brief so "did it crash?" goes to these scripts.
- [x] **7. Privacy docs.** `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html` and both apps' `PrivacyInfo.xcprivacy` add Crash Data (`NSPrivacyCollectedDataTypeCrashData`, not linked, no tracking, App Functionality). `STORE.md` gets the store-form rows. The settings switch text mentions crash reports in all app languages.
- [x] **8. Store forms, then switch on.** Play Data safety "Crash logs" (through the Play API where it allows, else the user), App Store App Privacy "Crash Data" for both apps (user, App Store Connect), and the Microsoft Store privacy URL (unchanged). Then set `crashReports.enabled: true` in both remote configs; that is the main thread's call, after the forms are saved.
  - 2026-11-01, done in the user's Chrome at their request: App Store App Privacy published for both apps (Crash Data, not linked, no tracking, App Functionality). Play Data safety saved for both apps (Crash logs: collected, not shared, not ephemeral, optional, Analytics, which is where Play's own wording puts crash diagnosis); the "Submit for review" click in Publishing overview was blocked by the permission check, so the user sent both.
  - 2026-11-01 ~11:45, switched on: Play Console Publishing overview for both apps shows "Last published on November 1, 2026" with no change waiting, so the Data safety change is live. The main thread set `"crashReports":{"enabled":true}` in both R2 config objects (`readthestupidtext-downloads/dinoreader/config.json` and `readthestupidtext/config.json`, other fields unchanged) with `wrangler r2 object put --remote`. Read back: `GET https://dinoreader.sirous.uk/api/app-config` and `https://readthestupidtext.sirous.uk/api/app-config` both return `"crashReports":{"enabled":true}`.
  - Still open: the Play Developer Reporting API is off in Cloud project 607390827721 (`read-the-stupid-text`); enabling it with the Play key was refused by the permission check, so it waits on the user clicking Enable at https://console.cloud.google.com/apis/library/playdeveloperreporting.googleapis.com?project=read-the-stupid-text.
- [ ] **9. The 2026-10-28 TestFlight crash.** Symbolicate it against build 1.73.1's dSYM (binary UUID `f672ab4737943faeaff6a739de0777ed`): SIGABRT, `-[__NSDictionaryM __setObject:forKey:]: object cannot be nil` during `UIView` layout, iPhone 12 on iOS 26.6.2. Find the managed frame, check whether a later build still has it, and fix it here or in its own slice.

#### Verification

- Unit: `InstallChannel` and `CrashEligibility` rules, `CrashReportJson` round trip and caps, the rolling-file swap and tail, and the user-text test (`dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`). Worker: `npm ci && npm run typecheck` and `node --test worker/crashes.test.mjs`.
- Android emulator (scripted, both apps): Debug hook crash, relaunch, and a report reaches a local `wrangler dev` with its log tail. An `unclean_exit` after `am kill` while in front. The harness off switch sends nothing.
- iOS: compile-checked in CI; on a TestFlight build on a real iPhone, a forced crash shows up in R2 tagged `testflight`. MetricKit is checked only on a real device, 24 h later.
- After deploy: `pull-crashes.mjs` lists the test reports, and `app_crashes` has the matching rows; `dino_usage` gets no TestFlight rows.

#### Changed while building

- Items 1-7 built; item 8 done 2026-11-01 (forms published, `crashReports.enabled` on, except the Reporting API Enable click, see item 8); item 9 (symbolicate the 2026-10-28 TestFlight crash) is not done.
- Device checks found two bugs in the first runs, both fixed: `CrashReporter` installed its handlers only when the gate was open at start (the Debug stub is set mid-run), so handlers now install always and write only while the gate is open; and a Debug build's channel is `dev`, for which the report builder makes nothing, so the stub now makes a Debug build report as `store`, and a skipped report logs a line. Production builds were not affected by either.

#### From review

- High pass (`reviews/s227-high.md`): 16 findings, all fixed.
- Medium pass (`reviews/s227-medium.md`): 10 findings, 8 fixed. Left as is: finding 3 (an unclear iOS receipt is tagged `testflight`, by decision) and finding 8 (native crash dedupe by 2-minute proximity, accepted).

#### Verified on a device

- Android emulator, 2026-10-31: scripted flows `.maestro/dino/crash-report-reaches-stub.yaml` and `.maestro/rtst/...` PASS. The managed report is saved in the crashing process, sent with 204 on relaunch, the body has app/platform/kind/channel/type right, and the log tail has the known line.

#### Not yet verified

- iOS on a real TestFlight build: a managed crash reaches the Worker tagged `testflight`, and the receipt name is logged (whether `sandboxReceipt` is what TestFlight shows on iOS 26/27).
- MetricKit diagnostics: a `native` report about 24 h later, real device only.
- `crash-test kind=kill` gives exactly one `unclean_exit` (no flow yet).
- Android exit reasons (native, ANR, foreground low memory) on a device.
- The Worker after deploy: `POST /api/crash` answers 204 with config off; an R2 object and an `app_crashes` row appear once on.
- `dino_usage` iOS TestFlight leak closed (cause still unproven); the 27 leaked rows remain stored.
- `pull-crashes.mjs`, `pull-apple.py` and `pull-play.py` not run against the real services. `pull-play.py` cannot run yet: on 2026-11-01 the Play key reached Google but the Play Developer Reporting API is switched off in Cloud project 607390827721, and switching it on with that key was refused by the permission check (Waiting on the user in plan.md; recorded by the 2026-11-01 feedback loop).

#### Outstanding after merge (main thread)

- Done 2026-10-31: Worker deployed (Workers Builds success on b8c420d); live `POST https://dinoreader.sirous.uk/api/crash` answers 204 with crash reports off; `/crashes/x.json` answers 404; R2 lifecycle rule `crash-reports-90d` (prefix `crashes/`, expire 90 days) added.
- Open: the read-only R2 token `CF_R2_CRASHES_READ` could not be created (no Cloudflare token in `D:\secrets` may create API tokens). Either the user creates an R2 read token in the Cloudflare dashboard, or `pull-crashes.mjs` is changed to read through the Cloudflare REST API with wrangler's own login (no new token).
- Done 2026-11-01: the Play Data safety change was sent and is live on both apps, the App Store forms are published, and `crashReports.enabled` is true in both remote configs (item 8).
- Item 9 (symbolicate the 2026-10-28 TestFlight crash) open.

#### For CLAUDE.md (lifted 2026-10-31)

- `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`: log calls never carry typed or read text, book titles or file names (only kinds, lengths, ids). (Slice 227: docs/plan/slices/227-crash-reports.md)
- `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`: crash handlers install always and write only while the gate is open. (Slice 227: docs/plan/slices/227-crash-reports.md)
- `worker/CLAUDE.md`: the `crashes/` prefix is never served; `CRASH_LIMITER` is required (a missing binding answers 503). (Slice 227: docs/plan/slices/227-crash-reports.md)
