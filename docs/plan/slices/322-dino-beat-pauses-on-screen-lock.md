# Slice 322: Dino's silent beats pause when the screen locks

*Built 2026-11-08, merged as PR #963 (63271fbd). Taken over the same day from a dead session that stopped at "reviewed, device checks pending"; origin/main merged (90278ae3). Verified on the S21 (real lock) and the emulator; iOS and Windows not run.* Found by the 2026-11-08 device run (L69, Slice 278's open check "the beat pausing on screen lock"): on the emulator, after `input keyevent 26` (Asleep; the app logged `[GrownUps] closed on stop`), the silent beats kept going (`silent piece shown`, sentences 4-12, about every 1.2 s). Likely cause, from the code: the beat pauses only from `ReadingPage.OnDisappearing` (`src/DinoReader.App/ReadingPage.xaml.cs:952`), which Android does not fire on a screen lock. Evidence: `reviews/emu3/results.tsv` and `bugs.md` under the worktrees folder.

**Touches:** `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.Core/SilentBeat.cs`, `.maestro/dino/silent-piece-beat-lock.yaml`, `tests/DinoReader.Tests/SilentBeatTests.cs`

## Items

1. [x] First read Slice 278 (`docs/plan/slices/278-dino-store-books-only.md`) and the slice that added the silent beat, and write here what they decided should happen on a screen lock: the beat pauses, and resumes on unlock or waits for Play. Then confirm the cause from the code with file:line.
2. [x] The beat follows the same rule as the read's audio on screen lock, app background and return, on Android and iOS alike: one place that reacts to the app stopping (the MAUI window or app lifecycle that does fire on lock), not only `OnDisappearing`.
3. [x] A unit test for the pause and resume decision where it is pure, and a Maestro flow `silent-piece-beat-lock` (lock with keyevent 26 mid-beat, check no `silent piece shown` while locked, unlock, check the resume behaviour from item 1).

## Intended behaviour and cause (item 1, written before the code)

**Intended** (Slice 278 part B, `docs/plan/slices/278-dino-store-books-only.md` lines 188, 199, 207, 211; the beat was added by that slice; no other slice owns it): "the silent beat pauses when the page is covered (lock, background, gate) and goes on when shown". A piece with no sound has nothing to hear, so it must never run on unseen. `SilentBeat` (Core) stops the timer on cover; on show a **whole** beat starts again (no waiting for Play); a pause the person made stays paused. The same slice's "Not yet verified" listed "the beat pausing on screen lock (no scripted flow)".

**Cause, confirmed in the code:** the only cover signal was `ReadingPage.OnDisappearing` -> `ReadingSession.PauseBeatWhenHidden` (`ReadingPage.xaml.cs:952`), and `OnAppearing` -> `ResumeBeatWhenShown` (`:929`). The comment on `OnDisappearing` itself says it also fires for a locked screen, but on Android a lock stops the activity (`Window.Stopped`, the event `App.xaml.cs:68` already uses to close the grown-ups pages, logged `[GrownUps] closed on stop` in the 2026-11-08 run) without a Page `Disappearing`. So the timer in `ReadingSession.EndBeatAsync` (`:1001`) kept ticking. MAUI docs (context7 `/dotnet/maui`): `Window.Stopped` is raised when the app goes to the background (Android onStop; iOS `DidEnterBackground`), `Window.Resumed` when it returns (iOS `WillEnterForeground`), so one handler pair covers Android and iOS.

**Fix:** `SilentBeat` gets a second cover cause, the stopped window (`Background`/`Foreground`; `Hidden` = page covered or window stopped, so unlocking under a still-open gate does not start the beat). `ReadingPage` already watches the window (`WatchWindow`, `Stopped`/`Resumed` for the screen-awake rule) and now calls `ReadingSession.PauseBeatWhenWindowStopped` / `ResumeBeatWhenWindowBack` there; when the page stops watching it clears the flag, so it cannot hold the next read.

## Design after the second strike

