### Slice 126 — Dino: keep the screen awake while a book is playing

*Planned.*

**Touches:** `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ScreenAwake.cs` (new), `src/DinoReader.App/CLAUDE.md`

**Why.** Reported by the user on 2026-10-25, after checking the 16-page pigs on their Android phone: while a book plays, the phone's screen goes to sleep. The child can no longer see the pictures or the highlighted words, and on many phones the reading stops too. Nothing in the app keeps the screen awake today: there is no `KeepScreenOn` or wake lock anywhere in `src/`.

**Decisions.**
- Keep the screen on **only while the book is playing aloud**. When reading is paused or stopped, the phone's normal sleep timer applies again, so a book left open on pause does not drain the battery.
- Also release it when the reading page closes (`OnDisappearing`), when the app goes to the background, and when the book reaches its last page and stops.
- Use MAUI's `DeviceDisplay.Current.KeepScreenOn` (Android sets the window's keep-screen-on flag, with no extra permission; iOS and Windows have equivalents), checked with context7 before coding. The same code therefore covers iOS and the Windows head.
- Only the Dino app. RTST mobile reads with the screen off on purpose and is not changed.

**Work.**
- [ ] `ScreenAwake`: one small class that turns keep-screen-on on or off on the UI thread and remembers its state, so repeated calls are cheap and safe.
- [ ] `ReadingPage`: on when playback starts or resumes (`OnPlayTapped` and any auto-play), off on pause, stop, the end of the book, `OnDisappearing` and the app going to the background. Page turns while playing keep it on.
- [ ] Unit-test the on/off rules if they can be pulled out as a pure state rule (playing gives on, anything else gives off). The platform call itself is checked on a device.
- [ ] Emulator check via `device-test`, with the screen timeout set to 15 s (`adb shell settings put system screen_off_timeout 15000`):
  - Playing a book for more than 30 s keeps the screen on.
  - Paused, the screen sleeps after about 15 s.
  - Leaving the book while it plays lets the screen sleep.
  - Restore the timeout afterwards.

**Verification.** `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green, the emulator check above, then the user's phone: a whole book plays with the screen on.
