# Slice 152 — Dino: a character's animal comes from the book, never from its voice

*Built 2026-10-27, PR #436 (merged), not yet verified on device (see Not yet verified).* Dino Reader only.

**Batch:** 31 (tester review, 2026-10-26; Decision 157, reverses Decision 132)
**Touches:** `src/DinoReader.Core/AnimalSound*.cs`, `src/DinoReader.Core/CastCharacters.cs`, `src/DinoReader.Core/StoryCast.cs`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/SettingsRows.cs` (shared with Slice 150: sequence them), `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/StarterAnimals.cs`, `tests/DinoReader.Tests/StoreTexts.cs`, `tests/DinoReader.Tests/StarterBuilder.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `.claude/agents/dino-story-writer.md`

## Why

Characters must still make their sounds ("Meeeh!", "Baaa!"). Decision 132 / Slice 136 made the animal follow
the storyteller character a grown-up picks: once a member is edited, the animal is found from its voice and
flavour (`AnimalCharacters.Of`). So picking a voice changes what animal a character is, and a goat cast to the
Brave Knight goes silent. The user calls this a wrong decision (2026-10-26). The animal is a fact of the story,
not of the voice.

## Decisions locked with the user (Decision 157, reverses Decision 132)

- **Store books and starter books:** each character's animal comes only from the book's data: the `story.json`
  cast field `Animal`, a kind or `none`. Every character in these books carries it explicitly (no guessing).
  Changing a voice or character never changes the animal or its sound.
- **The animal is not shown in the UI.** Remove the cast page's naming of the animal through the nearest
  roster character, and any other place that shows it.
- The sound is still spoken in the character's own voice and flavour (Slice 117), so it changes with the voice;
  what the sound says does not.
- **Parent-provided books (photos, PDFs, typed): out of scope, unchanged** (they keep today's
  `Resolve`/`AnimalGuess` path).
- **Animal sounds becomes one app-wide grown-up setting, on by default** (Batch 31 mock, frame 70; adopted 2026-10-26).
  It leaves the cast page; the stored per-book `Story.AnimalSoundsOn` is ignored.

## Work

- [x] `AnimalSounds.OfCast`: for books with an explicit animal on every member, use the stored animal only and
      ignore `Edited` and `AnimalCharacters.Of`. Other books keep today's code path. Tests: recasting a goat to the
      Brave Knight still bleats; recasting a pig to the wolf still oinks.
- [x] Builders write an explicit `Animal` (kind or `none`) for every character of every starter and store book
      (`StarterAnimals.cs`, and the store equivalent). A test fails a starter or store book with a member that has no animal.
- [x] Rebuild the 15 starters (the version bump makes installed copies update). Store books: bump and re-upload
      after the user's OK (memory: store uploads need approval). The books in open illustration PRs (#393, #394, #402,
      #413, #414, #415) get it when they merge.
- [x] Cast page: no animal text anywhere, and no Animal sounds card (`CastPage.cs`).
- [x] App-wide `AnimalSounds` preference (default on) in grown-up settings, below Reading speed (frame 70); `StoryVoicing`
      reads it instead of `Story.AnimalSoundsOn` (kept in the format, ignored). Strings in all 14 locales. Test: off silences every book.
- [x] Docs: replace the "Which animal makes sounds (Slice 136)" sentence in `src/DinoReader.Core/CLAUDE.md`. The
      writer agent must give every character an animal or `none`.

## Verification

- `dotnet test tests/DinoReader.Tests`.
- Emulator (Billy Goats Gruff and the Ugly Duckling): recast every animal to a non-animal character and to another animal's character;
  each still makes its own sound in its new voice. The cast page shows no animal.

#### Design (Dino Reader Batch 31 mock, screens 69 and 70, added 2026-10-26)

Source: `docs/design/dino-reader/Dino Reader Batch 31.dc.html`, screen 69 (lines 105-120) and the Animal sounds row of screen 70 (lines 122-149). Two 360x760 frames, real UI. The mock agrees with Decision 157 on "no animal in the UI" and disagrees with the plan on where the Animal sounds switch lives.

- **Cast page, frame 69.** No animal line under any character and no Animal sounds card. The example: Big Billy Goat is recast to the Brave Knight and still bleats, now in the knight's voice (in store and starter books the animal comes from the book, never from the voice). Parent-made books keep today's guess, also unseen. The row layout is in Slice 151's Design subsection.
- **Animal sounds row, frame 70.** A white card, radius 18, padding 12/14, min-height 64, gap 12. Title "Animal sounds" 15/600. Sub-line "Animals say “Oink!” or “Baa!” now and then, in every book" 12.5 `#5a5470`, line-height 1.35. Switch at the right: 52x32, radius 16, on = fill `#5B57E8` with a 26 px white thumb at the right (3 px inset). Shown on in the frame. Sits between the Reading speed card and "Help make Dino better".
- **New strings (14 locales).** "Animal sounds" (title) and the sub-line above (a draft in the mock).

