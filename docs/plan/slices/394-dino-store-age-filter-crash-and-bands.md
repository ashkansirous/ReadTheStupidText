# Slice 394 — Dino: the store's "9–12" chip crashes on Android, every book sits in two age bands, empty filters still show
**Product:** Dino

*Scoped 2026-11-15 from the user's report; scope and Decisions 1-5 approved by the user the same day.* *Built 2026-11-18 (night 2026-11-17-2105, wave 3), PR #1249, not merged; item 1 (the crash) and the live re-ranging are not yet verified.* Premise check 2026-11-18: `PlacePurchaseArea` and the purchase card are gone from `StorePage.xaml*` (Slice 396 removed them), so item 2 has nothing left to fix and the crash cause (item 1) is unproven again; current lines: `BuildChips` StorePage.xaml.cs:791, `RefreshChips` :824, `ShowAuthor` :860, `StoreFilter.InBand` StoreFilter.cs:32. The new book in draft PR #1244 (Love from Seagull Island) is not on main; its band is set when that PR lands (follow-up).

**Touches:** `src/Dino/DinoReader.App/StorePage.xaml.cs` (`BuildChips`/`RefreshChips`, `ShowAuthor`), `src/Dino/DinoReader.Core/StoreFilter.cs`, `src/Dino/DinoReader.App/StorePage.xaml` (`AgeChipsScroll`), `content/dino/*/book.json` (`ageMin`/`ageMax`) and the four starters' `testbooks/*.dino`, the store's `meta.json` per book (published by dino-store.yml publish-store on the batch release, item 6), `tests/Dino/DinoReader.Tests` (StoreFilter tests), Maestro store flows `.maestro/dino/store-age-chips.yaml` and `.maestro/dino/store-age-chips-all-taps.yaml`, this slice file.

## Why

The user (2026-11-15), on Android:

1. Tapping the oldest age chip ("9–12") in the store crashes the app, every time they tried.
2. Every book shows under both "5 & under" and "6–8", so the age chips do not sort anything. Jack and the Beanstalk is for older children; Freddy and the Slipper and The Lion and the Mouse are for the youngest.
3. "9–12" has no book at all, so it should not be shown. The same goes for any filter with nothing in it.

## What the code does today

- **Every book straddles the 5/6 boundary.** All 15 books have ranges `3-6`, `4-7`, `4-8` or `5-8` (`content/dino/*/book.json`), and `StoreFilter.InBand` (`src/Dino/DinoReader.Core/StoreFilter.cs:32`) shows a book under a band when its range *overlaps* it. So every book is under both "5 & under" and "6–8", and none is under "9–12" (min 9).
- **Likely crash cause (to prove, item 1).** "9–12" is the only chip that empties the list. When `_rows` goes to 0, `ApplyFilter` calls `PlacePurchaseArea` (`src/Dino/DinoReader.App/StorePage.xaml.cs:850`), which takes the "Bought books before?" `PurchaseArea` out of `StoryList.Footer` and adds it back to `StoreRoot` in the same pass (Slice 364 review). On Android, a view that was a CollectionView footer can still be attached to the RecyclerView's native parent at that moment, the classic `Java.Lang.IllegalStateException: The specified child already has a parent`. It runs whether `PurchaseArea` is visible or not, and the move back (picking "All ages" again) has the same risk. No crash report has been read yet: the crash store needs wrangler's login (Slice 243).
  - First real reports read 2026-11-16: Dino Android 1.129.2 (store), 2026-11-15 07:36Z, 2 reports, 1 user, `Java.Lang.IllegalStateException` "The specified child already has a parent" from `ViewGroup.AddView` via `LayoutHandler.MapAdd` on a tap (Play issue `ccf56f3f5a591c4a787ab3e17c32465d`, InnerGestureListener.n_onSingleTapUp; crash store ids `46545d4c-4779-400b-bfb8-be167a55d07f`, `576efac0-3f75-49a4-b84a-48b5748c9e02`). The exception matches the likely cause; the reports do not show which tap it was, so the cause is still "likely" until item 1 proves it.
- **The chips are fixed.** `BuildChips` (`StorePage.xaml.cs:699`) adds one chip per `StoreAgeBand` value, whatever the list holds. The author row already hides when no author is in the band (`ShowAuthor`), and the author picker only lists authors in the band, so it never empties the list.

## Decisions (approved by the user, 2026-11-15)

