# Slice 127 — Release safety: Dino iOS .ipa gap and the Store id fallback

*Built 2026-10-25 (PR #358), not yet verified: no workflow_dispatch dry run of `store-submit.yml` yet.*

**Changed while building:**

- Root cause of the v1.42.0 failure (run 40145694797): the `.ipa` WAS on the draft release. `testflight-submit-dino` failed 5 s in, at "Download the .ipa", so the "carries no .ipa" message was wrong. Probable cause (not proven): `gh release view | grep -q` under `pipefail` (grep exits early, SIGPIPE). Fixed by capturing the asset list into a variable, then grepping it. The same pattern was fixed in `check-android-natives.sh` and `packs.yml`.
- Deviation: no skip-with-notice path was added. `release-publish` already hard-fails a missing signed Dino `.ipa`, so a skip in the TestFlight job would only hide it.
- `store-submit.yml`: `product_id` is required with no `STORE_PRODUCT_ID` fallback. A guard per package fails when RTST's id differs from `vars.STORE_PRODUCT_ID` or Dino's differs from `vars.DINOREADER_STORE_APP_ID`. The `package` input is now a `choice`. `release.yml` passes `product_id` explicitly. The retry note in `STORE.md` is updated.

#### Not yet verified

- `workflow_dispatch` dry run of `store-submit.yml` with each package.
- `actionlint` (no local copy; it runs first in CI).
- That the SIGPIPE theory is the real cause of the v1.42.0 failure (next release will show).

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

- [x] Find why v1.42.0 was flagged as signed Dino iOS with no `DinoReader-<v>.ipa` (`gh run view` on the
      43e2ef1 run: the iOS build job, the draft check, the flag that `testflight-submit-dino` reads).
      Fix the cause so the flag and the asset can't disagree; if the build legitimately produced no
      `.ipa`, the TestFlight job skips with a notice instead of failing.
- [x] `store-submit.yml`: make `product_id` required, with no fallback to `STORE_PRODUCT_ID`. RTST's
      caller passes `vars.STORE_PRODUCT_ID` explicitly, Dino's passes `vars.DINOREADER_STORE_APP_ID`.
      Add a guard step that fails when `package` is `DinoReader.App` and the id equals
      `vars.STORE_PRODUCT_ID` (and the reverse).
- [ ] One line in `.github/CLAUDE.md` for each rule.

## Verification

- `actionlint` clean.
- A `workflow_dispatch` dry run (or the next release) shows `store-submit` receiving the RTST id
  explicitly and `store-submit-dino` still skipped while `DINOREADER_STORE_APP_ID` is unset.
- The 43e2ef1 root cause is written in this file with the run id.

#### For CLAUDE.md (lifted 2026-10-25)

For `.github/CLAUDE.md`:

- In workflow shell, never pipe `gh release view` (or any `gh` call) into `grep -q` under `pipefail`: grep exits early and SIGPIPE fails the step. Capture the output into a variable, then grep it. (Slice 127: docs/plan/slices/127-release-safety-fixes.md)
- `store-submit.yml` takes a required `product_id` with no default. Callers pass `vars.STORE_PRODUCT_ID` for `ReadTheStupidText` and `vars.DINOREADER_STORE_APP_ID` for `DinoReader.App`; a guard step fails on a mismatch. (Slice 127: docs/plan/slices/127-release-safety-fixes.md)
