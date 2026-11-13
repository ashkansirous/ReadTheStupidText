# Slice 364 — Slice 357 design pass: the Missing States mock's items

*Built 2026-11-13, merged as PR #1062 (96133a3e). Part A and the Part C strings are built; RT1/RT2 row layouts are not (see *Waiting on the user*). Slice 357's file is not ticked: the 357 mock items built here are listed under *Work*.*

**Touches:** `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/StoreRows*.cs` and `src/DinoReader.Core/StoreRow*.cs` (new if needed; on origin/main the row state lives in `StorePage.xaml.cs`), `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.Core/HttpErrorKind*.cs`, `src/DinoReader.Core/BookAccess.cs` (price tag), `src/DinoReader.App/Purchases/BookPurchases.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources.cs`, `src/ReadTheStupidText.App/Strings/*/Resources.resw`, the voice-pack row views in `src/ReadTheStupidText.Mobile/**` (Settings > Voices) and `src/ReadTheStupidText.App/**` (control-panel voice row; find them by `Wait_Retry` / `LanguageRetry`), `src/ReadTheStupidText.Mobile/LocalizedPackStatus.cs`, `src/ReadTheStupidText.App/PackSetupText.cs`, `src/DinoReader.App/Testing/TestStore.cs`, `.maestro/dino/store-server-errors.yaml`, `.maestro/dino/store-payment-required.yaml`, `.maestro/dino/paid-books.yaml` (restore alert gone, buttons moved into the list footer), `tests/ReadTheStupidText.Tests/*`, `tests/DinoReader.Tests/*` (read Slice 357's *Touches* line for the full list and trim to what these items need).

## Why

Slice 357 ([file](357-server-error-messages.md), merged #1052) was built to the plan as it stood before the 2026-11-13 Missing States mock. Its Parts A-C are built; the items that came from the mock are still unticked there and are built here. Build every unticked work item in 357's file that came from the mock; **Slice 357's file wins** where this one differs. Do not retype them here: follow them in 357.

## Work

Follow 357's *Design (Dino Reader 2026-11-13 Missing States mock, section 2)* section and the copy note `docs/plan/notes/2026-11-13-dino-copy.md` (*Missing States*), and these items of 357's *Work*:

- [x] **A.** `StorePage` failed row: the message strip inside the row's card (Design S2) with `Store_TryAgain`, `Error_PaymentRequired` keeping the price button, the strip going away when the next try starts, the list failure text in place of the offline panel.
- [x] **A. S1, prices and tags:** the store price with a cart glyph, the green `Store_Free` tag (new in all 15 languages), `Store_Get`, the ring and percent, `Store_OnShelf`.
- [x] **A. S3, "Bought books before?" card** at the foot of the Store list (docked at the page bottom when the list is empty or offline), replacing `PurchaseActions`.
- [x] **A. Restore puts the books on the shelf:** download the restored books, count the arrivals, `Store_Restored_One` / `_Other` and `Store_NothingToRestore`; `Store_Restored` (alert) goes.
- [x] **A. Strings** in `AppResources.resx` and every `AppResources.<lang>.resx` (all 15 languages; the non-English text is machine-drafted).
- [ ] **C. RT1 and RT2 rows:** the strip under a failed voice row with `Error_Server` / `Error_VoiceGone`, `Wait_Retry` / `LanguageRetry.Content`, `Pack_RowSize`, `Pack_NotDownloaded`, with the RTST strings in the Mobile `.resx` and App `.resw` languages. **Strings only built:** `Error_Server` / `Error_VoiceGone` (404 maps to `Error_VoiceGone`) in the RTST Mobile `.resx` and App `.resw`. The row layouts (`Pack_RowSize`, `Pack_NotDownloaded`, the strip under a voice row) are not built: the plan's premise was wrong, main has no per-language voice-row list on Mobile (only `LanguagePage`, the `VoicePickerPage` status line and `ReaderPage`) and no Retry voice row in the Windows `ControlPanelWindow` / `LanguageWindow`.

## Waiting on the user

- *Answered without the user, 2026-11-13:* RT1/RT2 do have rows to live on. The phone `LanguagePage` list and the Windows `LanguageWindow` list are the voice rows; they only lack a size, state and Retry of their own. Item C's row layouts moved to [Slice 373](373-rtst-voice-rows-rt1-rt2.md) (planned).
- *Decided 2026-11-13 (obvious copy choice):* keep the copy note's single "nothing to restore" line when every bought book is already on the shelf; no extra line.

## Changed while building

- Restore design, after two review rounds: a per-visit cancel, one pure `StoreRowRestore.LineFor(fetched, arrived, alreadyDownloading)` in `src/DinoReader.Core/StoreRowRestore.cs`, the result line shown only when the run ends, and Restore re-checks the row state before fetching.
- The 404 step moved from `store-server-errors.yaml` to the end of `store-payment-required.yaml` (a 404 row has no button to continue from). `paid-books.yaml` now scrolls to the footer card and the result line.

## From review

- `reviews/s364-high.md` (9 findings, read 9/9): applied #1-4.
- Skipped #5 (`reviews/s364-high.md`): "no IsGrownUp re-check and no cancel when the user leaves the tab" - Restore runs behind the grown-up gate already; books still arriving on the shelf after leaving the page is the result Restore promises.
- Skipped #6 (`reviews/s364-high.md`): "no compile-time check, KeyNotFoundException at runtime if renamed" - style, the keys follow the page's existing pattern.
- Skipped #7 (`reviews/s364-high.md`): "BoughtBeforeLine treats every non-phone idiom" - wording choice for non-phone idioms, not a defect.
- Skipped #8 (`reviews/s364-high.md`): "so on a long list they are behind the scroll" - the mock S3 places the card at the foot of the list.
- Skipped #9 (`reviews/s364-high.md`): "GetLabel is a constant but is still raised on every state change" - the mock shows no button on a 404 row; GetLabel and flicker gave no failure path.
- `reviews/s364-medium.md` (9 findings, read 9/9): applied #1-4 (as the Restore design above) and #8.
- Skipped #5 (`reviews/s364-medium.md`): "Unauthorized/Forbidden collapsed into generic" server text - the mock shows Error_Server for other 4xx.
- Skipped #6 (`reviews/s364-medium.md`): "the method returns silently after RestoreAsync/ReplanRowsAsync already ran" - the grown-up gate cancelled means they backed out, so no message is right.
- Skipped #7 (`reviews/s364-medium.md`): "maps any non-iOS/non-WinUI platform to Google text" - wording choice, as high #7.
- Skipped #9 (`reviews/s364-medium.md`): "duplicates SoundStatusPillRing progress plumbing" - file name, style.
- `reviews/s364-check.md` (7 findings, read 7/7): applied #1, 2, 6.
- Skipped #3 (`reviews/s364-check.md`): "safe only because _busy serialises restores" - the finding says it is safe; the `_busy` serialisation is accepted.
- Skipped #4 (`reviews/s364-check.md`): "already has a parent" - the emulator checked the footer move: no crash.
- Skipped #5 (`reviews/s364-check.md`): "401/403 now map to generic server text" - as medium #5.
- Skipped #7 (`reviews/s364-check.md`): "Restore count includes rows the user fetched by hand" - books already downloading arrive during Restore, so counting them is the design.
- `reviews/s364-check2.md` (9 findings, read 9/9): none applied (listed below).
- Skipped #1 (`reviews/s364-check2.md`): "ShowOffline reuses StoreText.ErrorMessage, which has book-specific texts" - moved to *Not yet verified*: whether the list endpoint can answer 402/404 is not checked.
- Skipped #2 (`reviews/s364-check2.md`): "the later subscribe leak" - low leak; `_busy` serialises restores.
- Skipped #3 (`reviews/s364-check2.md`): "disposed only when the next restore starts" - low dispose issue; `_busy` serialises restores.
- Skipped #4 (`reviews/s364-check2.md`): "so cancel only takes effect between rows" - by design.
- Skipped #5 (`reviews/s364-check2.md`): "MAUI handlers may keep the old parent" - the device run passed.
- Skipped #6 (`reviews/s364-check2.md`): "it goes stale after a manual Get, retry or ownership change" - the line clears on the next Restore.
- Skipped #7 (`reviews/s364-check2.md`): "re-downloaded on every Restore tap" - a Failed NotFound row retrying on Restore is intended.
- Skipped #8 (`reviews/s364-check2.md`): "file name does not match the class" - style.
- Skipped #9 (`reviews/s364-check2.md`): "pass arriving.Count and arrived instead" - style.
- `reviews/s364-check3.md` (2 findings, read 2/2): none applied (listed below).
- Skipped #1 (`reviews/s364-check3.md`): "No bought books found on this account." - the copy note has one "nothing to restore" line; a separate "all already on your shelf" line is a copy decision, added under *Waiting on the user* (blocks nothing).
- Skipped #2 (`reviews/s364-check3.md`): "with no mention of the failed one" - a failed book's row shows its own error strip, so the count line need not repeat it.

## Device run

Emulator-5554, Dino cache 3e671eda1cb8: `store-server-errors` PASS, `store-payment-required` PASS, `paid-books` FAIL once (the result line was off screen; log `[StoreRestore] toFetch=0 downloading=0 arrived=0 line=NothingToRestore`), PASS after the flow fix (redeem opened, outcome=Bought). Logs: `D:/ReadTheStupidText-worktrees/reviews/w53-device/s364-dino-paid-books.log`, `s364-flow-paid-books.log`.

#### For CLAUDE.md (lifted 2026-11-13)

- Dino `StorePage` restore: the result line comes from the one pure `StoreRowRestore.LineFor` in `src/DinoReader.Core/StoreRowRestore.cs`, shown only when the run ends; restore re-checks a row's state before fetching and cancels per visit. Do not add a second place that counts arrivals. (Slice 364: docs/plan/slices/364-server-errors-design-pass.md)

#### Not yet verified

- Windows head store page (Dino) not run on the VM (5.8 GB free, VM not started).
- RTST App x64 voice error text (`Error_Server` / `Error_VoiceGone`) not run on the VM.
- RTST Android pack-failure text not run on a device.
- iOS compile and TestFlight not run.
- Native reads of the 14 non-English languages (machine-drafted).
- A list-load 402/404 shows a book-specific text (`ShowOffline` reuses `StoreText.ErrorMessage`): check whether the list endpoint can answer those.

## What Slice 357 already built that this pass reworks

- Per-status strings under other keys (not the mock's `Error_*` keys): the pass replaces them with the `Error_*` wording from the copy note, including the mock's changed 5xx and 404 text.
- The Dino shop's server-error panel (distinct from the offline one) and the per-status row error with `SetState(state, error)`: restyled as the strip and kept distinct.
- The RTST pack-failure texts (`PackDownloadOutcome`, `PackCatalogResult.ServerAnswer`): the new keys replace the strings, the way the kind travels stays.
- The two Maestro flows `store-payment-required.yaml` and `store-server-errors.yaml` keep their split (a 402 re-plans the row to Buy); their expected texts change with the keys.

Part D of Slice 357 (the `minVersion` change) stays in 357.

## Verification

As Slice 357's *Verification* section (the Restore and RT lines included).
