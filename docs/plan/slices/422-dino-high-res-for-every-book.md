### Slice 422 — Dino: a high-res package for every store book, the four old "starter" books too
**Product:** Dino

*Planned 2026-11-20 at the user's request ("yes" to building it, after they found only 12 of 16 books had a high-res package).*

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

- [ ] Remove the `BookKind.Store` gate on the high-res set in `BookHash`, `HighResPack` (problems and unpulled checks) and wherever the starter books are built and published, so a starter with a complete set builds and uploads `story-v<n>.hd.dino` and lists `hdBytes` in `meta.json`.
- [ ] Find the path that builds and uploads the four starter books to the store today (they are live on R2) and make it produce the high-res package the same way `publish-store` does, including BookBuilder `check`'s proof that both packs carry the same entries, manifest and story.json.
- [ ] Run `stamp` for the four books so their versions and `contentHash` match the new hash; `check` passes.
- [ ] Tests: a starter with a complete high-res set gets a high-res pack and its set is in the hash; one with an incomplete set gets neither (replace the tests in `HighResPackTests.cs` that assert starters never get one).
- [ ] `worker/CLAUDE.md`: the high-res paragraph says "a book" with a complete set, not "a store book".

## Verification

- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` green.
- After merge, the `dino-store.yml` run uploads `story-v<n>.hd.dino` for the four books, and `/api/stories` lists `hdDownloadUrl` and `hdBytes` for all 16 books.
- The four packages are downloaded into `D:\high-res-books` beside the other 12 (the user's original ask).
