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

## Work

- [ ] A1. `tools/animal-sounds`: find 2-3 Freesound CC0 candidates per animal (Sonniss where Freesound has none), record
  each sound's URL, author and licence line; trim and loudness-match; a listening page for the user's picks.
- [ ] A2. The picked files under `Resources/Raw/animal-sounds/`, a `NOTICE`/credits line per file in `STORE.md` even
  though CC0 needs none.
- [ ] A3. App: the sound slot plays the bundled file instead of speaking the word (Android, iOS, Windows head); speed,
  pause, Back unchanged; a missing file falls back to silence, never to the spoken word.
- [ ] A4. Generator and dump skip sounds; Three Little Pigs fa audio re-checked (no sound clips).

## Verification

- Unit tests: the sound slot carries the animal kind; dump and generator skip sound entries.
- Android emulator: Three Little Pigs plays a real pig and wolf sound next to their words; iOS compile and the same tests.
- The user's picks written here.
