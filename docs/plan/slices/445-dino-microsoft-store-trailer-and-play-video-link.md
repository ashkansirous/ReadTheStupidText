# Slice 445 — Dino: Microsoft Store trailer, Partner Center push and Play video link
**Product:** Dino
**Touches:** `tools/dino/promo/store-landscape/**`, `docs/store-listings/screenshots/dino/windows/**`, `docs/store-listings/**`, `tools/emulator/store-size-check.ps1`, `tools/emulator/test-store-size-check.ps1`, `.github/workflows/tools-selftests.yml`, `docs/plan/notes/2026-11-23-dino-store-screenshots-and-video.md`

*Items 1 and 2 built 2026-11-28 (PR #1459, not merged). Items 3-5 not done: the coordinator runs them after the merge through the `store-listing` agent.*

**Design (coordinator, wave 2026-11-28):** Slice 433 is built and closed, so this slice can start. Item 1 first needs a Microsoft Store target in `store-size-check.ps1`. Microsoft's rules (https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images) are: desktop screenshots are PNG, landscape or portrait, "1366 x 768 pixels or larger", up to 3840 x 2160, at most 50 MB, up to 10 of them per listing language (the checker only warns above 10 per language and exits 0; it does not fail). The trailer is MP4 (H.264 High, AAC-LC 48 kHz) or MOV, exactly 1920 x 1080 and under 2 GB, 60 s or less recommended. The trailer thumbnail is a 1920 x 1080 PNG. So the Windows screenshot target is a range check (a minimum and a maximum per side, either orientation), not an exact size. It allows alpha: all eight PNGs are RGBA 1920 x 1080, and Microsoft does not forbid alpha. The trailer and its thumbnail are exact 1920 x 1080 targets. Add a self-test, `tools/emulator/test-store-size-check.ps1`, which runs in `tools-selftests.yml` like `test-store-screenshots.ps1`. Write the new rows into the Sizes table of the 2026-11-23 notes file too. Items 3 and 4 (the store pushes) run after the merge, through the `store-listing` agent in apply mode (the user's standing rule, 2026-11-15).

**Starts after:** Slice 433 merges (storyboard in `tools/dino/promo/store/`, `store-size-check.ps1`); the YouTube and Play-link part also after Slice 435's first real upload (its upload script merged in #1423, `tools/dino/promo/youtube/upload.mjs`).

Moved from Slice 434 items 3-5 (docs/plan/slices/434-dino-windows-store-shots-and-landscape-video.md). Own work: (1) run `store-size-check.ps1` on the eight Windows PNGs in `docs/store-listings/screenshots/dino/windows/`; (2) render the 1920 x 1080 H.264/AAC 48 kHz MP4 and 1920 x 1080 PNG thumbnail from `tools/dino/promo/store-landscape/` with 433's storyboard; (3) push the Microsoft Store screenshots, six captions per listing language (up to 200 characters) and the trailer through the `store-listing` agent and read them back; (4) upload the same MP4 to YouTube through 435's script and set the Play preview video URL, read back; (5) record per store in Slice 291's file and tick Slice 404 item 5.

Verification: the live Microsoft Store page shows the set and trailer; the Play video field reads back.

#### Status of the items (2026-11-28)

- [x] 1. `store-size-check.ps1` now has three Windows targets: `windows-screenshot` (range 1366 x 768 to 3840 x 2160, either orientation, PNG, 50 MB; more than 10 files per language is a warning with exit 0), `windows-thumbnail` (exactly 1920 x 1080 PNG) and `windows-trailer` (exactly 1920 x 1080, H.264 High, AAC-LC 48 kHz, under 2 GB; over 60 s is a warning). New self-test `tools/emulator/test-store-size-check.ps1`, run in `tools-selftests.yml`. All eight Windows PNGs pass. The Sizes table in the 2026-11-23 notes file has the new rows.
- [x] 2. A 20 s landscape trailer rendered from `tools/dino/promo/store-landscape/` (433's storyboard beats) with footage from Slice 434's run13 and a silent AAC track added. Files are in the git-ignored `tools/dino/promo/store-landscape/renders/` of the s445 worktree: `dino-microsoft-store-trailer.mp4`, `-thumbnail.png` and the frame at 4.5 s. The record shot's caption moved so it covers no control.
- [ ] 3. Push screenshots, captions, trailer and thumbnail to the Microsoft Store and read them back: after the merge, through the `store-listing` agent.
- [ ] 4. YouTube upload and Play preview URL read back: after the merge.
- [ ] 5. Record per store in Slice 291's file and tick Slice 404 item 5: after 3 and 4.

#### For CLAUDE.md

None for this pass.

#### Not yet verified

- [session] Push the Microsoft Store screenshots, captions, trailer and thumbnail, then read them back (item 3).
- [session] YouTube upload through 435's script and the Play preview video URL read back (item 4).

#### Follow-ups

- The 2026-11-27 live check (no Windows target in `store-size-check.ps1`) is resolved by item 1 above.

#### From review

- `reviews/pr-1459-high.md`: nine findings, eight fixed (its Outcome section lists 1-7 and 9 as fixed); the eighth finding is the one recorded below.
- Skipped #1 (`reviews/pr-1459-medium.md`): "a later session reads it as blocked" - this record does it: the status block above ticks both built items with their results.
- Skipped #2 (`reviews/pr-1459-medium.md`): "the checker only warns above 10 files and exits 0." - the Design paragraph is updated in this pass to say more than 10 only warns.
- Skipped #3 (`reviews/pr-1459-medium.md`): "Thumbnail cut at -ss 4.5 while the note says the poster lands on the voices shot at about 5 s" - the poster frame at 4.5 s is within the note's "about 5 s".
- Skipped #4 (`reviews/pr-1459-medium.md`): "A clean trailer is probed twice" - a second ffprobe run on a 20 s file costs under a second, and `$ErrorActionPreference` set inside a PowerShell function is scoped to that function, so nothing leaks.
- Skipped #8 (`reviews/pr-1459-high.md`): "Get-TrailerProblems does not check pix_fmt (yuv420p) or frame rate; Get-VideoProbe does not request pix_fmt." - Microsoft's trailer rules quoted in the Design name no pixel format or frame rate, so checking them would add a rule the store does not have.
- Review #4/#5 (`reviews/pr-1459-high.md`): the landscape film follows the note's Video timeline, the same six beats, times and captions as 433's storyboard (`tools/dino/promo/store/index.html`), so it is the storyboard in landscape, not a placeholder; the README now says so. The render and trailer were ffprobed (H.264 High, yuv420p, 30 fps) and the README gives a re-encode fallback.
