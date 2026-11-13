# Slice 357 — Server errors the user can see: standard messages per status, the server's text only on 400

*Planned 2026-11-13 (user, RefineScope).*

**Touches:** `src/ReadTheStupidText.Application/Net/HttpErrorKind.cs` (new), `src/ReadTheStupidText.Application/Net/HttpErrorClassifier.cs` (new), `src/ReadTheStupidText.Application/Reading/PackErrorClassifier.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/Purchases/BookPurchases.cs`, `src/DinoReader.App/Platforms/Android/Purchases/AndroidPurchases.cs`, `src/DinoReader.App/Platforms/iOS/Purchases/IosPurchases.cs`, `src/DinoReader.App/Platforms/Windows/Purchases/WindowsPurchases.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/ReadTheStupidText.Infrastructure/Reading/WindowsVoicePackService.cs`, `src/ReadTheStupidText.App/Strings/*/Resources.resw`, `src/DinoReader.App/StoreRows*.cs` and `src/DinoReader.Core/StoreRow*.cs` (the row state and strip, if split out), `src/DinoReader.Core/BookAccess.cs` (price tag), the voice-pack row views in `src/ReadTheStupidText.Mobile/**` (Settings > Voices) and `src/ReadTheStupidText.App/**` (control-panel voice row; find them by `Wait_Retry` / `LanguageRetry`), `worker/stories.ts`, `worker/stories.test.mjs`, `tests/ReadTheStupidText.Tests/Net/*` (new), `tests/DinoReader.Tests/*`, `src/DinoReader.App/CLAUDE.md`, `worker/CLAUDE.md`

## Why

On 2026-11-13 the user tried to get a paid book from the Dino shop on an older app version. It failed with no message.
What the code shows (read from origin/main 2026-11-13, `ground-in-code`):
- An app from before paid books sends no purchase proof, so the Worker answers **402** `{"code":"payment-required"}`
  (`worker/stories.ts:607`).
- `StoreClient` (`src/DinoReader.App/StoreClient.cs:290-321`) catches every failure, reports a fallback event
  (`FallbackArea.Store`, `StoreUnreachable`/`StoreFailed`) and shows nothing. `StorePage.xaml.cs:697-724` turns any failure
  into `StoreRowState.Failed`, which shows only "Try again" (`:78`). 402, 403, 404, 429 and 5xx all look the same.
- There is no shared error-to-message layer. Each client has its own `HttpClient` and its own catches. The only existing
  network texts are `Store_OfflineTitle`/`Store_OfflineBody`, `Store_TryAgain`, `Store_BuyFailed` (`AppResources.resx:62-64, 369`).
- The Worker sends no sentences today. `errorResponse(code, status)` (`worker/stories.ts:505`) always returns
  `{ "code": "<slug>" }` (`stories-unavailable`, `not-found`, `story-unavailable`, `payment-required`, `purchase-not-valid`,
  `cannot-verify`, `rate-limited`). No shop route returns 400. A missing store secret gives **403** `cannot-verify`.
- Old installs cannot get an app fix. The only lever is `minVersion` in the remote config (Slice 132:
  docs/plan/slices/132-dino-remote-config-and-force-update.md), which shows the "Please update" page.

## Decisions (user, 2026-11-13, RefineScope)

1. **402 "payment required"** shows its own translated message, "You need to buy this book first", with a Buy button on
   that row where the buy flow can start from there.
