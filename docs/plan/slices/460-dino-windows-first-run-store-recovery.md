# Slice 460 — Dino Windows: the store works at first run, or recovers on its own
**Product:** Dino

*Planned 2026-11-27 from the 2026-11-26 usage analysis (Ninjalog `ashkan-sirous/dino-book-reader`, index `dino_usage`, 19-26 Nov).*

**Starts after:** [Slice 453](453-dino-first-books-cancelled-load-copy.md) (both edit `FirstBooksPage.cs`).

**Touches:** `src/Dino/DinoReader.App/Platforms/Windows/Purchases/WindowsPurchases.cs`, `src/Dino/DinoReader.App/Purchases/BookPurchases.cs`, `src/Dino/DinoReader.App/Purchases/PurchaseTrace.cs`, `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Shared/Sirous.Reading.Application/Usage/FallbackKind.cs`, `worker/events.ts`, `worker/events.test.mjs`, `tests/Dino/DinoReader.Tests/**`, `docs/analytics/ninjalog-queries.md`, `src/Dino/DinoReader.App/Platforms/Windows/App.xaml.cs`, `src/Shared/Sirous.Reading.Application/Usage/**`, `tools/desktop-vm/checks/**`, `tests/Shared/Shared.Tests/**` (last four widened 2026-11-27: the window wait, a new cause enum, item 1's VM check, the fallback list-sync test), `src/Dino/DinoReader.App/FirstBookPrefetch.cs` (widened 2026-11-27, review 1 #2), `src/Dino/DinoReader.App/Purchases/IBookPurchases.cs` (widened 2026-11-27, review 1 #1/#6: the snapshot carries the failure cause per read)

*Built 2026-11-27 (items 2, 3 and 4), merged as PR #1428 (06339e27). Item 1 is open, and the Windows, iOS and S21 checks are in *Not yet verified*.*

**Why:**
- All 3 real Windows first launches that left during onboarding left on `first_run`. Two of them had fallback `store:store_unreachable` or `store:store_failed` (Russia 1.136.5.0, Oman 1.136.3.0). The Oman user retried in a new session and finished.
- Across all platforms, `store_unreachable` appeared in 12 first-launch sessions, mostly at `first_run`.
- What the code does today (read 2026-11-27 on origin/main):
  - `StoreClient.ReportFallback` (`StoreClient.cs:919-937`) maps timeouts, cancels and socket errors to `store_unreachable`, and anything else to `store_failed`.
  - The list call has a 20 s timeout (`:171`) and no automatic retry. A failed list shows the picker's offline page, and only a manual Try again recovers (`FirstBooksPage.cs:215-236`).
  - On Windows, `WindowsPurchases.Context()` (`:76-98`) returns null when the app window doesn't exist yet or the Store call throws, for example 0x803F6107 "not associated with the Store". `RefreshAsync` then reports `store_unreachable` (`:34-38`). That this is the first-run cause is plausible from the code but unproven.
  - `StoreClient.cs:236` also reports `store_unreachable` when the purchase-settle budget runs out, so a slow but working store can show up as unreachable.
  - Analytics keeps only the area and reason, never the error. The error and HRESULT stay in the local `AppTrace` log (`PurchaseTrace.Line`).

**Decisions (owner, 2026-11-27: "2-agree"):**
- The app retries a failed store call on its own a few times, with longer gaps each time, before it shows the offline page.
- On Windows it waits for its window before asking the Store.
- Analytics gets a closed list of failure causes in the fallback detail: timeout, no network, DNS, TLS, HTTP 5xx, HTTP 4xx, not associated with the Store, no window yet, settle budget ran out, other. It never carries raw error text or an HRESULT string beyond that list.

**Open for the owner:** none.

**Work:**
- [ ] 1. Find the cause first. Read the `AppTrace` log on a fresh Windows install in the desktop VM (`tools/desktop-vm`) at first run, and on a release build installed from the Store. Write down which call fails, its error, and whether the window/Store-context ordering is the trigger. Write the finding into this file before item 2.
  - *Item 1 not run (2026-11-27):* the desktop VM needs 8 GB free and only 6.8 GB was free (probe note `reviews/s460/probe-firstrun.md`); it stays open as a `[session]` line in *Not yet verified* (the wave later had 4.5 and 7.9 GB free, still under 8). Items 2-4 are built to hold either cause ("no window yet" and "not associated with the Store" are both in the list).
- [x] 2. Add the closed cause list (`FallbackKind` or a sibling enum), map it in `ReportFallback` and `WindowsPurchases`, and add a Worker rule and tests that accept only the listed causes. Split the settle-budget timeout out of `store_unreachable`.
- [x] 3. Make first run recover on its own:
  - Retry the list and the Store context with backoff, for example 1 s, 3 s and 7 s, behind the existing wait screen with its Cancel.
  - On Windows, don't create the Store context before the window handler exists. Retry it once the window is up.
  - Show the offline page only after the retries fail. It keeps Try again.
- [x] 4. Android and iOS get the same retry in the shared `StoreClient` path. Only the Store-context part is Windows-only, because only Windows has that API.

**Verification:**
- Unit tests cover the cause mapping and the retry schedule, including Cancel during a retry.
- Device runs: a Windows VM fresh install shows first run finishing with the network cut for 5 s and then restored, with no manual Try again. Android and iOS run the same check.
- Ninjalog, after release: the "Store failures at first run" query in `docs/analytics/ninjalog-queries.md` (every `store:` detail, split by version; old builds still send `store_unreachable`), compared with the 19-26 Nov baseline of 12 sessions. Windows first-launch sessions that end on `first_run` should drop, and the new cause detail should show what is left.

**Built notes (2026-11-27, items 2-4):** `StoreCause`/`StoreCauses` and `StoreRetry` (Application/Usage), nine new `store_*` reasons in `FallbackReason` and `FALLBACK_REASONS` (`store_unreachable`/`store_failed` stay; `store_failed` is "other"), `StoreClient.ListWithRetryAsync` used by the first-books picker behind its wait and Cancel, `WindowsPurchases` looks for the window before `StoreContext.GetDefault()` and retries 1/3/7 s (a "not associated" answer is not retried again in the process), `BookPurchases` reports the platform's `LastFailure`. Not retried yet: `FirstBookPrefetch.cs` (outside Touches; its failed list still reports one fallback before the picker's retry). context7 quota was spent, so no doc query ran; only the BCL types `HttpRequestError`, `SocketException.SocketErrorCode` and the already-used Store calls were touched, and the compiler checked the types.

