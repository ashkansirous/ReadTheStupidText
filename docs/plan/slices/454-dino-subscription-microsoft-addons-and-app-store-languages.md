# Slice 454 — Dino subscription: Microsoft Store subscription add-ons, the 14 other App Store languages, script gaps
**Product:** Dino
**Touches:** `.github/scripts/store-products.mjs`, `.github/scripts/store-products.test.mjs`, `docs/store-listings/dino/subscription-listings.md`, this file

*Plan cleanup check 2026-11-28, verdict keep open (item 2 only).* Read live today with an App Store Connect GET (read-only): group 22582310 localizations = `en-US` only; `sub.monthly` and `sub.yearly` APPROVED, each with `en-US` only. So item 2 is still not applied; the 14 locales are the slice's own work and the live apply is due now, run by the next builder in an interactive session (the night denial is not an outside wait). Items 1 and 3 stay done. Moved out: the three low review follow-ups below (script nits) become one *Outstanding work* line; the Microsoft certification read-back and the 14 Microsoft listing languages are a *Watch* line / unverified, not this slice's closing condition.

*Built (wave 2026-11-27), merged as PR #1417 (fdb2114c). Items 1 and 3 built in code; item 2 built and unit-tested but its live App Store apply has not run, so item 2 stays open. Planned 2026-11-25 (moved out of Slice 384).*

