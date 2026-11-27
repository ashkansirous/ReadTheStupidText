# Slice 455 — Dino: buy, restore and subscription outcome events
**Product:** Dino
**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `worker/events-ui.test.mjs`, `src/Dino/DinoReader.Core/UsageEvent.cs`, `src/Dino/DinoReader.App/UsageCounter.cs`, `src/Dino/DinoReader.App/Purchases/**`, `src/Dino/DinoReader.App/Platforms/iOS/**`, `src/Dino/DinoReader.App/Platforms/Android/**`, `src/Dino/DinoReader.App/Platforms/Windows/**`, `src/Dino/DinoReader.App/StorePage.xaml.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml.cs`, `src/Dino/DinoReader.Core/StoreKit2Answers.cs`, `tests/**` (Dino usage tests), `site/dino/privacy.html`, `STORE.md`, `src/Dino/DinoReader.App/BackupPage.xaml.cs` (widened at review fix 1, wave 4 of night 2026-11-27-0154: backup restore passes countAsRestore false)

*Built 2026-11-27 (night 2026-11-27-0154, wave 4), PR #1408, not merged; not yet verified on a device or in the logs (see Not yet verified).* (Planned 2026-11-25; extended 2026-11-26 after the owner's ask.)

**Why:** the apps send no buy, restore or subscription event, so the logs cannot say whether a purchase, trial or restore happened (found 2026-11-15, Slice 390 line 38 "Finding for a later slice"; nothing owned it). On 2026-11-26 the owner asked who bought two iOS books and how many people started the free trial. Only App Store Connect could answer (2 Ugly Duckling buys, SG 2026-11-20 and CH 2026-11-25; 0 intro-trial starts and 2 "Gift Bahar" offer-code starts, 2026-09-16 to 2026-11-25). `dino_usage` had nothing, and Play's sales reports were unreadable with the current key. The owner's words: "this is a fucked up situation. add this to the plan so we collect these events".

**Decisions (decided by this planning session, 2026-11-26):**
- One new closed-set event kind, `purchase`, with its outcome in `detail` (blob9). `dino_usage` uses all 20 blobs (`worker/events.ts:524-537`), so no new column is added. Detail is `<what>.<outcome>[.<offer>]`:
  - what: `book`, `sub_monthly`, `sub_yearly`, `restore`, `code`.
  - outcome: `shown`, `started`, `bought`, `pending`, `cancelled`, `failed`, `already_owned`, `restored`, `nothing_to_restore`.
  - offer: `trial`, `paid` or `code`, for subscriptions only.
- A book event carries the store book's id in blob2 (its package GUID, the same one `book.json` holds, so the title can be looked up), plus the book language (blob3). Country, platform, version and UI language come from the usage envelope as for every event. No price, no account, no transaction id, no receipt (`worker/CLAUDE.md`: only closed-list fields).
- "Shown" is logged once per opening of the subscription card on StorePage and once per opening of a paid book's buy sheet in the Gallery. It is not logged on every redraw.
- Only production builds count, through `UsageCounter` and `UsageGate` as today (production-only-analytics). Beta builds still go to `dino_usage_beta`.
- Conversion, renewal, cancellation and refund happen after the app closes. They are [Slice 458](458-dino-store-server-notifications.md) (store server notifications), not here.

