# Slice 156 — Dino: the book chooses each character's voice, not the app

*In progress: code built and merged in PR #438 (2026-10-27); the Every book writer pass is still to do.* Dino Reader only.

**Batch:** 33 (2026-10-26; Decision 161)
**Touches:** `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryCast.cs`, `src/DinoReader.Core/CastCharacters.cs`, `src/DinoReader.Core/CastEditor.cs`, `src/DinoReader.Core/CharacterTraits.cs` (new), `src/DinoReader.Core/CharacterRoster*.cs`, `src/DinoReader.Core/StoryPackage*.cs`, `src/DinoReader.Core/StarterUpdate.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/StoreTexts.cs`, `tests/DinoReader.Tests/StoreBuilder.cs`, `tests/DinoReader.Tests/StarterTexts.cs`, `tests/DinoReader.Tests/StarterBuilder.cs`, `tests/DinoReader.Tests/**`, `docs/design/story-art/*.md`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `.claude/agents/dino-story-writer.md`, `.claude/agents/dino-story-reviewer.md`, `.claude/skills/create-dino-story/SKILL.md`

## Why

The app picks every character's voice when a book opens (`CastCharacters.Cast`, `src/DinoReader.Core/CastCharacters.cs:43`).
It knows only a guessed gender and a word-list flavour (`FlavourGuess`), so it cannot tell how a seagull or a pirate should
sound. Store books are cast at build time by the same picker (`tests/DinoReader.Tests/StoreBuilder.cs:10`) and saved with
`Edited = false`, so the app may recast them, and no store voice was ever chosen on purpose. Example from the user
(2026-10-26), "X Marks the Treasure!": the Seagull got an awful voice; **Splashy Duckling** (`duckling2`) fits it, and
**Wiggly Puppy** (`puppy2`) fits Billy better. Whoever writes the story knows its characters; the app can't.

## Decisions locked with the user (Decision 161)

1. **Each character in a `.dino` book carries traits and a suggested voice.**
   - **Traits** stay correct even when voices change: age (`baby`/`child`/`young`/`adult`/`old`), gender, kind (an animal,
     `person`, or a fantasy kind such as `pirate`, `troll`, `robot`) and voice qualities (`squawky`, `bouncy`, `gruff`,
     `soft`, `booming`, …). The vocabulary is a closed set of enums in `CharacterTraits`, not free text.
   - **Suggested voice** is a roster id (e.g. `duckling2`), not a display name and not an engine voice id. Roster ids are
     the same in every language.
   - All new fields are optional, so `schemaVersion` stays 1 and older books and apps still open each other's packages.
2. **The app uses the suggestion when it can, and otherwise matches the traits.** If the suggested roster id is missing
   in that language (Chinese has 10 characters), was renamed or removed, or is a typo, the app picks the roster character
   whose traits are closest. So every roster character gets the same trait words (Splashy Duckling = young, female,
   bird-like, bouncy). A bad suggestion can never break a book.
3. **An author's pick is a third kind of voice**, next to "picked by the app" and "changed by a grown-up". The runtime
   picker never recasts it. A grown-up's change always wins. The cast page offers **"Back to the book's voice"** on an
   edited character whose book has a suggestion.
4. **Every store and starter book gets traits and a suggested voice now.** The `dino-story-writer` picks them, the
   `dino-story-reviewer` checks them, and each book is version-bumped. Starters update through `StarterUpdate`; store
   books are re-uploaded after the user's OK (uploading to R2 needs approval).
5. **Out of scope:** books a parent adds (photos, PDFs, typed). They keep today's picker.
6. **Builds on Slice 152.** Slice 152 makes the book's `Animal` field the only source of a character's animal. This slice
   reads that field as the "kind" trait and does not add a second animal field. 152 merges first; this slice follows.

## Work

- [x] **Smallest end-to-end: the Treasure book.** Add `CharacterTraits` (closed enums) and the optional `Traits` and
      `SuggestedVoice` fields on the cast entry in `story.json`, plus a `VoiceSource` (`App`/`Book`/`Grownup`) that
      replaces the meaning of `Edited` (old packages: `Edited = true` reads as `Grownup`). `StoryCast`/`CastCharacters`
      keep a `Book` member as long as its roster id exists in that language. Give "X Marks the Treasure!" traits and
      suggestions (Seagull → `duckling2`, Billy → `puppy2`, the rest chosen by the writer), bump it, build it.
      Tests: a `Book` member survives a recast and a narrator change; an old package with `Edited` still loads as before.
- [x] **Trait fallback.** Trait words on every roster character in every language (`CharacterRoster*.cs`). When a
      suggestion is missing in a language, `CastCharacters` picks the closest by traits (kind, then age, then gender,
      then voice qualities), avoiding repeats as today. Tests: a missing id, a typo and a Chinese book each get a
      trait-matched voice; a test fails any roster character without traits.
- [x] **Grown-up control.** `CastEditor` sets `Grownup`; "Back to the book's voice" restores `Book` (`CastPage.cs`,
      `CastRows.cs`). New string in all 14 locales.