Moved out of Slice 384 (built, #1174) by the plan cleanup of 2026-11-25. Read live that day: App Store Connect group 22582310 has both subscriptions (`uk.sirous.dinoreader.sub.monthly`, `.sub.yearly`) APPROVED, but their group and subscription localizations hold en-US only; Partner Center product 9N9VHRLCZXN5 lists 12 add-ons, all `Durable` books (`uk.sirous.dinoreader.book.*`), no subscription add-on. Links: [App Store Connect subscriptions](https://appstoreconnect.apple.com/apps/6825053435/distribution/subscriptions), [Partner Center, Dino `9N9VHRLCZXN5`](https://partner.microsoft.com/en-us/dashboard/products/9N9VHRLCZXN5/overview).

## Decisions (coordinator, wave 2026-11-27, from the API docs read that day: `reviews/s454/api-shapes.md` under the worktrees folder)

- **Item 1 has no API route.** The Microsoft Store submission API accepts only `Durable` and `Consumable` add-ons (learn.microsoft.com create-an-add-on, manage-add-ons); it has no billing period, trial or recurring price, and pricing is a tier id. So the script never POSTs `productType: Subscription`: its Microsoft subscription path stops before any write and prints the Partner Center steps (two subscription add-ons, `uk.sirous.dinoreader.sub.monthly` 1.99 GBP and `.sub.yearly` 14.99 GBP, 7-day trial, the listing texts from `subscription-listings.md`), and `SUBSCRIPTION_MANUAL_STEPS` says so. Creating them is an owner step in Partner Center (written as a `**You:**` line in `docs/plan/unverified.md` by the index pass). The Windows app's card check waits on it.
- **Item 2 uses Apple's current v2 routes** (`subscriptionVersions` + `/v2/subscriptionLocalizations`, `subscriptionGroupVersions` + `/v2/subscriptionGroupLocalizations`, review through `reviewSubmissions`), not the deprecated v1 ones. The builder writes and unit-tests the code and runs only the dry run; the live apply (one locale first, read back, then the rest) runs after both reviews, as the slice's check step.
- Item 3 as written.

## Work

- [x] 1. (built in code; creating the add-ons is an owner step in Partner Center, see Not yet verified) Microsoft Store: create the two subscription add-ons (monthly 1.99 GBP, yearly 14.99 GBP, 7-day trial) with `node .github/scripts/store-products-local.mjs --apply --store microsoft`; first read its dry run, and check the UNVERIFIED subscription request shapes (`store-products.mjs` header) against Partner Center's docs through context7 before the apply. Verify: `listinappproducts` for 9N9VHRLCZXN5 shows them and the Windows app's subscription card reads them.
- [ ] 2. App Store: add the 14 other localizations to group 22582310 and to both subscriptions from `docs/store-listings/dino/subscription-listings.md` (Apple refused them with 409 UNMODIFIABLE while the subscriptions were Waiting for Review; they are APPROVED now). Verify: API read-back of the locales.
- [x] 3. (built: `withinLimit` fails an over-limit name or description; the script uploads the review screenshot) `store-products.mjs`: fail (not cut) on a subscription name or description over Apple's limit; fold the subscription review screenshot upload into the script (`subscriptionAppStoreReviewScreenshots`; the "by hand" text at line 1034 is wrong). Low: the description constant is now 38 characters, within the 45 allowed.

*Changed while building:*
- Item 1: no API route (Decisions). The script makes no Microsoft subscription call; it returns `Outcome.Manual` and prints the Partner Center steps (`SUBSCRIPTION_MANUAL_STEPS`). `store-products-local.mjs` was not changed (removed from Touches).
- Item 2: code uses the v2 routes (`subscriptionVersions`, `subscriptionGroupVersions`, v2 localizations), with `--locales` and `--submit` flags. It reuses an open review submission only when that holds only our items; a draft is any non-final state. Unit-tested (`node --test .github/scripts/*.test.mjs` green; `store-products.test.mjs` 103 tests). The live apply was denied at 09:45Z by the auto-mode classifier ("[Destructive or Unwanted Actions]") and was not run.

*From review:* `reviews/pr-1417-high.md` (8 findings, all 8 applied), `reviews/pr-1417-medium.md` (6 findings, all 6 applied), `reviews/pr-1417-check.md` (4 nits: 1-3 are low follow-ups with skip lines below; 4 is a docs nit fixed in this file):
- Low: `.github/scripts/store-products.mjs:1665`, a version in an unknown state is treated as an open draft; allow-list draft states (PREPARE_FOR_SUBMISSION) and refuse when more than one candidate.
- Low: `.github/scripts/store-products.mjs:1680`, a rerun after a failed submit prints "exists" with no note when the draft is in an unrecognised state; note the state.
- Low: `.github/scripts/store-products.mjs:1895`, `--submit` without `--apply`, and `--locales`/`--submit` with `--check-prices`, are silently ignored; reject them.
- Skipped #1 (`reviews/pr-1417-check.md`): "version in unknown state treated as open draft" - a nit after the second fix round; the script logs every state it sees and the first live run decides the allow-list (low follow-up above).
- Skipped #2 (`reviews/pr-1417-check.md`): "rerun after failed submit prints" - a nit about a log note only; no store write changes (low follow-up above).
- Skipped #3 (`reviews/pr-1417-check.md`): "--submit without --apply, and --locales/--submit with --check-prices, silently ignored" - a nit: nothing is written in those cases (low follow-up above).

#### For CLAUDE.md (lifted 2026-11-27)

None.

#### Not yet verified

- [session] Live App Store apply, denied at night by auto mode (needs an interactive session): `node .github/scripts/store-products-local.mjs --apply --store apple --locales fr-FR`, read back, then all locales, then `--submit`.
- **Microsoft subscription add-ons created and submitted 2026-11-28** (starter session, in the owner's Chrome at the owner's ask: "do in fucking chrome"): monthly `uk.sirous.dinoreader.sub.monthly` (Store ID 9NQSJT497RH8) and yearly `uk.sirous.dinoreader.sub.yearly` (Store ID 9MT0B184J9N3); Subscription type, period Monthly / Annually, free trial 1 week, content type Electronic books, privacy Yes with `https://dinoreader.sirous.uk/privacy` (as the book add-ons), all worldwide markets, visibility "Hidden in the Microsoft Store, available for purchase from within the parent product only" (as the book add-ons), base price 1.99 GBP / 14.99 GBP, one English (United States) listing: "Every book (monthly)" / "Every book (yearly)", "Opens every book while you subscribe. New books included." Both submissions are in certification. **Still open:** read back that both pass certification and are In the Microsoft Store (https://partner.microsoft.com/en-us/dashboard/products/9NQSJT497RH8/overview, https://partner.microsoft.com/en-us/dashboard/products/9MT0B184J9N3/overview); the other 14 languages of `docs/store-listings/dino/subscription-listings.md` as Partner Center listings (an add-on listing can be added in a later submission).
- [session] The Windows app's subscription card reads the two add-ons, after that owner step.
- [owner] Native read of the machine-written subscription texts (already in unverified.md under Slice 384).
