# Real-phone audio and timing checks (Slice 165)

Audio quality and speed are checked on the user's own phone, over **wireless adb only (never USB)**, not on the emulator
(it cannot say how the voice sounds, and it runs the neural voice on a PC CPU).

- `connect.ps1` looks at `adb mdns services` / `adb devices`. A ready wireless phone is printed and exits 0. Otherwise it prints the
  exact ask (pairing IP:port and the 6-digit code from *Developer options > Wireless debugging > Pair device with pairing code*, then
  the connect IP:port) and exits 2. Pass those as `-PairAddress -PairCode -ConnectAddress` to pair and connect. `-Serial` checks one serial.
- `audio-timing.ps1 -App rtst|dino [-Rates 1,2] [-GapLimitMs n] [-VoiceChanged] [-SkipInstall]` builds the arm64 Debug build, installs it as
  **`<app id>.dbg`** (a second package, so the Play install, its data and its signature are never touched; nothing is uninstalled or cleared),
  skips first run with the Slice 162 hook, then reads the same fixed passage (both apps) through the `read text=... speed=...` hook at each rate
  (the speed goes to the reader; nothing is stored), and prints per read: time to first audio (from the app's own `HOOK read start` line), per chunk
  audio / play / audio-ready time and the gap beyond the audio length (flagged over `-GapLimitMs`), and the total against the audio length at that rate.
  The first run downloads the voice pack: progress lines show, and `-PackTimeoutSeconds` (default 1200) bounds it.
  `-VoiceChanged` adds one `listen now: ...` line; the script never asks anyone to time anything by hand.
  `-LogFile <saved logcat>` re-prints a table from a saved log of one read (pass `-Rates 2` for a 2x log; raw logs of each run are in `<worktrees folder>/.phone/` and hold only the dev app's lines, tags `RTST-TEST` and `DOTNET`, never the rest of the phone's log).
- The phone may stay locked: nothing is tapped. It never unlocks, changes settings or touches the volume.
- Markers: the Slice 162 `READ_START`, `CHUNK n`, `READ_END` (tag `RTST-TEST`) plus `CHUNK_AUDIO n <ms>` (Debug builds only: `ReadTimingTracker` raises a static event, `TestMarkers` writes it through `TestHookLog`, same tag).
  Debug builds send no analytics (`UsageGate`).
- Gap = time between a chunk starting and the next chunk starting, minus the chunk's audio length divided by the rate (a systematic offset exists, so judge it against the baseline in the slice file). Audio-ready time of chunk n = its `CHUNK_AUDIO` minus the previous one's (the first from the read command); synthesis overlaps playback, so it is not pure synthesis time.
- iOS: no Mac or iPhone here; iOS audio checks stay with TestFlight internal testers.
- Reading starts through the Debug `read text=... speed=...` hook, so the phone may stay locked. Dino reads the same fixed passage through Dino's own reader (no Debug hook opens a starter book page, so its per-character page reading is not exercised: "Dino timing OK" is about the voice and the reader, not a book page). The gap limit default (1300 ms) comes from the re-run baseline in the slice file: the gap beyond the audio length is a systematic 350-400 ms at 1x and 750-900 ms at 2x, so the limit is the worst of those plus about 50%; a regression at 1x smaller than about 0.9 s will not be flagged, so compare the numbers with the baseline too.
