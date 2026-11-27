# Slice 460 — Dino Windows: the store works at first run, or recovers on its own
**Product:** Dino

*Planned 2026-11-27 from the 2026-11-26 usage analysis (Ninjalog `ashkan-sirous/dino-book-reader`, index `dino_usage`, 19-26 Nov).*

**Starts after:** [Slice 453](453-dino-first-books-cancelled-load-copy.md) (both edit `FirstBooksPage.cs`).

**Touches:** `src/Dino/DinoReader.App/Platforms/Windows/Purchases/WindowsPurchases.cs`, `src/Dino/DinoReader.App/Purchases/BookPurchases.cs`, `src/Dino/DinoReader.App/Purchases/PurchaseTrace.cs`, `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Shared/Sirous.Reading.Application/Usage/FallbackKind.cs`, `worker/events.ts`, `worker/events.test.mjs`, `tests/Dino/DinoReader.Tests/**`, `docs/analytics/ninjalog-queries.md`, `src/Dino/DinoReader.App/Platforms/Windows/App.xaml.cs`, `src/Shared/Sirous.Reading.Application/Usage/**`, `tools/desktop-vm/checks/**`, `tests/Shared/Shared.Tests/**` (last four widened 2026-11-27: the window wait, a new cause enum, item 1's VM check, the fallback list-sync test)

*In progress (wave 2026-11-27), draft PR.*

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
- [ ] 2. Add the closed cause list (`FallbackKind` or a sibling enum), map it in `ReportFallback` and `WindowsPurchases`, and add a Worker rule and tests that accept only the listed causes. Split the settle-budget timeout out of `store_unreachable`.
- [ ] 3. Make first run recover on its own:
  - Retry the list and the Store context with backoff, for example 1 s, 3 s and 7 s, behind the existing wait screen with its Cancel.
  - On Windows, don't create the Store context before the window handler exists. Retry it once the window is up.
  - Show the offline page only after the retries fail. It keeps Try again.
- [ ] 4. Android and iOS get the same retry in the shared `StoreClient` path. Only the Store-context part is Windows-only, because only Windows has that API.

**Verification:**
- Unit tests cover the cause mapping and the retry schedule, including Cancel during a retry.
- Device runs: a Windows VM fresh install shows first run finishing with the network cut for 5 s and then restored, with no manual Try again. Android and iOS run the same check.
- Ninjalog, after release: `index=dino_usage event=fallback stage=store outcome=store_unreachable screen=first_run | stats dc(session) by platform, version`, compared with the 19-26 Nov baseline of 12 sessions. Windows first-launch sessions that end on `first_run` should drop, and the new cause detail should show what is left.
