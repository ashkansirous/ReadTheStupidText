### Slice 233 — "Download paused. Tap to finish." posts on the same events on Android and iOS (Slice 208 follow-up)

*In progress (wave 2026-11-01), draft PR.* Planned 2026-11-01. The user asked (2026-11-01) for the two platforms to work like each other instead of accepting Slice 208's difference.

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidPackKeepAlive.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/PackDownloadService.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosPackNotification.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/PackDownloadHost.cs`, `tests/ReadTheStupidText.Tests/**`, `.maestro/**`, `tools/phone/**`, `docs/plan/slices/233-paused-notice-same-on-android-and-ios.md`

#### Why

Slice 208 posts the notice from one shared run-end path (`MobileVoicePackService.cs:212-247`: a resumable end calls `PackDownloadHost.Paused`), but each platform then gates it differently (read from origin/main on 2026-11-01):

- **Android** (`AndroidPackKeepAlive.Post`, `AndroidPackKeepAlive.cs:185-210`) posts only when the pack is in the timed-out set, filled only by `PackDownloadService.OnTimeout` (the dataSync time limit, `PackDownloadService.cs:94`), and the app is not on screen (`:165-177`). Every other resumable end returns silently (`:195`).
- **iOS** (`IosPackNotification.cs:75-80`, `:119`) posts for any resumable end (a terminal transport error after the retries, network gone) while `ApplicationState == Background`.

So a download that stops because the network went away while the app is in the background tells the user on iOS and says nothing on Android. Neither platform posts when the OS kills the process: both gates run in the process that ran the download, and nothing is left to run after it dies. That case is already the same on both and stays out of this slice. (The plan's earlier line "iOS also posts it when the app is suspended" was wrong; corrected here.)

#### Decisions

- **One rule on both platforms:** a pack download that ends resumable (network lost, time limit, a transport error after the retries) while the app is not in the foreground posts "Download paused. Tap to finish." A person's cancel, the notification's Cancel, a non-resumable error, success and an installed pack post nothing and clear it, as now.
- The "not in the foreground" test is one shared check with a platform hook (Android: not on screen; iOS: `ApplicationState` not `Active`), so the two can't drift again.
- Android keeps the time-limit path as one of the resumable ends, not a special gate.
- Process killed by the OS: not posted on either platform (unchanged); out of scope.

**User answers (2026-11-01):** iOS gets a compile check in the night run; the VoiceOver / TestFlight / iPhone / iPad hand checks go to this slice's *Not yet verified* for the user to do later.

#### Work

- [ ] Shared: move the "post or not" decision into one pure, unit-tested function (`PausedNoticePolicy` or in `PackDownloadHost`): inputs are the run's end (resumable / not, cancelled by the person) and whether the app is in the foreground; output is post / clear / nothing.
- [ ] Android: `AndroidPackKeepAlive.Post` drops the timed-out-set requirement and uses the shared decision; `OnTimeout` still ends the run as resumable so the same path posts.
- [ ] iOS: `IosPackNotification` uses the same shared decision (no behaviour change expected; prove it with the unit tests).
- [ ] Unit tests: the policy table (each end type x foreground / background), the same table run for both platform hooks.
- [ ] Debug marker unchanged (`PACKDL paused-notified`), plus the end reason in it, so a device run shows which end posted.

#### Verification

- `dotnet test tests/ReadTheStupidText.Tests` (the policy table).
- Android emulator (scripted): start a pack download, send the app to the background, cut the app's network with the per-app netpolicy block (never a whole-device cut over wireless adb), and see `PACKDL paused-notified` with reason network; restore, tap the notice, the download finishes. Same flow with the time limit still posts.
- iOS: TestFlight hand check, background the app during a download and turn Wi-Fi off (mobile data off): the notice appears in Notification Center.

#### Not yet verified

- Everything: planned only.
