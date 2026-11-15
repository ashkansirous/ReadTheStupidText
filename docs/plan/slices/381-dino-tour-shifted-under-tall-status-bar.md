### Slice 381 — Dino: the first-run tour's highlights and tooltips sit lower than the real buttons on a tall status bar
**Product:** Dino

*In progress (wave 2026-11-15), draft PR.*

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
- [ ] 1. Smallest end-to-end first, Android: place every tour copy and tooltip from the target's real position on screen, relative to the overlay's own position on screen (the same coordinate space for both), never from adding up layout `X/Y`. Check the MAUI / platform API for an element's on-screen position with `library-docs` (context7) first. The fix covers every tour stop: Books, Store, Child's shelf, Grown-ups, and any other copy `CornerTour` draws.
- [ ] 2. iOS task: the same on iPhone, including a notch / Dynamic Island phone where the top inset is largest. If the shared code needs a per-platform half for the screen position, write both halves.
- [ ] 3. A Debug log line per stop with the copy's bounds and the target's bounds in the same space (for example `[Onboarding] tour stop=Store copy=x,y,w,h target=x,y,w,h`), written through the existing test-hook log.
- [ ] 4. A Maestro flow `onboarding-tour-position` that runs the tour and fails when any stop's copy is more than 4 dp off its target. Run it on the emulator at 1280x2856 / 495 dpi with the tall cutout, and at the default emulator size.
- [ ] 5. If the offset rule is pure (for example converting between the two spaces), pull it into Core and unit-test it.

**Verification.**
- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Emulator: `onboarding-tour-position` passes at the Pixel 10 Pro size with the cutout and at the default size. Small screenshots of the Child's shelf and Grown-ups stops show the copy exactly over the real control.
- S21: the same flow passes.
- iOS: compile on Windows; the simulator workflow or TestFlight shows the copies on their targets on a notch/Dynamic Island iPhone.
