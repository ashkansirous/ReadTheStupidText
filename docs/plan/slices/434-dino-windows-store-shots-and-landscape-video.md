### Slice 434 — Dino: Microsoft Store desktop screenshots at exactly 1920 x 1080 and the landscape video (YouTube for Play, Microsoft Store trailer)
**Product:** Dino

*Planned 2026-11-24 from the phone doc [2026-11-23-dino-store-screenshots-and-video.md](../notes/2026-11-23-dino-store-screenshots-and-video.md), milestone 3 and the landscape part of milestone 4; split from Slice 433 (user, 2026-11-24). Follow every line of that note's Constraints, Decisions (approved), Sizes and Video timeline; this file wins where they differ.*

*In progress (wave 2026-11-24), draft PR.*

**Premise check (2026-11-24, before the claim):** the `full-shelf` hook (`src/Dino/DinoReader.App/Testing/DinoTestCommands.FullShelf.cs:1`) and every `DinoTestCommands.*.cs` compile only under `#if DEBUG`; `DINO_TEST_BOOKS` (`DinoReader.App.csproj:162-165`, `tools/desktop-vm/Build-TestPackage.ps1:52`) compiles in only the 4 test books. So item 1 widens the hook's condition to `DEBUG || DINO_TEST_BOOKS` (and whatever of `Testing/` it needs), which gives the 12 books in the Release test package; the `DINO_TEST_BOOKS` route alone is not enough. `Set-ScreenResolution` exists (`tools/desktop-vm/checks/_lib.ps1:394-399`); there is no display-scale (DPI) setter and no window-sizing code, and whether the VM's display adapter offers a mode at or above 1920 x 1080 is unproven: item 1 proves it first. `store-size-check.ps1` is Slice 433's (plan-not-needed: 433); this slice merges `origin/main` after 433 merges and reuses it. Slice 435's upload script is not built (plan-not-needed: 435): the YouTube upload and the Play video URL in items 3-4 wait for it and go to *Not yet verified* if it has not merged when this slice ships.

**Touches:** `tools/desktop-vm/checks/dino-store-shots.ps1` (new), `tools/desktop-vm/checks/_lib.ps1`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/desktop-vm/CLAUDE.md`, `src/Dino/DinoReader.App/Testing/**`, `src/Dino/DinoReader.App/Platforms/Windows/**`, `tools/dino/promo/store-landscape/**` (new), `docs/store-listings/screenshots/dino/windows/**` (new), `docs/store-listings/dino/**`, `src/Dino/DinoReader.App/DinoReader.App.csproj`, `tools/desktop-vm/Build-TestPackage.ps1`, `tools/desktop-vm/_dryrun.ps1`, `docs/plan/slices/434-dino-windows-store-shots-and-landscape-video.md`. Records in `docs/plan/slices/291-store-listings-and-screenshots.md` and ticks `docs/plan/slices/404-dino-store-pictures-foldables-and-retake.md` item 5 (record lines only).

**Replaces:** Slice 404 item 5 (Microsoft Store desktop shots from the VM with the full shelf), which found no Dino store-shot check on the VM and no `full-shelf` hook in a Release package (404 line 119).

## Why

The Microsoft Store set must be landscape 1920 x 1080 with no text in the image (captions go in Partner Center's caption field). The note forbids a full-screen grab of a high-resolution monitor: the app window itself is sized so the capture is exactly 1920 x 1080 with the picture and text filling it (user, 2026-11-23). The same window setup films the landscape video (note: *Video orientation*). The VM screen today is 1024 x 768 (`tools/desktop-vm/CLAUDE.md` line 11), so the check must set the guest's resolution and display scale itself.

## Decisions (settled, never re-ask)

- The note's *Windows capture size* and *Video orientation* decisions (user, 2026-11-23).
- A plan of its own, so the other stores don't wait (user, 2026-11-24).
- The video's storyboard, captions and HyperFrames setup are Slice 433's (`tools/dino/promo/store/`); this slice films the Windows footage and renders the 1920 x 1080 cut from it.

## Work

- [ ] 1. **First step:** a VM check `dino-store-shots` that sets the guest display to a resolution and scale where the Dino window can be sized to exactly 1920 x 1080 client area, puts the 12 books on the shelf in the Release test package (a Release-reachable form of 404's `full-shelf` hook, or the `DINO_TEST_BOOKS` route, whichever works in Release), captures the app window only, and prints its size; a contact sheet of one shot written into this slice (picture and text fill the frame, no big empty margins: the note's acceptance line).
- [ ] 2. The six screens (the note's list) at 1920 x 1080 PNG, no text in the image; the six captions in each listing language in Partner Center's caption field (up to 200 characters); checked with Slice 433's `store-size-check.ps1` (if 433 has not merged, this slice adds that script and 433 reuses it).
- [ ] 3. Landscape footage from the same window for the note's timeline; render the 1920 x 1080 cut with Slice 433's storyboard: H.264/AAC 48 kHz MP4 plus a 1920 x 1080 PNG thumbnail for the Microsoft Store trailer; the same file uploaded to YouTube (unlisted or public, ads off; through Slice 435's upload script once it exists) and its link set as the Play preview video.
- [ ] 4. Push the Microsoft Store screenshots and trailer (Partner Center; through the `store-listing` agent) and read them back; set the Play video URL and read it back.
- [ ] 5. Record per store in Slice 291's file; tick Slice 404 item 5 as replaced.

Rules on the way: `tools/desktop-vm/CLAUDE.md` (dry-run limits on width/height constants unless the check sets a resolution, `Find-UiaExact`, no fixed sleeps, `# after:` prerequisites, English UI), `src/Dino/DinoReader.App/Platforms/Windows/CLAUDE.md`. Windows-only work: no Android or iOS twin needed (the phones are Slice 433).

## Verification

- `Invoke-DesktopTest.ps1 -Check dino-store-shots` passes on the VM and every output is exactly 1920 x 1080.
- Partner Center listing and the Play video field read back through their APIs; the live Microsoft Store page shows the new set and trailer (Chrome, by the run that pushed them).
