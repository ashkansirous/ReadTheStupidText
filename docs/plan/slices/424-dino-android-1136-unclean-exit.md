# Slice 424 — Dino: the Android 1.136.0 unclean exit of 2026-11-20 (first run, then the new shelf's Settings) and the gaps that hide its cause
**Product:** Dino

*Plan-cleanup check 2026-11-25: keep open, nothing started. Checked on origin/main 1a8bf725: no `first-run-to-settings.yaml`; no PR for this slice's work (only planning PR #1323); `RollingLogFile` has no 5 s timer; the Crashes folder has no session id or last-page field (git grep). `AndroidCrashHooks` already reads `GetHistoricalProcessExitReasons` (Slice 227) but only for native, ANR and low-memory kinds, so item 3 (reason on `unclean_exit`) is still open. No outside waits; all 8 items are this slice's own work.*

*Planned 2026-11-20 from the crash store and `dino_usage` (read 2026-11-20 ~18:30 UTC). User, 2026-11-20: "add a bug/slice for the crash".*

**Touches:** `src/Shared/Sirous.Reading.Mobile.Shared/Crashes/CrashReporter.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/Platforms/Android/Crashes/AndroidCrashHooks.cs`, `src/Shared/Sirous.Reading.Application/Crashes/LaunchFacts.cs`, `src/Shared/Sirous.Reading.Application/Crashes/CrashReport.cs`, `src/Shared/Sirous.Reading.Application/Crashes/CrashReportBuilder.cs`, `src/Shared/Sirous.Reading.Application/Crashes/CrashReportJson.cs`, `src/Shared/Sirous.Reading.Application/Crashes/RollingLogFile.cs`, `src/Shared/Sirous.Reading.Application/Crashes/PendingCrashStore.cs`, `tests/Shared/Shared.Tests/` (crash tests), `worker/` (only if the report JSON gains a field the Worker must keep), `.maestro/dino/first-run-to-settings.yaml` (new), this slice file.

## Why

The first crash report on a 1.136.0 build (Slice 418's one shelf with a lock):

- **Report `0ac1c962-6f03-40b9-9c45-5d228a426e16`**: `unclean_exit` (run marker left behind), Android 1.136.0 store build 1136000, phone, arm64, RAM 8+, Android 17, GB. Written at the next launch, 2026-11-20 09:45Z. No stack, no Play crash within 24 h. The report has **no session, app state or memory fields**.
- **Its log tail** (stored copy cut mid-line) starts 09:07:01 `page shown FirstRunPage`, runs the first-run pack downloads (fa v3, en v1, fa v2), a read with premade clips 40-45 ("28 of 28 pieces have a clip"), and ends 09:08:19.662 `I page sho` right after `reading stopped (user)`, `page left ReadingPage`, `page left NavigationPage`.
- **The usage events of the matching session** (`97bef124…`, install age `first`; matched by device bucket, version, country and time: *likely*, the report carries no session) go on to 122 s after launch: the grown-ups' gate, `corner_books:book_more`, `corner_store`, `corner_settings`, and last `tap corner_settings:tab_books`. So the app was in use about 45 s after the last log line, and the log tail on disk was behind.
- **Other reports since 2026-11-17** (all Android store): one managed "child already has a parent" on 1.131.3 (Slice 394's signature); three `unclean_exit`s on 1.131.3 (11-18 14:03, 14:18, 14:22) from one device bucket with a **byte-identical log tail** ending in `memory warning Background` at 08:07, which looks like one death reported three times (*likely*); one `unclean_exit` on 1.129.0 (11-19) that fits Slice 400's "reading with premade clips, then killed" group.

[Slice 400](400-dino-crashes-2026-11-14.md) grouped 26 earlier unclean exits as likely system kills. Today's one is on a newer build than any of those and died in the first minutes of a fresh install on an 8 GB phone, on the new shelf (Settings tab), so a system kill is the likely reading but not a safe one. No cause is proven; anything below that names a code path is "likely" until a log or a rerun shows it.

## Work

- [ ] 1. **Reproduce.** Scripted flow `.maestro/dino/first-run-to-settings.yaml` on the emulator and the S21 (release build 1.136.0 from Play on the S21; dev install on the emulator): fresh install, first run with fa and en, read one first book to a stop, back to the shelf, grown-ups' gate, Books "more", Store, Settings, the Books tab. Watch logcat for `FATAL`, `ANR`, native signals and the process dying; read `ApplicationExitInfo` for the app after the run. Same walk on the iOS simulator (CI, `dinoreader-ios-sim.yml`) for the iOS twin.
- [ ] 2. **Read Play's side.** Run `tools/crashes/pull-play.py` for 1.136.0 (crashes and ANRs) and write what it shows here. If Play has a matching crash or ANR, its stack names the fix and item 5 is that fix.
- [ ] 3. **The exit reason in the report.** An Android `unclean_exit` carries the `ApplicationExitInfo` reason, sub-reason, importance and description of the crashed launch when Android has one (user swipe, low memory, ANR, native crash, "user requested"), so a system kill is shown, not guessed. iOS: carry the MetricKit exit counts of the period when they arrive (foreground/background, memory, watchdog), matched by the same ledger as Slice 227.
- [ ] 4. **The crashed launch's own facts.** The crashed launch's session id, last page and app state (foreground/background) are written to its facts file as they change, and an `unclean_exit` (both platforms) carries them. Slice 250 sent null here on purpose; that was before the per-launch facts file of Slice 243 existed. Keep it a per-launch random id, never a device id.
- [ ] 5. **The log tail is current.** The rolling log is flushed on every page change, on going to the background and at least every 5 s, so a death loses at most a few seconds (today's lost about 45 s). Test with a pure test on `RollingLogFile`.
- [ ] 6. **One death, one report.** Find why three 1.131.3 reports carry the same log tail (the marker or the pending report not cleared after sending, or the log never advancing) and make a second launch never resend the same death. Pure test in `Shared.Tests`.
- [ ] 7. **Fix what items 1-3 name**, on Android and iOS, in this slice if it is small and inside Touches; otherwise a follow-up slice with its own Touches, linked here.
- [ ] 8. Add today's report and the 11-17..11-20 ones to the grouping (here, not in Slice 400's table) with "system kill" or "needs a fix" for each.

## Verification

- Item 1: the flow runs on the emulator and the S21 and either reproduces the death (with its logcat lines quoted here) or passes three times with no death.
- Items 3-5: Debug hook `crash-test kind=kill` on the emulator, then relaunch: the stored report has an exit reason, a session id, the last page, and a log tail whose last line is within 5 s of the kill. iOS: the same fields on a simulator `kill` in CI (exit reason may be absent there; say so).
- Item 6: the pure test, then on the emulator a kill followed by three relaunches gives exactly one report.
- After release: the crash store shows the new fields on the next real `unclean_exit` (read with `tools/crashes/report.mjs -App dino`).

## Not yet verified

_(none yet)_
