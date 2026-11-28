# Slice 465 — Dino: the store-restore, paid-books and subscription flows pass on a real phone
**Product:** Dino

**Touches:** `.maestro/dino/store-restore.yaml`, `.maestro/dino/paid-books.yaml`, `.maestro/dino/subscription.yaml`, `src/Dino/DinoReader.App/StorePage.xaml.cs`, `src/Dino/DinoReader.App/StorePage.xaml` (the `StoreRestoreResult` label, :432), `src/Dino/DinoReader.App/GalleryPage.xaml.cs` (only the `[Subscription] shelf columns=` marker block near :1130), `src/Dino/DinoReader.App/Testing/**`, `tests/Dino/DinoReader.Tests/**`, this slice file. `tools/phone/run-flows.ps1` is run, not edited.

*Planned 2026-11-27 (index pass, from Slice 448's S21 run 4: 1 of Slice 396's 4 flows passed, these 3 failed).*

*Built 2026-11-28 (PR #1456); all three flows pass on the S21 (run 2, build key 1eb1333ce861); not run on emulator-5554 this wave.*

**Premise check (coordinator, wave 2026-11-28):** the run 4 logs say, for `store-restore` and `paid-books`, that `Tap on id: StoreRestorePurchases` COMPLETED and then the result (`Nothing bought with this .* yet\.`, id `StoreRestoreResult`) was not visible; for `subscription`, every step COMPLETED and only the log marker `[Subscription] shelf columns=3 grownUp=True` was missing. That marker is written at `GalleryPage.xaml.cs:1130` inside `#if DEBUG` and only on the path that lays out shelf columns, so the phone's column count or build may differ. The debug screenshots and hierarchies in the run 4 folder decide which; read them before choosing a fix. A parked worktree (Slice 403's s403) has uncommitted edits in StorePage.xaml.cs near lines 507-728 and DinoTestCommands.cs near 219: keep edits there to the minimum the fix needs.

## Why
Slice 448 made `tools/phone/run-flows.ps1` run the `.maestro/dino` flows on the leased S21 (docs/plan/slices/448-tools-phone-runs-maestro-dino-flows.md). Its run 4 passed one flow and failed these three, each inside the store flows. Until they pass, a real-phone run cannot check buying, restoring or the subscription shelf.

## Work items
- [x] 1. Read the S21 run 4 logs in `D:/ReadTheStupidText-worktrees/reviews/s448-device/run4/`. The three failures:
  - `store-restore`: the assert `Nothing bought with this .* yet\.` is not visible.
  - `paid-books`: the id `StoreRestoreResult` is not visible.
  - `subscription`: the marker `[Subscription] shelf columns=3 grownUp=True` is missing.
- [x] 2. For each, find whether the flow or the app is wrong on a real phone (the emulator passes them), using `src/Dino/DinoReader.App/StorePage.xaml.cs` and the test hooks under `src/Dino/DinoReader.App/Testing/`.
- [x] 3. Fix it (flow, app or test hook) with a test in `tests/Dino/DinoReader.Tests/` where the cause is in code.
- [x] 4. Rerun on the S21: `pwsh -NoProfile -File tools/phone/run-flows.ps1 -App dino -Flow store-restore,paid-books,subscription -Install`; all three must pass.

