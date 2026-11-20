### Slice 422 — Dino: a high-res package for every store book, the four old "starter" books too
**Product:** Dino

*Planned 2026-11-20 at the user's request ("yes" to building it, after they found only 12 of 16 books had a high-res package).*

*Built 2026-11-20 (uncommitted, awaiting review):* the `BookKind.Store` gate is gone from `BookHash`, `HighResPack` (broken-pointer and unpulled checks); `build` now writes each starter's committed `.dino` first, then its `story-v<n>.hd.dino` and `hdBytes` (a starter with an unpulled or bad high-res set is named and skipped, like a store book); `check` proves the starters' pack pair (`StarterPackPairProblemAsync`) and checks every book's picture sizes. Good Enough's standard pack now carries its back cover (`BuildAllAsync` never copied it, so the two packs listed different files). Good Enough's `high-res/` were the raw 2.4-3.3 MB downloads, so they were re-saved in place with `prepare-story-pictures.ps1 -HighRes` (JPEG 85, 1792x2400). Versions after `stamp` and `build`: good-enough 14, gingerbread-man 15, three-little-pigs 19, tortoise-and-hare 15; the four committed `testbooks/*.dino` were rewritten; `check` says "All 16 books are current"; 4846 Dino tests pass. `publish-store-books.mjs` needed no change. `contact-sheet.jpg` is not counted by `Find` (it only reads page, cover and back-cover names), so it stays where it is. *Review pass 1 (`reviews/s422-high.md`):* `HighResPack.WriteAsync` now overwrites the file the story names (`story.Pages[i].ImageFile`), so Good Enough's `N.jpg` pages become print size (its hd pack is 8.97 MB, was 3.5 MB; same version 14) and it throws (`PictureBytesProblem`) unless every page, cover and back-cover entry equals its `high-res/` file and differs from the standard pack's, which `check` and `build` both run; the high-res picture check selects books by `story.md` or `lines.json` instead of the Good Enough stem; comments no longer say "store book only" or "bundled". Skipped #3 (`reviews/s422-high.md`): "`build` without `--live` ... skips the four starters and exits 2" - skipped by the caller as a nit. *Review pass 2 (`reviews/s422-medium.md`):* fixed #1 (a starter's package write goes through `TryWriteStarterPackageAsync`: named, half-written packages deleted, `failed[...]`) and #2 (`HighResPack.WriteAsync` throws "high-res page count differs" first; test added). Skipped #3 (`reviews/s422-medium.md`): "StarterPackPairProblemAsync temporarily swaps the static StoreBuilder.OutputFolder ... Not safe for parallel use" - skipped by the caller as a nit. Skipped #4 (`reviews/s422-medium.md`): "CheckHighResAsync loops over Books.All but runs PackPairProblemAsync only for store books" - skipped by the caller as a nit. Skipped #5 (`reviews/s422-medium.md`): "book.json was already stamped to a new version and hash ... committed testbooks/*.dino stays old" - skipped by the caller as a nit. Skipped #6 (`reviews/s422-medium.md`): "It does not say a starter's high-res pack is built by build (starter loop) and proved by check" - skipped by the caller as a nit.

**Touches:** `tools/dino/DinoReader.BookBuilder/**`, `tests/Dino/DinoReader.Tests/HighResPackTests.cs`, `tests/Dino/DinoReader.Tests/BookBuilderTests.cs`, `content/dino/good-enough/book.json`, `content/dino/gingerbread-man/book.json`, `content/dino/three-little-pigs/book.json`, `content/dino/tortoise-and-hare/book.json`, `.github/scripts/publish-store-books.mjs` (only if the starter path needs it), `worker/CLAUDE.md`, this file.

## The fault

No book ships inside the app (Slice 261): every book, on every device, comes from the store when the user picks it. Slice 375's plan was corrected on 2026-11-13 (`4506087f`) to say the two packs cover every book. Its build (`15828aa9`, 2026-11-14) still made the high-res package only for books whose `book.json` says `"kind": "Store"`:

- `tools/dino/DinoReader.BookBuilder/BookHash.cs:47` (high-res set counted in the hash only for `BookKind.Store`)
- `tools/dino/DinoReader.BookBuilder/HighResPack.cs:83` and `:89` (problems checked only for `BookKind.Store`)
- `tools/dino/DinoReader.BookBuilder/BookCommands.cs:29` (`publish-store` builds only `BookKind.Store` books)

The four books still tagged `"kind": "Starter"` (Good Enough, The Gingerbread Man, The Three Little Pigs, The Tortoise and the Hare) have a complete `high-res/` folder in git (every page, cover, back cover) but no `story-v<n>.hd.dino` on R2 and no `hdBytes` in their `meta.json` (read 2026-11-20: versions 13, 14, 18, 14). `/api/stories` therefore offers them no high-res download.

## Decisions

1. Every book with a complete `high-res/` set gets `story-v<n>.hd.dino` and `hdBytes`, whatever its `kind`. The `Starter` kind only means how the book is versioned (the `stamp` command and `contentHash`); it never decides picture size.
2. The `Starter` kind itself stays (StarterBuilder, StarterUpdate and their tests still use it); retiring it is out of scope.
3. A complete high-res set is part of each starter's content hash, as it already is for store books, so the four books' versions go up by one and the store run publishes them with both packs (never overwriting an existing key, per `publish-store-books.mjs`).
4. A stray non-page file in `high-res/` (`three-little-pigs/high-res/contact-sheet.jpg`) is not a picture of the book and is never packed; if the completeness check counts it, move it out of `high-res/` (it belongs beside the art record).
5. Code comments that call starters "bundled" or "never packed with high-res" are corrected: no book ships in the app.

## Work items

- [x] Remove the `BookKind.Store` gate on the high-res set in `BookHash`, `HighResPack` (problems and unpulled checks) and wherever the starter books are built and published, so a starter with a complete set builds and uploads `story-v<n>.hd.dino` and lists `hdBytes` in `meta.json`.
- [x] Find the path that builds and uploads the four starter books to the store today (they are live on R2) and make it produce the high-res package the same way `publish-store` does, including BookBuilder `check`'s proof that both packs carry the same entries, manifest and story.json.
- [x] Run `stamp` for the four books so their versions and `contentHash` match the new hash; `check` passes.
- [x] Tests: a starter with a complete high-res set gets a high-res pack and its set is in the hash; one with an incomplete set gets neither (replace the tests in `HighResPackTests.cs` that assert starters never get one).
- [x] `worker/CLAUDE.md`: the high-res paragraph says "a book" with a complete set, not "a store book".

## Verification

- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` green.
- After merge, the `dino-store.yml` run uploads `story-v<n>.hd.dino` for the four books, and `/api/stories` lists `hdDownloadUrl` and `hdBytes` for all 16 books.
- The four packages are downloaded into `D:\high-res-books` beside the other 12 (the user's original ask).