**Decided (2026-10-26): Animal sounds is app-wide.** The user asked for the plan to follow the Batch 31 mock, so the per-book switch on the cast page (`CastPage.cs:302`, read in `StoryVoicing.cs:142`) is replaced by one grown-up setting, on by default; the stored per-book value is ignored, as Slice 151 does for `WordsOverPictures`.

#### Review notes (high pass)

- The animal is decided per member, not per book: a member with a stored animal or `none` keeps it, an undecided member (or a character the cast does not list) follows the old voice rule. Installed older store books and edited starters therefore still gain from their stored animals.
- Accepted loss: a grown-up's per-book "Animal sounds off" (`Story.AnimalSoundsOn` false) is ignored and sounds are on again until the app-wide switch is turned off. No migration.
- Cat, Duck, Duckling, Dilly, Mama Bear and the two mice stay stored as `none`: the stored animal also picks the voice, and giving it back puts a bear, duckling, kitten or mouse voice of the wrong gender on them (56 gender tests fail). A "cast as a person" field would be needed to let them sound.
- The builder fails a member the story text suggests is an animal (`AnimalGuess`) but has no animal decided; deliberate people are listed in `StarterAnimals.NotAnimalsByStory`.


#### Changed while building

- Per member: an explicit animal or `none` from the book wins over voice, flavour and recast; undecided members (parent-made books) keep the old rule. `StarterAnimals.Explicit` stamps every starter and store member and throws if the text suggests an undecided animal; `NotAnimalsByStory` holds deliberate people (Grandpa, Granny).
- Animal sounds is one app-wide switch (`AnimalSoundsPreference`, default on) on `LanguagePage` between Reading speed and usage; `Story.AnimalSoundsOn` is ignored (per-book off values dropped, accepted). The cast page animal switch rows are removed.
- Starter update: the bundled explicit animal replaces the stored one for kept members; recordings are unaffected (page text unchanged). Starters rebuilt: Good Enough v3, Pigs v9, Gingerbread v5, Tortoise v5.
- Store: all 11 store books bumped by 1, packages in `artifacts/store/<id>/`; NOT uploaded (needs the user's OK).
- Cat, Duck, Duckling, Dilly, Mama Bear and the mice stay mute until Slice 156 (the book picks the voice).

#### From review

- High pass (`reviews\152-high.md`): findings 1, 2, 4, 7, 9 fixed. Medium pass (`reviews\s152-medium.md`): 3, 4, 6, 10 fixed. See the review notes above.

#### Verified

- Emulator 2026-10-27, headless: the Animal sounds card toggles and persists; recasting a pig to a non-animal voice keeps its sounds planned; switch off gives none; no crash.

#### Not yet verified

- Billy Goats store book on device.
- Hearing a recast animal by ear.
- Windows head and iOS.
- Native read of the `Settings_AnimalSounds_*` strings.
- Store upload and live check of the 11 books.

#### For CLAUDE.md (lifted 2026-10-27)

- `src/DinoReader.Core/AnimalSounds.cs`: a member's explicit animal (kind or `none`) from the book wins over voice, flavour and recast; only undecided members use the old voice rule. `StarterAnimals.Explicit` must stamp every starter and store member; deliberate people go in `NotAnimalsByStory`. (Slice 152: docs/plan/slices/152-dino-animal-from-the-book.md)
- `src/DinoReader.App/StoryVoicing.cs`: animal sounds read the app-wide `AnimalSoundsPreference`, never `Story.AnimalSoundsOn` (ignored). (Slice 152: docs/plan/slices/152-dino-animal-from-the-book.md)


#### Full rule text (moved from src/DinoReader.Core/CLAUDE.md)

**Which animal makes sounds (Slice 152, Decision 157, reversing Decisions 132 and 136):** a store or starter book names every character's animal itself (the cast field `Animal`: a kind or `none`; `StarterAnimals`/`StarterBuilder` write one for every member, and the builder fails a member the text suggests is an animal but has none decided; `AnimalFromBookTests` fails a book with an undecided member). `AnimalSounds.OfCast` decides per member: a member with an animal setting (a kind or `none`) plays it only, so changing a voice, flavour or character never changes the animal or its sound, and the UI never shows the animal; a member with no setting (parent-made books: photos, PDFs, typed; or a character the cast does not list) keeps the older rule: `Edited` false keeps `Resolve`'s animal, once edited the animal is the cast character's kind found by voice and flavour (`AnimalCharacters.Of`). Whether animals sound at all is the grown-up's one app-wide switch (`AnimalSoundsPreference`); `Story.AnimalSoundsOn` stays in the format but is ignored. Rabbit has no sound (`SoundOf` null). The read uses `StoryVoicing.AnimalsOf` for the language read. `CastEditor.SetAnimal`/`WithAnimal` are gone. A new roster character needs a free (voice, flavour) place in all six slot lists and a name and hint in all 14 word lists.
