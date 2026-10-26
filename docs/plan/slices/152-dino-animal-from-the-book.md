# Slice 152 — Dino: a character's animal comes from the book, never from its voice

*Planned 2026-10-26.* Dino Reader only.

**Batch:** 31 (tester review, 2026-10-26; Decision 157, reverses Decision 132)
**Touches:** `src/DinoReader.Core/AnimalSound*.cs`, `src/DinoReader.Core/CastCharacters.cs`, `src/DinoReader.Core/StoryCast.cs`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/SettingsRows.cs` (shared with Slice 150: sequence them), `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/StarterAnimals.cs`, `tests/DinoReader.Tests/StoreTexts.cs`, `tests/DinoReader.Tests/StarterBuilder.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `.claude/agents/dino-story-writer.md`

## Why

Characters must still make their sounds ("Meeeh!", "Baaa!"). Decision 132 / Slice 136 made the animal follow
the storyteller character a grown-up picks: once a member is edited, the animal is found from its voice and
flavour (`AnimalCharacters.Of`). So picking a voice changes what animal a character is, and a goat cast to the
Brave Knight goes silent. The user calls this a wrong decision (2026-10-26). The animal is a fact of the story,
not of the voice.

## Decisions locked with the user (Decision 157, reverses Decision 132)

- **Store books and starter books:** each character's animal comes only from the book's data: the `story.json`
  cast field `Animal`, a kind or `none`. Every character in these books carries it explicitly (no guessing).
  Changing a voice or character never changes the animal or its sound.
- **The animal is not shown in the UI.** Remove the cast page's naming of the animal through the nearest
  roster character, and any other place that shows it.
- The sound is still spoken in the character's own voice and flavour (Slice 117), so it changes with the voice;
  what the sound says does not.
- **Parent-provided books (photos, PDFs, typed): out of scope, unchanged** (they keep today's
  `Resolve`/`AnimalGuess` path).
- **Animal sounds becomes one app-wide grown-up setting, on by default** (Batch 31 mock, frame 70; adopted 2026-10-26).
  It leaves the cast page; the stored per-book `Story.AnimalSoundsOn` is ignored.

## Work

- [ ] `AnimalSounds.OfCast`: for books with an explicit animal on every member, use the stored animal only and
      ignore `Edited` and `AnimalCharacters.Of`. Other books keep today's code path. Tests: recasting a goat to the
      Brave Knight still bleats; recasting a pig to the wolf still oinks.
- [ ] Builders write an explicit `Animal` (kind or `none`) for every character of every starter and store book
      (`StarterAnimals.cs`, and the store equivalent). A test fails a starter or store book with a member that has no animal.
- [ ] Rebuild the 15 starters (the version bump makes installed copies update). Store books: bump and re-upload
      after the user's OK (memory: store uploads need approval). The books in open illustration PRs (#393, #394, #402,
      #413, #414, #415) get it when they merge.
- [ ] Cast page: no animal text anywhere, and no Animal sounds card (`CastPage.cs`).
- [ ] App-wide `AnimalSounds` preference (default on) in grown-up settings, below Reading speed (frame 70); `StoryVoicing`
      reads it instead of `Story.AnimalSoundsOn` (kept in the format, ignored). Strings in all 14 locales. Test: off silences every book.
- [ ] Docs: replace the "Which animal makes sounds (Slice 136)" sentence in `src/DinoReader.Core/CLAUDE.md`. The
      writer agent must give every character an animal or `none`.

## Verification

- `dotnet test tests/DinoReader.Tests`.
- Emulator (Billy Goats Gruff and the Ugly Duckling): recast every animal to a non-animal character and to another animal's character;
  each still makes its own sound in its new voice. The cast page shows no animal.

#### Design (Dino Reader Batch 31 mock, screens 69 and 70, added 2026-10-26)

Source: `docs/design/dino-reader/Dino Reader Batch 31.dc.html`, screen 69 (lines 105-120) and the Animal sounds row of screen 70 (lines 122-149). Two 360x760 frames, real UI. The mock agrees with Decision 157 on "no animal in the UI" and disagrees with the plan on where the Animal sounds switch lives.

- **Cast page, frame 69.** No animal line under any character and no Animal sounds card. The example: Big Billy Goat is recast to the Brave Knight and still bleats, now in the knight's voice (in store and starter books the animal comes from the book, never from the voice). Parent-made books keep today's guess, also unseen. The row layout is in Slice 151's Design subsection.
- **Animal sounds row, frame 70.** A white card, radius 18, padding 12/14, min-height 64, gap 12. Title "Animal sounds" 15/600. Sub-line "Animals say “Oink!” or “Baa!” now and then, in every book" 12.5 `#5a5470`, line-height 1.35. Switch at the right: 52x32, radius 16, on = fill `#5B57E8` with a 26 px white thumb at the right (3 px inset). Shown on in the frame. Sits between the Reading speed card and "Help make Dino better".
- **New strings (14 locales).** "Animal sounds" (title) and the sub-line above (a draft in the mock).

**Decided (2026-10-26): Animal sounds is app-wide.** The user asked for the plan to follow the Batch 31 mock, so the per-book switch on the cast page (`CastPage.cs:302`, read in `StoryVoicing.cs:142`) is replaced by one grown-up setting, on by default; the stored per-book value is ignored, as Slice 151 does for `WordsOverPictures`.
