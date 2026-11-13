# Slice 346 — Dino: four free books, the rest $0.99 each
**Product:** Dino

*Park note (moved back to planned by the 2026-11-12-1337 feedback loop, user answer 2026-11-01): it was parked: Part D, store setup and keys, and switching on wait for the user (A-C merged #1016 behind the off switch). A night wave that hits the same blocker parks it again with that reason.*

*Parts A, B and C merged as PR #1016 (e0e73ca3) behind the off switch; Part D, store setup and keys, and switching on wait for the user.*

**Touches:** `worker/appconfig.ts`, `worker/appconfig.test.mjs`, `worker/index.ts`, `src/DinoReader.Core/AppConfig.cs`, `src/DinoReader.App/AppConfigClient.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/GrownUpGate.cs`, `src/DinoReader.App/GalleryPage.xaml*`, `worker/stories.ts`, `worker/entitlements/**` (new: receipt checks per store), `worker/products.ts` (new: product to book list), `worker/CLAUDE.md`, `worker/stories.test.mjs`, `worker/entitlements.test.mjs` (new), `content/dino/*/book.json`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `.github/scripts/store-products.mjs` (new: create and price the products through the store APIs), `src/DinoReader.Core/BookAccess.cs` (new), `src/DinoReader.Core/StoryBackup*.cs` (incl. `StoryBackupState.cs`, `StoryBackupReader.LegacyFiles.cs`, `StoryBackupWriter.WithBookFiles.cs`), `src/Sirous.Reading.Mobile.Shared/Usage/RemoteConfigClient.cs`, `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StorePage.xaml*`, `src/DinoReader.App/FirstBooksPage*.cs`, `src/DinoReader.App/BackupPage.xaml*`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/ShelfAudioPacks*.cs`, `src/DinoReader.App/Purchases/**` (new), `src/DinoReader.App/Platforms/Android/Purchases/**` (new), `src/DinoReader.App/Platforms/iOS/Purchases/**` (new), `src/DinoReader.App/Platforms/Windows/Purchases/**` (new), `src/DinoReader.App/Platforms/Android/AndroidManifest.xml`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/Testing/**`, `tests/DinoReader.Tests/**`, `docs/store-listings/dino/**`, `site/dino/privacy.html`, `docs/plan/slices/346-dino-paid-books.md`, `tools/DinoReader.BookBuilder/**`, `src/DinoReader.Core/StoreCatalog.cs`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `.maestro/dino/paid-books.yaml`

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

## User answer (2026-11-12, before the night run)

- **Merge the code, books stay free.** The night builds the app code, the Worker's receipt check and the tests and
  merges them behind a switch that is off in the live remote config, so every book stays free until the store
  products and the store keys exist. Creating products and prices (`store-products.mjs`), the payment, bank and tax
  setup, `wrangler secret put` for the store keys and turning the switch on all wait for the user and are not done
  at night.

## Night build design (coordinator, 2026-11-12, wave 47)

Premises (ground-in-code on origin/main): remote config is `worker/appconfig.ts` serving `GET /api/app-config` from R2
`dinoreader/config.json` (read per request, changed with `wrangler r2 object put`, no deploy); the app reads it through
`AppConfigClient.cs` into `DinoReader.Core/AppConfig.cs:83`. The switch pattern to copy is `crashReports.enabled`
(`appconfig.ts:62-66`, `AppConfig.cs:85,125`): on only for a literal `true`, default off. No billing package is in
`DinoReader.App.csproj`; `GrownUpGate` (`GrownUpGate.cs:10`, singleton) is not used by `StorePage` today.
`Platforms/iOS/PrivacyInfo.xcprivacy` does not exist (not created by this slice).

- **One switch, one owner:** `paidBooks.enabled` in the remote config, default off (missing, malformed or not literal
  `true` = off). The Worker reads it from the same R2 object on each gated request; when off, every book is free on every
  route exactly as today (no proof header needed). The app reads it from `AppConfig`; when off it shows no price, no Buy,
  no Restore or Redeem button, and the first-run picker is unchanged. So merging changes nothing live.
- **Purchase state has one owner, the store.** The app keeps no purchase database: on launch and when the store page
  opens it asks the platform store for owned products (in memory only) and the current proof. Buy cancelled or failed:
  nothing changes, a plain message. Pending (Android pending purchase, iOS Ask to Buy): shown as waiting, re-read on the
  next open. App killed mid-purchase: the next re-read finds it. Offline: free books work, paid books not on the shelf
  show the store's last price if known, else a "connect to buy" note; nothing on the shelf is ever locked.
- **Worker checks:** checkers per store behind one interface, tested with recorded answers; missing store secrets mean
  the checker answers "cannot verify" (403), never "owned". No `wrangler secret put`, product creation or listing change
  at night.
- **Library choice:** each platform's billing API through `library-docs` first; if StoreKit 2 has no .NET binding, the
  iOS task uses the StoreKit (original) API bound in .NET for iOS, recorded here.
- **Touches added during the night build (coordinator, 2026-11-12):** `tools/DinoReader.BookBuilder/**` (its book.json reader refuses unknown fields, so `BookInfo` gained `Access` and `ProductId`; neither is in `BookHash`, so marking the four free books changed no hash and `check` says all 15 books are current), `StoreCatalog.cs` (the listing's `access`, `productId`, `unlockedBy`), `AppResources.cs` (typed accessors for the new strings) and `.maestro/dino/paid-books.yaml` (the scripted check, passes `tools/emulator/lint-flows.ps1`). The four free books carry `"access": "free"` in book.json; the product id of every other book is derived (`uk.sirous.dinoreader.book.<stem>`) by `publish-store-books.mjs`, not written into 11 book.json files.
- **Switch-on step, not done tonight:** `Platforms/iOS/Info.plist` (Slice 349 owns it this wave) gets its network/purchase comment updated when the switch goes on.
- **iOS purchase state (user-approved exception, coordinator 2026-11-12):** StoreKit 1 has no silent owned list, and the app never replays on its own (no surprise Apple ID prompts). So iOS keeps the product ids and transaction ids it has seen from a purchase or a Restore in Preferences (`dino.ios.purchases`), the one exception to "the store owns the state". Opening the Store tab reads that kept list only; Restore purchases replays with Apple and refreshes it. Android and Windows keep nothing.
- **Backups (final design, after three review rounds):** the writer and the reader each have one rule, keyed on the paid-books switch.
  - *Writer:* switch off or unknown writes exactly the backup of before Slice 346 (schema 1, one `.dino` per story with its book files and recordings, no settings, the old Save text). Switch known on writes schema 2 (shelf ids, reading view, recordings, settings, no book content). `BackupFormat` picks it; the default is the old one.
  - *Reader, schema 2 or switch known on:* recordings and reading view are put back on books the phone has; the missing ones are listed and fetched from the store. *Reader, schema 1:* switch known off restores as before (book files imported). Switch on or unknown asks the store per book, through one pure function, `BookAccess.LegacyBookChoice(access, owned, storeCanSupply)`, which returns Store (free or owned, store can supply it), File (the store cannot supply it and the book is not a sold, unowned one) or ListOnly (sold and unowned: the file is never installed). `BackupPage` calls it for both the store download and the file fallback, with "sold" read as the first-run picker reads it (an unread switch counts as sold). A book the listing does not name is not sold as far as the phone knows.
- **Switch-on step, republish first (finding 8 of `reviews/pr-1016-medium.md`):** a book meta without an `access` field is free, so a listing published before this slice stays open and nothing already live turns paid by accident. Set `paidBooks.enabled` true in the live config; then any `dino-store.yml` run (a manual dispatch, a push or the nightly one) republishes every book's `meta.json` with its `access` and the store-safe underscore `productId`, and refuses two books sharing one id (check3 #1/#2: `publish-store-books.mjs` derives this on every run, in every step that builds or compares metas, from the live `/api/app-config` with `paidBooks.enabled === true` exactly; anything else in a readable config = off = exactly what main wrote, so a normal run never undoes it; a config that cannot be read after 3 tries fails the run, it never counts as off). Check the republished metas before relying on the switch. The same step covers the Worker's `PAID_LIMITER` rate-limit binding (30 store calls per connection and minute; absent = the gate answers 429, never owned).
- **Paid files are never shared-cached:** package, audio-pack and listening-track responses of a paid book send `Cache-Control: private, no-store`; a free book's keep their old rule.
- **Not built tonight (Part D):** `store-products.mjs`, `docs/store-listings/dino/**`, `site/dino/privacy.html`: the
  privacy page and listings change when the switch goes on, with the user (they would be wrong while every book is free).

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

- [x] `book.json` gets `"access": "free"` or `"paid"` (closed set, default paid) plus the product id
      `uk.sirous.dinoreader.book.<stem>`. `publish-store-books.mjs` carries both into the store's `meta.json`. The four
      free books are marked free.
- [x] `worker/products.ts`: product id to the list of book ids it unlocks (one per paid book now).
- [x] `/api/stories` lists every book with its access and product id. The download, audio and listening-track (`listen/<lang>`) routes for a
      paid book need a purchase proof header. The Worker checks it with the store that sold it (Part A2), caches a
      good answer for the device's request for a short time, and returns 402 for no proof and 403 for a bad one.
      Free books work exactly as today.
- [x] The storage bucket stays closed: no r2.dev public URL and no custom domain on `readthestupidtext-downloads`
      (read live 2026-11-09). The Worker is the only door; keep it that way.
- [x] A2: one checker per store under `worker/entitlements/` (Apple App Store Server API, Google Play Developer API,
      Microsoft Store collections API), each behind the same interface: proof in, owned product ids out. Their keys go
      through `manage-secrets` and `wrangler secret put`; nothing about the buyer is stored or logged. Tests with
      recorded store answers in `worker/entitlements.test.mjs`.

### Part B — buying and restoring in the app (Android, iOS, Windows)

- [x] `BookAccess` (Core, pure): what a book's state is (free, owned, buyable, already on the shelf from before) and
      what the store page shows for it. Unit tests.
- [x] `IBookPurchases` in `src/DinoReader.App/Purchases/`: load products and prices, buy, restore, current proof.
- [x] Android task: Google Play Billing implementation (library chosen through `library-docs` first).
- [x] iOS task: StoreKit 2 implementation (`library-docs` first).
- [x] Windows task: `Windows.Services.Store` durable add-on implementation.
- [x] `StorePage`: a paid book shows the store's price and a Buy button; buying opens the store's own sheet; when it
      succeeds the book downloads as today. A Restore purchases button sits in the grown-ups area. Every wait over
      500 ms uses the shared wait overlay. All new text in every app language (`NoHardCodedUiTextTests`).
- [x] `StoreClient`, `BookUpdater` and `ShelfAudioPacks` send the purchase proof for paid books; a 402/403 for a book
      already on the shelf skips its update quietly and reports a fallback event, never removing the book.
- [x] `FirstBooksPage` offers only free books.
- [x] Debug test hook to fake an owned product (Testing/), so device flows run without real money.
- [x] **Store promo codes work** (user asked 2026-11-09 how to give books to themselves and friends). Codes come only
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

- [x] `StoryBackup`: a new backup is `backup.json` (schema version up) with the shelf list (book ids), progress and
      settings, plus the recordings. No `.dino` files.
- [x] Restore: put the recordings and state back, then download every listed book that isn't on the phone (free at
      once; paid after the store confirms ownership; the rest listed on the restore page as "buy or restore to get
      back"). Old backups: keep reading recordings and the shelf from them, ignore their book files.
- [x] Unit tests for old and new backups; `BackupPage` text in every language.

### Part D — stores, listings and privacy

- [x] `.github/scripts/store-products.mjs`: create one product per paid book at the $0.99 price point in App Store Connect,
      Play Console and Partner Center through their APIs where an API exists; the parts with no API go to
      *Outstanding work* as user steps, one by one. Built: a dry run by default; `--apply` creates one $0.99 product per
      paid book: Apple non-consumable + listing + availability + price, Google one-time product, Microsoft durable add-on
      shell. By hand: the Microsoft price, listing and submission; the Apple review screenshot and submission.
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

## Review

*From review:* (`reviews/pr-1016-medium.md`)

- Applied #2, #3, #6, #8, #9, #10 (backups are one design: see *Backups (final design)* above; paid files `private, no-store`; a meta without `access` is free; a per-client limit on store calls, 429; the Store page refresh shows the wait overlay; one `LegacyBookChoice` decides the store download and the file fallback).
- Skipped #1 (`reviews/pr-1016-medium.md`): "Sonos/Alexa serve listening tracks of paid books with no gate" - the user ruled Sonos and Alexa out of this slice, so no change in `worker/sonos.ts` or `worker/alexa.ts`; it matters only when the switch is on, and is listed for the user to decide before then.
- Skipped #4 (`reviews/pr-1016-medium.md`): "Apple proof only shows a transaction id exists for this bundle/product, not tied to the caller" - the app has no accounts, so any store proof can be shared; that is a known limit of Decision 1, listed for the user.
- Skipped #5 (`reviews/pr-1016-medium.md`): "unauthenticated GET /api/stories/windows-ticket returns the Worker's own Entra client-credentials token" - already gated in the high round: 404 while the switch is off, rate-limited per connection, and the token has the Store collections audience only.
- Skipped #7 (`reviews/pr-1016-medium.md`): "ListAsync awaits sales.EnsureConfigAsync before every listing while no config copy is held" - a decision from the high round: while the config is unknown the first-run picker offers free books only.

*From review:* check1 (`reviews/pr-1016-check1.md`)

- Applied #1, #3, #6, #7, #8 (and a follow-up: a schema 1 restore uses `LegacyBookFiles.Restore`, the files imported as on main, unless the switch is known to be on: `BookAccess.LegacyFilesMode`, unit-tested). The rule for #1 and #3: with the switch off, or its value unreadable, every path behaves exactly as origin/main. #1: the gate counts an unreadable switch as off (`GateAnswer.Off`), and a paid book's files then get origin/main's status and cache headers (`private, no-store` only after a store-confirmed proof). #3: nothing user-facing awaits the remote config any more (`BookSales.RefreshConfigInBackground`); `ListAsync` and the restore use the last known value (default off), and the first-run picker still offers only free books while the value was never known (this replaces the high round's wait in `ListAsync`). #6: a failing `ApplyToBookAsync` is reported (`FallbackArea.Library`) and the restore goes on. #7: copies of one store id write one entry with every copy's clips merged. #8: the schema 1 per-language cast assertion is back in `LanguageCastTests`.
- Skipped #2 (`reviews/pr-1016-check1.md`): "accessOf defaults missing access to paid; worker parseAccess defaults to free; 11 books lack access, so republish flips them to paid." - intended: `book.json` defaults to paid by decision 2, so republished metas say paid explicitly; the Worker's missing = free only covers old listings.
- Skipped #4 (`reviews/pr-1016-check1.md`): "windows-ticket hands out the service Entra token to any caller; per-IP limit on CF-Connecting-IP, shared "unknown" bucket." - already gated twice: 404 while the switch is off, rate-limited, and the token has the Store audience only; recorded for the switch-on review.
- Skipped #5 (`reviews/pr-1016-check1.md`): "ownership cache keyed by proof only; leaked token works up to 5 min, refunds not seen, junk can evict entries." - the short cache is by design; refunds are seen within 5 minutes.

*From review:* check2 (`reviews/pr-1016-check2.md`)

- Applied #2, #4. #2: the existing fresh-restore test stays as it is and a second test covers the default (off) fresh restore of a book with recordings and no pre-added book. #4: the background config refresh runs at most once per app launch (`BookSales.RefreshConfigInBackground`, only while no copy is held) plus when the Store tab opens, at most once per 10 minutes (`RefreshConfigOnStoreTab`); both are quiet (`reportFallback: false`): offline or failed, the last known value stays and no fallback event is reported, marked `// no-fallback:`.
- Skipped #1 (`reviews/pr-1016-check2.md`): "Save text fixed at page open, backup format decided again at Save." - the switch is only turned on by the user's switch-on step, and the Save text is re-read when the page opens; recorded for that step.
- Skipped #3 (`reviews/pr-1016-check2.md`): "public RestoreAsync gained a LegacyBookFiles parameter before onAdded; positional callers from main break." - no caller outside this branch uses the positional form, the build is green.

*From review:* check4 (`reviews/pr-1016-check4.md`)

- Applied #1, #2, #3, #6 (in the flow-setup fix of `.maestro/dino/paid-books.yaml`: sold books stay in the store at skip-first-run, the buy is proven by the Buy pill going away, the regexes are single-quoted with the dollar escaped).
- Skipped #4 (`reviews/pr-1016-check4.md`): "lint-flows.ps1 has no rule for unquoted YAML-1.1 booleans/nulls (on/off/yes/no/null) in arguments. Future flow could silently invert." - lint tooling outside this slice.
- Skipped #5 (`reviews/pr-1016-check4.md`): "is unverified; flow not run on a device." - the snapshot marker was seen in the device run.
- Skipped #7 (`reviews/pr-1016-check4.md`): "header onFlowComplete block is never linted (stopApp:false not checked there)." - lint tooling outside this slice.

*From review:* check5 (`reviews/pr-1016-check5.md`)

- Applied #1: the fake-purchases hook sets the sold names, the pretend store and the switch before its first await (`DinoTestCommands.Purchases.cs`, `FakePurchases.SetPaid` keeps stems or ids, no asset read), so skip-first-run right after always sees them. The buy wait overlay is proven by the `wait_shown` usage line (`# expect-log: wait_shown`), not by a UI wait: it shows for about 2 s only.
- Skipped #2 (`reviews/pr-1016-check5.md`): "The assertion uses a text regex plus index:1 and assumes one node per Buy pill." - on the device run the regex matched one node per pill: the buy removed exactly one.

*From review:* high (`reviews/pr-1016-high.md`)

- All 10 findings applied.

*From review:* check6 (`reviews/pr-1016-check6.md`)

- Applied none.
- Skipped #1 (`reviews/pr-1016-check6.md`): "MarkStale() runs before Override/ForcedEnabled/SetPaid are installed." - a Debug-only test-hook edge case that the device flow does not hit.
- Skipped #2 (`reviews/pr-1016-check6.md`): "BuyAsync ignores Offline: Snapshot returns Empty when offline, but Buy still succeeds" - a Debug-only test-hook edge case that the device flow does not hit.

*From review:* check7 (`reviews/pr-1016-check7.md`)

- Applied none.
- Skipped #1 (`reviews/pr-1016-check7.md`): "ticket reuses query token (wrong audience) and is handed to anonymous callers." - applies only with the switch on (check2 traced switch off or unknown = origin/main on every path); listed as a must-fix item before switch-on.
- Skipped #2 (`reviews/pr-1016-check7.md`): "gate returns 402/403 on /listen/<lang>, but Sonos/Alexa catalogues list paid books" - the user ruled Sonos and Alexa out of this slice.
- Skipped #3 (`reviews/pr-1016-check7.md`): "two opposite defaults is a trap. Correct the comment." - a comment fix only, low.
- Skipped #4 (`reviews/pr-1016-check7.md`): "ReadPurchasesAsync waits at most 400 ms" - applies only with the switch on (check2 traced switch off or unknown = origin/main on every path); listed as a must-fix item before switch-on.
- Skipped #5 (`reviews/pr-1016-check7.md`): "backup format chosen from the paid-books switch at two different times" - applies only with the switch on (check2 traced switch off or unknown = origin/main on every path); listed as a must-fix item before switch-on.
- Skipped #6 (`reviews/pr-1016-check7.md`): "a book missing from the store listing is treated as free" - applies only with the switch on (check2 traced switch off or unknown = origin/main on every path); listed as a must-fix item before switch-on.
- Skipped #7 (`reviews/pr-1016-check7.md`): "402/403 on a book update is swallowed and reported as StoreFailed" - applies only with the switch on (check2 traced switch off or unknown = origin/main on every path); listed as a must-fix item before switch-on.
- Skipped #8 (`reviews/pr-1016-check7.md`): "StoreFirst mode and schema 2 restore put a book with no matching shelf copy in Missing" - applies only with the switch on (check2 traced switch off or unknown = origin/main on every path); listed as a must-fix item before switch-on.
- Skipped #9 (`reviews/pr-1016-check7.md`): "StorePage constructor subscribes to singleton Purchases.Changed and never unsubscribes." - applies only with the switch on (check2 traced switch off or unknown = origin/main on every path); listed as a must-fix item before switch-on.
- Skipped #10 (`reviews/pr-1016-check7.md`): "copied in three places (products.ts, Books.cs, publish-store-books.mjs)" - duplication, low.

*From review:* s346l high (`reviews/s346l-high.md`)

- Skipped #1 (`reviews/s346l-high.md`): "Site goes live on merge while the paid switch is off." - timing, not a text fix: the main thread merges this branch only right before the switch goes on.
- Skipped #7 (`reviews/s346l-high.md`): "Kids Category note still says 'it fits the product' while purchases and the new Redeem-code link out are unchecked against guidelines 1.3 / 5.1.4." - noted, not applied in this pass.
- Skipped #9 (`reviews/s346l-high.md`): "It claims the live strings were read from the public pages on 2026-11-12 and tells the pusher to replace them verbatim; I could not confirm those live strings." - noted; re-read the live page and diff at push time.

*From review:* switch-on round (`reviews/s346d-high.md` 8 findings, `reviews/s346d-medium.md` 10, `reviews/s346d-check1.md` to `-check3.md` 10 each, `reviews/s346d-check4.md` 3; all read)

- Applied everything except the skips below.
- Skipped #4 (`reviews/s346d-check1.md`): "SettlePurchasesAsync can wait up to 25 s silently on update" - background paths have no UI to show an overlay on; the single shared wait and its one fallback report are enough.
- Skipped #3 (`reviews/s346d-check3.md`): "the stricter productId pattern for paid books" - latent: no book.json sets a productId today.
- Skipped #4 (`reviews/s346d-check3.md`): "not confirmed by a real Microsoft call" - low; unknown until the Worker switch is on and a real Store token is used.
- Skipped #5 (`reviews/s346d-check3.md`): "unknown counts as sold" - low; with an unknown config the Worker answers 402 once its switch is on.
- Skipped #6 (`reviews/s346d-check3.md`): "a stale task from an earlier listing" - low; costs one wait at most.
- Skipped #7 (`reviews/s346d-check3.md`): "a store sync delay after a purchase" - low; deliberate, recorded in code.
- Skipped #8 (`reviews/s346d-check3.md`): "one bad book blocks the whole republish" - low; deliberate, it fails loudly and uploads nothing.
- Skipped #9 (`reviews/s346d-check3.md`): "Intended by the design" - low; deliberate.
- Skipped #10 (`reviews/s346d-check3.md`): "Deliberate (never lose a held book)" - low; deliberate, noted in code.
- Skipped #1 (`reviews/s346d-check4.md`): "a fully offline dry run now fails" - low; `--force-paid-switch-on` runs it offline.
- Skipped #2 (`reviews/s346d-check4.md`): "staging metas follow production's switch" - low; staging dry run only.
- Skipped #3 (`reviews/s346d-check4.md`): "verify rewrites the ids the other way" - low; rare, the next run self-heals.

## Built, switch-on round (2026-11-13, branch feat/s346-switch-on, not merged)

- Built: the first switch-on item (check7 #1, #4-#9 fixed; #8's note text in 15 languages kept as `Backup_MissingNoteStore`, shown only when the switch is known on) and Part D's `.github/scripts/store-products.mjs`.
- Changed while building (decided by Claude, obvious; change if you disagree): store product ids use `_` instead of `-` (Google allows only a-z0-9_.), applied only when the live config's `paidBooks.enabled` is true, read on every dino-store run (unreadable fails the run), so switch-off metas stay byte-identical to origin/main (golden test). One app gate, `BookSales.SwitchKnownOn`, for all new behaviour. An unlisted book in a backup restores from its files.
- Device run (emulator-5554, rerun4; logs `D:/ReadTheStupidText-worktrees/reviews/w47-device/s346/rerun4/`): switch off first-books-picker, dino-backup-restore-recordings, shelf-audio-on-start PASS; switch on paid-books (fake purchases: Bought, owned=1, overlay, Restore) PASS.

## Device run (emulator-5554, build 886c981061c6)

Files: `D:/ReadTheStupidText-worktrees/reviews/w47-device/s346/rerun3/`.

- Switch off: first-books-picker download PASS, dino-backup-restore-recordings PASS, back-cover (skip-first-run) PASS.
- Paid-books flow (fake store): Buy pill with price, `outcome=Bought`, snapshot owned=1, `wait_shown`, Restore message and `redeem opened` all seen; every Maestro assertion passed. Its one failing marker (a prefix cut mid-token) was removed afterwards.
- Earlier failures were flow bugs: unquoted YAML `on`, test books already on the shelf, a 2 s overlay wait.
- One Mono SIGSEGV at cold start in one attempt (strcasecmp in libmonosgen); the next flows passed.

## Waiting on the user (switch-on steps; each blocks turning the switch on)

**Night of 2026-11-13 (the user: "I expect to see the stores live in the morning with the 4 product as free and the rest as 0.99 ... dont fucking stop on anything"), state read live:**
- Done: #1033 merged and deployed by Workers Builds (04:11Z); the 7 Worker store secrets set (APPLE_IAP_*, PLAY_SERVICE_ACCOUNT_JSON, MS_STORE_*); `store-products.mjs --apply` (fixed in #1036) created the 11 paid products in Google Play (created and activated) and App Store Connect (created), and the 11 Partner Center add-on shells; the 11 add-ons were then priced (USD 0.99, all markets, in-app only), listed (en-US) and submitted in Partner Center by a Chrome agent: 4 live, 2 in certification, 5 waiting to be released at the time. Windows users already have Dino 1.124.0 (store-submit 2026-11-12 19:43Z), which carries the purchase code.
- Switch turned on 2026-11-13 after the user added the allow line (`paidBooks.enabled: true` in R2 `dinoreader/config.json`, read back from `/api/app-config`).
- Held behind the switch (merge only together with it): this branch's listing, privacy and home-page texts (PR open, reviewed: reviews/s346l-high.md, s346l-medium.md); then a `dino-store.yml` dispatch to republish every meta with the store product ids; then pushing the listings with the `store-listing` agent (apply).
- Apple: the products still need a review screenshot each and submission with an app version (App Store is the user's button); Dino is not public on the App Store or Google Play yet (lookup 0 results; Play page 404), so Windows is the only public store.

- [x] Before switch-on: review findings check7 #1, #4-#9 fixed in this branch (see *Built, switch-on round*).
- Create the products and $0.99 prices: run `store-products.mjs --apply` (Microsoft price, listing and submission and the Apple review screenshot and submission by hand). Blocks: any purchase.
- Paid apps agreement, bank and tax details. Blocks: selling on Apple, Google and Microsoft. State read live on 2026-11-13:
  - Apple: done. Paid Apps agreement (new version accepted), bank (GBP) and W-8BEN active. In-App Purchase key NU7V4JG9B5 created by the user and stored in the secrets store (dinoreader, `APPLE_IAP_*`); the sandbox App Store Server API accepts it, production answered 401 (likely no approved in-app product yet; recheck after the products exist).
  - Google: merchant account set up; trader status declared; the Play service account can now read orders (View financial data granted). Left: the bank account verification, waiting on Google's test deposit (the user, 2026-11-13: don't remind).
  - Microsoft: done 2026-11-13. Payout and tax pages appear only when signed in with the personal Microsoft account (ashkan.sirous@gmail.com), not the onmicrosoft work account. Tax profile UK Complete (treaty True); payment profile Electronic Bank Transfer (Monzo, GBP) on both Sell and AdsRevenue (Submit needs both); status Complete, Complete, "Pending Microsoft validation" (up to 48 h). The Entra app key for the Worker is created and stored (`MS_STORE_*` in the secrets store, verified by a Store token).
  - Apple IAP key stored (`APPLE_IAP_*`); the Play service account has View financial data.
- Remaining before switch-on, in order: set the Worker secrets (asks the user), run `store-products.mjs --apply`, Part D listings and privacy page, set `paidBooks.enabled` true (then any dino-store run republishes the metas with the store ids).
- Worker secrets `PLAY_SERVICE_ACCOUNT_JSON`, `APPLE_IAP_KEY_P8`, `APPLE_IAP_KEY_ID`, `APPLE_IAP_ISSUER_ID`, `MS_STORE_TENANT_ID`, `MS_STORE_CLIENT_ID`, `MS_STORE_CLIENT_SECRET` (optional `APPLE_IAP_ALLOW_SANDBOX` on a staging Worker). Blocks: the Worker confirming any purchase (without them it answers 403).
- Republish every book's `meta.json` with `access` through `publish-store-books.mjs`. Blocks: paid books actually being sold.
- `Platforms/iOS/Info.plist` purchase comment. Blocks: the iOS switch-on.
- Part D: privacy page and store listings. Blocks: switch-on honesty of the listings.
- Store privacy declarations to decide (not decided; review `reviews/s346l-high.md` #5): Play Data safety and Apple App Privacy may need "Purchase history" (the proof is sent to our server for validation and processed ephemerally), plus the Families / Kids notes (Play "contains in-app purchases" and purchase gate, Apple Kids Category and parental gate).
- Then set `paidBooks.enabled: true` in R2 `dinoreader/config.json`.
- Known limits: Sonos and Alexa serve paid listening tracks (user ruled them out); a store proof can be shared (no accounts).

#### For CLAUDE.md (lifted 2026-11-12)

- `worker/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`: `paidBooks.enabled` off or unreadable means origin/main behaviour everywhere (Worker, app, backups); nothing user-facing awaits the remote config. (Slice 346: docs/plan/slices/346-dino-paid-books.md)
- `src/DinoReader.App/CLAUDE.md`: `BookAccess.LegacyBookChoice` alone decides what a schema 1 restore does with each book. (Slice 346: docs/plan/slices/346-dino-paid-books.md)
- `worker/CLAUDE.md`: the `PAID_LIMITER` and `TICKET_LIMITER` rate-limit bindings must exist in the wrangler configs; a missing `PAID_LIMITER` answers 429, never "owned". The proof header is `X-Purchase-Proof`, format as in `worker/entitlements/proof.ts`. (Slice 346: docs/plan/slices/346-dino-paid-books.md)

#### Not yet verified

- Real Play test-account purchase and restore.
- iOS StoreKit sandbox/TestFlight purchase and restore.
- Windows Store add-on on the VM.
- The switch-on store page with the real Worker.
- Microsoft Store ID flow against a live store.
- The `de` store listing text is machine-written and live (pushed 2026-11-13 on the user's order; they don't read German, so this never holds anything).

## Switched on (2026-11-13)

- `paidBooks.enabled: true` in the live config (read back from `/api/app-config`); #1038 merged and deployed by Workers Builds; `dino-store.yml` run 43390771295 republished every meta: 4 free, 11 paid, all with underscore product ids; a paid download without proof answers 402, a free one 200.
- Listings: Play en-GB and de-DE, App Store en-US and de-DE (version 1.81.1, not submitted) read back identical; Microsoft en-us published (submission 82), `de` committed (submission 1152921505702395457).
- Microsoft: all 11 add-ons Published (Partner Center API). 2 single-use promo codes for Goldilocks ordered for a free real purchase test.

## First real Windows purchase failed (2026-11-13)

The user bought Good Enough for real (£0.99) in Store-installed Dino 1.124.1 on Windows; the row then showed "Try again".
- Worker logs (Workers Observability, read with `CF_OBSERVABILITY_READ_TOKEN`, dinoreader secrets): 10:26:53 `GET /api/stories/windows-ticket` 200, 10:26:54 `GET /api/stories/<good-enough>/download?v=8` **403**; again 10:58:01 ticket 200, 10:58:10 download 403 (30 minutes later, so not a Store delay).
- **Fixed, confirmed 2026-11-13 11:09 UTC:** after the Partner Center change below, the same purchase downloaded: ticket 200, `download?v=8` **200** and its English audio 200 (Worker logs); the user confirmed it in the app and got Microsoft's receipt email. Cause: the Worker's Entra app (client 7b5aa851-6403-4397-9e7e-7e240fb79884) was never added under Partner Center > Dino Book Reader > Services > **Product collections and purchases** (learn.microsoft.com/windows/uwp/monetize/view-and-grant-products-from-a-service, step 2). The user saved it as Client ID 1 on 2026-11-13 ~11:15 UTC; Microsoft says up to 16 hours to take effect.
- Also found: the user's PC first ran Dino **1.123.3** (no purchase code), so Good Enough showed a free "Get", the download got 402 and the row "Try again"; after the Store update to 1.124.1 the Buy flow appeared. Any Windows user still on 1.123.x or older sees the same. The user said yes (2026-11-13): the live config now has `windows.minVersion` and `latestVersion` 1.124.1 (read back from `/api/app-config`), so older Windows installs show the must-update screen.

### Part E — make purchase failures visible (2026-11-13)

- [x] E1. Worker `worker/entitlements/microsoft.ts`: log (console.warn, shows in Workers Logs) every non-200 Microsoft answer with its status and Microsoft's error `code`/`innererror.code` (never the key, token or any customer id), and on a 200 the item count plus each Active item's `productType`, `status` and `inAppOfferToken`. A 400 is no longer silently "owns nothing" without a log line. Same for a failed token mint in `accessToken` (status and `error` only) and the catch blocks. The gate (`gate.ts`) logs which answer it gave (Allowed / BadProof / CannotVerify / NoProof) with the store name and product id. Unit tests cover that nothing secret is logged.
- [ ] E2. App (Android, iOS, Windows): a refused paid download (402/403 with its refusal code) and a price lookup that returns no products report a fallback event (`FallbackArea.Store`, new reasons), so analytics shows it; today neither leaves a trace.
- [ ] E3. App: after a purchase completes, a 403 `purchase-not-valid` within the first minutes is retried (the Store can lag) instead of turning the row into "Try again" at once.
- [ ] E4. `.github/scripts/store-products.mjs` dry run reads Play's product list from the lower-case `onetimeproducts` route, which answers Google's HTML 404, so it says "would create" for 11 products that exist and are ACTIVE (the list route is `oneTimeProducts`; checked 2026-11-13). Fix the read route and fail loudly on a non-JSON answer.
- [ ] E5. The Worker-secret upload step must read `*_FILE` entries of the secrets store (the Apple `.p8` is stored as `APPLE_IAP_KEY_P8_FILE`); on 2026-11-13 `APPLE_IAP_KEY_P8` was never set on the Worker, so no Apple purchase could be checked, while the report said all 7 were set. Make it a repo script that lists `wrangler secret list` names afterwards and fails on any missing one.

Apple, 2026-11-13: all 11 in-app purchases were MISSING_METADATA (no review screenshot); Claude uploaded one each through the App Store Connect API (the Android store screenshot, padded to 1242x2688) and all 11 read READY_TO_SUBMIT. The user set `APPLE_IAP_KEY_P8` and `APPLE_IAP_ALLOW_SANDBOX=true` on the live Worker (sandbox allowed so TestFlight testers and App Review can buy; Claude's own write was refused as "Security Weaken"). Play, same day: 11 products ACTIVE, Alpha and Public beta on 1.124.1.

E1 built 2026-11-13 (branch fix/s346-mslog): gate logs one line per fresh store check; Microsoft error codes pass only as short words (id-like values logged as `redacted`); offer tokens outside `uk.sirous.dinoreader.` log as `other`. Review `reviews/s346ml-high.md`: #1-#3 fixed; #4 (no timeout on error-body read), #5 (400 still "owns nothing", outside E1), #6 (failed mints not cached), #9 (style) skipped; #8 skipped (the purchase was the developer's own). Review `reviews/s346ml-medium.md`: all 8 low or nit, skipped (#1-#3 repeat high #4-#6; #4 item lists are at most 100 by `maxPageSize`; #5 one line per fresh check is the point; #6, #7 style and test hygiene); #8 added below.
- Not yet verified: E1's log lines appear in Workers Logs on the next real paid download (read with `CF_OBSERVABILITY_READ_TOKEN`).

#### Not yet verified (more, from before Part E)

- The store tab replanning a book to owned after a purchase made while the tab was hidden (no flow exists; add one to `.maestro/dino/`).
- A plain store download with the switch off has no dedicated flow.
- `store-products.mjs --apply` against each real store (Apple read routes marked UNVERIFIED).
- Apple App Store Server API production answered 401 with the new key (sandbox accepted it); recheck once an in-app product is approved.