## Causes (found 2026-11-28 from the run 4 logcat and Maestro log)
- **`store-restore` and `paid-books`: the flow was wrong.** The app logged `[StoreRestore] message=NothingFound` at 10:38:51.185, right after the tap at 10:38:50.7. Maestro's tap then waited 10 s for the screen to settle (its default; Maestro log: tap COMPLETED at 10:39:00.84), so the wait for the message started after its 5 s life (`MessageSeconds`) was over, and failed with the message already gone. Fix: `waitToSettleTimeoutMs: 500` on the `StoreRestorePurchases` taps (the Redeem tap already had it).
- **`subscription`: the flow was wrong.** The phone logged `[Subscription] shelf columns=2 grownUp=False` once; a phone held upright has 2 columns since Slice 418 (`ShelfTitleRules.Columns`), and the marker is written only when the layout key changes, never again on unlock. The expectation `columns=3 grownUp=True` could not happen on a phone. Fix: expect `[Subscription] shelf columns=2`; `ShelfTitleRulesTests` gains the S21's 360x800 case.
- **What each run shows (review fix, PR #1456).** *S21 run 4* (`reviews/s448-device/run4/`): `store-restore` logged `message=NothingFound` at 10:38:51.185, the tap COMPLETED only at 10:39:00.84 (10 s settle), and the wait then failed with the message gone: that cause is read from the log. `paid-books` failed at `assertVisible id: StoreRestoreResult` straight after the tap (`phone-paid-books.log` lines 19-20) and that run has no `[StoreRestore]` marker for it, so the 10 s settle is only the likely cause there. *Live check 2026-11-27* (Slice 448 Follow-ups, build ab345cbf; I did not check whether this PR's flow edits were in it): `store-restore` PASSED (68 s) with the flow unchanged and `paid-books` FAILED at `id: StoreRestoreResult` not visible, a 30 s wait, so the settle delay alone does not obviously explain `paid-books`. The paid-books cause stays **likely, not proven** (candidates: the message's 5 s life against slow dumps, the already-owned path `RestoreLine.AllOnShelf` / `Store_OnShelf` in `StorePage.xaml.cs`, another message hiding it) until item 4's S21 rerun. `subscription` failed on the `columns=3` marker in that check too, which matches the cause above.
- **Fix added in review (Debug builds only):** `StorePage.MessageSeconds` is 30 in a Debug build and 5 in release, so the asserts after the message appeared (each a slow dump) still see it; `store-restore.yaml`'s last wait for the message to go now allows 45 s. No other flow waits for a store message to disappear (grep of `.maestro/`). The runtime effect is Debug-only, but the S21 rerun must confirm it.
- **Item 4, the S21 rerun (same form as the failure, `run-flows.ps1 -App dino -Flow store-restore,paid-books,subscription -Install`).** Run 1 (00:20Z): all 3 FAILED at the first step `Assert that id: FirstRunLanguage is visible`; the focused window was `NotificationShade` (the shade was left open by `tools/phone/preflight.ps1 -Fix`), so the run says nothing about the fixes (logs `reviews/w12-device/s465/`). Run 2, after `cmd statusbar collapse`: all 3 PASSED in 332 s (store-restore 81.3 s, paid-books 58.2 s, subscription 189.0 s), build key 1eb1333ce861 (logs `D:/ReadTheStupidText-worktrees/reviews/w12-device/s465-run2/`). `paid-books` passed with the 500 ms settle and the Debug 30 s message life together; the run cannot tell which of the two did it, so the paid-books cause is proven only as far as "this combination passes".
- **Checks:** `DinoReader.Tests` 5341 passed, 0 failed, 2 skipped; `lint-flows -CheckSource` 175 flows, 0 findings; `Build-TestPackage -App dino` Release PASS (key a109f7495ec9).

#### From review
- Applied, high pass (pr-1456-high, 6 findings read): applied finding 1 (Causes text reconciled with the Slice 448 live check; paid-books stays "likely" until the rerun), 2 (Debug-only `MessageSeconds` 30 s in `StorePage.xaml.cs`, store-restore's last wait 45 s), 3 (same Debug message life covers the paid-books `assertNotVisible` steps), 6 (note added to the `GalleryPage.xaml.cs` marker comment). Findings 4 and 5: see the lines below.

- Applied, medium pass (pr-1456-medium, 4 findings read): none applied, all four left as they are (lines below).
- Skipped #4 (`reviews/pr-1456-high.md`): "`shelf columns=2` is a bare substring with no `grownUp` part, and the marker is written only when the layout key (`chrome|columns`) changes, so an unlock re-layout is never seen: a later change to 3 columns when unlocked would still pass." - the finding itself says acceptable: the key excludes grownUp (Slice 418).
- Skipped #5 (`reviews/pr-1456-high.md`): "The new `(360, 800, 2)` row adds no coverage over the existing 375/393/412/440 phone-portrait rows and does not pin the logged marker string." - the finding itself says keep as documentation; the row is harmless.
- Skipped #1 (`reviews/pr-1456-medium.md`): "so the 5 s life users get is unchecked" - a nit: release users keep the 5 s life unchanged (only Debug builds got 30 s), so no behaviour a user sees changed.
- Skipped #2 (`reviews/pr-1456-medium.md`): "coupled to the Debug 30 s constant with no guard" - a nit: the coupling is recorded in this slice's Causes and the 45 s wait only needs to exceed 30 s; no test guards it.
- Skipped #3 (`reviews/pr-1456-medium.md`): "`columns=2` is weaker than the old `columns=3 grownUp=True`" - repeats high #4; the old expectation could never happen on a phone (Causes), and the S21 rerun passed the new one.
- Skipped #4 (`reviews/pr-1456-medium.md`): "The (360, 800, 2) row duplicates existing phone-portrait rows" - repeats high #5; the row stays as documentation of the S21 size.

#### Follow-ups (planned work, not this slice)
- `tools/phone/preflight.ps1 -Fix` left the notification shade open, and `run-flows.ps1` then failed every flow at the first step (`Assert that id: FirstRunLanguage is visible`, focused window `NotificationShade`; S21 run 1, 2026-11-28, logs `reviews/w12-device/s465/`). Preflight should collapse the shade (`cmd statusbar collapse`) and check the focused window. Needs its own slice.

#### For CLAUDE.md
None.

#### Not yet verified
- [session] The three changed flows (`store-restore`, `paid-books`, `subscription`) still pass on emulator-5554; only the S21 was run this wave.
