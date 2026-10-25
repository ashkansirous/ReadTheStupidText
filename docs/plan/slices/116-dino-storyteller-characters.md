# Slice 116 — Dino: storyteller characters in every language

*In progress (wave 2026-10-25), draft PR.* *Planned 2026-10-25*, from the user's ask to "create multiple characters, auto assign them yourself,
then allow people to change the character… be creative with the voice… create enough characters for
all languages". Dino Reader only; RTST is not touched.

**Batch:** 24 (storyteller characters)
**Depends on:** Slice 115 (storyteller voices by default)
**Touches:** `src/DinoReader.Core/StorytellerCharacter*.cs` (new), `src/DinoReader.Core/CharacterRoster*.cs` (new), `src/DinoReader.Core/VoiceCaster.cs`, `src/DinoReader.Core/CastEditor.cs`, `src/DinoReader.Core/Cast*.cs`, `src/DinoReader.Core/FlavourGuess.cs`, `src/DinoReader.Core/FlavourWords.cs`, `src/DinoReader.Core/LanguageRules.cs`, `src/DinoReader.Core/*Rules.cs`, `src/DinoReader.Core/NarratorSetting.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/ReadTheStupidText.Domain/Reading/VoiceFlavour.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/CastText.cs`, `src/DinoReader.App/CastFlavourMenu.cs`, `src/DinoReader.App/NarratorPage.cs`, `src/DinoReader.App/NarratorChoice.cs`, `src/DinoReader.App/VoicePreview.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`

## Why

The cast screen shows raw engine voices with names from an adult anime (Supertonic: Albedo, Shalltear,
Demiurge, Momonga…; `SupertonicVoiceTable.cs:20-30`) or plain Kokoro names, and a separate "Sounds"
row (Slice 113). Children's stories want characters: Tiny Mouse, Grandpa Bear, Sleepy Owl.

## Decisions (2026-10-25, agreed with the user)

- **Decision 122: Dino's cast is a list of storyteller characters, the same idea in every language.**
  A character is a voice plus a flavour (Slice 113's pitch and pace, with room for new flavours such
  as Squeaky, Grumpy, Whisper, Giant) plus a kid-friendly name written for that language (Farsi
  "موش کوچولو", not "Tiny Mouse"). About 16–20 characters per language, including a few narrators.
  Underneath, each language uses its best voice: Kokoro where the storyteller pack serves it, else
  Supertonic-3 or the language's Piper/Melo pack. Engine and raw voice names are never shown;
  the Overlord names are gone from Dino's UI.
- **Dino casts by itself.** Each story character gets the roster character that best fits its name
  (Slice 113's guess words, gender, and "little"/"baby"/"grandad" words: a little sheep becomes Tiny
  Lamb), no two speaking characters share one when the roster allows. A grown-up swaps a character on
  the cast screen through a picker sheet (name, plain words, play button, Girls/Boys/Everyone filter,
  frame 53); the swap is pinned per language, as
  today. The separate "Sounds" row is replaced by the character pick.
- The narrator picks from the roster's narrator characters (Slice 109's one narrator per language).
- Stored casts (voice id + flavour) map to the nearest character on first read; no story file
  format change beyond an optional character id.

#### Design (Dino Reader Batch 24 mock, frames 52-55, added 2026-10-25)

Source: `docs/design/dino-reader/Dino Reader Batch 24.dc.html`, section "Storyteller characters" (frames 52, 53, 54, 55). Four real-UI frames (360x760 phone, 6px `#2b2540` border, radius 34). The mock agrees with the plan on one character per story character, plain names, auto-cast and a narrator list. It adds a picker sheet with a gender filter and settles the row layout. Icons are inline SVG strokes, not Phosphor names (own `IconKind` set). All character names and the Farsi names are drafts (the mock says so; Farsi for a native speaker to check). Supersedes frame 48 and the voice dropdown of frame 40.

