# Slice 125 — Dino: each character knows its animal, and animal voices to match

*Built 2026-10-25, not yet verified on an emulator (1061 tests green, Android build clean).* Dino Reader only; RTST is not touched.

**Batch:** 26 (Batch 25 mock follow-ups)
**Depends on:** Slice 117 (animal sounds), Slice 116 (storyteller characters), Slice 124 (same cast-page files; merge after it), PR #336 (the pigs retell edits `StarterBuilder.cs`/`StarterTexts.cs`; merge after it)
**Touches:** `src/DinoReader.Core/AnimalSound*.cs`, `src/DinoReader.Core/AnimalGuess*.cs` (new), `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/StoryCast.cs`, `src/DinoReader.Core/CastCharacters.cs`, `src/DinoReader.Core/CastEditor.cs`, `src/DinoReader.Core/FlavourGuess.cs`, `src/DinoReader.Core/StorytellerCharacter.cs`, `src/DinoReader.Core/CharacterRoster*.cs`, `src/DinoReader.Core/VoiceCaster.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/CastFlavourMenu.cs`, `src/DinoReader.App/Cast*Picker*.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.App/Resources/Raw/starters/*.dino`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`

## Why

Slice 117 finds a character's animal from its name only. In The Three Little Pigs the pigs are Pip, Pop
and Pim, so only the Wolf makes a sound, and the pigs get ordinary voices. The user (2026-10-25): "we
can take decision from the story and the name. We are building the stories beforehand so we can take
that decision that pip and the others are piglets and add the voices and set the proper voice for each of
them. On the client side, if someone adds a new book, they can control what voice to pick for each.
It's enough that we give our best guess and it is enough that we have some piglets, sheep and other animal
voices available."

Grounding (2026-10-25): `CastMember` (`Story.cs:22`) has no animal. `AnimalSounds.AnimalsIn` (`:53`) is the only
resolver and calls `KindOf(name)`. The builders take names only (`StoreStorySpec.Characters`). The English
roster has one animal character ("Cheeky Piglet"). Nothing guesses an animal from the story text.

## Decisions (Decision 132, from the user's message; defaults chosen where it was silent)

- **Each cast character can carry an animal.** `CastMember.Animal`, stored as the optional `story.json` cast
  field `animal` (an `AnimalKind` name, or `none` for "not an animal"; absent = guess). It travels in
  `.dino` and backups, survives recasts (`StoryCast.ToMembers`, `CastEditor.KeepExplicitPicks`, per-language
  casts), and an unknown value from a newer app reads as absent.
- **Resolution order:** the stored animal, then the name (Slice 117's `KindOf`), then a **best guess from the
  story text**: an animal word next to the name in the same sentence ("Pip the pig", "a pig called Pip",
  "the smallest pig, Pim") or a group introduction that the named characters belong to ("three little pigs ... Pip, Pop and Pim").
  It is a pure, deterministic Core function with tests in English and at least Farsi and one other language. It
  uses each language's existing animal word lists.
- **Built books are decided at build time.** Starter and store specs can name each character's animal. That
  covers the pigs (Pip, Pop, Pim = pig, young), Peter Rabbit (Peter, Mrs. Rabbit = rabbit), Goldilocks (the
  bears), and Billy Goats Gruff (goats). Packages are rebuilt, and the bundled starter's content version is bumped
  so installed copies update (Decision 130).
- **New kinds:** Rabbit and Bear join `AnimalKind`. Rabbit has no sound. Bear gets a soft "Grr!" (gentle, Decision 130),
  written per language like the others.
- **Animal voices in every language's roster:** piglet, lamb, puppy, kitten, bunny, duckling, bear cub (young);
  and big bear, wolf, mother hen/duck, grown sheep (grown). Each is built from that language's existing voices
  with a flavour (young = Small/Squeaky/Bouncy, grown = Big/Grumpy/Giant) and has a localized name and hint. No
  new voice packs. A character with a known animal is auto-cast to its animal character, young or grown by the
  name's diminutive words ("little", "baby", "Mrs.", "Papa"...; default young for pig, lamb, duckling and bunny).
  This happens only while the grown-up hasn't picked that character's voice (`Edited`).
- **The grown-up can change it:** each character row on the cast page gets an "Animal" line under its voice
  picker. It shows the animal's word, or "Not an animal". It opens a sheet in the style of the existing
  character picker: "Not an animal" first, then the animals. Changing it re-picks the voice only if the voice
  wasn't chosen by hand, and it updates the Animal sounds card (its "no animals" state included). There is
  no mock, so it reuses the existing picker row and sheet styles.

## Work

- [x] 1. Smallest end to end, English: `CastMember.Animal` plus the `story.json` field; `AnimalsIn` takes the cast
  and prefers the stored animal; the pigs starter names Pip, Pop and Pim as pigs, so they oink. Tests: round
  trip, recast keeps it, the pigs get sounds.
- [x] 2. The story-text guess (`AnimalGuess`) used when a book is cast (import and recast) for characters with no
  stored animal; tests on all 14 starter and store texts plus invented sentences.
- [x] 3. Rabbit and Bear kinds, Bear's sound words in 14 languages.
- [x] 4. Animal characters in every roster, and the animal-first auto-cast.
- [x] 5. Store and starter specs name each character's animal; rebuild the packages; bump the starter content
  version; re-upload the changed store stories with `tools/upload-store-story.ps1` as a new `story-v<n+1>`.
  Built and tested; the upload is not done (needs `tools/upload-store-story.ps1` for billy-goats-gruff and peter-rabbit at `story-v4`; the pigs starter is bundled, its store copy is `three-little-pigs`).
- [x] 6. The cast page "Animal" line and sheet, with strings in all 14 `.resx` files; rules in the Core and App `CLAUDE.md` files.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and the Android build.
- Emulator: the pigs starter, English and Farsi. Pip, Pop and Pim are cast as piglets, oink every 2-3 lines, and the
  Wolf is cast as the wolf. On the cast page Pip shows "Pig"; setting Pip to "Not an animal" stops Pip's sounds and
  recasts the voice. A user-added book with "Pip the pig" in the text gets the guess.

*Changed while building:* `story.json` cast field `Animal` plus `NotAnimal`. `AnimalSounds.Resolve` order is stored, name, text guess (`AnimalGuess`, `AnimalGuessWords`; strict: adjacent name only, group lists next to a plural animal word, plural-only words never tag one name; full cue lists for en/fr/es/pt/nl/da/sv/nb, punctuation-only elsewhere). Rabbit (silent) and Bear kinds in 14 languages. Eight animal roster characters (lamb, puppy, kitten, duckling, bear cub, big bear, wolf, ewe) plus the existing piglet in all 14 rosters (roster-size test cap 30); es/pt bear cub stays Normal (3 voices). Non-animals avoid animal characters while a plain one is free. Cast row "Animal" line plus `CastAnimalPicker`. Built books' animals live in `tests/DinoReader.Tests/StarterAnimals.cs`. Pigs starter rebuilt as version 5 on top of #336's retold text. `StarterUpdate` fills animals into kept casts and `StarterStories.ReplaceAsync` recasts un-picked voices (`StoryVoicing.RecastCharacters`, others held for that recast only, failure logged, copy kept). Store specs billy-goats-gruff and peter-rabbit at Version 4. Merged origin/main mid-slice (#336, #338).

*From review:* high findings 1-8 and 10 applied, 9 accepted (Slice 124 limit); medium 1-8 applied, 9 and 10 skipped.

#### For CLAUDE.md

Already on the branch: Core rule (`src/DinoReader.Core/CLAUDE.md`, animal resolver and roster) and App rule (`src/DinoReader.App/CLAUDE.md`, cast animal line and built-book animals). Nothing further to lift. (Slice 125: docs/plan/slices/125-dino-character-animals.md)

#### Not yet verified

- Emulator check from this slice's Verification (pigs starter in English and Farsi, cast page Animal line).
- Store upload of three-little-pigs v5, billy-goats-gruff v4 and peter-rabbit v4 (done after merge).
- Native read of the new strings and roster names in 13 languages.
- Windows head.
