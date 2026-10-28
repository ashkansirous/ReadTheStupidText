# Slice 165 — Audio and timing checks on the real phone over wireless adb

*Built 2026-10-28 on the Pixel 10 Pro over wireless adb; iOS compile and the user's listening check not yet verified.* Batch 36 (fast device tests), RTST Mobile and Dino Reader, Android. Decision 165. After Slice 162 (it
reuses the hooks and the `RTST-TEST` logcat markers).

**Touches:** `tools/phone/**` (new: `connect.ps1`, `audio-timing.ps1`, `README.md`),
`src/ReadTheStupidText.Application/**/ReadTimingTracker.cs` (a Debug-only static event only),
`src/ReadTheStupidText.Mobile.Shared/Testing/**` (`read` hook, `CHUNK_AUDIO` marker),
`src/ReadTheStupidText.Mobile/CLAUDE.md`

## Why

The emulator can't tell whether the neural voice sounds right, or how fast it really runs on a phone CPU (and the
x86_64 emulator may have no neural engine at all, Slice 161). Those checks belong on the user's own phone, over
**wireless adb only, never USB** (the user's global rule).

## Work

- [x] `connect.ps1`: `adb mdns services` / `adb devices` first. If the phone is missing, it prints the exact ask
  (IP:port and the 6-digit code from *Developer options > Wireless debugging > Pair device with pairing code*), then
  `adb pair` / `adb connect`. It never offers USB.
- [x] `audio-timing.ps1 -App rtst|dino`: installs the arm64 Debug build on the phone, uses the Slice 162 hooks to
  load a fixed passage (read through Dino's reader for Dino; no hook opens a book page), reads it at 1x and 2x, and turns the `RTST-TEST` markers into a short
  table: time to first audio, per-chunk synthesis time, gaps between chunks, total time against the expected time
  for the rate. It flags a gap over a set limit (number fixed in the script, from the first baseline run).
- [x] The listening part stays the user's: the script prints one line, "listen now: <what to listen for>", only when
  a change touched the voice. It never asks the user to time anything by hand.
- [x] Rule in Mobile `CLAUDE.md`: audio quality and timing are checked on the phone with `tools/phone/`, not on the
  emulator.

iOS: no local device or Mac; iOS audio checks stay with TestFlight (internal testers). Exempt by situation, not by
choice: revisit when a Mac or iPhone is reachable.

## Verification

- With the phone paired, `audio-timing.ps1 -App rtst` prints the timing table for 1x and 2x, and the first run's
  numbers are recorded here as the baseline.
- Adding an artificial delay between chunks (a local test build) is flagged.
- With the phone off Wi-Fi, `connect.ps1` prints the pairing ask and exits without trying USB.

## Built (2026-10-28)

- `tools/phone/`: `connect.ps1`, `audio-timing.ps1`, `common.ps1`, `timing.ps1`, `README.md`. `ReadTimingTracker` raises a Debug-only static event outside its lock and `TestMarkers` writes it as `CHUNK_AUDIO n <ms>` through `TestHookLog` (tag `RTST-TEST`): the Slice 162 markers carry no audio length.
- The Slice 162 hooks could not load text, so a Debug-only **`read text=... [speed=x]`** hook was added to `TestHookDispatcher` (shared by both apps and both platforms, so iOS gets it through its URL scheme; no per-app handler file changed). `TestMarkers` now counts chunks per read (the first `CHUNK` arrives just before `READ_START`, which used to reset the count). Approved by the coordinator (Touches widened to `Mobile.Shared/Testing/**`).
- The Play installs of both apps are on the phone, so the Debug build goes on as `<app id>.dbg` (`-p:ApplicationId=...`).
- Playback runs with the phone locked (docked, secure lock); no taps, so the script needs no unlock.
- The Dino run reads the same fixed passage through the Dino build's reader (not a book page); The Dino gap is judged at 2x too (the review-1 re-run showed Dino's gaps match RTST's).
- Gap proof: a local 1.5 s delay in `HandleChunkCompleted` (reverted) gave `gap 1916 ms  <-- GAP over 1200 ms`.
- Not yet done: iOS (no Mac); a listening check by the user.

*Changed while building:* the Slice 162 hooks had no read/play hook, so the `read` hook and `Mobile.Shared/Testing/**` were added (Touches widened, coordinator-approved). Dino reads a fixed passage through its reader, not a book page (no page hook). Time zero is `HOOK read start`, logged before `SpeakAsync`. Logcat capture is filtered to `RTST-TEST` and `DOTNET` (privacy). `-GapLimitMs` is 1300.

*From review:* pass 1 and 2 (`reviews/165-review1.md`, `165-review2.md`) were fixed by the fixer (the Debug-only tracker event raised outside the lock, `HOOK read start` as time zero, filtered logcat capture, Dino wording and baseline re-run). Per the brief, the bad-speed and faulting-read hook paths stay unexercised; the review-2 note that `timing.ps1` and `StartRead` have no tests is not recorded as addressed.

**Follow-up worth a slice:** the first Dino 2x read after install was cold (8.9 s to first audio, a 4918 ms gap between chunks; the gap flag caught it). Warm runs are fine.

#### For CLAUDE.md

- Mobile (`src/ReadTheStupidText.Mobile/CLAUDE.md`): audio quality and timing are checked on the real phone with `tools/phone/`, not on the emulator; wireless adb only. Already added (Slice 165: docs/plan/slices/165-real-phone-audio-timing-checks.md).
- Mobile: phone dev builds install as `<app id>.dbg` and never replace the Play install. Already in the same rule (Slice 165: docs/plan/slices/165-real-phone-audio-timing-checks.md).

#### Not yet verified

- iOS compile of the shared `read` hook and `CHUNK_AUDIO` marker (no Mac).
- Dino reads a fixed passage through its reader, not a starter book page (no page hook).
- The `read` hook's bad-speed and faulting-read paths were not exercised.
- The user's listening check of the voice on the phone.

### Baseline (Pixel 10 Pro, Android 17, arm64 Debug, same passage, 2 chunks; re-run 2026-10-28 after review-2 fixes, time zero = the app's `HOOK read start`)

| App | Rate | First audio | Audio ready chunk 1 / 2 | Gap beyond audio after chunk 1 | Total read vs audio at rate |
| --- | --- | --- | --- | --- | --- |
| rtst | 1x | 247 ms | 107 / 79 ms | 338 ms | 26.5 s vs 25.9 s |
| rtst | 2x | 220 ms | 112 / 55 ms | 758 ms | 14.4 s vs 12.9 s |
| dino | 1x | 3974 ms | 3868 / 3041 ms | 381 ms | 26.5 s vs 25.9 s |
| dino | 2x | 1.7-2.0 s (warm) | 1653 / 71 ms | 792-873 ms | 11.3 s vs 9.7 s |

"Audio ready" is the gap between consecutive `CHUNK_AUDIO` lines, not pure synthesis time (synthesis overlaps playback). The very first Dino 2x run after install was cold (first audio 8.9 s) and chunk 2 was late: a real 4918 ms gap, flagged; warm runs are as in the table. Dino's first audio varies a lot (1.7-8.9 s): the number to watch when its voice changes. Unfiltered phone logs were dropped: saved logs now hold only the dev app's `RTST-TEST` and `DOTNET` lines.
`-GapLimitMs` default stays **1300** (worst warm gap 873 ms plus about 50%); the gap has a systematic offset that grows with the rate (about 350 ms at 1x, 800 ms at 2x), so a regression under about 0.9 s at 1x is not flagged by the limit alone.
