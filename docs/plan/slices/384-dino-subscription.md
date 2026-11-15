# Slice 384 — Dino: a monthly and yearly subscription for every book, alongside buying single books
**Product:** Dino

*Built 2026-11-15 (wave 2026-11-15), merged as PR #1174 (7548ec1d). Verified on the Android emulator only; see Not yet verified.*

**Touches:** `src/Dino/DinoReader.Core/BookAccess.cs`, `src/Dino/DinoReader.Core/SubscriptionAccess.cs` (new), `src/Dino/DinoReader.Core/AppConfig.cs`, `src/Dino/DinoReader.Core/StoreCatalog.cs`, `src/Dino/DinoReader.App/Purchases/**`, `src/Dino/DinoReader.App/Platforms/Android/Purchases/**`, `src/Dino/DinoReader.App/Platforms/iOS/Purchases/**`, `src/Dino/DinoReader.App/Platforms/Windows/Purchases/**`, `src/Dino/DinoReader.App/StorePage.xaml*`, `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml*`, `src/Dino/DinoReader.App/BookUpdater.cs`, `src/Dino/DinoReader.App/ShelfAudioPacks*.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**`, `src/Dino/DinoReader.App/CLAUDE.md`, `worker/entitlements/**`, `worker/products.ts`, `worker/stories.ts`, `worker/index.ts` (added in review: wires the subscription route), `worker/appconfig.ts`, `worker/appconfig.test.mjs`, `worker/entitlements.test.mjs`, `worker/stories.test.mjs`, `worker/CLAUDE.md`, `.github/scripts/store-products.mjs`, `.github/scripts/store-products.test.mjs`, `tests/Dino/DinoReader.Tests/**`, `.maestro/dino/subscription.yaml` (new), `.maestro/dino/paid-books.yaml` (scroll to the second Buy pill below the subscription card), `docs/store-listings/dino/**`, `site/dino/privacy.html`, `src/Dino/DinoReader.App/GrownUpGate.cs`, `src/Dino/DinoReader.App/GrownUpMode.cs`, `src/Dino/DinoReader.App/GrownUpHeader.cs`, `src/Dino/DinoReader.App/BackupPage.xaml.cs`, `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Dino/DinoReader.App/StoryTile.cs` (added during build: lapsed tile flag), `docs/plan/slices/384-dino-subscription.md`

## Why

Books are sold one at a time today (Slice 346, live: `paidBooks.enabled` is `true` in the live
`/api/app-config`, checked 2026-11-14; 15 store books, 4 free and 11 paid). The user wants a subscription
*as well as* single books: one payment that opens every paid book while it runs.

## Decisions (user, 2026-11-14, RefineScope)

1. **Both ways to pay.** The store still shows the 4 free books and every paid book for sale one at a time,
   exactly as Slice 346 does. Next to them it offers a subscription. Slice 346 stays as it is.
2. **Two plans: £1.99 a month and £14.99 a year**, both with a **7-day free trial** (the stores' own
   introductory free-trial offer, once per store account). The UK price is set in each store; the stores fill in
   other countries. The app shows the store's own localised price strings, never a price written in the app.
3. **While the subscription runs**, every paid book can be downloaded and listened to (book, page audio, audio
   packs, high-res package).
4. **When the subscription ends**, every downloaded book that is neither free nor bought is **greyed out on the
   parents' screen** (grown-up mode of the shelf) and **removed from the child's shelf**. Its files stay on the
   device, so renewing or buying it opens it at once with no new download. On the parents' screen each greyed-out
   book offers **Subscribe again** and **Buy this book**.
5. **Free books and bought books are never affected** by the subscription starting, running or ending.
6. **Offline:** the app remembers the end date the store last reported. With no internet it locks those books
   3 days after that date. Online, it follows the store's answer at once.
7. **Android, iPhone and Windows together**, the same three places single books are sold.

## Decided by Claude (obvious behaviour; tell the user, change if they disagree)

