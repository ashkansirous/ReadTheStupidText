# Slice 256 — Dino: `dump-sentences` works for picture books (Good Enough), so it gets Gemini audio
**Product:** Dino

*Built 2026-11-04 (items 1-2; item 3 render not run), merged as PR #782 (5630087).* (Planned 2026-11-03.) Dino Reader tooling only. Found while casting the store books for Slice 246: `dotnet run --project
tools/DinoReader.BookBuilder -- dump-sentences good-enough --lang fa` exits 1 with "Good Enough has 1 pages but 10
pictures". The dump reads only `story.md`; Good Enough is a picture book whose pages come from `lines.json`
(`Books.cs`: "null for a picture book whose pages come from lines.json"). Without a dump its cast
(`content/dino/good-enough/audio-cast.json`, merged in #767, one speaker Hazelnut) cannot be checked and its Farsi
audio cannot be rendered.

**Touches:** `tools/DinoReader.BookBuilder/SentenceDump.cs`, `tools/DinoReader.BookBuilder/Dumps.cs`,
`tools/DinoReader.BookBuilder/Books.cs`, `tests/DinoReader.Tests/**` (BookBuilder tests), `content/dino/good-enough/audio-cast.json`, `tools/premade-audio/generate.py` (only if item 2 needs it), `src/DinoReader.Core/**` (read-only reuse of the segmenter; edit only if it must be made reachable), `docs/plan/slices/256-good-enough-sentence-dump.md`.

## Work

- [x] 1. `dump-sentences` takes a `lines.json` picture book: the pieces the app reads aloud, in the same order and with the same
  index, speaker and narrator fields as the app's reader uses for that book (reuse the app's own segmenter path, as
  `SentenceDump.cs` does for `story.md` books). Test: Good Enough in en and fa.
- [x] 2. `python -I tools/premade-audio/generate.py --sentences <dump> --cast content/dino/good-enough/audio-cast.json --dry-run`
  passes in fa and en; fix the cast's speaker name if the dump differs.
- [ ] 3. (not run: paid render, command under *Built*) Render Good Enough in Farsi with `generate.py --batch` (Slice 246's batch mode) into
  `D:/ReadTheStupidText-worktrees/s246-audio/books-good-enough/`, then a short listening page for the user (Hazelnut's voice
  Autonoe was never heard).

## Verification

- BookBuilder tests for the picture-book dump; the dry run; the batch render ending with 0 left.

## Decisions (user, 2026-11-03)

- Hazelnut's Farsi voice: **Leda** (listened against Autonoe, Laomedeia, Zephyr; page https://claude.ai/artifact/51c1ZpE8eDU5ESkr6BTKVf). Written into `content/dino/good-enough/audio-cast.json`. Item 3's listening page is done.

## Built (night run 2026-11-04)

- Items 1 and 2 done. `SentenceDump.StoryOf` (new, in `SentenceDump.cs`) returns the store-built story for a `story.md` book and, for a book with no `story.md` (Good Enough), the story the starter builder packs from `lines.json` (`StarterBuilder.BuildGoodEnoughAsync` in a scratch library + `WithTranslations`), line boxes included, so the break map and sentence index are the app's. `Build` is unchanged. Tests: `tests/DinoReader.Tests/PictureBookSentenceDumpTests.cs` (en and fa).
- Good Enough dumps 19 pieces in en and fa (17 narrator, 2 Hazelnut); the cast's speaker name `Hazelnut` matches, no cast change. `generate.py --dry-run` passes in fa and en ("19 need a request").
- Decision (unattended): the picture-book path is taken when there is no `story.md`, and then only for the `good-enough` stem (`SentenceDump.SourceOf`); any other book without `story.md` throws a clear error. A later `lines.json` book needs its own builder call added there.
- Item 3 is NOT run (paid Gemini render). Command for the user's PC, with `GEMINI_API_KEY` set, from the repo root:

```
dotnet run --project tools/DinoReader.BookBuilder -- dump-sentences good-enough --lang fa --out tmp/good-enough-fa.json
python -I tools/premade-audio/generate.py --sentences tmp/good-enough-fa.json --cast content/dino/good-enough/audio-cast.json --out D:/ReadTheStupidText-worktrees/s246-audio/books-good-enough --batch
```

  Rerun the second line until it reports 0 left (exit code for "batch pending" means state is kept). Hazelnut's voice is Leda (decision above); no listening page is needed again.

From review (files outside the repo, all read):

- High (`s256-high.md`, 5 findings): 1 applied (picture-book path only for `good-enough`, other no-story.md books throw: `SentenceDump.SourceOf`). 2 applied (tests pin the fa dump and the counts). Skipped, low: 3 (the picture-book dump copies page JPEGs into a scratch library and blocks with `GetAwaiter().GetResult()`); 4 (built-note numbers had no backing test, exit code unnamed); 5 (class summary and usage text still say "store book").
- Medium (`s256-medium.md`, 4 findings): 1 applied (same as high 1). 2 applied (unknown-book test covers the guard). Skipped, low: 3 (`StoryOf` blocks on async); 4 (weak Farsi assertion, content-tied counts, scratch library rebuilt per test).
- Check pass (`s256-check-medium.md`, 6 findings, all low, all skipped): 1 `StoryOf` blocks via `GetAwaiter().GetResult()`; 2 a full scratch build per call, five calls in tests; 3 path hard-coded to `good-enough` (documented as deliberate above); 4 `SourceOf`/`DumpSource` real Text-is-null detection untested (tests use a synthetic `hasStoryMd` flag); 5 the Farsi assertion fails if a legit name or sound word stays Latin; 6 tests hard-code live content facts (19 pieces, 17/2 split, first sentence).

#### For CLAUDE.md (lifted 2026-11-04)

- `dump-sentences` handles a book without `story.md` only for `good-enough` (`tools/DinoReader.BookBuilder/SentenceDump.cs`, `SourceOf`/`StoryOf`); any other such book throws. A later `lines.json` book needs its own builder call added there. (Slice 256: docs/plan/slices/256-good-enough-sentence-dump.md)

#### Not yet verified

- The dump index matches the app's sentence index on a device during premade-audio playback (Slice 246 C).
- Item 3: Good Enough Farsi batch render (paid Gemini, needs `GEMINI_API_KEY`) not run; command above.
