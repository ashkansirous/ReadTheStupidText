# Slice 445 — Dino: Microsoft Store trailer, Partner Center push and Play video link
**Product:** Dino
**Touches:** `tools/dino/promo/store-landscape/**`, `docs/store-listings/screenshots/dino/windows/**`, `docs/store-listings/**`, `tools/emulator/store-size-check.ps1`, `tools/emulator/test-store-size-check.ps1`, `.github/workflows/tools-selftests.yml`, `docs/plan/notes/2026-11-23-dino-store-screenshots-and-video.md`

*In progress (wave 2026-11-28), draft PR.*

**Design (coordinator, wave 2026-11-28):** Slice 433 is built and closed, so this slice can start. Item 1 first needs a Microsoft Store target in `store-size-check.ps1`. Microsoft's rules (https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images) are: desktop screenshots are PNG, landscape or portrait, "1366 x 768 pixels or larger", up to 3840 x 2160, at most 50 MB, up to 10 of them. The trailer is MP4 (H.264 High, AAC-LC 48 kHz) or MOV, exactly 1920 x 1080 and under 2 GB, 60 s or less recommended. The trailer thumbnail is a 1920 x 1080 PNG. So the Windows screenshot target is a range check (a minimum and a maximum per side, either orientation), not an exact size. It allows alpha: all eight PNGs are RGBA 1920 x 1080, and Microsoft does not forbid alpha. The trailer and its thumbnail are exact 1920 x 1080 targets. Add a self-test, `tools/emulator/test-store-size-check.ps1`, which runs in `tools-selftests.yml` like `test-store-screenshots.ps1`. Write the new rows into the Sizes table of the 2026-11-23 notes file too. Items 3 and 4 (the store pushes) run after the merge, through the `store-listing` agent in apply mode (the user's standing rule, 2026-11-15).

**Starts after:** Slice 433 merges (storyboard in `tools/dino/promo/store/`, `store-size-check.ps1`); the YouTube and Play-link part also after Slice 435's first real upload (its upload script merged in #1423, `tools/dino/promo/youtube/upload.mjs`).

Moved from Slice 434 items 3-5 (docs/plan/slices/434-dino-windows-store-shots-and-landscape-video.md). Own work: (1) run `store-size-check.ps1` on the eight Windows PNGs in `docs/store-listings/screenshots/dino/windows/`; (2) render the 1920 x 1080 H.264/AAC 48 kHz MP4 and 1920 x 1080 PNG thumbnail from `tools/dino/promo/store-landscape/` with 433's storyboard; (3) push the Microsoft Store screenshots, six captions per listing language (up to 200 characters) and the trailer through the `store-listing` agent and read them back; (4) upload the same MP4 to YouTube through 435's script and set the Play preview video URL, read back; (5) record per store in Slice 291's file and tick Slice 404 item 5.

Verification: the live Microsoft Store page shows the set and trailer; the Play video field reads back.

#### Not yet verified

- [session] Render the 1920 x 1080 landscape cut and push the Microsoft Store screenshots, captions and trailer, then read them back, after Slice 433 merges (its storyboard in `tools/dino/promo/store/`) (moved from Slice 434's block).
- [session] The YouTube upload and the Play video URL wait for Slice 435's first real upload; its upload script merged in #1423 (moved from Slice 434's block).
- [session] Run `store-size-check.ps1` on the eight Windows PNGs once Slice 433 merges (moved from Slice 434's block).

#### Follow-ups

- Live check 2026-11-27 (after wave 4): item 1 cannot run as written: `tools/emulator/store-size-check.ps1` on origin/main 8ec0cb56 knows only the Play, App Store and preview targets (`play-phone` ... `preview-ipad`), no Microsoft Store / Windows target, so the eight PNGs in `docs/store-listings/screenshots/dino/windows/` have no size row to check against. Item 1 needs a Windows target (Microsoft Store screenshot size, read from Microsoft's own help page) added to the script and its Sizes note, through this slice's reviewed build, before the check runs.
