# Slice 459 — Dino first books: the first book is playable within seconds of Continue
**Product:** Dino

*Built 2026-11-27 (night wave 2), PR #1395, not merged; not yet verified on the S21 throttled, the iOS simulator or the Windows VM (see Not yet verified).*

*Planned 2026-11-27 from the 2026-11-26 usage analysis (Ninjalog `ashkan-sirous/dino-book-reader`, index `dino_usage`, 19-26 Nov).*

**Starts after:** [Slice 453](453-dino-first-books-cancelled-load-copy.md) (both edit `FirstBooksPage.cs`). The small "quick" package is [Slice 463](463-dino-quick-first-book-package-for-slow-internet.md).

**Touches:** `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Dino/DinoReader.App/FirstBooksPage.Views.cs`, `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/OnboardingSteps.cs`, `src/Dino/DinoReader.App/OnboardingSteps.Languages.cs`, `src/Dino/DinoReader.App/FirstRunPage.xaml.cs`, `tests/Dino/DinoReader.Tests/FirstBooksTests.cs`, `docs/analytics/dino-session-queries.md`, `src/Dino/DinoReader.App/FirstBookPrefetch.cs` (new), `src/Dino/DinoReader.App/OnboardingSteps.Route.cs`, `src/Dino/DinoReader.App/AudioPackStore.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/MauiProgram.cs`, `src/Dino/DinoReader.Core/FirstBooks.cs`, `tests/Dino/DinoReader.Tests/FirstBookPrefetchTests.cs` (new), `.maestro/dino/**`, `src/Dino/DinoReader.Core/BackgroundQueue.cs` and `src/Dino/DinoReader.Core/BackgroundJobStore.cs` (new, pure queue rules), `tests/Dino/DinoReader.Tests/BackgroundQueueTests.cs` (new), `src/Dino/DinoReader.App/GalleryPage.xaml.cs` and `tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` (added after the build, 2026-11-27: Decision 3's shelf progress and Cancel for the background queue live on the gallery) (widened at claim, 2026-11-27, from the premise check: `FirstBooks.Order/ToDownload/EndOfContinue`, pack install and the onboarding route live there)

**Why:**
- On the `first_books` screen, 14 first-launch sessions showed `wait_shown getting_file:>20` (over 20 s), plus more at 5-10 s and 10-20 s. Most still finished.
- At least one real user (Iran, Android 1.131.1) left during a 10-20 s wait. Another (Japan, Android 1.136.0) hit `store:store_failed` there.
- What the code does today (origin/main, 2026-11-27):
  - `ContinueAsync` (`FirstBooksPage.cs:361`) downloads the ticked books one after another (`:405-412`).
  - Each book's audio packs install after it, one per story language, up to 3 (`StoreClient.cs:438-452, 615-640`).
  - The screen is blocked until every book and pack is done. Nothing downloads before Continue.
- Measured sizes (live list `https://dinoreader.sirous.uk/api/stories`, 2026-11-27):
  - A free book is 4-6 MB with one language: package 2.5-3.9 MB of 896x1200 JPEG q85 pictures, plus a 1.5-2.3 MB pack of AAC 48 kb/s clips.
  - Two story languages add another 1.5-2.4 MB per book.
  - The four free books in one language are about 20 MB: about 2.7 min at 1 Mbit/s, 32 s at 5 Mbit/s.

**Decisions (owner, 2026-11-27: "1- I agree", to these three):**
- Start downloading the pre-ticked first book (The Three Little Pigs) while the parent is still on the intro steps.
- Get the first ticked book and its main-language pack first, and let the parent open it as soon as those two land.
- The other ticked books and the other languages' packs carry on in the background, with progress on the shelf. Nothing blocks the parent.

**Open for the owner:** none (the slow-internet package is Slice 463).

**Design (coordinator, at claim 2026-11-27; the builder builds this, it does not invent another):**
- Today (origin/main 47dbf9ee): nothing is persisted; `FirstBooksPage` holds `_ticked`, `_onShelf`, `_tickOrder`, `_downloadCancel` (`FirstBooksPage.cs:71-74`); `StoreClient._inFlight` (`StoreClient.cs:177, 341`) hands a second caller the FIRST caller's task, cancel token and progress; `WantedPacks` reads `StoryLanguages.Current()` at download time (`StoreClient.cs:403`); the store list is fetched only in `FirstBooksPage.LoadAsync`.
- **One owner of the prefetch state:** a new singleton `FirstBookPrefetch` (App, registered in `MauiProgram.cs`). It owns its own `CancellationTokenSource`, one task for one (book id, main language), and a progress value the page can subscribe to and unsubscribe from. The page never passes its own token into it, and it never calls `StoreClient.DownloadAsync` while a page download of the same id is in flight (it is started first, and the page joins it, never the reverse).
- **States:** `Idle` -> `Listing` (fetches the store list once; the result is cached and `FirstBooksPage.LoadAsync` reuses it when present, else fetches as today) -> `Downloading` (package, then the main-language pack only) -> `Landed` | `Failed` | `Cancelled`.
- **Start:** right after the story language is saved (`OnboardingSteps.Languages.cs:134`), for the pre-ticked first book from `FirstBooks.Order`, only if it is not on the shelf already. Telemetry on/off changes nothing.
- **Continue:** the page's queue is: the prefetched book first (await the prefetch task through the page's own wait, with the prefetch's progress shown), else the first ticked book plus its main pack; once that one book is on the shelf with its main pack, the wait ends and the shelf opens with it playable (item 2). The other ticked books, then the other languages' packs of every book, run in a background queue owned by `StoreClient` (not by the page, which is gone), with the existing shelf progress and Cancel.
- **Interrupts:**
  - Untick of the prefetched book: cancel the prefetch; delete its `.part`; if it already landed, remove it from the shelf (Decision 1: "cancel and delete it").
  - Page Cancel / Back during the first-book wait: cancels the prefetch and the queue, exactly as today's `_downloadCancel` (landed books stay); `OnDisappearing` stays as is.
  - Offline or failed prefetch: silent (no UI, `Failed`); Continue then runs the normal first-book path, which shows today's error and Retry.
  - Retry after a failure: a fresh attempt from zero; a `Failed`/`Cancelled` prefetch is never joined.
  - App killed: the prefetch does not resume. The background queue does: its remaining jobs (book ids and pack languages) are saved in Preferences each time they change and picked up again at the next launch (changed after review 1 on 2026-11-27, `reviews/pr-1395-high.md` #2: without this, a kill loses the books the parent ticked). A failed background job shows on the shelf banner with Retry, never silently. On next launch, a leftover `.part` of the prefetched id is deleted before the prefetch starts again from zero; a book on the shelf without its main pack gets that pack queued by the same background queue.
  - Language changed in the intro after the prefetch started: cancel and restart with the new main language.
- **Concurrency, one design (coordinator, review pass 2, 2026-11-27; second strike in the join/queue area, `reviews/pr-1395-medium.md`):**
  - `BackgroundBooks` is one serial worker. Jobs live in one list under one lock. `Start`, `Retry` and `ResumeAsync` only add jobs and wake the worker, starting it if none runs, and they never drop jobs. `Cancel` raises a generation number and cancels the current job's token. The worker finishes unwinding, drops the jobs of the old generation and goes on with any added later. Callbacks (`Changed`, `BookLanded`) run outside the lock.
  - Per book id in `StoreClient`, at most one download runs. A caller that finds a cancelled or finished entry waits for it to end, ignoring its result, and then starts fresh. A caller that joins a running download afterwards installs the packs in its own `packLanguages` that the joined run did not install (`EnsurePackAsync`).
  - A job is dropped, not retried, when its book is no longer on the shelf (pack jobs) or the store says it is gone. Any other failed job is retried at most 3 launches, then dropped. While it waits it shows as failed on the banner.
  - The saved job list is parsed defensively. Bad entries are dropped, and an unreadable value clears the key.
  - `FirstBookPrefetch.Start` decides and swaps its run under its lock. Untick waits for the cancelled run to end before it removes a book that landed.
  - A job cancelled only because a joined download was cancelled goes back in the queue, unless the queue's own generation was cancelled.
  - At launch, every leftover first-books `.part` is swept, not only the first book's.
- **Simplified after the check pass (coordinator, 2026-11-27, `reviews/pr-1395-check.md`; this replaces the `StoreClient` part of the bullet above):** the join and top-up changes in `StoreClient` reached the store page and Restore (#4, #5). So `StoreClient.DownloadAsync` goes back to `origin/main`'s join behaviour, with one change only: an entry whose task is cancelled or finished is removed before a new caller looks, so it is never joined. `TopUpPacksAsync` goes. Packs are never installed by joining. After a book lands, the first-book path and the queue each install the packs they need as their own steps (`EnsurePackAsync` per language), so a language change or a joined run still gets its main pack (#1).
  - The page runs untick, re-tick and Continue one at a time. Continue waits for any untick removal in flight before it plans (#2).
  - Leftover `.part` files are matched by the exact name `StoreClient` writes, never by a prefix glob (#3).
  - Worker callbacks are wrapped, so a throwing handler cannot stop the worker (#6).
  - `_installedByRun` is cleared once Continue hands over: after that the book is the parent's, and an untick never deletes it (#7).
  - A book missing from the listing counts as a failed attempt under the 3-launch budget, not as done (#8).
- **Pure function:** the queue order and the early-open point are one pure, unit-tested function in `src/Dino/DinoReader.Core/FirstBooks.cs` (input: ticked ids in order, on-shelf ids, prefetched id and its state, story languages; output: the first-book step and the background list).

**Work:**
- [x] 1. Prefetch: once the story language is known in the intro, start the pre-ticked book's package and main-language pack in the background. If the parent unticks it, cancel and delete it. Telemetry off or offline changes nothing about what is shown.
- [x] 2. Order and early open: after Continue, finish the first book plus its main pack first, then open the shelf with that book playable. The remaining books and packs queue in the background and keep the existing progress and Cancel behaviour on the shelf. A pack failure keeps the book, as today.
- [x] 3. Wait screen: shows only until the first book is ready, with real progress for that one book (over 500 ms rule). `wait_shown getting_file` keeps its buckets, so the before/after comparison holds.
- [x] 4. Android, iOS and Windows all get it, through the shared MAUI code.

**Verification:**
- Unit tests for the queue order, the early-open point, prefetch cancel on untick, and pack failure in the background.
- Scripted first-run device flows on Android (S21), iOS and the Windows VM, with the network throttled to 1 Mbit/s: the first book opens before the rest finish.
- Ninjalog, after release: `index=dino_usage event=wait_shown screen=first_books detail=getting_file:* | stats dc(session) by detail, version`. Real first-launch sessions with `getting_file:>20` should be rare, against 14 in 19-26 Nov, and `10-20` should shrink with them.

*Changed while building:* the Design paragraph and its later bullets record each redesign (review 2: one concurrency design; check pass: `StoreClient.DownloadAsync` back to `origin/main`'s join behaviour). The first-run gate clears at the first book (Decision 2), so a later background failure shows on the shelf banner with Retry, not on the picker. New strings `FirstBooks_Queue_One/Other/Audio/Failed` in 15 languages; the 14 translations are machine-written.

*Built and run:* Android, Windows and iOS compile. Dino tests 5028 pass; Shared `FallbackGuardTests` pass. Emulator Maestro flows `first-book-opens-first`, `first-books-picker`, `first-books-offline`, `onboarding-phone` and `first-books-cancel` all PASS (logs `D:/ReadTheStupidText-worktrees/reviews/s459-device/` and `run2/`).

#### From review

- `reviews/pr-1395-high.md` (9 findings): #1, #2, #3, #4, #6, #7, #8 fixed (its Outcome section). #5 and #9: see the two lines below.
- Skipped #5 (`reviews/pr-1395-high.md`): "`catch (OperationCanceledException)` assumes "we cancelled", and an HttpClient or stall timeout is also an OperationCanceledException." The reviewer marked it unconfirmed and could not reproduce a stuck state, and StoreClient handles its own timeouts.
- Skipped #9 (`reviews/pr-1395-high.md`): "Tests cover only the pure functions in FirstBooks." `FirstBookPrefetch` depends on the MAUI-bound StoreClient and FileSystem and cannot be linked into the test project. Its rules are tested as pure Core functions, and the class through the emulator flows.
- `reviews/pr-1395-medium.md` (10 findings): all 10 fixed (its Outcome section).
- `reviews/pr-1395-check.md` (8 findings): all 8 fixed (its Outcome section; #4 and #5 fixed by taking `StoreClient.DownloadAsync` back to `origin/main`'s join behaviour and deleting `TopUpPacksAsync`).
- `reviews/pr-1395-check2-medium.md` (5 findings): #1 and #2 fixed; #3, #4, #5 are the follow-ups below.
- `reviews/pr-1395-check3-medium.md` (3 findings): #1 and #2 fixed; #3 is the follow-up below.
- `reviews/pr-1395-check4-medium.md`: INCOMPLETE pass (the fork was not run; the delta was read directly, no findings). It was rerun as check5, so check4 counts for nothing.
- `reviews/pr-1395-check5-medium.md` (4 lows, no crash, deadlock or data-loss): no fix; all four are follow-ups below.
- `reviews/pr-1395-check6-medium.md`: no findings (delta since check5 only: a `no-fallback` comment and the expect-log markers; `FallbackGuardTests` 48 passed).

- Follow-up #3 (`reviews/pr-1395-check2-medium.md`): "`_listing` is not cleared, so every later picker run in the process uses the first listing and cannot show offline or load-failed, and may offer a removed or re-priced book." - low; affects only a later picker run; clear it in HandOver, DetachRun or on Failed.
- Follow-up #4 (`reviews/pr-1395-check2-medium.md`): "Records a store download for a prefetched book that was already on the shelf (installed=false), skewing the before/after count." - low; no user-visible effect; record only when the prefetch or page fetched the package.
- Follow-up #5 (`reviews/pr-1395-check2-medium.md`): "A queue package job that joined the prefetch download, then saw it cancelled by an untick or language change, settles as Failed and its pack jobs fail with it, so the shelf shows the failed banner for a book the grown-up dropped." - low; Retry recovers it; re-run once when the job ended Cancelled while its own token was not cancelled.
- Follow-up #3 (`reviews/pr-1395-check3-medium.md`): "Missing-book path: Try again reloads the same cached prefetch listing, so the failure can repeat; CancelRun already ran before the lookup." - low; look the item up before CancelRun and drop stale ticks absent from _items.
- Follow-up #1 (`reviews/pr-1395-check5-medium.md`): "AwaitUntickRemovalAsync waits on _untickDone with only Cancel as exit." - low; one hung removal blocks every later Continue until restart; add a logged timeout or let Continue re-read the shelf.
- Follow-up #2 (`reviews/pr-1395-check5-medium.md`): "_downloadCancel is set before ShowGetting and the try; if ShowGetting throws, it stays set" - low, pre-existing shape in GetFirstBookAsync; move ShowGetting into the try.
- Follow-up #3 (`reviews/pr-1395-check5-medium.md`): "Untick wait shows stale _bookOfText/_percentText/_progress from an earlier cancelled download" - low; reset the fields before ShowGetting; log and count a cancel.
- Follow-up #4 (`reviews/pr-1395-check5-medium.md`): "A removal not yet finished shows the full getting page at once, no 500 ms delay" - low; show it only after about 500 ms.
- Follow-up: a native-speaker read of the 14 machine-written translations of `FirstBooks_Queue_One/Other/Audio/Failed`.

#### For CLAUDE.md

- First-book prefetch and the background queue live in the `FirstBookPrefetch` singleton (`src/Dino/DinoReader.App/FirstBookPrefetch.cs`); the page never hands it its own token. `StoreClient.DownloadAsync` keeps `origin/main`'s join behaviour (only an ended entry is removed before a lookup): install packs as their own step (`EnsurePackAsync`), never by joining. (Slice 459: docs/plan/slices/459-dino-first-book-playable-in-seconds.md)

#### Not yet verified

- [phone] The S21 first run with the network throttled to 1 Mbit/s: the first book opens before the rest finish (`tools/emulator` has no throttle flag).
- [cloud] iOS simulator first run.
- [session] Windows VM first run (VM not started: 7.8 GB free memory, under the 8 GB the VM needs).
- [session] The Ninjalog `wait_shown` query after release (the Verification line above).
