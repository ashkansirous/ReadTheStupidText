# Slice 357 — Server errors the user can see: standard messages per status, the server's text only on 400

*Parts A-C built 2026-11-13 to the plan as it stood before the mock, merged as PR #1052 (5778b02b). The mock's items (S1 tags and price button, S2 strip styling and wording keys `Error_*`, S3 Bought-books card and restore-to-shelf, RT1/RT2 rows) are not built: they move to Slice 364 (docs/plan/slices/364-server-errors-design-pass.md, planned). Part D stays open until paid books are switched on.*

**Touches:** `src/ReadTheStupidText.Application/Net/HttpErrorKind.cs` (new), `src/ReadTheStupidText.Application/Net/HttpErrorClassifier.cs` (new), `src/ReadTheStupidText.Application/Reading/PackErrorClassifier.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/Purchases/BookPurchases.cs`, `src/DinoReader.App/Platforms/Android/Purchases/AndroidPurchases.cs`, `src/DinoReader.App/Platforms/iOS/Purchases/IosPurchases.cs`, `src/DinoReader.App/Platforms/Windows/Purchases/WindowsPurchases.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/ReadTheStupidText.Infrastructure/Reading/WindowsVoicePackService.cs`, `src/ReadTheStupidText.App/Strings/*/Resources.resw`, `src/DinoReader.App/StoreRows*.cs` and `src/DinoReader.Core/StoreRow*.cs` (the row state and strip, if split out), `src/DinoReader.Core/BookAccess.cs` (price tag), the voice-pack row views in `src/ReadTheStupidText.Mobile/**` (Settings > Voices) and `src/ReadTheStupidText.App/**` (control-panel voice row; find them by `Wait_Retry` / `LanguageRetry`), `worker/stories.ts`, `worker/stories.test.mjs`, `tests/ReadTheStupidText.Tests/Net/*` (new), `tests/DinoReader.Tests/*`, `src/DinoReader.App/CLAUDE.md`, `worker/CLAUDE.md`, `worker/entitlements.test.mjs`, `src/DinoReader.Core/BookAccess.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Application/Reading/VoicePackSetup.cs`, `src/ReadTheStupidText.Application/Reading/IVoicePackService.cs`, `src/ReadTheStupidText.Mobile/LocalizedPackStatus.cs` and its three callers (`LanguagePage.xaml.cs`, `ReaderPage.xaml.cs`, `VoicePickerPage.xaml.cs`), `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources.cs`, `src/ReadTheStupidText.App/PackSetupText.cs`, `ControlPanelWindow.xaml.cs`, `LanguageWindow.xaml.cs`, `src/DinoReader.App/Testing/TestStore.cs`, `.maestro/dino/store-server-errors.yaml` (new), `.maestro/dino/store-payment-required.yaml` (new) (added by the coordinator 2026-11-13; not `worker/downloads.ts`, which Slice 355 owns)

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

5. **A 403 is always "purchase not valid" (decision, 2026-11-13, coordinator).** With the code slugs gone, the background book update treats any 403 on a sold book's files as the store's definitive refusal (the book is skipped for good), where an unreadable code used to stay retryable. Reason: the Worker is the only origin behind Cloudflare for these routes, so a 403 from a CDN or WAF page in front of it is not a case that exists here; the Worker's own 403 is the one meaning.
6. **Open item: the 502 slug.** `ServedFile.unavailable` (`worker/downloads.ts`, the 502 `story-unavailable` body for a listed file missing from the bucket) still carries a JSON code. `worker/downloads.ts` belongs to Slice 355 in this wave, so it is left as is: the apps ignore every non-400 body now. Remove the slug there once Slice 355 has merged.

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

- [x] `HttpErrorKind` (closed enum in Application: `Offline`, `BadRequest`, `Unauthorized`, `Forbidden`, `NotFound`,
  `PaymentRequired`, `TooManyRequests`, `ServerError`, `OtherClientError`) and `HttpErrorClassifier.Classify(status?)`
  returning it, plus the 400 message read from the body (`{"message"}`, trimmed, length-capped). Data only, no UI copy.