The first design kept two cover flags in `SilentBeat` (page, window) plus `ForgetWindow`, and the page told the session about each window event. Three review findings hit that same area (window watch, forget, swap: `reviews/s322-high.md` #1, `reviews/s322-medium.md` #1-3): a forget that restarts nothing, a swap that resumed unconditionally, a window already stopped when the page first subscribes. A third patch on the same seam would not hold, so the shape changed (this replaces the "Fix" paragraph above):

- **One input to the beat:** `SilentBeat.SetHidden(bool hidden)` (pure, idempotent). Hidden returns "stop a running timer"; shown returns "start a whole beat now" (only when the beat is active and not held by the person). The separate page/window flags, `Hide/Show/Background/Foreground` and `ForgetWindow` are gone.
- **`ReadingPage` owns two facts** (`BeatCoverFacts` in Core: page shown, window in front) and after EVERY event (Appearing, Disappearing, `Window.Stopped`, `Window.Resumed`, a window swap) calls `ReadingSession.SetBeatHidden(!pageShown || !windowInForeground)`.
- **`windowInForeground` is read from the platform whenever the page (re)subscribes to a window**, not assumed: `WindowForeground.IsInForeground()`, one seam with an Android half (the current activity's AndroidX `Lifecycle.CurrentState` is at least `Started`), an iOS half (`UIApplication.ApplicationState != Background`) and a Windows half (always true). Docs used: context7 `/dotnet/maui` (Android `OnStop` -> `Window.Stopped`, `OnRestart` -> `Window.Resumed`; iOS `DidEnterBackground` -> `Stopped`, `WillEnterForeground` -> `Resumed`).
- **Unwatching sets nothing:** the page leaving already makes `pageShown` false, so `hidden` is true.
- Tests: `BeatCoverFactsTests` feeds every event order up to length 4 over appear, disappear, stop, resume, swap(fg), swap(bg) and checks the beat runs exactly when the page is shown and its window in front; `SilentBeatTests` keeps the order tests of the beat itself.
- Log lines: one pair, `silent piece paused, the window is not in front` (or `..., the page is not shown` when the page itself is the cause) and `silent piece resumed, the page is shown and its window is in front`; the Maestro flow's `expect-log` lines follow.
- Check 1 fixes (`reviews/s322-check1.md`): every recompute (`RefreshBeatCover`) re-reads `WindowForeground.IsInForeground()` (`bool?`, null = unknown leaves the fact unchanged; Window.Stopped is trusted, not re-read); `SilentBeat` and `BeatCoverFacts` both start hidden; the Windows half reports true because `Stopped` is not used there (unverified on the VM).

## Design after the third strike

Check 2 (`reviews/s322-check2.md` #1-4) showed that re-reading platform state inside the window handlers lags the event: MAUI raises `Window.Resumed` from Android `OnRestart` (before the activity reaches Started) and on iOS from `WillEnterForeground` (before Active), so a poll there still says "background" and the beat stays stopped after unlock. Rule, with one source per moment:

- **The event always wins.** `Window.Stopped` sets `windowInForeground = false`, `Window.Resumed` sets it true. Nothing is re-read inside those handlers.
- **The platform read (`WindowForeground.IsInForeground()`, `bool?`) is used only when the page subscribes to a window** (the first watch, or a swap to another window), where no event will arrive. `null` (unknown) keeps the current fact.
- **Every other recompute (Appearing, Disappearing) uses the stored fact**, no poll.
- Windows: `Stopped`/`Resumed` still drive the fact there; the Windows half of the read returns `null`, so it never overrides them (check2 #6).
- Initial facts are consistent with whether Appearing has happened: the session's beat starts in `BeatCoverFacts.StartsHidden` (= a new facts object's `Hidden`, page not shown yet), and unit tests pin that and that `Appeared` flips it (check2 #5).
- The first time the beat is shown logs `silent piece started`, later ones `silent piece resumed` (check2 #7).
- `BeatCoverFactsTests` covers every order up to length 4 of appear, disappear, stop, resume, swap(fg), swap(bg), swap(unknown), including resume-after-stop => shown.

### Skipped from review

- Skipped #6 (`reviews/s322-medium.md`): "SilentBeat.cs:20, low (style): two backing fields plus ref helper; could be simpler." - style only (and the two fields are gone with the second design anyway).
- Skipped #8 (`reviews/s322-check1.md`): "page event wiring needs a UI host; covered by the Maestro flow" - the page's event feeding cannot be unit-tested without a UI host; the Maestro flow covers it.
- Reverted after a second strike (`reviews/s322-check-final.md` #4, #5): #4 (a window re-subscribe overwrites the event fact with a platform read) and #5 (a null `CurrentActivity` reads as in front) were fixed, reviewed again (`reviews/s322-check-final2.md` #3, #5) and reverted; the code is back at the S21-proven version (commit 90278ae3), on which the S21 real-lock run passed 3/3. Both stay as edge cases under *Not yet verified*.
- Skipped #3, #6, #7, #8 (`reviews/s322-check-final.md`) and #4, #6, #7 (`reviews/s322-check-final2.md`): low severity, not worth a change in an area that has had two strikes.

- Not applied, recorded as known flake risk (`reviews/s322-check-final3.md`, 9 findings, none in app code): #1-#3 (medium) the screen cycle could still sleep the display during the flow's final launch/assert or setup waits; #4-#9 (low) loose log-order check, Stop-ScreenCycle ordering in `finally`, a leftover adb child racing Wake-Emulator, full-buffer polling, loose wiring test, Get-FlowNeeds parsed twice. The flow passed twice, so none were changed.
- `reviews/s322-check-final2.md`: #1 (clear logcat before starting the cycle) applied in the harness; the rest skipped (above) or moot after the revert of #4/#5.

## Changed while building (takeover, 2026-11-08)

- The original emulator flow never locked the screen: Maestro `pressKey: lock` is a no-op on the emulator (330 `dumpsys power` samples, all Awake), so the check had never exercised the pause. New harness need `# needs: screen-cycle` (`tools/emulator/run-flows.ps1` `Start-ScreenCycle`/`Stop-ScreenCycle`, `tools/emulator/dialog-guard.ps1` `Get-ScreenCycleCommands`, `tools/emulator/lint-flows.ps1`): KEYCODE_SLEEP for 4 s every 12 s, then WAKEUP and dismiss-keyguard, starting after the flow's logcat clear and the first `silent piece shown` (150 s cap), stopped in the `finally`. `.maestro/dino/silent-piece-beat-lock.yaml` rewritten to use it.
- Review passes after takeover: `reviews/s322-check-final.md` (8 findings: #1, #2 applied; #4, #5 applied then reverted after a second strike; #3, #6, #7, #8 skipped), `reviews/s322-check-final2.md` (7 findings), `reviews/s322-check-final3.md` (9 findings). Read 8/8, 7/7, 9/9.

## Device results (2026-11-08)

- S21, real lock (keyevent 223/224), 3/3 rounds PASS: paused within about 60 ms of sleep, resumed on unlock, no `silent piece shown` while asleep (log `reviews/s322-s21-lock-logcat.txt`).
- Emulator flow PASS twice: emulator-5556 at 19:50, and emulator-5554 at 20:05 on the final code (paused/resumed pairs 20:05:52.864/56.826 and 20:06:05.148/08.938; Asleep confirmed by `dumpsys power`).

#### For CLAUDE.md

- An emulator flow that needs the screen locked must declare `# needs: screen-cycle` (`tools/emulator/run-flows.ps1`); Maestro `pressKey: lock` does nothing on the emulator (Slice 322: docs/plan/slices/322-dino-beat-pauses-on-screen-lock.md).
- Silent-beat cover state: `ReadingPage` feeds `BeatCoverFacts` from page and window events only; the event always wins, the platform read (`WindowForeground`) is used only when subscribing to a window (Slice 322: docs/plan/slices/322-dino-beat-pauses-on-screen-lock.md).

#### Not yet verified

- iOS (no Mac): the same `Window.Stopped`/`Resumed` handlers, not run.
- Windows head: `WindowForeground` returns unknown and `Deactivated` is unhandled; not run on the VM.
- Edge cases reverted after the second strike: a window re-subscribe while an event fact is newer than the platform read, and a null `Platform.CurrentActivity`.
- Harness flake risk from `reviews/s322-check-final3.md` #1-#3: the screen cycle could sleep the display during the flow's final launch/assert or setup waits.
