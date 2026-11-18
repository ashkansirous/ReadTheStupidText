# Slice 416 — Dino: store-products adds the Apple review screenshot and submits a new book's purchase itself

**Product:** Dino

**Touches:** `.github/scripts/store-products.mjs`, `.github/scripts/store-products.test.mjs`, `.github/scripts/ios-review-cadence.mjs` (a `delete` on the ASC client), `tools/dino/iap-review-screenshot.png`

## Why

"Love from Seagull Island" (`uk.sirous.dinoreader.book.love_from_seagull_island`, Apple id 6836361223) sat in App Store
Connect as MISSING_METADATA, so it was left out of the 1.133.1 submission (`appstore-submit` run 44413771332,
2026-11-18) and could not be bought on its own on iPhone. The only missing piece was the App Store review screenshot.
`store-products.mjs` never adds one, and its header (line 25) and `MANUAL_STEPS.apple` (line 346) say there is "no API"
for it and that products can only go to review "together with an app version". Both are false: on 2026-11-18 the
`store-listing` agent reserved, uploaded and committed the screenshot through `inAppPurchaseAppStoreReviewScreenshots`
and submitted the product alone with `POST /v1/inAppPurchaseSubmissions` (201; now WAITING_FOR_REVIEW). User,
2026-11-18: "fix the love from seagull island!". No user-facing app change, so no Android/iOS app task.

## Decisions

- The screenshot is one shared image, `tools/dino/iap-review-screenshot.png` (a real capture of the in-app Store
  screen with a book's Buy button, 1242x2688 or another size Apple accepts), used for every book product. The
  composed image used for Seagull Island on 2026-11-18 is a stopgap and is replaced by the next run's capture only
  where Apple asks for it again.
- The Apple step order per book becomes: product, en-US listing, availability, price, review screenshot (reserve,
  PUT each upload operation, PATCH `uploaded: true` with the md5), then, when the product is READY_TO_SUBMIT and the app
  already has an APPROVED in-app purchase, `POST /v1/inAppPurchaseSubmissions`. A product that already has a COMPLETE
  screenshot or is already submitted is left alone, so a rerun stays safe.
- `MANUAL_STEPS.apple` and the header lose the "no API" text; only what truly has no API stays listed.

## Work items

1. [x] Screenshot step and its completeness check in `createAppleProduct` (`store-products.mjs` around 435-451), with
   tests in `store-products.test.mjs` for: missing screenshot uploaded, COMPLETE screenshot left alone, failed upload
   reported. Built: `appleEnsureScreenshot` and helpers; the image is read only when an upload is needed.
2. [x] Submission step for READY_TO_SUBMIT products when the app has an approved purchase; a refusal is printed with
   Apple's message and does not stop the other books. Built: `appleSubmitWhenReady` (a refusal throws, so only that book fails).
3. [x] `tools/dino/iap-review-screenshot.png` captured on the Android emulator from the Store screen (2026-11-18,
   `device-test` agent, 1080x2400): the Dino debug app's Store tab through the test hooks `test-store mode=ok`,
   `fake-purchases mode=on price=\$0.99`, `skip-first-run lang=en`, `enter-grown-up tab=store`; four book cards with
   "$0.99" Buy buttons, placeholder covers, the subscription card's last lines at the top edge. Nothing bought. No
   Maestro flow (`maestro` is not on PATH here), so the capture was adb-driven.
4. [x] Header and `MANUAL_STEPS.apple` corrected.
5. [x] Request shapes checked through context7 `/websites/developer_apple_appstoreconnectapi` (2026-11-18): reserve POST,
   PATCH with `uploaded` + `sourceFileChecksum` (MD5), `POST /v1/inAppPurchaseSubmissions`; they match the live run.

## Verification

- Unit tests above pass; a dry run lists the screenshot and submission steps for every paid book.
- The next new paid book, run with `--apply --store apple --book <stem>`, ends WAITING_FOR_REVIEW with no hand step.

#### From review

- Skipped #1 (`reviews/s416-high.md`): "tools/dino/iap-review-screenshot.png ... is not in the change and does not exist in the worktree" - the PNG is work item 3, an emulator capture done next.
- Skipped #7 (`reviews/s416-high.md`): "putUploadOperation ... has no test and no retry" - the PUT stays injected; no retry added.
- Skipped #2 (`reviews/s416-medium.md`): "putUploadOperation does one un-retried fetch to Apple's upload URL" - one PUT per screenshot, and a rerun redoes it (the reservation stays AWAITING_UPLOAD and is filled again).
- Skipped #5 (`reviews/s416-medium.md`): "outcome does not separate a skipped submission from done" - the reason is printed in the run's NOT DONE notes.
- Skipped #8 (`reviews/s416-medium.md`): "the new delete client method has no case in retryingAsc" - retryingAsc covers the GETs that loop; these writes run once per book.
- Review #9 check (2026-11-18): one read-only `GET /v1/apps/6825053435/inAppPurchasesV2?filter[state]=APPROVED&limit=1` with the Lets-Call-Mom ASC key answered 200 with one APPROVED purchase, so the filter is accepted; no write call was made, and the screenshot route is still proven only by the earlier live run.
- Check pass (`reviews/s416-check.md`): #3 resolved by committing `tools/dino/iap-review-screenshot.png` in this PR; #4 fixed (a screenshot with no upload operations is replaced, not committed empty; tests updated).
- Skipped #1 (`reviews/s416-check.md`): "new Apple product GETs ... still use the bare asc client, not retryingAsc" - one 429 fails only that book, and the run is rerun-safe; the older Apple steps use the same bare client.
- Skipped #2 (`reviews/s416-check.md`): "the submit poll (6 tries, 5 s) run for every product every run" - at most ~25 s per book that never settles, on a hand-run script with a few new books a month.
- Skipped #5 (`reviews/s416-check.md`): "replace path does DELETE then reserve with no unwinding" - the next run sees no screenshot and reserves one, as the finding says.
- Skipped #6 (`reviews/s416-check.md`): "a skipped submission still ends as Exists" - the same as medium #5, the reason is in the NOT DONE notes.
- Skipped #7 (`reviews/s416-check.md`): "filter[state]=APPROVED is stated as proven live but only fake-tested" - the live read-only GET above answered 200 with an APPROVED purchase.
- Skipped #8 (`reviews/s416-check.md`): "readReviewScreenshot can still throw after the product is created" - openStore checks the file first, and a rerun completes the product.
- Skipped #9 (`reviews/s416-check.md`): "constants ... sit among unrelated HTTP constants" - cleanup only.
- Check pass 2 (`reviews/s416-check2.md`): #1, #2, #3 and #4 fixed. `appleScreenshotNeed` acts only on known states
  (no screenshot: reserve; FAILED: replace; AWAITING_UPLOAD: upload, or replace when Apple gave no upload operations)
  and leaves delivered, processing and unknown states alone, so a good screenshot is never deleted or re-sent.
- Skipped #1 (`reviews/s416-check3.md`): "AWAITING_UPLOAD with no uploadOperations is replaced" - at worst one wasted re-upload, and an empty commit would fail.
- Skipped #2 (`reviews/s416-check3.md`): "a missing or unknown state returns 'none', so it stays MISSING_METADATA silently" - the submit step's NOT DONE note names a product that is not READY_TO_SUBMIT.
- Skipped #3 (`reviews/s416-check3.md`): "'upload' on an old reservation fails if the PNG size changed" - the run reports it as FAILED and the next run replaces it, as the finding says.
