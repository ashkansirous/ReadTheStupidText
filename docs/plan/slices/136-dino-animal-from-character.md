# Slice 136 — Dino: the animal comes from the picked character (no Animal line)

*Reversed by Slice 152 (Decision 157, planned 2026-10-26): the animal comes from the book again, never from the picked character.*

*Built 2026-10-25 (PR #360), not yet verified on the emulator.* Dino Reader only; RTST is not touched.

**Batch:** 26 (Batch 25 mock follow-ups)
**Depends on:** Slice 125 (built), Slice 135 (merged: roster variants, starter-update recast)
**Touches:** `src/DinoReader.Core/CharacterRoster*.cs`, `src/DinoReader.Core/StorytellerCharacter.cs`, `src/DinoReader.Core/CastCharacters.cs`, `src/DinoReader.Core/AnimalSound*.cs`, `src/DinoReader.Core/VoiceCaster.cs`, `src/DinoReader.Core/CastEditor.cs`, `src/DinoReader.Core/StarterUpdate.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/CastAnimalPicker.cs` (deleted), `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`

## Why

Slice 125 added an "Animal" line under each cast row and a sheet to change it. The Batch 26-28 mock
(frame 59, `docs/design/dino-reader/Dino Reader Batch 26-28.dc.html`) drops both: the picked character
already names the animal (Cheeky Piglet, Big Bad Wolf), so asking again is redundant.

## Decisions (Decision 132, changed 2026-10-25)

- A character's animal comes from the storyteller character it is cast as. Each roster character carries
  an animal kind, or none (Cheeky Piglet = pig, Big Bad Wolf = wolf, Brave Knight = none). Animal sounds
  use the cast character's animal.
- `CastMember.Animal`, the `story.json` `animal` field, `AnimalGuess` and the build-time animals in
  `tests/DinoReader.Tests/StarterAnimals.cs` stay. They are used only to pick the first character when a
  book is cast or recast; they do not drive sounds after the cast.
- If a grown-up casts a pig as a non-animal character, it makes no sound. That's intended.
- The Animal sounds card (its "no animals" state included) follows the current cast.

#### Design

Frame 59 of `docs/design/dino-reader/Dino Reader Batch 26-28.dc.html` (line 27): each cast row is the
avatar, name, word count, then the character picker and play button, and nothing under it. The Animal
sounds card stays below the cast card, unchanged. Changing the character (frame 53) is the only control.

## Work

- [x] Give every roster character an animal kind or none, in all 14 languages (Slice 135's new variants
      included); a test that every animal roster character has a kind and every plain one has none.
- [x] `AnimalSounds` resolves from the cast character's animal, not from `CastMember.Animal`; the Animal
      sounds card's "no animals" state follows the same rule.
- [x] Remove the cast row "Animal" line (`CastRows.AnimalLine`) and `CastAnimalPicker`, plus their strings
      in all 14 `.resx` files and `AppResources.cs`.
- [x] Update the tests (a pig cast as Brave Knight is silent; the pigs starter still oinks; an own book
      with "Pip the pig" first casts Pip as a piglet) and the Core and App `CLAUDE.md` rules.
      (CLAUDE.md rules are lifted by the index pass, see below.)

*Changed while building:* no new field on `StorytellerCharacter`. The animal comes from the roster id via
the existing `AnimalCharacters.ByRosterId` (ids are the same in all 14 languages, so Slice 135's variants
are covered). Refines Decision 132: a member not Edited by a grown-up keeps its story animal (`Resolve`),
so a cow with no cow character still moos and a duck auto-cast to a piglet quacks; an Edited member takes
the cast character's animal (none = silent; a pig cast as Brave Knight is silent). Sheep and goat
characters follow the story's resolved animal. The sound plan and the Animal sounds card share
`AnimalsOf`/`WithSound`, built from the spoken language's cast.

*Removed:* `CastRows.AnimalLine`, `CastAnimalPicker.cs`, `CastEditor.SetAnimal`/`WithAnimal`,
`AnimalSounds.NameOf`, and the four `Cast_Animal_*` strings in the 14 `.resx` files and `AppResources.cs`.
The `HasAnimalSetting` paths stay for old saved data.

*Tests:* 1097 pass; Android build green.

#### For CLAUDE.md (lifted 2026-10-25)

- `src/DinoReader.Core/CLAUDE.md`: a cast member not Edited by a grown-up keeps its story animal
  (`Resolve`), so a cow with no cow character still moos; an Edited member takes the cast character's animal
  via `AnimalCharacters.ByRosterId` (none = silent, a pig as Brave Knight is silent). Sheep/goat characters
  follow the story's resolved animal. The sound plan and the Animal sounds card share `AnimalsOf`/`WithSound`,
  built from the spoken language's cast. There is no per-cast Animal setting any more; keep `HasAnimalSetting`
  paths for old saved data. (Slice 136: docs/plan/slices/136-dino-animal-from-character.md)

#### Not yet verified

- Emulator: pigs starter in English and Farsi (oink, Wolf growls).
- Emulator: cast page matches frame 59 (no Animal line).
- Emulator: recasting Pip to a non-animal character goes silent.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and the Android build.
- Emulator: the pigs starter, English and Farsi: the cast page matches frame 59 (no Animal line), the pigs
  oink, the Wolf growls; recast Pip to a non-animal character and Pip goes quiet.

#### Emulator result (2026-10-26)

PASS: the cast page matches frame 59 in English and Farsi (no Animal line). The animal sounds (oink, Wolf growls) and recasting Pip to a non-animal going silent are not verifiable by adb and need a listen.
