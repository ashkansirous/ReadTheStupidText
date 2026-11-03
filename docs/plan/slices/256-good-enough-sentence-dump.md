# Slice 256 — Dino: `dump-sentences` works for picture books (Good Enough), so it gets Gemini audio

*Planned 2026-11-03.* Dino Reader tooling only. Found while casting the store books for Slice 246: `dotnet run --project
tools/DinoReader.BookBuilder -- dump-sentences good-enough --lang fa` exits 1 with "Good Enough has 1 pages but 10
pictures". The dump reads only `story.md`; Good Enough is a picture book whose pages come from `lines.json`
(`Books.cs`: "null for a picture book whose pages come from lines.json"). Without a dump its cast
(`content/dino/good-enough/audio-cast.json`, merged in #767, one speaker Hazelnut) cannot be checked and its Farsi
audio cannot be rendered.

**Touches:** `tools/DinoReader.BookBuilder/SentenceDump.cs`, `tools/DinoReader.BookBuilder/Dumps.cs`,
`tools/DinoReader.BookBuilder/Books.cs`, `tests/DinoReader.Tests/**` (BookBuilder tests), `content/dino/good-enough/audio-cast.json`.

## Work

- [ ] 1. `dump-sentences` takes a `lines.json` picture book: the pieces the app reads aloud, in the same order and with the same
  index, speaker and narrator fields as the app's reader uses for that book (reuse the app's own segmenter path, as
  `SentenceDump.cs` does for `story.md` books). Test: Good Enough in en and fa.
- [ ] 2. `python -I tools/premade-audio/generate.py --sentences <dump> --cast content/dino/good-enough/audio-cast.json --dry-run`
  passes in fa and en; fix the cast's speaker name if the dump differs.
- [ ] 3. Render Good Enough in Farsi with `generate.py --batch` (Slice 246's batch mode) into
  `D:/ReadTheStupidText-worktrees/s246-audio/books-good-enough/`, then a short listening page for the user (Hazelnut's voice
  Autonoe was never heard).

## Verification

- BookBuilder tests for the picture-book dump; the dry run; the batch render ending with 0 left.

## Decisions (user, 2026-11-03)

- Hazelnut's Farsi voice: **Leda** (listened against Autonoe, Laomedeia, Zephyr; page https://claude.ai/artifact/51c1ZpE8eDU5ESkr6BTKVf). Written into `content/dino/good-enough/audio-cast.json`. Item 3's listening page is done.
