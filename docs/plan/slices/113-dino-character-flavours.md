# Slice 113 — Dino: playful character voices (pitch and pace per character)

*In progress (wave 2026-10-24), draft PR.* *Planned 2026-10-24*, from the user's note that Dino's voices are "too grown up": stories about sheep
and pigs are read in a serious adult tone.

**Batch:** 23 (voices that sound like a story for children)
**Depends on:** Slice 108 (a cast per reading language; flavours are stored per cast member)
**Touches:** `src/ReadTheStupidText.Application/Reading/VoicedSegment.cs`, `src/ReadTheStupidText.Application/Reading/PitchShifter.cs` (new), `src/ReadTheStupidText.Application/Reading/ISpeechReader.cs`, `src/ReadTheStupidText.Domain/Reading/VoiceFlavour.cs` (new), `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Infrastructure/Reading/NeuralSentenceSource.cs`, `src/ReadTheStupidText.Mobile.Shared/**/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/**/*SentenceSource*.cs`, `src/DinoReader.Core/CastMember.cs`, `src/DinoReader.Core/CastEditor.cs`, `src/DinoReader.Core/FlavourGuess.cs` (new), `src/DinoReader.Core/*Rules.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`

## Why

Every Dino voice is a calm adult reading voice (Supertonic-3's ten, the four Piper Farsi voices). The
pipeline only picks *which* voice speaks: `VoicedSegment` carries a voice id and nothing else
(`VoicedSegment.cs:12`), and synthesis runs at speed 1.0 (`SherpaEngineFactory.cs:44-56`). There is no
pitch, pace or style per character. No permissive, phone-sized model has children's or emotive voices
(Chatterbox and Orpheus are too big; research 2026-10-24). Shaping the voices we have works in every
language, Farsi included, and costs no download.

## Decisions (2026-10-24, agreed with the user)

- **Decision 117: each character gets a voice flavour, guessed by Dino and editable by a parent.** A
  closed set `VoiceFlavour` (Normal, Small: higher and a little quicker, Big: deeper and slower, Bouncy:
  higher and quick, Sleepy: slow and a little lower), each a fixed pitch (semitones) and pace. Dino
  guesses it from the character: a per-language word list in each `LanguageRules` (lamb, mouse, rabbit,
  bird → Small; pig, bear, giant, elephant → Big; owl, grandad → Sleepy), else Normal. The guess is the
  auto-cast default; a parent changes it per character on the cast screen (a flavour row under the
  voice, with a sample button). Pinned with the voice, per language's cast (Slice 108).
- The narrator gets a storyteller pace (slightly slower than 1.0, warmer pauses at sentence ends), with
  no pitch change.
- Pace uses the engine's own `speed` argument per segment. Pitch is a pure C# shift on the generated
  samples (`PitchShifter`, time-domain overlap-add) applied before playback, so it works the same on
  Android, iOS and Windows and needs no new library. No LGPL/GPL audio library (SoundTouch is LGPL).
  The shift range is kept small (about ±4 semitones) so voices don't turn into chipmunks.
- RTST (the text reader) is unchanged: it always plays Normal.

#### Design (Dino Reader Batch 22-23 mock, frame 48, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 22-23.dc.html`, frame 48 (lines 259-283); frame 49 shows the same rows collapsed. One real-UI frame (360x760 phone frame, 6px `#2b2540` border, radius 34.); the open flavour menu is drawn over the third row. Icons in the mock are inline SVG strokes (2.2 wide, `#5B57E8`), not Phosphor names; the app uses its own `IconKind` set. The mock agrees with the plan on the flavour set and the row, and disagrees on the guesses.

- **Sounds row (113-3).** Under each character's name and voice ("Boy · Momonga"), a row: label "Sounds" 13 `#5a5470`, a flavour picker (36 high, radius 10, `#F3EFE6`, 13, chevron) and a 36 play circle `#ECE7FF` that samples it. The open picker's selected value has a 2px `#5B57E8` ring. Rows are collapsed to the subtitle in the mock for space (the boy/girl toggle and voice picker as in frame 40).
- **Flavour menu.** A floating list, 190 wide, white, radius 14, shadow `0 10px 30px rgba(43,37,64,.25)`, items 44 high: "Normal" (selected, `#ECE7FF`, 14 `#3f3bc4`), "Small" - "higher, a little quicker", "Big" - "deeper, slower", "Bouncy" - "higher, quick", "Sleepy" - "slow, a little lower" (14 name, 11.5 `#8a8399` hint).
- **Guesses shown.** Little Pig: Small; The Wolf: Big; Mother Pig: Normal. Picking one pins it (EDITED) in that language's cast.
- **Word list (note).** The mock names: pig, wolf, bear to Big; lamb, mouse, rabbit to Small; owl, grandad to Sleepy; else Normal.

**Open point: guesses for the pigs.** The plan's list puts pig in Big and has no wolf. The mock's note puts pig and wolf in Big, yet its rows show Little Pig as Small and Mother Pig as Normal. **Default:** Big list is wolf, bear, giant, elephant (no "pig"); "little" and "small" are modifier words that beat the animal word, so Little Pig is Small, The Wolf is Big and Mother Pig is Normal, as drawn. The mock wins; work item 2 now says so.

Changed to match the mock (2026-10-24): wolf is in the Big list; the flavour row is labelled "Sounds" and sits under the voice.

New strings (all 14 locales): "Sounds", "Normal", "Small", "Big", "Bouncy", "Sleepy" and the four hints ("higher, a little quicker", "deeper, slower", "higher, quick", "slow, a little lower").

## Work

- [ ] 1. Smallest end to end: `VoiceFlavour` + pitch/pace table, `VoicedSegment` carries it,
  `PitchShifter` (unit tests on a synthetic tone: pitch moves, length stays), the sherpa sentence sources
  pass speed and shift; one hard-coded Small character on the emulator to hear it.
- [ ] 2. `FlavourGuess` (Big: wolf, bear, giant, elephant; Small: lamb, mouse, rabbit, bird plus the modifiers "little"/"small" that win over the animal word; Sleepy: owl, grandad; so Little Pig is Small and Mother Pig Normal, as in frame 48) with word lists in each language's rules (all 14 app languages; Farsi included),
  unit tests; auto-cast stores the guess on `CastMember` (optional `story.json` field, schema stays 1).
- [ ] 3. Cast screen "Sounds" row (label, flavour picker with the hints, sample button; frame 48); edits through `CastEditor`, pinned per language.
- [ ] 4. Narrator storyteller pace.
- [ ] 5. Rules in `src/DinoReader.Core/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` and
  `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: a starter with an animal cast in English and in Farsi: small animals sound higher,
  big ones deeper; change a flavour on the cast screen and hear it on the next read; RTST reads unchanged.
- Kill criterion: if shifted voices sound robotic in the listening check, ship pace-only flavours and
  drop pitch.