- [x] Unit tests: every status maps as above; a 400 with no or bad body gives `BadRequest` with no message, which shows the
  general text; a non-400 body is ignored.
- [x] `StoreClient`: list, cover, download and purchase calls return the classified error instead of a bare failure. Keep
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
- [x] **Android** and **iOS**: the shared `StorePage` covers both; the purchase paths `AndroidPurchases.cs` and
  `IosPurchases.cs` pass Worker refusals through as `HttpErrorKind`. **Windows**: `WindowsPurchases.cs` the same.

### B. Worker

- [x] `worker/stories.ts`: `errorResponse` sends an empty body for every non-400 status; `cannot-verify` becomes 503.
  Add a `badRequest(message)` helper for 400 with `{"message": "<English sentence>"}`; use it only where a request is
  malformed and the user can fix it (none today; add it where the shop validates input).
- [x] `worker/stories.test.mjs`: assert no body on 402/403/404/429/503 and the 503 for a missing store secret.
- [x] `worker/CLAUDE.md`: rule "only a 400 carries a body (`{message}`, English); everything else is status only".

### C. Voice packs (both apps)

- [x] `PackErrorClassifier` uses `HttpErrorClassifier` for HTTP failures; Dino's audio-pack rows that the user starts and
  RTST's pack download rows show the same per-status texts. **Android/iOS:** `MobileVoicePackService`; **Windows:**
  `WindowsVoicePackService` and the RTST `.resw` strings in every language.
- [ ] **RT1 and RT2 rows** (Design): the failed row shows the strip under it with `Error_Server` / `Error_VoiceGone`, `Wait_Retry`
  / `LanguageRetry.Content` "Retry" at the row's end, and the row's second line `Pack_RowSize` "Voice pack · {0} MB" (phone) or
  `Pack_NotDownloaded` "not downloaded" (Windows panel). New RTST strings: `Error_Server`, `Error_VoiceGone`, `Pack_RowSize`,
  `Pack_NotDownloaded`, in the Mobile `.resx` and App `.resw` languages. 429 and 402 never happen for packs.

### D. Old versions (live, at switch-on)

- [ ] When paid books go live for everyone, set `minVersion` for Dino to `1.124.0` in the remote config through its
  normal route (Slice 132), and record the date here. Not before: with the switch off, old apps still get free books.

## Built

*Changed while building:*
- Built: `HttpErrorKind`/`HttpErrorClassifier` (`src/ReadTheStupidText.Application/Net`); per-status text in the Dino shop (rows, plus a server-error panel distinct from the offline one); a 402 shows "Buy"/"Connect to buy" when the plan offers it; Worker non-400 answers are status only, a missing store key is 503, `badRequest(message)` exists but no route sends a 400 today; RTST pack failures show the status text; `StoreRefusal` was removed from `BookAccess`; 5xx update refusals still report `StoreUnreachable`.
- Row errors: the error is set together with the state in one call, `SetState(state, error)`.
- Pack errors were redesigned after three review rounds drew findings on the shared per-key map: the kind now travels in the failing call's result (`PackDownloadOutcome`, `PackCatalogResult.ServerAnswer`), with no shared lookup (see *From review*).
- A 402 re-plans the row to Buy, so the Maestro flows test 402 in its own flow (`.maestro/dino/store-payment-required.yaml`); the other statuses are in `.maestro/dino/store-server-errors.yaml`. Earlier failed device runs were flow-order bugs of this kind.
- Open item: `worker/downloads.ts` 502 still has a `story-unavailable` body slug (decision 6 above); apps ignore non-400 bodies.

*Tests:* RTST 2258, Dino 6995, Worker 349 plus typecheck. Builds with 0 errors: Dino and RTST Android, iOS compile, Dino Windows head, RTST Windows app.

*Device:* emulator-5554, 2026-11-13, build 4ce700f8109a: `store-payment-required` PASS, `store-server-errors` PASS (NotFound, TooManyRequests, ServerError, retry), no FATAL. Logs: `D:/ReadTheStupidText-worktrees/reviews/w52-device/*-run3.log`.

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

## From review

