# Slice 458 — Dino: count trial conversions, renewals, cancellations and refunds from the stores' server notifications
**Product:** Dino

*Built items 1, 2, 3, 5 (wave 2026-11-27), merged as PR #1449 (5c4cfca2); items 4 (console wiring) and 6 (Partner Center research) are open. Code written by a cloud session at 33f44bc9 (run cse_012UYzYUZFozXBxTHrqxPhPQ) and fixed locally.*

**Touches:** `worker/index.ts`, `worker/store-notifications/**` (new), `worker/store-notifications.test.mjs` (new), `worker/entitlements/apple-jws.ts`, `worker/events.ts`, `worker/events.test.mjs`, `wrangler.jsonc` (repo root; there is no `worker/wrangler.jsonc`), `wrangler.workers-dev.jsonc` (kept level with `wrangler.jsonc` by `check-configs.mjs`; widened by the coordinator after the cloud build), `STORE.md`, `site/dino/privacy.html`

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
- [x] 1. Apple endpoint, verifier and mapping table for `notificationType`/`subtype` to kind, with fixture tests built from Apple's sample signed payloads (`apple-jws-fixture.mjs` style).
- [x] 2. Google endpoint: verify the Pub/Sub OIDC token, call `subscriptionsv2.get` and `purchases.products.get` with the Dino Play service account, and map the results to kinds. Tests use recorded fixtures.
- [x] 3. `store` rule in `worker/events.ts` (closed detail list, `NO_BOOK` or book source), with tests.
- [ ] 4. Wiring the owner can't skip, done by tool:
  - Set the App Store Connect "App Store Server Notifications" production and sandbox URLs for Dino with the App Store Connect API (`apps/{id}` `subscriptionStatusUrl`, version 2).
  - Create the Pub/Sub topic and push subscription for Play with `gcloud`, and set it in Play Console's Monetization setup. If no API exists for that setting, the slice records the one console field with its deep link.
  - Prove the wiring: send Apple's `requestTestNotification` and Play's "Send test notification", then read the `dino_usage_beta` rows back.
- [x] 5. `STORE.md` and `site/dino/privacy.html`: one line each on what the stores send and what is kept.
- [ ] 6. Check Microsoft Partner Center for an add-on subscription notification (`library-docs`/`web-research`) and write the answer into this file. If one exists, plan its own slice.

**Verification:**
- Worker tests are green.
- Apple's test notification and Play's test notification each land as one `store` row in `dino_usage_beta`, read back with `analytics-query`.
- After release, *Watch* (plan.md *Outstanding work*): the next real trial start shows up as `ios.trial_started.trial` or `android.trial_started.trial` in `dino_usage`, and Apple's SUBSCRIPTION_EVENT report agrees for that day.

