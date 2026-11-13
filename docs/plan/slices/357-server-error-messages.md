# Slice 357 — Server errors the user can see: standard messages per status, the server's text only on 400

*In progress (wave 2026-11-13), draft PR.*

**Touches:** `src/ReadTheStupidText.Application/Net/HttpErrorKind.cs` (new), `src/ReadTheStupidText.Application/Net/HttpErrorClassifier.cs` (new), `src/ReadTheStupidText.Application/Reading/PackErrorClassifier.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/Purchases/BookPurchases.cs`, `src/DinoReader.App/Platforms/Android/Purchases/AndroidPurchases.cs`, `src/DinoReader.App/Platforms/iOS/Purchases/IosPurchases.cs`, `src/DinoReader.App/Platforms/Windows/Purchases/WindowsPurchases.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/ReadTheStupidText.Infrastructure/Reading/WindowsVoicePackService.cs`, `src/ReadTheStupidText.App/Strings/*/Resources.resw`, `worker/stories.ts`, `worker/stories.test.mjs`, `tests/ReadTheStupidText.Tests/Net/*` (new), `tests/DinoReader.Tests/*`, `src/DinoReader.App/CLAUDE.md`, `worker/CLAUDE.md`

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
  tries, wait a minute) and **every 5xx**: "Problem connecting to the server, please retry later". No answer at all
  (no network, timeout) keeps the existing "You're offline" text.
- Any other 4xx without its own text shows the 5xx-style general text.
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
- [ ] `StorePage`: a failed row shows the message for its `HttpErrorKind` under the row with "Try again"; `PaymentRequired`
  shows "You need to buy this book first" and the Buy button when the row can buy. The list failing shows the same text
  in place of the offline panel when the error isn't `Offline`.
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

### D. Old versions (live, at switch-on)

- [ ] When paid books go live for everyone, set `minVersion` for Dino to `1.124.0` in the remote config through its
  normal route (Slice 132), and record the date here. Not before: with the switch off, old apps still get free books.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` and `tests/DinoReader.Tests` (classifier,
  strings in every language, fallback baseline).
- `node --test worker/stories.test.mjs`.
- Emulator (Android) with a Debug hook or the local Worker: a 402, 404, 429 and 503 on Get each show their text under the row.
- iOS: compile on Windows (`-p:IncludeIos=true`), then a TestFlight check of one refused Get. Windows head on the VM: one refused Get.
- RTST Windows and Android: a pack download against a 404 shows "not found".

## Not yet verified

_(filled when built)_
