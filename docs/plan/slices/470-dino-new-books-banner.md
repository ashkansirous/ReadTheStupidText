# Slice 470: Dino: a banner of new books when a grown-up opens the app

**Product:** Dino

*Plan cleanup check 2026-11-28: verdict keep open, not started. Read `origin/main`: no `addedAt`, `NewBooks/` or banner mock in `docs/design/` (only dino-reader, rtst-phone, sonos). The mocks are a start condition (`Starts after`), not a wait inside the work; no other claim in this file was stale. The user's own words (2026-11-28 12:45) match the Decisions.*

*Planned 2026-11-28 from the owner's RefineScope answers (this session). **Starts after:** the owner's Claude Design mocks of the banner for 1, 2, 3, 4 and 5 books (owner: "I will design a banner for 1,2,3,4,5 books with claude design before starting to implement it"). When the mock arrives, `import-claude-design` copies it into `docs/design/` and the mock decides the banner's look and words.*

**Touches:** `.github/scripts/publish-store-books.mjs`, `worker/stories.ts`, `worker/stories*.test.mjs`, `src/Dino/DinoReader.Core/StoreCatalog.cs`, `src/Dino/DinoReader.App/App.xaml.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml`, `src/Dino/DinoReader.App/GalleryPage.xaml.cs`, `src/Dino/DinoReader.App/StorePage.xaml.cs`, `src/Dino/DinoReader.App/NewBooks/` (new), `src/Dino/DinoReader.App/Testing/`, `tests/Dino/DinoReader.Tests/`, `tools/dino/` (the banner flow), `docs/design/` (the mock).

## Why

The owner is about to publish three new books and Dino has no way to tell families about them. The news must reach the grown-up, not interrupt a child playing. A cold start is most likely the grown-up, so that is when the banner shows. Slice 471 adds the push notification, and tapping it opens this same banner.

## Decisions (owner, 2026-11-28, this session)

- The banner shows on a **cold start** (not on a resume) when the store has books released after the family's **last store visit**. It shows the covers of those books, **newest first, at most 5**.
- **Once per batch** (owner: "once per batch. less nagging"). After the banner has been shown for a set of new books, it does not show again until a newer book is released. Opening the Store also counts as seeing them.
- **The last store visit is saved every time the Store opens** (owner: "always save the latest time they opened the store in the app").
- **New installs** count the install time as a store visit, so there is no banner on the first launch (owner: agreed).
- **Families who update the app** to this version get the update time as their last store visit, as if they had just seen the store (owner: "the ones that update the app, should see the update time as if they saw the shop"). Old books never show as new.
- Tapping a cover opens the grown-up lock, then that book in the Store. The banner itself sits on the children's shelf and has a close button. Nothing behind it is reachable without the lock.
- Android, iOS and the Windows head all get the banner.
- The mock decides layout, words and the close control (*Claude Design* rule: the mock always trumps the plan).

Decided by this session (not the owner's words): the server already keeps a date-only `added` per book (`worker/stories.ts:133`, used for ordering at :429). A day is too coarse to compare with a visit time, so publishing also records the exact UTC time the book first went live.

## Work

- [ ] 1. **Release time on every book.** `publish-store-books.mjs` writes `addedAt` (ISO 8601 UTC) into `meta.json` the first time a book goes live, and never changes it on later versions. Books already live get a one-time backfill of their `added` date at 00:00 UTC, or the first `story-v1` upload time where there is no `added`. `worker/stories.ts` passes `addedAt` through on each `/api/stories` item (bump the cache key constant at :79) and orders by it after the tags. Add worker tests.
- [ ] 2. **App reads it.** `StoreItem` gains `AddedAt` (nullable `DateTimeOffset`), and `StoreCatalog.Parse` reads it. A book with no `AddedAt` is never "new". Add tests.
- [ ] 3. **The two saved times.** Add named-constant `Preferences` keys: last store visit, and newest release time already shown in a banner. Opening `StorePage` writes the visit time. On the first launch of this version, the visit time is set to now, both on a new install and after an update (one "migrated" flag). Unit-test the rule "new = AddedAt > max(last visit, last shown)", capped at 5, newest first.
- [ ] 4. **Banner on cold start.** In `App.xaml.cs`, after the catalog fetch that cold start already does (around :171), if there are new books, the shelf (`GalleryPage`) shows the banner built from the mock for 1 to 5 covers. Showing it saves "last shown". Covers load from `CoverUrl` with the existing cover cache, and any wait over 500 ms shows a placeholder. A failed fetch shows nothing. A resume never shows it.
- [ ] 5. **Tap and close.** Tapping a cover opens `GrownUpGate` and then the Store scrolled to that book. Close hides it. Expose a "show banner for books since <time>" entry point that ignores "last shown", for Slice 471's notification tap.
- [ ] 6. **Android, iOS and Windows tasks.** The same behaviour on each head. The Windows head runs it the same way from its window start.
- [ ] 7. **Debug test hook and scripted flow.** A Debug-only hook sets the two saved times and a fake catalog. Add a headless flow `new-books-banner` with these cases: a cold start with 3 new books shows 3 covers; the second cold start shows none; opening the Store clears them; a fresh install shows none; a cover tap shows the lock.

## Verification

- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj`, plus the worker tests (`npm test` in `worker/`).
- `new-books-banner` passes on the Android emulator, the S21 and an iOS simulator, and the Windows head is checked with `Build-TestPackage.ps1`.
- After the first merge, read the live `/api/stories` and check that every item has `addedAt`.