**Work:**
- [x] 1. Worker: add the `purchase` rule to `EVENT_RULES` with the closed detail list above, plus sources (`SHELF_SOURCE` for book, `NO_BOOK` for the rest). Add the tests in `worker/events.test.mjs`: every detail is accepted, an unknown one is dropped, and a book event without a valid GUID is dropped.
- [x] 2. Core: add `UsageEventName.Purchase` and a small `PurchaseDetail` builder (no magic strings) that the App and the tests share. Extend the Core/Worker list-match test to cover it.
- [x] 3. App, shared: in `BookPurchases.BuyAsync` (`Purchases/BookPurchases.cs:108-126`), log `started` before the store call and map `BuyOutcome` (Bought, Pending, Cancelled, Failed, plus already-owned) to the outcome after it. In `RestoreAsync` (105), log `restored` or `nothing_to_restore`. `RedeemCodeAsync` (129-141) logs `code.started`, since the store's redeem screen returns no result. The thrown-buy path keeps its existing `fallback` `StoreFailed` report and also logs `failed`.
- [x] 4. iOS: carry the offer kind from `DinoStoreKit.swift` `describe()` (31-47) through `StoreKit2Answers.cs` and `IosPurchases.cs` `OutcomeOf` (198-217). Free trial (`offer.paymentMode == .freeTrial`, or `offerType == .introductory`) maps to `trial`, `offerType == .code` to `code`, otherwise `paid`. On launch, a subscription transaction from an offer code that the app hasn't logged yet logs `code.bought` once. The redeem sheet itself gives no result, so this is how a code redemption gets counted.
- [x] 5. Android: `AndroidPurchases.cs` already knows which offer token it launched (`BuyAsync(withTrial)`, 89, `trialPlans` 191-225). Pass `trial` or `paid` from it into the outcome at `PurchasesUpdated` (401-439), and add `already_owned` (429). Play's client cannot confirm a trial was granted (a reused account gets charged), so Slice 458's RTDN is the source of truth for trials on Android. The slice says so in `STORE.md`.
- [x] 6. Windows: map `StorePurchaseStatus` in `WindowsPurchases.cs` (45-68) the same way: NotPurchased is `cancelled`, AlreadyPurchased is `already_owned`. Use offer `paid`, since no offer info is read.
- [x] 7. UI: log `shown` from `StorePage` (`ApplyCardLayout` 536, once per open, with `_planShowsTrial` 559 picking `sub_*.shown.trial` or `.paid`) and from `GalleryPage` (1858) when a paid book's buy prompt opens. Keep the existing no-tap-count markers, or swap them for counted controls if `SilentButtonGuardTests` allows.
- [x] 8. Docs: add one line for the new event to `site/dino/privacy.html` and `STORE.md` (`DinoReader.App/CLAUDE.md`, "Usage counts"). `PrivacyInfo.xcprivacy` is unchanged (no Device ID).

**Verification:**
- Worker and .NET tests are green, including the Core/Worker list match and `SilentButtonGuardTests`.
- Device runs on Android (S21) and iOS (TestFlight to `dino_usage_beta`), and Windows: open the subscription card, cancel a buy, and buy a book with a sandbox or test account. `analytics-query` on `dino_usage_beta` then shows `purchase` rows with `book.started`, `book.cancelled`, `book.bought` (with the book GUID), and `sub_monthly.shown.trial`.
- After the next production release, *Watch* (in plan.md *Outstanding work*): `analytics-query` on `dino_usage` shows `purchase` rows, and their book buys match App Store Connect's SALES report for the same days.

#### Changed while building

- Windows: the buy no longer forces offer `paid`. After a real Bought, `BookPurchases.BuyAsync` reads the store's subscription state and counts `trial` only for Trial and `paid` only for Active, else the asked offer (`PurchaseDetail.OfferAfterBuy`). A cancelled buy re-reads and records with the asked offer.
- Restore counts only on the Restore button; the backup restore passes `countAsRestore` false, so `BackupPage.xaml.cs` joined Touches. `RestoreOutcome` is `failed` when the read failed or there was no internet, `restored` when this press's fresh read holds a paid book or an entitled subscription, else `nothing_to_restore`.
- iOS `code.bought` counts only a redemption found after this build's first complete read (the baseline), and is claimed only once its count was queued.
- StorePage logs `shown` per plan card per visit (monthly and yearly separately) and before a paid-book Buy; GalleryPage logs `shown` when a locked book's Buy prompt opens.
- Android trial means the trial offer was asked for and listed by Play, not proof it was granted; Slice 458's store server notifications are the source of truth. STORE.md says so.

