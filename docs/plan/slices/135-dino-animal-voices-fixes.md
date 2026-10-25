# Slice 135 — Dino: every pig a piglet, and the emulator fixes for Slices 124-125

*In progress (wave 2026-10-25), draft PR.* Dino Reader only; RTST is not touched.

**Batch:** 26 (Batch 25 mock follow-ups)
**Depends on:** Slices 124, 125 (merged)
**Touches:** `src/DinoReader.Core/CastCharacters.cs`, `src/DinoReader.Core/CharacterRoster*.cs`, `src/DinoReader.Core/StarterUpdate.cs`, `src/DinoReader.Core/CastEditor.cs`, `src/DinoReader.Core/AnimalSound*.cs`, `src/DinoReader.App/StarterStories.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`

## Why

The emulator check of Slices 124-125 (2026-10-25, screenshots in `D:\ReadTheStupidText-worktrees\reviews\verify-125\`) found these problems:

1. **Only one pig gets a piglet voice.** Each roster has one piglet, so in The Three Little Pigs Pip got "Playful Puppy" and Pop got "Bear Cub". The user asked for "some piglets, sheep and other animal voices".
2. **An updated starter keeps old voices for name-detected animals.** The Wolf stayed "Rumbly Dragon". `StarterUpdate.AnimalVoicesToRecast` only recasts members that *gained* an animal.
3. **In the Farsi cast, Pop and Pim show "Not an animal"**, while all three are pigs in the English cast. The per-language copy of animals is wrong, probably `CastEditor.WithAnimal` or `StarterUpdate`.
4. **Animal sounds was off after the starter update** on the emulator. It is unconfirmed whether the update lost the switch or the tester had turned it off earlier.
5. **Words panel, upright (pigs page 1):** the panel covered about 30% of the picture but hung about 100px below the picture's bottom edge. It should sit inside the visible picture (Decision 131).

## Decisions

- **Two or three variants of each young farm animal** in every roster: piglets x3, lambs x2, ducklings x2, puppies x2, kittens x2. Each variant uses a different voice or flavour of that language's existing voices, with its own localized name. When a kind runs out of free variants, the next pick is the same animal kind with a reused voice and a different flavour, never a different animal.
- **Starter update recast:** every kept member whose voice was not picked by hand (`Edited` false) and whose resolved animal (stored, name or text) differs from its current character's animal is recast to that animal's character. That includes the Wolf.
- Animals and the Animal sounds switch must survive a starter update in every language's cast.

## Work

- [ ] 1. Roster variants and the same-kind fallback in `CastCharacters`; a test that the pigs starter casts three distinct piglet voices in English and Farsi.
- [ ] 2. The starter-update recast covers name-detected animals; a test with the Wolf.
- [ ] 3. Find and fix the Farsi cast losing Pop and Pim's animals; a test.
- [ ] 4. Check that `AnimalSoundsOn` survives `StarterUpdate.Refresh`, and fix it if it does not; a test.
- [ ] 5. Keep the upright words panel inside the visible picture (`PictureTextPages`).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and the Android build.
- Emulator: an updated pigs install shows three piglet voices and the Wolf voice, Farsi included, with sounds on. On page 1 upright, the panel's bottom edge sits on the picture's bottom edge.
