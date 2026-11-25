# Slice 455 — Dino: buy, restore and subscription outcome events
**Product:** Dino
**Touches:** `worker/events.ts`, `worker/**` tests, `src/Dino/DinoReader.App/**` (`BookPurchases`)

*Planned 2026-11-25 (plan-cleanup of that day; not built).*

**Why:** the apps send no buy, restore or subscription event (`worker/events.ts` has only `purchase_not_valid` and `download_refused`), so the logs cannot say whether a purchase or restore happened (found 2026-11-15, Slice 390 line 38 "Finding for a later slice"; nothing owned it).

**Work:**
- [ ] 1. Add closed-set event kinds for buy, restore and subscription outcomes (started, succeeded, cancelled, failed) in `worker/events.ts`, sent from `BookPurchases` on both iOS and Android, production builds only (production-only-analytics), no price or account data.
- [ ] 2. Worker tests for the new kinds; `analytics-query` read shows a row for a real purchase on the next release.
