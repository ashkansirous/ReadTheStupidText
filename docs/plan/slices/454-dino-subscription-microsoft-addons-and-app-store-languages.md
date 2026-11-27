# Slice 454 — Dino subscription: Microsoft Store subscription add-ons, the 14 other App Store languages, script gaps
**Product:** Dino
**Touches:** `.github/scripts/store-products.mjs`, `.github/scripts/store-products.test.mjs`, `docs/store-listings/dino/subscription-listings.md`

*Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 384; not built).*

Moved out of Slice 384 (built, #1174) by the plan cleanup of 2026-11-25. Read live that day: App Store Connect group 22582310 has both subscriptions (`uk.sirous.dinoreader.sub.monthly`, `.sub.yearly`) APPROVED, but their group and subscription localizations hold en-US only; Partner Center product 9N9VHRLCZXN5 lists 12 add-ons, all `Durable` books (`uk.sirous.dinoreader.book.*`), no subscription add-on. Links: [App Store Connect subscriptions](https://appstoreconnect.apple.com/apps/6825053435/distribution/subscriptions), [Partner Center, Dino `9N9VHRLCZXN5`](https://partner.microsoft.com/en-us/dashboard/products/9N9VHRLCZXN5/overview).

## Work

- [ ] 1. Microsoft Store: create the two subscription add-ons (monthly 1.99 GBP, yearly 14.99 GBP, 7-day trial) with `node .github/scripts/store-products-local.mjs --apply --store microsoft`; first read its dry run, and check the UNVERIFIED subscription request shapes (`store-products.mjs` header) against Partner Center's docs through context7 before the apply. Verify: `listinappproducts` for 9N9VHRLCZXN5 shows them and the Windows app's subscription card reads them.
- [ ] 2. App Store: add the 14 other localizations to group 22582310 and to both subscriptions from `docs/store-listings/dino/subscription-listings.md` (Apple refused them with 409 UNMODIFIABLE while the subscriptions were Waiting for Review; they are APPROVED now). Verify: API read-back of the locales.
- [ ] 3. `store-products.mjs`: fail (not cut) on a subscription name or description over Apple's limit; fold the subscription review screenshot upload into the script (`subscriptionAppStoreReviewScreenshots`; the "by hand" text at line 1034 is wrong). Low: the description constant is now 38 characters, within the 45 allowed.

#### Not yet verified

- [owner] Native read of the machine-written subscription texts (already in unverified.md under Slice 384).
