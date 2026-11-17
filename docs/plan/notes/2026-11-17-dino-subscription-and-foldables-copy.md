# Dino Reader copy: subscription pages and the foldable pages (2026-11-17)

Final app copy for the Subscription, Missing States, iPhone Duo, Galaxy Z Fold8 and Fold8 Ultra design pages, checked against `AppResources.resx` and Slices 384, 396, 397, 398, 399. Canvas text was changed to match; layout, markup and data-props are untouched.

- **Restore with nothing found.** The built `Store_NothingToRestore` ("No bought books found on this account.") is dropped. One wording, per store, as Slice 396 already planned: `Store_NothingBought_Google` / `_Apple` / `_Microsoft` ("Nothing bought with this Google account yet."). Restore also brings back a subscription, so "books" alone was wrong. Subscription RR3 already said it; the Missing States note now says it too (and no longer says "green line under the card").
- **Built text wins.** The foldable pages' onboarding frames said "Reading languages", "English is already on", "Dino's language · always on" and a made-up listen hint; they now say what the build and the Onboarding page say ("Story languages", "English is always on. Add any others your child hears.", "App language · always on", "Listen to Dino read" + its line). Gate: "Press and hold for 3 seconds" and "Back to my child's shelf". Back cover: "Illustration: …" (not "Pictures by"). Settings row "Help make Dino better" (not "Usage counts"). Author chip "Anyone" (not "All"). "Yearly · renews on 15 Nov 2027" on all pages.
- **Known user decisions kept:** "Save {0}%" on yearly, yearly first, the corner tour's Store tip names the subscription (already drawn on the Onboarding page; the built `Tour_TipStore` has to change to match).
- **Prices and dates** are always `{0}` from the store; no trial or price claim written in the app.
- Not in the table: book titles, cast names, story sentences and credits (book data, not app strings).
- Foldable wide Settings frames omit the helper line under Restore purchases and Redeem a code (Settings_RestoreLine_*, Settings_RedeemLine_*); the build should show them as on SB9.

Status: **new** = no such key in the resx yet (key names follow Slices 396-399); **built** = exact built text.