1. **Each book belongs to one band.** Ranges are narrowed so no book crosses 5/6: "5 & under" books get `3-5`, "6–8" books get `6-8`. The overlap rule in `StoreFilter.InBand` stays (a book with a range like `4-7` from a future upload still shows under both), so the fix is in the data, not a rule change.
2. **The grouping** (pages and words from `content/dino/*/story.md`; all are 11-17 pages):

   | Book | Now | Proposed | Why |
   | --- | --- | --- | --- |
   | Freddy and the Slipper | 3-6 | **3-5** | user's example; shortest text (518 words) |
   | The Lion and the Mouse | 4-7 | **3-5** | user's example; short (566 words) |
   | The Three Little Pigs (starter) | 3-6 | **3-5** | repetition, simple plot |
   | Goldilocks | 3-6 | **3-5** | repetition |
   | The Gingerbread Man (starter) | 3-6 | **3-5** | refrain to join in with |
   | The Little Red Hen | 3-6 | **3-5** | refrain, simple moral |
   | The Tortoise and the Hare (starter) | 4-7 | **3-5** | short fable |
   | X Marks the Treasure | 3-6 | **3-5** | picture hunt, simple |
   | Three Billy Goats Gruff | 4-7 | **3-5** | repetition (trip-trap), gentle troll |
   | Jack and the Beanstalk | 5-8 | **6-8** | user's example; longest text (823 words), giant |
   | The Ugly Duckling | 5-8 | **6-8** | feelings, being left out |
   | Good Enough (starter) | 5-8 | **6-8** | feelings story |
   | The Elves and the Shoemaker | 4-8 | **6-8** | slower plot, work and thanks |
   | Little Red Riding Hood | 4-7 | **6-8** | the wolf tricks her |
   | Town Mouse and Country Mouse | 4-7 | **6-8** | contentment moral, more talk |

   That is 9 books in "5 & under" and 6 in "6–8".
3. **A chip with no book is not shown.** After the list loads, only bands with at least one book get a chip, so "9–12" is gone today and comes back by itself when a 9+ book is added. "All ages" shows only when two or more bands do (one band alone filters nothing, so neither chip is shown). A selected band that loses its last book falls back to "All ages".
4. **The author filter follows the same rule:** the author row shows only when the band holds two or more authors (one author filters nothing). Today it hides only at zero.
5. **The crash is fixed in the code too,** not only hidden by dropping the empty chip: an empty result can still happen (a cached list offline, a future book with no range, a band that empties after a refresh).

## Mock added 2026-11-15

The 2026-11-15 Subscription mock (section 4) removes the "Bought books before?" card (`PurchaseArea`) from the Store and moves Restore and Redeem to the Store first line, Settings and the 402 strip: that is Slice 396. Item 2 below (the `PlacePurchaseArea` move) is therefore done by Slice 396, which deletes the card and the move (the mock supersedes keeping two toggled views). Item 1 (prove the crash) still runs first; if the crash is not the card move, Slice 396 does not fix it and items 2-3 are re-scoped. The mock's chips are "All ages", "5 & under", "6–8", as Decision 3 says.

## Work

