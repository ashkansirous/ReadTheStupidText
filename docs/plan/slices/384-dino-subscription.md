# Slice 384 — Dino: a monthly and yearly subscription for every book, alongside buying single books
**Product:** Dino

*Plan cleanup 2026-11-25: verdict close. Read live: both App Store subscriptions APPROVED, en-US localizations only; Partner Center 9N9VHRLCZXN5 has 12 Durable book add-ons and no subscription. The Microsoft subscriptions, the 14 App Store localizations (Apple refused them only while Waiting for Review; now approved) and the script gaps (over-long text, subscription review screenshot) moved to a new slice. Nothing waits on Apple.*

*Built 2026-11-15 (wave 2026-11-15), merged as PR #1174 (7548ec1d). Verified on the Android emulator only; see Not yet verified.*

**Touches:** `src/Dino/DinoReader.Core/BookAccess.cs`, `src/Dino/DinoReader.Core/SubscriptionAccess.cs` (new), `src/Dino/DinoReader.Core/AppConfig.cs`, `src/Dino/DinoReader.Core/StoreCatalog.cs`, `src/Dino/DinoReader.App/Purchases/**`, `src/Dino/DinoReader.App/Platforms/Android/Purchases/**`, `src/Dino/DinoReader.App/Platforms/iOS/Purchases/**`, `src/Dino/DinoReader.App/Platforms/Windows/Purchases/**`, `src/Dino/DinoReader.App/StorePage.xaml*`, `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml*`, `src/Dino/DinoReader.App/BookUpdater.cs`, `src/Dino/DinoReader.App/ShelfAudioPacks*.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**`, `src/Dino/DinoReader.App/CLAUDE.md`, `worker/entitlements/**`, `worker/products.ts`, `worker/stories.ts`, `worker/index.ts` (added in review: wires the subscription route), `worker/appconfig.ts`, `worker/appconfig.test.mjs`, `worker/entitlements.test.mjs`, `worker/stories.test.mjs`, `worker/CLAUDE.md`, `.github/scripts/store-products.mjs`, `.github/scripts/store-products.test.mjs`, `tests/Dino/DinoReader.Tests/**`, `.maestro/dino/subscription.yaml` (new), `.maestro/dino/paid-books.yaml` (scroll to the second Buy pill below the subscription card), `docs/store-listings/dino/**`, `site/dino/privacy.html`, `src/Dino/DinoReader.App/GrownUpGate.cs`, `src/Dino/DinoReader.App/GrownUpMode.cs`, `src/Dino/DinoReader.App/GrownUpHeader.cs`, `src/Dino/DinoReader.App/BackupPage.xaml.cs`, `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Dino/DinoReader.App/StoryTile.cs` (added during build: lapsed tile flag), `docs/plan/slices/384-dino-subscription.md`

## Mock added 2026-11-15

The 2026-11-15 Subscription mock (`docs/design/dino-reader/Dino Reader 2026-11-15 Subscription.dc.html`) draws this slice's screens. The built record above stays as it is; the remaining work is in Slices 396 (Restore and Redeem out of the Store foot), 397 (the Store card in every state, Save tag, wide layout), 398 (locked tiles, why-banner, offline notice) and 399 (Settings, Books and buying, tour tip). The mock trumps the build where they differ.

## Design (2026-11-15 Subscription mock)

`docs/design/dino-reader/Dino Reader 2026-11-15 Subscription.dc.html`, 14 frames, all UI (the books and covers in them are placeholder rows). Final string keys and English text for every frame: `docs/plan/notes/2026-11-17-dino-subscription-and-foldables-copy.md` (slices cite its keys and never invent wording). The built record above is not edited. The mock trumps the build where they differ; the slice named in each line builds it.

