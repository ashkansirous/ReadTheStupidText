# TestFlight builds approved but never sent to testers (2026-10-28)

## What went wrong

Builds the daily job (`.github/scripts/ios-review-cadence.mjs`, Slice 147) submits for Beta App Review
had `buildBetaDetails.autoNotifyEnabled = false`. When Apple approved them they stayed at
**Approved** (`externalBuildState = BETA_APPROVED`) and public-link testers never got them until someone
pressed **Notify Testers** in App Store Connect. Read The Stupid Text: 1.54.1 was the last build to reach
testers on its own; 1.62, 1.63 and 1.65 sat at Approved (the user notified 1.65 by hand). Dino: 1.61 and
1.67.1 the same. Lets Call Mom builds have it `true` and reach testers.

Verified live: `PATCH /v1/buildBetaDetails/{detailId}` with
`{"data":{"type":"buildBetaDetails","id":"<detailId>","attributes":{"autoNotifyEnabled":true}}}` returns 200
while the build is `WAITING_FOR_BETA_REVIEW` (Dino 1.71.1 was switched on this way, 2026-10-28).

## The fix (in `ios-review-cadence.mjs` + its `.test.mjs`)

1. `normalizeBuild` also reads the included `buildBetaDetails` (already in `include`): `betaDetailId`,
   `autoNotify` (bool) and `externalState`.
2. **Before submitting** a build for Beta App Review, if its `autoNotify` is not true, PATCH its
   buildBetaDetail to `autoNotifyEnabled: true` (shape above). Apple then sends it to testers at approval.
3. **Every TestFlight run, before the submit decision, release the newest approved build.** Pure function
   `decideNotify(builds)`: among valid, unexpired builds whose `externalState` is `BETA_APPROVED` or
   `IN_BETA_TESTING`, take the newest (builds are newest-first). If it is `BETA_APPROVED`, notify it with
   `POST /v1/buildBetaNotifications`
   `{"data":{"type":"buildBetaNotifications","relationships":{"build":{"data":{"type":"builds","id":"<buildId>"}}}}}`.
   Older approved-but-unsent builds are superseded and are left alone (notifying them would only email
   testers about an older build). A refused notify fails the app's result with Apple's text, like a refused
   submit, but the submit step still runs first-come: do the notify, record its outcome, then run the
   existing submit decision; the summary line names both (e.g. `notified 1.63 (…); 1.68 (…) submitted for
   Beta App Review`). Either failing makes the app's outcome `failed`.
4. Also switch on `autoNotify` for any build still `WAITING_FOR_BETA_REVIEW` / `IN_BETA_REVIEW` with it off,
   so an older waiting submission reaches testers at approval too.
5. Unit tests for `decideNotify`, the new `normalizeBuild` fields, and the request bodies (with the existing
   fake ASC client in the test file).

Never cancel a review or expire a build (the cadence rules in `ios-review-cadence` still hold).
