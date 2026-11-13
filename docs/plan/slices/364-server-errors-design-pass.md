# Slice 364 — Slice 357 design pass: the Missing States mock's items

*Planned 2026-11-13 (index pass, from Slice 357's open items). Not started.*

**Touches:** `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/StoreRows*.cs` and `src/DinoReader.Core/StoreRow*.cs` (the row state and strip), `src/DinoReader.Core/BookAccess.cs` (price tag), `src/DinoReader.App/Purchases/BookPurchases.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources.cs`, `src/ReadTheStupidText.App/Strings/*/Resources.resw`, the voice-pack row views in `src/ReadTheStupidText.Mobile/**` (Settings > Voices) and `src/ReadTheStupidText.App/**` (control-panel voice row; find them by `Wait_Retry` / `LanguageRetry`), `src/ReadTheStupidText.Mobile/LocalizedPackStatus.cs`, `src/ReadTheStupidText.App/PackSetupText.cs`, `src/DinoReader.App/Testing/TestStore.cs`, `.maestro/dino/store-server-errors.yaml`, `.maestro/dino/store-payment-required.yaml`, `tests/ReadTheStupidText.Tests/*`, `tests/DinoReader.Tests/*` (read Slice 357's *Touches* line for the full list and trim to what these items need).

## Why

Slice 357 ([file](357-server-error-messages.md), merged #1052) was built to the plan as it stood before the 2026-11-13 Missing States mock. Its Parts A-C are built; the items that came from the mock are still unticked there and are built here. Build every unticked work item in 357's file that came from the mock; **Slice 357's file wins** where this one differs. Do not retype them here: follow them in 357.

## Work

Follow 357's *Design (Dino Reader 2026-11-13 Missing States mock, section 2)* section and the copy note `docs/plan/notes/2026-11-13-dino-copy.md` (*Missing States*), and these items of 357's *Work*:

- [ ] **A.** `StorePage` failed row: the message strip inside the row's card (Design S2) with `Store_TryAgain`, `Error_PaymentRequired` keeping the price button, the strip going away when the next try starts, the list failure text in place of the offline panel.
- [ ] **A. S1, prices and tags:** the store price with a cart glyph, the green `Store_Free` tag (new in all 15 languages), `Store_Get`, the ring and percent, `Store_OnShelf`.
- [ ] **A. S3, "Bought books before?" card** at the foot of the Store list, replacing `PurchaseActions`.
- [ ] **A. Restore puts the books on the shelf:** download the restored books, count the arrivals, `Store_Restored_One` / `_Other` and `Store_NothingToRestore`; `Store_Restored` (alert) goes.
- [ ] **A. Strings** in `AppResources.resx` and every `AppResources.<lang>.resx` (all 15 languages).
- [ ] **C. RT1 and RT2 rows:** the strip under a failed voice row with `Error_Server` / `Error_VoiceGone`, `Wait_Retry` / `LanguageRetry.Content`, `Pack_RowSize`, `Pack_NotDownloaded`, with the RTST strings in the Mobile `.resx` and App `.resw` languages.

## What Slice 357 already built that this pass reworks

- Per-status strings under other keys (not the mock's `Error_*` keys): the pass replaces them with the `Error_*` wording from the copy note, including the mock's changed 5xx and 404 text.
- The Dino shop's server-error panel (distinct from the offline one) and the per-status row error with `SetState(state, error)`: restyled as the strip and kept distinct.
- The RTST pack-failure texts (`PackDownloadOutcome`, `PackCatalogResult.ServerAnswer`): the new keys replace the strings, the way the kind travels stays.
- The two Maestro flows `store-payment-required.yaml` and `store-server-errors.yaml` keep their split (a 402 re-plans the row to Buy); their expected texts change with the keys.

Part D of Slice 357 (the `minVersion` change) stays in 357.

## Verification

As Slice 357's *Verification* section (the Restore and RT lines included).