- **SB1-SB5, SB10 (Store card states, built by Slice 397).** SB1 Offer (new subscriber, with the "Save 37%" tag), SB2 Trial already used (plain prices), SB3 Subscribed in the free trial (state line, "Included" tags, Manage), SB4 Payment problem (grace, "Fix payment"), SB5 Ended. SB1-SB5 are phone frames 375 wide; SB10 is the Windows Store at 1100 x 760 (words left, a 300-wide plan column right; the same split on tablets and phone landscape). Yearly is the main button, monthly second; "Save {0}%" sits on the yearly plan (user, 2026-11-16).
- **RR1-RR4 (Restore and Redeem, Slice 396).** RR1 Restore from the Store's first line (and its in-progress state), RR2 Restored message, RR3 Nothing found with a Redeem action, RR4 the "Bought it already?" link on the 402 strip. The "Bought books before?" card at the foot of the Store goes (user, 2026-11-16).
- **SB6-SB8 (Slice 398).** SB6 the parents' Books tab after a lapse (banner with two links, locked tiles with two pills), SB7 the offline notice (end date passed, inside the 3 days), SB8 the child's shelf after a lapse (only unlocked books).
- **SB9 (Slice 399).** Settings with a "Books and buying" group: Subscription (state line), Restore purchases, Redeem a code, each with a helper line. The same page's end note on the corner tour: `Tour_TipStore` and the welcome card now name the subscription (the text is on the 2026-11-13 Onboarding page; user, 2026-11-16).
- **Foldable screens** that show the Store card and Settings (iPhone Duo, Galaxy Z Fold8, Fold8 Ultra) are in Slice 403.
- Canvas furniture, not built: the Windows window chrome in SB10 (minimise, maximise, close), the status bars and "9:41" clocks.

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

## Store setup (was "Waiting on the user"; nothing waits on the user now, 2026-11-15)

- Create the subscription and its two plans in Google Play Console, App Store Connect and Microsoft Partner Center,
  with the prices and trial above. `.github/scripts/store-products.mjs` should do as much as the store APIs allow;
  only what no API can set goes to the user, one step at a time with direct links. Plan: `store-products.mjs --apply`
  for Play and App Store Connect, Partner Center by hand. **Done 2026-11-15:** Play and App Store Connect (see below).
  **Open, Claude's work:** Microsoft Partner Center subscriptions are not created.
  **Deep links (Slice 393 item 17, ids read 2026-11-18 from the repo vars and earlier slice lines; none is a to-do for the user):**
  App Store Connect, Dino subscriptions https://appstoreconnect.apple.com/apps/6825053435/distribution/subscriptions (app 6825053435);
  Google Play Console, Dino https://play.google.com/console/u/0/developers/5892509739539100062/app/4974567301865424326/app-dashboard;
  Microsoft Partner Center, Dino product `9N9VHRLCZXN5` (`DINOREADER_STORE_APP_ID`) https://partner.microsoft.com/en-us/dashboard/products/9N9VHRLCZXN5/overview
  (the product page; the add-on subscriptions are created from its *Add-ons* area, which is where `store-products.mjs --store microsoft` would create the shells).
  **Can `store-products.mjs --apply` do the rest?** Play and App Store Connect: already applied 2026-11-15 (above), nothing left for `--apply` there.
  Microsoft: the script's Microsoft request shapes are still marked UNVERIFIED, and `--apply` changes live store state, so it was not run by Slice 393;
  run it once with `--store microsoft` and read its output first, as the script's own header says.
- Approve the listing text: void. The user ruled on 2026-11-15 that store text is written, checked and pushed without a
  review round (Slice 291, *User answers (2026-11-15, RTST App Store and review rounds)*).
- Submitting the two App Store subscriptions with an app version: **done 2026-11-15 by Claude** through the API, on the
  user's request "dino reader seems to be ready for ios release. do it for me": they are WAITING_FOR_REVIEW in review
  submission d259ffc8-0e09-43dd-ad88-67770f155ea1 with Dino 1.131.1 (Slice 291, *Dino App Store submission 2026-11-15*).

## Store listing switch (Slice 291 item 10, 2026-11-14)

