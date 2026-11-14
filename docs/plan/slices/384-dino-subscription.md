# Slice 384 — Dino: a monthly and yearly subscription for every book, alongside buying single books
**Product:** Dino

*Planned.*

**Touches:** `src/Dino/DinoReader.Core/BookAccess.cs`, `src/Dino/DinoReader.Core/SubscriptionAccess.cs` (new), `src/Dino/DinoReader.Core/AppConfig.cs`, `src/Dino/DinoReader.Core/StoreCatalog.cs`, `src/Dino/DinoReader.App/Purchases/**`, `src/Dino/DinoReader.App/Platforms/Android/Purchases/**`, `src/Dino/DinoReader.App/Platforms/iOS/Purchases/**`, `src/Dino/DinoReader.App/Platforms/Windows/Purchases/**`, `src/Dino/DinoReader.App/StorePage.xaml*`, `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml*`, `src/Dino/DinoReader.App/BookUpdater.cs`, `src/Dino/DinoReader.App/ShelfAudioPacks*.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**`, `src/Dino/DinoReader.App/CLAUDE.md`, `worker/entitlements/**`, `worker/products.ts`, `worker/stories.ts`, `worker/appconfig.ts`, `worker/appconfig.test.mjs`, `worker/entitlements.test.mjs`, `worker/stories.test.mjs`, `worker/CLAUDE.md`, `.github/scripts/store-products.mjs`, `.github/scripts/store-products.test.mjs`, `tests/Dino/DinoReader.Tests/**`, `.maestro/dino/subscription.yaml` (new), `docs/store-listings/dino/**`, `site/dino/privacy.html`, `docs/plan/slices/384-dino-subscription.md`

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
  only what no API can set goes to the user, one step at a time with direct links.

## Work

Smallest end-to-end first: Android subscription unlocking one paid book, server-checked.

- [ ] **A. Core rules (pure, tested).** `SubscriptionAccess` (new): state from the store (active, trial,
      in grace, expired, unknown) plus last known end date, and the 3-day offline rule. `BookAccess` gains the
      per-book "unlocked by" record and answers, for each book: readable, locked-by-lapse (greyed for parents,
      hidden from the child), or buyable. Tests cover every row of Decisions 4-6 and the Slice 346 exceptions.
- [ ] **B. Worker check.** `worker/entitlements/*`: verify a subscription proof per store (Google subscriptions
      API, Apple App Store Server API transaction with `expiresDate`, Microsoft Store collections for subscription
      add-ons) and refuse an expired or revoked one. `products.ts`: the subscription products unlock every paid book.
      Same proof header, rate limits and error codes as Slice 346 (`worker/CLAUDE.md`). Tests in
      `worker/entitlements.test.mjs` and `worker/stories.test.mjs`.
- [ ] **C. Android.** Play Billing `ProductType.Subs` query, base plans and the trial offer, buy, acknowledge,
      read the active subscription and its end date, Restore. Builds on Slice 380's listener-only product details
      (do 380 first or together).
- [ ] **D. iOS.** StoreKit subscription purchase, trial, renewals and expiry from the transactions, Restore.
      Same behaviour and tests as Android.
- [ ] **E. Windows.** `Windows.Services.Store` subscription add-ons, trial, expiry, Restore.
- [ ] **F. Store page and shelf.** Store page: a subscription card (both plans, trial, terms, price from the
      store) above the books, behind the grown-ups gate; books still show their own price. Parents' screen:
      greyed-out lapsed books with Subscribe again / Buy this book. Child's shelf: lapsed books hidden. Manage
      subscription link in the grown-ups area. `BookUpdater` and `ShelfAudioPacks` send the subscription proof for
      subscription books. Every wait over 500 ms shows a loading state. Texts in all 15 app languages.
- [ ] **G. Store setup script.** `store-products.mjs` creates the subscription, plans, prices and trial through
      the Play and App Store Connect APIs (dry run by default); what Partner Center can't do by API is listed for
      the user.
- [ ] **H. Store listings and privacy.** Listings mention the subscription and its terms (through the
      `store-listing` agent, draft then apply after approval); `site/dino/privacy.html` unchanged in substance
      (no new data leaves the device) but names the subscription.
- [ ] **I. Device tests.** `.maestro/dino/subscription.yaml` with a fake subscription (Debug `FakePurchases`):
      subscribe, download a paid book, lapse it, check greyed on the parents' screen and gone from the child's shelf,
      renew, check it opens without a download; a bought book and a free book unchanged throughout.

## Verification

- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj`: `SubscriptionAccess` and `BookAccess` rows.
- `node --test worker/` for the entitlement and story-route tests.
- Android emulator and iOS simulator: `.maestro/dino/subscription.yaml` on both. Windows: the same flow on the
  Hyper-V VM.
- Real store test (after store setup): Play licence tester and an Apple sandbox account subscribe with the trial,
  the sandbox renewal speed-up shows a lapse locks and a renewal unlocks; a book bought earlier stays readable.
