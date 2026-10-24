# Slice 108 — Dino: a cast per reading language

*In progress (wave 2026-10-24), draft PR.* *Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
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

#### Design (Dino Reader Batch 22-23 mock, frame 40, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 22-23.dc.html`, frame 40 (lines 109-126). One real-UI frame (360x760 phone frame, 6px `#2b2540` border, radius 34.); voice names ("Farsi voice 2" to "4") are placeholders until the real Farsi pack names are wired. The mock agrees with the plan.

- **Header (108-3).** The page is localized: title "چه کسی حرف می‌زند" (Farsi for "Who's speaking") 19/600 RTL, subtitle "The Tale of Peter Rabbit" 12 `#8a8399`.
- **Language chip.** Under the header, pill 32 high, radius 16, fill `#ECE7FF`, text `#3f3bc4` 13/600, a small globe icon (15) and "Voices for فارسی". It says whose cast is being edited.
- **Narrator row (read-only).** White card, radius 18, 60 high: 34 circle `#2b2540` with a white icon, "Narrator · Ganji Adabi" 15/600, link "Change for all books" 13 `#5B57E8` (24 tall tap target). This opens the Narrator page (Slice 109). The per-book picker and its notice are gone.
- **Character rows.** White card, radius 18. Per row: 34 avatar circle with the initial (colours `#4FC060`, `#FF7A9C`, `#B8A6FF`, white 600), name 15/600, word count 12 `#8a8399`, a 36 play circle `#ECE7FF`. Badges 10.5/700: **MAIN** (`#5B57E8` on `#ECE7FF`) and **EDITED** (`#9a5b00` on `#FFF0C2`), radius 6. Below: a Boy / Girl / ? segmented control (radius 9, track `#F3EFE6`, 28 high segments, selected = white with a hairline shadow) and the voice picker (32 high, radius 10, `#F3EFE6`, 13).
- **Merge.** Outlined pill "Merge two characters", 48 high, radius 24, 2px `#c9c0f5`, text `#5B57E8` 15/600.
- **Behaviour (note).** App in Farsi, book in English, translate on: the cast lists Farsi voices. EDITED pins are kept per language; switching the app back to English shows the English cast untouched. Merges are shared across languages.

The mock settles: the language chip is a pill at the top, not a heading; the narrator row is a card above the characters. Icons in the mock are inline SVG strokes (2.2 wide, `#5B57E8`), not Phosphor names; the app uses its own `IconKind` set.

New strings (all 14 locales): "Voices for {0}" (the chip).

## Work

- [ ] 1. Core: per-language cast on `Story`, `StoryJson` read/write with the old-cast migration,
  `CastEditor` operating on one language's cast; unit tests (round trip, migration, merge shared, pins
  per language).
- [ ] 2. `StoryVoicing`: `Prepare`/`Cast`/`Remapped` use the reading language's cast, auto-casting and
  saving it on first use.
- [ ] 3. `CastPage`/`CastRows`: voice list and gender from the reading language, the language label as a "Voices for {language}" chip under the header (frame 40), and
  edits saved into that language's cast.
- [ ] 4. `.dino` package and backup round trip carry every language's cast.
- [ ] 5. Rules in `src/DinoReader.Core/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: an English book, app in Farsi, open the cast screen: Farsi voices with a Farsi label;
  pin one, read it, the pinned voice speaks; switch the app to English: the English cast is unchanged.
- An old backup from before this slice restores and reads.
