# Slice 458 — Dino: count trial conversions, renewals, cancellations and refunds from the stores' server notifications
**Product:** Dino

*Planned 2026-11-26.*

**Touches:** `worker/index.ts`, `worker/store-notifications/**` (new), `worker/store-notifications.test.mjs` (new), `worker/entitlements/apple-jws.ts`, `worker/events.ts`, `worker/wrangler.jsonc`, `STORE.md`, `site/dino/privacy.html`

**Why:** a free trial turning paid, a renewal, a cancellation or a refund happens on the store's side, usually while the app is closed, so the app's own events ([Slice 455](455-dino-purchase-outcome-events.md)) can't see it. The Worker has no App Store Server Notifications v2 endpoint and no Play Real-time Developer Notifications (RTDN) endpoint today. It only verifies an Apple JWS proof on request (`worker/entitlements/apple-jws.ts:229`). On 2026-11-26 the owner asked how many people opted into the trial. App Store Connect could answer only through daily reports, and Play couldn't answer at all with the current key. The owner's words: "add this to the plan so we collect these events".

**Decisions (decided by this planning session, 2026-11-26):**
- The Worker gets two endpoints:
  - Apple: `POST /api/store/apple`, App Store Server Notifications v2. The signed payload is verified with the existing `apple-jws.ts` chain check.
  - Google: `POST /api/store/google`, a Pub/Sub push for Play RTDN. The push is verified by its Google-signed OIDC token, then the purchase is read with `purchases.subscriptionsv2.get` to learn the offer (trial or paid) and the state.
- Each verified notification writes one `store` row to `dino_usage`. Detail `<platform>.<kind>.<offer>`, kind from a closed list: `trial_started`, `subscribed`, `converted`, `renewed`, `cancelled`, `expired`, `refunded`, `grace`, `billing_retry`, `code_redeemed`, `book_bought`, `book_refunded`. The row also carries the storefront country, and the product (plan, or the book GUID via `meta.json` productId for a book).
- No account id, transaction id or token is stored. A duplicate delivery is dropped by a short-lived KV key of the notification UUID, which expires and is never logged.
- Apple sandbox, Play test purchases and license testers go to `dino_usage_beta`, never `dino_usage` (production-only-analytics). For Apple that's the `environment` field; for Play, `testPurchase` on the purchase.
- Windows is not covered in this slice. Microsoft Store offers no comparable push for add-on subscriptions as far as this plan knows. Item 6 checks that and opens its own slice if one exists.

**Work:**
- [ ] 1. Apple endpoint, verifier and mapping table for `notificationType`/`subtype` to kind, with fixture tests built from Apple's sample signed payloads (`apple-jws-fixture.mjs` style).
- [ ] 2. Google endpoint: verify the Pub/Sub OIDC token, call `subscriptionsv2.get` and `purchases.products.get` with the Dino Play service account, and map the results to kinds. Tests use recorded fixtures.
- [ ] 3. `store` rule in `worker/events.ts` (closed detail list, `NO_BOOK` or book source), with tests.
- [ ] 4. Wiring the owner can't skip, done by tool:
  - Set the App Store Connect "App Store Server Notifications" production and sandbox URLs for Dino with the App Store Connect API (`apps/{id}` `subscriptionStatusUrl`, version 2).
  - Create the Pub/Sub topic and push subscription for Play with `gcloud`, and set it in Play Console's Monetization setup. If no API exists for that setting, the slice records the one console field with its deep link.
  - Prove the wiring: send Apple's `requestTestNotification` and Play's "Send test notification", then read the `dino_usage_beta` rows back.
- [ ] 5. `STORE.md` and `site/dino/privacy.html`: one line each on what the stores send and what is kept.
- [ ] 6. Check Microsoft Partner Center for an add-on subscription notification (`library-docs`/`web-research`) and write the answer into this file. If one exists, plan its own slice.

**Verification:**
- Worker tests are green.
- Apple's test notification and Play's test notification each land as one `store` row in `dino_usage_beta`, read back with `analytics-query`.
- After release, *Watch* (plan.md *Outstanding work*): the next real trial start shows up as `ios.trial_started.trial` or `android.trial_started.trial` in `dino_usage`, and Apple's SUBSCRIPTION_EVENT report agrees for that day.
