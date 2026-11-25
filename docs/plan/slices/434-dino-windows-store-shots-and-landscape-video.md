### Slice 434 — Dino: Microsoft Store desktop screenshots at exactly 1920 x 1080 and the landscape video (YouTube for Play, Microsoft Store trailer)
**Product:** Dino

*Plan-cleanup 2026-11-25 (verdict: close): items 1-2 are built and merged (PR #1341 MERGED 2026-11-24T21:02Z; eight 1920 x 1080 PNGs on `origin/main` in `docs/store-listings/screenshots/dino/windows/`; HyperFrames project `tools/dino/promo/store-landscape/` on main). Items 3-5 only wait on other slices (433: still planned, draft PR #1342, so `store-size-check.ps1` and `tools/dino/promo/store/` are not on main; 435: planned, no upload script), so they moved to a new slice that starts after them. Slice 404 item 5 stays unticked until that slice pushes the set (still `[ ]` on origin/main).*

*Planned 2026-11-24 from the phone doc [2026-11-23-dino-store-screenshots-and-video.md](../notes/2026-11-23-dino-store-screenshots-and-video.md), milestone 3 and the landscape part of milestone 4; split from Slice 433 (user, 2026-11-24). Follow every line of that note's Constraints, Decisions (approved), Sizes and Video timeline; this file wins where they differ.*

*Built 2026-11-24 (items 1 and 2: shots made on the VM, run 13 PASS); items 3-5 open; merged as PR #1341 (8503a816).*

**Premise check (2026-11-24, before the claim):** the `full-shelf` hook (`src/Dino/DinoReader.App/Testing/DinoTestCommands.FullShelf.cs:1`) and every `DinoTestCommands.*.cs` compile only under `#if DEBUG`; `DINO_TEST_BOOKS` (`DinoReader.App.csproj:162-165`, `tools/desktop-vm/Build-TestPackage.ps1:52`) compiles in only the 4 test books. So item 1 widens the hook's condition to `DEBUG || DINO_TEST_BOOKS` (and whatever of `Testing/` it needs), which gives the 12 books in the Release test package; the `DINO_TEST_BOOKS` route alone is not enough. `Set-ScreenResolution` exists (`tools/desktop-vm/checks/_lib.ps1:394-399`); there is no display-scale (DPI) setter and no window-sizing code, and whether the VM's display adapter offers a mode at or above 1920 x 1080 is unproven: item 1 proves it first. `store-size-check.ps1` is Slice 433's (plan-not-needed: 433); this slice merges `origin/main` after 433 merges and reuses it. Slice 435's upload script is not built (plan-not-needed: 435): the YouTube upload and the Play video URL in items 3-4 wait for it and go to *Not yet verified* if it has not merged when this slice ships.

**Touches:** `tools/desktop-vm/checks/dino-store-shots.ps1` (new), `tools/desktop-vm/checks/_lib.ps1`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/desktop-vm/CLAUDE.md`, `src/Dino/DinoReader.App/Testing/**`, `src/Dino/DinoReader.App/Platforms/Windows/**`, `tools/dino/promo/store-landscape/**` (new), `docs/store-listings/screenshots/dino/windows/**` (new), `docs/store-listings/dino/**`, `src/Dino/DinoReader.App/DinoReader.App.csproj`, `tools/desktop-vm/Build-TestPackage.ps1`, `tools/desktop-vm/_dryrun.ps1`, `.github/scripts/fetch-shelf-packages.mjs`, `.github/scripts/fetch-shelf-packages.test.mjs` (added 2026-11-24 for the offline VM shelf, `--include-free`; Slice 433 lists them but does not edit them), `docs/plan/slices/434-dino-windows-store-shots-and-landscape-video.md`. Records in `docs/plan/slices/291-store-listings-and-screenshots.md` and ticks `docs/plan/slices/404-dino-store-pictures-foldables-and-retake.md` item 5 (record lines only).

**Replaces:** Slice 404 item 5 (Microsoft Store desktop shots from the VM with the full shelf), which found no Dino store-shot check on the VM and no `full-shelf` hook in a Release package (404 line 119).

## Why

The Microsoft Store set must be landscape 1920 x 1080 with no text in the image (captions go in Partner Center's caption field). The note forbids a full-screen grab of a high-resolution monitor: the app window itself is sized so the capture is exactly 1920 x 1080 with the picture and text filling it (user, 2026-11-23). The same window setup films the landscape video (note: *Video orientation*). The VM screen today is 1024 x 768 (`tools/desktop-vm/CLAUDE.md` line 11), so the check must set the guest's resolution and display scale itself.

## Decisions (settled, never re-ask)

- The note's *Windows capture size* and *Video orientation* decisions (user, 2026-11-23).
- A plan of its own, so the other stores don't wait (user, 2026-11-24).
- The video's storyboard, captions and HyperFrames setup are Slice 433's (`tools/dino/promo/store/`); this slice films the Windows footage and renders the 1920 x 1080 cut from it.

## Work

- [x] 1. **First step:** a VM check `dino-store-shots` that sets the guest display to a resolution and scale where the Dino window can be sized to exactly 1920 x 1080 client area, puts the 12 books on the shelf in the Release test package (a Release-reachable form of 404's `full-shelf` hook, or the `DINO_TEST_BOOKS` route, whichever works in Release), captures the app window only, and prints its size; a contact sheet of one shot written into this slice (picture and text fill the frame, no big empty margins: the note's acceptance line).
- [x] 2. (Shots made; the size check with `store-size-check.ps1` waits for Slice 433 to merge, and the captions in Partner Center are item 4.) The six screens (the note's list) at 1920 x 1080 PNG, no text in the image; the six captions in each listing language in Partner Center's caption field (up to 200 characters); checked with Slice 433's `store-size-check.ps1` (if 433 has not merged, this slice adds that script and 433 reuses it).
- [ ] 3. (MOVED 2026-11-25 to the new slice "Microsoft Store trailer, Partner Center push and Play video link", starts after 433 and 435.) Landscape footage from the same window for the note's timeline; render the 1920 x 1080 cut with Slice 433's storyboard: H.264/AAC 48 kHz MP4 plus a 1920 x 1080 PNG thumbnail for the Microsoft Store trailer; the same file uploaded to YouTube (unlisted or public, ads off; through Slice 435's upload script once it exists) and its link set as the Play preview video.
- [ ] 4. (MOVED, same slice.) Push the Microsoft Store screenshots and trailer (Partner Center; through the `store-listing` agent) and read them back; set the Play video URL and read it back.
- [ ] 5. (MOVED, same slice.) Record per store in Slice 291's file; tick Slice 404 item 5 as replaced.

Rules on the way: `tools/desktop-vm/CLAUDE.md` (dry-run limits on width/height constants unless the check sets a resolution, `Find-UiaExact`, no fixed sleeps, `# after:` prerequisites, English UI), `src/Dino/DinoReader.App/Platforms/Windows/CLAUDE.md`. Windows-only work: no Android or iOS twin needed (the phones are Slice 433).

## Verification

- `Invoke-DesktopTest.ps1 -Check dino-store-shots` passes on the VM and every output is exactly 1920 x 1080.
- Partner Center listing and the Play video field read back through their APIs; the live Microsoft Store page shows the new set and trailer (Chrome, by the run that pushed them).

**Device result:** VM runs 1-13 (logs in `D:/ReadTheStupidText-worktrees/reviews/s434-device/`). Run 13 PASS: eight PNGs exactly 1920 x 1080 (01 page, 02 reading, 03 record, 04 en/es/ja, 05 shelf, 06 Store with the subscription card, covers, ages and the paid books "On the shelf") and five clips. The shots are committed in `docs/store-listings/screenshots/dino/windows/`.

**Changed while building:**

- Release-reachable test hooks under `DEBUG || DINO_TEST_BOOKS` (full shelf offline, full screen plus 1.5x zoom, story language, open book), with a request-file watcher in `src/Dino/DinoReader.App/Platforms/Windows/FullShelfRequest.cs`.
- VM check `dino-store-shots`: offline, follows `dino-onboarding-skip`, sets the guest display to 1920 x 1080 with `ChangeDisplaySettings`, full-screen presenter, blank-shot guard, focus clearing, Start retry on the Record intro. `Invoke-DesktopTest.ps1 -WithShelfPackages`; `fetch-shelf-packages.mjs --include-free` and `shelf-listing.json` (ages, access, productId); `TestStore` covers from packages and every staged book; `FakePurchases` in the offline hook with the real plan prices (£1.99 / £14.99, Slice 384); clips encoded to mp4 on the host one at a time; HyperFrames project `tools/dino/promo/store-landscape/`.
- Decisions changed at the desk: the VM stays offline (analytics isolation); the VM's maximum mode is 1920 x 1200 (Get-VMVideo Default), so a framed 1920 x 1080 window is impossible: the app runs full screen at 1920 x 1080 with a test-only 1.5x layout zoom (the 150% laptop look; no undocumented DPI call); books are opened by id through a hook, not through shelf tiles.

#### Not yet verified

- `[session]` Render the 1920 x 1080 landscape cut and push the Microsoft Store screenshots, captions and trailer, then read them back, after Slice 433 merges (its storyboard in `tools/dino/promo/store/`).
- `[session]` The YouTube upload and the Play video URL wait for Slice 435's upload script.
- `[session]` The dino-onboarding mic step failed once on the VM 2026-11-24 (run2.log: "FAIL R on the voice step started no recording and showed no microphone-off state (waited 15 s)"); recheck on main. The store-shots check follows `dino-onboarding-skip` and no longer runs that step.
- `[session]` Run `store-size-check.ps1` on the eight Windows PNGs once Slice 433 merges.

#### For CLAUDE.md (lifted 2026-11-24)

For `tools/desktop-vm/CLAUDE.md`:

- The Hyper-V VM display maxes out at 1920 x 1200 (Default), so a framed 1920 x 1080 window is impossible: store shots use full screen at 1920 x 1080 plus the test zoom hook. (Slice 434: docs/plan/slices/434-dino-windows-store-shots-and-landscape-video.md)
- `ReadingSurface` is not exposed to UIA; wait on the visible title instead. (Slice 434: docs/plan/slices/434-dino-windows-store-shots-and-landscape-video.md)

#### Follow-ups

The six nits of `reviews/s434-final.md` (see *From review*): keep `FaultSummary` under `#if DEBUG`; reset `ForcedOfferedLanguages` on reset; delete dead `SizeClient`/`Restore` in `_lib.ps1`; merge the doubled summary on `RunFullShelfAsync`; make `CoverUrlOfAsync` skip an unchanged write and `ShelfEntryOf` survive a malformed file; fix the comment naming `ShelfListingOf` in `fetch-shelf-packages.mjs`.

#### From review

- Applied #1 (`reviews/s434-high.md`): "Shot 04 can show three different books" - `Open-FirstBook` in `tools/desktop-vm/checks/dino-store-shots.ps1` reopens the same book in en, es and ja.
- Applied #2 (`reviews/s434-high.md`): "A missing ffmpeg is found only at the end of the run" - ffmpeg checked up front and a failed clip no longer drops the rest, in `tools/desktop-vm/Invoke-DesktopTest.ps1` (`Copy-FootageClipToMp4`).
- Applied #3 (`reviews/s434-high.md`): "The request watcher dies silently on the first non-IOException" - loop body guarded, keeps polling, in `src/Dino/DinoReader.App/Platforms/Windows/FullShelfRequest.cs`.
- Applied #4 (`reviews/s434-high.md`): "The full-shelf hook runs on a thread-pool thread" - sent to the main thread, in `FullShelfRequest.cs`.
- Applied #5 (`reviews/s434-high.md`): "A race can read the request file after it is created" - fixed in `FullShelfRequest.cs` (`TakeRequest`).
- Applied #6 (`reviews/s434-high.md`): "The book id comes from `$f.Directory.Name` over a recursive search" - fixed in `Copy-ShelfPackagesToGuest`, `tools/desktop-vm/Invoke-DesktopTest.ps1`.
- Applied #8 (`reviews/s434-high.md`): "It runs before Set-LargeResolution, so it does not prove the scale" - `Assert-StoreScale` in `dino-store-shots.ps1`.
- Applied #9 (`reviews/s434-high.md`): "The file name 'story-language.request' is written inline" - named constant in `dino-store-shots.ps1`.
- Applied #1 (`reviews/s434-medium.md`): "Rule says footage frames go to -CopyOut" - sentence corrected in `tools/desktop-vm/CLAUDE.md`.
- Applied #3 (`reviews/s434-medium.md`): "Open-FirstBook silently falls back to tile 0" - now throws when nothing matches, in `dino-store-shots.ps1`.
- Applied #5 (`reviews/s434-medium.md`): "New hook lines found by counting lines in window-trace.log" - fixed in `dino-store-shots.ps1`.
- Applied #1 (`reviews/s434-check.md`): "offline branch relies on TestStore.Mode default Ok" - mode set explicitly in `src/Dino/DinoReader.App/Testing/DinoTestCommands.FullShelf.cs`.
- Applied #3 (`reviews/s434-check.md`): "--include-free key story-v<version>.dino for free books unconfirmed" - fixed in `.github/scripts/fetch-shelf-packages.mjs`.
- The six nits of the final check pass (file cited in the lines below) are all left as follow-ups.
- Skipped #7 (`reviews/s434-high.md`): "Per-frame overhead makes the footage choppy" - footage smoothness is judged on the first VM run; changing the capture loop before any run is speculative.

- Skipped #2 (`reviews/s434-medium.md`): "RunAsync deletes full-shelf.request via TakeRequest" - the store-shots check runs only the Release DinoTestBooks package from Build-TestPackage.ps1, never a Debug build.
- Skipped #4 (`reviews/s434-medium.md`): "Save-Clip fires scripted actions only inside the frame loop; slow captures make actions bunch up" - footage pacing is judged on the first VM run (same reason as high #7).
- Skipped #2 (`reviews/s434-check.md`): "Debug fault line lost FaultSummary format; keep it under #if DEBUG" - a Debug-only trace format, not on the store-shot path (the VM run uses the Release DinoTestBooks package).
- Skipped #4 (`reviews/s434-check.md`): "two consecutive <summary> blocks on RunFullShelfAsync; merge" - a doubled doc comment, no behaviour.
- Skipped #5 (`reviews/s434-check.md`): "ItemFromPackageAsync items carry no Sale/ages, so paid/owned/age state in shots differs from live shelf" - whether the offline shelf looks like the live one is judged on the VM run's contact sheet.
- Skipped #1 (`reviews/s434-final.md`): "Debug fault line changed from TestHookLog.FaultSummary(ex) to type and message" - follow-up: check passes converged, no crash/data-loss/security finding.
- Skipped #2 (`reviews/s434-final.md`): "ForcedOfferedLanguages is process-wide and never reset" - follow-up: check passes converged, no crash/data-loss/security finding.
- Skipped #3 (`reviews/s434-final.md`): "NativeWindow.SizeClient and Restore have no callers" - follow-up: check passes converged, no crash/data-loss/security finding.
- Skipped #4 (`reviews/s434-final.md`): "RunFullShelfAsync carries two stacked summary blocks" - follow-up: check passes converged, no crash/data-loss/security finding.
- Skipped #5 (`reviews/s434-final.md`): "CoverUrlOfAsync rewrites the cover with File.Create" - follow-up: check passes converged, no crash/data-loss/security finding.
- Skipped #6 (`reviews/s434-final.md`): "Comment names TestStore.ShelfListingOf, which does not exist" - follow-up: check passes converged, no crash/data-loss/security finding.