- Built 2026-11-18 (implement-slice): items 2-5, 7, 8 done (item 2 moot, Slice 396 removed `PlacePurchaseArea`; chips are built once and only hidden, never re-parented, so the chip rebuild cannot recreate the parent clash). Item 1 is still unproven on a device (no reproduction run; the 1.129.2 crash matches the removed `PlacePurchaseArea` move from code). Item 6: the four starters were stamped and rebuilt (versions 11, 17, 14, 14, `testbooks/*.dino`). No manual upload: store books are published by `dino-store.yml`'s publish-store job (the content hash includes the ages, live + 1), and tonight's merges carry the skip-CI marker, so the night's batch release starts it. Flows: `.maestro/dino/store-age-chips.yaml` (log `[AgeChips]`) and `.maestro/dino/store-age-chips-all-taps.yaml` (taps every chip and checks for no FATAL and no "already has a parent"; written by the device run).
- Emulator run 2026-11-18 (build 64f501e12aa3, `reviews/s394-emu.md`): `store-age-chips` PASS (`[AgeChips] visible=True bands=2 selected=0`, then 1, 2, 0), `store-age-chips-all-taps` PASS (0 FATAL, 0 "already has a parent"), `store-tags` PASS. The live store already showed only two bands, so the old oldest-chip crash path (the "9-12" chip) was not exercised: item 1 stays open.
- [ ] 1. Prove the crash: run the store on the Android emulator (Debug, `test-store`), tap "9–12", and read the exception from logcat (and the crash store if wrangler's login is available). Write the stack here. If it is not the `PurchaseArea` move, re-scope items 2-3 before building.
- [x] 2. Fix `PlacePurchaseArea` so moving `PurchaseArea` between `StoryList.Footer` and `StoreRoot` cannot fail on Android (for example: two separate purchase-area views, one in the footer and one at the bottom, each toggled with `IsVisible`, so no view changes parent). Both directions: list to empty, and empty back to list.
- [x] 3. Age chips from the data: build the chips from the bands that hold a book (`StoreFilter` gets a pure `BandsWithBooks(items)`), rebuild them when the list loads or refreshes, hide "All ages" (and the whole chip row) when fewer than two bands have books, and fall back to "All ages" when the selected band is gone.
- [x] 4. Author row: show it only when the current band holds two or more authors; clear a chosen author that is no longer offered (as today).
- [x] 5. Change `ageMin`/`ageMax` in the 15 `content/dino/*/book.json` files to the table in Decision 2 (the four starters too: their store entries come from `StoreBuilder.StarterEntries`).
- [x] 6. (No manual upload; the publish-store job does it on the batch release, see Work above. The live list is still to be read back.) Get the new ranges into the live store: find out whether a `meta.json`-only change needs a book version bump (`tools/dino/upload-store-story.ps1` checks `story-v<version>.dino` is present), then rebuild each book's store folder (BookBuilder) and re-upload with `upload-store-story.ps1`. Read the Worker's list back and check every book's `ageMin`/`ageMax`.
- [x] 7. Tests (`tests/Dino/DinoReader.Tests`): `BandsWithBooks` for no books, one band, two bands, a book with no range (shows under every band, so it must not make an empty band look full); `InBand` with the new `3-5`/`6-8` ranges puts each book under exactly one band.
- [x] 8. A Maestro flow on the Android emulator: open the store, assert "9–12" is not shown, tap "5 & under" and "6–8" and check a book from each list (Freddy under "5 & under", Jack under "6–8" and not "5 & under"), tap "All ages" back; no crash.

## Verification

- On the Android emulator: the store shows "All ages", "5 & under" and "6–8" only; Jack and the Beanstalk is only under "6–8"; Freddy and the Slipper and The Lion and the Mouse are only under "5 & under"; switching chips never crashes (item 8).
- A Debug-only check that empties the list (an author and band with no book, or an empty test store) moves the purchase card without a crash (item 2).
- The user taps the store chips on their own Android phone after the release.

#### From review

- Review read: `reviews/pr-1249-high.md` findings 1-5 all applied. Finding 1 (crash fix unproven, medium) is applied as a record: item 1 stays open and is listed under *Not yet verified*; the emulator run above did not reach the old crash path. Finding 2 (chip row margin gap), 3 (stale author), 4 (record and a test reading the real `book.json` ranges) and 5 (flow scrolls; the flow depends on the re-upload) are fixed.
- `reviews/pr-1249-medium.md` findings 1-5 applied (load order of `ShowAuthor`/`ApplyFilter`, no `ApplyFilter` after Appearing changes, filter state out of `RefreshChips`, `TryGetProperty` in the range test, re-version of the other books).
- `reviews/pr-1249-check-medium.md`: findings 2, 3 and 5 applied.
- `reviews/pr-1249-check2-medium.md` follow-ups (no blocking findings):
  - `StorePage.xaml.cs:770`: `RenderFilter` no longer writes the resolved author back to `_author`, so a dropped author is reapplied if it returns after a reload. Nothing crashes. (Follow-up. Coordinator read the code 2026-11-18: a chip tap drops the author for good (`StorePage.xaml.cs:858`), as the decision says; a reload or Appearing keeps the requested `_author` (`:773`), so an author whose band emptied before a reload can come back when that band returns. Making the reload path drop it too is the follow-up.)
  - `StorePage.xaml.cs:779`: `ApplyFilter` skips `_rows.Clear()` when the list is unchanged; fine while `StoreRow` compares by reference (no fault).
  - `.maestro/dino/store-age-chips.yaml:6`: the flow depends on the live `meta.json` ranges; a false red until the re-upload lands.
  - `content/dino/tortoise-and-hare/book.json:5`: the live `/api/stories` meta must get the same age bump (publish-store, item 6).
- Item 6 needs no manual `meta.json` rebuild for the version: `StoreVersioning.Decide` re-versions a book whose content hash (it includes `ageMin`/`ageMax`) differs from the live hash on the next publish-store run.
- Re-upload of re-ranged books (`reviews/pr-1249-medium.md` #5): no version bump is needed for the store-only books. `BookHash.Of` covers `ageMin`/`ageMax`, and `publish-store-books.mjs --live-state` plus `BookBuilder build --live` re-version any book whose hash differs from the live one (`StoreVersioning.Decide`: live + 1). Only the four starter books carry a stored hash/version in `book.json`, and those were bumped.
- Review round 2 (`reviews/pr-1249-medium.md` #1-#3): the band/author state is now one pure `StoreFilter.Resolve` (band, author, visible bands, author row); `StorePage` keeps only the requested values and draws chips, author row and list from that one result (`RenderFilter`) on load, Appearing and every tap.
- Skipped #6 (`reviews/pr-1249-medium.md`): "Rebuilt bundles are not verifiable in review; no check that embedded ranges match book.json" - the four `testbooks/*.dino` are the BookBuilder stamp/build output that `BookBuilder check` requires and CI compares.
- Skipped #7 (`reviews/pr-1249-medium.md`): "Asserts literal chip text and titles against live store data" - item 8 asks for a flow against the live store; its dependence on the re-upload is already recorded above.
- Decision taken by the coordinator (review `reviews/pr-1249-check-medium.md` #1, obvious choice): an author whose band empties is dropped and does not come back; the requested band is kept and returns when its books do. `SelectBand` compares the tapped band with the requested band (#2); `ApplyFilter` rebuilds the rows only when the resolved list changed (#3); tests for both rules in `StoreFilterTests` (#5).
- Skipped #1 (`reviews/pr-1249-check-medium.md`): "`_author = _state.Author` overwrites the requested author with the resolved one; the requested author is not kept separately" - no code change: the coordinator decision above makes dropping the author the rule.
- Skipped #4 (`reviews/pr-1249-check-medium.md`): "src/Dino/DinoReader.Core/StoreFilter.cs:70 (Resolve), low. With fewer than two authors, Author is null although the request matched the only author, so `authorFilterSet` analytics flag reads false while the list is limited to that author" - not a bug: with Author null the list is not limited by it (`Apply` and the flag both read `_state.Author`), and keeping it would leave a hidden author filter the grown-up could not clear.
- Test `EveryBookJsonRange_FitsExactlyOneBand` reads every `content/dino/*/book.json` and fails when a range crosses the 5/6 line (Decision 1).
- Fixed the review's findings 2 (chip row margin gap), 3 (stale author when the author row hides) and 5 (flow scrolls to rows; the `[AgeChips]` marker prints the band count and the selected band's number, so no enum names).
- Follow-ups from the last check pass (`reviews/pr-1249-check3-medium.md`, all low, after the device run added `store-age-chips-all-taps.yaml`): #1 `StorePage.xaml.cs:770` the reload path keeps the requested author (same as the check2 follow-up above); #2 `:868` `OnAuthorTapped` sets `_author` after its await without re-resolving; #3 `:1251` download flags read the resolved `_state` while `_band` is the requested value (likely benign); #4 `:780` the empty-result `NoMatchLabel` path is still unexercised.

## Out of scope

- Changing the bands themselves (Google Play's 5 & under, 6-8, 9-12; Decision 116).
- Writing a 9-12 book.
- iOS and Windows were not reported crashing; the item-2 fix runs on every head, so a quick store tap on the iOS simulator is enough there.

#### For CLAUDE.md

For `src/Dino/DinoReader.App/CLAUDE.md`:

- The store's age band, author, chip list and author row come from one pure result, `StoreFilter.Resolve` (`src/Dino/DinoReader.Core/StoreFilter.cs`); `StorePage.xaml.cs` keeps only the requested band and author and draws everything through `RenderFilter`. Never set band or author state elsewhere in the page. An author whose band empties is dropped and does not come back; the requested band is kept and returns when its books do. Age chips are built once and only hidden, never re-parented (Android "already has a parent" crash). (Slice 394: docs/plan/slices/394-dino-store-age-filter-crash-and-bands.md)
- Every `content/dino/*/book.json` range fits one band (`3-5` or `6-8`); `EveryBookJsonRange_FitsExactlyOneBand` fails on a range that crosses 5/6. A new book gets its band when it lands. (Slice 394: docs/plan/slices/394-dino-store-age-filter-crash-and-bands.md)

#### Not yet verified

- Item 1, the crash fix with old-range data: tap the oldest chip on a build whose store still has a "9-12" chip, or check the crash store after the release (`Java.Lang.IllegalStateException` "already has a parent", Dino Android 1.129.2). The emulator run did not exercise that path.
- iOS and Windows store chip taps.
- The live store list after the batch release publishes the re-ranged books (read `/api/stories` back; every book's `ageMin`/`ageMax`, Tortoise and the Hare included).
- The Love from Seagull Island book (draft PR #1244) needs its band when it lands.
