# Slice 394 — Dino: the store's "9–12" chip crashes on Android, every book sits in two age bands, empty filters still show
**Product:** Dino

*Scoped 2026-11-15 from the user's report; scope and Decisions 1-5 approved by the user the same day. Not built yet.*

**Touches:** `src/Dino/DinoReader.App/StorePage.xaml.cs` (`PlacePurchaseArea`, `BuildChips`/`RefreshChips`, `ShowAuthor`), `src/Dino/DinoReader.Core/StoreFilter.cs`, `content/dino/*/book.json` (`ageMin`/`ageMax`), the store's `meta.json` per book (rebuild and re-upload), `tests/Dino/DinoReader.Tests` (StoreFilter tests), a Maestro store flow under `.maestro/dino/`, this slice file.

## Why

The user (2026-11-15), on Android:

1. Tapping the oldest age chip ("9–12") in the store crashes the app, every time they tried.
2. Every book shows under both "5 & under" and "6–8", so the age chips do not sort anything. Jack and the Beanstalk is for older children; Freddy and the Slipper and The Lion and the Mouse are for the youngest.
3. "9–12" has no book at all, so it should not be shown. The same goes for any filter with nothing in it.

## What the code does today

- **Every book straddles the 5/6 boundary.** All 15 books have ranges `3-6`, `4-7`, `4-8` or `5-8` (`content/dino/*/book.json`), and `StoreFilter.InBand` (`src/Dino/DinoReader.Core/StoreFilter.cs:32`) shows a book under a band when its range *overlaps* it. So every book is under both "5 & under" and "6–8", and none is under "9–12" (min 9).
- **Likely crash cause (to prove, item 1).** "9–12" is the only chip that empties the list. When `_rows` goes to 0, `ApplyFilter` calls `PlacePurchaseArea` (`src/Dino/DinoReader.App/StorePage.xaml.cs:850`), which takes the "Bought books before?" `PurchaseArea` out of `StoryList.Footer` and adds it back to `StoreRoot` in the same pass (Slice 364 review). On Android, a view that was a CollectionView footer can still be attached to the RecyclerView's native parent at that moment, the classic `Java.Lang.IllegalStateException: The specified child already has a parent`. It runs whether `PurchaseArea` is visible or not, and the move back (picking "All ages" again) has the same risk. No crash report has been read yet: the crash store needs wrangler's login (Slice 243).
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

## Work

- [ ] 1. Prove the crash: run the store on the Android emulator (Debug, `test-store`), tap "9–12", and read the exception from logcat (and the crash store if wrangler's login is available). Write the stack here. If it is not the `PurchaseArea` move, re-scope items 2-3 before building.
- [ ] 2. Fix `PlacePurchaseArea` so moving `PurchaseArea` between `StoryList.Footer` and `StoreRoot` cannot fail on Android (for example: two separate purchase-area views, one in the footer and one at the bottom, each toggled with `IsVisible`, so no view changes parent). Both directions: list to empty, and empty back to list.
- [ ] 3. Age chips from the data: build the chips from the bands that hold a book (`StoreFilter` gets a pure `BandsWithBooks(items)`), rebuild them when the list loads or refreshes, hide "All ages" (and the whole chip row) when fewer than two bands have books, and fall back to "All ages" when the selected band is gone.
- [ ] 4. Author row: show it only when the current band holds two or more authors; clear a chosen author that is no longer offered (as today).
- [ ] 5. Change `ageMin`/`ageMax` in the 15 `content/dino/*/book.json` files to the table in Decision 2 (the four starters too: their store entries come from `StoreBuilder.StarterEntries`).
- [ ] 6. Get the new ranges into the live store: find out whether a `meta.json`-only change needs a book version bump (`tools/dino/upload-store-story.ps1` checks `story-v<version>.dino` is present), then rebuild each book's store folder (BookBuilder) and re-upload with `upload-store-story.ps1`. Read the Worker's list back and check every book's `ageMin`/`ageMax`.
- [ ] 7. Tests (`tests/Dino/DinoReader.Tests`): `BandsWithBooks` for no books, one band, two bands, a book with no range (shows under every band, so it must not make an empty band look full); `InBand` with the new `3-5`/`6-8` ranges puts each book under exactly one band.
- [ ] 8. A Maestro flow on the Android emulator: open the store, assert "9–12" is not shown, tap "5 & under" and "6–8" and check a book from each list (Freddy under "5 & under", Jack under "6–8" and not "5 & under"), tap "All ages" back; no crash.

## Verification

- On the Android emulator: the store shows "All ages", "5 & under" and "6–8" only; Jack and the Beanstalk is only under "6–8"; Freddy and the Slipper and The Lion and the Mouse are only under "5 & under"; switching chips never crashes (item 8).
- A Debug-only check that empties the list (an author and band with no book, or an empty test store) moves the purchase card without a crash (item 2).
- The user taps the store chips on their own Android phone after the release.

## Out of scope

- Changing the bands themselves (Google Play's 5 & under, 6-8, 9-12; Decision 116).
- Writing a 9-12 book.
- iOS and Windows were not reported crashing; the item-2 fix runs on every head, so a quick store tap on the iOS simulator is enough there.
