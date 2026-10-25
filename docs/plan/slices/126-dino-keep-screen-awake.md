### Slice 126 — Dino: keep the screen awake while a book is playing

*In progress (wave 2026-10-25), draft PR.*

**Touches:** `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ScreenAwake.cs` (new), `src/DinoReader.App/CLAUDE.md`

**Why.** Reported by the user on 2026-10-25 for Android, who then asked that it be fixed on iOS too, after checking the 16-page pigs on their Android phone: while a book plays, the phone's screen goes to sleep. The child can no longer see the pictures or the highlighted words, and on many phones the reading stops too. Nothing in the app keeps the screen awake today: there is no `KeepScreenOn` or wake lock anywhere in `src/`.

**Decisions.**
- Keep the screen on **only while the book is playing aloud**. When reading is paused or stopped, the phone's normal sleep timer applies again, so a book left open on pause does not drain the battery.
- Also release it when the reading page closes (`OnDisappearing`), when the app goes to the background, and when the book reaches its last page and stops.
- **Android and iOS are both required** (the user, 2026-10-25), and the Windows head comes along. Use MAUI's `DeviceDisplay.Current.KeepScreenOn`, checked with context7 for each platform before coding:
  - Android sets the window's keep-screen-on flag, with no extra permission.
  - iOS turns off the idle timer (`UIApplication.IdleTimerDisabled`).
  - Windows has its own equivalent.
- iOS clears the idle-timer setting when the app is backgrounded, so on iOS the screen must be kept on again when the app returns to the front while a book is still playing. The same applies on Android after the window is recreated. Re-apply the on state on resume, never just once at start.
- Only the Dino app. RTST mobile reads with the screen off on purpose and is not changed.

**Work.**
- [ ] `ScreenAwake`: one small class that turns keep-screen-on on or off on the UI thread and remembers its state, so repeated calls are cheap and safe.
- [ ] `ReadingPage`: on when playback starts or resumes (and again when the app comes back to the front while playing, for iOS) (`OnPlayTapped` and any auto-play), off on pause, stop, the end of the book, `OnDisappearing` and the app going to the background. Page turns while playing keep it on.
- [ ] Unit-test the on/off rules if they can be pulled out as a pure state rule (playing gives on, anything else gives off). The platform call itself is checked on a device.
- [ ] Emulator check via `device-test`, with the screen timeout set to 15 s (`adb shell settings put system screen_off_timeout 15000`):
  - Playing a book for more than 30 s keeps the screen on.
  - Paused, the screen sleeps after about 15 s.
  - Leaving the book while it plays lets the screen sleep.
  - Restore the timeout afterwards.

- [ ] iOS: the app compiles for iOS (`dotnet build src/DinoReader.App -f net10.0-ios -p:IncludeIos=true -p:IncludeWindows=false -p:IncludeAndroid=false`, compile only on Windows). No iOS device or simulator is reachable from this machine, so the iPhone check is the user's, on the next TestFlight build: a whole book plays with Auto-Lock at 30 s and the screen stays on, and paused it locks again.

**Verification.** `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green, the iOS compile, the Android emulator check above, then the user's devices: on both an Android phone and an iPhone, a whole book plays with the screen on.
