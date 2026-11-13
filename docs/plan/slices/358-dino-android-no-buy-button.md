# Slice 358 — Dino Android: "Connect to the internet to buy" while online, no Buy button

*Built 2026-11-13 (wave 2026-11-13), merged as PR #1075 (ce397f07). Emulator checks pass; the real cause is not yet verified on a Play-installed phone.*

*Planned 2026-11-13 (user bug report).*

**Touches:** `src/DinoReader.App/Platforms/Android/Purchases/AndroidPurchases.cs`, `src/DinoReader.App/Platforms/iOS/Purchases/IosPurchases.cs`, `src/DinoReader.App/Platforms/Windows/Purchases/WindowsPurchases.cs`, `src/DinoReader.App/Purchases/BookPurchases.cs`, `src/DinoReader.App/Purchases/IBookPurchases.cs`, `src/DinoReader.Core/BookAccess.cs`, `src/DinoReader.App/StorePage.xaml.cs`, `src/ReadTheStupidText.Application/Usage/FallbackKind.cs`, `worker/events.ts`, `worker/events.test.mjs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/*`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `.maestro/dino/store-*.yaml`, `src/DinoReader.App/Purchases/PurchaseTrace.cs` (new, after the high review: Release trace lines)

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

- [x] **Find the cause first, read-only.** Through the Play Developer API (service account in `D:\secrets`), list the
  app's one-time products and their state for the 11 ids above, and whether the user's Play account is a licence tester /
  on the track the build is installed from. Write the answer here before any fix. If the cause is Play setup (product not
  active, merchant account pending, tester missing), the fix is that setup, and the code items below still ship.
- [x] **Report it.** `AndroidPurchases`: check the product-details response code; report a new fallback reason
  `store:prices_missing` (and `store:billing_error` with the response code as a closed value) when any asked id returns no
  details. Add the reason to `FallbackReason`, `worker/events.ts` `FALLBACK_REASONS` and its test.
- [x] **iOS twin:** `IosPurchases` reports the same reason for ids in `invalidProductIdentifiers` or an empty response.
  **Windows:** `WindowsPurchases` the same for a product with no price.