- **This narrows one Slice 346 rule.** Slice 346 says "nothing on the shelf is ever taken away or locked". That
  stays true for free books, bought books and books a family downloaded while everything was free (before
  `paidBooks.enabled` went on). Only a book whose sole reason to be on the device is the subscription can be locked.
  So the app records, per book, what unlocked it at download (free, bought, subscription, from-before), and
  `BookAccess` decides from that plus the live purchase and subscription state.
- **Buying a book while subscribed keeps it forever.** Owning it wins over the subscription.
- **Restore purchases also brings back an active subscription** (same button, all three stores). No accounts of
  our own (Slice 346 Decision 1): the subscription lives in the parent's Apple, Google or Microsoft account.
- **Behind the grown-ups gate**, like buying a book. The child's side never shows an offer.
- **The Worker checks the subscription with the store** before it serves a paid book's files, the same way it
  checks a purchase now, and it also checks the subscription has not expired (today's check has no expiry).
- **The subscription is one product group per store** with a monthly and a yearly plan (Play: one subscription
  with two base plans; Apple: one subscription group with two products; Microsoft: two subscription add-ons).
  `worker/products.ts` maps the subscription products to "every paid book", including books added later.
- **A "manage subscription" link** in the grown-ups area opens the store's own subscription page (cancel and
  change plan happen there, never in the app). Apple and Google both require an auto-renewing subscription's
  price, length, trial and renewal terms to be shown before buying, with links to the terms and privacy pages.

## Waiting on the user (store setup; do these when the code is merged)

- Create the subscription and its two plans in Google Play Console, App Store Connect and Microsoft Partner Center,
  with the prices and trial above. `.github/scripts/store-products.mjs` should do as much as the store APIs allow;
  only what no API can set goes to the user, one step at a time with direct links. Plan: `store-products.mjs --apply`
  for Play and App Store Connect, Partner Center by hand.
- Approve the listing text drafted in `docs/store-listings/dino/` before the `store-listing` agent pushes it.

## Store listing switch (Slice 291 item 10, 2026-11-14)

- The live Play text is `docs/store-listings/dino/play.md` (four free, buy each book once). The subscription text
  is written in advance in `docs/store-listings/dino/play-subscription.md` and is pushed **only when this slice is
  live on Play** (same for the App Store and Microsoft versions, written from it then, with the EULA and privacy
  links Apple requires). The switch is the last work item here; record its date in Slice 291 item 10.

## Design note (coordinator, wave 2026-11-15)

- **iOS stays on StoreKit 1** for this slice, the same path single books use today (`IosPurchases.cs`, transaction id as
  the proof, the Worker reads it from the App Store Server API, which returns `expiresDate` for subscription
  transactions). StoreKit 1 sells auto-renewing subscriptions; moving every iOS purchase to StoreKit 2 stays in
  plan-not-needed: 390 and does not block this one. Reason: one purchase path per platform, and the parked StoreKit 2 route choice is the user's.
- **One owner of subscription state:** `SubscriptionAccess` (pure, Core) holds the last store answer (state, end date,
  checked-at) and is persisted through one app-side store (the same settings store purchases use) after every store
  read. Interrupts: app killed: the persisted record is read at start; offline: the record decides, locked 3 days after
  the end date (Decision 6); store read fails while online: treated as offline (keep the record), never as expired;
  cancelled purchase sheet: no change; Restore: replaces the record with the store's answer.
- **One owner of "unlocked by":** `BookAccess` keeps the per-book record (free, bought, subscription, from-before) written
  once when a book is downloaded, and upgraded to bought when a purchase lands; never downgraded.
- **Work G and H do not go live from this slice's build:** `store-products.mjs` gains the subscription in dry-run by
  default; running `--apply` against the stores and pushing listings are the user's steps under *Waiting on the user*.
  Listing text H is drafted in the repo only.

