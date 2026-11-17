# Slice 397 — Dino: the Store's subscription card in every state the store can be in (Save tag, trial used, trial running, payment problem, ended, wide screens)
**Product:** Dino

*In progress (wave 2026-11-17, night 2026-11-17-2105 wave 2), draft PR.*

*Planned 2026-11-15 from the 2026-11-15 Subscription mock (`docs/design/dino-reader/Dino Reader 2026-11-15 Subscription.dc.html`, sections 1 and 3, frames SB1-SB5 and SB10). Slice 384 was built without a mock; this slice builds what the mock adds or changes. Not built yet.*

**Touches:** `src/Dino/DinoReader.App/StorePage.xaml`, `src/Dino/DinoReader.App/StorePage.xaml.cs` (`ApplySubscriptionCard`), `src/Dino/DinoReader.Core/SubscriptionAccess.cs`, `src/Dino/DinoReader.Core/SubscriptionCard.cs` (new, pure), `src/Dino/DinoReader.App/Purchases/**`, `src/Dino/DinoReader.App/Platforms/Android/Purchases/**`, `src/Dino/DinoReader.App/Platforms/iOS/Purchases/**`, `src/Dino/DinoReader.App/Platforms/Windows/Purchases/**`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**` (`FakePurchases`), `.maestro/dino/subscription.yaml`, `tests/Dino/DinoReader.Tests/**`, `docs/plan/slices/397-dino-subscription-card-states.md`

## Why

The mock draws the subscription card in five states, where Slice 384 built one (offer, with "Try free" always shown) plus a plain "You are subscribed" line. Missing: a "Save N%" tag, a plain-price card when the trial is already used, a state line with the trial or renewal date, the payment-problem (grace) card, the ended card and the wide-screen layout.

## Decisions (the mock, 2026-11-15)

1. **Yearly first and primary, monthly second.** User, 2026-11-16 (settled, never re-ask): yearly is the main button, monthly second, as drawn. (Mock end question 2, "Prefer monthly first?", is answered.)
2. **A "Save {0}%" tag on the yearly plan.** User, 2026-11-16 (settled, never re-ask): the tag stays (mock end question 1 is answered). N = 100 minus yearly / (12 x monthly) x 100 from the two store price numbers, rounded down; hidden when either price is missing or the saving is under 10%. SB1's example is 37%. It is a new claim on screen, worked out from the store's own prices on the device, never written in the app.
3. **Store prices are the store's own strings**, as built (Slice 384 Decision 2). Dates use the device's locale.
4. **Trial eligibility (SB2).** When the store says the account cannot have the free trial (Play: no free-trial offer in the product details for this user; Microsoft: no trial SKU), the buttons show the plain price and the terms drop the trial sentence. iOS keeps "Try free" until it can ask StoreKit 2 (`isEligibleForIntroOffer`) through Slice 390's wrapper, which is built but not yet checked on the user's iPhone; once the wrapper answers, iOS follows the same rule. The mock says StoreKit 1 cannot tell.
5. **The grace store name follows the platform** ("Google Play", "the App Store", "the Microsoft Store").
6. **Store book rows show the mock's "Ages {0}–{1} · {2} min"** (copy note open point, resolved: the mock wins). New `Store_LengthMinutes` "{0} min" joins the built `Store_Ages` "Ages {0}–{1}" with " · " between; the built `Store_Length` ("{0} pages · {1} min") is no longer used on the row and goes if nothing else uses it.
7. **Copy.** Every string of these frames, with its key, is in `docs/plan/notes/2026-11-17-dino-subscription-and-foldables-copy.md` (rows "Subscription SB1-SB5, SB10"); this slice cites those keys and invents no wording.

## Design (2026-11-15 Subscription mock, sections 1 and 3: SB1-SB5 are phone frames 375 wide, SB10 is the Windows frame 1100 x 760; all are UI, the books under the card are placeholder rows; Slice 384 is followed where the notes say built, the mock where they say new)

Common to SB1-SB5: the card is the first thing under the age chips and the author row (which stay above it, so filtering never hides it); white, radius 18, padding 16, gap 12, in a column padded 14 with gap 10. Header row: a 40 x 40, radius 12 icon tile and a text column, gap 2: title 17 / 600, line 1.25; body 13.5 `#5a5470`, line 1.4. The Store's first line (Slice 396) is above the chips. Chips: "All ages", "5 & under", "6–8" (Slice 394: only bands with books, so "9–12" is gone).

- **SB1, Offer (new subscriber; built, plus the tag).** Tile `#ECE7FF`. Title "Every book, one subscription" (`Sub_Title`); line "Open every book while you subscribe. Books you buy stay yours for good." (`Sub_Line`). Two buttons, min 52 high, radius 26: yearly `#5B57E8`, shadow `0 3px 0 #3f3bc4`, white 15.5 / 600, padding 0 10 0 18, "Try free, then £14.99 a year" (`Sub_Yearly`) with the new tag at the right, "Save 37%" (new `Sub_Save` "Save {0}%"): 22 high, radius 11, padding 0 8, fill `#FFC24D`, text `#3a2a00`, 11.5 / 700, not shrinking; monthly "Try free, then £1.99 a month" (`Sub_Monthly`) as the secondary button (built style). Terms (`Sub_Terms`) "7 days free for new subscribers. After that it renews by itself every month or every year at the price shown, until you cancel in your store account. Cancel any time."; links "Terms of use" and "Privacy policy" (`Sub_TermsLink`, `Sub_PrivacyLink`). Tapping a plan opens the store sheet; the wait overlay shows after 500 ms.
- **SB2, Trial already used (new).** Same card; yearly "£14.99 a year" with "Save 37%", monthly "£1.99 a month" (new `Sub_YearlyPlain` "{0} a year", `Sub_MonthlyPlain` "{0} a month"); terms without the trial sentence (new `Sub_TermsNoTrial`): "Renews by itself every month or every year at the price shown, until you cancel in your store account. Cancel any time."
- **SB3, Subscribed, in the free trial (built line, new state line).** Tile `#DFF5E3` with a check icon `#2E7D32` (stroke 2.2). Title "You are subscribed. Every book is open." (`Sub_Active`); state line 13.5 `#5a5470`: in the trial "Free trial until 22 Nov. Then £14.99 a year, unless you cancel before." (new `Sub_StateTrial` "Free trial until {0}. Then {1}, unless you cancel before."); after it "Yearly plan · renews on 15 Nov 2027." or "Monthly plan · renews on …" (new `Sub_StateRenewsYearly` "Yearly plan · renews on {0}.", `Sub_StateRenewsMonthly` "Monthly plan · renews on {0}."). Button "Manage subscription" (`Sub_Manage`, built; opens the store page). Every paid book row below shows a tint "Included" tag (new `Store_Included`) and the brand "Get" (`Store_Get`) instead of its price; a book on the shelf shows "On the shelf"; one downloading shows the ring and percent (60%).
- **SB4, Payment problem (grace) (new).** Card with an inset 2 px ring `#F5C9B8`; tile `#FFF0EA` with a card icon `#A4462A`. Title "Your payment didn’t go through" (new `Sub_GraceTitle`); body "Every book stays open until 18 Nov while Google Play tries again. Check your payment method to keep them." (new `Sub_GraceBody_Google`, `_Apple`, `_Microsoft`, with the store name and the date as arguments). Button "Fix payment" (new `Sub_FixPayment`) with an external-link icon (17, white, stroke 2.2) at its left; it opens the same store page as Manage. Books stay "Included".
- **SB5, Ended (new).** Title "Your subscription ended" (new `Sub_EndedTitle`); body "3 books on your shelf are locked and hidden from your child. Subscribe again, or buy the ones you want to keep." (new `Sub_EndedBody` with the count of books locked on this device; `Sub_EndedBody_One` for one). Then the two plan buttons as SB2 (with the Save tag, no trial) with the terms and links. Paid books show their single price again.
- **SB10, Windows Store, 1100 x 760.** Window title "Grown-ups’ corner · Dino Reader" with minimise, maximise and close controls (canvas furniture, not built: the window chrome is the OS); header "Grown-ups’ corner" with "Back to my child’s shelf" and "Only grown-ups see this."; rail tabs "Books Ctrl+1", "Store Ctrl+2", "Settings Ctrl+3"; the Store title row has "Picked and cast for Dino" and "Restore purchases Ctrl+R" (Slice 396). The card spans the content width above the two-column book cards: heading, line, terms and links on the left; the two plan buttons in a 300-wide column on the right ("Try free, then £14.99 a year" with "Save 37%", "Try free, then £1.99 a month"). Tablets and phone landscape (600 and wider) use the same split. Windows sells two subscription add-ons: the buttons and prices come from the Microsoft Store.

## Work

Smallest end-to-end first: the pure card and the Save tag on Android, then the states, then eligibility per store, then wide.

- [ ] 1. Pure `SubscriptionCard` (Core): from the state (offer, trial-used, trial, active, grace, ended), the two prices and the end date, choose the title, body, buttons, tag and strings; `SavePercent(yearly, monthly)` with the rounding and the 10% rule. Tests for every state and the tag edge cases (missing price, saving 9%, 10%, 37%).
- [ ] 2. Android: card states in `StorePage` driven by item 1; the Save tag; trial eligibility from Play Billing offer details; grace from Play "in grace period"; the end date for the state line; Fix payment opens the Play subscriptions page.
- [ ] 3. iOS: the same states; grace from the Worker `grace` answer (Slice 384); eligibility from Slice 390 StoreKit 2 wrapper ("Try free" while it cannot answer); Fix payment opens the Apple manage sheet (Slice 384, *iOS Manage subscription*).
- [ ] 4. Windows: the same states; trial eligibility from the SKU trial availability; the wide layout (SB10) and its Ctrl+R hint; the two add-on prices.
- [ ] 5. Wide layout for tablets and phone landscape at 600 and wider (words left, 300-wide plan column right).
- [ ] 5b. Store book rows: "Ages {0}–{1} · {2} min" (Decision 6) on every platform; "Included" and "Get" on SB3-SB4 rows.
- [ ] 6. Strings above, plus `Store_LengthMinutes`, in `AppResources.resx` and every `AppResources.<lang>.resx` (15 languages; `NoHardCodedUiTextTests`).
- [ ] 7. `FakePurchases` gains the states (trial used, trial, grace, ended); `.maestro/dino/subscription.yaml` checks each card title.

## Verification

- Dino tests pass (`SubscriptionCard` rows).
- Android emulator: the Maestro flow in item 7 shows each card; the Save tag shows "Save 37%" with the live prices and is absent with one price missing.
- iOS simulator, Windows VM: the same flow and the wide layout.
- Real store (sandbox / licence tester): a second trial purchase on the same account shows the plain-price card on Play and Microsoft; iOS after Slice 390 is checked on the iPhone.

## Out of scope

- The parents Books tab (Slice 398) and Settings (Slice 399).
- The foldable layouts of the card (inner screens: card side by side, plans in a 250 column) are Slice 403.
