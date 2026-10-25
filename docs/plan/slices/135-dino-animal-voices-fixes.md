# Slice 135 — Dino: every pig a piglet, and the emulator fixes for Slices 124-125

*Built 2026-10-25, not yet verified on the emulator (wave 2026-10-25). Dino Reader only; RTST is not touched.*

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

- **Two or three variants of each young farm animal** in every roster: piglets x3, lambs x2, ducklings x2, puppies x2, kittens x2. Each variant uses a different voice or flavour of that language's existing voices, with its own localized name. When a kind runs out of free variants, the next pick is the same animal kind's character reused with a flavour it has not sounded with yet, never a different animal (a bunny, which has one character, is reused too).
- **Starter update recast:** every kept member whose voice was not picked by hand (`Edited` false) and whose resolved animal (stored, name or text) differs from its current character's animal is recast to that animal's character. That includes the Wolf.
- Animals and the Animal sounds switch must survive a starter update in every language's cast.

## Work

- [x] 1. Roster variants and the same-kind fallback in `CastCharacters`; a test that the pigs starter casts three distinct piglet voices in English and Farsi.
- [x] 2. The starter-update recast covers name-detected animals; a test with the Wolf.
- [x] 3. Find and fix the Farsi cast losing Pop and Pim's animals; a test.
- [x] 4. Check that `AnimalSoundsOn` survives `StarterUpdate.Refresh`, and fix it if it does not; a test. **Result:** no loss found in the code (`Refresh` copies `AnimalSoundsOn` from the stored story, and a test proves it). The likely cause of the emulator observation is that the tester had turned the switch off earlier, or the story was opened before the update; confirm on the emulator.
- [x] 5. Keep the upright words panel inside the visible picture (`PictureTextPages`).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and the Android build.
- Emulator: an updated pigs install shows three piglet voices and the Wolf voice, Farsi included, with sounds on. On page 1 upright, the panel's bottom edge sits on the picture's bottom edge.


## Built

*Built 2026-10-25, not yet verified on the emulator.* 1091 tests green, Android build clean.

*Changed while building:* root causes found. (3) The Farsi cast never got bundled animals: `KeptLanguageCasts` skipped languages with no bundled cast, and ids are shared across language casts, so animals are now matched by id. (5) The panel was anchored to the card bottom below the letterboxed picture; now `PictureTextBook.LetterboxBelow` / `PlacePanel` in Core (tested). (4) No `AnimalSoundsOn` loss in code.

Built: piglet2/3, lamb2, duckling2, puppy2, kitten2 in all 14 rosters (32 characters; es and pt have 27, only piglet2; Farsi young variants use Normal/Whisper/Sleepy for lack of voices). Casting tiers: own kind, then other young animal, then plain; a reused character takes an unused flavour. The starter recast covers any un-edited member not playing a character of its own animal kind on the adapted roster (none if the roster has no such kind). Starters bumped and rebuilt: pigs 6, gingerbread-man 3, tortoise-and-hare 3.

*From review:* high findings 1-9 applied; medium 1, 2, 4, 5, 9 applied, 7 not a bug, 3, 6 and 8 skipped.

#### For CLAUDE.md

- `src/DinoReader.Core/CLAUDE.md`, extend the Slice 125 line: animal casting tiers are own animal kind, then another young animal, then plain; a reused character takes an unused flavour. The starter recast (`StarterUpdate`) covers any un-edited member not playing a character of its own animal kind on the adapted roster (none if the roster has no such kind). Per-language casts match animals by id. (Slice 135: docs/plan/slices/135-dino-animal-voices-fixes.md)

#### Not yet verified

- Emulator re-check: updated pigs install in English and Farsi, three piglet voices, Wolf voice, sounds on, page 1 panel edge.
- Store copies of the bumped starters, if their store entry versions must match.
- Native read of the new roster names.

#### Follow-up fix: Farsi pigs shown as bear cub and puppy

Emulator showed Pip, Pop and Pim all as Pig, but Pop's voice named "bear cub" and Pim's "playful puppy". Root cause: the default Farsi narrator (ganji-adabi) shares a voice with two of the three piglets, so only `piglet2` (Gyro, Squeaky) was offered; the other two pigs reused it, and `CastCharacters.State.Distinct` gave them the first unused flavour, Small and Bouncy, which on the Gyro voice are exactly the bear cub's and the puppy's sound. The cast screen names a sound by its nearest roster character, so the pigs were shown (and, on update, judged) as those animals. Fix: (1) an animal's own roster characters withheld only for sharing the narrator's voice (`NarratorVoiceAnimals`) are offered to animal story characters when their flavour is not the narrator's (Storyteller), so Farsi gets three distinct piglets (`piglet`, `piglet3`, `piglet2`); (2) `Distinct` skips a reuse flavour that the cast screen would name another animal, judged with `CharacterRoster.Nearest` over the whole speaker roster (family and distance), memoised per character and flavour; none free, the reuse shares its sound and the screen flags it. Starter `three-little-pigs.dino` is v7 so installed copies re-judge: `AnimalVoicesToRecast` already recasts Small/Bouncy pigs on the cub/puppy. Tests: `Farsi_pigs_with_the_default_narrator_are_three_distinct_piglets`, `AnimalVoicesToRecast_RecastsFarsiPigsThatReadAsTheBearCubAndThePuppy`.
