# Slice 117 — Dino: animal characters make their sounds

*Built 2026-10-25, merged (PR #327); emulator PASS, not yet verified by ear.* *Planned 2026-10-25*, from the user's ask that reading be "more kids like": a daddy sheep should say
"baa" every 2–3 sentences when the story doesn't, a cow "moo", a cat "meow" and so on, in every
language. Dino Reader only; RTST is not touched.

**Batch:** 24 (storyteller characters)
**Depends on:** Slice 116 (characters; the sound uses the animal's character voice)
**Touches:** `src/DinoReader.Core/AnimalSound*.cs` (new), `src/DinoReader.Core/LanguageRules.cs`, `src/DinoReader.Core/*Rules.cs`, `src/DinoReader.Core/Cast*.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/Story.cs`, `src/ReadTheStupidText.Application/Reading/PitchShifter.cs`, `src/ReadTheStupidText.Infrastructure/Reading/NeuralSentenceSource.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/*/*SupertonicSpeechReader*.cs`, `tests/ReadTheStupidText.Tests/PitchShifterTests.cs`, `src/ReadTheStupidText.Domain/Reading/VoiceFlavour.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`

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

#### Design (Dino Reader Batch 24 mock, frames 52 and 54, added 2026-10-25)

Source: `docs/design/dino-reader/Dino Reader Batch 24.dc.html`, the "Animal sounds" card at the bottom of frame 52 (pigs) and frame 54 (rabbits, no sounds). Two real-UI frames. The mock agrees with the plan: per story, on by default, spoken only, recordings never changed. It settles the card and the empty state. There are no icons on the card.

- **Animal sounds card (117-4, frame 52).** A white card (radius 18, padding 12x14, min height 64) under the cast card: title "Animal sounds" 15 / 600, hint "Animals say “Oink!” or “Baa!” now and then" 12.5 `#5a5470`, and a switch (52x32, radius 16; on = `#5B57E8` track, 26 white knob at the right). The caption says the pigs get an "Oink!" every 2nd or 3rd of their lines, in their own voice, never added to the page text.
- **No animals state (117-4, frame 54).** Same card, the hint becomes "No animals that make a sound in this story". The switch is still drawn, on. The caption says rabbits have no sound in the word list, so the card says so instead of hiding.
- **Simplified.** The hint's "Oink!" and "Baa!" are fixed English examples in the frame.

**Open point: switch in the empty state.** The mock keeps the switch visible and on when there are no sounding animals. **Default:** show it on but disabled (dimmed, no effect), so it cannot look like a switch that does something; the stored value stays on.

**Open point: example words per language.** The mock hint names "Oink!" and "Baa!". **Default:** one string per language with examples written in that language's sound words (Slice 117's table), not a literal translation.

New strings (all 14 locales): "Animal sounds", "Animals say “Oink!” or “Baa!” now and then", "No animals that make a sound in this story".

## Work

- [x] 1. Smallest end to end, English: `AnimalSounds` (word list → sound, "already said" check, every
  2nd/3rd line rule) in `DinoReader.Core`, `StoryVoicing` inserts the extra segment in the character's
  voice. Unit tests: daddy sheep gets "Baa!" on the expected lines, a line that already says "baa"
  gets none, narration gets none.
- [x] 2. The stronger per-sound flavour (a wobble flavour on `PitchShifter`'s output for bleats, or
  the nearest existing flavour if the wobble sounds bad in the listening check).
- [x] 3. Word lists and sound words for the other 13 languages.
- [x] 4. The "Animal sounds" card on the cast screen (switch stored per story, default on; hint, or "No animals that make a sound in this story" with the switch dimmed when the story has none; frames 52 and 54); strings in all
  14 `.resx` files; rules in the Core and App `CLAUDE.md` files (For CLAUDE.md block).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (every language has a word list; the
  insertion rule).
- Android emulator: a starter with an animal cast in English and in Farsi: the sheep says its sound
  every 2–3 of its lines; switching it off silences them; the on-screen text is unchanged.

*Changed while building:* 17 animals in 14 languages (`AnimalSounds`, `AnimalSoundWords`, `AnimalSoundTables` in Core). `AnimalSounds.Placements` puts one sound after the line's last sub-piece and after its dialogue tag, in the speaker's voice, keyed on the line's last `SourceEnd`, so translated pieces and mid-sentence resumes neither duplicate nor drop it. The cadence (after the 2nd, 5th, 7th, 10th...) counts over the whole book. A line that already says the sound gets none (ja/zh: the sound standing alone or doubled); narration never. Words that are also human names are dropped (billy, robin, kitty, drake, ram, cock, mare, sparrow, big, mis, ori, pinto, marrano, جواد, مهر, حمل, جدي, ハチ); common lion words stay. ja/zh names match whole or with a curated prefix/suffix. `Bleat`/`BleatLamb` flavours (`PitchShifter.Apply`, then `Wobble` at ~7 Hz, depth const `BleatWobble`) apply only to the sound, are never guessed or offered, and all three readers call `Apply`. `Story.AnimalSoundsOn` is the optional `story.json` field `animalSounds` (absent = on) and travels in backups and `.dino`. The switch is a `ToggleSwitch` with `AccessibleName`. The plan is cached per story. The goat sound is "Meeeh!". Touched outside the original Touches (approved, Touches updated): `PitchShifter`, `NeuralSentenceSource`, the Android/iOS readers, `Story.cs`, `StoryJson.cs`, `AppResources.cs`, `ToggleSwitch`.

*From review:* high pass findings 1-9 applied. Medium pass findings 1 and 3 applied; 2 skipped (Persian pig "خرخر" left for a native read).

#### For CLAUDE.md (lifted 2026-10-25)

Already added on the branch in `src/DinoReader.Core/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md` (Slice 117: docs/plan/slices/117-dino-animal-sounds.md).

*Emulator result (2026-10-25): PASS.* The card is on by default; the Wolf's sound segments follow the 2nd/5th/7th line cadence and never appear in page text; the switch off persists across reopening the story and an app restart (after a fix); the Farsi card renders; the none state is dimmed; the switch has an accessibility label and checked state.

*Known limit:* sounds come from the character's name only, so in The Three Little Pigs (pigs named Pip/Pop/Pim) only the Wolf makes a sound. Follow-up idea: let the cast know a character's animal (from the story text or a grown-up pick).

#### Not yet verified

- Native read of all animal words and sounds (Persian pig and cow are guesses).
- Bleat wobble depth (`BleatWobble`) by ear.
- iOS reader build.
- Known limit, follow-up idea: the animal comes from the name only (Pip/Pop/Pim pigs are silent); let the cast know a character's animal.
