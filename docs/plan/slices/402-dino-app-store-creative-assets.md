# Slice 402 — Dino: App Store header and search-results pictures
**Product:** Dino

*Planned and started 2026-11-16 at the user's "yes" to making a picture for App Store Connect's empty "Header and Search Results" slots (iOS App Version 1.131.1, Product Page Information).*

**Touches:** `docs/store-listings/creative/dino/` (new), this slice file. No app code.

## Why

App Store Connect now has two optional "creative asset" slots (App Store Connect help, "Manage creative assets"): a product page **header** and a **search results** picture. They show on iOS/iPadOS 27 and later; when empty, the screenshots are used. Dino's are empty. Search results are where most people first see the app.

## Decisions

- **No text in the pictures**, not even the app name: no Dino picture carries printed words (user, 2026-11-10), and a text-free picture serves all 15 listing languages with one file. The App Store shows the app name and icon beside it.
- **Made from the existing book art** (`content/dino/<stem>/cover.jpg` and `pictures/*.jpg`, 896 × 1200), composed by a script; no new drawing.
- Two dedicated files, not one universal: header 21:9 at 3840 × 1646, search results 3:2 at 3840 × 2560 (Apple's specs, no alpha channel; the 21:9 header turned out to accept PNG only, see item 3).
- Keep faces and key art away from the edges and the bottom-left (where the store overlays the icon and name).

## Work

- [x] 1. Pick 5-7 covers whose characters read well small (bright, one clear character, gentle). Compose `docs/store-listings/creative/dino/header-3840x1646.png` (PNG: Apple's 21:9 header takes PNG only) and `search-3840x2560.jpg` (JPEG q92, 1.8 MB instead of an 8 MB PNG): covers on a soft background in the app's palette, no text, no alpha.
- [x] 2. Check each at a small size (search results show about 340 px wide on a phone): no cropped faces, nothing scary, no text, not blurry.
- [x] 3. Upload both in App Store Connect (Asset Library / Product Page Information, English (U.S.) primary; same files for the other languages) and submit them as a standalone asset submission for review. API first; if no API covers creative assets, the user's console with a direct link.
  Uploaded 2026-11-16 through the App Store Connect API (Asset Library, category `CREATIVE_ASSETS`): search results `search-3840x2560.jpg` = image `c5800019-6ce0-8cfb-8003-1ef3c2601265`; header = image `32800019-6ce0-8cfb-801f-67bbaaa4cf44`, uploaded as `header-3840x1646.png` because Apple's 21:9 header spec accepts PNG only (the JPG failed with INVALID_ASSET_FILE_FORMAT and was deleted). Review submission `649bea29-fc82-47e5-929b-9888354db861` (iOS) with both images: state WAITING_FOR_REVIEW. Placement (`PRODUCT_PAGE_HEADER_ASSET`, `APP_STORE_SEARCH_RESULTS_ASSET`, group `DEFAULT_PROFILE`, on the live version's localizations) is refused until the images are APPROVED ("parent is already approved"), so it is the follow-up once review passes.
- [ ] 4. Once both images read `APPROVED` (`GET /v1/appAssetLibraryImages/{id}`), place them: for each localization of the live iOS version (en-US is `ffb2d915-3064-4b82-a6fb-87db76558407`; list the rest with `GET /v1/appStoreVersions/{id}/appStoreVersionLocalizations`), `POST /v1/appAssetLibraryPlacements` with `placementType` `PRODUCT_PAGE_HEADER_ASSET` (header image) or `APP_STORE_SEARCH_RESULTS_ASSET` (search image), `placementGroup` `DEFAULT_PROFILE`, relationships `image` and `appStoreVersionLocalization`. Read back that each localization lists both placements. Run it through the `store-listing` agent. If Apple rejects an image, read the rejection reason from the submission, fix that image per the reason, re-upload it and submit it again; place only images that read `APPROVED`. The endpoint and enum names come from the upload run of 2026-11-16 (the API returned them in its 409 reply); confirm them in context7 `/websites/developer_apple_appstoreconnectapi` before running. Review status: https://appstoreconnect.apple.com/apps/6825053435/distribution/ios/version/inflight

## Verification

- Both files match Apple's sizes and have no alpha channel (checked by script).
- App Store Connect shows both assets submitted, then approved.
- On an iOS 27 device, App Store search for "Dino Book Reader" shows the new picture.

## Not yet verified

- How it looks in App Store search on an iOS 27 device.

## Review

- Pass 1 (high), `reviews/402-high.md`: fixed 3 (rejection path and confirm the API names in item 4) and the "JPEG or PNG" wording from 7. Skipped 1 (header cards sit a little right of centre): the image is already in Apple's review, and moving them would need a re-upload and a new review for a small layout change. Skipped 2 (compose script not kept): it is a one-off; the Decisions and item 1 describe the picture well enough to make it again. Skipped 4, 5, 6: notes only; the open lines go to `plan.md` in the index pass.
