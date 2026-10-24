# Slice 108 — Dino: a cast per reading language

*Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
reading language, one narrator, parent recordings, store filters" (2026-10-24) —
https://drive.google.com/file/d/1b2XPenCW2D4Kx1yx98js_LK4CRBSnnLf/view?usp=drivesdk (part 3, milestone 3).

**Batch:** 22
**Depends on:** Slice 65 (cast screen), Slice 95 (translation), Slice 107 (so own books actually translate)
**Touches:** `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/CastEditor.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`

## Why

Peter Rabbit (English) in a Farsi app: the cast screen offered only English voices. The cast is stored
once per story with voice ids from the story language's pack. `CastPage` (`CastPage.cs:166,266-285`) and
`StoryVoicing.Cast` (`StoryVoicing.cs:138,145`) use `VoicesFor(story.Language)`, and a translated read
remaps those voices onto the spoken language by gender (`StoryVoicing.Remapped`, `VoiceRemap.MapSet`,
`StoryVoicing.cs:73,208-213`). So a parent never picks the Farsi voices, and pins don't carry over.

## Decisions (2026-10-24, agreed with the user)

- **Decision 113: a separate cast per reading language.** `Story` keeps one cast per language code: the
  source-language cast plus one per language the book has been read in. A language's cast is auto-cast
  on the first read in that language; pins (`CastMember.Edited`) are kept per language; merges of
  characters are language-independent and shared. The cast screen lists the voices of the language the
  book will be read in ("my language" when `StoryTranslator.Applies`), with a small language label so the
  parent knows which cast they edit. Switching the app back to English restores the English cast. The
  gender remap stays only as the auto-cast seed for a language with no cast yet.
- `story.json` gains an optional per-language cast field; `schemaVersion` stays 1. The old single cast is
  read as the source-language cast, and old packages and backups still open.
- The Slice 66 rule holds: only `ReadingPage` switches `SpokenLanguage`, never the cast screen.

## Work

- [ ] 1. Core: per-language cast on `Story`, `StoryJson` read/write with the old-cast migration,
  `CastEditor` operating on one language's cast; unit tests (round trip, migration, merge shared, pins
  per language).
- [ ] 2. `StoryVoicing`: `Prepare`/`Cast`/`Remapped` use the reading language's cast, auto-casting and
  saving it on first use.
- [ ] 3. `CastPage`/`CastRows`: voice list and gender from the reading language, the language label, and
  edits saved into that language's cast.
- [ ] 4. `.dino` package and backup round trip carry every language's cast.
- [ ] 5. Rules in `src/DinoReader.Core/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: an English book, app in Farsi, open the cast screen: Farsi voices with a Farsi label;
  pin one, read it, the pinned voice speaks; switch the app to English: the English cast is unchanged.
- An old backup from before this slice restores and reads.
