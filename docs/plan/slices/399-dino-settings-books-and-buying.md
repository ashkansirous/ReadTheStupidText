# Slice 399 — Dino: a "Books and buying" group in Settings (subscription state, Restore, Redeem) and the tour Store tip names the subscription
**Product:** Dino

*Planned 2026-11-15 from the 2026-11-15 Subscription mock (`docs/design/dino-reader/Dino Reader 2026-11-15 Subscription.dc.html`, section 3, frame SB9, and end question 3). Not built yet.*

**Touches:** the grown-ups Settings tab page and `src/Dino/DinoReader.App/GrownUpHeader.cs`, `src/Dino/DinoReader.Core/SubscriptionCard.cs` (Slice 397 pure state), `src/Dino/DinoReader.App/Purchases/**`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**`, `.maestro/dino/`, `tests/Dino/DinoReader.Tests/**`, `docs/plan/slices/399-dino-settings-books-and-buying.md`

## Decisions (the mock, 2026-11-15)

1. Slice 384 asks for a Manage subscription link "in the grown-ups area"; the build has it only inside the Store card. Settings gets it (the mock supersedes the build).
2. **The tour Store tip names the subscription.** Mock end question 3: **drawn in the mock, change if the user says otherwise.** The mock quotes the fragment "…or every book with one subscription" and calls it a one-line change; no frame for it is in this mock file, so the line is the built `Tour_TipStore` with that fragment added (Slice 362 is built and owns the tip): "New stories, free and paid. Every book comes in all your family’s languages. Buy one book, or every book with one subscription. Bought before? Restore your books here." The last sentence stays true: Restore is on the Store first line (Slice 396).

## Design (2026-11-15 Subscription mock, SB9; one phone frame 375 wide, UI)

- **SB9, Settings, Books and buying.** Header "Grown-ups’ corner · Settings", "Child’s shelf" pill. A new group between "Reading" and "This phone" (then "Backup" and "About & updates 1.131.0" as built; the version is filled from the real build). Group label "Books and buying" (new `Settings_BooksAndBuying`), 12 / 700, letter spacing 1, uppercase, `#5a5470`, padding 14 4 6. Group card white, radius 18, rows min 52 high, padding 8 16, gap 10, a 1 px `#efe9dd` divider between rows; row title 15, value 14 / 600 `#5a5470`.
  - "Subscription" (`Settings_Subscription`) with the state line under it: "Yearly · renews on 15 Nov 2027" (new `Settings_SubYearly` "Yearly · renews on {0}", `Settings_SubMonthly`), "Free trial until 22 Nov" (`Settings_SubTrial`), "Not subscribed" (`Settings_SubNone`), "Payment problem" (`Settings_SubGrace`). Opens the store subscription page, with an external-link icon, not a chevron. When not subscribed, the row opens the Store tab instead.
  - "Restore purchases" (`Store_RestorePurchases`) with the line "Books and a subscription bought with this Google account" (new `Settings_RestoreLine_Google`, `_Apple`, `_Microsoft` with "Apple ID" and "Microsoft account"): runs the Slice 396 restore.
  - "Redeem a code" (`Store_RedeemCode`) with the line "A gift or promotion code from Google Play" (new `Settings_RedeemLine_Google`, `_Apple` "…the App Store", `_Microsoft` "…the Microsoft Store"): opens the store redeem screen.
- **Tour tip** as Decision 2, `Tour_TipStore` in all 15 languages.

## Work

- [ ] 1. Android: the group in Settings with the four state lines (from the Slice 397 state), Manage, Restore and Redeem.
- [ ] 2. iOS and Windows: the same (the rail Settings tab on wide screens).
- [ ] 3. Strings above and the changed `Tour_TipStore` in `AppResources.resx` and every `AppResources.<lang>.resx` (15 languages).
- [ ] 4. Slice 362 is built, so it is not edited; this slice owns the tip change, and any Maestro tour flow that checks the tip text is updated.
- [ ] 5. Maestro flow: Settings shows "Books and buying"; the state line follows `FakePurchases`; Restore from here gives the Slice 396 message.

## Verification

- Dino tests pass (state-line choice). Android emulator: the Maestro flow; iOS simulator and Windows VM: the group and its rows.
