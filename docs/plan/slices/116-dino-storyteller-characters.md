# Slice 116 — Dino: storyteller characters in every language

*Planned 2026-10-25*, from the user's ask to "create multiple characters, auto assign them yourself,
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
  the cast screen (a list with a play button per character); the swap is pinned per language, as
  today. The separate "Sounds" row is replaced by the character pick.
- The narrator picks from the roster's narrator characters (Slice 109's one narrator per language).
- Stored casts (voice id + flavour) map to the nearest character on first read; no story file
  format change beyond an optional character id.

## Work

- [ ] 1. Smallest end to end, English: the roster type in `DinoReader.Core` (id, voice id, flavour,
  gender, age/size tags, localized name key), 16–20 English characters on Kokoro, `VoiceCaster` casts
  from it, the cast screen shows character names with play buttons and swap. Unit tests: casting from
  names, no duplicates, swap pins.
- [ ] 2. New flavours in `VoiceFlavour` (Squeaky, Grumpy, Whisper, Giant or similar; keep pitch within
  the range the listening check allows) and the per-language guess words that pick them.
- [ ] 3. Rosters for the other 13 languages, each on that language's best engine (Supertonic-3 for
  fr/es/pt/ru/ja/ar/da/fi/sv/nl, `piper-fa` for Farsi, `piper-nb` for Norwegian, `melo-zh-en` for
  Chinese), with names written in each language.
- [ ] 4. Narrator characters on the narrator screen; migrate stored casts and narrator picks to
  characters; remove the "Sounds" row and Supertonic names from the UI.
- [ ] 5. Strings in all 14 `.resx` files; rules in `src/DinoReader.Core/CLAUDE.md` and
  `src/DinoReader.App/CLAUDE.md` (For CLAUDE.md block).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (rosters complete for all 14 languages,
  every roster voice exists in its engine table, casting tests).
- Android emulator: an English and a Farsi starter auto-cast with sensible characters; swapping one
  is heard on the next read and kept after a restart; no engine or anime name anywhere in Dino.
- The user listens to the English roster and picks favourites; drop characters that sound broken.
