# Slice 255 — Dino: real animal recordings instead of voiced sound words
**Product:** Dino

*Built 2026-11-04 (A2-A4), merged as PR #783 (456a6a5); Android emulator run done, see Not yet verified.* (Planned 2026-11-03.) Dino Reader only. From the user: "the animal noises should not be read by the ai voices. I prefer
to use real animal voices. can we get them form somewhere instead? like sheep and cow and pig and alike instead of
reading it".

**Depends on:** Slice 254 (where a sound sits: next to its animal's own words, never across a tag).
**Touches:** `src/DinoReader.Core/AnimalSounds*.cs`, `src/DinoReader.App/Reading*.cs`, `src/DinoReader.App/StoryVoicing.cs`,
`src/DinoReader.App/Resources/Raw/animal-sounds/**` (new), `src/DinoReader.App/Platforms/**`, `tests/DinoReader.Tests/**`,
`tools/DinoReader.BookBuilder/SentenceDump.cs`, `tools/premade-audio/generate.py`, `tools/animal-sounds/**` (new),
`src/DinoReader.App/CLAUDE.md`, `src/DinoReader.Core/CLAUDE.md`, `STORE.md`, `src/DinoReader.App/DinoReader.App.csproj` (Raw assets), `src/ReadTheStupidText.Mobile.Shared/**` (audio player, if A3 plays through it), `.maestro/dino/**` (the clip flow), `tools/premade-audio/README.md`, `tools/premade-audio/testdata/**`, `docs/plan/slices/255-dino-real-animal-sounds.md`.

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
- [x] A2. The picked files under `Resources/Raw/animal-sounds/`, a `NOTICE`/credits line per file in `STORE.md` even
  though CC0 needs none. *Built 2026-11-04 (nightly builder): 15 `.m4a` files (`<kind>.m4a`, 6-14 KB each), bundled as flat
  `MauiAsset`s `animal-<kind>.m4a` (the Windows package drops folders, like the starter books); credits table in STORE.md.*
- [x] A3. App: the sound slot plays the bundled file instead of speaking the word (Android, iOS, Windows head); speed,
  pause, Back unchanged; a missing file falls back to silence, never to the spoken word. *Built 2026-11-04, compiled for
  Android, iOS (compile only) and the Windows head; run on the Android emulator only (device run below).*
- [x] A4. Generator and dump skip sounds; Three Little Pigs fa audio re-checked (no sound clips). *Built 2026-11-04:
  `dump-sentences` lists no sound entries (78 sentences in fa and en, none with `sound`); `generate.py` also drops `sound`
  entries of an older dump; the pigs `audio-cast.json` still checks clean against the new dumps (14 bound lines).*

## How A3 works (nightly builder, 2026-11-04)

- `StoryVoicing.AnimalSoundAfter` still adds the zero-width segment at the same place (Slice 254), but its `Transform` is
  an `AnimalClipMarker(kind)` (Core, `AnimalSoundClips.cs`), no longer a flavour. An animal without a recording (bear,
  lion, rabbit, wolf) gets no slot at all.
- No speech reader (Android Supertonic, system voice, iOS, Windows, premade) was touched. `ReadingSession` splits the
  segments at the first marker (`AnimalClipRuns.Next`), gives the reader only the run before it, and when that run
  completes (`ApplyCompleted`) plays the clip through the existing `IRecordingPlayer` (the one parent recordings use, so
  all three platforms already play AAC from a file path), then speaks the rest the same way until the segments are
  used up. Pause, Resume, Stop and a new Start/Back reuse the `_recorded` state of a recorded page; the Loading state shows
  between the clip and the next audio.
- `AnimalClipFiles` (App) copies the needed `animal-<kind>.m4a` from the package into the cache folder once per app
  version before a read starts (`FileSystem.OpenAppPackageFileAsync`, the call the starter books already use), so a slot
  starts its clip with no wait. A missing or unplayable file is skipped silently, never spoken.
- Debug-only log lines (RTST-TEST): `[Reading] animal clip <Kind> plays`, `... has no file, slot skipped`, `... could not play
  (...), slot skipped`. Scripted check: `.maestro/dino/animal-clip-plays.yaml` (lint clean).
- Choices made unattended: the bear/lion/rabbit/wolf slots are dropped (the user's "no sound for this animal"), no question
  asked; the grown-up "Animal sounds" switch still decides whether a slot exists; the clips play at their own speed (the
  reading speed does not change them); the sound word is kept in the segment text only as a label.
- Known behaviour left in place: the speech engine starts the run after a clip only when the clip ends, so there can be a
  short gap (the Loading state) after a clip while the next chunk is made; a head start cannot span a clip.

## Review round 2 (medium): one step machine

- The read is a flat list of steps (`AnimalClipRuns.Plan`: speak a run / play a clip). `ReadingSession` holds one `StepRead`
  (read id, step index, cancellation) and a pending-advance flag. Every end signal (run spoken, clip ended or failed, clip
  file missing, posted so it never recurses) calls `AdvanceStep(readId)`, which ignores a stale or cancelled read, defers
  while paused (Resume takes the step) and otherwise starts the next one (`AnimalClipRuns.Decide`, unit-tested).
- (Replaced in round 3: a speech run's `Completed` counts only for a run seen `Playing`, as on origin/main.)
- Slot positions: the plan counts each character's own lines (`AnimalSounds.Plan`), so adding slots for animals with a
  recording but no sound word in the story's language (and dropping bear, lion, rabbit, wolf) never moves another
  character's slot. Slots are zero-width, so highlight and parent-recording positions are unchanged; a test checks both
  the property and that the filter change only adds animals with a recording and only drops the four without.
- Not changed: the clip cache keeps one folder per app version (finding 4, skipped), the copy before the first read is
  short (8), a written animal sound word keeps the AI voice (10, the recorded decision).

## Review round 3 (medium): one rule for a speech run's end

- `ISpeechReader.Completed` carries no id, so accepting one before the run was seen `Playing` let a stopped run's late
  `Completed` advance the next run. `ReadingSession` now counts runs (`_run`, bumped when a speech step starts, on Stop,
  StartAt and ResumePendingAdvance); `Playing` records `_playingRun`; `OnCompleted` stamps `_run` when raised;
  `AnimalClipRuns.AcceptCompleted` (unit-tested) accepts only a stamp equal to the current run that was seen Playing for the
  active read, and the session clears `_playingRun` after taking one, so a duplicate is ignored. `_speechReturnedId` and
  `_playingReadId` are gone. ResumePendingAdvance also stops a paused clip before clearing `_recorded`.
- Runs/steps that finish before any `Playing` (a fully cached run, a blank run) are not advanced by `Completed`: same as origin/main.

## From review

Findings files (in `D:/ReadTheStupidText-worktrees/reviews/`, outside the repo):

- `s255-high.md`: applied 1 (re-check id and token after the clip copy), 2 (rest-run cancellable), 3 (slot gated on `AnimalSoundClips.Has`, word kept as a label), 4, 6 (tests) and 7 (premade-audio README). Skipped 5 (old per-version cache folders are not deleted), 8 (`AnimalSounds.FlavourFor` and the animal flavours have no production caller, kept; only tests use them), 9 (style: interpolated debug log strings, using order).
- `s255-medium.md`: 1, 2 and 3 fixed by the one step machine (round 2 above); 5 applied (slot-plan test); 6 skipped (the label cannot reach the voice, not user-visible); 4 (old cache folders), 7 (`generate.py` keeps original `index` values for older dumps), 8 (copy not cancellable) and 9 (using order, line endings) skipped; 10 recorded for the user (Waiting on the user below).
- `s255-check-medium.md` (round 3): 1, 2, 3, 4 and 6 closed by one design (run counter and `AcceptCompleted`, round 3 above); 8 applied (`ResumePendingAdvance` stops a paused clip); 5 kept as origin/main behaviour (Pause while a run loads does nothing); 7 and 9 skipped.
- `s255-check2-medium.md`: the five fork findings it lists were refuted on reading the code; its three low items were not applied: 1 (a double sound when a quote already says the word in a language with no sound table), 2 (removed placement tests in `SentenceDumpTests.cs`, see AnimalSoundClipTests), 3 (old premade dumps, see Not yet verified).
- `s255-check3-medium.md` (final check pass after the flow fix): no new findings; `.maestro/dino/animal-clip-plays.yaml` passes on the emulator.
- `s255-check3-medium.md` (the final pass) was named in the brief but does not exist in the reviews folder; no entry is written for it.

## Waiting on the user

- Kept behaviour (not a bug here): Pause while a speech run is still loading does nothing, as on origin/main; with clips the
  gap after each animal sound shows the same Loading state, so a Pause tapped then has no effect until the run plays.
- A line whose own text already says the animal's sound word (e.g. "moo") is read by the voice with no clip (SaysIt), per
  this slice's recorded decision. The user's quote says animal noises should not be AI-voiced: confirm or change.

## Verification

- Unit tests: the sound slot carries the animal kind; dump and generator skip sound entries.
- Android emulator: Three Little Pigs plays a real pig and wolf sound next to their words; iOS compile and the same tests.
- The user's picks written here.

### Device run 2026-11-04 (emulator RTST_Test, build def5fb937bbd, 10-min cap; logs in reviews/s255-device-run/)

- 1. animal-clip-plays flow: FAIL as written (143.9 s): marker `[Reading] read starts story=three-little-pigs` never logged (the open-book hook's `read start` path does not log it; ReadingPage.xaml.cs:206). Product behaviour passes: `[Reading] animal clip Pig plays` logged 3 times in one read, CHUNK lines go on after each, no stall. Flow bug: drop or re-source that expect-log line.
- 2. Pause/Resume/Back during a clip (adb UI taps): PASS. Pause shows Play, no log lines for 8 s, resume continues CHUNKs, Back logs `left the page, read stopped`, no crash.
- 3. Fa: clip plays (`animal clip Pig plays` after `lang=fa` launch). Caveat: the app was already running, so the lang extra may not have been applied ("intent delivered to running instance"); not proven Fa. Treat as not verified.
- 4. leave-book-stops-read flow: PASS (38.6 s).
- 5. Re-run of animal-clip-plays (-NoBuild, build def5fb937bbd): PASS (103.0 s); log `D:/ReadTheStupidText-worktrees/reviews/s255-device-run/rerun.log`.

#### For CLAUDE.md (lifted 2026-11-04)

- `src/DinoReader.App/CLAUDE.md`, replace the "Dino animal sounds (Slice 117)" paragraph: an animal's sound is a bundled CC0 recording (`animal-<kind>.m4a`, 15 animals; bear, lion, rabbit and wolf have none and get no slot), played through `IRecordingPlayer` between speech runs by `ReadingSession`, never spoken. The slot gate is `AnimalSoundClips.Has(kind)`, not a sound-word table. A line that already says the animal's word (SaysIt) keeps the voice and gets no clip. Do not add a clip path to a speech reader. A speech run's `Completed` counts only for the current run seen `Playing` (`AnimalClipRuns.AcceptCompleted`). (Slice 255: docs/plan/slices/255-dino-real-animal-sounds.md)
- `src/DinoReader.Core/CLAUDE.md`, "Animal sounds": `AnimalSounds.Plan` counts each character's own lines, so adding or dropping a slot never moves another character's slot; `SentenceDump` and `tools/premade-audio/generate.py` skip sound entries. (Slice 255: docs/plan/slices/255-dino-real-animal-sounds.md)

#### Not yet verified

- Farsi clip on a device (the device run's `lang=fa` launch may not have been applied to the running app).
- Clip playback on iOS and on the Windows head (compiled only).
- A possible double sound when a quote already says the animal's word in a language without a sound table (check2 #1).
- Old premade dumps (Pigs, Wolf) predate the sound-entry skip and need regenerating before Slice 246 C uses them.
- A fully cached run with no `Playing` state would not advance (same as origin/main).
