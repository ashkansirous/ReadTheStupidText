### Slice 383 — Dino: a book keeps reading when the phone is locked
**Product:** Dino

*Built 2026-11-15 (wave 7), not merged. Verified on the emulator and on the S21 (pack path, about 6 s locked); a long locked read and iOS are not yet verified.*

**Touches:** `src/Dino/DinoReader.App/ReadingPage.xaml.cs`, `src/Dino/DinoReader.App/ReadingSession.cs`, `src/Dino/DinoReader.Core/SilentBeat.cs`, `src/Dino/DinoReader.Core/BeatCoverFacts.cs`, `src/Dino/DinoReader.App/WindowForeground.cs`, `src/Dino/DinoReader.App/Platforms/Android/**` (only if a media playback service or manifest entry is needed), `src/Dino/DinoReader.App/Platforms/iOS/Info.plist` (only if a background mode is missing), `tests/Dino/DinoReader.Tests/SilentBeatTests.cs`, `tests/Dino/DinoReader.Tests/BeatCoverFactsTests.cs`, `.maestro/dino/silent-piece-beat-lock.yaml`, `.maestro/dino/read-while-locked.yaml` (new), `src/Dino/DinoReader.App/CLAUDE.md`, `src/Dino/DinoReader.App/Platforms/iOS/**` (only the audio-session code, if the iOS side needs it), `docs/plan/slices/383-dino-reading-keeps-going-when-locked.md`

**Why.** The user reported this on 2026-11-14, Dino 1.129.0, while reading a book in Farsi to their daughter: "it simply stopped playing. then I unlocked the phone and it started to read again. there might be a problem with phone staying in locked screen. we should fix it cause I dont want my daughter to look at the screen while I play it for her."

**Earlier rules on this behaviour (quoted).**
- Slice 248 (`docs/plan/slices/248-dino-stop-read-on-leave-ios.md`, Work a): "A push over the page (the grown-ups' gate), a locked screen or another app still never stops the read."
- Slice 322 (`docs/plan/slices/322-dino-beat-pauses-on-screen-lock.md`), from Slice 278: "the silent beat pauses when the page is covered (lock, background, gate) and goes on when shown". "A piece with no sound has nothing to hear, so it must never run on unseen."
- Slice 126 (`docs/plan/slices/126-dino-keep-screen-awake.md`): keep the screen on only while a book plays aloud. That stays: it only stops the screen sleeping by itself, it does not stop a person locking it.

**Decision (user, 2026-11-14).** A book that is playing keeps reading to the end with the phone locked or the screen off, on Android and iOS, so a parent can read with the screen dark. This replaces Slice 322's rule for a locked screen only: while the phone is locked, a page with no sound (a silent beat) passes after its normal length and the read goes on to the next page. A gate or sheet covering the page with the screen on still pauses the beat, as Slice 322 decided.

**Decisions (coordinator choice, wave 7, 2026-11-15).**
- With the screen on, Home or another app no longer pauses a silent beat either. A silent beat lasts about 1.2 s; telling a lock from another app needs per-platform signals that iOS lacks without a passcode, and one shared rule keeps Android and iOS the same. The read itself already goes on in another app (plan-not-needed: 248).
- The beat's cover is now the page alone (`BeatCoverFacts.Hidden = !PageShown`); the window-foreground inputs and `WindowForeground` were removed, `Window.Stopped` only releases the screen and writes a log line.

**Device results (wave 7, 2026-11-15).**
- Emulator, build 642a7daede2a: `read-while-locked` PASS (`[Reading] book finished: read all 16 of 16 pages`); `silent-piece-beat-lock` PASS (`[AudioPack] silent piece shown while the window is stopped (the screen is locked or off)`). Logs `reviews/wave7-device/383-emu-*.log`.
- S21, same build: Good Enough in Farsi read all 10 of 10 pages with the screen locked (`mWakefulness` Dozing); premade clips kept playing after the lock (`premade clip plays ... fixture-16.m4a`). Log `reviews/wave7-device/383-s21-accept.log`. It used the Debug fixture audio pack (the store has no Farsi audio for Good Enough), so only about 6 s were locked.
- Item 3: the audio does not stop on lock (S21, pack path), so no foreground service is needed.

**Not yet verified.**
- Android and iOS: a long locked read (1 minute or more, e.g. three-little-pigs with its real Farsi pack, already on the S21 dev install) to show a silent beat's 1.2 s `Task.Delay` is not stalled by Doze or CPU suspend (pr-1169 high #1). Wake the phone (`KEYCODE_WAKEUP`) before `open-book`. The emulator never suspends the CPU and the S21 run was only about 6 s locked.
- iOS: TestFlight check that a book plays to the end with the iPhone locked, that the process runs through a silent beat (no sound) long enough for the `Task.Delay`, and whether `OnDisappearing` fires on lock or background (if so `_beatCover.Disappeared()` pauses the beat on lock as before; Slice 322 saw only that Android lock does not raise it).
- Another app in front with the screen on: not tested on any device (item 5).

