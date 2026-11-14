# Slice 385 — RTST: the `voice-cancel-holds-scan-pack` flow fails on main's build
**Product:** RTST

*Planned.*

**Touches:** `.maestro/rtst/voice-cancel-holds-scan-pack.yaml`, the RTST Mobile code the first work item names as wrong (`src/Rtst/ReadTheStupidText.Mobile/**`, only if the app is the faulty side), `docs/plan/slices/385-rtst-voice-cancel-flow-fails-on-main.md`

## Why

Slice 378's device run (2026-11-14) ran the RTST flows on a build of `main` too. The flow `voice-cancel-holds-scan-pack` fails on main's build with:
`Assertion is false: "در حال دانلود صداهای طبیعی.*", id: PackStatusLabel is visible`.
Logs: `D:/ReadTheStupidText-worktrees/reviews/s378-device/baseline/` (`rtst-main-maestro.log`, `main-rtst.log`). It is not yet known whether the app or the flow is wrong.

## Work

- [ ] 1. Read the flow and the logs, and find whether the app shows the wrong status text/state after the voice download is cancelled, or the flow asserts the wrong thing (state which, with the file and line).
- [ ] 2. Fix the faulty side. If the app: Android and iOS alike (same app), with a test where the logic is pure. If the flow: correct the assertion without weakening what the flow proves.

## Verification

- `voice-cancel-holds-scan-pack` passes on the emulator, in the same form as the failed run (same flow, same build).
- Not run in the planning pass: nothing here is verified yet.
