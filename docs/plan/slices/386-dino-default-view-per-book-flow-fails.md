# Slice 386 — Dino: the `default-view-per-book` flow fails on main
**Product:** Dino

*Built 2026-11-15, PR #1163, not merged. Passed on the Android emulator 2026-11-15.*

**Touches:** `.maestro/dino/default-view-per-book.yaml`, `docs/plan/slices/386-dino-default-view-per-book-flow-fails.md`.

## Why

Slice 378's device run (2026-11-14) ran the Dino flows on a build of `main`. The flow `default-view-per-book` fails with:
`Assertion is false: "Picture only.*Selected" is visible`.
Logs: `D:/ReadTheStupidText-worktrees/reviews/s378-device/baseline/` (`dino-main-maestro.log`, `main-dino.log`). The flow comes from [Slice 196](196-dino-default-view-per-book.md).

## Work

- [x] 1. Cause (proven 2026-11-15): neither the "Selected" label nor the premise's missing flag. Commit d28e4ad7 (Slice 350, Good Enough v9, text-free pictures) deliberately removed `"textInPictures": true` from `content/dino/good-enough/book.json`; the test-store asset `Resources/Raw/testbooks/good-enough.dino` (rebuilt in that commit) has no `TextInPictures` in its `story.json`, so `Story.cs:172` gives Picture + text. The flow `default-view-per-book.yaml` (lines 23-26 before the fix) still asserted "Picture only.*Selected" for Good Enough. The app is right, the flow is stale.
- [x] 2. Fixed on the flow side (app unchanged, so Android and iOS alike): Good Enough now asserts Picture + text Selected on open, then picks Picture only, reopens and asserts it is kept; the second starter still opens in Picture + text. The Picture-only default for a `textInPictures` book stays covered by the `Story.Mode` unit tests, since no test-store book carries the flag now.

## Verification

- [x] `default-view-per-book` passes on the emulator, in the same form as the failed run. Emulator 2026-11-15: PASS `default-view-per-book` (55 s), run-flows "1 passed, 0 failed", build key 7c4d1494130c, logcat `D:/ReadTheStupidText-worktrees/.emulator/logcat-i1/dino-default-view-per-book.log`.

*Changed while building:* the plan's premise (the test-store book lacks `textInPictures`) was a guess; the cause is that Slice 350 removed the flag on purpose, so the flow was fixed, not the app (item 1). The Touches line was narrowed to the flow and this file.

*From review:* high (`reviews/s386-high.md`) applied #1 (the gap is recorded in *Not yet verified*), #2, #4 and #5 (#5 lifted in the index pass, see *For CLAUDE.md*).
- Skipped #3 (`reviews/s386-high.md`): "the device stage must run it, else list it in Not yet verified" — Verification stays open until the device stage, which then ran and passed (line above).

Medium pass: 4 low findings (flow and doc only), each below.
- Skipped #1 (`reviews/s386-medium.md`): "no device flow; slice lists it under Not yet verified" — already recorded as a *Not yet verified* line.
- Skipped #2 (`reviews/s386-medium.md`): "bare substring regex; ModeSwitch inactive-segment label unchecked" — the device run passed the tap on "Picture only".
- Skipped #3 (`reviews/s386-medium.md`): "Verification has no recorded emulator run" — now recorded above.
- Skipped #4 (`reviews/s386-medium.md`): "only listed for later" — lifted in the index pass (*For CLAUDE.md*).

## Not yet verified

- No test-store book has carried `textInPictures` since Slice 350, so the device path of the Picture-only default (story.json `TextInPictures` -> `Story.Mode` -> `PictureTextPages.IsPictureOnly` -> `ModeSwitch`) is covered only by the `Story.Mode` unit tests (`TextInPicturesTests.cs`), not by any flow.

## For CLAUDE.md

- `src/Dino/DinoReader.Core/CLAUDE.md:26` and Slice 196's statements (Verification, Checks, Not yet verified) say Good Enough sets `textInPictures` and opens in Picture only; since Slice 350 no shipped book carries the flag. Correct them in the index pass; Slice 196's "flow not shown failing on old main" unverified line is moot. plan-not-needed: 196, 350
