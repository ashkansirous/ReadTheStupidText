# Slice 453 — Dino first-books picker: a cancelled or failed list load must not say "Connect to the internet"
**Product:** Dino
**Touches:** `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Dino/DinoReader.App/FirstBooksPage.Views.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/Dino/DinoReader.Tests/FirstBooks*Tests.cs`, `src/Dino/DinoReader.App/StoreClient.cs` (review #1: only the first-books list call's classification of a server error or bad JSON), `src/Dino/DinoReader.App/FirstBooksLoadCopy.cs` (new: the pure choice of copy), `tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` (links it), `src/Dino/DinoReader.App/StoreListError.cs` (new: the list call's error kind)

*Built (wave 2026-11-27, night 2026-11-27-0154 wave 1), PR #1390, not merged; not yet verified on a phone (see below).* Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 299 item 2). Wave decision: the English copy is written by the builder in the style of the existing FirstBooks_Offline* strings, and the 14 translations are filled in the same resx files as every other FirstBooks string.

Moved out of Slice 299 item 2 by the plan cleanup of 2026-11-25. Read live on origin/main that day: `FirstBooksPage.LoadAsync` (`src/Dino/DinoReader.App/FirstBooksPage.cs:226-236`) answers a Cancel on the list wait, and any other load failure, with `ShowLoadFailed()`, which shows `_offlineLayer` (`:330-335`), whose copy is `FirstBooks_OfflineTitle` "Connect to the internet to choose your first books" / `FirstBooks_OfflineBody` (`AppResources.resx:115-116`). A grown-up who taps Cancel, or a server error with a working network, is told the phone is offline.

## Work

- [x] 1. Two new strings (cancelled, failed) with their own title and body, in all 15 app languages (`Resources/Strings/AppResources.*.resx`), shown by `ShowLoadCopy` (chosen by `FirstBooksLoadCopy`) for the cancelled and failed cases; the real offline case (`FirstBooksLoadCopy.ForOfflineListing` with no answer) keeps the current copy. Try again stays.
- [x] 2. Tests of the pure choice of copy (`FirstBooksLoadCopy`, `StoreListError`) and of the strings in every language: a cancelled list call and a throwing one show the new copy, not the offline copy. (Not a fake store client: `StoreClient` and the page are not linked into the test project.)
- [x] 3. Android and iOS the same (shared MAUI page).

Side effect, on purpose: a 200 answer whose body is not a listing (a captive portal's HTML) is classed `ServerError` for the live list call only (`StoreListError`, raised by `StoreClient.ParseListing`), so `StorePage.ShowOffline` also shows its server-error panel instead of "You're offline" for it; a JsonException from a local purchase or saved-listing read is not reclassified. The failed screen keeps Try again, and (after check pass 1 #1) it reloads by itself when the network returns, like the offline screen, so a captive portal's HTML recovers; the cancelled screen does not reload by itself.

*Changed while building:* one pure `FirstBooksLoadCopy` chooses the copy (offline / cancelled / failed), the badge and the log marker (`offline`, `cancelled-load`, `failed-load`; the old single `failed-load` marker is split); `StoreListError` maps a bad store-list body to `ServerError` for the list call only; four new strings in the 15 resx files; `FirstBooksPage` shows `ShowLoadCopy`; Cancel and Back on the list wait set the cancelled state. Tests (builder's run): Shared 1736 with 0 failures, RTST 630 (2 skipped), Dino 4902 (2 skipped); the Dino Android head compiles. No device run: no scripted flow exists for Cancel during the list load. iOS is the same shared page with no iOS-only code.

#### Not yet verified

- [phone] Emulator: open the first-run picker, Cancel during the list load: the cancelled copy shows with Try again, and the device log carries the `cancelled-load` marker (not `failed-load`).
- [phone] With the network back after a failed load (offline or a server error), the first-books list reloads by itself.
- [phone] The same two checks on the iPhone (shared page, no iOS-only code).

#### Follow-ups (from `reviews/pr-1390-check2-medium.md`, not fixed)

- Follow-up 1 (`FirstBooksLoadCopy.cs:24`): a local IOException or JsonException from the purchases or saved-listing read in `StoreClient.ListAsync` gives Offline with no error kind, so "Connect to the internet" shows on a working network. Origin/main behaviour (see skipped check1 #2).
- Follow-up 2 (`FirstBooksPage.cs:237`): an unrequested cancel (not Cancel or Back) shows the failed copy "on our side" and reloads silently on reconnect; the code comment says "nothing reloads by itself". Consider neutral copy and no auto-reload for that case.
- Follow-up 3 (`FirstBooksPage.cs:96`): `_stoppedByGrownUp` is set by any wait Cancel, download included; it is reset at the start of `LoadAsync` and `ListWithWaitAsync`, so the leak is practically closed.

#### From review

Outcome blocks read in `reviews/pr-1390-high.md`, `reviews/pr-1390-medium.md`, `reviews/pr-1390-check1-medium.md` and `reviews/pr-1390-check2-medium.md`: the high review fixed findings 1-5 and 7 (finding 6, the log-marker split, has no outcome line and is kept: the phone check reads `cancelled-load`); the medium review, round 2, fixed 2, 3, 4, 6 and 7; check pass 1 fixed 1 and 3; check pass 2 had no blocking findings (its three follow-ups are listed above).- Skipped #1 (`reviews/pr-1390-medium.md`): "Bad JSON now maps to ServerError, which also changes StorePage's panel." - StoreClient.cs is on the Touches line and a malformed listing showing the server-error panel instead of "offline" is this slice's aim; recorded above.
- Skipped #5 (`reviews/pr-1390-medium.md`): "Failed from an Offline-status listing no longer auto-reloads when the connection returns." - the failed screen keeps Try again (item 1) and a server error is not cured by the network coming back. Later applied by check pass 1 #1 (`reviews/pr-1390-check1-medium.md`): the failed copy now reloads when connectivity returns (a captive portal's HTML maps to failed); cancelled does not.
- Skipped #2 (`reviews/pr-1390-check1-medium.md`): "`IsNetworkFailure` still includes a bare `JsonException`, so a JsonException from local `ReadPurchasesAsync` or `SaveListingAsync` in the same try returns Offline with a null error kind and shows the offline copy and badge." - a corrupt local purchase or saved-listing file showing the offline copy is origin/main's behaviour, kept on purpose by fix pass 2 finding #2; this slice changes only the store list call.