| Page | Screen (frame id + name) | String key | English text | New? |
| --- | --- | --- | --- | --- |
| Subscription | SB1 Offer; SB10 Windows Store; RR1 | Store_Title | Store | built |
| Subscription | SB1-SB5, SB10, RR1-RR4 Store first line | Store_Subtitle | Picked and cast for Dino | built |
| Subscription | SB1-SB5, SB10, RR2-RR4 | Store_RestorePurchases | Restore purchases | built |
| Subscription | RR1 Restore, from the top of the Store | Store_Restoring | Restoring… | new |
| Subscription | SB1-SB5 age chips | Store_AllAges / Store_AgeUpTo5 / Store_Age6To8 | All ages / 5 & under / 6–8 | built |
| Subscription | SB1-SB5 author row | Store_Author / Store_Anyone | Author / Anyone | built |
| Subscription | SB1, SB2, SB5, SB10, RR1, RR3 card | Sub_Title | Every book, one subscription | built |
| Subscription | SB1, SB2, SB10, RR1, RR3 card | Sub_Line | Open every book while you subscribe. Books you buy stay yours for good. | built |
| Subscription | SB1, SB10, RR1, RR3 yearly button | Sub_Yearly | Try free, then {0} a year | built |
| Subscription | SB1, SB10, RR1, RR3 monthly button | Sub_Monthly | Try free, then {0} a month | built |
| Subscription | SB1, SB2, SB5, SB10 yearly tag | Sub_Save | Save {0}% | new |
| Subscription | SB1, SB10, RR1, RR3 terms | Sub_Terms | 7 days free for new subscribers. After that it renews by itself every month or every year at the price shown, until you cancel in your store account. Cancel any time. | built |
| Subscription | SB1-SB2, SB5, SB10 links | Sub_TermsLink / Sub_PrivacyLink | Terms of use / Privacy policy | built |
| Subscription | SB2 Trial already used, yearly | Sub_YearlyPlain | {0} a year | new |
| Subscription | SB2, SB5 monthly | Sub_MonthlyPlain | {0} a month | new |
| Subscription | SB2, SB5 terms | Sub_TermsNoTrial | Renews by itself every month or every year at the price shown, until you cancel in your store account. Cancel any time. | new |
| Subscription | SB3 Subscribed, in the free trial (title) | Sub_Active | You are subscribed. Every book is open. | built |
| Subscription | SB3 state line, in the trial | Sub_StateTrial | Free trial until {0}. Then {1}, unless you cancel before. | new |
| Subscription | SB3 state line, after the trial (yearly) | Sub_StateRenewsYearly | Yearly plan · renews on {0}. | new |
| Subscription | SB3 state line, after the trial (monthly) | Sub_StateRenewsMonthly | Monthly plan · renews on {0}. | new |
| Subscription | SB3 button | Sub_Manage | Manage subscription | built |
| Subscription | SB3, SB4 book tags | Store_Included | Included | new |
| Subscription | SB3, SB4 book rows | Store_Get | Get | built |
| Subscription | SB1-SB5, SB10 book rows | Store_OnShelf | On the shelf | built |
| Subscription | SB1-SB5 book rows (free) | Store_Free | Free | built |
| Subscription | SB1-SB5, SB10 book rows (age and length) | Store_Ages + a minutes-only length | Ages {0}–{1} · {2} min | new (the minutes part: `Store_LengthMinutes` "{0} min"; built `Store_Length` is "{0} pages · {1} min") |
| Subscription | SB4 Payment problem (title) | Sub_GraceTitle | Your payment didn’t go through | new |
| Subscription | SB4 body, Google | Sub_GraceBody_Google | Every book stays open until {0} while Google Play tries again. Check your payment method to keep them. | new |
| Subscription | SB4 body, Apple | Sub_GraceBody_Apple | Every book stays open until {0} while the App Store tries again. Check your payment method to keep them. | new |
| Subscription | SB4 body, Microsoft | Sub_GraceBody_Microsoft | Every book stays open until {0} while the Microsoft Store tries again. Check your payment method to keep them. | new |
| Subscription | SB4 button | Sub_FixPayment | Fix payment | new |
| Subscription | SB5 Ended (title) | Sub_EndedTitle | Your subscription ended | new |
| Subscription | SB5 body, several books | Sub_EndedBody | {0} books on your shelf are locked and hidden from your child. Subscribe again, or buy the ones you want to keep. | new |
| Subscription | SB5 body, one book | Sub_EndedBody_One | 1 book on your shelf is locked and hidden from your child. Subscribe again, or buy the one you want to keep. | new |
| Subscription | SB6 Books tab after a lapse (title) | Sub_BannerTitle | Your subscription ended | new |
| Subscription | SB6 banner body, several | Sub_BannerBody | {0} books are locked. Your child doesn’t see them. Subscribe again, or buy the ones you want to keep. | new |
| Subscription | SB6 banner body, one | Sub_BannerBody_One | 1 book is locked. Your child doesn’t see it. Subscribe again, or buy the one you want to keep. | new |
| Subscription | SB6 banner link | Sub_BannerPlans | See plans in the Store | new |
| Subscription | SB6 locked tile | Sub_Again | Subscribe again | built |
| Subscription | SB6 locked tile | Sub_BuyPrice | Buy · {0} | new |
| Subscription | SB6 locked tile accessible name | Sub_Locked | Locked: the subscription ended | built |
| Subscription | SB6, SB7, SB8 corner tabs | Corner_TabBooks / Corner_ChildsShelf | Books / Child’s shelf | built |
| Subscription | SB7 Offline, end date passed (title) | Sub_OfflineTitle | Can’t check your subscription | new |
| Subscription | SB7 body | Sub_OfflineBody | No internet. Subscription books stay open until {0}, 3 days after the last date the store gave. Connect to keep them open. | new |
| Subscription | SB8 Child’s shelf after a lapse | Gallery_Title / Gallery_GrownUpsPill | My stories / Grown-ups | built |
| Subscription | SB1-SB7, SB9 header | Gallery_CornerTitle | Grown-ups’ corner | built |
| Subscription | SB9 Settings, group | Settings_BooksAndBuying | Books and buying | new |
| Subscription | SB9 row | Settings_Subscription | Subscription | new |
| Subscription | SB9 row value, yearly | Settings_SubYearly | Yearly · renews on {0} | new |
| Subscription | SB9 row value, monthly | Settings_SubMonthly | Monthly · renews on {0} | new |
| Subscription | SB9 row value, trial | Settings_SubTrial | Free trial until {0} | new |
| Subscription | SB9 row value, none | Settings_SubNone | Not subscribed | new |
| Subscription | SB9 row value, grace | Settings_SubGrace | Payment problem | new |
| Subscription | SB9 row line, Google | Settings_RestoreLine_Google | Books and a subscription bought with this Google account | new |
| Subscription | SB9 row line, Apple | Settings_RestoreLine_Apple | Books and a subscription bought with this Apple ID | new |
| Subscription | SB9 row line, Microsoft | Settings_RestoreLine_Microsoft | Books and a subscription bought with this Microsoft account | new |
| Subscription | SB9 row | Store_RedeemCode | Redeem a code | built |
| Subscription | SB9 row line, Google | Settings_RedeemLine_Google | A gift or promotion code from Google Play | new |
| Subscription | SB9 row line, Apple | Settings_RedeemLine_Apple | A gift or promotion code from the App Store | new |
| Subscription | SB9 row line, Microsoft | Settings_RedeemLine_Microsoft | A gift or promotion code from the Microsoft Store | new |
| Subscription | SB9 Settings rows | Settings_Title / Settings_GroupReading / Language_Title / StoryLanguages_SettingsTitle / Speed_Title / Speed_Value / Settings_GroupPhone / Backup_Title / Settings_About | Settings / Reading / Language / Story languages / Reading speed / {0}x / This phone / Backup / About & updates | built |
| Subscription | RR2 Restored | Store_Restored_One / Store_Restored_Other | 1 book restored. It’s on the shelf. / {0} books restored. They’re on the shelf. | built |
| Subscription | RR3 Nothing found, Google | Store_NothingBought_Google | Nothing bought with this Google account yet. | new |
| Subscription | RR3 Nothing found, Apple | Store_NothingBought_Apple | Nothing bought with this Apple ID yet. | new |
| Subscription | RR3 Nothing found, Microsoft | Store_NothingBought_Microsoft | Nothing bought with this Microsoft account yet. | new |
| Subscription | RR3 message action | Store_RedeemCode | Redeem a code | built |
| Subscription | RR4 Bought it already? (strip) | Error_PaymentRequired | You need to buy this book first. | built |
| Subscription | RR4 link | Store_BoughtItAlready | Bought it already? Restore purchases | new |
| Subscription | SB10 Windows Store, left rail | Gallery_BackToShelf / Gallery_CornerLine (short: Gallery_CornerLineShort) | Back to my child’s shelf / Only grown-ups see this. | built |
| Missing States | Restore and Redeem note (section 2, Store frames) | Store_NothingBought_Google / _Apple / _Microsoft | Nothing bought with this Google account yet. (Apple ID, Microsoft account on the others) | new |
| Missing States | Store frames, results | Store_Restored_One / Store_Restored_Other | 1 book restored. It’s on the shelf. / {0} books restored. They’re on the shelf. | built |
| Missing States | Store frame, redeem | Store_RedeemCode | Redeem a code | built |
| Onboarding | Corner tour, Store tip (page unchanged; build must follow it) | Tour_TipStore | New stories, free and paid, or every book with one subscription. Every book comes in all your family’s languages. Bought before? Restore your books here. | new (changes the built text of this key) |
| iPhone Duo, Fold8, Fold8 Ultra | DO1/FO1/UO1, DI4/FI4/UI4 Kids’ shelf | Gallery_Title / Gallery_GrownUpsPill | My stories / Grown-ups | built |
| iPhone Duo, Fold8, Fold8 Ultra | DO2/FO2/UO2, DI1/FI1/UI1 Corner, Books | Gallery_CornerLine / Gallery_CornerLineShort / Corner_TabBooks / Corner_ChildsShelf | Only grown-ups see this. Your child’s shelf is one tap away. / Only grown-ups see this. / Books / Child’s shelf | built |
| iPhone Duo, Fold8, Fold8 Ultra | DO3/FO3/UO3, DI2/FI2/UI2 Corner, Store | Store_* and Sub_* as Subscription SB1 | see Subscription page | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI3/FI3/UI3 Corner, Settings | Settings_BooksAndBuying, Settings_Subscription, Settings_SubYearly, Store_RestorePurchases, Store_RedeemCode, Settings_AnimalSounds_Title, Settings_On, Usage_Title | as Subscription SB9; Animal sounds; On; Help make Dino better | new (as SB9) / built |
| iPhone Duo, Fold8, Fold8 Ultra | DO4/FO4/UO4, DI5/FI5/UI5 Reading | (story text only) | — | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI6/FI6/UI6 Recording a line | Record_LineOf / Common_Done / Record_Says / Record_Next | Line {0} of {1} / Done / {0} says: / Next | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI7/FI7/UI7 Back cover, left page | Reading_TheEnd | The end | new |
| iPhone Duo, Fold8, Fold8 Ultra | DI7/FI7/UI7 Back cover, left page | Reading_ReadAgain | Read again | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI7/FI7/UI7 Back cover, credits | BackCover_OriginalStoryBy / BackCover_RetoldBy / BackCover_Illustration / BackCover_Date | Original story by {0} / Retold for young readers by {0} / Illustration: {0} / {0} {1} | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI8/FI8/UI8 Grown-ups’ gate | Gallery_ForGrownUps | For grown-ups | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI8/FI8/UI8 Grown-ups’ gate | Gate_HoldDescription | Press and hold for 3 seconds | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI8/FI8/UI8 Grown-ups’ gate | Gallery_BackToShelf | Back to my child’s shelf | built |
| iPhone Duo, Fold8, Fold8 Ultra | DO5/FO5/UO5, DI9/FI9/UI9 Onboarding, languages (step bar) | Onboarding_StepOf + StoryLanguages_SettingsTitle | Step 2 of 6 · Story languages | built |
| iPhone Duo, Fold8, Fold8 Ultra | DO5/FO5/UO5 title | Onboarding_LanguagesTitle | Which languages does your family read in? | built |
| iPhone Duo, Fold8, Fold8 Ultra | DO5/FO5/UO5 line | Onboarding_LanguagesBody | English is always on. Add any others your child hears. | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI9/FI9/UI9 left page | Onboarding_LanguagesBody + Onboarding_WhyMoreBody | English is always on. Many children hear one language at home and another at school or with grandparents. Every book is read in each language you pick, and your child can switch on any page. Pick up to three. | built (the build also keeps "Add any others your child hears." in front; mock and Onboarding WN2 omit it) |
| iPhone Duo, Fold8, Fold8 Ultra | DO5/FO5/UO5, DI9/FI9/UI9 list | StoryLanguages_AppLanguageAlwaysOn | App language · always on | built |
| iPhone Duo, Fold8, Fold8 Ultra | DO5/FO5/UO5, DI9/FI9/UI9 button; DI10/FI10/UI10 | Common_Continue | Continue | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI10/FI10/UI10 Onboarding, listen (step bar) | Onboarding_StepOf + Onboarding_Step_Listen | Step 4 of 6 · Listen | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI10/FI10/UI10 title | Onboarding_ListenTitle | Listen to Dino read | built |
| iPhone Duo, Fold8, Fold8 Ultra | DI10/FI10/UI10 hint | Onboarding_ListenBody | Every character has its own voice, and Dino reads with feeling. | built |
