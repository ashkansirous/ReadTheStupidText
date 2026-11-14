# Slice 244 — Dino: find and fix the crashes of 2026-10-31 and 2026-11-01
**Product:** Dino

*In progress (wave 2026-11-14, night 2026-11-14-1547 wave 4: audio-backed simulator run), draft PR.* *Workflow built 2026-11-14 (wave 62), PR merged for the workflow; simulator run 2 passed the navigation flow 2026-11-14 (no audio pack), an audio-backed run and the iPhone 8 run are open.* *Items 2-5 built 2026-11-02, merged as PR #722 (6d3a0e5d). Item 1's iOS simulator / iPhone 8 reproduction and item 6's proof stay open, so the slice stays open after merge.* *Planned 2026-11-01.* From the user, after testing Dino Reader on an iPhone: "there are a lot of crashes ... as far as
I can tell, when the story is playing and I click on back, it does not pause/stop the story", then "another slice to
figuring out what can have cause the crashes today and yesterday and fixing them".

**Touches (wave 4, audio-backed simulator run):** `.maestro/dino/ios-read-audio.yaml` (new), `.github/workflows/dinoreader-ios-sim.yml`, `.github/scripts/ios-sim-*.sh` (only if the flow needs a parameter), `.github/scripts/ship-targets.test.mjs` (only if a new test-only file needs an exclusion), `docs/plan/slices/244-dino-ios-crashes-2026-11-01.md`

**Depends on:** none. Slice 243 adds the dSYM and log lines that would prove a cause; this slice does not wait for it
and works from the evidence below plus reproduction.
**Touches (current layout after Slice 371, set at claim 2026-11-14):** `src/Dino/DinoReader.App/ReadingPage.xaml.cs`, `src/Dino/DinoReader.App/Reading*.cs`, `src/Dino/DinoReader.App/Story*.cs`,
`src/Dino/DinoReader.App/Platforms/iOS/**`, `src/Dino/DinoReader.App/Testing/**`, `src/Shared/Sirous.Reading.Mobile.Shared/Platforms/iOS/**`,
`src/Shared/Sirous.Reading.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader*.cs`,
`src/Shared/Sirous.Reading.Mobile.Shared/Crashes/**`, `src/Shared/Sirous.Reading.Mobile.Shared/**/MobileVoicePackService.cs`,
`src/Shared/Sirous.Reading.Application/**/SpeechTextChunker.cs`, `tests/Dino/DinoReader.Tests/**`, `tests/Shared/Shared.Tests/**`,
`src/Dino/DinoReader.App/CLAUDE.md`, `.maestro/dino/**`, `.maestro/README.md`, `.github/workflows/dinoreader-ios-sim.yml` (new),
`.github/scripts/ios-sim-*.sh` (new), `.github/CLAUDE.md`, `docs/plan/slices/244-dino-ios-crashes-2026-11-01.md`; widened after review pass 2 (coordinator, 2026-11-14): `.github/workflows/release-rtst.yml` and `.github/workflows/release-dino.yml` (path exclusions for the new test-only files), `.github/scripts/ship-targets.test.mjs` (its `RTST_EXTRA_NEGATIONS` list names the two new exclusions)

## Claim notes (night 2026-11-14-1547 wave 4: audio-backed simulator run)

