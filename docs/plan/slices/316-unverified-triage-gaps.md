# Slice 316: Gaps found by the unverified-checks triage
**Product:** Shared

*Built 2026-11-08 (wave 2026-11-08), merged as PR #946 (0d2b0b6f); device checks partly run on emulator 1, the rest under Not yet verified.* The [triage of `unverified.md`](../notes/2026-11-08-unverified-triage.md) found lines that were not device checks but real missing pieces. They move here so they get built instead of waiting for a device run that can never close them. Slice 307 item 10 (`phone-network-guard.mjs`) was also one; it stays in plan.md's *Outstanding work*, where it is already tracked.

**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `.maestro/**`, `tools/phone/run-checks.ps1`, `tools/premade-audio/README.md`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/Platforms/Windows/**`, `tests/ReadTheStupidText.Tests/ReadAloudServiceGeneratingTests.cs`, `.github/scripts/ship-targets.mjs`, `docs/plan/slices/228-*.md`, `docs/plan/slices/233-*.md`, `src/ReadTheStupidText.Application/Reading/ReadTimingTracker.cs`, `src/ReadTheStupidText.Application/Reading/ReadTimingFormatter.cs`, `tests/ReadTheStupidText.Tests/ReadTimingFormatterTests.cs`, `tests/ReadTheStupidText.Tests/ReadTimingTrackerTests.cs`, `src/ReadTheStupidText.Mobile/Platforms/iOS/IosSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/AudioPackUsage.cs`, `src/DinoReader.Core/WindowPlacement.cs`, `src/DinoReader.App/DinoWindowFrame.cs`, `.github/scripts/ship-targets.test.mjs`, `.github/workflows/*.yml`, `tools/premade-audio/**`, `src/DinoReader.App/AudioPackStore.cs`, `src/DinoReader.Core/PremadeAudio.cs`, `tools/phone/checks/*.ps1`, `tools/phone/test-run-checks.ps1`, `.github/CLAUDE.md`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSpeechReader.cs`, `docs/plan/slices/316-*.md`

## Premise check (coordinator, 2026-11-08, `ground-in-code` on origin/main)

- Item 1: present. `ShowTiming` (`ReaderPage.xaml.cs:700-704`) formats `timing.Elapsed` with no clamp and no idle branch; elapsed is baseline + position uncapped (`ReadTimingTracker.cs:211-218`). iOS shares it (`IosSupertonicSpeechReader.cs` uses the same tracker and page). Put the clamp in the shared tracker or `ReadTimingFormatter` (pure, unit-tested), and the idle reset where the page sees Idle, so Android and iOS get it in one place. Windows (`ControlPanelWindow.xaml.cs:645`) gets the clamp for free through the formatter; no Windows-only change.
- Item 2: `ClipOffsetAsync` at `StoryVoicing.cs:208` (called at :178 when `fromPosition != 0`) has no test; add one in `tests/DinoReader.Tests` (shared code, all heads).
- Item 3: the read_voice count is built in `StoryVoicing.cs:75-82` (`AudioPackUsage.ReadVoice`) and recorded in `ReadingPage.xaml.cs:1343-1346`; the builder finds the failure path and adds message and URL there.
- Item 4: no `.maestro/ios` folder exists; write each flow platform-neutral in `.maestro/dino/` or `.maestro/rtst/` (Maestro runs the same YAML on iOS). The Slice 233 re-post check needs the phone (none tonight): write it into the `tools/phone/run-checks.ps1` catalogue, do not run it.
- Item 5: `Stop-CacheMissed` (`run-checks.ps1:751`) already prints `Format-NoBuildMiss`; add the missed inputs only if that line does not already name them. `tools/premade-audio/README.md:86` lists exit codes; add the `exit_code_for` rule (`generate.py:393`) only if missing.
- Item 6: "Android Notify reports no failure" is true (`AndroidPackKeepAlive.cs:187`, Notify returns void). "Worker before app" is a deploy-order fact, true by design. Still to read in code: no modal over the reader, Dismiss refresh before `_mode.Leave` (`GalleryPage.xaml.cs:1115-1121`, `BookSheet.cs:394,522`, `OnBookSheetDismissed` :1193), Idle means no download.
- Item 7 decision (coordinator, obvious behaviour): the logic is `DinoReader.Core/WindowPlacement.cs:69-111`, which passes `saved.Maximised` through after clamping. Keep reopening maximised when it was saved maximised (that is what the user left), but the restored (normal) bounds behind it must be the clamped ones, so un-maximising never gives a window bigger than the screen. Unit test in `tests/DinoReader.Tests`.
- Item 8: present. `ship-targets.mjs` `CORE_PATHS` (:98-113) has `'*.slnx'`, so a Dino-only line in `ReadTheStupidText.slnx` re-releases RTST. Fix: drop `*.slnx` from core if no release or test job builds through the slnx (check the workflows); if one does, keep it and record why. Update `ship-targets.test.mjs` and any workflow `paths` lists in step.
- Item 9: `gh run list --workflow tests.yml --limit 100` shows zero failed runs from 2026-10-26 to now. Close it with that evidence; no code change.

## Items

Android and iOS: items 1-3 and 7 are shared MAUI code; each scripted check gets an Android flow and the matching iOS flow, as Platform parity requires.

1. **RTST Android timer** (Slice 60): the elapsed label can show `elapsed > total` and is not reset on idle (`ReaderPage.xaml.cs` `ElapsedLabel`). Fix it and add a unit test for the clamp and the reset. Check whether iOS shares the bug.
2. **Mid-book Farsi read start** (Slice 296): `ClipOffsetAsync` for a read that starts mid-book is not modelled or run. Add a test that starts on a later page and checks the clip match count.
3. **Failure-path log lines** (Slice 267): the `read_voice` failure-path lines carry no message or URL. Add them, so a fallback in production names what failed.
4. **Missing scripted checks**: Review-pick-then-Read (Slice 304); the phone flow asserting that the tablet title stays hidden (Slice 242 item 6); the alert-versus-silent re-post on a repeated failed automatic resume (Slice 233).
5. **Tooling printouts**: `tools/phone/run-checks.ps1` `Stop-CacheMissed` prints the inputs that missed (Slice 294); document the `exit_code_for` exit rule in `tools/premade-audio/README.md` (Slice 259).
6. **Unchecked review skip reasons** (Slices 228 and 233): the code facts the skip reasons state ("Android's Notify reports no failure"; no modal over the reader; Dismiss refresh before `_mode.Leave`; Idle means no download; Worker before app). Read each one in the code. A false reason reopens its finding as a fix in this slice.
7. **Dino Windows window** (Slice 235): a window clamped to a small screen and saved as maximised reopens maximised. Decide: reopen at the clamped normal size, which is the obvious behaviour unless the code says otherwise.
8. **Release paths** (Slice 58): check that a Dino-only project change in `ReadTheStupidText.slnx` or the workflows no longer re-releases RTST (`ship-targets.mjs` core paths). Fix the path list if it still does.
9. **Flaky test**: `ReadAloudServiceGeneratingTests.Auto_read_prepares_the_head_start_before_the_settle_delay_ends` (timing; failed once in CI on 2026-10-21). Make it deterministic with a fake clock, or close it if it has not failed in CI since.

## Built (2026-11-08)

- Item 1: `ReadTimingFormatter.ClampToTotal` (elapsed never past a known total) and `Idle`; `Format` clamps (Windows gets it free). Final design (see *Design after the second strike*): `ReaderPage` resets the clock on Idle in arrival order and shows every timing update clamped (no `ShouldShow` state filter), and the readers stop timing before they raise Idle (`AndroidSupertonicSpeechReader.cs`, `AndroidSpeechReader.cs`). iOS shares `ReaderPage`, so one fix. Tests in `ReadTimingFormatterTests`.
- Item 2: `FarsiClipKeyTests` models the mid-book read (`ReaderTextsAsync(story, fromPosition)`, offset = whole spoken count minus partial's, as `ClipOffsetAsync`): with the offset every piece plays its own clip, without it the clips miss. `StoryVoicing` itself is MAUI-side, so this is a model, as that file's header says.
- Item 3 (after the Touches widening): `AudioPackStore.FailureDetail(ex, url)` as redesigned: no exception message is logged; the line carries the exception type, the HTTP status, the inner exception's type and the pack URL as scheme + host + port + path (`<invalid-url>` otherwise). The two "failed" lines and the "not in the store" line use it, the size-mismatch line adds the url. Tests in `AudioPackStoreTests`.
- Item 4: `.maestro/rtst/review-pick-then-read.yaml` (new; Debug marker `READ_LANGUAGE <code>` in `ReaderPage.SpeakWithLoadingAsync`, placed between the `IsCurrentRead` check and `AllowReadAsync`; the trailing Play tap was removed and the `ReadingText` wait is optional); `assertNotVisible ReadingTabletTitle` added to the phone flow `dino-landscape-reading.yaml`. The Slice 233 re-post check is now written (not run): `tools/phone/checks/paused-notification-repost.ps1` (header line = catalogue row) runs `paused-notification.ps1 -Repost`: after the second connection drop the re-post's notification flags must carry FLAG_ONLY_ALERT_ONCE (0x8), the first post's must not. I also added its row to the expected table in `tools/phone/test-run-checks.ps1` (outside the written Touches, needed for that test to pass).
- Item 5: `Stop-CacheMissed` now prints the key's inputs and which differ (`Write-BuildKeyInputs`, was missing in the phone runner); README gained the `exit_code_for` rule.
- Item 7: the code already did the decided thing (`WindowPlacement.Resolve` clamps the bounds and passes `Maximised` through; `DinoWindowFrame.Apply` moves to the clamped rect, then maximises). Two tests pin it; no code change.
- Item 8: `*.slnx` dropped from `CORE_PATHS` and from the `paths` of the eight workflows (no workflow builds or tests through the solution; each uses its own csproj); test case now expects no target. `.github/CLAUDE.md` was corrected in this PR (review medium 1). No job builds the solution: `git grep slnx origin/main -- .github` hits only path lists and tests.
- Item 9: closed with evidence, no code: `gh run list --workflow tests.yml --limit 100` (2026-11-08) covers 2026-10-26 to now: 73 success, 27 cancelled, 0 failed.

## Item 6 result: skip reasons read in code (2026-11-08)

- 233 "Android's Notify reports no failure": TRUE. `AndroidPackKeepAlive.cs:187` calls `manager.Notify(...)`, which returns void.
- 233 "Worker before app" / 228 findings 4, 8, 9: TRUE by design. Workers Builds deploys `main` on `worker/*` changes (`worker/CLAUDE.md`), at the same merge as the app build; production reaches users only through manual store promotion. Only internal testers on the same day's build can have `intro_step` events dropped for minutes.
- 228 "no modal opens over the reader": TRUE for navigation pages (`ReadingPage.xaml.cs` pushes none; the intro itself is a modal `NavigationPage`, `IntroWizardPage.cs:91`, and the reader is on its stack). An alert does show on a failed read (`ReadingPage.xaml.cs:1592`), but then the read has not Finished, so no pop is queued; and `PopIntroReadAsync` (:154) re-checks the reader is the top page.
- 228 "Dismiss refresh before `_mode.Leave`": TRUE for the start order, not the finish. `BookSheet.Dismiss()` (:350-358) calls `Dismissed` synchronously, so `OnBookSheetDismissed` (`GalleryPage.xaml.cs:1193`) starts `RefreshShelfAsync` before `ReadNowAsync` continues to `_mode.Leave()` (:1121); the refresh can end after Leave. Harmless: the tiles are built from `mode.IsGrownUp` when built and `Leave` raises `PropertyChanged`, which `OnModeChanged` (:349) applies. A failed refresh is caught (:412) and logged only. No fix.
- 228 "Idle means no download": TRUE in the installer (`VoicePackInstaller.cs`: Idle is the start state, a non-held cancel, or no language chosen). MOOT for Dino: since Slice 278 part B Dino has no `VoicePackInstaller` and `PlayForIntro` (`ReadingPage.xaml.cs:105`) plays once the page appeared.
- No false reason, so no finding reopens.

## Not yet verified

- The reader clock shows `0:00` after a read and never `elapsed > total` on a device (unit-tested only).
- The pack failure log line format (type, status, inner type, url) has not been seen in a device log.
- `paused-notification-repost` has never run on a phone: the `flags=0x` parsing of dumpsys is unproven.
- iOS flows (`rtst/review-pick-then-read`, `dino-landscape-reading`) not run (no Mac); iOS compile not run here.
- That a later edit clears the Review pick on the reader page: no flow step yet.

## Device run (emulator 1, `-NoBuild`, 2026-11-08)

- rtst `speed`: pass. dino `dino-landscape-reading`: pass, including `assertNotVisible ReadingTabletTitle` (no layout-check ok line, so the layout is not recorded).
- rtst `review-pick-then-read`: failed twice, then passed after the READ_LANGUAGE marker moved before `AllowReadAsync` (the emulator has no voice, so the marker after the voice check never logged) and the YAML change (`ReadingText` wait optional, Play tap removed).
- rtst `sentence-highlight`: failed, `Missing log marker(s) under tag RTST-TEST: SENTENCE 0 1`; the emulator has no voice, not caused by this slice.
- dino `pack-failure-network`: Maestro Kotlin error; the pack log line was not seen.

## From review

Read: `reviews/pr-946-high.md` 7/7, `pr-946-medium.md` 9/9, `pr-946-check1.md` 9/9, `check2` 5/5, `check3` 6/6, `check4` 4/4, `check5` 4/4, `check6` 5/5. Findings not listed below were applied (high 2, 3, 7; medium 1, 3-6, 8, 9; check1 1-5, 7, 9; check2 1, 2; check5 1, 4 (stray `tmp-mark.txt`, gone from the worktree)); high 7, medium 3 and 4 and check1 1-3 led to the pack-log redesign above, high 6, medium 5 and check1 4-5 to the clock redesign.

- Skipped #1 (`reviews/pr-946-high.md`): "Neither was seen on a device." - phone-only, under Not yet verified.
- Skipped #4 (`reviews/pr-946-high.md`): "Failure: a future agent believes a .slnx edit ships everything" - `.github/CLAUDE.md` was fixed later in this PR (medium 1).
- Skipped #5 (`reviews/pr-946-high.md`): "releases nothing in CI" - `git grep slnx origin/main -- .github` hits only path lists and tests: no job builds the solution.
- Skipped #6 (`reviews/pr-946-high.md`): "Whether reset-on-finish is wanted is only listed as an unverified device look." - the Idle reset is what item 1 asks; superseded by the second-strike design.
- Skipped #2 (`reviews/pr-946-medium.md`): "A solution edit (project mapping, Deploy rule) now ships and tests nothing" - same grep: no job builds the solution.
- Skipped #7 (`reviews/pr-946-medium.md`): "Flow never asserts detected language is Italian" - the device run decides; the flow later passed.
- Skipped #6 (`reviews/pr-946-check1.md`): "ClampToTotal pins elapsed to an estimated total." - the clamp is item 1.
- Skipped #8 (`reviews/pr-946-check1.md`): "differing dumpsys layout makes the repost check throw on every run" - a missing flags line is a check error by decision.
- Skipped #3 (`reviews/pr-946-check2.md`): "new Idle guard in RaiseTiming drops an early OnRangeStart" - `SpeakFrom` raises timing right after Playing.
- Skipped #4 (`reviews/pr-946-check2.md`): "Matches decided design; heads-up." - the decided design.
- Skipped #5 (`reviews/pr-946-check2.md`): "re-post check takes first PostTime though first notice was cancelled" - phone-only, under Not yet verified.
- Skipped #1 (`reviews/pr-946-check3.md`): "SafeUrl keeps the full path; a path-embedded token is logged." - low; pack URLs are the store's public paths.
- Skipped #2 (`reviews/pr-946-check3.md`): "only the immediate inner type is named" - low; accepted.
- Skipped #3 (`reviews/pr-946-check3.md`): "not-found catch adds only the type name" - low; accepted.
- Skipped #4 (`reviews/pr-946-check3.md`): "no test of the not-found or size-mismatch log" - low; accepted.
- Skipped #5 (`reviews/pr-946-check3.md`): "SafeUrl edge cases untested" - low; accepted.
- Skipped #6 (`reviews/pr-946-check3.md`): "other failure logs (255, 280, 343) still log only the type" - low; other logs are outside item 3.
- Skipped #1 (`reviews/pr-946-check4.md`): "inline magic string, not a TestHookLog constant" - low.
- Skipped #2 (`reviews/pr-946-check4.md`): "logs requested readingLanguage, not plan.Language" - low.
- Skipped #3 (`reviews/pr-946-check4.md`): "Misleading only." - low; header comment. The marker itself was moved between `IsCurrentRead` and `AllowReadAsync` by the coordinator after the emulator run.
- Skipped #4 (`reviews/pr-946-check4.md`): "No runtime bug." - low; helper not extracted.
- Skipped #2 (`reviews/pr-946-check5.md`): "Translate row tap toggles with no starting-state check" - low.
- Skipped #3 (`reviews/pr-946-check5.md`): "a failure reads as a regression to en." - low.
- Skipped #1 (`reviews/pr-946-check6.md`): "assumes Translate row starts ticked" - low.
- Skipped #2 (`reviews/pr-946-check6.md`): "optional ReadingText wait hides a missed Read tap" - low; the flow still fails on the marker.
- Skipped #3 (`reviews/pr-946-check6.md`): "assumes Translate row starts ticked" - low.
- Skipped #4 (`reviews/pr-946-check6.md`): "flow needs a Debug install" - low; documented in the flow header.
- Skipped #5 (`reviews/pr-946-check6.md`): "possible log flush race; speculative, unconfirmed." - low.

## Design after the second strike (coordinator, 2026-11-08)

Two areas came back in high, medium and check 1 (`reviews/pr-946-*.md`), so each gets one design instead of another patch.

- **Pack failure log lines (item 3).** Free-text scrubbing of `ex.Message` kept leaking (URL queries, userinfo, UNC and relative paths), so the message is not logged at all. A failure line carries a closed set only: the exception type, the HTTP status code when it is an `HttpRequestException` with one, the inner exception's type, and the pack URL as scheme + host + port + path (`Uri.GetComponents`, never userinfo or query; `<invalid-url>` when it does not parse). That names what failed (which pack, which kind of failure, which status) and cannot carry a device path or a token, because no free text from the device goes in.
- **Reader clock (item 1).** The `ShouldShow(state)` filter compared queued timing updates with the reader's live state, and it dropped a new read's first timing (total stuck at `--:--`). Rule now: on the main thread, in the order the events arrive, Idle resets the clock to `0:00` / `--:--`, and every timing update is shown, clamped to its total. A timing update raised after the read's own Idle is a reader bug, fixed in the reader that raises it: the reader stops its timing ticks before it raises Idle. Whether any reader (Android and iOS Supertonic, the system readers) can raise timing after Idle is read in the code, with file:line written here.
  - **Read answer (2026-11-08).** `AndroidSupertonicSpeechReader.cs`: both ways timing could follow the Idle of the same read are closed. A poll tick already past its delay when `StopPolling` cancelled it (`ReportProgress`, :1666, raising at :1687) now returns when its token is cancelled; a chunk whose synthesis finishes after Stop (`RecordChunkDuration` raising at :959, called from `GenerateChunkAsync` :806) now stops at `token.ThrowIfCancellationRequested()` first. `Stop` and `FinishNaturalEnd` (:487-495) already stopped the poll before `UpdateState(Idle)` (:495, :1216). `IosSupertonicSpeechReader.cs`: nothing to change, its timing is raised only at read start (:226), skip (:324) and `ResetProgress` (:448, before Idle at :436/:791), and its poll (`StopPolling` :322, :421, :782) raises none. `AvSpeechReader.cs`: `OnWillSpeak` (:269-279) returns unless the utterance is `_current`, which `Stop` clears (:96) before Idle (:103). `AndroidSpeechReader.cs` (system reader, in `Mobile.Shared/Platforms/Android/`): `Stop` (:253-259) set Idle with no guard while `OnRangeStart` (:389-395) runs on a binder thread, so one in-flight callback could raise timing after Idle. Closed: `RaiseTiming` (:355) returns while `State` is Idle (the utterance id is one constant, so state is the check); `SpeakFrom` sets Playing (:340) before its own raise (:341). No unit test: the logic sits in an Android `UtteranceProgressListener` and is not reachable from the test project.
