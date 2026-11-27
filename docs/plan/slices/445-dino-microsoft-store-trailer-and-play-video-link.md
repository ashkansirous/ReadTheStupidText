# Slice 445 — Dino: Microsoft Store trailer, Partner Center push and Play video link
**Product:** Dino
**Touches:** `tools/dino/promo/store-landscape/**`, `docs/store-listings/screenshots/dino/windows/**`, `docs/store-listings/**`

*Planned 2026-11-25 (plan-cleanup of that day; not built).*

**Starts after:** Slice 433 merges (storyboard in `tools/dino/promo/store/`, `store-size-check.ps1`); the YouTube and Play-link part also after Slice 435's upload script.

Moved from Slice 434 items 3-5 (docs/plan/slices/434-dino-windows-store-shots-and-landscape-video.md). Own work: (1) run `store-size-check.ps1` on the eight Windows PNGs in `docs/store-listings/screenshots/dino/windows/`; (2) render the 1920 x 1080 H.264/AAC 48 kHz MP4 and 1920 x 1080 PNG thumbnail from `tools/dino/promo/store-landscape/` with 433's storyboard; (3) push the Microsoft Store screenshots, six captions per listing language (up to 200 characters) and the trailer through the `store-listing` agent and read them back; (4) upload the same MP4 to YouTube through 435's script and set the Play preview video URL, read back; (5) record per store in Slice 291's file and tick Slice 404 item 5.

Verification: the live Microsoft Store page shows the set and trailer; the Play video field reads back.

#### Not yet verified

- [session] Render the 1920 x 1080 landscape cut and push the Microsoft Store screenshots, captions and trailer, then read them back, after Slice 433 merges (its storyboard in `tools/dino/promo/store/`) (moved from Slice 434's block).
- [session] The YouTube upload and the Play video URL wait for Slice 435's upload script (moved from Slice 434's block).
- [session] Run `store-size-check.ps1` on the eight Windows PNGs once Slice 433 merges (moved from Slice 434's block).
