### Slice 198 — RTST phone: a Read tapped while early work is still running reuses it

*Closed 2026-10-30: measured on the S21, no code change needed (PR #584).* *Planned 2026-10-29.* Read The Stupid Text on Android and iOS (shared MAUI read path; Windows unchanged). Gap left in Slice 139 (*Not yet verified*).

**Touches:** `src/ReadTheStupidText.Application/Reading/EarlyStartCoordinator.cs`, `src/ReadTheStupidText.Application/Reading/EarlyStartPlan.cs`, `src/ReadTheStupidText.Application/Reading/EarlyStartVoice.cs`, `src/ReadTheStupidText.Application/Reading/ReadAloudService.EarlyStart.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.EarlyStart.cs`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `tests/ReadTheStupidText.Tests/EarlyStart*.cs`, `tools/phone/**` (a scripted race run), `docs/plan/slices/198-rtst-phone-in-flight-early-start-race.md`.

#### Why

Early start (Slice 139) makes the first sentence ready before Read is tapped. When the early work is still running, the read starts cold instead of waiting for it: a Read tapped about 0.3 s after unticking Translate (which restarts early work), or Translate-and-read before the early voice is stored. On the swapping emulator that was 12-24 s of silence instead of under a second. The real Pixel 10 Pro run (2026-10-28) gave 2.7 s first audio but never tried the race.

#### Decisions

- First a scripted real-phone run of the race (`tools/phone`, wireless adb, the Slice 165 timing markers): untick Translate, tap Read 0.3 s later, five times; and Translate-and-read before the early voice lands. If first audio is within 1 s of the non-race case every time, record it and close the slice with no code change.
- If it reproduces, the fix is in the coordinator: a Read that finds early work in flight for the same text, voice and translation joins it (bounded by the existing 10 s settle cap) instead of starting a second synthesis. A different text or voice still cancels and starts fresh.

#### Claim notes (2026-10-30, grounded)

- Single owner today: `EarlyStartCoordinator._run` (`EarlyStartCoordinator.cs:32`). `RestartDeferred` (:80-97) cancels the old run unless `KeepsChoice` (:113-116), which needs a tap to have committed it. A Read 0.3 s after unticking Translate reaches `ChooseRead` (:151); `SettleChosenAsync` (:168) awaits only committed tasks, so an in-flight run with `OriginalCommitted=false` is not joined: a cold start is possible (from code, not yet measured).
- The "10 s settle cap" is not in this file; find it (`ReadAloudService.EarlyStart.cs` or `ReaderPage.EarlyStart.cs`) before relying on it.
- Order: the phone measurement merges its numbers into this file first; code only if it reproduces.

#### Work

- [x] Scripted phone run of both races; numbers recorded here.
- [x] Not needed: the join rule in `EarlyStartCoordinator`, with the decision (same key? in flight? restarted?) in a pure, unit-tested method.
- [x] Not needed (no code change): unit tests: Read during in-flight work for the same key joins; for a different key cancels; restart after untick then Read joins the restarted work.
- [x] Not needed (no code change): Android and iOS share the code; iOS compile check here, TestFlight timing under *Not yet verified*.

- Phone run 2026-10-30, S21 (Debug build of main, French text offering Translate to Farsi, Review page driven by uiautomator + `input tap`; script `tmp/race.sh`, time = phone clock at the tap to the `CHUNK 1` marker). Non-race baseline (untick, wait 5 s, Read): 372, 482, 346 ms. Race A (untick, Read about 0.3 s later, 5 tries): 362, 397, 394, 374, 407 ms, all within 1 s of baseline. Race B (Translate-and-read tapped the moment the Translate row appeared, before the early voice, 5 tries): 1482, 1089, 1072, 1041, 1288 ms; baseline Translate-and-read after 5 s: 1229 (cold engine, first run), 765, 800 ms; B is at most about 0.7 s above the warm baseline, so within 1 s. Neither race reproduces; no code change needed.

#### Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`.
- Real phone: the same scripted race run, first audio within 1 s of the non-race case in 5 of 5 tries.
- iOS: compile here; TestFlight later.
