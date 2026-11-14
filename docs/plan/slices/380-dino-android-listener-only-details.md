# Slice 380 — Dino Android: product details through the listener only, Slice 358 probe removed
**Product:** Dino

*Planned.*

**Touches:** `src/Dino/DinoReader.App/Platforms/Android/Purchases/AndroidPurchases.cs`, `tests/Shared/Shared.Tests/FallbackGuardBaseline.txt`, `docs/plan/slices/380-dino-android-listener-only-details.md`

## Why

The user, 2026-11-14, after Slice 358's 1.129.0 made paid books buyable on Android: "add a slice for them", meaning the two
leftovers named that day: the diagnostic probe is still in the code, and every refresh still sends the async product-details
request that always comes back empty before asking again through the listener.

Found in [Slice 358](358-dino-android-no-buy-button.md) (1.129.0 from Play on the S21, 2026-11-14 21:39):
`product details (async): code=Ok ... asked=11 got=0`, then `product details (listener): code=Ok ... asked=11 got=11`.

Upstream (one search, 2026-11-14): [dotnet/android-libraries #1396](https://github.com/dotnet/android-libraries/issues/1396),
"Xamarin.Android.Google.BillingClient 8.3.0.2 returns a null ProductDetailsList", open, no maintainer reply, no fix. Same symptom:
`QueryProductDetailsAsync` returns Ok with an empty list while the callback overload returns the products; confirmed there on
9.1.0.1. One user reports that on 9.1.0.2 the async result's `ProductDetails` property is filled instead (not checked here; our
9.1.0.2 build still logs `got=0` from `ProductDetailsList`). The same thread reports `QueryPurchasesAsync` working, so the owned
books query (`QueryOwnedAsync`) stays as it is.

## Decisions

- The listener overload (`client.QueryProductDetails(query, listener)`) is the only product-details query. No async first try, no
  "use the listener if better" step, no probe. Reading the async result's `ProductDetails` property instead is not taken: it is one
  user's report, and the listener path is proven on the S21.
- A listener that does not answer within the timeout, or a query that throws, counts as a failed query (`Failed = true`, so
  `BookPurchases` reports `billing_error` as it does for any non-Ok code). It is no longer a "diagnostic second query".
- One trace line per refresh stays in the Release log: `product details: code=... debug=... asked=... got=... unfetched=... [...]`
  (codes, counts and ids only), so the next empty-store report can still be read from logcat.
- iOS: no task. `IosPurchases` uses StoreKit, has no async/listener pair and no probe; its `InvalidProducts` log stays. This
  slice only removes Android-only code that has no iOS twin.

## Work

- [ ] `QueryPricesAsync` builds the query once and awaits `QueryViaListenerAsync`; prices and `_details` come from that answer.
- [ ] Remove `client.QueryProductDetailsAsync`, `ListenerIfBetterAsync`, the "async wrapper is empty" line, `ProbeProductId`,
  `ProbeUnknownIdAsync` and the comments that call the listener query a diagnostic.
- [ ] Timeout or throw in `QueryViaListenerAsync`: the refresh returns no prices with `Failed = true`, and the trace line says why
  (`no answer within the timeout` / `query threw <Type>`).
- [ ] `TraceDetails` drops the `via` part (one path left); a null `Result` still prints `code=none` and counts as not ok.
- [ ] Each catch removed or changed: keep its `// no-fallback:` reason or report a fallback, and lower this file's number in
  `FallbackGuardBaseline.txt` when a catch goes.
- [ ] Comment above the query names #1396 as the reason the async wrapper is not used, so a later binding upgrade (Slice 379)
  does not put it back without checking.

## Verification

- Android Release build (0 errors); `DinoReader.Tests` and `Shared.Tests` (incl. `FallbackGuardTests`) pass.
- S21, the release from Play's internal track: logcat has one `product details: code=Ok ... asked=11 got=11` line, no `(async)`,
  `(listener)` or `(probe)` line, and the store shows a price and Buy on every paid book (never buy).

## Not yet verified

_(filled after the device run)_
