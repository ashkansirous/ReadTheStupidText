# Slice 396 — Dino: Restore purchases and Redeem a code leave the foot of the Store, into the Store's first line, the 402 strip and the result message
**Product:** Dino

*Built 2026-11-17 (night 2026-11-17-2105, wave 1), merged as PR #1234 (ec3dbbd7); verified on the emulator only, not yet on iOS, the Windows VM or the real stores.* *Planned 2026-11-15 from the 2026-11-15 Subscription mock (`docs/design/dino-reader/Dino Reader 2026-11-15 Subscription.dc.html`, section 4, frames RR1-RR4). The mock trumps Slices 357 (S3), 358, 346 and 394 where they differ.*

**Touches:** `src/Dino/DinoReader.App/StorePage.xaml`, `src/Dino/DinoReader.App/StorePage.xaml.cs` (`PurchaseArea`, `PlacePurchaseArea`, `RestorePurchasesAsync`, the 402 strip), `src/Dino/DinoReader.Core/StoreRowRestore.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**`, `.maestro/dino/` (a Restore flow), `tests/Dino/DinoReader.Tests/**`, `src/Dino/DinoReader.App/CLAUDE.md` (the rule lines named below), `docs/plan/slices/396-dino-restore-and-redeem-out-of-the-store-foot.md` Widened at claim (2026-11-17 premise check): `src/Dino/DinoReader.App/Platforms/Windows/DinoKeyboard.cs` (Ctrl+R), the redeem launchers `src/Dino/DinoReader.App/Platforms/{Android,iOS,Windows}/Purchases/*Purchases.cs`, `src/Dino/DinoReader.App/Purchases/{BookPurchases,IBookPurchases,SwitchablePurchases}.cs` (only if Redeem needs a new call), `docs/plan/notes/2026-11-17-dino-subscription-and-foldables-copy.md` (read only), `src/Dino/DinoReader.App/GalleryPage.xaml` and `GalleryPage.xaml.cs` (title row on the Store tab only; also `src/Dino/DinoReader.App/BusyGuard.cs` for an optional cancel token, check-pass fix 1, added during the build so the first line matches RR1-RR4; Slice 395 edits its record-demo part).

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

- [x] 1. Android: remove `PurchaseArea` and `PlacePurchaseArea`; add the first-line link and its in-progress state, the dark message (5 s, above the tab bar), the 402 link. Redeem opens Play's redeem screen.
- [x] 2. iOS: the same (shared XAML); Redeem opens Apple's redeem sheet; the Apple message variant.
- [x] 3. Windows: the same; Ctrl+R; Microsoft's redeem page; the Microsoft message variant.
- [x] 4. Pure `StoreRowRestore` gets the message choice (restored one / many / subscription back / nothing) with the store name; unit tests for each row and plural.
- [x] 5. Strings (`Store_Restoring`, `Store_NothingBought_*`, `Store_BoughtItAlready`) in `AppResources.resx` and every `AppResources.<lang>.resx` (15 languages; `NoHardCodedUiTextTests`). Remove the unused `Store_BoughtBefore*` and `Store_NothingToRestore` keys everywhere.
- [x] 6. `src/Dino/DinoReader.App/CLAUDE.md`: change any line that says Restore and Redeem sit in the card at the foot of the Store (Slice 357 S3) to the three places above.
- [x] 7. Maestro flow: the Store's first line shows "Restore purchases"; tap it with the fake store holding two books, check the message and "On the shelf"; with none, check the Redeem action; no "Bought books before?" anywhere.

## Verification

- Dino tests pass. Android emulator: the Restore flow in item 7, and tapping every age chip (Slice 394's flow, which re-taps "9–12") never crashes.
- iOS simulator and Windows VM: the first line and the message are shown; Ctrl+R on Windows.
- The user taps Restore on the real stores (sandbox) after release.

## Out of scope

- A Redeem link on the Store's first line.

## Built

The Bought-books card is gone. "Restore purchases" is a link on the Store's first line, right of "Picked and cast for Dino" (moved from `GalleryPage`'s `ContentSub` into `StorePage`), with a spinner and "Restoring…" while it runs. The result is a dark message above the tab bar for 5 s: green for restored or all-on-shelf (`Store_OnShelf`), amber for nothing-found, naming Google, Apple ID or Microsoft, with "Redeem a code" inside. The 402 strip carries an inline "Bought it already? Restore purchases". Ctrl+R restores on Windows (grown-up mode, Store on top, no text focus). `StoreRowRestore` gained `RestoreLine.NothingFound`, `SubscriptionBack` and `AllOnShelf`. Five new strings in the 15 resx files (the 14 translations are machine-written); `Store_BoughtBefore*` and `Store_NothingToRestore` are removed. `BusyGuard.RunWhenFreeAsync` takes an optional cancel token. Restore errors report `FallbackArea.Store`; offline and server errors show `Error_Server` text, and a timeout is not a silent cancel.

