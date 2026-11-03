# Slice 255 — Dino: real animal recordings instead of voiced sound words

*Planned 2026-11-03.* Dino Reader only. From the user: "the animal noises should not be read by the ai voices. I prefer
to use real animal voices. can we get them form somewhere instead? like sheep and cow and pig and alike instead of
reading it".

**Depends on:** Slice 254 (where a sound sits: next to its animal's own words, never across a tag).
**Touches:** `src/DinoReader.Core/AnimalSounds*.cs`, `src/DinoReader.App/Reading*.cs`, `src/DinoReader.App/StoryVoicing.cs`,
`src/DinoReader.App/Resources/Raw/animal-sounds/**` (new), `src/DinoReader.App/Platforms/**`, `tests/DinoReader.Tests/**`,
`tools/DinoReader.BookBuilder/SentenceDump.cs`, `tools/premade-audio/generate.py`, `tools/animal-sounds/**` (new),
`src/DinoReader.App/CLAUDE.md`, `src/DinoReader.Core/CLAUDE.md`, `STORE.md`.

## Why

- Today an animal's sound is a sound word read by the voice (`AnimalSounds`, Slice 117; e.g. «خرخر!»), and the Gemini
  generator voices it too. The user wants real recordings.
- 19 animals have sounds (`AnimalKind`, `src/DinoReader.Core/AnimalSounds.cs`): owl, rooster, goat, sheep, cow, cat,
  dog, duck, pig, hen, horse, frog, mouse, lion, bee, wolf, rabbit, bear, bird.

## Sources (licence pages read 2026-11-03, web-research)

- **Freesound, CC0 only** (https://freesound.org/help/faq/: CC0 means "you can do pretty much what you want with the
  sound ... you could even sell the sound"). Licence is per sound: each chosen sound's page must show CC0, recorded here.
  Wolf howl: 72 CC0 results. Not checked: login needed to download, coverage of pig, mouse, bee.
- **Sonniss GDC bundles** (https://sonniss.com/gdc-bundle-license/): apps allowed, no attribution; may not be supplied
  "as sound effects" on their own (fine inside an app). Animal coverage not checked.
- Not used: BBC Sound Effects (non-commercial RemArc), xeno-canto (per recording, many NC, attribution), Zapsplat free
  (attribution), Wikimedia Commons (per file, thin coverage), Pixabay and Mixkit (app use not confirmed on their pages).

## Decisions (coordinator; change if wrong)

- **Only animal noises change.** Other sound words in a story stay voiced: the user, 2026-11-03: "keep the ai voice for
  sounds like آپچی! آپچی! آپچی!" (sneezes, knocks like «تق تق تق!», and any sound word written in the story text). The
  recording replaces only the animal sound the app adds for an animal character (`AnimalSounds`).
- One short recording per animal (about 0.5-2 s, trimmed, loudness-matched), AAC, bundled in the app (no download).
- The recording plays where the sound word plays today (after Slice 254: next to the animal's words), on every path:
  on-device voices and pre-made Gemini audio. The sound word is no longer spoken; whether it still shows on screen
  stays as today.
- The Gemini generator and the BookBuilder dump skip sound entries (no clip, no request).
- The user picks each recording on a listening page (2-3 CC0 candidates per animal).
- Android, iOS and the Windows head alike (shared Core + App code; each platform's player checked in A3).

## Decisions (the user, listening round 1, 2026-11-03 20:41 UTC)

Page: https://claude.ai/artifact/151qSfw1SiXsJ2zRykH6JJ. Candidates, trimmed clips and `picks.json` live outside git in
`D:/ReadTheStupidText-worktrees/s255-sounds/` until A2 copies the picked files into the app. Kept:

- cat: round 1 option 1, "cat meow" by tuberatanka, https://freesound.org/people/tuberatanka/sounds/110011/ (CC0)
- bee: round 1 option 2, "Insects - Bee; Honeybee, Buzz, Close Perspective" by TheKingOfGeeks360, https://freesound.org/people/TheKingOfGeeks360/sounds/846433/ (CC0)
- bird: round 1 option 1, "BIRDSong_Blackcap, Recording 9 _JW Audio" by JW_Audio, https://freesound.org/people/JW_Audio/sounds/827197/ (CC0)
- duck: round 1 option 1, "single quack from a duck" by Mikes-MultiMedia, https://freesound.org/people/Mikes-MultiMedia/sounds/418509/ (CC0)
- frog: round 1 option 1, "Frog.ogg" by egomassive, https://freesound.org/people/egomassive/sounds/536759/ (CC0)
- goat: round 1 option 3, "goat.mp3" by saha213131, https://freesound.org/people/saha213131/sounds/683259/ (CC0)
- hen: round 1 option 2, "chicken sound - clucking.wav" by chanyatenn, https://freesound.org/people/chanyatenn/sounds/424076/ (CC0)
- horse: round 1 option 2, "horse neigh shortened.wav" by shadoWisp, https://freesound.org/people/shadoWisp/sounds/269571/ (CC0)
- rooster: round 1 option 3, "Rooster Crow 1" by BenjaminNelan, https://freesound.org/people/BenjaminNelan/sounds/435508/ (CC0)

Not accepted; the user asked for more ("I think we need more animal sound picks"): cow, owl, sheep, pig ("find shorter
one and without surrounding noise"), mouse, rabbit ("find without surrounding noise"), dog, wolf (other), lion (none
right), bear (one candidate, none right). Round 2 page, 2-5 new CC0 candidates each, quiet single calls about 1 s:
https://claude.ai/artifact/GsiKvyUm6o8cC3x3DwkwKK (published 2026-11-03; clips and `round2/candidates.json` in
`D:/ReadTheStupidText-worktrees/s255-sounds/round2/`).

## Decisions (the user, listening round 2, 2026-11-03 21:02 UTC)

- cow: round 2 option 1, "Moo 3 - Moo Moo the Cow" by manofham, https://freesound.org/people/manofham/sounds/700380/ (CC0)
- owl: round 2 option 4, "owl_hooting_000102_0145S3 002-070 000-002 068-074.wav" by Gerent, https://freesound.org/people/Gerent/sounds/558396/ (CC0)
- sheep: round 2 option 1, "bah bala oveja/baa bleat sheep.WAV" by SergioJbs, https://freesound.org/people/SergioJbs/sounds/669872/ (CC0)
- pig: round 2 option 1, "oink.wav" by krzpia, https://freesound.org/people/krzpia/sounds/510974/ (CC0)
- mouse: round 2 option 1, "Mouse Squeaks.wav" by shyguy014, https://freesound.org/people/shyguy014/sounds/463789/ (CC0)
- dog: round 2 option 5, "Ladrido [Bark]" by SmirnoffLulo, https://freesound.org/people/SmirnoffLulo/sounds/585873/ (CC0; a puppy)
- bear, lion, rabbit, wolf: none picked, note on each: "no sound for this animal". Read as: these four get no
  recording and no sound is played for them (not the voiced sound word either, which the slice replaces). A3 must
  decide what the reader does at such a sound slot (skip it silently); ask the user only if skipping reads wrong.

All 19 animals are now settled (15 recordings, 4 with no sound), so A1's picking is done.

## Work

- [x] A1. `tools/animal-sounds`: find 2-3 Freesound CC0 candidates per animal (Sonniss where Freesound has none), record
  each sound's URL, author and licence line; trim and loudness-match; a listening page for the user's picks.
  *Done 2026-11-03 (two rounds, picks in the decisions above; files outside git in
  `D:/ReadTheStupidText-worktrees/s255-sounds/` and `round2/`, with `picks.json` and `candidates.json`; the scripts were
  ad hoc, not in `tools/`). A2 copies the 15 picked `.m4a` files in; bear, lion, rabbit and wolf get no sound.*
- [ ] A2. The picked files under `Resources/Raw/animal-sounds/`, a `NOTICE`/credits line per file in `STORE.md` even
  though CC0 needs none.
- [ ] A3. App: the sound slot plays the bundled file instead of speaking the word (Android, iOS, Windows head); speed,
  pause, Back unchanged; a missing file falls back to silence, never to the spoken word.
- [ ] A4. Generator and dump skip sounds; Three Little Pigs fa audio re-checked (no sound clips).

## Verification

- Unit tests: the sound slot carries the animal kind; dump and generator skip sound entries.
- Android emulator: Three Little Pigs plays a real pig and wolf sound next to their words; iOS compile and the same tests.
- The user's picks written here.