**Changed while building:**
- Built by a cloud session (33f44bc9, run cse_012UYzYUZFozXBxTHrqxPhPQ), then fixed locally after review. context7's quota was out, so vendor facts come from the vendors' pages: Apple's retry horizon of about 157 h (https://developer.apple.com/documentation/appstoreservernotifications/responding-to-app-store-server-notifications) sets the 7-day dedup and freshness window (one constant, `DEDUP_WINDOW_MS` in `worker/store-notifications/dedup.ts`); Google's OIDC keys are read from https://www.googleapis.com/oauth2/v3/certs.
- Apple: `POST /api/store/apple` verifies the ASSN v2 JWS through `worker/entitlements/apple-jws.ts`. Google: `POST /api/store/google` verifies the Pub/Sub OIDC token, then reads `subscriptionsv2.get` / `purchases.products.get`.
- One `store` row per decided notification goes to `dino_usage`; anything not Production (Apple sandbox, Xcode, LocalTesting, missing `environment`) and any Play test purchase goes to `dino_usage_beta`. The app's `/api/events` cannot write `store` (server-only in `worker/events.ts`).
- Duplicates are dropped by notification id for 7 days (matches Apple's retry horizon).
- `STORE.md` and `site/dino/privacy.html` carry the two lines of item 5.
- Touches widened by the coordinator after the cloud build: `wrangler.workers-dev.jsonc` (needed `/api/store/*` and `/dino/api/store/*` in `assets.run_worker_first` for `check-configs`) and `worker/events.test.mjs`.
- Inactive until item 4: the dedup KV namespace (`STORE_DEDUP`) and the vars `STORE_GOOGLE_AUDIENCE` and `STORE_GOOGLE_PUSH_EMAIL` are in no wrangler config. Until then every repeat delivery is counted and every Google push gets 401.
- Android refund of a one-time book is not counted: Play's voided-purchase notification names no sku.

**Not yet verified:** (also in `docs/plan/unverified.md`)
- [cloud] Apple trial-to-paid detection (`isTrialConversion`: renewal carries no offer and the previous period is at most 8 days, since Apple's renewal has no "came from a trial" field) and the Google trial/conversion heuristic (first `..0` order with an offer id, `offerPhase.freeTrial`) were built on synthetic fixtures (Apple signed with a throwaway chain, Play answers hand-written to the documented shapes). Confirm on the first real notifications after item 4.
- Items 4 and 6 are still open (see Work), so no real or test notification has reached the Worker yet.

#### From review
- High review file (findings 10): all 10 fixed (including #4, the `wrangler.workers-dev.jsonc` entries, and #6, the one 7-day constant).
- Medium review file (findings 8): #1-#4 fixed; #5-#8 are the four lines below.
- `reviews/pr-1449-check-1.md`: 5 follow-ups, none reopened code. Left as they are, to settle against real notifications after item 4:
  - Follow-up 1 (`worker/store-notifications/apple.ts:101`): EXPIRED, GRACE_PERIOD_EXPIRED, DID_FAIL_TO_RENEW, REFUND and REVOKE always count offer `paid`, so a cancelled trial that expires lands as `ios.expired.paid` and trial drop-off hides in paid churn.
  - Follow-up 2 (`apple.ts:22`): `isTrialConversion` assumes a 7-day trial plus a day of slack, so a 14-day trial is counted `renewed` and a 7-day free offer code is counted `converted`.
  - Follow-up 3 (`handler.ts:90`): dedup is check-then-act, so two near-simultaneous deliveries can both write a row (KV reads can be about 60 s stale).
  - Follow-up 4 (`apple.ts:53`): a payload with no `environment` is routed to `dino_usage_beta` with no log line.
  - Follow-up 5 (`apple.ts:150`): a missing or non-string `notificationUUID` gives the key `apple:`, which dedup treats as never seen, so replays inside the 7 days each write a row.
- Skipped #5 (`reviews/pr-1449-medium.md`): "`converted` is decided from `latestOrderId` read from Play now, not from the order the notification was about; a delayed or retried RENEWED push read after the next renewal sees `..1` and counts `renewed.paid`, losing the conversion." - follow-up: the Google trial/conversion heuristic is in Not yet verified (synthetic fixtures); settle it against real pushes after item 4's wiring, not by another guess.
- Skipped #6 (`reviews/pr-1449-medium.md`): "For cancelled, expired, grace, billing-retry and refunded kinds the offer is read from the current state (`inFreeTrial(item)`), so a trial that has ended is counted as `paid` (`android.expired.paid` instead of `.trial`)." - same follow-up as #5.
- Skipped #7 (`reviews/pr-1449-medium.md`): "An unknown product id forces a full R2 list plus one get per book on every delivery, with no negative cooldown." - unknown products are rare and the one refetch is already rate-bounded by the catalogue cache window.
- Skipped #8 (`reviews/pr-1449-medium.md`): "A failed JWKS fetch returns the cached keys but does not refresh `cachedKeys.at`, so after expiry the 60 s cooldown blocks any new fetch and legitimate pushes get 401 for at least a minute." - a 60 s window of 401s makes Pub/Sub retry, so no push is lost.

#### For CLAUDE.md (lifted 2026-11-27)
- `store` rows come only from `worker/store-notifications/` (Apple and Google handlers); `worker/events.ts` marks `store` server-only, so the app's `/api/events` must never be able to write it. Anything not Production (Apple Sandbox, Xcode, LocalTesting, missing `environment`; Play `testPurchase`) goes to `dino_usage_beta`, never `dino_usage`. (Slice 458: docs/plan/slices/458-dino-store-server-notifications.md)
- The store-notification dedup and freshness window is one constant, `DEDUP_WINDOW_MS` in `worker/store-notifications/dedup.ts`, and must stay above Apple's retry horizon of about 157 h. No account id, transaction id or token is stored. (Slice 458: docs/plan/slices/458-dino-store-server-notifications.md)
- A new `/api/store/*` style route needs its `assets.run_worker_first` entries in both `wrangler.jsonc` and `wrangler.workers-dev.jsonc` (`check-configs.mjs` fails otherwise). (Slice 458: docs/plan/slices/458-dino-store-server-notifications.md)

#### For README.md
Nothing: server-side only; the user-facing line is in `site/dino/privacy.html`.
