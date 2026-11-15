# Slice 385 — RTST: the `voice-cancel-holds-scan-pack` flow fails on main's build
**Product:** RTST

*Built 2026-11-15, PR #1165, not merged. Flow-side fix only (the app is unchanged). Verified on the emulator 2026-11-15.*

**Touches:** `.maestro/rtst/voice-cancel-holds-scan-pack.yaml`, the RTST Mobile code the first work item names as wrong (`src/Rtst/ReadTheStupidText.Mobile/**`, only if the app is the faulty side), `docs/plan/slices/385-rtst-voice-cancel-flow-fails-on-main.md`

## Why

Slice 378's device run (2026-11-14) ran the RTST flows on a build of `main` too. The flow `voice-cancel-holds-scan-pack` fails on main's build with:
`Assertion is false: "در حال دانلود صداهای طبیعی.*", id: PackStatusLabel is visible`.
Logs: `D:/ReadTheStupidText-worktrees/reviews/s378-device/baseline/` (`rtst-main-maestro.log`, `main-rtst.log`). It is not yet known whether the app or the flow is wrong.

## Work

- [x] 1. Read the flow and the logs, and find whether the app shows the wrong status text/state after the voice download is cancelled, or the flow asserts the wrong thing (state which, with the file and line). **Result: the flow is faulty (a race), the app is fine.** In `.maestro/rtst/voice-cancel-holds-scan-pack.yaml` the optional `android:id/button2` wait (lines 21-25, 15 s) found no sheet and ran 23:12:19.4 to 23:12:29.997 (`rtst-main-maestro.log` lines 33-35); meanwhile piper-fa downloaded `PACKDL start 23:12:18.305` to `PACKDL done ... seconds=11.8 23:12:30.068` (`.emulator/logcat-i1/rtst-voice-cancel-holds-scan-pack.log`). The Downloading assertion (lines 31-35) started about 23:12:31.8, after the download was done, and timed out at 23:13:13.6.
- [x] 2. Fix the faulty side. Flow: the first launch now arms `stall-pack-network id=piper-fa` (the Slice 320 fix already used by `cancelled-voice-pack-stays-cancelled.yaml`), so the voice download hangs until the Cancel hook lands; the assertions are unchanged, so the cancel still happens mid-download and the hold marker is still required. The sibling flow already had this fix and has no race.

## Verification

- [x] `voice-cancel-holds-scan-pack` passes on the emulator, in the same form as the failed run (same flow, same build). Emulator 2026-11-15: PASS `voice-cancel-holds-scan-pack` (35 s), `rtst: 1 passed, 0 failed`, markers `HOOK stall-pack-network ok` and `OCRPACK held-voice-cancelled fa` seen, build key `7c4d1494130c`. Logcat `D:/ReadTheStupidText-worktrees/.emulator/logcat-i1/rtst-voice-cancel-holds-scan-pack.log`, run log `D:/ReadTheStupidText-worktrees/reviews/wave6-device/s385-flow-vc.log`. The failing form on old main is the s378 baseline run (`D:/ReadTheStupidText-worktrees/reviews/s378-device/baseline/`).

## From review:

High (`reviews/s385-high.md`, findings 6/6 read): #3, #4 and #5 applied; the other three are listed below. Medium (`reviews/s385-medium.md`, findings 4/4 read): all four are listed below.

- Skipped #1 (`reviews/s385-high.md`): "With the stall armed, Cancel hits a transfer hung on its first body read with zero bytes copied, not an active download." - same state as the sibling flow, whose cancel proves the same Downloading state.
- Skipped #2 (`reviews/s385-high.md`): "wait is dead time inside the stall-guard budget; nothing bounds the time until Cancel." - no failure path shown; the device run will show it.
- Skipped #6 (`reviews/s385-high.md`): "`# expect-log: HOOK stall-pack-network ok` only proves the hook was received, not that piper-fa's transfer took the stall." - the OCRPACK hold marker is what the flow proves.
- Skipped #1 (`reviews/s385-medium.md`): "After Cancel and cold restart, the stalled read may still run until the 60 s guard; no check that no new download starts." - the cold-restart no-download check is the job of the sibling flow `cancelled-voice-pack-stays-cancelled`.
- Skipped #2 (`reviews/s385-medium.md`): "New header comment holds an incident timeline (timestamps) and restates the sibling's Slice 320 recipe." - the timeline comment documents the race.
- Skipped #3 (`reviews/s385-medium.md`): "Items 1-2 ticked [x] while verification says the emulator run is pending." - the items stay ticked; the verification is now recorded above.
- Skipped #4 (`reviews/s385-medium.md`): "Two-launchApp arm-then-start recipe is copied from the sibling flow, not shared via runFlow." - copying the sibling's two-launch recipe is the documented pattern (`tools/CLAUDE.md`; hooks use `launchApp` with `stopApp: false`).
