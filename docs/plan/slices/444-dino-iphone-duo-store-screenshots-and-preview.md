# Slice 444 — Dino: iPhone Duo store screenshot set and Duo preview
**Product:** Dino
**Touches:** `.maestro/dino/store-shot-ios-duo-*.yaml`, `.github/workflows/dinoreader-ios-sim.yml`, `docs/store-listings/screenshots/dino/**`

*Planned 2026-11-25 (plan-cleanup of that day; not built).*

**Starts after:** Slice 403 ships (foldable layouts).

Moved from Slice 433 item 8 and Slice 404 item 6. Duo screenshot sets (outer 1398 x 2034, inner 2007 x 2853, portrait) and a Duo preview (886 x 1920) on Apple's Duo simulator, new flows `.maestro/dino/store-shot-ios-duo-*.yaml`, in the new caption look from Slice 433. Read the Duo `screenshotDisplayType` from Apple's API reference or a live call, never guessed; simulator only, 12-book `full-shelf` hook, never build a book package (404 Decisions 1, 3, 5). Push through the `store-listing` agent when a Dino version is editable; read back; record in Slice 291.

#### Not yet verified

- [owner] **Device:** the iPhone Duo set, until Xcode has a Duo simulator and Slice 403 has shipped (re-homed from Slice 404 when it closed).