*Changed while building:* `GalleryPage.xaml`, `GalleryPage.xaml.cs` and `BusyGuard.cs` were added to Touches. `.maestro/dino/paid-books.yaml` and `store-payment-required.yaml` were updated for the new layout (Get sits below the new first line, so `scrollUntilVisible`; the 402 assert matches the strip's whole label); a new `.maestro/dino/store-restore.yaml` covers Restore and Redeem.

*Checks:* DinoReader.Tests 4607 (2 skipped), Shared.Tests 1722, lint-flows 142/0, Android and Windows Debug builds 0 errors (SDK 11 from `s379b/tmp/dotnet`: this PC has no system SDK 11). Emulator-5554 (x64 build 2d6780443186): `store-restore`, `paid-books`, `subscription` and `store-payment-required` all PASS, after the two flow fixes. S21 run 0/4: `tools/phone` has no Maestro runner and all four stopped at FirstRunLanguage (a harness gap, not an app result; evidence in `reviews/s396-device/`).

## From review

- Skipped #5 (`reviews/pr-1234-high.md`): "ShowRestoreMessage builds icons from raw path strings with a new PathGeometryConverter per show; RestoreGlyph/CheckGlyph/AlertGlyph duplicate geometry while IconView (IconKind.Check) already exists; two icon systems on one page, tokens can drift." Low, style only; the mock's icons stay as built.
- Skipped #3 (`reviews/pr-1234-medium.md`): "A Restore queued behind a held _busy (Buy/Redeem) shows "Restoring..." with no Cancel or timeout; a second request is silently dropped while _restoring is true." Low; the second request would run the same restore again, and the first one is already showing its spinner.
- Skipped #4 (`reviews/pr-1234-medium.md`): "The restore message auto-dismisses after 5 s and now holds the only route to "Redeem a code" (button removed from the Store), so the action can vanish mid-tap; no persistent redeem route." The slice and mock RR1-RR4 set the 5 s hide; the store-restore flow taps Redeem inside it, and Settings (Slice 399) is the persistent route.
- Skipped #5 (`reviews/pr-1234-medium.md`): "Static _onScreenStore strong reference is cleared only via Disappearing/HandlerChanged; a page torn down without them leaks and Ctrl+R hits a detached view." Low, speculative.
- Skipped #6 (`reviews/pr-1234-medium.md`): "Message icon builds a new PathGeometryConverter and parses the path on every message; (string)Resources[...] cast throws at runtime if the resource type differs. Use preloaded Paths or a trigger." Low; same as high #5.
- Skipped #7 (`reviews/pr-1234-medium.md`): "Redeem coverage removed from this flow (expect-log and StoreRedeemCode tap) and now depends on store-restore.yaml; the flow also asserts `[StoreRestore] message=Restored`, tying it to the ownsBooks rule." Low; the Redeem tap is covered by store-restore.yaml, which runs in the same device check.
- Skipped #8 (`reviews/pr-1234-medium.md`): "Ctrl+R held-key repeats (WasKeyDown) fall through to the focused control instead of being swallowed and added to _used like the Record keys." Low; `_restoring` already stops a repeated restore.

## Follow-ups (from review)

- #2 (`reviews/pr-1234-check1-medium.md`): "Restore's book-download wait runs outside _busy; Buy/Subscribe/Redeem can replace _allRows meanwhile. Scenario: restore then computes arrived/ownsBooks from changed rows, shows wrong count or overwrites the Buy message."
- #5 (`reviews/pr-1234-check1-medium.md`): "ownsBooks has a dead _allRows.Any(BoughtProduct...) branch; ownedNow may hold only a subscription/consumable id. Scenario: user sees "On the shelf" with no books restored instead of "nothing found"."
- #6 (`reviews/pr-1234-check1-medium.md`): "HideRestoreMessageLaterAsync sets IsVisible=false after its delay with no newer-message check. Scenario: A's delay completes as B is shown; B is hidden at once."
- #7 (`reviews/pr-1234-check1-medium.md`): "The 402 "Restore purchases" strip is a gesture on a Span with no button semantics. Scenario: TalkBack/VoiceOver user cannot find the 402 recovery action."
- #8 (`reviews/pr-1234-check1-medium.md`): "ContentSub/Store_Subtitle remain in GalleryPage.xaml while the subtitle is duplicated in StorePage (GalleryStoreSub). Scenario: stale subtitle from previous tab, two elements for one id in Maestro/UIA."
- #9 (`reviews/pr-1234-check1-medium.md`): "ShowRestoreMessage re-parses geometry and casts resource strings per call. Scenario: a missing or mistyped resource key throws InvalidCastException with no catch."
- #10 (`reviews/pr-1234-check1-medium.md`): "LineFor's new optional flags default false, so old call shapes now mean NothingFound. Scenario: a caller omitting ownsBooks reports "nothing bought" for an owner; make the flags required."
- #1 (`reviews/pr-1234-check2-medium.md`): "src/Dino/DinoReader.App/StorePage.xaml:338, medium: Redeem button gone; only reachable via 5 s nothing-found message. Comment says Settings (Slice 399) but 399 is only a plan on origin/main; RedeemCodeAsync called only from OnRedeemCodeClicked. A family with a promo code cannot reach redeem on Android, iOS or Windows until 399 ships." Redeem moves to Settings with Slice 399 (planned); until then it is reachable from the nothing-found message only, as the mock RR1-RR4 shows.
- #1 (`reviews/pr-1234-check3-medium.md`): "src/Dino/DinoReader.App/StorePage.xaml.cs:1127, low: HideRestoreMessageLaterAsync does not re-check its token after the delay." Not applied: convergence rule after the second check pass (only crash, data-loss, security, privacy, missing-twin or loading-state faults reopen code).
- #2 (`reviews/pr-1234-check3-medium.md`): "src/Dino/DinoReader.App/StorePage.xaml.cs:1039, low: with an empty catalog (_allRows empty) a restore ends with no message at all." Not applied: convergence rule after the second check pass (only crash, data-loss, security, privacy, missing-twin or loading-state faults reopen code).
- #3 (`reviews/pr-1234-check3-medium.md`): "src/Dino/DinoReader.Core/StoreRowRestore.cs:34, low: LineFor got defaulted params (subscriptionActive, ownsBooks = false); an old-form caller reports "Nothing bought" for an owning account. Make them required." Not applied: convergence rule after the second check pass (only crash, data-loss, security, privacy, missing-twin or loading-state faults reopen code).
- #1 (`reviews/pr-1234-check4-medium.md`): ".maestro/dino/store-restore.yaml:~103, medium-low: assertVisible StoreRestoreResult after the 2-books wait can race the 5 s hide." Not applied: convergence rule after the second check pass; the flow passed on the emulator.
- #2 (`reviews/pr-1234-check4-medium.md`): ".maestro/dino/store-restore.yaml:~111, low: scrollUntilVisible "On the shelf" index 1 does not prove the two restored books are on the shelf (slice item 7)." Not applied: convergence rule after the second check pass; the flow passed on the emulator.

#### For CLAUDE.md (lifted 2026-11-17)

- Restore purchases is a link on `StorePage`'s first line (not in `GalleryPage`'s title row, not a card at the foot); its result is a 5 s message above the tab bar, and the 402 strip carries its own Restore link. Redeem a code lives only inside the nothing-found message until Slice 399 puts it in Settings. Ctrl+R restores on Windows (`Platforms/Windows/DinoKeyboard.cs`). (Slice 396: docs/plan/slices/396-dino-restore-and-redeem-out-of-the-store-foot.md)
- Restore errors report `FallbackArea.Store`; offline shows the offline text, anything else `Error_Server`, and a timeout is never a silent cancel (`StorePage.xaml.cs`). `BusyGuard.RunWhenFreeAsync` takes an optional cancel token. (Slice 396: docs/plan/slices/396-dino-restore-and-redeem-out-of-the-store-foot.md)

