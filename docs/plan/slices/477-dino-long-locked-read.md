### Slice 477 — Dino: a book keeps reading through a long lock, on battery, on every phone
**Product:** Dino

**Touches:** `src/Dino/DinoReader.App/Platforms/Android/` (new `ReadAloudService.cs`, `AndroidManifest.xml`), `src/Dino/DinoReader.App/ReadingPage.xaml.cs`, `src/Dino/DinoReader.App/ReadingSession.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/Platforms/iOS/IosAudioSession.cs`, `src/Dino/DinoReader.App/Platforms/iOS/` (Now Playing), `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `worker/events.ts`, `.maestro/dino/` (new `read-long-lock.yaml`), `tests/Dino/DinoReader.Tests/`

**Why.** The owner (2026-11-28): "the app should continue to play a book while the phone is locked! the case is when I want to put it for my daughter while she is not looking at it! so I should be able to lock the phone so she does not get curious and try to play with the phone." Slice 383 (`docs/plan/slices/383-dino-reading-keeps-going-when-locked.md`, built 2026-11-15) removed the pause that stopped the read on lock, but it was only checked for a few seconds locked. Its own *Not yet verified* lists a long lock, iOS, and Doze.

**What was found on 2026-11-28 (three sources).**
- **Device:** S21, Play build 1.136.5, *The Three Little Pigs*. Locked for 110 s, audio played and pages advanced; with HOME for 64 s, the same. The phone was on USB power, so it did not doze. There was no media session (`dumpsys media_session`: 0 sessions) and no foreground notification.
- **Code:**
  - Android has no media foreground service, no MediaSession and no wake lock in Dino or Mobile.Shared. The manifest has only `FOREGROUND_SERVICE_DATA_SYNC`, for pack downloads.
  - Page turns ride on audio-end callbacks marshalled to the main thread and on a `Task.Delay` silent beat (`ReadingSession.cs:622,1018,1263`). A frozen or dozing process stalls them.
  - iOS sets `AVAudioSession` category Playback and has `UIBackgroundModes` audio. No iPhone check has been run.
- **Logs (dino_usage, 14 days):** no event shows a lock, a page advance, or the OS stopping a read. The few sessions backgrounded mid-read hint that some stop (3 Android `read_stopped` right after backgrounding), but there are too few to prove it.
- **Likely cause** (not yet shown by a log or a rerun): on battery, with a long lock, Android dozes or freezes the app because nothing marks it as playing media. On iOS, the gap between clips (a silent beat) may let the process suspend.

**Decisions.**
- Owner (2026-11-14, Slice 383; restated 2026-11-28): a playing book reads to the end with the phone locked or the screen off, on Android and iOS.
- Decided by this plan: Android runs read-aloud inside a `mediaPlayback` foreground service with a MediaSession while a book is playing, and stops it when the read ends, pauses or leaves the book. The service brings the system's notification and lock-screen controls (title, Pause/Play), with text from Resources/Strings. iOS gets the matching Now Playing info and remote Pause/Play, so both phones behave the same.

**Work.**
- [ ] 1. Logs first. Add `read_stopped` detail `window_stopped` (already accepted by the Worker, never emitted) when the OS stops or interrupts a read. Add a `pages_while_locked` count on `read_stopped`/`book_listened`. Production builds only (`production-only-analytics`).
- [ ] 2. Android: `ReadAloudService` (foreground, `foregroundServiceType="mediaPlayback"`, `FOREGROUND_SERVICE_MEDIA_PLAYBACK`, the notification permission flow on Android 13+) with a MediaSession. Pause and Play on the lock screen drive `ReadingSession`. It starts on read start and stops on pause, end or leave. Check the API through context7.
- [ ] 3. iOS: `MPNowPlayingInfoCenter` (book title, cover) and `MPRemoteCommandCenter` Play/Pause. Keep the audio session active through silent beats, so the process is not suspended between clips. Check whether `OnDisappearing` fires on lock (Slice 383's open question). If it does, make sure it does not pause the read.
- [ ] 4. Windows head: confirm that locking the PC does not stop a read (no change expected; record the result).
- [ ] 5. Device flow `read-long-lock.yaml`: start a book, lock, wait 10 minutes, then wake and check that `[Reading] book finished` or the expected page was reached. Run it on the S21 over wireless adb with USB unplugged, so it is on battery and Doze can happen (`dumpsys deviceidle force-idle` as a second run), and on the emulator.
- [ ] 6. Tests: the service start and stop rules from the read state, in `DinoReader.Tests`.

**Not yet verified (owner).**
- iPhone: a book reads to the end with the phone locked for 10 minutes (TestFlight build). Only the owner's iPhone can show it.

**Verification.** The S21 on battery reads a whole book locked for 10 minutes. The lock screen shows the book with Pause. The new log fields appear for real users after release.
