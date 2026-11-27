# Slice 459 — Dino first books: the first book is playable within seconds of Continue
**Product:** Dino

*Planned 2026-11-27 from the 2026-11-26 usage analysis (Ninjalog `ashkan-sirous/dino-book-reader`, index `dino_usage`, 19-26 Nov).*

**Starts after:** [Slice 453](453-dino-first-books-cancelled-load-copy.md) (both edit `FirstBooksPage.cs`). The small "quick" package is [Slice 463](463-dino-quick-first-book-package-for-slow-internet.md).

**Touches:** `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Dino/DinoReader.App/FirstBooksPage.Views.cs`, `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/OnboardingSteps.cs`, `src/Dino/DinoReader.App/OnboardingSteps.Languages.cs`, `src/Dino/DinoReader.App/FirstRunPage.xaml.cs`, `tests/Dino/DinoReader.Tests/FirstBooksTests.cs`, `docs/analytics/dino-session-queries.md`

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

**Work:**
- [ ] 1. Prefetch: once the story language is known in the intro, start the pre-ticked book's package and main-language pack in the background. If the parent unticks it, cancel and delete it. Telemetry off or offline changes nothing about what is shown.
- [ ] 2. Order and early open: after Continue, finish the first book plus its main pack first, then open the shelf with that book playable. The remaining books and packs queue in the background and keep the existing progress and Cancel behaviour on the shelf. A pack failure keeps the book, as today.
- [ ] 3. Wait screen: shows only until the first book is ready, with real progress for that one book (over 500 ms rule). `wait_shown getting_file` keeps its buckets, so the before/after comparison holds.
- [ ] 4. Android, iOS and Windows all get it, through the shared MAUI code.

**Verification:**
- Unit tests for the queue order, the early-open point, prefetch cancel on untick, and pack failure in the background.
- Scripted first-run device flows on Android (S21), iOS and the Windows VM, with the network throttled to 1 Mbit/s: the first book opens before the rest finish.
- Ninjalog, after release: `index=dino_usage event=wait_shown screen=first_books detail=getting_file:* | stats dc(session) by detail, version`. Real first-launch sessions with `getting_file:>20` should be rare, against 14 in 19-26 Nov, and `10-20` should shrink with them.
