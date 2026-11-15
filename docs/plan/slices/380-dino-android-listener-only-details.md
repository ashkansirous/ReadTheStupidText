# Slice 380 — Dino Android: product details through the listener only, Slice 358 probe removed
**Product:** Dino

*Built (wave 2026-11-15), merged as PR #1159 (58be727).*

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

- [x] `QueryPricesAsync` builds the query once and awaits `QueryViaListenerAsync`; prices and `_details` come from that answer.
- [x] Remove `client.QueryProductDetailsAsync`, `ListenerIfBetterAsync`, the "async wrapper is empty" line, `ProbeProductId`,
  `ProbeUnknownIdAsync` and the comments that call the listener query a diagnostic.
- [x] Timeout or throw in `QueryViaListenerAsync`: the refresh returns no prices with `Failed = true`, and the trace line says why
  (`no answer within the timeout` / `query threw <Type>`).
- [x] `TraceDetails` drops the `via` part (one path left); a null `Result` still prints `code=none` and counts as not ok.
- [x] Each catch removed or changed: keep its `// no-fallback:` reason or report a fallback, and lower this file's number in
  `FallbackGuardBaseline.txt` when a catch goes.
- [x] Comment above the query names #1396 as the reason the async wrapper is not used, so a later binding upgrade (Slice 379)
  does not put it back without checking.

## Verification

- Android Release build (0 errors); `DinoReader.Tests` and `Shared.Tests` (incl. `FallbackGuardTests`) pass.
- S21, the release from Play's internal track: logcat has one `product details: code=Ok ... asked=11 got=11` line, no `(async)`,
  `(listener)` or `(probe)` line, and the store shows a price and Buy on every paid book (never buy).

## Changed while building

- The async wrapper (`client.QueryProductDetailsAsync`) is unused because of dotnet/android-libraries #1396; revisit in Slice 379 (plan-not-needed: 379).
- Timeout, throw and no-details each give no prices, `Failed = true` and one trace line carrying `asked=<n>`; each refresh writes one `product details: code=... asked=... got=... unfetched=...` line.
- The listener callback always completes its task in a `finally`; its trace call is guarded (catch marked `// no-fallback:`).
- `FallbackGuardBaseline.txt` needed no change (removed catches were `// no-fallback:` marked).
- Checks: Android Release build 0 errors; Shared.Tests 1721 green; Dino tests green.

## From review

- Applied: high #1, medium #1 (callback completes in `finally`, trace guarded) and medium #4 (`asked=<n>` on every failure trace), from `reviews/s380-high.md` and `reviews/s380-medium.md`.
- Skipped #2 (`reviews/s380-high.md`): "Task.Delay(SetupTimeout) timeout is never cancelled when the listener answers first." - low: an unobserved Task.Delay ends on its own.
- Skipped #3 (`reviews/s380-high.md`): "A listener timeout, throw or null-details answer now returns (prices, Failed=true), so the store shows a billing error; before, the async result stood." - Failed=true on timeout/throw/no details is this slice's own work item; platform billing code has no unit-test host.
- Skipped #4 (`reviews/s380-high.md`): "No Work items ticked though most code is done; the Touches line lists BookPurchases.cs, which was not changed." - ticks come in the slice-mode docs pass; the BookPurchases.cs mention was dropped from Touches.
- Skipped #5 (`reviews/s380-high.md`): "comment (#1396) is not enforced" - low.
- Skipped #2 (`reviews/s380-medium.md`): "listener is the only details path, no retry or fallback. One 15 s timeout or null answer makes the whole refresh Failed with empty prices" - accepted by the slice (see skipped #3 above); a retry is a new behaviour.
- Skipped #3 (`reviews/s380-medium.md`): "Task.Delay(SetupTimeout) never cancelled; a late callback calls TrySetResult on an abandoned task." - already recorded above (skipped #2 of the high pass); harmless.
- Skipped #5 (`reviews/s380-medium.md`): "rule exists only as a comment; no guard test" - low.
- Skipped #1-#7 (`reviews/s380-check.md`): "catch filter narrower than doc comment" (#2) - origin/main already had the same `when (ex is Java.Lang.Exception or InvalidOperationException)` filter (AndroidPurchases.cs:221 on main), not introduced here; #1 and #3 repeat skips above; #4-#7 low.

## Not yet verified

- S21 with a Play internal-track build shows a price for every product and one `product details:` line per refresh (needs the next batched release).

#### For CLAUDE.md (lifted 2026-11-15)

- Dino Android billing (`AndroidPurchases.cs`): query product details only through the listener overload; never `client.QueryProductDetailsAsync` (empty result, dotnet/android-libraries #1396; revisit with the binding upgrade, Slice 379). (Slice 380: docs/plan/slices/380-dino-android-listener-only-details.md)
