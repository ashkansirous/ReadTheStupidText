# Slice 136 — Dino: the animal comes from the picked character (no Animal line)

*Planned 2026-10-25.* Dino Reader only; RTST is not touched.

**Batch:** 26 (Batch 25 mock follow-ups)
**Depends on:** Slice 125 (built), Slice 135 (in progress, draft PR #352: roster variants, starter-update recast; merge after it, both edit the rosters, `AnimalSounds` and the cast page)
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

- [ ] Give every roster character an animal kind or none, in all 14 languages (Slice 135's new variants
      included); a test that every animal roster character has a kind and every plain one has none.
- [ ] `AnimalSounds` resolves from the cast character's animal, not from `CastMember.Animal`; the Animal
      sounds card's "no animals" state follows the same rule.
- [ ] Remove the cast row "Animal" line (`CastRows.AnimalLine`) and `CastAnimalPicker`, plus their strings
      in all 14 `.resx` files and `AppResources.cs`.
- [ ] Update the tests (a pig cast as Brave Knight is silent; the pigs starter still oinks; an own book
      with "Pip the pig" first casts Pip as a piglet) and the Core and App `CLAUDE.md` rules.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and the Android build.
- Emulator: the pigs starter, English and Farsi: the cast page matches frame 59 (no Animal line), the pigs
  oink, the Wolf growls; recast Pip to a non-animal character and Pip goes quiet.