- **Cast row (116-1, frames 52 and 54).** Each story character: a 34 round colour badge with the initial, name 15 / 600 (MAIN `#ECE7FF` / `#5B57E8` or EDITED `#FFF0C2` / `#9a5b00` chips, 10.5 / 600, radius 6), "N words" 12 `#8a8399`; below, indented 44, a character picker (40 high, radius 10, `#F3EFE6`, 14 / 600, chevron 12) and a 40 round `#ECE7FF` play button. No "Boy · voice" subtitle and no Sounds row. An EDITED row's picker has a 2px `#5B57E8` ring and white fill (The Wolf = "Grumpy Troll"). Cast shown: Little Pig "Cheeky Piglet" (MAIN), The Wolf "Grumpy Troll" (EDITED), Mother Pig "Kind Mummy". Rows sit in one white card (radius 18). Narrator row on top: 34 round `#2b2540` book badge, "Narrator · Story Granny" 15 / 600 and "Change for all books" 13 / 600 `#5B57E8`. "Merge two characters" stays at the bottom (48 high, radius 24, 2px `#c9c0f5` border), as `Cast_MergeButton`.
- **Character picker sheet (116-1, frame 53).** Bottom sheet 640 high over a dimmed screen, white, top radius 28. Title "Who plays The Wolf?" 20 / 600, subtitle "For this book, in English" 13 `#8a8399`. A 3-way segmented filter "Girls" / "Boys" / "Everyone" (`#F3EFE6` track, radius 12, items 38 high, selected white with shadow); it starts on the story character's gender and replaces the old boy/girl toggle. Rows 60 high: name 15 / 600 and a few plain words 12 (for example "gruff and low"), a 44 round `#ECE7FF` play button; the selected row has fill `#ECE7FF`, name `#3f3bc4` and a check (20, stroke 3). A character already used in this book shows "· Little Pig" after its words ("small and quick · Little Pig"); picking it anyway is allowed. Tapping a row picks it, closes the sheet and pins it (EDITED) in this language. Boys shown: Grumpy Troll "gruff and low", Friendly Giant "big, deep and slow", Grandpa Bear "deep and kind", Sly Fox "quick and whispery", Brave Knight "bold and clear", Cheeky Piglet "small and quick", Sleepy Owl "slow and soft", Squeaky Mouse "tiny and high".
- **Farsi cast (116-3, frame 54).** Same layout as frame 52, right-aligned: title in Vazirmatn 19 / 700 (Farsi for "Who is speaking"), subtitle in Latin, a "Voices for" + Farsi language-name chip (32 high, `#ECE7FF`), picker names in Vazirmatn 700: Peter = Cheeky Bunny, Mrs. Rabbit = Kind Mummy, Mr. McGregor = Grumpy Gardener, each written in Farsi; narrator in Farsi ("Story Granny"). Replaces frame 40's "Farsi voice 2/3/4". The exact Farsi strings are in the mock (frame 54, lines 128-134) and are drafts for a native speaker.
- **Narrator page (116-4, frame 55).** Same page as frame 39. Header "Narrator" 20 / 600 with "The storyteller voice for every book" 12. Per language a white card (radius 18): the language name (16 / 600; "English" with a "MY LANGUAGE" chip 11 / 600 and a 2px `#c9c0f5` ring on the own-language card), then a narrator picker (44 high, radius 12) with a 44 play button. English is shown open: a `#F3EFE6` list with "Story Granny" "warm and gentle" (selected, check 18), "Story Grandad" "deep and cosy", "Bedtime Whisperer" "soft and slow" (rows 52 high, play 44). Others closed: Farsi and Spanish ("Abuela Cuentacuentos"). Narrator characters appear only here, not in the character picker.
- **Simplified in the frames.** The dimmed screen behind the picker is an empty rounded box. Word counts and MAIN/EDITED states are sample data.

**Open point: "For this book" subtitle.** The picker subtitle says "For this book, in English", but the plan (and Slice 113) pin a pick per language, and the mock's own caption says "pinning it (EDITED) in this language". **Default:** pins stay per language as today; keep the subtitle as drawn but build it from its own string so the copy can change to "In English" if the user prefers (as drawn it promises a per-book scope the code does not have).

**Open point: filter default for unknown gender.** The mock says the filter "starts on the character's gender" but shows no case without one. **Default:** "Everyone".

**Open point: used-by hint.** The plan says no two speaking characters share one when the roster allows (auto-cast only); the mock lets a parent pick a used one and labels it. **Default:** allow it on manual picks, label with the story character's name, as drawn.

Settled by the mock: the plan's "list with a play button per character" is a bottom sheet; there is a gender filter, a plain-words description per character (a second string per character), a used-by label, and three English narrators with descriptions. The plan's 16-20 characters per language is not shown (the mock lists 8 in the Boys filter).

New strings (all 14 locales): picker title "Who plays {name}?", subtitle "For this book, in {language}", filters "Girls", "Boys", "Everyone", "The storyteller voice for every book", and per character a name plus a plain-words description (English drafts above; names written in each language). Check for existing keys: "MY LANGUAGE", "Voices for {language}", "Narrator", "Change for all books", "Merge two characters" (`Cast_MergeButton`), "Who's speaking".

## Work

- [ ] 1. Smallest end to end, English: the roster type in `DinoReader.Core` (id, voice id, flavour,
  gender, age/size tags, localized name key), 16–20 English characters on Kokoro, `VoiceCaster` casts
  from it, the cast screen shows one character picker plus play button per story character (frame 52) and a picker sheet with the Girls/Boys/Everyone filter that starts on the character's gender, plain-word descriptions, a "· Little Pig" used-by label and tap-to-pick (frame 53; replaces the boy/girl toggle). Unit tests: casting from
  names, no duplicates, swap pins.
- [ ] 2. New flavours in `VoiceFlavour` (Squeaky, Grumpy, Whisper, Giant or similar; keep pitch within
  the range the listening check allows) and the per-language guess words that pick them.
- [ ] 3. Rosters for the other 13 languages, each on that language's best engine (Supertonic-3 for
  fr/es/pt/ru/ja/ar/da/fi/sv/nl, `piper-fa` for Farsi, `piper-nb` for Norwegian, `melo-zh-en` for
  Chinese), with names written in each language.
- [ ] 4. Narrator characters on the narrator screen (three English narrators with descriptions, frame 55; Farsi names written in Farsi, frame 54); migrate stored casts and narrator picks to
  characters; remove the "Sounds" row and Supertonic names from the UI.
- [ ] 5. Strings in all 14 `.resx` files; rules in `src/DinoReader.Core/CLAUDE.md` and
  `src/DinoReader.App/CLAUDE.md` (For CLAUDE.md block).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (rosters complete for all 14 languages,
  every roster voice exists in its engine table, casting tests).
- Android emulator: an English and a Farsi starter auto-cast with sensible characters; swapping one
  is heard on the next read and kept after a restart; no engine or anime name anywhere in Dino.
- The user listens to the English roster and picks favourites; drop characters that sound broken.
