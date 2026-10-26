# Slice 156 — Dino: the book chooses each character's voice, not the app

*Planned 2026-10-26.* Dino Reader only.

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

- [ ] **Smallest end-to-end: the Treasure book.** Add `CharacterTraits` (closed enums) and the optional `Traits` and
      `SuggestedVoice` fields on the cast entry in `story.json`, plus a `VoiceSource` (`App`/`Book`/`Grownup`) that
      replaces the meaning of `Edited` (old packages: `Edited = true` reads as `Grownup`). `StoryCast`/`CastCharacters`
      keep a `Book` member as long as its roster id exists in that language. Give "X Marks the Treasure!" traits and
      suggestions (Seagull → `duckling2`, Billy → `puppy2`, the rest chosen by the writer), bump it, build it.
      Tests: a `Book` member survives a recast and a narrator change; an old package with `Edited` still loads as before.
- [ ] **Trait fallback.** Trait words on every roster character in every language (`CharacterRoster*.cs`). When a
      suggestion is missing in a language, `CastCharacters` picks the closest by traits (kind, then age, then gender,
      then voice qualities), avoiding repeats as today. Tests: a missing id, a typo and a Chinese book each get a
      trait-matched voice; a test fails any roster character without traits.
- [ ] **Grown-up control.** `CastEditor` sets `Grownup`; "Back to the book's voice" restores `Book` (`CastPage.cs`,
      `CastRows.cs`). New string in all 14 locales.
- [ ] **Authoring.** `StoreBuilder`/`StarterBuilder` write traits and suggestions from the story spec instead of running
      the app's picker. A test fails any store or starter character without traits or with an unknown suggestion.
      `dino-story-writer` must give every character traits and a suggestion (recorded in the book's art record);
      `dino-story-reviewer` checks the choices fit the character.
- [ ] **Every book.** Writer pass over all starter and store books (one PR per few books, skipping books with open PRs
      and picking them up after they merge). Bump each version. Rebuild the starters. Re-upload the store books after
      the user's OK, then compare each `meta.json` with `/api/stories`.
- [ ] Docs: the casting rules in `src/DinoReader.Core/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Emulator, English: open "X Marks the Treasure!". The Seagull speaks as Splashy Duckling and Billy as Wiggly Puppy.
  Change the Seagull's voice; it stays changed after reopening; "Back to the book's voice" restores Splashy Duckling.
- Emulator, Chinese: the same book opens with trait-matched voices and no error.
- The user listens to two or three updated books on the phone and confirms the voices fit.