- **The subscription's end date on iOS and Android comes from the Worker** (review finding 2, parts 2 and 3).
  `GET /api/stories/subscription` (same `X-Purchase-Proof`, limiter and error codes as a paid book's files) answers
  `{state: active|grace|expired|revoked, expiresAt}` from Apple's All Subscription Statuses or Play's
  `subscriptionsv2`; after each store read `BookPurchases` asks it in the background (the shelf never waits) and
  writes the answer into `SubscriptionAccess`'s record (`Confirmed`). Known behaviour: the local StoreKit 1 end date
  is used only until the first Worker answer. This replaces Android's "3 days after the last read" with the real end
  date. Windows keeps its local end date.
- **The record merge (review round 2, findings 1 and 2; one design, two-strikes rule).** `SubscriptionAccess.Merge(record,
  answer)` (pure, Core) is the only way the record changes. An answer is a device read or a Worker answer. Every device
  read that reached the store gets a new read number (kept in the record); a Worker answer carries the read number of
  the device read that started it. Rows:
  - A device read carries its kind: `Store` (Android Play and Windows: the store itself says entitled now) or `Estimated`
    (iOS StoreKit 1: entitled from a kept transaction), and for `Estimated` the newest transaction date.
  - Device says entitled: state Active (or Trial), `Confirmed=false`, new read number, end = the latest of: now (the read
    time), the held record's end (only when that record's state is Active, Trial or InGrace; an Expired or Revoked
    record's end is not carried) and the device's own estimate (iOS; none on Android). So an entitled read always
    grants at least 3 days from the read (Decision 6) and never shortens a known end.
  - An `Estimated` entitled read does not replace a Worker-confirmed Expired or Revoked record unless its newest
    transaction date is after that record's `CheckedAt` (a renewal or a new purchase); a `Store` entitled read always
    replaces it.
  - Device says not entitled (or lists no subscription): Expired, `Confirmed=false`, new read number.
  - Device read failed or offline: no change.
  - iOS (`Estimated`) read with no kept subscription transaction at all: no information (state Unknown), so no change, like a failed read; only an `Estimated` read whose kept transactions have all ended gives Expired (review check 5, finding 1).
  - Worker answer whose read number is not the record's current one: ignored (late answer).
  - Worker active / grace / expired / revoked for the current read: that state and `expiresAt`, `Confirmed=true`.
  - A Worker answer with an `expiresAt` replaces the record's end (earlier or later: the Worker is the truth); one with a
    null `expiresAt` keeps the record's known end only while the record's state is Active, Trial or InGrace (the same carry
    rule as the device read's latest end); otherwise the end stays null. The proof sent to the Worker is the newest kept
    subscription purchase's (latest purchase / transaction time), not the first listed.
  - iOS: a Restored transaction reads its `original` transaction's date for the end estimate and never moves the newest
    transaction date (only a Purchased transaction does), so a Restore is never a new purchase.
  - iOS: the subscription end is saved before the owned product.
  - Worker unknown state / error / 429 / offline: no change.
  The store applies one answer under one lock, so a late answer cannot overwrite a newer read.
- **Only a real "expired" from a store counts as expired** (review finding 3): Play 410 or an expired state, Apple's
  expired / revoked status values. A store 400 or 404 (not yet propagated after a purchase, a bad token) is
  `CANNOT_VERIFY`, so the app keeps its record.

## Work

Smallest end-to-end first: Android subscription unlocking one paid book, server-checked.

- [x] **A. Core rules (pure, tested).** `SubscriptionAccess` (new): state from the store (active, trial,
      in grace, expired, unknown) plus last known end date, and the 3-day offline rule. `BookAccess` gains the
      per-book "unlocked by" record and answers, for each book: readable, locked-by-lapse (greyed for parents,
      hidden from the child), or buyable. Tests cover every row of Decisions 4-6 and the Slice 346 exceptions.
- [x] **B. Worker check.** `worker/entitlements/*`: verify a subscription proof per store (Google subscriptions
      API, Apple App Store Server API transaction with `expiresDate`, Microsoft Store collections for subscription
      add-ons) and refuse an expired or revoked one. `products.ts`: the subscription products unlock every paid book.
      Same proof header, rate limits and error codes as Slice 346 (`worker/CLAUDE.md`). Tests in
      `worker/entitlements.test.mjs` and `worker/stories.test.mjs`.
- [x] **C. Android.** Play Billing `ProductType.Subs` query, base plans and the trial offer, buy, acknowledge,
      read the active subscription and its end date, Restore. Builds on Slice 380's listener-only product details
      (do 380 first or together).
- [x] **D. iOS.** StoreKit subscription purchase, trial, renewals and expiry from the transactions, Restore.
      Same behaviour and tests as Android.
- [x] **E. Windows.** `Windows.Services.Store` subscription add-ons, trial, expiry, Restore.
- [x] **F. Store page and shelf.** Store page: a subscription card (both plans, trial, terms, price from the
      store) above the books, behind the grown-ups gate; books still show their own price. Parents' screen:
      greyed-out lapsed books with Subscribe again / Buy this book. Child's shelf: lapsed books hidden. Manage
      subscription link in the grown-ups area. `BookUpdater` and `ShelfAudioPacks` send the subscription proof for
      subscription books. Every wait over 500 ms shows a loading state. Texts in all 15 app languages.
- [x] **G. Store setup script.** `store-products.mjs` creates the subscription, plans, prices and trial through
      the Play and App Store Connect APIs (dry run by default); what Partner Center can't do by API is listed for
      the user.
- [x] **H. Store listings and privacy.** Listings mention the subscription and its terms (through the
      `store-listing` agent, draft then apply after approval); `site/dino/privacy.html` unchanged in substance
      (no new data leaves the device) but names the subscription.
- [x] **I. Device tests.** `.maestro/dino/subscription.yaml` with a fake subscription (Debug `FakePurchases`):
      subscribe, download a paid book, lapse it, check greyed on the parents' screen and gone from the child's shelf,
      renew, check it opens without a download; a bought book and a free book unchanged throughout.

## Verification

- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj`: `SubscriptionAccess` and `BookAccess` rows.
- `node --test worker/` for the entitlement and story-route tests.
- Android emulator and iOS simulator: `.maestro/dino/subscription.yaml` on both. Windows: the same flow on the
  Hyper-V VM.
- Real store test (after store setup): Play licence tester and an Apple sandbox account subscribe with the trial,
  the sandbox renewal speed-up shows a lapse locks and a renewal unlocks; a book bought earlier stays readable.

## Changed while building

- Built during review: the Worker route `GET /api/stories/subscription` (state and `expiresAt` read from Apple/Play), the app's `SubscriptionEndClient`, and the wiring in `worker/index.ts` (added to Touches).

## From review

Eleven findings files under the reviews folder (all read to the last finding); every finding was applied except the lines below:
`pr-1174-high` (10 findings: 1-8 and 10 applied); `pr-1174-medium` (8: 1 and 2 replaced by the single `SubscriptionAccess.Merge` design, see the Design note; 3, 4, 5, 7, 8 applied); `pr-1174-check` (8: 1, 3, 4, 7 applied); `pr-1174-check2` (7: 1, 2 as the final entitled-read rule, 4 applied); `pr-1174-check3` (6: 1, 3, 4 applied); `pr-1174-check4` (2: both applied); `pr-1174-check5` (2: both applied); `pr-1174-check6` (5: 2, 3 applied); `pr-1174-check7.md` (3: all applied); `pr-1174-check8.md` (1: applied); `pr-1174-check9.md` (no findings).

- Skipped #9 (reviews/pr-1174-high.md): "Every PurchaseSnapshot Changed event and every grown-up mode flip now rebuilds the whole shelf" - low, performance only.
- Skipped #2 (`reviews/pr-1174-check.md`): "iOS SubscriptionNow never returns Expired once any subscription transaction is kept, so a lapsed subscription reads as Active (past end)" - online with the Worker failing is treated as offline, Decision 6 allows 3 days.
- Skipped #5 (`reviews/pr-1174-check.md`): "is labelled Trial only if held trial end equals Worker expiresAt within 1 s" - low.
- Skipped #6 (`reviews/pr-1174-check.md`): "HEAD on /api/stories/subscription answers 200 whenever a source is wired, even with paidBooks.enabled off or no proof" - low.
- Skipped #8 (`reviews/pr-1174-check.md`): "the app calls it after every device read that reached the store; polling can starve book downloads with 429" - the route is called once after each store read, not polled (`BookPurchases.cs:140` is the only `StartEndCheck` call, inside `ReadAsync`; `ApplyEndAsync` is serialised by `_endGate`, `BookPurchases.cs:225`).
- Skipped #6 (`reviews/pr-1174-medium.md`): "askSubscription/askSubscriptionStatus and subscriptionOwns/subscriptionStatusOf (Apple: subscriptionProductOf/subscriptionResultOf/checkSubscription vs statusOfLast/bestStatus/checkSubscriptionStatus) duplicate URL, fetch, status mapping; can drift" - low refactor, not needed for correctness; left as is.
- Skipped #2 (`reviews/pr-1174-check3.md`): "A Store-kind entitled read always replaces a Worker-confirmed Expired record" - Decision 6: online the app follows the store's answer at once; Play's own entitled answer is that answer, and the Worker re-checks before serving any paid file.
- Skipped #5 (`reviews/pr-1174-check3.md`): "_subTrial is not persisted although _subEnds and _subNewest are" - low.
- Skipped #6 (`reviews/pr-1174-check3.md`): "NoteSubscription reads Products (plain Dictionary) from the queue callback while the ProductsDelegate writes it" - low.
- Skipped #3 (`reviews/pr-1174-check2.md`): "Trial estimate fires for any first Purchased tx with empty _subEnds; IntroductoryPrice is set for every user, not eligibility" - the local StoreKit 1 end date is used only until the first Worker answer (Design note); StoreKit 1 exposes no trial eligibility.
- Skipped #5 (`reviews/pr-1174-check2.md`): "Any non-200 Worker reply (404 switch off, 402, 403, 429) reported as StoreUnreachable while online; client discards status" - low.
- Skipped #6 (`reviews/pr-1174-check2.md`): "StartEndCheck queues a Task.Run per entitled read, serialized on _endGate with 15 s timeout each; no coalescing" - low.
- Skipped #7 (`reviews/pr-1174-check2.md`): "HEAD to subscription path returns 200 when a source is wired even when switch off or proof missing (GET gives 404/402); 429 has no Retry-After" - low.

- Skipped #1 (`reviews/pr-1174-check6.md`): "LatestEnd makes an iOS estimated entitled read end at least now, so a lapsed sub stays granted while the Worker gives no 200" - a Worker that gives no answer is treated as no internet, and Decision 6 "With no internet it locks those books 3 days after that date"; the Worker checks the store before serving any paid file, so no new paid book can be downloaded meanwhile.
- Skipped #4 (`reviews/pr-1174-check6.md`): "gate (subscriptionProductOf/subscriptionResultOf) and status route (statusOfLast) duplicate the Apple rules with different 400/404 handling" - low.
- Skipped #5 (`reviews/pr-1174-check6.md`): "Trial kept only when Worker end is within 1 s of the held trial end and Worker never reports trial" - low.

## Tests and devices (2026-11-15)

- Dino 4568 pass, Shared 1722, worker 393, typecheck clean; Android Debug and iOS compile with 0 errors.
- Android emulator `emulator-5554`, build `ed97db36e0c6`: `subscription`, `paid-books` and `open-book` flows PASS (runs 1-4; logs `reviews/wave8-device/*-run4.log`).
- `grown-ups-button` fails the same way on main's build `90f93c61710d` (GalleryDone visible on Settings; `reviews/wave8-device/grown-ups-button-main.log`), so it is not this slice's.

#### For CLAUDE.md (lifted 2026-11-15)

None from this slice's brief.

#### Not yet verified

- iOS simulator subscription flow (`.maestro/dino/subscription.yaml`).
- Windows Hyper-V VM subscription flow.
- Real store sandbox (Play licence tester, Apple sandbox) after store setup.
- Worker route `GET /api/stories/subscription` live, after the batched release deploy.
- `store-products.mjs` request shapes: Apple subscription shapes checked against the App Store Connect API docs (2026-11-15, after the first `--apply --store apple` failed on `availableInAllTerritories`); the first successful live run is the proof. Play and Microsoft shapes still UNVERIFIED.

## App Store subscription creation fix (2026-11-15, user: "I still dont see the subscriptions")

Cause, read live: App Store Connect had no subscription group for app 6825053435, so iOS returned no plan prices and the Store page kept the subscription card hidden (StorePage.xaml.cs `ApplySubscriptionCard`). The script's Apple subscription calls were rewritten per the docs: separate availability call, per-territory prices from the GBR point's equalisations and per-territory 7-day trials, no automatic retry of a POST, a rerun reads what Apple holds first.

From review (`reviews/s384a-check.md`), follow-ups, not changed: #1 if GBR is ever missing from the held availability, the base price and trial are not posted and no note says so (availability is posted for every territory, so GBR is held); #2 a territory Apple refuses permanently fails on every rerun and is listed each time. Low: an existing price's amount is not compared; an entry with no territory only adds a note; notes are lost when no session is passed.

**Live 2026-11-15:** `store-products.mjs --apply --store apple` (after #1198) created group "Dino Book Reader subscription" with `uk.sirous.dinoreader.sub.monthly` (1.99 GBP) and `.sub.yearly` (14.99 GBP), prices and 7-day free trials in all 175 territories, 0 failed. Claude then uploaded one App Store review screenshot to each through the API (the Dino phone shelf shot padded to 1242x2688, as for the books in Slice 346); both read back `READY_TO_SUBMIT`. The script's "by hand: review screenshot (no API)" line is wrong: `subscriptionAppStoreReviewScreenshots` and the IAP equivalent exist; fold the upload into the script. Still the user's: submitting the subscriptions for review with an app version (https://appstoreconnect.apple.com/apps/6825053435/distribution/subscriptions). Play and Microsoft subscriptions are not created yet.

Not yet verified (moved below the Apple section):
- Offline 3-day lock on a real device.
- Restore on iOS.
- The plans showing in the iOS app (sandbox/TestFlight) now that the products exist.

## iOS Manage subscription (2026-11-15)

Bug (user, TestFlight): "the subscription worked, but after, I tried to cancel it. it opened another external place where it showed that I have no subscriptions." Cause: iOS `ManageSubscriptionAsync` opened https://apps.apple.com/account/subscriptions, and a TestFlight app runs in the sandbox, which that page never lists.

Fix: Apple's in-app sheet `AppStore.showManageSubscriptions(in:)` (iOS 15+; the iOS 17 subscription-group overload is not used) through the new Swift export `dino_sk_manage_subscriptions` (Slice 390's wrapper; it answers `ok`, or `no-scene`, `unsupported` (iOS app on a Mac) or the StoreKit error). `IosPurchases.ManageSubscriptionAsync` returns true when the sheet was shown and then raises `PurchaseUpdates` so the status is read again (a plan change shows; a cancel does not change the card until the plan ends, because Apple keeps listing it as owned and the app has no "will not renew" state on any platform, Android included; review of 2026-11-15, finding 3, left as it is). The Swift export picks only a foreground-active (else foreground-inactive) scene; with none it answers `no-scene`. The web page opens only when the bridge is missing or the answer is `no-scene` or `unsupported`; any other failure returns false, `BookPurchases` reports a `Store`/`StoreFailed` fallback and the page shows `Store_ManageFailed`. A second tap while the sheet is open does nothing, and the wait ends after 10 minutes (the waiting entry is abandoned) so the grown-up guard always ends. Debug trace lines: `manage sheet shown`, `manage sheet failed`, `manage sheet unavailable`, `manage sheet gave no answer in time`. Android keeps the Play subscriptions link (it works for Play's own test purchases); Windows is unchanged.

From review (`reviews/s384m-medium.md`), follow-ups, not changed: #1 after the 10-minute timeout the sheet may still be open and its later close is not re-read (the next store read picks it up); #2 a repeat tap while the sheet is open does nothing visible; #3 on Android and Windows a false from the link opener now reports `StoreFailed` though no store failed (telemetry reason only); #4 a StoreKit error shows the failure message with no web fallback; #5 a `foregroundInactive` scene can be chosen during a transition; #6 the tap is ignored silently while another external activity runs.

Not yet verified:
- Manage subscription on a TestFlight iPhone shows Apple's sheet with the sandbox subscription, and the status is read again when it closes (a cancel made there shows only once the plan ends; the app has no "will not renew" card line) (the Swift side is proven by `dinoreader-ios` on the branch, the behaviour only on a device).
