# Slice 165 — Audio and timing checks on the real phone over wireless adb

*Planned.* Batch 36 (fast device tests), RTST Mobile and Dino Reader, Android. Decision 165. After Slice 162 (it
reuses the hooks and the `RTST-TEST` logcat markers).

**Touches:** `tools/phone/**` (new: `connect.ps1`, `audio-timing.ps1`, `README.md`),
`src/ReadTheStupidText.Application/**/ReadTimingTracker.cs` (marker lines only, if the timing is not already logged),
`src/ReadTheStupidText.Mobile/CLAUDE.md`

## Why

The emulator can't tell whether the neural voice sounds right, or how fast it really runs on a phone CPU (and the
x86_64 emulator may have no neural engine at all, Slice 161). Those checks belong on the user's own phone, over
**wireless adb only, never USB** (the user's global rule).

## Work

- [ ] `connect.ps1`: `adb mdns services` / `adb devices` first. If the phone is missing, it prints the exact ask
  (IP:port and the 6-digit code from *Developer options > Wireless debugging > Pair device with pairing code*), then
  `adb pair` / `adb connect`. It never offers USB.
- [ ] `audio-timing.ps1 -App rtst|dino`: installs the arm64 Debug build on the phone, uses the Slice 162 hooks to
  load a fixed text (and a fixed Dino page), reads it at 1x and 2x, and turns the `RTST-TEST` markers into a short
  table: time to first audio, per-chunk synthesis time, gaps between chunks, total time against the expected time
  for the rate. It flags a gap over a set limit (number fixed in the script, from the first baseline run).
- [ ] The listening part stays the user's: the script prints one line, "listen now: <what to listen for>", only when
  a change touched the voice. It never asks the user to time anything by hand.
- [ ] Rule in Mobile `CLAUDE.md`: audio quality and timing are checked on the phone with `tools/phone/`, not on the
  emulator.

iOS: no local device or Mac; iOS audio checks stay with TestFlight (internal testers). Exempt by situation, not by
choice: revisit when a Mac or iPhone is reachable.

## Verification

- With the phone paired, `audio-timing.ps1 -App rtst` prints the timing table for 1x and 2x, and the first run's
  numbers are recorded here as the baseline.
- Adding an artificial delay between chunks (a local test build) is flagged.
- With the phone off Wi-Fi, `connect.ps1` prints the pairing ask and exits without trying USB.
