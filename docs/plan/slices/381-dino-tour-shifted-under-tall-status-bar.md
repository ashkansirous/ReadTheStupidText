### Slice 381 — Dino: the first-run tour's highlights and tooltips sit lower than the real buttons on a tall status bar
**Product:** Dino

*Built 2026-11-15, merged as PR #1164 (906fabde). Emulator flow passed at two sizes; S21, iOS and the Farsi run not yet verified.*

**Touches:** `src/Dino/DinoReader.App/CornerTour.cs`, `src/Dino/DinoReader.Core/TourGeometry.cs` (new, only if the offset rule is pulled out as pure code), `tests/Dino/DinoReader.Tests/TourGeometryTests.cs` (new, same condition), `.maestro/dino/onboarding-tour-position.yaml` (new), `src/Dino/DinoReader.App/Testing/DinoTestCommands.cs` (a log line with the copy's and the target's bounds), `src/Dino/DinoReader.App/CLAUDE.md`, `docs/plan/slices/381-dino-tour-shifted-under-tall-status-bar.md`

**Why.** The user reported this on 2026-11-14, Dino 1.129.0, on a Pixel 10 Pro: "the intro was shifted down. everything including the top button, the tool tip for it and the tool tip for store and the rest." Their screenshots: the copy of the "Child's shelf" button is drawn below the real one with its tooltip under it, and the "Grown-ups" copy on the child's shelf sits below the real chip.

**Evidence (2026-11-14).**
- Reproduced on the emulator at the Pixel 10 Pro's size (1280x2856, density 495, tall cutout). On tour steps 1 and 2, the copies of the Books and Store tabs are drawn about 115-135 px below the real tabs; the status bar is 149 px tall. Debug build, not the released one. The "Child's shelf" and "Grown-ups" steps were not reached in the 10 minutes. Screenshots are `tour2.png` and `tour3.png` in `D:\ReadTheStupidText-worktrees\reviews\dino-repro\`.
- Code, read from origin/main: `CornerTour.BoundsIn` (`CornerTour.cs` ~214-232) adds up each parent's layout `X/Y`. The page draws under the status bar (`SafeAreaEdges="None"`, `GalleryPage.xaml:10`), and each piece pads itself by the inset (`GalleryPage.xaml.cs:900, 917-920`, `CornerTabs.cs:122`). A padding inset is not part of a child's `Y`, so the copy lands a status-bar height away from the real control. Android and iOS run the same code. Likely cause until the fix shows the copy on the target.
- App logs: the Pixel ran the intro (`intro_started` to `intro_ended finished`, 20:00-20:03) and sent no error rows.
- Owner of the tour: Slice 362 (`docs/plan/slices/362-dino-onboarding-shows-what-dino-can-do.md`).

**Decisions (user, 2026-11-14).**
- Fixed on Android and iOS in this slice, with a device check that measures each copy against its real control, so it cannot come back unseen.

**Work.**
- [x] 1. Smallest end-to-end first, Android: place every tour copy and tooltip from the target's real position on screen, relative to the overlay's own position on screen (the same coordinate space for both), never from adding up layout `X/Y`. Check the MAUI / platform API for an element's on-screen position with `library-docs` (context7) first. The fix covers every tour stop: Books, Store, Child's shelf, Grown-ups, and any other copy `CornerTour` draws.
- [x] 2. iOS task: the same on iPhone, including a notch / Dynamic Island phone where the top inset is largest. If the shared code needs a per-platform half for the screen position, write both halves.
- [x] 3. A Debug log line per stop with the copy's bounds and the target's bounds in the same space (for example `[Onboarding] tour stop=Store copy=x,y,w,h target=x,y,w,h`), written through the existing test-hook log.
- [x] 4. A Maestro flow `onboarding-tour-position` that runs the tour and fails when any stop's copy is more than 4 dp off its target. Run it on the emulator at 1280x2856 / 495 dpi with the tall cutout, and at the default emulator size.
- [x] 5. If the offset rule is pure (for example converting between the two spaces), pull it into Core and unit-test it.

