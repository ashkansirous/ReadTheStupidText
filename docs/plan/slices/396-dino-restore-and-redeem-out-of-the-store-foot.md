# Slice 396 — Dino: Restore purchases and Redeem a code leave the foot of the Store, into the Store's first line, the 402 strip and the result message
**Product:** Dino

*Planned 2026-11-15 from the 2026-11-15 Subscription mock (`docs/design/dino-reader/Dino Reader 2026-11-15 Subscription.dc.html`, section 4, frames RR1-RR4). The mock trumps Slices 357 (S3), 358, 346 and 394 where they differ. Not built yet.*

**Touches:** `src/Dino/DinoReader.App/StorePage.xaml`, `src/Dino/DinoReader.App/StorePage.xaml.cs` (`PurchaseArea`, `PlacePurchaseArea`, `RestorePurchasesAsync`, the 402 strip), `src/Dino/DinoReader.Core/StoreRowRestore.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**`, `.maestro/dino/` (a Restore flow), `tests/Dino/DinoReader.Tests/**`, `src/Dino/DinoReader.App/CLAUDE.md` (the rule lines named below), `docs/plan/slices/396-dino-restore-and-redeem-out-of-the-store-foot.md`

## Why

The mock (section 4): the shipped "Bought books before?" card sits after every book at the foot of the Store list, so nobody finds it, and moving it in and out of the list's footer is the likely cause of the "9–12" crash (Slice 394, item 2). The card goes. Restore and Redeem live where people look.

## Decisions (the mock, 2026-11-15; the mock supersedes Slice 357 S3, "Restore and Redeem live there, not in Settings")

