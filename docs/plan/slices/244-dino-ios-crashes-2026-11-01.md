# Slice 244 — Dino: find and fix the crashes of 2026-10-31 and 2026-11-01

*Items 2-5 built 2026-11-02, merged as PR #722 (6d3a0e5d). Item 1's iOS simulator / iPhone 8 reproduction and item 6's proof stay open, so the slice stays open after merge.* *Planned 2026-11-01.* From the user, after testing Dino Reader on an iPhone: "there are a lot of crashes ... as far as
I can tell, when the story is playing and I click on back, it does not pause/stop the story", then "another slice to
figuring out what can have cause the crashes today and yesterday and fixing them".

**Depends on:** none. Slice 243 adds the dSYM and log lines that would prove a cause; this slice does not wait for it
and works from the evidence below plus reproduction.
**Touches:** `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/Reading*.cs`, `src/DinoReader.App/Story*.cs`,
`src/DinoReader.App/Platforms/iOS/**`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader*.cs`,
`src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosTtsEngine.cs`,
`src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader*.cs`,
`src/ReadTheStupidText.Mobile.Shared/Crashes/**`, `src/ReadTheStupidText.Application/**/SpeechTextChunker.cs`,
`tests/DinoReader.Tests/**`, `tests/ReadTheStupidText.Tests/**`, `src/DinoReader.App/CLAUDE.md`

**Touches, widened at claim (premise check, 2026-11-01 night):** also `src/DinoReader.App/Platforms/iOS/IosTextMeasurer.cs` and `IosTextLineSource.cs` (named; covered by `Platforms/iOS/**`), `src/ReadTheStupidText.Mobile.Shared/**/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosBackgroundDownloader.cs`, `.maestro/dino/**` (an Android emulator flow), `docs/plan/slices/244-dino-ios-crashes-2026-11-01.md`.

## Claim notes (coordinator, night 2026-11-01-2355)

Premise check on origin/main corrected the slice:
- **No MAUI handlers are customised.** The only text-attribute code is `Platforms/iOS/IosTextMeasurer.cs:18,24` (`UIStringAttributes`) and `IosTextLineSource.cs:47-56` (paragraph style, `AddAttribute`), both Dino and iOS only. Item 2 guards those two.
- **Android is already bounded, iOS is not.** iOS `IosSupertonicSpeechReader.cs:444-452` (`StartGenerations`) starts one task per chunk for the whole text, awaited in order at `:376-380`. Android keeps `GenerationWindow = 8` (`AndroidSupertonicSpeechReader.cs:62`, chained `:476-482`) but still creates every task object up front.
- **No CI workflow runs an iOS simulator flow** (`dinoreader-ios.yml` is compile and bundle check only), and the crashes were on an iPhone 8 under 3 GB RAM, which a simulator does not reproduce. Decision (coordinator): tonight builds no new simulator-flow workflow; iOS gets the existing compile check (`dinoreader-ios.yml`, the user allowed a few CI runs) and the reproduction on the iPhone 8 / simulator goes to *Not yet verified*. The Android emulator runs the Back / re-read / background flow.

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

- [ ] 1. (Android emulator flow done 2026-11-02; iOS simulator and iPhone 8 open.) **Reproduce first.** Run the Farsi Three Little Pigs on the iOS simulator (CI macOS runner, scripted) and on
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
- [ ] 6. Write what each crash turned out to be into this file, proven vs likely, with the flow that shows it fixed.

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

## Changed while building

- iOS used to start every chunk at once. Both readers now use the generation window (`GenerationWindowPlan` in `SpeechTextChunker.cs`): chunk k waits for chunk k-window to finish; window 8, or 3 under 3 GB; a failed memory probe gives 8. The read's token is checked after the wait, so a stopped read starts no new work. Added Debug lines `READ_STOPPED` and `GEN_WINDOW` on both readers.
- `IosAttributes` / `AttributeFallback`: a missing font becomes the system font.
- `IosTextMeasurer.Dispose` no longer disposes MAUI's shared cached `UIFont` (strongest candidate for the NSDictionary nil crash; source evidence under "What each crash is").
- Back already stopped audio and synthesis (code read, item 4). `0xdead10cc`: unexplained; .NET on iOS does not lock `FileStream`s (dotnet/runtime main, not checked on .NET 10); no SQLite; the rolling log writer is the one handle open across suspension, not changed (outside Touches, unproven).

## Checks run

- DinoReader.Tests 1941, ReadTheStupidText.Tests 2002 (2 skipped); Android builds of both apps; iOS compiles of both.
- Android emulator 2026-11-02 (build 11e804d6e6f5): first run, 6 reads with Back/Home: no FATAL/ANR, `READ_STOPPED inFlight=8` after each Back, AudioTrack stop within milliseconds of Back. The new flow first failed only on its expect-log markers (whole-token matching), fixed; rerun `read-back-reread-background` PASS 121 s (READ_START x6, GEN_WINDOW x5, READ_STOPPED x5), `open-book` PASS. Logcat: `D:/ReadTheStupidText-worktrees/reviews/s244-device-logcat.txt` (outside the repo).

## From review

Files are outside the repo (`D:/ReadTheStupidText-worktrees/reviews/`); all read to the last finding.

- `s244-high.md` (10 findings): applied 1 (window makes total time and skip-forward limits arrive later on iOS; accepted and recorded in item 3), 3, 4, 8. Skipped: 2 (nil-font guard may never fire; reworded as hardening, cause unproven, kept); 5 (loop duplicated in both readers, low); 6 (voice swap restarts the run, a stall of about one chunk, low); 7 (helper lives in the chunker file, low); 9 (types public for a linked test, low); 10 (window not reactive to memory warnings, low).
- `s244-medium.md` (8 findings): applied 2, 8 and the wording of 1, 4. Skipped: 3 (3 GB phones get window 8; a smaller window on them is the safe side, not changed); 5 (empty text measures 0, low); 6 (Maestro flow taps text, low); 7 (as high 3, covered).
- `s244-check-medium.md` (8): applied 3 (the font dispose). Skipped the rest as low or repeats (window bounds generations, not finished clips; cause unproven; 3 GB threshold mixes memory notions; debug counter; helper style; duplication; counter includes model load).
- `s244-check2-medium.md` (8): applied 1 (cancel check after the window wait) and 3 (log wording). Skipped the rest (finished clips not bounded; probe in a field initializer; size sanitised only on fallback; cause unproven; `_attributes` not disposed; type in chunker file).
- `s244-check3-medium.md` (9): none applied, all low; cancellation is handled as a normal stop on both platforms.
- `s244-check4-medium.md`: no new findings (7 repeats).

## For CLAUDE.md (lifted 2026-11-02)

- Never dispose a `UIFont` obtained from MAUI's `IFontManager`: it is a shared cached instance (`src/DinoReader.App/Platforms/iOS/IosTextMeasurer.cs`). (Slice 244: docs/plan/slices/244-dino-ios-crashes-2026-11-01.md)
- Speech generation on both readers runs through `GenerationWindowPlan` (`src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs`); change the window logic there, not per reader. (Slice 244: docs/plan/slices/244-dino-ios-crashes-2026-11-01.md)

Target: `src/DinoReader.App/CLAUDE.md` for the first; the second in `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`.

## Not yet verified

- The iPhone 8 run (the user's): read Farsi Three Little Pigs, Back mid-sentence, re-read, background, 3 times.
- iOS simulator flow (no CI workflow runs one).
- No new NSDictionary or `0xdead10cc` crash for a week after release (`tools/crashes`).
- The window size on a real phone.
- The 3-window on a sub-3 GB device.
- The iOS twin of the Maestro flow.