- [x] **Authoring.** `StoreBuilder`/`StarterBuilder` write traits and suggestions from the story spec instead of running
      the app's picker. A test fails any store or starter character without traits or with an unknown suggestion.
      `dino-story-writer` must give every character traits and a suggestion (recorded in the book's art record);
      `dino-story-reviewer` checks the choices fit the character.
- [ ] **Every book.** Writer pass over all starter and store books (one PR per few books, skipping books with open PRs
      and picking them up after they merge). Bump each version. Rebuild the starters. Re-upload the store books after
      the user's OK, then compare each `meta.json` with `/api/stories`.
- [x] Docs: the casting rules in `src/DinoReader.Core/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md`.

## Built, and what is still open

- Built: the traits and suggestion fields, the `Book` voice source, the trait fallback, "Back to the book's voice", the authored-voice builders and the docs. The first five Work items are done.
- **Not yet verified / open:** "Every book" is not done. Only X Marks the Treasure! carries a suggestion and traits (`StarterVoices.AuthoredBooks`); every other book is cast by the app exactly as before in every
  language (checked against the base commit for all 14 languages). A book is added to `AuthoredBooks` only when the `dino-story-writer` pass has really chosen each character's voice and traits (the
  env-gated `StarterVoicesDump`, `DINO_DUMP_VOICES=1`, gives a starting point); `AuthoredVoiceTests` then requires traits and a known, unshared suggestion for each of its characters.
  The Slice 152 animal restorations (Ugly Duckling, Dilly, Little Red Hen's Cat and Duck, Mama Bear, the mice) were dropped again: a suggestion is one roster id for all languages, and keeping
  today's gender-matched voices in every language needs per-language choices, so they stay stored as no animal until their books are authored.
- **Not yet done at ship time:** `StarterVersions` and the store specs' versions are not bumped yet (done at ship, after the open Farsi PRs merge), then the starters are rebuilt (`DINO_BUILD_STARTERS=1`)
  and the store books re-uploaded after the user's OK.
- **Accepted:** an older Dino build that opens a new package and saves any edit rewrites `story.json` without the suggestion and traits, so that book's authored voices are lost for good
  (a store book is not refreshed from the bundle). Nothing is done about it.
- Treasure store book v4 is built, not uploaded.

*Changed while building:* `CharacterTraits` are closed enums; cast members gain optional `SuggestedVoice`/`Traits`; the voice source is derived (`Edited` = `Grownup`), `schemaVersion` stays 1. A `Book` member is kept through recasts and narrator changes, never takes the narrator's voice, and a duplicate suggestion falls back to traits (gender outweighs kind). "Back to the book's voice" uses `CastEditor.OffersBookVoice` and `CastCharacters.BookableSpeakers`. Builders write authored data from `StarterVoices`; `StarterVoices.AuthoredBooks` lists books with real choices (only X Marks the Treasure!: Seagull `duckling2`, Billy `puppy2`; Kathryn, Izzy and Polly are Claude picks to confirm). The starter refresh recasts kept members whose book data changed and clears dropped data. The Slice 152 animal restorations (Duckling/Dilly, Cat, Duck, Mama Bear, mice) failed 24 per-language gender tests and were reverted.

*From review:* `reviews\156-high.md` findings 2-7 and 11, `reviews\156-medium.md` findings 1-5 and 9 fixed.

**Writer pass still to do, per book:** traits and suggestions in every language without changing gender-matched voices (one roster id may not fit every language), add the book to `AuthoredBooks`, bump the version, reviewer check.

*Verified 2026-10-27, emulator headless:* Treasure Seagull/Billy voices, change and back to the book's voice, persistence; starters unchanged and show no link: PASS. Code-level compare: every non-Treasure cast identical in 14 languages.

#### For CLAUDE.md (lifted 2026-10-27)

- Dino casting: a book's `SuggestedVoice` is one roster id for all languages, so a book enters `StarterVoices.AuthoredBooks` only after a writer pass gives every character traits and a known, unshared suggestion that keeps gender-matched voices in every language (`src/DinoReader.Core/CLAUDE.md`). (Slice 156: docs/plan/slices/156-dino-authored-character-voices.md)
- A `Book`-sourced cast member is never recast by the picker and never takes the narrator's voice; `Edited` reads as `Grownup` (`src/DinoReader.Core/CastCharacters.cs`). (Slice 156: docs/plan/slices/156-dino-authored-character-voices.md)

#### Not yet verified

- Chinese Treasure book on device.
- Windows head and iOS.
- Native read of the new "Back to the book's voice" string.
- Store upload of Treasure v4 (needs the user's OK).
- An older build saving an edit drops `SuggestedVoice`/`Traits` (accepted).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Emulator, English: open "X Marks the Treasure!". The Seagull speaks as Splashy Duckling and Billy as Wiggly Puppy.
  Change the Seagull's voice; it stays changed after reopening; "Back to the book's voice" restores Splashy Duckling.
- Emulator, Chinese: the same book opens with trait-matched voices and no error.
- The user listens to two or three updated books on the phone and confirms the voices fit.