**From review:**
- Skipped #1 (`reviews/pr-1169-high.md`): "The silent beat's Task.Delay runs while the screen is off, but nothing keeps the CPU or process awake" - a device question, moved to Not yet verified above (the S21 run was only about 6 s locked).
- Skipped #2 (`reviews/pr-1169-high.md`): "Cause chosen (silent-beat cover) may not be what the user hit" - a device question; settled by the S21 pack-path run (item 3: audio does not stop on lock).
- Skipped #6 (`reviews/pr-1169-high.md`): "raises Disappearing on lock/background" - a device question, moved to Not yet verified above (item 4 stays open).
- Check pass (`reviews/pr-1169-check.md`), none of its findings applied:
  - Skipped #1 (`reviews/pr-1169-check.md`): "Debug-only _pagesReached HashSet mutated across threads unguarded" - Debug-only test bookkeeping; a third change to that record would be a strike.
  - Skipped #2 (`reviews/pr-1169-check.md`): "_pagesReached is filled by navigation (public MoveTo(int) skip), not only played pages" - Debug-only test bookkeeping, a third change to that record would be a strike; the S21 check reads page progress directly.
  - Skipped #3 (`reviews/pr-1169-check.md`): "Slice 322 unverified lines describe WindowForeground behaviour this slice deletes" - `docs/plan/unverified.md` is outside this slice's Touches; the index pass drops the old WindowForeground line there.
  - Skipped #4 (`reviews/pr-1169-check.md`): "OnWindowStoppedReleaseScreen releases keep-awake on Stopped; Suspend(false) only on Resumed, not Activated" - the keep-awake on Activated is unchanged against origin/main (the diff does not touch the ScreenAwake lines).
  - Skipped #5 (`reviews/pr-1169-check.md`): "Now a one-bool wrapper (Hidden = !PageShown)" - low.
- Applied: high #3, #4 (both flows now fail against the old behaviour: they expect the silent piece to pass while locked), #5 and #8 (dead window-foreground inputs and `WindowForeground` removed), #9 (finish line names pages read of the page count), #10 (ticks corrected); medium #1 (the pages record is cleared on `StartAtAsync`) and #3 (test bookkeeping under `#if DEBUG`; #2's bool became `volatile` with it).
- Skipped #7 (`reviews/pr-1169-high.md`): "is written on every Window.Stopped" - low; the line now says only that the window stopped.
- Test count (`reviews/pr-1169-medium.md`): `BeatCoverFactsTests` fell from 2809 to 34 cases because its exhaustive window-and-page input theory lost its window inputs; other test classes are unchanged (per-class lists in `s383/tmp/perclass-*.txt`).
- Skipped #2 (`reviews/pr-1169-medium.md`): "`_windowStopped` plain bool written on UI thread, read on beat thread, no volatile" - not skipped in effect: made volatile while moving it under `#if DEBUG` (#3).
- Skipped #4 (`reviews/pr-1169-medium.md`): "nothing shows the read survives Doze/iOS suspension with the screen off" - a device question, already in Not yet verified above.
- Skipped #5 (`reviews/pr-1169-medium.md`): "the flow cannot assert an absent line" - low, the flow's reading-to-the-end check is what counts.
- Skipped #6 (`reviews/pr-1169-medium.md`): "class is now a one-bool wrapper around PageShown" - low, kept as the tested place for the page-shown rule.
- Skipped #7 (`reviews/pr-1169-medium.md`): "log texts changed without a repo-wide check" - low, not applied this pass.

**Evidence (2026-11-14).**
- App logs: the only Farsi read tonight was Good Enough (book `61bb25b8…`, about 20:00): premade Farsi clips, 0 pieces from the device voice, no fallback rows. The story language was then switched to English and the book read again. Logs are sampled and show no silence, so they cannot place the stop.
- Likely cause, from the code (not yet seen on a device): Slice 322 pauses the silent beat when the window stops (`Window.Stopped`, which a screen lock fires) and starts it again on `Window.Resumed`. That is exactly "stopped when locked, read again when unlocked" if the book reached a page with no sound. A second possible cause is the audio itself stopping on lock. Item 1 settles which.

