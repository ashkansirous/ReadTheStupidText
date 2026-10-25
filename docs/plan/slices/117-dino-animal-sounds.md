# Slice 117 — Dino: animal characters make their sounds

*Planned 2026-10-25*, from the user's ask that reading be "more kids like": a daddy sheep should say
"baa" every 2–3 sentences when the story doesn't, a cow "moo", a cat "meow" and so on, in every
language. Dino Reader only; RTST is not touched.

**Batch:** 24 (storyteller characters)
**Depends on:** Slice 116 (characters; the sound uses the animal's character voice)
**Touches:** `src/DinoReader.Core/AnimalSound*.cs` (new), `src/DinoReader.Core/LanguageRules.cs`, `src/DinoReader.Core/*Rules.cs`, `src/DinoReader.Core/Cast*.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/ReadTheStupidText.Domain/Reading/VoiceFlavour.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`

## Why

Dino has no sound-effect or interjection mechanism (grep on `origin/main`, 2026-10-25). Segments are
built per line in `StoryVoicing.ToVoicedSegment` (`StoryVoicing.cs:353-360`) with the voice from
`VoiceCast.VoiceForLine`; an animal sound is one extra short segment there.

## Decisions (2026-10-25, agreed with the user)

- **Decision 123: an animal character says its sound about every 2–3 of its lines.** Dino detects the
  animal from the character's name with a per-language word list in each `LanguageRules` (sheep/lamb:
  baa, cow: moo, cat: meow, dog: woof, duck: quack, pig: oink, hen: cluck, owl: hoo, horse: neigh,
  frog: ribbit, mouse: squeak, lion: roar, and more), and the sound word is written per language
  ("Bêê !" in French, "Bää!" in Swedish, Farsi's own). It is added after every 2nd or 3rd of that
  character's lines (deterministic, so a re-read sounds the same), never on a line that already has
  the sound word, and only on the character's own spoken lines (not narration).
- The animal's own character voice says it, with a stronger flavour on that one word (a wobble for
  sheep and goats, deep and slow for cows); a grown-up animal sounds deeper than a baby one, following
  the character.
- Sounds are spoken by the voice engine, not recorded clips. The sound is spoken but not added to the
  page text on screen.
- On by default; a per-story "Animal sounds" switch on the cast screen turns them off. Parent
  recordings (Slice 111) are never changed.

## Work

- [ ] 1. Smallest end to end, English: `AnimalSounds` (word list → sound, "already said" check, every
  2nd/3rd line rule) in `DinoReader.Core`, `StoryVoicing` inserts the extra segment in the character's
  voice. Unit tests: daddy sheep gets "Baa!" on the expected lines, a line that already says "baa"
  gets none, narration gets none.
- [ ] 2. The stronger per-sound flavour (a wobble flavour on `PitchShifter`'s output for bleats, or
  the nearest existing flavour if the wobble sounds bad in the listening check).
- [ ] 3. Word lists and sound words for the other 13 languages.
- [ ] 4. The "Animal sounds" switch on the cast screen (stored per story, default on); strings in all
  14 `.resx` files; rules in the Core and App `CLAUDE.md` files (For CLAUDE.md block).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (every language has a word list; the
  insertion rule).
- Android emulator: a starter with an animal cast in English and in Farsi: the sheep says its sound
  every 2–3 of its lines; switching it off silences them; the on-screen text is unchanged.
