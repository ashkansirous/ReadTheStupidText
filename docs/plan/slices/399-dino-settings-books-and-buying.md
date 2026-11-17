# Slice 399 — Dino: a "Books and buying" group in Settings (subscription state, Restore, Redeem) and the tour Store tip names the subscription
**Product:** Dino

*Planned 2026-11-15 from the 2026-11-15 Subscription mock (`docs/design/dino-reader/Dino Reader 2026-11-15 Subscription.dc.html`, section 3, frame SB9, and end question 3). Not built yet.*

**Touches:** the grown-ups Settings tab page and `src/Dino/DinoReader.App/GrownUpHeader.cs`, `src/Dino/DinoReader.Core/SubscriptionCard.cs` (Slice 397 pure state), `src/Dino/DinoReader.App/Purchases/**`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**`, `.maestro/dino/`, `tests/Dino/DinoReader.Tests/**`, `docs/plan/slices/399-dino-settings-books-and-buying.md`

## Decisions (the mock, 2026-11-15)

1. Slice 384 asks for a Manage subscription link "in the grown-ups area"; the build has it only inside the Store card. Settings gets it (the mock supersedes the build).
2. **The corner tour's Store tip and the welcome card mention the subscription.** User, 2026-11-16 (settled, never re-ask; mock end question 3 is answered). The 2026-11-13 Onboarding page draws both (the mock supersedes the earlier line here, which was written before that page had the text):
   - `Tour_TipStore` (built text changes): "New stories, free and paid, or every book with one subscription. Every book comes in all your family’s languages. Bought before? Restore your books here." (Slice 362 is built and owns the tip; this slice changes it.) The last sentence stays true: Restore is on the Store first line (Slice 396).
   - The welcome card's Store row (N8a, "Get books in the Store") line `Tour_StoreLine` changes from the built "Free and paid stories, in all your family’s languages." to the page's "Free and paid stories, or all of them with one subscription." (not in the 2026-11-17 copy note's table; the text is the Onboarding page's.)
3. **Foldable Settings frames keep SB9's helper lines** under Restore purchases and Redeem a code (copy note open point, resolved: the mock's SB9 wins; the foldable pages simply did not draw them). Slice 403 builds the wide Settings with them.
4. **Copy.** Every string of SB9, with its key, is in `docs/plan/notes/2026-11-17-dino-subscription-and-foldables-copy.md` (rows "Subscription SB9" and "Onboarding"); this slice cites those keys and invents no wording.

## Design (2026-11-15 Subscription mock, SB9; one phone frame 375 wide, UI)

- **SB9, Settings, Books and buying.** Header "Grown-ups’ corner · Settings", "Child’s shelf" pill. A new group between "Reading" and "This phone" (then "Backup" and "About & updates 1.131.0" as built; the version is filled from the real build). Group label "Books and buying" (new `Settings_BooksAndBuying`), 12 / 700, letter spacing 1, uppercase, `#5a5470`, padding 14 4 6. Group card white, radius 18, rows min 52 high, padding 8 16, gap 10, a 1 px `#efe9dd` divider between rows; row title 15, value 14 / 600 `#5a5470`.
  - "Subscription" (`Settings_Subscription`) with the state line under it: "Yearly · renews on 15 Nov 2027" (new `Settings_SubYearly` "Yearly · renews on {0}", `Settings_SubMonthly`), "Free trial until 22 Nov" (`Settings_SubTrial`), "Not subscribed" (`Settings_SubNone`), "Payment problem" (`Settings_SubGrace`). Opens the store subscription page, with an external-link icon, not a chevron. When not subscribed, the row opens the Store tab instead.
  - "Restore purchases" (`Store_RestorePurchases`) with the line "Books and a subscription bought with this Google account" (new `Settings_RestoreLine_Google`, `_Apple`, `_Microsoft` with "Apple ID" and "Microsoft account"): runs the Slice 396 restore.
  - "Redeem a code" (`Store_RedeemCode`) with the line "A gift or promotion code from Google Play" (new `Settings_RedeemLine_Google`, `_Apple` "…the App Store", `_Microsoft` "…the Microsoft Store"): opens the store redeem screen.
- **Tour tip and welcome card** as Decision 2: `Tour_TipStore` and `Tour_StoreLine` in all 15 languages (Onboarding page, frames N8a and the Store tip).

## Work

- [ ] 1. Android: the group in Settings with the four state lines (from the Slice 397 state), Manage, Restore and Redeem.
- [ ] 2. iOS and Windows: the same (the rail Settings tab on wide screens).
- [ ] 3. Strings above and the changed `Tour_TipStore` and `Tour_StoreLine` in `AppResources.resx` and every `AppResources.<lang>.resx` (15 languages).
- [ ] 4. Slice 362 is built, so it is not edited; this slice owns the tip change, and any Maestro tour flow that checks the tip text is updated.
- [ ] 5. Maestro flow: Settings shows "Books and buying"; the state line follows `FakePurchases`; Restore from here gives the Slice 396 message.

## Verification

- Dino tests pass (state-line choice). Android emulator: the Maestro flow; iOS simulator and Windows VM: the group and its rows.