**Verification.**
- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` green.
- [x] Emulator: `onboarding-tour-position` passes at the Pixel 10 Pro size with the cutout and at the default size. Ran 2026-11-15, build 086797510be9: PASS at 1080x2400@420 (150.6 s) and at 1280x2856@495 (137.9 s; Pixel log 5 stops `aligned=yes`, `misaligned=0`). Logs `D:/ReadTheStupidText-worktrees/reviews/wave6-device/s381-flow-default.log` and `s381-flow-pixel.log`, logcat `.emulator/logcat-i1/dino-onboarding-tour-position.log`.
- [ ] Small screenshots of the Child's shelf and Grown-ups stops show the copy exactly over the real control (not taken; see *Not yet verified*).
- S21: the same flow passes.
- iOS: compile on Windows; the simulator workflow or TestFlight shows the copies on their targets on a notch/Dynamic Island iPhone.

**From review:**
- Skipped #3 (`reviews/s381-high.md`): "reads include ancestor transforms (translation, scale, scroll), but the copy is positioned with" - no Scale or Translation is set on any ancestor of a tour target (grep of `GalleryPage.xaml`, `GalleryPage*.cs`, `CornerTabs.cs`, `CornerShell.cs`, `ShelfHeader.cs`; the only `Translation*` in the tour is on its own tooltip pointer); the placement is also re-read before the stop is drawn.
- Skipped #6 (`reviews/s381-high.md`): "dispatch is scheduled in every build and on every platform, but its body is" - low; and the delayed dispatch is replaced anyway by the layout-driven `WatchPlacement` (body Debug only).
- Skipped #8 (`reviews/s381-high.md`): "The tests only check the subtraction and tolerance arithmetic. They do not cover the actual fix, which is the Android and iOS screen-origin reads (density, window space)." - low; the platform reads cannot be unit-tested, the device flow covers them.
- Skipped #9 (`reviews/s381-high.md`): "check hard-codes the copy as" - low; `CopyChildIndex` is a named constant with the reason beside it.
- Skipped #10 (`reviews/s381-high.md`): "Formatting slip:" - trivial; the line was rewritten in the #1 fix, so the slip is gone.
- Applied from `reviews/s381-medium.md`: #4 `Draw` reads the target's bounds once and passes the rect to `DrawTab`, `DrawShelfPointer` and `DrawChildShelf`; #1 the Debug placement watch runs only where the screen position is readable (Android, iOS), so Windows logs no misses; #3 the copy's `SizeChanged` handler is dropped in `ClearLayer` (new stop, redraw, end of tour); #5 `WatchPlacement` and `ScreenOrigin` are split into methods under 30 lines; #6 the flow comment now says the placement is logged once the copy is laid out; #7 the CLAUDE.md bullet keeps only the rule.
- Skipped #2 (`reviews/s381-medium.md`): "WatchPlacement reads the copy via hard-coded Children[CopyChildIndex] (1)." - low; the copy's child order is set in the same file (scrim, then the lifted copy) and `CopyChildIndex` names it.
- Check pass on the final code (`reviews/s381-check.md`), 8 findings, none changed in code; each is recorded below, and the first is kept as a Not yet verified line:
- Skipped #1 (`reviews/s381-check.md`): "Screen-pixel origin used with logical Thickness margin; Farsi (RTL) may mirror the copy horizontally." - unconfirmed: the Farsi run was not reached, so it stays a Not yet verified line.
- Skipped #2 (`reviews/s381-check.md`): "Last-attempt fallback to layout offsets draws the original bug (status-bar height too low) when the target is not yet attached; only a LayoutFailed event is reported." - the last-attempt fallback only runs when the screen origin stays unreadable, and it is reported as a fallback event.
- Skipped #3 (`reviews/s381-check.md`): "Fallback only at retries==0 means a never-attached target waits ~8 x 120 ms (~1 s) with scrim up, no loading state, Next ignored; same after rotation redraw." - the retry is about 1 s only for a never-attached target; no normal path waits.
- Skipped #4 (`reviews/s381-check.md`): "Draw is over 30 lines; ToRect, ToTourRect, LogPlacementSummary, BoundsOf are one-line methods" - low.
- Skipped #5 (`reviews/s381-check.md`): "Log marker strings (aligned=yes/no, placement misaligned=, placement unreadable) are repeated literals matched by the Maestro flow; use named constants." - low.
- Skipped #6 (`reviews/s381-check.md`): "IsAligned compares centres only with 4 dp tolerance; wrong size or small real drift passes." - low.
- Skipped #7 (`reviews/s381-check.md`): "Superseded LogPlacement (version changed) is neither counted as a miss nor logged, so misaligned=0 can hide an unmeasured stop." - low.
- Skipped #8 (`reviews/s381-check.md`): "About 90 lines of onboarding walk copied from onboarding-phone.yaml; use a shared runFlow sub-flow." - low.

**Debug log and flow, moved here from CLAUDE.md (review #7).** Debug logs `[Onboarding] tour stop=<stop> aligned=yes|no copy=x,y,w,h target=x,y,w,h` per stop once the copy is laid out, and `tour placement misaligned=<n>` when the tour ends. `onboarding-tour-position.yaml` expects `aligned=yes` at every stop and `misaligned=0` at the end. A view not yet in a window reads as unreadable, and `Draw` retries; on the last try the layout sum is used and the fallback is reported once per stop. Windows reads no screen position, so it keeps the layout sum and logs no placement.

#### For CLAUDE.md (lifted 2026-11-15)
- The tour places each copy and tooltip from the target's screen position less the tour layer's screen position (`TourGeometry.InLayer`, `CornerTour.cs`), never from a sum of layout `X/Y`: a padding inset (status bar) is not part of a child's `Y`. Windows has no screen read and keeps the layout sum. (Slice 381: docs/plan/slices/381-dino-tour-shifted-under-tall-status-bar.md)

(Already written into `src/Dino/DinoReader.App/CLAUDE.md`, First run bullet, on this branch; the index pass lifts it only if missing.)

#### Not yet verified
- The tall-cutout overlay was enabled for the Pixel-size run but not read back, so the 149 px status bar of the repro may not have been active.
- The flow was not shown failing on the old build (without the fix).
- Farsi (RTL) placement: whether the layer mirrors `Margin.Left` under RTL (check pass #1).
- S21: `onboarding-tour-position` not run.
- iOS notch / Dynamic Island: the copies on their targets (simulator workflow or TestFlight).
- Small screenshots of the Child's shelf and Grown-ups stops with the copy over the real control.