- No test hook installs a real audio pack offline (TestStore.cs:122-131 builds a 4-byte fake clip for gingerbread-man `sv` only; `import-test-book` imports text only), so the audio-backed run uses the live store: a new flow `.maestro/dino/ios-read-audio.yaml` with `test-store mode=off`, which fetches a real `audio-<lang>-v<n>.zip` through `/api/stories` (AudioPackStore.cs:597), then 3 rounds of read, Back, re-read, background, as the Not yet verified line asks. The workflow gets a `flow` dispatch input (default stays the current flow).
- Production-only analytics: the run is a Debug simulator build; the builder confirms from the code that a Debug build sends no usage event or crash report, before the first run. The run reads the live store and downloads one pack (read only, no live state changed).
- The run is dispatched on the branch (`gh workflow run dinoreader-ios-sim.yml --ref fix/244-sim-audio -f flow=...`) before merge; its result goes into item 6.
- The clip proof is in the flow, not the log (review `reviews/s244c-high.md` #2-4): each round waits until the sound pill `ReadingAudioStatus` is gone (shown only while a piece has no clip), so a device-voice read fails the flow. The log-count step stays warning-only: the hook log is Console output on iOS and the rolling log file is not written in a Debug build.
- Skipped #5 (`reviews/s244c-high.md`): "The 6 s 'let the clip play' wait is an optional extendedWaitUntil on a never-existing id used as a sleep" - low, triaged out by the caller.
- Skipped #6 (`reviews/s244c-high.md`): "Depends on live store listing three-little-pigs with a Farsi pack; nothing checks it" - low, triaged out; the pill wait now fails the flow if no pack installs.
- Skipped #8 (`reviews/s244c-high.md`): "`log show --last 60m` not scoped to flow start" - low, triaged out by the caller.
- Applied #1 (`reviews/s244c-medium.md`): after the 6 s play wait each round now also has `assertNotVisible: id ReadingAudioStatus`.
- Skipped #2 (`reviews/s244c-medium.md`): ""Check the pack clips played" only warns on zero clip lines; never fails the job." - repeat of high #2/#3: a Debug iOS build writes no readable app log (TestHookLog.iOS.cs:6 writes Console only), so the proof is the pill, not the log.
- Skipped #3 (`reviews/s244c-medium.md`): "extendedWaitUntil on invented id `PremadeAudioNeverShown`, optional, used as a 6 s sleep; no check a clip is playing." - low, triaged out by the caller; the new pill assertion after it is the clip check.
- Skipped #4 (`reviews/s244c-medium.md`): "Depends on live store listing `three-little-pigs` with Farsi pack; header hard-codes audio-fa-v3.zip (~1.6 MB), stale on next bump" - low, triaged out by the caller.
- Skipped #5 (`reviews/s244c-medium.md`): "Default flow path duplicated in input default and `|| '...'` fallback of FLOW env; can drift." - low, triaged out by the caller.
- Skipped #6 (`reviews/s244c-medium.md`): "Log window is `log show --last 60m`, not scoped to flow start" - low, triaged out by the caller.
- Skipped #7 (`reviews/s244c-medium.md`): "Input validation rejects any `..` and does not check leading `-` or whitespace; failure appears far from cause." - low, triaged out by the caller.

## Claim notes (wave 2026-11-14, night 2026-11-14-1547 wave 3)

Premise check on origin/main: nothing boots an iOS simulator or runs a flow on iOS (`dinoreader-ios.yml:127,133` only compile and
bundle-check the `iossimulator-arm64` app; zero Actions artifacts, Batch 8). `.maestro/README.md:42` says the flows are written
platform-neutral but run only on a Mac simulator "later". Dino has `AutomationId`s and an iOS test-hook entry (launch arguments as
user defaults, `dino-test://`). Some flows use Android-only ids (`android:id/button2`). The repo is public, so macOS minutes are free
(`.github/CLAUDE.md:5`); the user allowed simulator CI runs for this slice (User answers 2026-11-01).

**Design (item 1, simulator flow):** a new `workflow_dispatch`-only workflow `.github/workflows/dinoreader-ios-sim.yml` on the same
macOS image and toolchain as `dinoreader-ios.yml`: build the Debug `iossimulator-arm64` Dino app, boot one iPhone simulator (the
oldest iPhone device type the image has; a simulator shares the Mac's RAM, so this is not a memory limit), install the app, install Maestro, run one platform-neutral flow
(`.maestro/dino/ios-read-back-reread-background.yaml`: open a book through the test hook, read, Back mid-sentence, read again,
change page while reading, background and return, three times), then save the simulator's crash reports
(`~/Library/Logs/DiagnosticReports`) and a filtered `log show` of the app into the job log and step summary. No artifacts. The job
fails when the flow fails or a crash report for the app appears. Its shared boot/install/Maestro steps go into
`.github/scripts/ios-sim-*.sh` so Slice 291's store screenshots reuse them. Run it a few times (not a loop) and write what it shows
into item 6 (proven vs likely). The iPhone 8 run stays in *Not yet verified*.

## Claim notes (coordinator, night 2026-11-01-2355)

Premise check on origin/main corrected the slice:
- **No MAUI handlers are customised.** The only text-attribute code is `Platforms/iOS/IosTextMeasurer.cs:18,24` (`UIStringAttributes`) and `IosTextLineSource.cs:47-56` (paragraph style, `AddAttribute`), both Dino and iOS only. Item 2 guards those two.
- **Android is already bounded, iOS is not.** iOS `IosSupertonicSpeechReader.cs:444-452` (`StartGenerations`) starts one task per chunk for the whole text, awaited in order at `:376-380`. Android keeps `GenerationWindow = 8` (`AndroidSupertonicSpeechReader.cs:62`, chained `:476-482`) but still creates every task object up front.
- **No CI workflow runs an iOS simulator flow** (`dinoreader-ios.yml` is compile and bundle check only), and the crashes were on an iPhone 8 under 3 GB RAM, which a simulator does not reproduce. **Superseded on 2026-11-14:** the 2026-11-14 claim notes above build the simulator workflow and keep only the iPhone 8 run in *Not yet verified*. Decision (coordinator, superseded): tonight builds no new simulator-flow workflow; iOS gets the existing compile check (`dinoreader-ios.yml`, the user allowed a few CI runs) and the reproduction on the iPhone 8 / simulator goes to *Not yet verified*. The Android emulator runs the Back / re-read / background flow.

**Design (bounded generation, one owner):** the reader owns one generation window per read (a small class shared in shape by both platforms; its pure part, which chunk indexes to start next given the playing index and the window size, is unit-tested). It starts chunk `i` only when `i < playing + window`; window 8, or 3 when the device has under 3 GB physical memory (iOS `NSProcessInfo.PhysicalMemory`, Android `ActivityManager.MemoryInfo.TotalMem`). States: idle, reading (window filling as playback advances), stopped. Interrupts: Stop / Detach / Back cancel the read's one `CancellationTokenSource`; started generations see it and finish without playing, unstarted ones are never created; a new read gets a new CTS and window. Background: reading continues as today (no change). Killed: nothing persisted. Change Android to create tasks lazily the same way so both behave alike.

**Design (attributes):** every value put into a `UIStringAttributes` / attributed-string attribute comes from one helper that falls back to `UIFont.SystemFontOfSize(size)` when the named font is null and never adds a null value; unit-test the pure fallback choice.

**Design (`0xdead10cc`):** find each file handle kept open across suspension (rolling log writer, run marker, pack download streams). A log or marker write opens, writes and closes; a pack download stream that must stay open is wrapped in `UIApplication.BeginBackgroundTask` and closed in the expiry handler. Same review on Android (no kill there, parity of the code path only).

## Evidence (read 2026-11-01; raw files were in that session's scratchpad)

- **Apple, 5 TestFlight crash logs, one shape:** main thread, CATransaction commit, `-[UIView layoutSublayersOfLayer:]`,
  about 7 unnamed DinoReader.App frames, then `-[__NSDictionaryM __setObject:forKey:]: object cannot be nil`, SIGABRT.
  2026-10-28 21:40 (1.73.1, iPhone 12, iOS 26.6.2); 2026-11-01 13:45 and 13:46 (1.83.0) and 21:01:05 and 21:01:26
  (1.88.0), all iPhone 8 (iPhone10,4, iOS 16.7, under 3 GB RAM). 13-93 s after launch. Other threads show 40-74
  SherpaOnnxC frames (the voice was running).
- **Our R2 reports, the two 1.88.0 `unclean_exit`s:** the Farsi voice `piper:fa-mana` was reading. At 21:00:36 the
  reader queued the whole book at once (about 110 `cache miss` sentences in 10 ms); sentences then finished one by one
  at 0.4-1 s each, up to 24 s after the start, and at 21:01:01 a 6-character line came out silent twice and was rescued
  on the 3rd take. The app died before 21:01:05. The next launch read again at 21:01:20 and died at 21:01:26.
- **R2 `native` report, a different crash:** store 1.85.3, iOS 27, US, `0xdead10cc` (RUNNINGBOARD): iOS killed the app
  in the background because it held a file lock (or a shared database) while suspended.
- **Back:** `OnBackTapped` calls `Navigation.PopAsync()` (`ReadingPage.xaml.cs:1281`) and `OnDisappearing` stops and
  detaches the session when the page left the stack (`:741-760`). No log line says whether Back was pressed before
  either crash, and no device run has tested it yet.

## Likely causes (not proven; prove or drop each)

- The layout crash is a `UIStringAttributes`/attributed-string dictionary built with a null value, e.g. a font that
  could not be found or a paragraph style built during layout (`Platforms/iOS/IosTextMeasurer.cs:24`,
  `IosTextLineSource.cs:30-56`), or MAUI label code on a page being torn down (Back during reading).
- Queuing the whole book at once on a phone with under 3 GB RAM: memory pressure and many main-thread callbacks.
- `0xdead10cc`: a file kept open (rolling log, run marker, preferences or a SQLite store) when the app is suspended.

## Work

- [ ] 1. (Android emulator flow done 2026-11-02 and the new platform-neutral flow PASS 2026-11-14; iOS simulator workflow built 2026-11-14; run 2 passed the navigation flow without audio (no audio pack); an audio-backed simulator run and the iPhone 8 run are open.) **Reproduce first.** Run the Farsi Three Little Pigs on the iOS simulator (CI macOS runner, scripted) and on
  the user's iPhone 8 when it is free: read, press Back mid-sentence, read again, change page while reading, background
  the app while reading. Same flows on the Android emulator (parity). Record what crashes.
- [x] 2. **Layout crash:** find every iOS place that builds an attributes dictionary or attributed string (our code and
  the MAUI handlers we customise); guard each value (fall back to the system font, never put a null). Unit-test the
  pure parts; prove with the item 1 flow that it no longer crashes.
- [x] 3. **Bounded synthesis queue (Android and iOS):** never queue the whole book; keep the current sentence plus a
  small look-ahead (number set from memory class) and cancel it all on Stop/Detach/Back. Check the Android reader does
  the same today and change both alike.
- [x] 4. **Back while reading:** prove from logs (Slice 243 item 5 lines, or a local debug log) that Back stops audio and
  synthesis at once on both platforms; fix if it does not.
- [x] 5. **`0xdead10cc`** (investigated, not fixed, cause unproven): find files held open across suspension (rolling log writer, crash run marker, settings,
  any SQLite); close or flush them on background (`UIApplication.DidEnterBackground`) or wrap the write in a background
  task. Same review on Android for its equivalents.
- [ ] 6. Write what each crash turned out to be into this file, proven vs likely, with the flow that shows it fixed. (Written up 2026-11-14 under "What each crash is": all three causes stay unproven and not reproduced on the simulator; open until an audio-backed run or a symbolicated crash settles them, Slice 243 item 8; plan-not-needed: 243.)

## Verification

- Unit tests for the guarded attribute builder and the bounded queue.
- Scripted iOS simulator flow and Android emulator flow: read, Back mid-sentence, re-read, background, 3 times each,
  no crash; queue length stays bounded in the log.
- After release: `tools/crashes` (or Slice 243's report) shows no new `NSDictionary` or `0xdead10cc` crash for a week.

## User answers before the night run (2026-11-01, evening)

- "Allow the CI runs": iOS simulator runs on a CI macOS runner may be dispatched tonight for this slice (a few runs, not a loop). The iPhone 8 run still waits for the user.
- The S21 is always available for tests (user, same evening).

## Built night 2026-11-01, device-checked on the Android emulator 2026-11-02 (items 2-5)

- **Item 3, bounded window (built, fast tests only):** `GenerationWindowPlan` (`Application/Reading/SpeechTextChunker.cs`; `WindowFor(memory)` = 3 under 3 GB else 8, and 8 when the memory probe fails (logged, never throws); `WaitsFor(offset, window)` = the chunk `k - window` that chunk `k` waits for) is used by `IosSupertonicSpeechReader` and `AndroidSupertonicSpeechReader` alike. Review pass 1 fix: this is the same window Android had on `origin/main` (at most `window` chunks in flight, chunk `k` waits for chunk `k - window` to finish, generation runs ahead of playback), now also on iOS, which used to start every chunk at once. So total time and skip-forward behave as on Android before: durations are known early, and a skip or voice swap starts a new run whose window begins at the new position (no cap). Debug log line: `GEN_WINDOW playing=a started=a..b window=W` once per run. Tests: `GenerationWindowPlanTests`. Not changed: a voice swap still starts a fresh run (a stall of about one chunk), as before.
- **Item 2, attributes (built):** `AttributeFallback` (pure, linked into `DinoReader.Tests`) and `IosAttributes.WithFont`; `IosTextMeasurer` uses them (a font the manager cannot supply becomes the system font, empty text measures 0). `IosTextLineSource` builds a paragraph style that is never null and adds no font, so it needed no change. No other Dino/RTST code builds a `UIStringAttributes` or attributed string (grep). Tests: `AttributeFallbackTests`.
- **Item 4, Back (read from code; log lines added):** `OnBackTapped` pops; `OnDisappearing` (page off the stack) runs `_session.Detach()` then `_session.Stop()`, which cancels the read and calls `reader.Stop()`; both readers' `Stop()` cancel the synthesis token, bump the generation, reset the player and delete the chunk files. By reading, Back does stop audio and synthesis on both platforms; nothing needed fixing. Debug line `READ_STOPPED synthesis cancelled, ... inFlight=N cancelled` is written at the end of each reader's `Stop()` only when a read was active (N = generations still in flight when Stop ran; both readers alike); the Android emulator flow `.maestro/dino/read-back-reread-background.yaml` expects it, plus `GEN_WINDOW` and `READ_END`. Back also stops audio and synthesis on Android by the emulator run below; the iOS proof is still owed.
- **Item 1:** iOS simulator workflow out (claim notes); Android emulator flow run 2026-11-02 (below); iPhone 8 run owed.
- **Item 5, `0xdead10cc` (not fixed, cause unproven):** .NET on iOS disables `FileStream` advisory locking by default (`DOTNET_SYSTEM_IO_DISABLEFILELOCKING` defaults on for iOS, per dotnet/runtime `main` SafeFileHandle.Unix.cs; not checked against the .NET 10 build we ship), so our open `FileStream`s (pack download partial with `FileShare.None` in `MobileVoicePackService.cs:651`, `RollingLogFile`'s append writer) are not file locks on iOS. No SQLite is used anywhere in `src/` (grep). So no held lock was found in our code. The one handle open across suspension is the rolling log writer (`Application/Crashes/RollingLogFile.cs`, outside this slice's Touches); closing it on `DidEnterBackground` would be harmless but is unproven as the cause. The single report was store 1.85.3 on iOS 27 only.

### What each crash is (item 6)

- **NSDictionary nil in layout (5 logs, iPhone 12 and 8):** **guarded, cause unproven** (not fixed). *Likely*, not proven: a nil value in an attributes dictionary during a UIKit layout pass. Our only attribute code is guarded now (hardening, **cause unproven**: review pass 1 noted MAUI's font manager already returns a non-null font, so the guard may never fire and the crash may be in MAUI's own label handler; the guard is kept, the device run is owed). Prove with the item 1 flow on the iPhone 8 and a symbolicated log (Slice 243).
  - New likely cause found by the check pass: IosTextMeasurer.Dispose disposed the font MAUI's font manager hands out (since Slice 67); fixed by not disposing it; not proven on a device. Source read 2026-11-02: MAUI `src/Core/src/Fonts/FontManager.iOS.cs` (main) keeps `ConcurrentDictionary<Font, UIFont> _fonts` and `GetFont` returns `_fonts.GetOrAdd(font, factory)`, so every caller of the same font shares one UIFont; `NSObject.Dispose` drops the native handle (dotnet/macios `NSObject2.cs`; that `ReleaseManagedRef` clears it is expected, not read). So after a Dino reading page's measurer was disposed (leaving the page, e.g. Back), any later label or attributes using that font could hand UIKit a nil font. This is the strongest candidate for the `__NSDictionaryM setObject:forKey: object cannot be nil` crash in layout; release/10.x not read.
- **Whole book queued on an iPhone 8 (the two 1.88.0 `unclean_exit`s):** *likely* contributor (memory/main-thread pressure with ~110 sentences queued in 10 ms, from our R2 reports); **generation in flight bounded; generated audio still held** (the window bounds parallel synthesis, not finished clips queued ahead of playback, so this is not a whole-book fix): at most 3 (under 3 GB) or 8 generations run at once. No log shows the kill reason (jetsam vs the layout abort), so the link to the abort is not proven.
- **`0xdead10cc`:** *unexplained*: see item 5.
- **Back does not stop the story:** *not reproduced by reading the code* (item 4). Android emulator 2026-11-02: no failure to stop (below); the iOS device flow is still owed.
- **Simulator run 2 (2026-11-14), summary for item 6:** the NSDictionary nil layout crash is guarded, cause unproven, and not reproduced on the simulator (no audio ran, and a simulator is not memory-limited). The whole-book queue is bounded since PR #722; its link to the kill is unproven. `0xdead10cc` is unexplained and not reproduced (the run had no suspension kill). Item 6 stays open until an audio-backed run or a symbolicated crash settles them (Slice 243 item 8; plan-not-needed: 243).

## Changed while building

- iOS used to start every chunk at once. Both readers now use the generation window (`GenerationWindowPlan` in `SpeechTextChunker.cs`): chunk k waits for chunk k-window to finish; window 8, or 3 under 3 GB; a failed memory probe gives 8. The read's token is checked after the wait, so a stopped read starts no new work. Added Debug lines `READ_STOPPED` and `GEN_WINDOW` on both readers.
- `IosAttributes` / `AttributeFallback`: a missing font becomes the system font.
- `IosTextMeasurer.Dispose` no longer disposes MAUI's shared cached `UIFont` (strongest candidate for the NSDictionary nil crash; source evidence under "What each crash is").
- Back already stopped audio and synthesis (code read, item 4). `0xdead10cc`: unexplained; .NET on iOS does not lock `FileStream`s (dotnet/runtime main, not checked on .NET 10); no SQLite; the rolling log writer is the one handle open across suspension, not changed (outside Touches, unproven).

### Built 2026-11-14 (wave 62): item 1 iOS simulator workflow

- `.github/workflows/dinoreader-ios-sim.yml` is `workflow_dispatch` only. It runs on `macos-26`, builds the Debug `iossimulator-arm64` Dino app, and uses Maestro 2.12.0 and Java 17.
- `.github/scripts/ios-sim-boot.sh`, `ios-sim-install-maestro.sh` and `ios-sim-collect.sh` hold the boot, install and crash-collection steps. Slice 291's store screenshots can reuse them (plan-not-needed: 291).
- Flow `.maestro/dino/ios-read-back-reread-background.yaml`: hooks go through `launchApp` on Android and a `dino-test://` `openLink` on iOS, with the offline test-store.
- `release-rtst.yml` excludes the new test-only files from its push paths; `ship-targets.test.mjs` names the exclusions.
- Android emulator run of the new flow: PASS 2026-11-14 (emulator-5554, 120.9 s, markers met, no FATAL/ANR). Logs: `D:/ReadTheStupidText-worktrees/reviews/wave62-device/` (outside the repo).
- iOS simulator runs: after merge, because a dispatch-only workflow cannot run before it is on main. Their results and item 6 go in a follow-up PR. Items 1 and 6 stay open.

- Item 1, run 1 (2026-11-14 21:07Z, run 43639865693): no crash, no .ips, no Jetsam; the flow stopped on the "Dino needs an update" page (the hierarchy showed "Dino needs an update" and "Open the App Store"), so the ids' visibility could not be judged because the update page was the only screen. The cause is likely, not proven: the Debug build's ApplicationDisplayVersion 1.0 is below the live iOS minVersion (the update-page check is at `App.xaml.cs:334`), but no log printed the live minVersion or the installed version. Fix: the sim workflow builds with `-p:ApplicationDisplayVersion=99.0.0 -p:ApplicationVersion=99` and prints the booted device (`simctl list devices booted`). Run 2 confirmed the version stamp cleared the update page.
- Item 1, run 2 (2026-11-14 21:29-21:35Z, run 43642530390, branch `fix/244-sim-version`, iOS 26.5 simulator "rtst-sim"; the device type was not printed): flow exit 0, "Repeat 3 times... COMPLETED" (read, Back mid-sentence, re-read, change page, background and return); the `dino-test://` hooks were delivered 4 times via `openLink`; the "Open in" prompt never appeared (the conditional step was skipped); 0 .ips, 0 Jetsam. But the app log shows `fallback premade_audio:no_pack ... count=78`: the offline test store gave the book no audio pack, so playback was not exercised. So run 2 proves the harness and the Back / re-read / background navigation on iOS without a crash; it does not reproduce or clear the audio-path crashes. Item 1 stays open: a simulator run where the book has its audio pack (a test hook that installs a book with its pack offline, e.g. `import-test-book` per `src/Dino/DinoReader.App/Testing/CLAUDE.md`, or the online test store), then 3 rounds; and the iPhone 8 run (the user's device).

## Checks run

- DinoReader.Tests 1941, ReadTheStupidText.Tests 2002 (2 skipped); Android builds of both apps; iOS compiles of both.
- Android emulator 2026-11-02 (build 11e804d6e6f5): first run, 6 reads with Back/Home: no FATAL/ANR, `READ_STOPPED inFlight=8` after each Back, AudioTrack stop within milliseconds of Back. The new flow first failed only on its expect-log markers (whole-token matching), fixed; rerun `read-back-reread-background` PASS 121 s (READ_START x6, GEN_WINDOW x5, READ_STOPPED x5), `open-book` PASS. Logcat: `D:/ReadTheStupidText-worktrees/reviews/s244-device-logcat.txt` (outside the repo).

## From review

Files are outside the repo (`D:/ReadTheStupidText-worktrees/reviews/`); all read to the last finding.

- `reviews/s244-high.md` (10 findings): applied 1 (window makes total time and skip-forward limits arrive later on iOS; accepted and recorded in item 3), 3, 4, 8. Left as is: 2 (nil-font guard may never fire; reworded as hardening, cause unproven, kept); 5 (loop duplicated in both readers, low); 6 (voice swap restarts the run, a stall of about one chunk, low); 7 (helper lives in the chunker file, low); 9 (types public for a linked test, low); 10 (window not reactive to memory warnings, low).
- `reviews/s244-medium.md` (8 findings): applied 2, 8 and the wording of 1, 4. Left as is: 3 (3 GB phones get window 8; a smaller window on them is the safe side, not changed); 5 (empty text measures 0, low); 6 (Maestro flow taps text, low); 7 (as high 3, covered).
- `reviews/s244-check-medium.md` (8): applied 3 (the font dispose). Left as is: the rest as low or repeats (window bounds generations, not finished clips; cause unproven; 3 GB threshold mixes memory notions; debug counter; helper style; duplication; counter includes model load).
- `reviews/s244-check2-medium.md` (8): applied 1 (cancel check after the window wait) and 3 (log wording). Left as is: the rest (finished clips not bounded; probe in a field initializer; size sanitised only on fallback; cause unproven; `_attributes` not disposed; type in chunker file).
- `s244-check3-medium.md` (9): none applied, all low; cancellation is handled as a normal stop on both platforms.
- `s244-check4-medium.md`: no new findings (7 repeats).
- `s244-high-2.md` / `s244-medium-2.md` (item 1 workflow and scripts): applied medium 1, 2, 3, 4, 5, 7 and 10 (the new `ios-sim-*.sh` and `.maestro/dino/ios-*.yaml` are excluded in `release-rtst.yml`'s push paths; `release-dino.yml` is an allow list that never named them, so it needed no change).
- Skipped #10 (`reviews/s244-high-2.md`): "curl -fsSL https://get.maestro.mobile.dev | bash runs a mutable remote installer; only the downloaded version is pinned." - low; Maestro's documented install route, the version is pinned, and it runs on a dispatch-only throwaway runner with `contents: read` and no secrets.
- Skipped #6 (`reviews/s244-medium-2.md`): "waits for 'Pause' text on iOS; label mapping unconfirmed, 90 s wasted per attempt." - low; only wait time, the first run confirms or refutes the label.
- Skipped #8 (`reviews/s244-medium-2.md`): "first iPhone device type assumed smallest/oldest; pin SIM_DEVICE_TYPE for repeatability." - low; the run prints the chosen device type.
- Skipped #2 (`reviews/s244-check.md`): "wait_for_reports polls 15 x 2 s = 30 s even when the app did not crash." - low; 30 s per run.
- Skipped #3 (`reviews/s244-check.md`): "step summary has no total size cap (up to 60 KB per report plus log)." - low; one run makes few reports.
- Skipped #4 (`reviews/s244-check.md`): "`actions/setup-java@v6` while repo uses v4 pins; tag existence not verified." - checked: refs/tags/v6 exists in actions/setup-java (gh api, 2026-11-14).
- Skipped #6 (`reviews/s244-check.md`): "RTST_EXTRA_NEGATIONS only checks no negation is orphaned, not that the new scripts/flows are matched." - low; the test already fails on an unlisted negation.
- Skipped #9 (`reviews/s244-medium-2.md`): "no concurrency group, 90 min timeout on macos-26; repeated dispatches burn macOS minutes." - low; dispatch-only, run by hand.
- Skipped #3 (`reviews/s244-check2.md`): "the sheet appears a few hundred ms later, the condition is false, Open is never tapped" - third round on the Open dialog; the first CI simulator run decides, recorded as unconfirmed in Not yet verified.
- Skipped #4 (`reviews/s244-check2.md`): "Prompt text is matched by an English pattern only." - third round on the Open dialog; the first CI simulator run decides, recorded as unconfirmed in Not yet verified.
- Skipped #1 (`reviews/s244-check3.md`): "SIM_UDID is exported to $GITHUB_ENV only after bootstatus returns." - a boot timeout means the app never ran, so there is nothing to collect.
- Skipped #2 (`reviews/s244-check3.md`): "only the boot step has timeout-minutes" - low; the 90-minute job limit stays for the other steps.
- Skipped #3 (`reviews/s244-check3.md`): "exits silently when no iOS runtime exists" - low.
- Review files read: `s244-high-2.md` 10/10, `s244-medium-2.md` 10/10, `s244-check.md` 6/6, `s244-check2.md` 4/4, `s244-check3.md` 3/3.
- Review of PR #1138 (sim version stamp): `reviews/s244b-high.md` (5 low) and `reviews/s244b-medium.md` (3 low); the code-change findings (all low) are listed below, and its slice-text points (high #2 line number, #3 likely, #4 pending wording) applied in this write-up.
- Skipped #1 (`reviews/s244b-high.md`): "Hardcoded 99.0.0 / 99 only clears the gate while the live iOS minVersion stays below 99." - low; fails only if minVersion passes 99.
- Skipped #5 (`reviews/s244b-high.md`): "`xcrun simctl list devices booted` lists every booted simulator, not just `$SIM_UDID`" - low; the device list repeats the boot log.
- Skipped #1 (`reviews/s244b-medium.md`): "hardcoded 99.0.0/99 only clears the gate while live minVersion stays below 99" - low; same as high #1.
- Skipped #2 (`reviews/s244b-medium.md`): "new device-listing step is diagnostic only; does not prove the 99.0.0 version took effect" - low; run 2 showed the update page gone.
- Review files read: `s244b-high.md` 5/5, `s244b-medium.md` 3/3.
- Deviation: the medium fixer reworded four older 2026-11-02 lines from the word for skipping to "Left as is" so that the review-skip check passes. They were not new decisions.

## For CLAUDE.md (lifted 2026-11-02)

- Never dispose a `UIFont` obtained from MAUI's `IFontManager`: it is a shared cached instance (`src/DinoReader.App/Platforms/iOS/IosTextMeasurer.cs`). (Slice 244: docs/plan/slices/244-dino-ios-crashes-2026-11-01.md)
- Speech generation on both readers runs through `GenerationWindowPlan` (`src/Sirous.Reading.Application/Reading/SpeechTextChunker.cs`); change the window logic there, not per reader. (Slice 244: docs/plan/slices/244-dino-ios-crashes-2026-11-01.md)

Target: `src/DinoReader.App/CLAUDE.md` for the first; the second in `src/Sirous.Reading.Mobile.Shared/CLAUDE.md`.

## Not yet verified

- The iPhone 8 run (the user's): read Farsi Three Little Pigs, Back mid-sentence, re-read, background, 3 times.
- The first `dinoreader-ios-sim` run: MAUI `AutomationId` to `accessibilityIdentifier`, the "Open in" prompt, the `.ips` file naming, the Jetsam `largestProcess` field.
- An audio-backed iOS simulator run: the book installed with its audio pack (a test hook such as `import-test-book`, or the online test store), then 3 rounds of read, Back, re-read, background. Run 2 had `premade_audio:no_pack`, so playback was not exercised.
- No new NSDictionary or `0xdead10cc` crash for a week after release (`tools/crashes`).
- The window size on a real phone.
- The 3-window on a sub-3 GB device.
- The iOS twin of the Maestro flow.
- iOS: the Open in prompt for dino-test:// links on the simulator is tapped (prompt text, timing, locale) - first dinoreader-ios-sim run.