2. **Old app versions:** when paid books go live, `minVersion` is raised to the first Dino version that can buy
   (`dino-v1.124.0`, the first tag with PR #1016), so old apps show "Please update" instead of a shop that fails silently.
   This is a live config change: it is made only when the user switches paid books on for everyone, and the slice records it.
3. **Server answers:** a non-400 error has **no body**, only the status code; the app supplies the text. Only a **400**
   carries a message, an English sentence the app shows as it is (`{"message": "..."}`). Two codes change:
   `cannot-verify` (server missing a key) becomes **503**; `purchase-not-valid` stays **403**; `payment-required` stays
   **402**; `rate-limited` stays **429**. The short code slugs go.
4. **Where messages show:** only on actions the user starts: the shop list, Get/Buy, and voice pack downloads in Dino and
   in Read The Stupid Text (Android, iOS and Windows). Background work stays silent: remote config, usage counts, crash
   reports, book updates, shelf audio packs.

Defaults the user accepted (not argued):
- Standard translated messages for **401** (sign-in needed), **403** (not allowed), **404** (not found), **429** (too many
  tries, wait a minute) and **every 5xx**: "Problem connecting to the server, please retry later" (**the mock supersedes
  this text**: a 5xx means the server was reached, so it is `Error_Server`, "Something went wrong on the server. Please try
  again later."; 404 is `Error_BookGone` "This book isn't in the Store any more." in the Store and `Error_VoiceGone` "This
  voice isn't available any more." on a pack row; 429 is `Error_TooManyTries`; 402 is `Error_PaymentRequired`; keys and wording
  in `docs/plan/notes/2026-11-13-dino-copy.md`). No answer at all
  (no network, timeout) keeps the existing "You're offline" text.
- Any other 4xx without its own text shows the 5xx-style general text (`Error_Server`).
- The message shows inline under the book's row (and on the pack row), not a pop-up.

## Work

### A. Shop, end to end (smallest first)

- [ ] `HttpErrorKind` (closed enum in Application: `Offline`, `BadRequest`, `Unauthorized`, `Forbidden`, `NotFound`,
  `PaymentRequired`, `TooManyRequests`, `ServerError`, `OtherClientError`) and `HttpErrorClassifier.Classify(status?)`
  returning it, plus the 400 message read from the body (`{"message"}`, trimmed, length-capped). Data only, no UI copy.
- [ ] Unit tests: every status maps as above; a 400 with no or bad body gives `BadRequest` with no message, which shows the
  general text; a non-400 body is ignored.
- [ ] `StoreClient`: list, cover, download and purchase calls return the classified error instead of a bare failure. Keep
  the existing fallback events (`FallbackGuardTests` baseline must not go up).
- [ ] `StorePage`: a failed row shows the message for its `HttpErrorKind` in the strip inside the row's card (Design S2) with
  "Try again" (`Store_TryAgain`) at the end of the row; `PaymentRequired` shows `Error_PaymentRequired` and keeps the price
  button. The strip goes away when the next try starts. A 400 shows the server's sentence as it comes, in the same strip. The
  list failing shows the same text in place of the offline panel when the error isn't `Offline`.
- [ ] **S1, prices and tags** (Design): a paid book's button is the store's own price with a small cart glyph (`£0.99` in
  the mock, local currency; tapping it opens the store's buying sheet); a free book shows the green tag `Store_Free` "Free"
  after the length (`Store_Length`) and the brand button `Store_Get`; while a book comes in the button is the ring and percent
  (`40%`); an owned book shows `Store_OnShelf`. `Store_Free` is new in all 15 languages.
- [ ] **S3, "Bought books before?" card** at the foot of the Store list (Design; decided: Restore and Redeem live there, not
  in Settings): replaces `PurchaseActions` (`StorePage.xaml:114-118`). Title `Store_BoughtBeforeTitle`, account line
  `Store_BoughtBefore_Google` / `_Apple` / `_Microsoft` (tablet and computer variants too), buttons `Store_RestorePurchases` and
  `Store_RedeemCode`; the result under the card: `Store_Restored_One` / `Store_Restored_Other` ("{n} books restored. They're on the
  shelf.") in a green line, or `Store_NothingToRestore` in the sand strip. Still shown only while books are sold (remote switch).
- [ ] **Restore puts the books on the shelf.** Today `RestorePurchasesAsync` (`StorePage.xaml.cs:680`) only says by alert
  (`Store_Restored`) that they can be got from the Store. The mock says "They're on the shelf": after a restore, download every
  restored book that is not on the shelf (the normal store download, one at a time, progress on its row), count the ones that
  arrived, and show the result line. `Store_Restored` (alert) goes; the two new keys replace it. Unit test the count and plural.
- [ ] Strings in `AppResources.resx` and every `AppResources.<lang>.resx` (all 15 app languages; `NoHardCodedUiTextTests`).
  Language rule: text comes from `AppResources.Culture` (`src/DinoReader.App/CLAUDE.md`).
- [ ] **Android** and **iOS**: the shared `StorePage` covers both; the purchase paths `AndroidPurchases.cs` and
  `IosPurchases.cs` pass Worker refusals through as `HttpErrorKind`. **Windows**: `WindowsPurchases.cs` the same.

### B. Worker

- [ ] `worker/stories.ts`: `errorResponse` sends an empty body for every non-400 status; `cannot-verify` becomes 503.
  Add a `badRequest(message)` helper for 400 with `{"message": "<English sentence>"}`; use it only where a request is
  malformed and the user can fix it (none today; add it where the shop validates input).
- [ ] `worker/stories.test.mjs`: assert no body on 402/403/404/429/503 and the 503 for a missing store secret.
- [ ] `worker/CLAUDE.md`: rule "only a 400 carries a body (`{message}`, English); everything else is status only".

### C. Voice packs (both apps)

- [ ] `PackErrorClassifier` uses `HttpErrorClassifier` for HTTP failures; Dino's audio-pack rows that the user starts and
  RTST's pack download rows show the same per-status texts. **Android/iOS:** `MobileVoicePackService`; **Windows:**
  `WindowsVoicePackService` and the RTST `.resw` strings in every language.
- [ ] **RT1 and RT2 rows** (Design): the failed row shows the strip under it with `Error_Server` / `Error_VoiceGone`, `Wait_Retry`
  / `LanguageRetry.Content` "Retry" at the row's end, and the row's second line `Pack_RowSize` "Voice pack · {0} MB" (phone) or
  `Pack_NotDownloaded` "not downloaded" (Windows panel). New RTST strings: `Error_Server`, `Error_VoiceGone`, `Pack_RowSize`,
  `Pack_NotDownloaded`, in the Mobile `.resx` and App `.resw` languages. 429 and 402 never happen for packs.

### D. Old versions (live, at switch-on)

- [ ] When paid books go live for everyone, set `minVersion` for Dino to `1.124.0` in the remote config through its
  normal route (Slice 132), and record the date here. Not before: with the switch off, old apps still get free books.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` and `tests/DinoReader.Tests` (classifier,
  strings in every language, fallback baseline).
- `node --test worker/stories.test.mjs`.
- Emulator (Android) with a Debug hook or the local Worker: a 402, 404, 429 and 503 on Get each show their text under the row.
- iOS: compile on Windows (`-p:IncludeIos=true`), then a TestFlight check of one refused Get. Windows head on the VM: one refused Get.
- RTST Windows and Android: a pack download against a 404 shows `Error_VoiceGone` ("This voice isn't available any more.").
- Emulator: Restore with two bought books shows "2 books restored. They're on the shelf." and both are on the shelf; with none, the
  sand "No bought books found on this account." strip.

#### Design (Dino Reader 2026-11-13 Missing States mock, section 2, added 2026-11-13)

Five frames: S1, S2, S3 (Dino Store, phone 405 x 864) and RT1, RT2 (Read The Stupid Text rows). The mock agrees with the plan
on inline strips and per-status messages, and changes the 5xx and 404 wording. The RTST frames are small cut-outs of the real
panels (RT1 phone card 360 wide, RT2 Windows panel 376 wide), not whole screens; the Windows frame chrome, bezel and status bar are
canvas furniture. Text keys: `docs/plan/notes/2026-11-13-dino-copy.md` (*Missing States*). Hex as in the mock (use the `DinoTokens` key with
the same value, else add one). Fredoka for Dino; RTST uses its own look (system fonts, Batch 19 R3).

**Dino Store (all three frames share it):** ink header `#2b2540` (eyebrow "Grown-ups' corner" 11.5 / 600, letter-spacing .8, `#FFC24D`;
title "Store" 22 / 600; coral Child's shelf pill 44 high, radius 22, shadow 0 3 0 `#E2583A`); line under it `Store_Subtitle` 13.5 `#5a5470`.
Page fill `#F3EFE6`, list padding 0 14, card gap 10. Tab bar white, 1 px `#e8e1d3` rule (the Store tab lit `#5B57E8` with a
`#ECE7FF` 52 x 28 pill).
- **Book card:** white, radius 18, padding 12, gap 10. Row: cover 60 x 80, radius 10; text column gap 2: title 15.5 / 600, line 1.25;
  author 12.5 `#5a5470` (existing line); `Store_Length` 12.5 `#5a5470`. The action sits at the end of the row.
- **S1:** (a) free and on the shelf: green tag `Store_Free` after the length (height 20, padding 0 8, radius 10, `#DFF5E3`, `#2E7D32`,
  11.5 / 700) and, at the end, `Store_OnShelf` 14 / 600 `#2E7D32` with a check; (b) paid: brand button 40 high, radius 20, `#5B57E8`, white
  14.5 / 600, padding 0 16, the price ("£0.99" in the mock: the store's own price in local currency, filled from real data) with a small cart
  glyph; (c) in progress: the ring and percent ("40%", 14 / 600 `#3f3bc4`); (d) free, not yet on the shelf: the Free tag and brand `Store_Get`.
- **S2, strip:** inside the card under the row: gap 8, padding 9 12, radius 12, fill `#FFF0EA`, text `#A4462A` 13.5 / 500, line 1.4, an alert glyph
  at the start. Per status: 402 `Error_PaymentRequired` with the price button still there; 5xx and any other 4xx `Error_Server` and
  "Try again" (`Store_TryAgain`: 40 high, radius 20, fill `#ECE7FF`, text `#3f3bc4`, 14.5 / 600, padding 0 14) at the end of the row; 429
  `Error_TooManyTries` with Try again; Slice 358's `Store_NotForSaleNow` with **no button**. A 400 shows the server's sentence as it comes,
  same strip. The strip goes away when the next try starts. Shown only after something the grown-up tapped.
- **S3, calm strips:** store unreachable: sand strip, same size as the red one but fill `#F0E8D8`, text `#4a4460`, a cloud glyph,
  `Store_ConnectToBuy` (Slice 358 builds it); no button. 404: red strip `Error_BookGone`, no button.
- **S3, "Bought books before?" card** at the end of the list: white, radius 18, padding 16, gap 12. Title `Store_BoughtBeforeTitle` 16 / 600; account line
  13.5 `#5a5470`, line 1.45: `Store_BoughtBefore_Google` (Android; `_Apple` on iOS, `_Microsoft` on Windows, with the tablet and
  computer variants); two buttons, gap 8, 44 high, radius 22, padding 0 14, 15 / 600: `Store_RestorePurchases` fill `#ECE7FF` text `#3f3bc4`;
  `Store_RedeemCode` transparent, 2 px `#d9d2c4` border, text `#3a3450`. Result line under the card: gap 8, padding 10 12, radius 14, fill
  `#DFF5E3`, text `#1F5F23`, 13.5 / 600: `Store_Restored_One` / `Store_Restored_Other`; nothing found: `Store_NothingToRestore` in the sand strip.
  The `Store_Footer` lock line is not in the mock's frames (they stop at this card): it stays as built, after the card.

**RT1, RTST phone, Settings > Voices:** card 360 wide on `#f4f5f9`, radius 20, padding 14, system font, text `rgba(0,0,0,.87)`; heading
"Voices" 12 / 700, letter-spacing 1, `rgba(0,0,0,.55)`. List: white, 1 px `rgba(0,0,0,.11)`, radius 16. Row min height 64, padding 0 16, gap 12:
language name 16 / 600; second line `Pack_RowSize` 13 `rgba(0,0,0,.6)`; at the end `Wait_Retry` 14 / 600 `#4A46D6` (while another downloads: its percent
"40%" 14 / 600 `rgba(0,0,0,.6)`). Strip under the failed row: margin 0 12 12, padding 9 12, radius 12, fill `#FDECEC`, text `#9B2C2C`, 13.5, line 1.4:
`Error_Server` (404: `Error_VoiceGone`).

**RT2, RTST Windows control panel (Option C), voice row:** panel 376 wide, radius 8, `rgba(251,251,253,.96)`, 1 px `rgba(0,0,0,.1)`, shadow 0 18 50
rgba(20,30,60,.22), Segoe UI, padding 8. Row padding 10, gap 12: icon tile 32 x 32, radius 7, glyph `#2A57C0`; "Voice" 13, the second line 11.5
`rgba(0,0,0,.6)` ("Thorsten · Deutsch · " then `Pack_NotDownloaded`); Fluent accent button `LanguageRetry.Content` "Retry" 28 high, radius 4,
`#3B6FE3`, white 12.5 / 600, padding 0 12. Error line margin 0 10 8 54 (indented to the row's text), gap 6, 12 / 1.4, `#A1262C`, the system error glyph, then
`Error_Server`. The row's second line says "not downloaded" while the message shows.

New strings, all 15 `AppResources.<lang>.resx` (Dino) and the RTST `.resx`/`.resw` languages: `Error_PaymentRequired`, `Error_Server`, `Error_TooManyTries`,
`Error_BookGone`, `Store_Free`, `Store_BoughtBeforeTitle`, `Store_BoughtBefore_Google`/`_Apple`/`_Microsoft` (+ tablet/computer variants),
`Store_Restored_One`/`_Other` (replace `Store_Restored`), `Store_NothingToRestore`; RTST: `Error_Server`, `Error_VoiceGone`, `Pack_RowSize`, `Pack_NotDownloaded`.
(`Store_NotForSaleNow` is Slice 358's.)

**Plan changed to match the mock (2026-11-13):**
- 5xx text "Problem connecting to the server, please retry later" (accepted default) is superseded by `Error_Server` "Something went wrong on the server. Please try again later.", the same in both apps; the mock's note: a 5xx means the server was reached.
- 404 gets its own texts (`Error_BookGone`, `Error_VoiceGone`) instead of the general "not found" default; 429 `Error_TooManyTries`; 402 `Error_PaymentRequired` ("You need to buy this book first", the user's wording; Decision 1's Buy button is the row's price button, which stays, as the mock draws it).
- Message strip styling and "the strip goes away when the next try starts" added to the `StorePage` item.
- Work items added for S1's Free tag and price button (also built here, not in Slice 346) and for the S3 "Bought books before?" card at the foot of the Store list, with the result lines and the account line per store.
- **Restore now downloads the restored books** to the shelf (the mock says "They're on the shelf"); today it only alerts that they can be got from the Store (`StorePage.xaml.cs:680`). `Store_Restored` is replaced by `Store_Restored_One`/`_Other`.
- Restore and Redeem move from `PurchaseActions` (two plain buttons under the footer) into the card at the foot of the list (decided).
- RT1/RT2 work item added with the new RTST strings. Sibling Slice 346 (paid books, built): it would need `Store_Free`, the card and the restore-downloads change if they were not built here; they are built here, so no edit to 346. No `CLAUDE.md` rule clashes (the new strings are per-language; the Worker still sends no sentence except on 400).

## Not yet verified

_(filled when built)_