- The live Play text is `docs/store-listings/dino/play.md` (four free, buy each book once). The subscription text
  is written in advance in `docs/store-listings/dino/play-subscription.md` and is pushed **only when this slice is
  live on Play** (same for the App Store and Microsoft versions, written from it then, with the EULA and privacy
  links Apple requires). The switch is the last work item here; record its date in Slice 291 item 10.
  **App Store switch done 2026-11-15** (the subscription text on all 16 localizations, sent with the review
  submission; recorded in Slice 291 item 10). Play and Microsoft: not recorded as switched.

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
- Skipped #5 (`reviews/pr-1174-check.md`): "src/Dino/DinoReader.Core/SubscriptionAccess.cs:105, low." - low: it affects the Trial label and the trial-end wording, not whether the reader is entitled.
- Skipped #6 (`reviews/pr-1174-check.md`): "worker/stories.ts:722, low. HEAD on /api/stories/subscription answers 200 whenever a source" - low: only a HEAD preflight is unreliable; the apps use GET, which answers 404/402 correctly.
- Skipped #8 (`reviews/pr-1174-check.md`): "the app calls it after every device read that reached the store; polling can starve book downloads with 429" - the route is called once after each store read, not polled (`BookPurchases.cs:140` is the only `StartEndCheck` call, inside `ReadAsync`; `ApplyEndAsync` is serialised by `_endGate`, `BookPurchases.cs:225`).
- Skipped #6 (`reviews/pr-1174-medium.md`): "askSubscription/askSubscriptionStatus and subscriptionOwns/subscriptionStatusOf (Apple: subscriptionProductOf/subscriptionResultOf/checkSubscription vs statusOfLast/bestStatus/checkSubscriptionStatus) duplicate URL, fetch, status mapping; can drift" - low refactor, not needed for correctness; left as is.
- Skipped #2 (`reviews/pr-1174-check3.md`): "A Store-kind entitled read always replaces a Worker-confirmed Expired record" - Decision 6: online the app follows the store's answer at once; Play's own entitled answer is that answer, and the Worker re-checks before serving any paid file.
- Skipped #5 (`reviews/pr-1174-check3.md`): "src/Dino/DinoReader.App/Platforms/iOS/Purchases/IosPurchases.cs:202, low. _subTrial is not persisted although _subEnds and _subNewest" - low: after a relaunch only the "trial ends" wording is lost; the finding says the lock is unaffected.
- Skipped #6 (`reviews/pr-1174-check3.md`): "src/Dino/DinoReader.App/Platforms/iOS/Purchases/IosPurchases.cs:262, low. NoteSubscription reads Products (plain Dictionary) from the queue" - low: the worst case the finding names is the trial not detected for one transaction, not a wrong lock.
- Skipped #3 (`reviews/pr-1174-check2.md`): "Trial estimate fires for any first Purchased tx with empty _subEnds; IntroductoryPrice is set for every user, not eligibility" - the local StoreKit 1 end date is used only until the first Worker answer (Design note); StoreKit 1 exposes no trial eligibility.
- Skipped #5 (`reviews/pr-1174-check2.md`): "src/Dino/DinoReader.App/Purchases/BookPurchases.cs:234, low (overlaps earlier medium #5 on StoreUnreachable). Any non-200" - low: it only adds noise to fallback analytics, and the earlier medium finding #5 on StoreUnreachable already covers the fix.
- Skipped #6 (`reviews/pr-1174-check2.md`): "src/Dino/DinoReader.App/Purchases/BookPurchases.cs:213, low. StartEndCheck queues a Task.Run per entitled read, serialized" - low: it only delays a fresh answer when the Worker is slow, and Merge discards the stale ones.
- Skipped #7 (`reviews/pr-1174-check2.md`): "worker/stories.ts:721, low. HEAD to subscription path returns 200 when a" - low: it only affects a HEAD probe and the missing Retry-After on 429; GET answers correctly.

- Skipped #1 (`reviews/pr-1174-check6.md`): "LatestEnd makes an iOS estimated entitled read end at least now, so a lapsed sub stays granted while the Worker gives no 200" - a Worker that gives no answer is treated as no internet, and Decision 6 "With no internet it locks those books 3 days after that date"; the Worker checks the store before serving any paid file, so no new paid book can be downloaded meanwhile.
- Skipped #4 (`reviews/pr-1174-check6.md`): "worker/entitlements/apple.ts:94, low-medium: gate (subscriptionProductOf/subscriptionResultOf) and status route (statusOfLast) duplicate the" - low-medium: a drift risk between two copies of the Apple rules, not a current wrong answer.
- Skipped #5 (`reviews/pr-1174-check6.md`): "src/Dino/DinoReader.Core/SubscriptionAccess.cs:138, low: Trial kept only when Worker end is within" - low: the finding itself says it is not a lock-out; only the Trial label is lost after the first Worker call.

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