- [x] **Say the right thing.** `BookAccess`: split "no price" into `ConnectToBuy` (the store really unreachable) and a new
  `NotForSaleNow` when the store answered but had no price (the mock supersedes the old wording "Can't be bought right
  now, please try later"). Strings: `Store_NotForSaleNow` "This book can't be bought right now. Please try again later."
  (new, every `AppResources.<lang>.resx`; row shows it in the red strip with **no button**) and the existing
  `Store_ConnectToBuy` "Connect to the internet to buy" (calm sand strip with a cloud glyph, **no button**, for the store
  really unreachable). Unit tests in `tests/DinoReader.Tests` for each case.

## Built (2026-11-13, implementer)

- **Cause lookup (read-only, Play Developer API, `oneTimeProducts` list plus a throwaway edit's tracks, edit deleted):** all 11 products exist, every buy option is `ACTIVE`, 174
  regions each, all `AVAILABLE`, GB priced 0.89 GBP; the ids match the live listing. Tracks: `internal`, `alpha` and `Public beta` carry 1.125.0 (completed); `production` and `beta`
  are empty. So the products are not the problem on Play's side. Licence testers and the merchant-profile state are not in the API, so which of "this account is not a licence tester /
  not on the track", "merchant bank check pending (Slice 346 line 311)" or "build from before the products were activated on 2026-11-13" applies is still **likely, not proven**; only a
  Play-installed build on the user's phone settles it, and the new Debug line (`Dino Reader product details: code= asked= got= unfetched=`) will show it.
- **Report:** `BookPurchases` reports `store:prices_missing` (new `FallbackReason.PricesMissing`, `worker/events.ts`, its test) when a reachable store left any asked product without a price,
  on every platform at once (an iOS device with purchases restricted also gives it). `store:billing_error` (`FallbackReason.BillingError`) is reported when the price query itself failed: `PurchaseSnapshot.PricesFailed`
  (new, `IBookPurchases.cs`, widened Touches with the coordinator's OK) is set by Android for a non-OK Play response code, by iOS for a StoreKit request failure or timeout, by Windows for a Store `ExtendedError`.
  The event has no field for the code itself, so the code is only in the Debug line (`Dino Reader product details: code=...`); the event says "an error", the log says which.
- **Say the right thing:** `BookAccess.Plan(..., StoreAnswer)` gives `StoreAction.NotForSaleNow` for every no-price case except a store not reached (`ConnectToBuy`); both have no button.
  `Store_NotForSaleNow` in all 16 resx. `StorePage`: new row state, and two inline strips (red `StoreRowNotForSale`, sand with cloud glyph `StoreRowConnectToBuy`) in the card, hidden while Slice 364's
  error strip (the 402 one) is shown so a card never has two. The old small muted `NoteText` label now serves only `Waiting`.
- **Already covered by Slice 364 (not rebuilt):** the strip look (`StripRedFill/Text`, `StripSandFill/Text`, `AlertGlyph`, `CloudGlyph`), padding, radius and size, and the red/calm error strips for a failed Get.
- Unit tests: `BookAccessTests` (five new). Scripted check: `.maestro/dino/store-not-for-sale.yaml` (offline gives the sand strip, a blank price gives the red strip, markers `[StoreSale] row state=` and
  `[Purchases] prices missing`); not run (device runs come after review).

## Changed while building

- **Final rule (after five check rounds):** no internet on the device = sand "connect to the internet" strip, no event; with internet, any failed price query = red not-for-sale strip plus one `billing_error`; purchases restricted = red, no event; answered without a price = red plus `prices_missing`; priced = Buy. One pure function decides it: `BookAccess.Plan` / `BookAccess.AnswerOf` (`src/DinoReader.Core/BookAccess.cs`).
- **Debug offline fake:** the fake purchases' offline flag now drives `HasInternet` in `DINO_TEST_BOOKS` builds (found by emulator run 1, see Device run).

## From review

- Skipped #7 (`reviews/pr-1075-high.md`): "the flow header lists this UNVERIFIED and arg parsing may trim it." - the emulator run after the reviews checks the flow.
- Round 2 design (high 1, 3, 5; medium 1, 4): one pure function decides the strip, `BookAccess.Plan(..., StoreAnswer)` with `BookAccess.AnswerOf(reached, pricesFailed, restricted)` in `DinoReader.Core`; StorePage and `BookPurchases`/`BookSales` use only it. Store not reached -> sand "connect to the internet" strip; price query failed -> red `Store_NotForSaleNow`; purchases restricted on the device (iOS Screen Time) -> red `Store_NotForSaleNow`, no event; answered with no price -> red; a price -> Buy. **Decision:** no new string in 16 languages for the restricted case (rare); it shows the same red strip. `prices_missing` counts books against the merged prices the cards use; the dedupe key resets after a read with no problem.
- Skipped #5 (`reviews/pr-1075-medium.md`): "Failure derived from ExtendedError?.HResult != 0; empty Products with no ExtendedError counts as a successful answer." - an empty answer with no error is the "answered, no price" case this slice reports.
- Skipped #6 (`reviews/pr-1075-medium.md`): "Catch in ReadAsync marks unreachable after ReportReadProblems may already have sent events." - low, no failure path shown in this slice.
- Skipped #7 (`reviews/pr-1075-medium.md`): "Invalid product ids only logged; _observer.Products keeps stale SKProducts." - low, no failure path shown in this slice.
- Skipped #8 (`reviews/pr-1075-medium.md`): "Writes unconditionally to AppTrace in Release (tail goes out with crash reports); no sanitising, the no-account/price/token rule is only a comment." - low, no failure path shown in this slice.
- Skipped #9 (`reviews/pr-1075-medium.md`): "New strips duplicate the Border/Grid/Path/Label markup a third time with stale closing indentation." - low, cosmetic.
- Skipped #10 (`reviews/pr-1075-medium.md`): "Red-strip half depends on the fake-purchases hook keeping a blank price, which the flow itself lists as unverified." - the emulator run checks the flow.
- Skipped #8 (`reviews/pr-1075-high.md`): "ReportReadProblems exceeds the 30-line rule and mixes logging, telemetry and decision." - style; the report code was rewritten for findings 1-6 anyway (ReportPriceProblem split out).
- Applied (`reviews/pr-1075-check1.md`): #7 Restricted beats any price in `BookAccess.Plan` (red strip, no Buy); #3 one `BookAccess.HasPrice`/`PriceOf` used by PlanFor, UnpricedBooks and KeepPrices; #1 a change of `Restricted` raises Changed.
- Skipped #4 (`reviews/pr-1075-check1.md`): "ReportPriceProblem uses prices after KeepPrices overlay, so a product that lost its price this read counts as priced; card keeps Buy and purchase fails, event never fires." - keeping the older price is existing behaviour (`origin/main:src/DinoReader.App/Purchases/BookPurchases.cs:121`, `KeepPrices` on a read), not new in this diff.
- Skipped #2 (`reviews/pr-1075-check1.md`): "_reportedProblem dedup updates only on reachable reads and skips the exception path, so persistent billing_error/prices_missing is under-reported." - low, no failure path shown.
- Skipped #5 (`reviews/pr-1075-check1.md`): "Answer reads volatile _lastReadReachable and plain _snapshot without a lock; NoteReachable runs before snapshot swap, so a brief wrong strip (red vs sand) can show." - low, no failure path shown.
- Skipped #6 (`reviews/pr-1075-check1.md`): "failure detected only by ExtendedError.HResult != 0; an empty result with no error is reported prices_missing, not billing_error." - low, no failure path shown.
- Skipped #8 (`reviews/pr-1075-check1.md`): "Line writes product ids and an unbounded joined invalid-id string to the Release rolling log sent with crash reports" - low, no failure path shown.
- Skipped #9 (`reviews/pr-1075-check1.md`): "if trimmed, the red-strip path has no working automated check." - low; the emulator run checks the flow.
- Skipped #10 (`reviews/pr-1075-check1.md`): "Debug [StoreSale] log fires on every SetState, not on change, so expect-log can match an earlier row/screen and floods the log." - low, no failure path shown.
- Applied (`reviews/pr-1075-check2.md`): R2 a Play product-details code SERVICE_UNAVAILABLE, NETWORK_ERROR or SERVICE_DISCONNECTED (`BookAccess.IsPlayNotReached`, codes from context7 `/websites/developer_android_google_play_billing`) and an iOS StoreKit request failing in `NSURLErrorDomain` (`BookAccess.IsIosNotReached`) return `PurchaseSnapshot.Empty` = store not reached (sand connect strip, no billing_error); #1 spacing. Windows not mapped: `ExtendedError.HResult` has no documented list of network HRESULTs we could check, so any error stays `PricesFailed`.
- Skipped #2 (`reviews/pr-1075-check2.md`): "BillingError and PricesMissing share one dedupe key, so unpriced-book count change can suppress/trigger a re-report; a PricesFailed read with unpriced books never records prices_missing." - low, no failure path shown.
- Skipped #3 (`reviews/pr-1075-check2.md`): "Restricted snapshot keeps old Prices; only BookAccess.Plan hides Buy, any other reader of Snapshot.Prices can offer Buy. Clear Prices when Restricted." - BookAccess.Plan is the one place that decides Buy, by design.
- Skipped #4 (`reviews/pr-1075-check2.md`): "Red, sand and Waiting strips are three near-copy borders; bind one strip to colour/glyph/text." - low, cosmetic.
- Skipped #5 (`reviews/pr-1075-check2.md`): "Two logging paths (#if DEBUG TestHookLog and PurchaseTrace) for one event" - low, no failure path shown.
- Skipped R1 (`reviews/pr-1075-check2.md`): "BookPurchases.cs:143, atomicity of NoteReachable vs _snapshot swap (check1 #5): red strip may flash." - low, no failure path shown (already skipped as check1 #5).
- Skipped R3 (`reviews/pr-1075-check2.md`): "WindowsPurchases.cs:111, HResult != 0 marks PricesFailed even with priced products." - low, no failure path shown.
- Skipped R4 (`reviews/pr-1075-check2.md`): "BookPurchases.cs:143, _reportedProblem not reset on unreachable/exception reads." - low, no failure path shown (already skipped as check1 #2).
- Round 3 design (`reviews/pr-1075-check3.md` 1, 2, 3, 6, 9; replaces the per-platform code tables of check2 R2): "store not reached" is decided ONLY by the device having no internet, read once per read in `BookPurchases` (MAUI `Connectivity.Current.NetworkAccess != NetworkAccess.Internet`, context7 `/dotnet/maui`), the same on every platform: then the sand "connect" strip and no event.
  With internet, any failed price query (any Play non-OK code, any StoreKit error or timeout, any Windows ExtendedError) or a platform that could not be asked is the red strip, and one `billing_error` event for a failed price query (the code goes only to the trace line).
  `IsPlayNotReached`, `IsIosNotReached` and the Empty-on-network-code returns are removed, so a failed read keeps its owned and pending data (a purchase made elsewhere shows even offline); `BookAccess.AnswerOf(hasInternet, pricesFailed, restricted)` stays the one pure function.
  Offline reads send no `store_unreachable` or `billing_error` event at all; a platform that cannot connect while the device is online still sends `store_unreachable`.
- Skipped #4 (`reviews/pr-1075-check3.md`): "ReportPriceProblem dedupe key resets only when a read reaches the store; unreachable reads return early." - low, no failure path shown.
- Skipped #5 (`reviews/pr-1075-check3.md`): "The exception path in ReadAsync calls NoteReachable(false) but bypasses the dedupe and reports StoreFailed on every failing read." - low, no failure path shown.
- Skipped #7 (`reviews/pr-1075-check3.md`): "HasNote now covers only Waiting; for ConnectToBuy and NotForSaleNow the row's third grid column may render empty" - low, no failure path shown.
- Skipped #8 (`reviews/pr-1075-check3.md`): "a replan between NoteReachable(true) and the snapshot assignment can show the wrong strip for one frame." - low, no failure path shown (same as check1 #5).
- Skipped #10 (`reviews/pr-1075-check3.md`): "derives only from a non-zero HResult; an empty Products list with null ExtendedError" - low, no failure path shown.
- Skipped #1 (`reviews/pr-1075-check4.md`): "ReadAsync catch reports StoreFailed even when device has no internet, while ReportReadProblems sends nothing offline." - low-medium, no failure path shown.
- Skipped #2 (`reviews/pr-1075-check4.md`): "HasInternet() true only for NetworkAccess.Internet; ConstrainedInternet/Unknown count as offline." - low, no failure path shown.
- Skipped #3 (`reviews/pr-1075-check4.md`): "a caller omitting it silently gets the connect-to-buy strip." - low, no failure path shown.
- Skipped #4 (`reviews/pr-1075-check4.md`): "Connectivity.Current is static inside BookPurchases, so the offline/online event and strip rules are untestable without a device." - low, no failure path shown.
- Skipped #5 (`reviews/pr-1075-check4.md`): "HasSandNote/HasRedNote are false when HasError is true, so a failed Get/Buy on a no-price row hides the sand/red note behind a generic error strip." - one strip per card; the 402 error strip wins by design.
- Skipped #1 (`reviews/pr-1075-check5.md`): "After QueryTimeout the SKProductsRequest is not cancelled and its delegate is held only by a local; SKProductsRequest.Delegate is weak, so a late reply may be lost" - low, no failure path shown.
- Skipped #2 (`reviews/pr-1075-check5.md`): "Unpriced count includes sold books the account already owns (store may return no price for owned items), inflating prices_missing:N and its dedup key." - low, no failure path shown.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`; `node --test worker/events.test.mjs`.
- The user's phone, Play-installed build after the fix or the Play setup: the paid book shows its price and Buy button.
- Emulator: with Billing unavailable the row says "Connect to the internet to buy"; a Debug hook that empties the product
  details gives `Store_NotForSaleNow` ("This book can't be bought right now. Please try again later."), with no button.
- After release: `store:prices_missing` count by version in dino_usage.

#### Design (Dino Reader 2026-11-13 Missing States mock, section 2, added 2026-11-13)

Two of the section's frames matter here: S2's last card (Jack and the Beanstalk) and S3's first card (The Little Red Hen). The mock
agrees with the plan (two states, no button in either) and fixes the look and wording; the rest of S1-S3 is Slice 357's (see its
Design for the card, cover and row sizes, which these strips sit inside). Hex as in the mock (use the `DinoTokens` key with the same
value, else add one).
- **NotForSaleNow (S2):** the book's card shows its row (cover 60 x 80, title, author, length) with **no button at the end**, and inside
  the card under the row a strip: gap 8, padding 9 12, radius 12, fill `#FFF0EA`, text `#A4462A` (5.4:1 on the fill), 13.5 / 500, line 1.4,
  an alert glyph at the start, text `Store_NotForSaleNow`. It is the same strip as Slice 357's errors; it needs no tap (the row's state
  shows it as soon as the store answered without a price).
- **ConnectToBuy (S3, store really unreachable):** same strip shape, but a calm sand fill `#F0E8D8`, text `#4a4460`, a cloud glyph (nothing is
  wrong with the book), text `Store_ConnectToBuy`; no button. Shown while paid books have no price because the store did not answer.
- Both strips are inline in the card, never a pop-up.

New string for every language: `Store_NotForSaleNow`. `Store_ConnectToBuy` exists.

**Plan changed to match the mock (2026-11-13):** the "Say the right thing" item now names the keys and wording: `Store_NotForSaleNow`
"This book can't be bought right now. Please try again later." replaces "Can't be bought right now, please try later"; `Store_ConnectToBuy` gets
the sand strip with the cloud glyph; neither has a button. The emulator check text is updated to the new wording. No sibling edit
(Slice 357 owns the strip's shared styling). No `CLAUDE.md` clash.

## Device run

2026-11-13, `device-test`, released `dino-v1.124.1` APK sideloaded on emulator-5554 (google_apis x86_64, online,
`/api/stories` served the list): both paid books shown ("Goldilocks", "The Little Red Hen") say "Connect to the internet to
buy" with no price and no Buy button. Logcat shows only `BillingClient: IllegalArgumentException: Service not registered`
on `endConnection`. This emulator has a stub Play Store (`com.android.vending` 1.8), no Google account and a sideloaded
app, so Billing cannot work here: the symptom reproduces, the cause (no connection vs. empty product details) cannot be
told apart, and the release build logs nothing about products. Only a Play-installed build on a signed-in phone settles it.

### Emulator run after the reviews (2026-11-13, Dino Debug, emulator-5554)

- Run 1 (`reviews/w54-358-emu/`): `store-payment-required`, `store-server-errors` and `paid-books` PASS; `store-not-for-sale` FAIL because the Debug offline fake did not reach the Connectivity rule. Fixed: the fake's offline flag drives `HasInternet` in `DINO_TEST_BOOKS` builds.
- Run 2 (`reviews/w54-358-emu/run2/`): `store-not-for-sale` PASS, markers `[StoreSale] row state=ConnectToBuy`, then `[Purchases] prices missing count=1 of=1` and `[StoreSale] row state=NotForSaleNow`. No layout-check ok line was printed (not counted as a pass).

## Not yet verified

- The real cause on a Play-installed phone: the Release trace line `Dino Reader product details: code= asked= got= unfetched=` settles it.
- iOS and Windows strips are only compiled, not run.
- The translations of `Store_NotForSaleNow` are machine drafts.
