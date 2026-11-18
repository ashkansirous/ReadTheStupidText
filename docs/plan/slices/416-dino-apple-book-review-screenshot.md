# Slice 416 — Dino: store-products adds the Apple review screenshot and submits a new book's purchase itself

**Product:** Dino

**Touches:** `.github/scripts/store-products.mjs`, `.github/scripts/store-products.test.mjs`, `tools/dino/iap-review-screenshot.png`

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

1. [ ] Screenshot step and its completeness check in `createAppleProduct` (`store-products.mjs` around 435-451), with
   tests in `store-products.test.mjs` for: missing screenshot uploaded, COMPLETE screenshot left alone, failed upload
   reported.
2. [ ] Submission step for READY_TO_SUBMIT products when the app has an approved purchase; a refusal is printed with
   Apple's message and does not stop the other books.
3. [ ] `tools/dino/iap-review-screenshot.png` captured on the Android emulator from the Store screen (a scripted flow).
4. [ ] Header and `MANUAL_STEPS.apple` corrected.
5. [ ] Request shapes checked through context7 (App Store Connect API) before the code is written.

## Verification

- Unit tests above pass; a dry run lists the screenshot and submission steps for every paid book.
- The next new paid book, run with `--apply --store apple --book <stem>`, ends WAITING_FOR_REVIEW with no hand step.
