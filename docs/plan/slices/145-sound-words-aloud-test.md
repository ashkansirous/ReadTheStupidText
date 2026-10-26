# Slice 145 — Which sound-word spellings sound right when read aloud

*Built 2026-10-26 (wave PR #385), not yet verified: the user's listen is still open. Runs before Slice 146.*

**Batch:** 30 (Decisions 149-153)
**Touches:** `tools/sound-words-test/**` (new), `docs/plan/notes/2026-10-26-sound-words-aloud.md` (new), `.claude/skills/dino-storytelling/SKILL.md`

## Why

Sound words ("Dang!", "Woosh!", "Oink!") are short and can sound clipped from a TTS voice. The user
asked whether stretched spellings ("Daaannng", "Woooooshhhh") help. No engine normalises repeated
letters; whether each voice stretches, shortens or garbles them is unknown and differs per engine
(Supertonic, Kokoro, Piper/espeak) and per language.

## Decisions (locked with the user, 2026-10-26)

- **Decision 151:** measure before rewriting any book. The winning patterns per language become the
  sound-word list in `dino-storytelling`; until then the skill uses repetition with punctuation
  ("Dang! Dang! Dang!", "Whoosh… whoosh!"), which every engine reads reliably.

#### Design (Dino Reader Batch 30 mock, added 2026-10-26)

No screens. The Batch 30 mock intro says 145 (the sound-words test) changes no screens. Nothing to build from the mock.

## Work

- [x] A small offline harness (desktop sherpa-onnx with the same models and settings as the apps) that
      synthesises ~6 spellings each of a bang, a whoosh, a knock and an animal sound, per voice, in
      English, Farsi and 2-3 other languages (Spanish, German, Chinese). For example `Dang!`, `Daaang!`,
      `Daaannng!`, `Dang! Dang!`, `Dang… dang… dang!`; Farsi «دَنگ!», «دنگگگ!», «دَنگ! دَنگ! دَنگ!»,
      «ووووش!», «ووش… ووش!».
- [x] Record each clip's spoken length and flag garbled ones (read letter by letter, or skipped);
      write the table and a shortlist of WAVs to the note.
- [ ] The user listens to the shortlist and picks; write the per-language list and the rule into
      `dino-storytelling` section 3. (Provisional picks and the rule are in the skill; the user's listen
      replaces the provisional picks. Listen set: `D:\ReadTheStupidText-worktrees\sound-words-listen\`, outside the repo.)

## Verification

- The note has durations for every spelling and voice; the user's picks are in the skill.

*Changed while building:* the harness is `tools/sound-words-test` (sherpa-onnx 1.13.3, packs-7 models, the apps' chunking plus 150 ms gaps, 5 takes per spelling, exact-token Whisper check for garbling). About 4000 clips in en, es, zh and fa. Findings: repetition with punctuation is reliable; letter stretching helps little and can hurt. Piper mana drops short Farsi words (26% alone, 8% in a sentence); Slice 149 fixes that in code. The skill rule was cut to what all voices support. Full table: `docs/plan/notes/2026-10-26-sound-words-aloud.md`.

*From review:* 145-high (15 findings applied), 145-medium (14 findings applied).

#### For CLAUDE.md (lifted 2026-10-26)

- Sound words in Dino books: use repetition with punctuation ("Dang! Dang! Dang!", "Whoosh… whoosh!"); do not stretch letters ("Daaannng"), which helps little and can hurt on some voices. (Slice 145: docs/plan/slices/145-sound-words-aloud-test.md)

#### Not yet verified

- A human listen of the `sound-words-listen` set, Farsi and Chinese especially (the Farsi whoosh and cat picks are provisional).
- German, French and Portuguese were not tested.