#### Not yet verified

- iOS compile and iOS run of the Store's first-line Restore link and the result message.
- Windows head run on the VM: Ctrl+R and the first-line layout.
- Real-store Restore and Redeem on Play, the App Store and the Microsoft Store (sandbox, owner).
- Native read of the 14 machine-translated strings.
- Screen reader announcement of the restore result (TalkBack, VoiceOver, Narrator).

#### Follow-ups (not this slice)

- `tools/phone`: add a runner for the UI-flow files; the S21 run of this slice was 0/4 because none exists (all four stopped at FirstRunLanguage; evidence in `reviews/s396-device/`).
- #3 (`reviews/pr-1234-check2-medium.md`): "src/Dino/DinoReader.App/StorePage.xaml.cs:1075, low (unconfirmed): ownsBooks true for any non-empty ownedNow; non-book owned entries (old subscription id) could give green "On the shelf" instead of NothingFound. Not confirmed whether Snapshot.Owned can hold non-book ids."
- #4 (`reviews/pr-1234-check2-medium.md`): "src/Dino/DinoReader.App/StorePage.xaml.cs:936, low: _restoring stays true until the cancelled old run unwinds after Disappearing; a Restore tap or Ctrl+R right after returning is silently ignored, link may show "Restoring..." for a cancelled run."
- #5 (`reviews/pr-1234-check2-medium.md`): "src/Dino/DinoReader.App/StorePage.xaml.cs:1020, low: cancel after RunWhenFreeAsync takes the gate does not stop the work (runs with CancellationToken.None); _busy held for the whole store round-trip, blocking other Store actions meanwhile. Result is discarded, so no data loss."
- #6 (`reviews/pr-1234-check2-medium.md`): "src/Dino/DinoReader.App/StorePage.xaml.cs:1118, low: actionable "Redeem a code" sits in a transient 5 s message; SemanticScreenReader.Announce gives TalkBack/VoiceOver no way to activate it. With finding 1, no accessible route to redeem."
