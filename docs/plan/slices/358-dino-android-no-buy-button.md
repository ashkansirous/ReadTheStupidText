# Slice 358 — Dino Android: "Connect to the internet to buy" while online, no Buy button

*Planned 2026-11-13 (user bug report).*

**Touches:** `src/DinoReader.App/Platforms/Android/Purchases/AndroidPurchases.cs`, `src/DinoReader.App/Platforms/iOS/Purchases/IosPurchases.cs`, `src/DinoReader.App/Platforms/Windows/Purchases/WindowsPurchases.cs`, `src/DinoReader.App/Purchases/BookPurchases.cs`, `src/DinoReader.Core/BookAccess.cs`, `src/DinoReader.App/StorePage.xaml.cs`, `src/ReadTheStupidText.Application/Usage/FallbackKind.cs`, `worker/events.ts`, `worker/events.test.mjs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/*`

## Why

On 2026-11-13 the user, on the newest Dino Reader from Google Play (Android), online, saw a paid book's row say
"Connect to the internet to buy" (`Store_ConnectToBuy`) and no Buy button.

What three sources show (2026-11-13):
- **Code** (`ground-in-code`, origin/main): the text does not test the network. `BookAccess.cs:111` gives
  `StoreAction.ConnectToBuy` whenever the paid book has **no known price**. The price is the first non-null
  `snapshot.Prices[productId]` over `item.Products` (`BookPurchases.cs:208`). A price is missing when:
  (a) Play Billing does not connect (15 s timeout, `AndroidPurchases.cs:71-95`), which reports `store:store_unreachable`;
  (b) `QueryProductDetailsAsync` returns no details (`AndroidPurchases.cs:113-134`): product unknown or inactive in Play,
  account not allowed to see it, app not installed from Play. Its response code is never checked and **nothing is
  reported**; (c) `GetOneTimePurchaseOfferDetails()` is null (`:127`).
- **Logs** (`analytics-query`, dino_usage, 2026-11-06 to 11-13): 1.124.1, the newest Android version (139 events in two
  days), has **zero** store fallbacks. 1.123.2 has `store_unreachable` x5. So on 1.124.1 Billing most likely connects, and
  the price comes back empty with no report: case (b) or (c). Likely, not proven.
- **Live listing** (`GET /api/stories`, read 2026-11-13): 11 paid books, each with one `unlockedBy` id of the form
  `uk.sirous.dinoreader.book.<stem_with_underscores>`, matching `publish-store-books.mjs:375`.
- Slice 346 (docs/plan/slices/346-dino-paid-books.md) says the 11 Play products were created and activated by
  `store-products.mjs --apply` on the night of 2026-11-13, the Play merchant bank check is still pending (line 311), and the
  app is not public on Play (internal/test track).
- Emulator repro: see *Device run* below.

## Work

- [ ] **Find the cause first, read-only.** Through the Play Developer API (service account in `D:\secrets`), list the
  app's one-time products and their state for the 11 ids above, and whether the user's Play account is a licence tester /
  on the track the build is installed from. Write the answer here before any fix. If the cause is Play setup (product not
  active, merchant account pending, tester missing), the fix is that setup, and the code items below still ship.
- [ ] **Report it.** `AndroidPurchases`: check the product-details response code; report a new fallback reason
  `store:prices_missing` (and `store:billing_error` with the response code as a closed value) when any asked id returns no
  details. Add the reason to `FallbackReason`, `worker/events.ts` `FALLBACK_REASONS` and its test.
- [ ] **iOS twin:** `IosPurchases` reports the same reason for ids in `invalidProductIdentifiers` or an empty response.
  **Windows:** `WindowsPurchases` the same for a product with no price.
- [ ] **Say the right thing.** `BookAccess`: split "no price" into `ConnectToBuy` (the store really unreachable) and a new
  `NotForSaleNow` ("Can't be bought right now, please try later") when the store answered but had no price. New string in
  every `AppResources.<lang>.resx`. Unit tests in `tests/DinoReader.Tests` for each case.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`; `node --test worker/events.test.mjs`.
- The user's phone, Play-installed build after the fix or the Play setup: the paid book shows its price and Buy button.
- Emulator: with Billing unavailable the row says "Connect to the internet to buy"; a Debug hook that empties the product
  details gives "Can't be bought right now".
- After release: `store:prices_missing` count by version in dino_usage.

## Device run

2026-11-13, `device-test`, released `dino-v1.124.1` APK sideloaded on emulator-5554 (google_apis x86_64, online,
`/api/stories` served the list): both paid books shown ("Goldilocks", "The Little Red Hen") say "Connect to the internet to
buy" with no price and no Buy button. Logcat shows only `BillingClient: IllegalArgumentException: Service not registered`
on `endConnection`. This emulator has a stub Play Store (`com.android.vending` 1.8), no Google account and a sideloaded
app, so Billing cannot work here: the symptom reproduces, the cause (no connection vs. empty product details) cannot be
told apart, and the release build logs nothing about products. Only a Play-installed build on a signed-in phone settles it.

## Not yet verified

_(filled when built)_
