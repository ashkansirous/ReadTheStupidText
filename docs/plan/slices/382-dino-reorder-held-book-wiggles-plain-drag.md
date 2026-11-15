### Slice 382 — Dino reorder: the held book wiggles, a drag needs no hold in edit mode, covers are sharp on first open
**Product:** Dino

*Built 2026-11-15, PR #1162, not merged. Emulator run passed; Windows VM, S21 and iOS not yet verified (see Not yet verified).*

**Touches:** `src/Dino/DinoReader.App/ShelfReorderView.cs`, `src/Dino/DinoReader.App/ShelfReorderTiles.cs`, `src/Dino/DinoReader.App/StoryCover.xaml`, `src/Dino/DinoReader.App/StoryCover.xaml.cs`, `src/Dino/DinoReader.Core/ShelfDragSession.cs`, `src/Dino/DinoReader.Core/ReorderPan.cs` (new, only if the drag start rule is pulled out as pure code), `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/Dino/DinoReader.Tests/**`, `.maestro/dino/shelf-reorder.yaml`, `tools/desktop-vm/checks/dino-reorder.ps1`, `src/Dino/DinoReader.App/CLAUDE.md`, `src/Dino/DinoReader.App/StoryTile.cs` (sets `CoverImagePath`/`HasImage`), `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs` (the typed `Cover_Waiting` property, widened 2026-11-15 by review fix #11; no open PR touches it), `docs/plan/slices/382-dino-reorder-held-book-wiggles-plain-drag.md`

**Why.** The user reported this on 2026-11-14, Dino 1.129.0 (Pixel 10 Pro), with a screenshot of the Reorder screen showing blurred covers:
- "the one that I kept my finger on, does not wiggle"
- "the first try, the images were faded, but it fixed the second try"
- "after it goes to edit mode, I still need to keep my finger down on the books before it is picked up, instead of simple drag and drop."

**Evidence (2026-11-14).**
- Code, read from origin/main: the wiggle is an `Animation` on `Rotation` (`ShelfReorderTiles.cs` 60-66, 131-152). Lifting a tile stops its wiggle on purpose (`ShelfReorderView.cs` 411-414). This follows the mock's "Lift: scale 1.10, no wiggle" (Slice 264, `docs/plan/slices/264-dino-gallery-reorder-mode.md`, Design, Motion). The user's answer below replaces that one detail.
- A drag starts through MAUI's `DragGestureRecognizer` (`ShelfReorderView.cs` 163-165). On Android and iOS that is the system drag-and-drop, which itself needs a long press. So even in edit mode every book needs a second hold.
- The hint today is "Press and hold a book, then drag it to a new place. …" (`Gallery_ReorderHint`). The mock's hint is "Drag a book to a new place. Your child's shelf shows the same order." (Slice 264, Design).
- Covers come from the shared `StoryCover` control (`ShelfReorderTiles.cs:80`). Why they look blurred on the first open was not found in code. Item 4 finds it first.
- Not reproduced on a device yet: the emulator run ran out of its 10 minutes before the reorder screen.

**Decisions (user, 2026-11-14).**
- The book under the finger keeps wiggling and grows a little (the 1.10 lift scale), so it is clearly picked up. It does not stop wiggling.
- Once the shelf is in edit mode, touching a book and moving the finger drags it straight away, with no hold. A hold is needed only once, to enter edit mode. The hint text says that, in all 15 languages.
- Covers show sharp straight away. If a cover takes more than 500 ms, it shows a loading state, never a blurred or faded picture.
- Android and iOS in this slice; the Windows head keeps working (mouse drag and keyboard path).

**Work.**
- [x] 1. Smallest end-to-end first, Android: in edit mode, a touch-down on a cover followed by a move starts the drag at once. Use a pan gesture (or the platform touch events) instead of the system drag-and-drop for touch. Check the MAUI `PanGestureRecognizer` / pointer APIs on Android and iOS with `library-docs` first. Keep all the existing drag rules: the lit target spot, others sliding, edge scrolling, Cancel restores the old order, Done saves (Slices 264, 298, 321, and `ShelfDragSession`'s rules in `src/Dino/DinoReader.Core/CLAUDE.md`). A tap still only lifts. A scroll of the grid that starts between covers still scrolls.
  - Android design (check pass, two strikes on the synthetic Cancel): the first design sent a synthetic Cancel to the cover's other touch listeners when the drag began, so MAUI's tap recognizer would not turn the drag into a tap (medium #2). It broke twice: the Cancel never reached the cover's own `onTouchEvent` (pressed state and long-press callback stayed for the whole drag), and a copy of the Move carried mixed local/raw coordinates and ran other listeners inside the real Move. Rule now: no synthetic event at all. The drag threshold is never below the platform's own touch slop (`ReorderPan.ThresholdFor`: Android `max(8 dp, ViewConfiguration.ScaledTouchSlop / density)`, iOS 8 pt). MAUI's Android tap recognizer is an `android.view.GestureDetector` (`TapAndPanGestureDetector : GestureDetector`, `InnerGestureListener.OnSingleTapUp`, dotnet/maui 10.0.130, `src/Controls/src/Core/Platform/Android/`), which stops reporting a single tap once the finger leaves the touch slop, so by the time a drag starts no tap can follow it. A rebuild mid-drag (`ClearEntries`) ends the touch pan as cancelled but never resets `ShelfDragSession` (only `Close` and `Begin` do, Core CLAUDE.md), so the Windows late-Drop rule holds. The tracked finger's screen position is `GetRawX/GetRawY(index)` on API 29+, the RawX offset formula below.
- [x] 2. iOS task: the same drag on iPhone.
- [x] 3. The lifted / dragged book keeps the wiggle at the same angle and speed as the others, at scale 1.10 with the lift shadow. Reduce motion still means no wiggle anywhere (the dashed outline stays).
- [x] 4. Find why covers are blurred on the first open: read `StoryCover` and how the reorder tiles load their image (thumbnail, placeholder or fade). Write the cause here with file:line, then fix it so the cover is sharp at once. If the picture is not ready within 500 ms, show a loading state on the tile instead of a blurred picture.
  - Cause (from code, likely until a device run shows it): `ShelfReorderView.Open` built the tiles with `FitGrid(Math.Max(_scroll.Width, 1), ...)` while the scroll was still hidden (width 0), so on the first open every cover `Image` got its source at 1 dp wide and the platform loader decoded the picture at that size; the later layout at the real width never reloaded it. The second open had the width already. Fix: `BuildOrWait` / `OnScrollSizeChanged` build the tiles only once the scroll has a width. `StoryCover.xaml.cs` adds a spinner after 500 ms of `Image.IsLoading` and a Debug `[Cover] shown <id> in N ms` line.
- [x] 5. Hint text: the mock's line "Drag a book to a new place. Your child's shelf shows the same order." in all 15 locales (`Gallery_ReorderHint`).
- [x] 6. Pure parts (when a touch becomes a drag, for example a move threshold) in Core with unit tests.
- [x] 7. Update `.maestro/dino/shelf-reorder.yaml` to drag with a plain swipe in edit mode (no hold), check the dragged book is still wiggling (a log line from the wiggle), and check the covers on the first open after a fresh install (a log line when a cover shows its picture, with the time it took). Keep `tools/desktop-vm/checks/dino-reorder.ps1` passing.

**Verification.**
- [x] `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` green (7280 passed).
- [x] Emulator, fresh install: covers are sharp on the first open; in edit mode a plain swipe moves a book; the held book wiggles; Done saves and Cancel restores. 2026-11-15, build 22e6b62ec3cb: PASS `shelf-reorder` (267 s); logcat markers: drag start 1, drag wiggle running=True 1, drag end drop 1, tap lift 1, `[Cover] shown` 32 (first-open times 192-492 ms, waited=False). Logs: `D:/ReadTheStupidText-worktrees/reviews/wave6-device/s382-shelf-reorder-3.log` and `s382-shelf-reorder-3.logcat.log`.
- S21: the same.
- iOS: compile on Windows; simulator workflow or TestFlight for the same checks.
- Windows VM: `dino-reorder.ps1` passes.

**From review:** (`reviews/s382-high.md`)
- Applied #1, #2, #3, #4, #5, #6, #7, #8, #9, #11: Android touch handler sets Handled only while a drag runs and the flow taps a cover and expects `[Reorder] tap lift`; the pan is attached once per native view and removed on disconnect (no iOS retain cycle); a Down ends a stranded drag as cancelled and the tracked pointer is followed; only the 50 ms timer scrolls at the edge; Done and Enter before the first layout return the opening order, never an empty list; the cover pictures are loaded again when the tile width changes by 16 dp or more; the Windows drop no longer restarts a running wiggle; the cover spinner timer stops when the cover leaves the tree and "shown" is logged only with a picture source; iOS: the scroll view's pan waits for the cover's pan to fail, and a Failed pan ends nothing.
- #11: the cover spinner has the accessible name `Cover_Waiting` ("Waiting for the cover picture", 15 languages, typed accessor added in `Resources/Strings/AppResources.cs`, one line outside the Touches list). No visible text label: a per-tile label would not fit on a cover; no Cancel, since a picture decode cannot be stopped usefully.
- Skipped #10 (`reviews/s382-high.md`): "LogShown is guarded twice, by [Conditional("DEBUG")] and by #if DEBUG inside; Release compiles both away." - low, harmless double guard (the inner `#if DEBUG` is needed so Release still builds without the test-hook log type).
- Not yet verified (#9): the iOS pan (scroll view waits for the cover's pan, no 10 pt scroll before the drag, tap on a cover still lifts) has no scripted check on this Windows host; run the shelf-reorder flow on an iOS simulator or TestFlight build.
- Not yet verified (#1): the `[Reorder] tap lift` line on Android is scripted but not yet run on an emulator or the S21.
- From `reviews/s382-medium.md`: applied #1, #2, #4, #5, #7: the flow puts the swiped book back (hook 2 to 4) before the 4-to-2 hook; the Android pan sends a synthetic Cancel to the cover's other touch listeners when the drag begins; one rule for a drag in flight, `ClearEntries` ends it as cancelled (nothing applied, `ShelfDragSession` reset, `_dragged` cleared) and `PanEnd` takes the entry so a torn-down pan cannot end another cover's drag; `_pending` is cleared when the layer is hidden before the scroll got a width.
- From `reviews/s382-check.md`: applied #1, #2, #3, #4 as one design (see Work item 1, Android design): the synthetic Cancel is gone, so #1 and #2 have nothing left to fix; #3 uses `GetRawX/GetRawY(index)` on API 29+; #4 `ClearEntries` no longer resets `ShelfDragSession`.
- Medium #2 (`reviews/s382-medium.md`) is now met by the touch-slop rule (drag threshold at least the platform's touch slop, so Android's GestureDetector no longer reports a tap) instead of the synthetic Cancel.
- Skipped #3 (`reviews/s382-medium.md`): "on a touch-pan drop, SetLifted(false) runs Place(animate) then the code aborts and restarts the animation." - low, a 150 ms ZIndex flicker with no failure path.
- Skipped #6 (`reviews/s382-medium.md`): "ReloadPicture clears and restores BindingContext." - low, a blink on rotation only, no failure path.
- Skipped #8 (`reviews/s382-medium.md`): "LogShown has both" - nit, same double guard as skipped high #10.
- Skipped #9 (`reviews/s382-medium.md`): "AndroidPan sets native.Clickable and subscribes Touch, which can replace MAUI's own touch listener." - unconfirmed; settled by the device run's `tap lift` step, not by a code change now.
- From `reviews/s382-check2.md`: applied #1 (iOS pan keeps the scroll view it switched off at Began and switches that one back), #3 (`PanBegin` returns whether it accepted; `_dragging` and Handled only then, a refused begin leaves the touch unhandled), #5 (comment corrected), #6 (`ClearEntries` also hides the lit spot, still no `ShelfDragSession` reset).
- Skipped #2 (`reviews/s382-check2.md`): "Android: after a drag starts every later event incl. Up is Handled, and the synthetic Cancel is gone, so the Clickable cover keeps its pressed state and pending tap callbacks." - `Clickable` is set on a plain platform view (`ShelfReorderTiles.cs:236`) with no state-list background or ripple on the cover (no selector in ShelfReorderTiles.cs, StoryCover.xaml), so there is no pressed visual to stay on; the tap callbacks are stopped by the touch slop rule.
- Skipped #4 (`reviews/s382-check2.md`): "API<29 Raw() mixes view-local GetX(index) (rotated/scaled cover space) with screen RawX, so with a second finger or wiggle/lift scale the dx/dy drift a few dp." - low, API below 29 with several fingers only.
- Skipped #7 (`reviews/s382-check2.md`): "ReloadPicturesIfResized dispatches ReloadCover for every entry even during a drag or after a second Relayout replaced _entries; the dragged cover flashes under the finger." - low, a reload during a drag reloads the image only.
- Not yet verified (#9, medium pass): on Android a tap on a cover still lifts it with the pan attached (`[Reorder] tap lift` in the flow), and the synthetic Cancel at drag start leaves no stray tap. (The emulator run of 2026-11-15 logged `tap lift` once, so the tap-lift part is met on the emulator; the synthetic Cancel is gone, see Work item 1.)
- From `reviews/s382-check3.md` (findings 1/1 read): applied #1 (iOS pan `Changed` is handled only when `_began`, so a refused second finger no longer moves the dragged cover).
- From `reviews/s382-check4.md` (findings 3/3 read): none applied.
- Skipped #1 (`reviews/s382-check4.md`): "cover snaps up to ~5-10 dp away from the finger at drag start." - low, the snap comes from the lift scale and is cosmetic.
- Skipped #2 (`reviews/s382-check4.md`): "so a cover lifted by tap (tap-then-arrows) is un-lifted when another cover is touch-dragged, and not restored." - a new drag takes over the lift from a tap-lifted cover; the lift is a selection and no order data is lost; recorded as Not yet verified for a device look.
- Skipped #3 (`reviews/s382-check4.md`): "if the cover reattaches while IsLoading is still true, no property change fires, BeginWait never reruns." - low, a spinner missing on reattach mid-load is rare.
- From `reviews/s382-check5.md` (findings 0/0 read): no findings.

*Changed while building:*
- The emulator run found a `NullReferenceException` in `ShelfReorderView.ClearEntries`: the review #6 line read `_lit` before the first `Open` had made it (logcat `[Usage] fallback ui:tap_failed` after the Reorder tap). The coordinator guarded it with a null check (`ShelfReorderView.cs:344`); check5 confirmed the guard and that the other members it reads are initialised or null-safe.
- The Maestro swipe used an unsupported `to:` key (parse failure). It is rewritten to `from:` + `direction: UP`, with a Cancel and reopen in the flow.

#### Verified on devices (2026-11-15)
- Emulator (build 22e6b62ec3cb): PASS `shelf-reorder`, see the Verification line above.
- Windows VM: `dino-reorder` did not reach this slice's code. Its prerequisite `dino-onboarding-keys` failed twice ("Enter on the story-languages step did not open the book picker (waited 30 s)"; trace "Dino keyboard: Enter not used (FirstRunPage, no target)"). Slice 382 changes no first-run code. Logs: `reviews/wave6-device/vm-reorder.log`, `vm-reorder2.log`.

#### For CLAUDE.md
(none from the brief)

#### Not yet verified
- Windows VM `dino-reorder` (mouse drag, keyboard path, the new hint): blocked by the `dino-onboarding-keys` first-run Enter failure, not yet checked on main.
- S21: the same checks as the emulator (covers sharp on first open, plain swipe drag, held book wiggles, Done saves, Cancel restores).
- iOS pan (simulator or TestFlight): the scroll view waits for the cover's pan, no scroll before the drag, a tap on a cover still lifts.
- The drag's drop slot: the Maestro flow only proves that a drag happens, not where the book lands.
- Tap-lift a cover, then drag another cover (check4 #2): the new drag takes over the lift; look at it on a device.