**Live 2026-11-15:** `store-products.mjs --apply --store apple` (after #1198) created group "Dino Book Reader subscription" with `uk.sirous.dinoreader.sub.monthly` (1.99 GBP) and `.sub.yearly` (14.99 GBP), prices and 7-day free trials in all 175 territories, 0 failed. Claude then uploaded one App Store review screenshot to each through the API (the Dino phone shelf shot padded to 1242x2688, as for the books in Slice 346); both read back `READY_TO_SUBMIT`. The script's "by hand: review screenshot (no API)" line is wrong: `subscriptionAppStoreReviewScreenshots` and the IAP equivalent exist; fold the upload into the script. Submitting the subscriptions for review with an app version was first left to the user; **done 2026-11-15 by Claude through the API** (both WAITING_FOR_REVIEW in review submission d259ffc8-0e09-43dd-ad88-67770f155ea1 with Dino 1.131.1; both subscriptions' en-US description changed to "Opens every book while you subscribe."; see Slice 291, *Dino App Store submission 2026-11-15*). Page: https://appstoreconnect.apple.com/apps/6825053435/distribution/subscriptions . Play was created later the same day (below); Microsoft subscriptions are not created yet.

Follow-ups from the submission (Claude's work, not the user's):
- `store-products.mjs` sends a subscription description longer than Apple's limit, and Apple stored it cut off ("Opens every book in Dino Book Reader while"). The script should fail on over-long text and use "Opens every book while you subscribe." Its "review screenshot by hand" line stays wrong (noted above).
- Not yet verified: whether the app's subscription card shows each plan's name, length and price before buying (Apple requires it in the app).

Not yet verified (moved below the Apple section):
- Offline 3-day lock on a real device.
- Restore on iOS.
- The plans showing in the iOS app (sandbox/TestFlight) now that the products exist.

## iOS Manage subscription (2026-11-15)

Bug (user, TestFlight): "the subscription worked, but after, I tried to cancel it. it opened another external place where it showed that I have no subscriptions." Cause: iOS `ManageSubscriptionAsync` opened https://apps.apple.com/account/subscriptions, and a TestFlight app runs in the sandbox, which that page never lists.

Fix: Apple's in-app sheet `AppStore.showManageSubscriptions(in:)` (iOS 15+; the iOS 17 subscription-group overload is not used) through the new Swift export `dino_sk_manage_subscriptions` (Slice 390's wrapper; it answers `ok`, or `no-scene`, `unsupported` (iOS app on a Mac) or the StoreKit error). `IosPurchases.ManageSubscriptionAsync` returns true when the sheet was shown and then raises `PurchaseUpdates` so the status is read again (a plan change shows; a cancel does not change the card until the plan ends, because Apple keeps listing it as owned and the app has no "will not renew" state on any platform, Android included; review of 2026-11-15, finding 3, left as it is). The Swift export picks only a foreground-active (else foreground-inactive) scene; with none it answers `no-scene`. The web page opens only when the bridge is missing or the answer is `no-scene` or `unsupported`; any other failure returns false, `BookPurchases` reports a `Store`/`StoreFailed` fallback and the page shows `Store_ManageFailed`. A second tap while the sheet is open does nothing, and the wait ends after 10 minutes (the waiting entry is abandoned) so the grown-up guard always ends. Debug trace lines: `manage sheet shown`, `manage sheet failed`, `manage sheet unavailable`, `manage sheet gave no answer in time`. Android keeps the Play subscriptions link (it works for Play's own test purchases); Windows is unchanged.

From review (`reviews/s384m-medium.md`), follow-ups, not changed: #1 after the 10-minute timeout the sheet may still be open and its later close is not re-read (the next store read picks it up); #2 a repeat tap while the sheet is open does nothing visible; #3 on Android and Windows a false from the link opener now reports `StoreFailed` though no store failed (telemetry reason only); #4 a StoreKit error shows the failure message with no web fallback; #5 a `foregroundInactive` scene can be chosen during a transition; #6 the tap is ignored silently while another external activity runs.

**Verified by the user, 2026-11-15** (dino-v1.131.1 on TestFlight, "it worked"): Manage subscription opens Apple's sheet with the sandbox subscription, and cancelling there works. Also verified the same day: subscribing on iOS ("the subscription worked").

## Play subscription script review (2026-11-15)

From review (`reviews/s384p-high.md`), fixed: #1 a held base plan is activated only when its GB price equals the plan's (`playHeldGbPriceMatches`); #2 the trial offer's otherRegionsConfig only when the base plan has one; #3 a held plan with no regions stops that plan; #5 no conversion call on the held path (regions version = PLAY_REGIONS_VERSION or the default); #7 creation no longer stops on a missing GB conversion price (GB is set from the plan).

From review (`reviews/s384p-medium.md`), fixed: #1 Play's `convertRegionPrices` takes a tax-exclusive price and answers tax-inclusive ones (docs: "The intital price to convert other regions from. Tax exclusive." and "The converted price tax inclusive."), so the script converts 1.99 as is, reads GB's tax-inclusive answer and converts again from 1.99 divided by (1 + that VAT), which is what Play Console does for a typed GB base price; GB itself is set to 1.99, and an apply run prints GB, US, DE, JP, AU and CA prices per plan before the create call (a dry run makes no network call, so it cannot show them); #2 the held path takes the regions version from `PLAY_REGIONS_VERSION`, the held resource, or one conversion call; #3 the GB price check runs for ACTIVE plans too, before any offer; #4 a held trial offer that is INACTIVE or UNKNOWN is left alone and reported, only DRAFT is activated.
- Skipped #5 (`reviews/s384p-medium.md`): "gracePeriodDuration 'P7D' removed from the base plan with no reason or test; Play default grace applies" - this slice asks for no grace period (the app reads `grace` from the store's own state), so Play's default applies and nothing is sent.

From review (`reviews/s384p-high.md`), follow-ups, not changed: #4 "createPlayProduct (books) repeats the regions-version expression now held by playRegionsVersion; call the helper so they cannot drift. Behaviour unchanged." (low); #6 "Copy of the Apple retry wrapper using APPLE_-named constants; canRetryPlay resends a POST after 429 without requiring Retry-After, Apple's does. Share one store-neutral helper." (low).

From review (`reviews/s384p-check.md`): #1 (fresh create read Play's 404 body as a held plan, so the GB guard stopped both plans after the DRAFT POST) and #2 (no GB price in the conversion now stops before any write) fixed by the coordinator; #3 not changed: a held DRAFT "trial" offer is activated without checking its duration or targeting. Live reference read 2026-11-15 with a read-only convertRegionPrices call from the net of the GB price: monthly GB 1.99 GBP, US 2.19 USD, DE 2.39 EUR, JP 380 JPY, AU 3.59 AUD, CA 3.09 CAD; yearly GB 14.99, US 16.99, DE 17.99, JP 2900, AU 26.99, CA 23.99; regionVersion 2026/01.

**Play live 2026-11-15** (run by the user from the prompt, after #1201): `uk.sirous.dinoreader.sub` created with base plans monthly (GB 1.99 GBP, US 2.19 USD, DE 2.39 EUR, JP 380 JPY, AU 3.59 AUD, CA 3.09 CAD) and yearly (GB 14.99, US 16.99, DE 17.99, JP 2900, AU 26.99, CA 23.99), both activated; a 7-day free-trial offer for new subscribers in 174 regions on each, activated. The 11 books were left as they were. Microsoft Store subscriptions: not created (Partner Center, by hand).

## Subscription store listings in other languages (2026-11-15)

- Google Play: subscription `uk.sirous.dinoreader.sub` now has 17 listings (en-GB plus the 16 other languages of the Play store listing), pushed with `subscriptions.patch` (`updateMask=listings`, regionsVersion 2026/01) and read back identical; both base plans still ACTIVE, nothing else touched. Text: `docs/store-listings/dino/subscription-listings.md` (machine drafts from the app's `Sub_*` strings; native read not done).
- App Store Connect: not added. Apple answers 409 `ENTITY_ERROR.ATTRIBUTE.INVALID.UNMODIFIABLE` ("The field (NAME) can not be modified" for the group localization, "(LOCALE_CODE)" for the subscription one) because both subscriptions are Waiting for Review. Only en-US exists. Add the 15 locales from the same file once the subscriptions are editable.

- App Store localizations: the App Store text of the two subscriptions' en-US description was changed on 2026-11-15 (above); the other localizations below still wait for Apple.
- **Next (Claude, after Apple approves the subscriptions):** add the 14 other App Store localizations from `docs/store-listings/dino/subscription-listings.md` to group 22582310 and both subscriptions (Apple refused new localizations while they were Waiting for Review, 409 UNMODIFIABLE, 2026-11-15).
- Not yet verified: a native read of the machine-written subscription texts (Play live in 16 languages; App Store pending).