#### From review

High pass (`reviews/pr-1408-high.md`, 10 findings read): fixed 1-6, 9, 10; the other two are the lines below. Medium pass (`reviews/pr-1408-medium.md`, 8 findings read): fixed 1-5, 7, 8 to the coordinator's design (findings 1, 3, 4 reopened round-1 code); the other one is the line below. Check pass (`reviews/pr-1408-check.md`, 6 nits read): left as they are, because on a third pass only crash, data-loss, security, privacy, parity or loading findings reopen code; they are under Follow-ups.

- Skipped #7 (`reviews/pr-1408-high.md`): "The IPurchaseUsage seam has no test using it." - no test project references the App, so BookPurchases cannot be unit tested without a new project; the pure parts stay tested in Core.
- Skipped #8 (`reviews/pr-1408-high.md`): "Parity gap for gift codes." - the slice names code.bought for iOS only and already_owned for Android and Windows (applied under #5); no other platform event is named, so none is added; the gap stays recorded in STORE.md.
- Skipped #6 (`reviews/pr-1408-medium.md`): "Record returns false when sampled out, so redemption is retried and counted later under another date; no recency check." - a count lost to sampling is tried again by the next read on purpose (comment at BookPurchases.cs RecordCodeRedemptions); a later date is the read's date.
- Changed from item 6 (`reviews/pr-1408-high.md` #2): Windows no longer forces offer `paid`; `BookPurchases.BuyAsync` reads the subscription's collection-data trial state after a bought plan and counts `trial` when the store reports it, else `paid`.

#### Follow-ups (from `reviews/pr-1408-check.md`, none applied)

- #1 `BookPurchases.cs:175`: the post-buy trial/paid read uses the kept snapshot, so a failed read leaves stale state; use the fresh subscription state.
- #2 `BookPurchases.cs:243`: an incomplete or unreachable first read never seeds the iOS code baseline, so an early redemption is swallowed (accepted loss).
- #3 `BookPurchases.cs:128`: an `OperationCanceledException` from the platform buy leaves `started` with no outcome (callers pass None today).
- #4 `BookPurchases.cs:156`: `LastBuyNote` is a shared platform field, so overlapping buys can read each other's note; return the note with the outcome.
- #5 `BookPurchases.cs:321`: `ReadCoreAsync` is about 47 lines, over the 3-30 rule.
- #6 `AndroidPurchases.cs:422` (pre-existing): any Play callback completes the current buy's task, not matched to the product.

#### For CLAUDE.md

- Purchase outcome events are one `purchase` event whose detail is `<what>.<outcome>[.<offer>]`; the closed lists live in `worker/events.ts` and `UsageEvent.cs` (`PurchaseDetail`) and `PurchaseUsageTests` compares them, so change both together. A `book` detail needs the book's GUID; an offer only goes on `sub_*`. (Slice 455: docs/plan/slices/455-dino-purchase-outcome-events.md)
- `BookPurchases` logs `started` before the store call and the outcome after. Restore counts only from the Restore button; any other caller of `RestoreAsync` (the backup restore) passes `countAsRestore` false. (Slice 455: docs/plan/slices/455-dino-purchase-outcome-events.md)
- Android `trial` means the offer was asked for, not granted; trial truth comes from the store server notifications (Slice 458). (Slice 455: docs/plan/slices/455-dino-purchase-outcome-events.md)

#### Not yet verified

- [phone] S21: the Store card shows `sub_monthly.shown.trial`, and a cancelled buy sends `book.started` then `book.cancelled` with the book GUID (read in `dino_usage_beta` via `analytics-query`).
- [owner] A real buy, restore and code redemption on iOS TestFlight.
- [owner] Windows sandbox buy: trial versus paid is recorded correctly.
- [session] The Swift wrapper builds in the iOS CI job (`dinoreader-ios-sim.yml`).