1. The "Bought books before?" card (`PurchaseArea`, strings `Store_BoughtBeforeTitle`, `Store_BoughtBefore_*`) is removed from the Store, with its footer placement. **Slice 357 S3's card, its green/sand result line under the card and "Restore and Redeem live there, not in Settings" are superseded by the mock.** `PlacePurchaseArea` goes with it, so no view moves between the list footer and the page, which also removes Slice 394 item 2's crash cause. Slice 394 item 2 is then done by this slice; 394 keeps its crash proof (item 1) and the empty-list check.
2. Restore purchases and Redeem a code live in three places: (1) a Restore purchases link on the Store's first line (RR1); (2) Settings, Books and buying (Slice 399); (3) where it is needed: the 402 strip (RR4) and, for Redeem, the "nothing found" message (RR3).
3. Redeem a code is kept out of the Store's first line on purpose (few families have a code; 375 wide has room for one link). Same rule on Windows; Ctrl+R restores from the Store tab.
4. Results are a dark message pinned above the tab bar for 5 seconds, never at the end of a list. The wait overlay is not used: nothing blocks.
5. **User, 2026-11-16 (settled, never re-ask): the "Bought books before?" card goes.** The mock's three places above are the whole answer. Its text, keys and every string for these frames are in `docs/plan/notes/2026-11-17-dino-subscription-and-foldables-copy.md` (rows "Subscription RR1-RR4"); this slice cites those keys and invents no wording.
6. **Link to Slice 394 item 1 (the crash).** Two crash reports (Dino Android 1.129.2, `Java.Lang.IllegalStateException` "The specified child already has a parent" via `LayoutHandler.MapAdd`, on a tap) match `PurchaseArea` being moved by `PlacePurchaseArea` when the "9–12" chip empties the list. Removing `PurchaseArea` and `PlacePurchaseArea` likely fixes it (likely, not proven). Slice 394 item 1 still runs (it proves or disproves the cause), and its verification re-taps "9–12" on the build that has this slice.
7. **"Nothing found" wording (the mock wins; the copy note's open point is resolved).** Restore with nothing found uses `Store_NothingBought_Google` / `_Apple` / `_Microsoft` ("Nothing bought with this Google account yet."; "Apple ID", "Microsoft account"), replacing the built `Store_NothingToRestore`, because Restore also brings back a subscription.

## Design (2026-11-15 Subscription mock, section 4: RR1-RR4; four phone frames, all UI; the books in them are placeholder rows; the mock disagrees with Slice 357 S3 and is followed)

- **RR1, Store first line** (above the chips and the subscription card; the age chips follow Slice 394). Row padding 4 14 2 18, gap 8: "Picked and cast for Dino" (`#5a5470`, 13.5, line 1.4, flex 1) on the left; on the right "Restore purchases" (`Store_RestorePurchases`, restore icon 17, text `#4a46d6`, 14 / 600, tap area 36 high). While running the icon becomes a 17 px spinner (track `#d6cffc`, arc `#4a46d6`, width 3) and the text "Restoring…" (new `Store_Restoring`); the rest of the Store stays usable. Every Store frame of Slice 397 has this line.
- **RR2, Restored.** Message: `role=status`, pinned left 14 / right 14 / bottom 10 above the tab bar, min 48 high, padding 10 14, radius 16, fill `#2b2540`, text white 14 / 500, line 1.35, shadow `0 10px 24px rgba(20,14,40,.3)`, gap 10, a leading 18 px icon. Text: "2 books restored. They’re on the shelf." (built `Store_Restored_One` "1 book restored. It’s on the shelf." and `Store_Restored_Other` "{0} books restored. They’re on the shelf."); "You are subscribed. Every book is open." (built `Sub_Active`) when a subscription came back. Shown 5 seconds. The restored books' rows change at the same time: downloading (ring and percent), then "On the shelf" (`Store_OnShelf`).
- **RR3, Nothing found.** Same dark message, leading icon amber `#FFC24D` (circle with "!", 18 px): "Nothing bought with this Google account yet." with a **Redeem a code** action (`Store_RedeemCode`) inside the message. New strings per store: `Store_NothingBought_Google` "Nothing bought with this Google account yet.", `_Apple` "…with this Apple ID yet.", `_Microsoft` "…with this Microsoft account yet." (the mock names the Google one and says Apple ID and Microsoft account for the others). Redeem opens the store's own redeem screen, as built. The built `Store_NothingToRestore` ("No bought books found on this account.") is superseded.
- **RR4, the 402 strip.** The strip under a book (built `Error_PaymentRequired` "You need to buy this book first.") gets a link "Bought it already? Restore purchases" (new `Store_BoughtItAlready`; the link runs the same restore). The same link goes on Slice 398's banner.
- **Windows:** Ctrl+R on the Store tab runs Restore (the rail shows "Ctrl+R" next to Store in Slice 397's SB10).
- **Copy keys (the 2026-11-17 copy note).** Built: `Store_RestorePurchases` "Restore purchases", `Store_Restored_One` / `_Other`, `Sub_Active`, `Store_RedeemCode` "Redeem a code", `Error_PaymentRequired`. New: `Store_Restoring` "Restoring…", `Store_NothingBought_Google` / `_Apple` / `_Microsoft`, `Store_BoughtItAlready` "Bought it already? Restore purchases". Gone: `Store_NothingToRestore`, `Store_BoughtBefore*`.
- **Foldable frames** (iPhone Duo, Fold8, Fold8 Ultra: Restore purchases on the Store title row) are built by Slice 403; the Restore behaviour is this slice's.

## Work

- [ ] 1. Android: remove `PurchaseArea` and `PlacePurchaseArea`; add the first-line link and its in-progress state, the dark message (5 s, above the tab bar), the 402 link. Redeem opens Play's redeem screen.
- [ ] 2. iOS: the same (shared XAML); Redeem opens Apple's redeem sheet; the Apple message variant.
- [ ] 3. Windows: the same; Ctrl+R; Microsoft's redeem page; the Microsoft message variant.
- [ ] 4. Pure `StoreRowRestore` gets the message choice (restored one / many / subscription back / nothing) with the store name; unit tests for each row and plural.
- [ ] 5. Strings (`Store_Restoring`, `Store_NothingBought_*`, `Store_BoughtItAlready`) in `AppResources.resx` and every `AppResources.<lang>.resx` (15 languages; `NoHardCodedUiTextTests`). Remove the unused `Store_BoughtBefore*` and `Store_NothingToRestore` keys everywhere.
- [ ] 6. `src/Dino/DinoReader.App/CLAUDE.md`: change any line that says Restore and Redeem sit in the card at the foot of the Store (Slice 357 S3) to the three places above.
- [ ] 7. Maestro flow: the Store's first line shows "Restore purchases"; tap it with the fake store holding two books, check the message and "On the shelf"; with none, check the Redeem action; no "Bought books before?" anywhere.

## Verification

- Dino tests pass. Android emulator: the Restore flow in item 7, and tapping every age chip (Slice 394's flow, which re-taps "9–12") never crashes.
- iOS simulator and Windows VM: the first line and the message are shown; Ctrl+R on Windows.
- The user taps Restore on the real stores (sandbox) after release.

## Out of scope

- A Redeem link on the Store's first line.
