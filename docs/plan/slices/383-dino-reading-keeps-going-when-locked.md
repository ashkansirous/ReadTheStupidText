### Slice 383 — Dino: a book keeps reading when the phone is locked
**Product:** Dino

*In progress (wave 2026-11-15), draft PR.*

**Touches:** `src/Dino/DinoReader.App/ReadingPage.xaml.cs`, `src/Dino/DinoReader.App/ReadingSession.cs`, `src/Dino/DinoReader.Core/SilentBeat.cs`, `src/Dino/DinoReader.Core/BeatCoverFacts.cs`, `src/Dino/DinoReader.App/WindowForeground.cs`, `src/Dino/DinoReader.App/Platforms/Android/**` (only if a media playback service or manifest entry is needed), `src/Dino/DinoReader.App/Platforms/iOS/Info.plist` (only if a background mode is missing), `tests/Dino/DinoReader.Tests/SilentBeatTests.cs`, `tests/Dino/DinoReader.Tests/BeatCoverFactsTests.cs`, `.maestro/dino/silent-piece-beat-lock.yaml`, `.maestro/dino/read-while-locked.yaml` (new), `src/Dino/DinoReader.App/CLAUDE.md`, `src/Dino/DinoReader.App/Platforms/iOS/**` (only the audio-session code, if the iOS side needs it), `docs/plan/slices/383-dino-reading-keeps-going-when-locked.md`

**Why.** The user reported this on 2026-11-14, Dino 1.129.0, while reading a book in Farsi to their daughter: "it simply stopped playing. then I unlocked the phone and it started to read again. there might be a problem with phone staying in locked screen. we should fix it cause I dont want my daughter to look at the screen while I play it for her."

**Earlier rules on this behaviour (quoted).**
- Slice 248 (`docs/plan/slices/248-dino-stop-read-on-leave-ios.md`, Work a): "A push over the page (the grown-ups' gate), a locked screen or another app still never stops the read."
- Slice 322 (`docs/plan/slices/322-dino-beat-pauses-on-screen-lock.md`), from Slice 278: "the silent beat pauses when the page is covered (lock, background, gate) and goes on when shown". "A piece with no sound has nothing to hear, so it must never run on unseen."
- Slice 126 (`docs/plan/slices/126-dino-keep-screen-awake.md`): keep the screen on only while a book plays aloud. That stays: it only stops the screen sleeping by itself, it does not stop a person locking it.

**Decision (user, 2026-11-14).** A book that is playing keeps reading to the end with the phone locked or the screen off, on Android and iOS, so a parent can read with the screen dark. This replaces Slice 322's rule for a locked screen only: while the phone is locked, a page with no sound (a silent beat) passes after its normal length and the read goes on to the next page. A gate or sheet covering the page with the screen on still pauses the beat, as Slice 322 decided.

**Evidence (2026-11-14).**
- App logs: the only Farsi read tonight was Good Enough (book `61bb25b8…`, about 20:00): premade Farsi clips, 0 pieces from the device voice, no fallback rows. The story language was then switched to English and the book read again. Logs are sampled and show no silence, so they cannot place the stop.
- Likely cause, from the code (not yet seen on a device): Slice 322 pauses the silent beat when the window stops (`Window.Stopped`, which a screen lock fires) and starts it again on `Window.Resumed`. That is exactly "stopped when locked, read again when unlocked" if the book reached a page with no sound. A second possible cause is the audio itself stopping on lock. Item 1 settles which.

**Work.**
- [ ] 1. Reproduce on the S21 over adb with the released build: play Good Enough in Farsi and in English, lock with a real key event (keyevent 223/224, as Slice 322's S21 run did), and keep it locked for 3 minutes. Read logcat (`silent piece paused` lines, read progress) and write the cause here with file:line. Also check a book with no silent pieces, to rule out the audio stopping on its own.
- [ ] 2. Smallest end-to-end first, Android: a silent beat runs on while the window is stopped because of a lock or screen-off, if the read was playing. `BeatCoverFacts` keeps "page covered by a gate or sheet" as a pause cause and drops "window not in front" as one while a read is playing. Change only what item 1 shows is needed.
- [ ] 3. If item 1 shows the audio itself stops on lock (Android), keep it playing: check with `library-docs` whether Android needs a media playback foreground service and audio focus for this, and add the smallest one. Slice 183's `dataSync` service is for downloads and is not reused for playback.
- [ ] 4. iOS task: the same on iPhone. Background audio must be enabled (`UIBackgroundModes` `audio`, audio session category Playback). Check what is already set and check the APIs with `library-docs`. The silent beat's timer must keep running in the background on iOS too.
- [ ] 5. Leaving the book, pausing, or the end of the book still stops everything (Slice 248). Going to another app with the screen on: keep Slice 248's rule ("another app still never stops the read").
- [ ] 6. Update the unit tests (`SilentBeatTests`, `BeatCoverFactsTests`) for the new rule, and change `.maestro/dino/silent-piece-beat-lock.yaml` to expect the beat to go on while locked. Add a flow `read-while-locked` (`# needs: screen-cycle`) that plays a short book, locks, and checks with log lines that every page is read and the book reaches its end while asleep.

**Verification.**
- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Emulator: `read-while-locked` and the updated `silent-piece-beat-lock` pass.
- S21, real lock: Good Enough in Farsi plays to the end with the screen locked (logcat shows every page read while `mWakefulness` is Asleep or Dozing).
- iOS: compile on Windows; TestFlight check that a book plays to the end with the iPhone locked.