- Skipped #2 (`reviews/s357-high.md`): "An already-installed older app reads a 403 'purchase-not-valid' with an empty body" - not changed: Decision 3 says a non-400 answer carries no body. Known behaviour: apps released before this slice no longer read a 403 code, so their weekly book-update check retries that book (a background request, nothing shown to the person).
- Done differently from the finding #6 text (`reviews/s357-high.md`): the Worker has no 400 answer on any story route (a malformed id or `?v=` is a 404), so `badRequest(message)` stays exported and tested, and `worker/CLAUDE.md` says no route uses it yet.

- Skipped #2 (`reviews/s357-medium.md`): "`badRequest()` never called; the whole 400 {message} path is dead server-side and untested end to end." - repeat of high #6, already handled (see the "Done differently" line above).
- Skipped #6 (`reviews/s357-medium.md`): "bodies break installed Dino builds (403 empty body never definitive, 503 loses outage fallback reason)" - repeat of high #2, already recorded above as known behaviour.
- Skipped #4 (`reviews/s357-medium.md`): "Stall-timeout OperationCanceledException classified Offline and stored as a server answer" - not changed: this slice's defaults say "No answer at all (no network, timeout) keeps the existing \"You're offline\" text."
- Skipped #7 (`reviews/s357-medium.md`): "ListAsync failure with no HTTP status (TLS, malformed response) classified Offline" - not changed: same slice default, "No answer at all (no network, timeout) keeps the existing \"You're offline\" text."
- Skipped #8 (`reviews/s357-medium.md`): "Enum members out of status order, values unpinned." - not a bug: `HttpErrorKind` is never persisted or sent, only passed in memory from `HttpErrorClassifier` to `StoreText`/`PackSetupText`/`LocalizedPackStatus`, so its numeric values do not matter.
- Skipped #9 (`reviews/s357-medium.md`): "`_lastErrors`/NoteError copy-pasted into WindowsVoicePackService; share one small class in Application." - refactor beyond what a finding needs; left as is.
- Skipped #10 (`reviews/s357-medium.md`): "Fully qualified HttpErrorClassifier call in a Debug log line; add a using, log status only." - style only; left as is.
- Skipped #4 (`reviews/s357-check.md`): "A 400 body over the 2048-byte cap is truncated, so the JSON parse fails and MessageFrom returns null; the specific reason is lost with no log line." - not changed: the Worker's own 400 messages are one short English sentence, far under the 2048-byte cap (`RefusalBodyBytes` in `StoreClient.cs`), built by `badRequest` in `worker/stories.ts`; a body that long is not one of ours and falling back to the generic text is right.
- Redesign (`reviews/s357-check2.md`): the pack-error side channel (`DownloadErrorFor`, per-key `_lastErrors`, `CatalogErrorKey`) was replaced by construction. The kind now travels in the result of the call that failed: `PackCatalogResult.ServerAnswer` for the catalog fetch, and the new `PackDownloadOutcome(Status, ServerAnswer)` from `IVoicePackService.DownloadWithAnswerAsync` (the old `DownloadAsync` stays and returns its `Status`). Resolved by this redesign: #1 "CatalogErrorKey is one shared slot read after the fetch returns", #2 "DownloadErrorFor(pack) read after DownloadPackAsync returns", #5 "TryDownloadOnceAsync clears the stored answer each attempt" (the last attempt's outcome, answer included, is the result), #6 "NoteError runs before the network check in ClassifyFailure" (an Offline outcome carries no answer), #8 "Unavailable carries a null kind while other resolve failures read shared CatalogErrorKey" (both read the catalog result's `ServerAnswer`).
- Applied #3 (`reviews/s357-check2.md`): "StoreRefusedException.IsOutage can never be true now (only 400/402/403 throw it)" - `IsOutage` and its comments removed from `StoreClient.cs`; a 5xx is a plain `HttpRequestException` and reaches `store_unreachable` through the generic network catch (`ReportFallback`).
- Skipped #4 (`reviews/s357-check2.md`): "RecordDownloadError uses Classify on any exception; a stall-timeout OperationCanceledException is stored as Offline, so the row says "You're offline" while online." - not changed: same slice default as the skipped medium #4, no answer or a timeout shows Offline.
- Skipped #7 (`reviews/s357-check2.md`): "_lastErrors and NoteError are duplicated line for line in MobileVoicePackService; extract a shared PackErrorMemory." - removed by the redesign: neither exists any more.
- Skipped #1 (`reviews/s357-check3.md`): "deterministic 4xx (404 retired pack, 401/403, 429) are retried MaxAttempts times with backoff" - not changed: the retry policy pre-exists on origin/main (`WindowsVoicePackService.cs:198-201`); this slice only adds the server text.
- Skipped #2 (`reviews/s357-check3.md`): "a second RunDownloadAsync for the same id does TryRemove while the first's failure is about to be read" - not changed: the shop page starts at most one download per book, because a row shows no Get while it is Downloading (`IsGet`, `StorePage.xaml.cs:76`) and a Downloading row is not re-planned (`StorePage.xaml.cs:129`). Not checked: whether a background book update can download the same id at the same moment; that case is a Not yet verified line.
- Skipped #3 (`reviews/s357-check3.md`): "a torn read shows the previous run's server text for one refresh" - not changed: cosmetic, the next state change raises the event again and the text is corrected.
- Skipped #4 (`reviews/s357-check3.md`): "so an online device with a stalled/failing server is told" - not changed: the slice decides that no answer or a timeout shows Offline (same as the skipped medium #4).
- Skipped #5 (`reviews/s357-check3.md`): "Null the answer when Offline." - not changed: Offline is its own state and its kind is never read.
- Applied #3 (`reviews/s357-check4.md`): the four `status*` modes are listed in `src/DinoReader.App/Testing/CLAUDE.md`.
- Skipped #1 (`reviews/s357-check4.md`): "Scenario: later imports fail" - a measurement finding with no device evidence: run3 passed with this order (logs `reviews/w52-device/*-run3.log`); change it only when a run shows the flake.
- Skipped #2 (`reviews/s357-check4.md`): "tap does not scope to the gingerbread-man row" - same reason: run3 passed; no run showed the tap landing on another row.
- Skipped #4 (`reviews/s357-check4.md`): "a mid-flow failure leaves TestStore.Mode on a status mode" - the mode is a static in-process property (`TestStore.cs:59`), so the next app process starts with it off.
- Skipped #5 (`reviews/s357-check4.md`): "asserts only the text, not that the row returned to its plan" - the 402 re-plan is covered by the expect-log marker and the text; an extra assert waits for a run that needs it.
- Skipped #6 (`reviews/s357-check4.md`): "503 text assert may equal other kinds' general line" - the `[StoreError] row kind=ServerError` expect-log marker tells the kinds apart.
- Skipped #7 (`reviews/s357-check4.md`): "no wait confirming each mode switch before tapping Try again" - not seen failing in run3.
- No other review skip is recorded: the other findings in the four earlier review files were applied or redesigned.

## Not yet verified

- RTST pack-failure text on a device: no scripted hook for a pack server error.
- Dino shop list-failure panel text on a device: no list-error hook.
- Windows VM: a refused Get on the Dino Windows head.
- iOS TestFlight: one refused Get.
- Native read of the new strings in the 14 non-English languages.
- Dino shop: a failed Get while a background book update downloads the same book at the same moment (`StoreClient._downloadErrors` is keyed by book id; check3 #2).
- Part D: the `minVersion` change to `1.124.0` is open until paid books are switched on for everyone.

#### For CLAUDE.md (lifted 2026-11-13)

- A pack failure's server answer travels in the failing call's result (`PackDownloadOutcome`, `PackCatalogResult.ServerAnswer`), never in a shared per-key or per-service map: concurrent downloads and update checks overwrite it. (Slice 357: docs/plan/slices/357-server-error-messages.md)
- A 402 re-plans the shop row to Buy, so a Maestro flow that tests another status on the same row must not share a flow with the 402 case; keep 402 in `.maestro/dino/store-payment-required.yaml`. (Slice 357: docs/plan/slices/357-server-error-messages.md)
