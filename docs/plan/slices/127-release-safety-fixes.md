# Slice 127 — Release safety: Dino iOS .ipa gap and the Store id fallback

*In progress (wave 2026-10-25), draft PR.*

**Batch:** 27 (product separation, Decisions 133-137)
**Touches:** `.github/workflows/release.yml`, `.github/workflows/store-submit.yml`, `.github/workflows/dinoreader-ios.yml`, `.github/CLAUDE.md`

## Why

The architecture check of 2026-10-25 (note: [2026-10-25-product-separation](../notes/2026-10-25-product-separation.md))
found two small release problems:

1. Release run for `43e2ef1` (v1.42.0) failed in job `testflight-submit-dino`, step "Download the .ipa
   from the release": *"Release v1.42.0 was published as signed Dino Reader iOS but carries no
   DinoReader-1.42.0.ipa; nothing was sent to TestFlight."* The user got "Run failed" emails for it.
   Something marked the release as signed Dino iOS without the `.ipa` being attached (the draft check at
   `release.yml` ~509 should have stopped this).
2. `store-submit.yml` falls back to `vars.STORE_PRODUCT_ID` (Read The Stupid Text) whenever
   `product_id` is empty (`store-submit.yml` ~198). Today `store-submit-dino` only runs when
   `DINOREADER_STORE_APP_ID` is set, but one wrong edit would submit a Dino package to the RTST
   listing.

## Work

- [ ] Find why v1.42.0 was flagged as signed Dino iOS with no `DinoReader-<v>.ipa` (`gh run view` on the
      43e2ef1 run: the iOS build job, the draft check, the flag that `testflight-submit-dino` reads).
      Fix the cause so the flag and the asset can't disagree; if the build legitimately produced no
      `.ipa`, the TestFlight job skips with a notice instead of failing.
- [ ] `store-submit.yml`: make `product_id` required, with no fallback to `STORE_PRODUCT_ID`. RTST's
      caller passes `vars.STORE_PRODUCT_ID` explicitly, Dino's passes `vars.DINOREADER_STORE_APP_ID`.
      Add a guard step that fails when `package` is `DinoReader.App` and the id equals
      `vars.STORE_PRODUCT_ID` (and the reverse).
- [ ] One line in `.github/CLAUDE.md` for each rule.

## Verification

- `actionlint` clean.
- A `workflow_dispatch` dry run (or the next release) shows `store-submit` receiving the RTST id
  explicitly and `store-submit-dino` still skipped while `DINOREADER_STORE_APP_ID` is unset.
- The 43e2ef1 root cause is written in this file with the run id.
