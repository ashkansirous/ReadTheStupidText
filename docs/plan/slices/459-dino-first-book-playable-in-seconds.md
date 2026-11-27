# Slice 459 — Dino first books: the first book is playable within seconds of Continue
**Product:** Dino

*In progress (wave 2026-11-27), draft PR.*

*Planned 2026-11-27 from the 2026-11-26 usage analysis (Ninjalog `ashkan-sirous/dino-book-reader`, index `dino_usage`, 19-26 Nov).*

**Starts after:** [Slice 453](453-dino-first-books-cancelled-load-copy.md) (both edit `FirstBooksPage.cs`). The small "quick" package is [Slice 463](463-dino-quick-first-book-package-for-slow-internet.md).

**Touches:** `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Dino/DinoReader.App/FirstBooksPage.Views.cs`, `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/OnboardingSteps.cs`, `src/Dino/DinoReader.App/OnboardingSteps.Languages.cs`, `src/Dino/DinoReader.App/FirstRunPage.xaml.cs`, `tests/Dino/DinoReader.Tests/FirstBooksTests.cs`, `docs/analytics/dino-session-queries.md`, `src/Dino/DinoReader.App/FirstBookPrefetch.cs` (new), `src/Dino/DinoReader.App/OnboardingSteps.Route.cs`, `src/Dino/DinoReader.App/AudioPackStore.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/MauiProgram.cs`, `src/Dino/DinoReader.Core/FirstBooks.cs`, `tests/Dino/DinoReader.Tests/FirstBookPrefetchTests.cs` (new), `.maestro/dino/**` (widened at claim, 2026-11-27, from the premise check: `FirstBooks.Order/ToDownload/EndOfContinue`, pack install and the onboarding route live there)

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
  - App killed: no resume (as today). On next launch, a leftover `.part` of the prefetched id is deleted before the prefetch starts again from zero; a book on the shelf without its main pack gets that pack queued by the same background queue.
  - Language changed in the intro after the prefetch started: cancel and restart with the new main language.
- **Pure function:** the queue order and the early-open point are one pure, unit-tested function in `src/Dino/DinoReader.Core/FirstBooks.cs` (input: ticked ids in order, on-shelf ids, prefetched id and its state, story languages; output: the first-book step and the background list).

**Work:**
- [ ] 1. Prefetch: once the story language is known in the intro, start the pre-ticked book's package and main-language pack in the background. If the parent unticks it, cancel and delete it. Telemetry off or offline changes nothing about what is shown.
- [ ] 2. Order and early open: after Continue, finish the first book plus its main pack first, then open the shelf with that book playable. The remaining books and packs queue in the background and keep the existing progress and Cancel behaviour on the shelf. A pack failure keeps the book, as today.
- [ ] 3. Wait screen: shows only until the first book is ready, with real progress for that one book (over 500 ms rule). `wait_shown getting_file` keeps its buckets, so the before/after comparison holds.
- [ ] 4. Android, iOS and Windows all get it, through the shared MAUI code.

**Verification:**
- Unit tests for the queue order, the early-open point, prefetch cancel on untick, and pack failure in the background.
- Scripted first-run device flows on Android (S21), iOS and the Windows VM, with the network throttled to 1 Mbit/s: the first book opens before the rest finish.
- Ninjalog, after release: `index=dino_usage event=wait_shown screen=first_books detail=getting_file:* | stats dc(session) by detail, version`. Real first-launch sessions with `getting_file:>20` should be rare, against 14 in 19-26 Nov, and `10-20` should shrink with them.
