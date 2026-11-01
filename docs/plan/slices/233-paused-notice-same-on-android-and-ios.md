### Slice 233 — "Download paused. Tap to finish." posts on the same events on Android and iOS (Slice 208 follow-up)

*Built (wave 2026-11-01), merged as PR #672 (b62197c).* Planned 2026-11-01. The user asked (2026-11-01) for the two platforms to work like each other instead of accepting Slice 208's difference.

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidPackKeepAlive.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/PackDownloadService.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosPackNotification.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/PackDownloadHost.cs`, `src/ReadTheStupidText.Mobile.Shared/PackDownloadThrottle.cs`, `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookDispatcher.cs`, `tests/ReadTheStupidText.Tests/**`, `.maestro/**`, `tools/phone/**`, `docs/plan/slices/233-paused-notice-same-on-android-and-ios.md`

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

- [x] Shared: move the "post or not" decision into one pure, unit-tested function (`PausedNoticePolicy` or in `PackDownloadHost`): inputs are the run's end (resumable / not, cancelled by the person) and whether the app is in the foreground; output is post / clear / nothing.
- [x] Android: `AndroidPackKeepAlive.Post` drops the timed-out-set requirement and uses the shared decision; `OnTimeout` still ends the run as resumable so the same path posts.
- [x] iOS: `IosPackNotification` uses the same shared decision (no behaviour change expected; prove it with the unit tests).
- [x] Unit tests: the policy table (each end type x foreground / background), the same table run for both platform hooks.
- [x] Debug marker unchanged (`PACKDL paused-notified`), plus the end reason in it, so a device run shows which end posted.

#### Verification

- `dotnet test tests/ReadTheStupidText.Tests` (the policy table).
- Android emulator (scripted): start a pack download, send the app to the background, cut the app's network with the per-app netpolicy block (never a whole-device cut over wireless adb), and see `PACKDL paused-notified` with reason network; restore, tap the notice, the download finishes. Same flow with the time limit still posts.
- iOS: TestFlight hand check, background the app during a download and turn Wi-Fi off (mobile data off): the notice appears in Notification Center.

#### Review notes (medium pass)

- Finding 6 (stale notice after a superseded automatic run): taken, with a redesign after the S21 run (see *Pause episode*, below). Fixed: paused-notification.ps1 marker window starts after this run's network cut.
- Skipped: 1 iOS "not Active" counts as away (the Decisions say so, matching Android's "not on screen"); 2 policy/service ownership refactor; 3 and 4 low, no user-visible failure beyond a duplicate-free notice; 5 posting on any resumable end is the slice's asked-for parity; 7 test style.

#### Review notes (check pass on the final code, `reviews/s233-medium-check.md`)

- None applied. 1: Android's pack channel is Importance.Low, so a re-post after the run-start clear never sounds or pops up (it is the same quiet notice as iOS's Passive one). 2: an OperationCanceledException that is not a cancel is the stall timeout, a real pause. 3: Android's Notify reports no failure, so claiming the alert before posting changes nothing; with notifications denied there is no notice either way. 4: an abandoned episode ends with the process. 5: the Decisions' "not Active". 6: the drop flag is checked only from Debug call sites. 7, 9: by design (the platform logs what it posted; the ongoing download notice and the paused notice are different states). 8: the clear touches only the dev app, and the harness puts its packs back.

#### Not yet verified

- iOS on a device: the VoiceOver, TestFlight, iPhone and iPad hand checks (per the user's answer above). Only the iOS compile was checked.
- The silent re-post on a repeated failed automatic resume: no scripted check asserts the alert/silent difference.
- A real network loss: the device run used the Debug-only `drop-pack-network` hook, not a real loss.

#### Changed while building

`PausedNoticePolicy.Decide` is shared by both platforms. Android now posts for any resumable end while the app is not on screen (the timed-out set is removed). iOS uses `ApplicationState` not `Active`. The pause episode (see above) clears the notice at every run start, alerts on the first post and re-posts silently (iOS `Passive`). The debug marker is `paused-notified pack=<id> reason=<offline|failed|transport>`. The Debug-only `drop-pack-network` hook replaced the netpolicy cut. Both checks, `paused-notification-network` (new) and `paused-notification`, clear the download hold and re-grant notifications first.

#### Device run (S21, 2026-11-01, run 6, aot build)

- `paused-notification-network`: PASS. Notice "Download paused. Tap to finish." at 0% done; the tap resumed at offset 987771 and the notice went away.
- `paused-notification` (time limit): PASS. 5% done, resumed, notice gone.
- Earlier runs found: the netpolicy cut has no effect under a dataSync foreground service; the notice stayed up during an automatic restart (this led to the episode design); `pm clear` resets POST_NOTIFICATIONS (channel importance NONE).

#### For CLAUDE.md (lifted 2026-11-01)

- `tools/CLAUDE.md`: `cmd netpolicy restrict-background-blacklist` does not cut an app that runs a dataSync foreground service; use a Debug hook (Slice 233: docs/plan/slices/233-paused-notice-same-on-android-and-ios.md).
- `tools/CLAUDE.md`: `pm clear` (`reset-dev-app -Level clear`) revokes POST_NOTIFICATIONS; re-grant it (Slice 233: docs/plan/slices/233-paused-notice-same-on-android-and-ios.md).
- `tools/CLAUDE.md`: `run-checks` needs `RTST_PHONE_OWNER` exported, or it waits on its own lease (Slice 233: docs/plan/slices/233-paused-notice-same-on-android-and-ios.md).

#### Device finding and fix (2026-11-01, S21)

`paused-notification-network` could never pass: it cut the network with `cmd netpolicy add restrict-background-blacklist <uid>`, and the download never went offline; likely because a running dataSync foreground service is exempt (the diag run showed procState FGS and netpolicy effective=NONE; not proven by a direct netpolicy dump, and the raw dump, kept only in the removed worktree, is gone). Fix: a Debug-only test hook `drop-pack-network` (next to `cancel-pack-downloads`; `PackDownloadThrottle.RequestDrop`, compiled out of Release) makes the running download's next read throw `HttpRequestException(ConnectionError)`, the same exception a dropped connection throws. The app's real path then runs: `PackErrorClassifier` gives Offline, so the marker carries `reason=offline` (a dropped connection is classified before it can reach the `transport` catch; the check accepts offline, failed or transport). The `-NetworkOnly` variant now backgrounds the app, fires the hook and no longer uses netpolicy (common.ps1's function stays for other users). Not yet verified on a device.

#### Pause episode (redesign, 2026-11-01, S21 second run)

Two strikes: first the notice was cleared at every run start (high finding 4: each failed automatic retry alerted again), then the clear was skipped for automatic restarts (S21: `paused-notification` failed "the paused notice is still shown after the download restarted", because the restart kept it up). One owner now, `PackDownloadHost`, keeps a per-pack in-memory "pause episode" flag (`PausedEpisodes`, `alerted`):

- Every run start (a person's resume or an automatic restart) removes the visible notice (`PackDownloadHost.RunStarted`) but keeps `alerted`.
- A resumable end posts the notice; the platform asks `PackDownloadHost.ClaimAlert` once it has decided to post. The first claim of an episode returns true (alert, and `alerted` is set); later ones return false and the notice is posted silently (Android: `SetOnlyAlertOnce(true)` (`Notification.Builder` has no `SetSilent`, and the pack channel is `Importance.Low`, so Android never made a sound here anyway); iOS: the first post is unchanged from before this slice, the silent re-post sets `InterruptionLevel = Passive` (no banner, no sound; iOS 15+, older versions get a plain re-post)). The old "already showing, skip" checks are gone on both platforms (the notice is always cleared at run start, so they would only hide a legitimate re-post).
- `alerted` is cleared only when the episode ends: finished, cancelled, non-resumable failure (`PackDownloadHost.ClearPaused`), or a person's resume (`RunStarted(byPerson: true)`).
- A process kill loses the flag (in memory only), so the next resumable end after a restart alerts again. Accepted.
- Unit tests: `PausedEpisodesTests` (first claim alerts, repeat is silent, each end re-arms, packs are independent).

Tooling finding (same run): `paused-notification-network` ended with the dev app in `VOICEPACK held-user-cancelled`, so the next check never started its download. Both `paused-notification` variants now clear that hold at their own start (`Clear-DevDownloadHold`: `reset-dev-app.ps1 -Level clear`; `-Level data` is not a valid level). Fast checks only; not yet run on a device, and the alert/silent difference is not asserted by any device check (hand check: airplane-mode the backgrounded app twice, one alert only).
