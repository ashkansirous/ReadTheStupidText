# Slice 346 — Dino: four free books, the rest $0.99 each

*Planned (2026-11-09).*

**Touches:** `worker/stories.ts`, `worker/entitlements/**` (new: receipt checks per store), `worker/products.ts` (new: product to book list), `worker/CLAUDE.md`, `worker/stories.test.mjs`, `worker/entitlements.test.mjs` (new), `content/dino/*/book.json`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `.github/scripts/store-products.mjs` (new: create and price the products through the store APIs), `src/DinoReader.Core/BookAccess.cs` (new), `src/DinoReader.Core/StoryBackup*.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StorePage.xaml*`, `src/DinoReader.App/FirstBooksPage*.cs`, `src/DinoReader.App/BackupPage.xaml*`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/ShelfAudioPacks*.cs`, `src/DinoReader.App/Purchases/**` (new), `src/DinoReader.App/Platforms/Android/Purchases/**` (new), `src/DinoReader.App/Platforms/iOS/Purchases/**` (new), `src/DinoReader.App/Platforms/Windows/Purchases/**` (new), `src/DinoReader.App/Platforms/Android/AndroidManifest.xml`, `src/DinoReader.App/Platforms/iOS/Info.plist`, `src/DinoReader.App/Platforms/iOS/PrivacyInfo.xcprivacy`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/Testing/**`, `tests/DinoReader.Tests/**`, `docs/store-listings/dino/**`, `site/dino/privacy.html`, `docs/plan/slices/346-dino-paid-books.md`

## Why

The user wants four books free and every other book sold, so the books start paying for themselves. Today all 15
books are free and the story store hands any book to anyone who knows its link (`worker/stories.ts` has no check).
Slice 291 recorded in-app purchases as "not decided"; this slice decides them.

## Decisions (user, 2026-11-09, RefineScope)

1. **No accounts of our own.** A purchase lives in the parent's Apple, Google or Microsoft store account. The
   grown-ups area gets a **Restore purchases** button; on a new phone it brings back every book bought with that
   store account. No sign-up, no login, nothing personal sent by us. (Purchases don't carry across between Apple and
   Google; accepted.)
2. **Free books:** The Lion and the Mouse (`1ca50c6936cc44788b992726784e8132`), The Gingerbread Man
   (`bee62232085d4c1ab50cdf574530f473`), The Three Little Pigs (`966400017c494fb5b045786f53852c9e`), X Marks the
   Treasure (`a02c0731b7ac475a896e9184f3575e7b`). Every other book, and every book added later, is paid unless its
   `book.json` says free.
3. **$0.99 per book, one product per book, no bundles for now.** The user will add bundles later as products of their
   own. So the entitlement model is "a product unlocks a list of books" from day one: a per-book product is a list of
   one, and a later bundle is one more row, with no app update.
4. **The server checks the purchase.** The Worker hands out a paid book (its download, page audio and audio packs)
   only after it has confirmed the purchase with the store that sold it. Hiding books in the app alone is not enough.
   Buying sits behind the existing grown-ups gate (`GrownUpGate`).
5. **Backups hold no book content.** A backup holds only the app's own state (which books are on the shelf, by id;
   reading progress; settings) and the parents' own recordings. No book text, pictures or audio packs. Restore
   downloads the books and their audio again from the store: free books at once, paid books after Restore purchases
   confirms them. Old backups that carry `.dino` files still restore: their recordings and shelf list are used,
   their book files are ignored.
6. **Android, iOS and Windows in this slice** (Android and iOS parity rule; Dino's Windows head ships in the
   Microsoft Store too).

## Decided by Claude (obvious behaviour; tell the user, change if they disagree)

- **Books already on a phone stay readable.** Installs from before Slice 261 have Good Enough and Tortoise and Hare
  as old free starters, and any family may have downloaded paid books while everything was free. Nothing on the
  shelf is ever taken away or locked. Such a copy doesn't get store updates (`BookUpdater`) or come back from a
  backup unless it is bought. Never remove a book a family already has.
- **The first-run picker (`FirstBooksPage`, Slice 261) offers only the four free books**, and its minimum of 2 stays.
  Paid books show in the store with their price, behind the grown-ups gate.
- **The price shown is the store's own, localised price string** (from Play Billing, StoreKit or the Microsoft
  Store), never a "$0.99" written in the app or the Worker.

## Store rules (checked 2026-11-09)

From a web-research pass on 2026-11-09. "Not confirmed" means the vendor's own page wasn't read; check it again
through `library-docs` or `web-research` before building that part.

- **Apple: in-app purchase is required.** Guideline 3.1.1: "If you want to unlock features or functionality within
  your app ... you must use in-app purchase. Apps may not use their own mechanisms to unlock content." A restore
  mechanism is expected for restorable purchases. Apple Pay can't stand in for it.
  (https://developer.apple.com/app-store/review/guidelines/)
- **Apple: parental gate.** Required in the Kids Category (1.3). Dino is not in it (Slice 291), but purchases sit
  behind `GrownUpGate` anyway. Not in the Kids Category also means no "For Kids" wording in the listing (2.3.8).
  (https://developer.apple.com/app-store/kids-apps/)
- **Google: Play Billing is required** for digital goods
  (https://support.google.com/googleplay/android-developer/answer/10281818). Families-policy wording on purchase
  gates: not confirmed; the gate covers it either way.
- **Server checks.** Google: `purchases.products.get` (androidpublisher v3, scope `androidpublisher`), verified;
  service-account auth not confirmed. Apple: App Store Server API transaction history with an ES256 `.p8` key, not
  confirmed from Apple's page. Microsoft: the Store collections API still exists (Entra ID web app, Store ID key from
  `StoreContext.GetCustomerCollectionsIdAsync`, 30-day keys, needs a tenant global admin)
  (https://learn.microsoft.com/en-us/windows/uwp/monetize/view-and-grant-products-from-a-service).
- **$0.99:** commonly available on all three stores; not confirmed on any official price page.

## Out of scope

- **Alexa and Sonos: no work in this slice** (user, 2026-11-09: "focus on android and ios and windows ... we'll
  figure sonos and alexa later"). Paid books are locked on every route, the listening-track route they use
  (`worker/alexa.ts:252`) included. Whatever that does to them on its own (most likely they can no longer play paid
  books) is accepted for now; nothing is built or changed in `worker/alexa.ts` or `worker/sonos.ts` to adapt them.

## Work

### Part A — the Worker knows what is free and checks purchases

- [ ] `book.json` gets `"access": "free"` or `"paid"` (closed set, default paid) plus the product id
      `uk.sirous.dinoreader.book.<stem>`. `publish-store-books.mjs` carries both into the store's `meta.json`. The four
      free books are marked free.
- [ ] `worker/products.ts`: product id to the list of book ids it unlocks (one per paid book now).
- [ ] `/api/stories` lists every book with its access and product id. The download, audio and listening-track (`listen/<lang>`) routes for a
      paid book need a purchase proof header. The Worker checks it with the store that sold it (Part A2), caches a
      good answer for the device's request for a short time, and returns 402 for no proof and 403 for a bad one.
      Free books work exactly as today.
- [ ] The storage bucket stays closed: no r2.dev public URL and no custom domain on `readthestupidtext-downloads`
      (read live 2026-11-09). The Worker is the only door; keep it that way.
- [ ] A2: one checker per store under `worker/entitlements/` (Apple App Store Server API, Google Play Developer API,
      Microsoft Store collections API), each behind the same interface: proof in, owned product ids out. Their keys go
      through `manage-secrets` and `wrangler secret put`; nothing about the buyer is stored or logged. Tests with
      recorded store answers in `worker/entitlements.test.mjs`.

### Part B — buying and restoring in the app (Android, iOS, Windows)

- [ ] `BookAccess` (Core, pure): what a book's state is (free, owned, buyable, already on the shelf from before) and
      what the store page shows for it. Unit tests.
- [ ] `IBookPurchases` in `src/DinoReader.App/Purchases/`: load products and prices, buy, restore, current proof.
- [ ] Android task: Google Play Billing implementation (library chosen through `library-docs` first).
- [ ] iOS task: StoreKit 2 implementation (`library-docs` first).
- [ ] Windows task: `Windows.Services.Store` durable add-on implementation.
- [ ] `StorePage`: a paid book shows the store's price and a Buy button; buying opens the store's own sheet; when it
      succeeds the book downloads as today. A Restore purchases button sits in the grown-ups area. Every wait over
      500 ms uses the shared wait overlay. All new text in every app language (`NoHardCodedUiTextTests`).
- [ ] `StoreClient`, `BookUpdater` and `ShelfAudioPacks` send the purchase proof for paid books; a 402/403 for a book
      already on the shelf skips its update quietly and reports a fallback event, never removing the book.
- [ ] `FirstBooksPage` offers only free books.
- [ ] Debug test hook to fake an owned product (Testing/), so device flows run without real money.
- [ ] **Store promo codes work** (user asked 2026-11-09 how to give books to themselves and friends). Codes come only
      from the stores; our own codes are not allowed on iOS (3.1.1: "Apps may not use their own mechanisms to unlock
      content"). Apple offer codes cover non-consumables (up to 10 active offers, 1 million codes a quarter, redeemed by
      URL, in the App Store or in the app); Play promo codes (500 a quarter for one-time products, redeemed in the Play
      Store or the app; "your app needs to properly handle redemptions that occur outside of your app"); Microsoft
      promotional codes for add-ons (1600 per six months, redeemed by URL). So: the app re-reads what the parent owns
      when it opens and when the store page opens, and a book bought or redeemed elsewhere shows as owned without
      pressing Restore. A **Redeem a code** button in the grown-ups area opens each store's own redeem screen (Android
      and iOS tasks; Windows opens the Microsoft redeem page). Sources: developer.apple.com/help/app-store-connect/
      manage-in-app-purchases/create-offer-codes-for-in-app-purchases/, developer.android.com/google/play/billing/promo,
      learn.microsoft.com/en-us/windows/apps/publish/generate-promotional-codes.

### Part C — backups hold state, not books

- [ ] `StoryBackup`: a new backup is `backup.json` (schema version up) with the shelf list (book ids), progress and
      settings, plus the recordings. No `.dino` files.
- [ ] Restore: put the recordings and state back, then download every listed book that isn't on the phone (free at
      once; paid after the store confirms ownership; the rest listed on the restore page as "buy or restore to get
      back"). Old backups: keep reading recordings and the shelf from them, ignore their book files.
- [ ] Unit tests for old and new backups; `BackupPage` text in every language.

### Part D — stores, listings and privacy

- [ ] `.github/scripts/store-products.mjs`: create one product per paid book at the $0.99 price point in App Store Connect,
      Play Console and Partner Center through their APIs where an API exists; the parts with no API go to
      *Outstanding work* as user steps, one by one.
- [ ] User-only: accept the Paid Apps agreement and fill bank and tax details in App Store Connect; set up a Google
      Payments merchant profile in Play Console; Microsoft payout and tax profile in Partner Center.
- [ ] Replace every "no in-app purchases" and "no account" line in `docs/store-listings/dino/*` and
      `site/dino/privacy.html`; say purchases go through the store and we keep nothing about the buyer. Check the
      `AndroidManifest.xml`, `Info.plist` and `PrivacyInfo.xcprivacy` network comments match what the app now sends.
      Push the listings through the `store-listing` agent (draft, then apply after the user approves). Update Slice
      291's "not decided" line to point here.

## Verification

- Worker tests: free book downloads without proof; paid book 402 without proof, 403 with a bad one, 200 with a good
  one for each store (recorded answers); a bundle row unlocks every book in its list.
- `dotnet test tests/DinoReader.Tests`: `BookAccess`, old and new backup restore.
- Android emulator: buy a paid book with a Play test account (licence tester), it downloads; clear data, Restore
  purchases, it comes back; first-run picker shows only the four free books.
- iOS: the same with a StoreKit test configuration and a sandbox account on TestFlight.
- Windows (Hyper-V VM): buy and restore with a Store test add-on.
- Backup made on one install, restored on a clean one: recordings back, free books downloaded, paid books listed
  until restored.