**Work.**
- [x] 1. **Result (S21, two real locks, log `reviews/wave7-device/383-repro.log`, 2026-11-15):** the stop is Slice 322's silent-beat cover. At each lock the app wrote `[AudioPack] silent piece paused, the window is not in front` and at each unlock `resumed`. Cause: `ReadingSession.cs:385`/`:402` (`SetBeatHidden`), fed by `Window.Stopped`/`Resumed` in `ReadingPage` through `BeatCoverFacts.Hidden`. The dev install had no audio pack, so every piece was silent and "the audio itself stops on lock" is not ruled out; item 3 stays open for a device check with a pack. Original ask: Reproduce on the S21 over adb with the released build: play Good Enough in Farsi and in English, lock with a real key event (keyevent 223/224, as Slice 322's S21 run did), and keep it locked for 3 minutes. Read logcat (`silent piece paused` lines, read progress) and write the cause here with file:line. Also check a book with no silent pieces, to rule out the audio stopping on its own.
- [x] 2. (Built: `BeatCoverFacts.Hidden` is now `!PageShown`, so a stopped window never covers the page; no platform API is needed because the rule is "a playing read ignores the window", and a gate or sheet still sets `PageShown` false. Android and iOS share it. Log line on each head: `window stopped (lock, screen off, Home or another app), the read and a silent beat go on`.) Smallest end-to-end first, Android: a silent beat runs on while the window is stopped because of a lock or screen-off, if the read was playing. `BeatCoverFacts` keeps "page covered by a gate or sheet" as a pause cause and drops "window not in front" as one while a read is playing. Change only what item 1 shows is needed.
- [x] 3. (Result: S21 with the pack path, premade clips kept playing after the lock and Good Enough read all 10 of 10 pages with the screen locked, so the audio does not stop on lock and no foreground service was added; log `reviews/wave7-device/383-s21-accept.log`.) If item 1 shows the audio itself stops on lock (Android), keep it playing: check with `library-docs` whether Android needs a media playback foreground service and audio focus for this, and add the smallest one. Slice 183's `dataSync` service is for downloads and is not reused for playback.
- [ ] 4. (Code only: `Platforms/iOS/Info.plist` already has `UIBackgroundModes` `audio`, and `IosAudioSession` sets category Playback; the same shared code change as item 2 applies. NOT checked on an iPhone: whether iOS keeps the process running through a silent beat, which plays no sound, long enough for the 1.2 s `Task.Delay`, and whether MAUI iOS raises `OnDisappearing` on lock (that would still pause the beat). Open until the TestFlight check.) iOS task: the same on iPhone. Background audio must be enabled (`UIBackgroundModes` `audio`, audio session category Playback). Check what is already set and check the APIs with `library-docs`. The silent beat's timer must keep running in the background on iOS too.
- [ ] 5. (Built: leaving, pausing and the book's end stop the beat as before; Home or another app no longer pauses a silent beat either (see Decisions). Not tested: another app in front with the screen on, on any device; open until a device run covers it.) Leaving the book, pausing, or the end of the book still stops everything (Slice 248). Going to another app with the screen on: keep Slice 248's rule ("another app still never stops the read").
- [x] 6. (Built: `BeatCoverFactsTests` rewritten, `SilentBeatTests` unchanged because `SilentBeat` itself did not change; flows `silent-piece-beat-lock` and new `read-while-locked`, plus a Debug `[Reading] book finished` line.) Update the unit tests (`SilentBeatTests`, `BeatCoverFactsTests`) for the new rule, and change `.maestro/dino/silent-piece-beat-lock.yaml` to expect the beat to go on while locked. Add a flow `read-while-locked` (`# needs: screen-cycle`) that plays a short book, locks, and checks with log lines that every page is read and the book reaches its end while asleep.

**Verification.**
- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Emulator: `read-while-locked` and the updated `silent-piece-beat-lock` pass.
- S21, real lock: Good Enough in Farsi plays to the end with the screen locked (logcat shows every page read while `mWakefulness` is Asleep or Dozing).
- iOS: compile on Windows; TestFlight check that a book plays to the end with the iPhone locked.

#### For CLAUDE.md
Goes in `src/Dino/DinoReader.App/CLAUDE.md`, replacing the older window rule there:
- A playing read and its silent beats go on while the window is stopped (lock, screen off, Home, another app); only a gate or sheet over the page pauses a silent beat (`BeatCoverFacts.Hidden = !PageShown`). (Slice 383: docs/plan/slices/383-dino-reading-keeps-going-when-locked.md)

#### Not yet verified
- A long locked read (1 minute or more, three-little-pigs with its real Farsi pack on the S21 dev install; `KEYCODE_WAKEUP` before `open-book`): a silent beat's delay is not stalled by Doze.
- iOS TestFlight: a book plays to the end with the iPhone locked, the silent beat's delay runs, and `OnDisappearing` does not pause it on lock.
- Another app in front with the screen on: the read goes on (no device run covers it).