**Decided by the wave 2026-11-27 coordinator (second strike), replacing the prefetch's held failure:** `DeferFailureReport`, the held failure and the 60 s timer are gone. `FirstBookPrefetch` runs the same `StoreClient.ListWithRetryAsync` sequence as the picker (behind no UI).
`StoreClient` owns one session-scoped `FirstRunFailureOnce` flag (thread-safe, Application/Usage): a first-run list sequence reports its final failure only if the flag is not set yet, then sets it; a success or a Cancel reports nothing.
So a failed prefetch plus a failed picker is one row, a failed prefetch plus a picker that succeeds is one row (the prefetch's real failure after its retries), and concurrent sequences give one. Tests: `StoreRecoveryTests`.

**Emulator run 1 findings (2026-11-27, `reviews/s460-device/`):** (1) Debug marker per attempt `[Store] list attempt=<n> source=prefetch|picker cause=<cause|ok|none> nextWait=<s>` plus `[Store] list failure source=<s> outcome=reported|suppressed|silent` (`StoreRetry.AttemptLine/OutcomeLine/NextWait`, logged in `ListWithRetryAsync`). (2) The offline log's late row was not the prefetch: the `test-store offline` fixture returned Offline with no exception, so no row and no 1/3/7 s retry (prefetch "ended Failed" 31 ms after start), and the row after `test-store ok ok` + Try again came from the purchase read below. The fixture now fails like a dropped connection (ConnectionError), so the sequence retries and reports once as on a real offline device; and only the picker's sequence reports now (`FirstRunListSource`): the prefetch is silent, so a prefetch failure the picker then recovers from leaves no row, and `FirstRunFailureOnce` keeps Try again from adding a second. (3) Every other `store_failed` count in those three logs comes from `BookPurchases.ReportReadProblems` (the purchase read after a list, line "subscription kept (store not asked)" right after each row): the emulator has no Play Billing, so the platform read is not Reachable while the device has internet, which reports Other per read (threads 4971/4981, 5795/5799; a second read at 14:35:15.458 after the picker's Continue). `test-store ok slow` is a fixture list that returns Ok, then `ReadPurchasesAsync` asks that same unreachable platform, hence store_failed (Other); it is the purchase read's own failure, not the list's.

**Built notes (check 4 #1, reverses review 2 medium #1):** a `HttpRequestException` with `ConnectionError`, `NameResolutionError` or `SecureConnectionError` is no network, DNS or TLS; an HTTP status is 4xx/5xx; a transport failure with no status and no named error (`HttpRequestError.Unknown`, a bare `IOException`) is cause Other (store_failed) but still retried, and a JSON or bad-body failure is Other and never retried.
The rule lives in `StoreCauses.Of` and `StoreCauses.IsRetryable` (the old `UnreachableCause` mapping of Other to NoNetwork is gone); `StoreRecoveryTests` tests each row.

**Android emulator run 2 (2026-11-27, `reviews/s460-device/run2/`):** first-run, first-books-picker and first-books-offline passed. The list attempts kept gaps of 1.0, 3.0 and 7.0 s, and one `outcome=reported` row came per failed session, with none after recovery. The leftover `store_failed` rows have no `[Store] list` failure line; run 1 tied them to `BookPurchases.ReportReadProblems` (the emulator has no Play Billing). That is likely, not proven (the S21 check is in *Not yet verified*).

#### From review

Review files (`reviews/s460/`): `pr-1428-high.md` (8 findings, all fixed), `pr-1428-medium.md` (5, all fixed), `pr-1428-check.md` (7 findings: all fixed except #5, set aside below), `pr-1428-check2.md` (7: all fixed, three by redesign, except #7, set aside below), `pr-1428-check3.md` (no Outcome block; 5 nits, see below), `pr-1428-check4.md` (two fixed, three follow-ups below), `pr-1428-check5.md` (one in *Not yet verified*, the rest follow-ups below).
- `pr-1428-check3.md` (5 nits; the coordinator wrote its Outcome block 2026-11-27): #1 (flag spent by a prefetch failure) was overtaken by the second-strike redesign above (only the picker reports); #2 is the COM thread line in *Not yet verified*; #5 (an offline device waits about 20-30 s) is answered by emulator run 2, where the offline page showed 11 s after the first attempt.
- [ ] Follow-up (`reviews/s460/pr-1428-check3.md` #3): "with no window, every non-press read waits 6 s again and reports store_no_window".
- [ ] Follow-up (`reviews/s460/pr-1428-check3.md` #4): "a persistent 429 reports as http_4xx and ignores Retry-After".

- [ ] Follow-up (check passes converged, `reviews/s460/pr-1428-check4.md`) #2: guard `DebugLog` with `DINO_TEST_BOOKS` too, so a Release VM package with the test store writes its `[Store]` lines.
- [ ] Follow-up (check passes converged, `reviews/s460/pr-1428-check4.md`) #4: the session flag is spent by the first picker failure, so a different cause after Try again is suppressed; key it on cause or state one row per session.
- [ ] Follow-up (check passes converged, `reviews/s460/pr-1428-check4.md`) #5: on an offline first launch prefetch and picker each run a full 4-attempt sequence in parallel; share the in-flight list or give the prefetch one attempt.
- [ ] Follow-up (check passes converged after pass 5, `reviews/s460/pr-1428-check5.md`) #1: "`Java.IO.IOException` maps to NoNetwork and Of walks base types, so `FileNotFoundException` (HttpURLConnection on HTTP 404/410) becomes `store_no_network` and is retried 3 times (about 11 s) on a working network." Drop the broad entry or exclude FileNotFoundException (`src/Shared/Sirous.Reading.Application/Usage/NativeNetworkErrors.cs:25`).
- [ ] Follow-up (check passes converged after pass 5, `reviews/s460/pr-1428-check5.md`) #2: "Outer `HttpRequestError.ConnectionError` is mapped before the inner exception, so Android UnknownHostException, SSLException and iOS timeouts (-1001) report `store_no_network` instead of dns/tls/timeout." Check the inner exception first for ConnectionError (`src/Shared/Sirous.Reading.Application/Usage/StoreCause.cs:59`).
- [ ] Follow-up (check passes converged after pass 5, `reviews/s460/pr-1428-check5.md`) #4: "Online but failed Play or StoreKit reads now report `store_failed` instead of `store_unreachable`; the comment at 529 says this is deliberate, but the stale comment at AndroidPurchases.cs:145 still says `store_unreachable` follows." Fix the comment and record the reclassification in this slice (`src/Dino/DinoReader.App/Purchases/BookPurchases.cs:532`).
- [ ] Follow-up (check passes converged after pass 5, `reviews/s460/pr-1428-check5.md`) #5: "Tls is retryable; a wrong clock or untrusted root fails every time, so the wait screen holds about 11 s for nothing." Retry Tls at most once (`src/Shared/Sirous.Reading.Application/Usage/StoreCause.cs:99`).
- [ ] Follow-up (check passes converged after pass 5, `reviews/s460/pr-1428-check5.md`) #6: "IsUnreachable accepts any OperationCanceledException, so a cancelled disk write (not the caller's token) reports `store_timeout`." Report `store_failed` for StoreStorageException and StoryPackageException before mapping (`src/Dino/DinoReader.App/StoreClient.cs:1084`).

- Skipped #5 (`reviews/s460/pr-1428-check.md`): "UnreachableCause calls StoreCauses.Of(ex) twice (once in IsTransientFailure, once in ReportFallback); native lookup (reflection, regex on iOS) runs repeatedly." - it runs only on a failure path, once per failed attempt, so computing it twice costs nothing measurable.
- Skipped #7 (`reviews/s460/pr-1428-check2.md`): "A COM or InvalidOperation failure other than not-associated returns failure null, reported as store_failed and not retried." - the launch COM HRESULT is now logged to AppTrace; which COM errors are transient waits on item 1's VM AppTrace read.

#### Not yet verified

- [session] Item 1, the Windows VM `AppTrace` read at first run: not run, the VM needs 8 GB free and this wave had 6.8, 4.5 and 7.9 GB. Compare the cached origin/main package `8bbbca166301` with the new one `9fc20fa691d1`, and write down which call fails, its error and whether the window or Store-context ordering is the trigger.
- [session] Windows VM fresh-install first run with the network cut for 5 s and restored: first run finishes with no manual Try again.
- [cloud] iOS simulator run of the same first-run and offline checks in `dinoreader-ios-sim.yml`.
- [phone] On the S21 a first run shows no `store_failed` row from the purchase read (`BookPurchases.ReportReadProblems`); the emulator's rows came from having no Play Billing, which is likely but not proven.
- [session] Windows VM run: `OpenContext` is now reached from a `Task.Run` pool thread (`WaitForWindowAsync` -> `ContextWithRetryAsync`) and touches `Application.Current.Windows`, `Handler.PlatformView`, `InitializeWithWindow` and writes `_context` unlocked; a wrong-thread COMException would be caught as Failure=null, end the poll early and report `store_failed` with no recovery (`src/Dino/DinoReader.App/Platforms/Windows/Purchases/WindowsPurchases.cs:149`; `reviews/s460/pr-1428-check5.md` #3, unconfirmed, not run on a device).
