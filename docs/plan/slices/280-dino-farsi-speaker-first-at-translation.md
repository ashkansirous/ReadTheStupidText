# Slice 280 — Dino: Farsi speaker first, built at translation time from the pieces we already have

*Planned 2026-11-06.* Dino Reader only. Replaces the read-time regroup of Slice 254
([254](254-dino-speaker-first-and-sound-placement.md)).

From the user, 2026-11-06:

- "in farsi, we dont say "I am ok" he said "what about it". We say he said "I am ok. what about it". I told you before
  but instead of fixing the story, you changed the reading of it. you should apply this to the translation time, not
  when reading"
- "you shouldnt need to retranslate the same text. you have "part a." he said "part b.". you should be able to just
  concat them and say he said "part a. part b." both in text and then should also be able to do the same with the
  audio that was generated. no re-recording"

**Touches:** `src/DinoReader.Core/SpeakerFirst.cs`, `src/DinoReader.Core/StoryTranslation.cs`,
`src/DinoReader.Core/TranslatedStory.cs`, `src/DinoReader.Core/BundledTranslations.cs`,
`src/DinoReader.Core/AnimalSounds*.cs`, `src/DinoReader.App/StoryTranslator.cs`,
`tools/DinoReader.BookBuilder/**`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`,
`.claude/skills/dino-storytelling/SKILL.md`.

## Why

Slice 254 (my mistake) kept each Farsi piece in the English order in the translated story and regrouped the pieces
only when reading (`SpeakerFirst.Regroup`, called from `StoryTranslator` after `StoryTranslation.TranslateAsync`).
The user asked for the translation itself to say «پیم گفت: «A. B.»», not for the reader to rearrange it. The
translations and the recorded clips we already have are enough: the tag, part A and part B each exist as a
translated piece and as a clip.

## Decisions (from the user's words)

- **No re-translation, no re-recording.** The Farsi line is made by joining the existing translated pieces
  (tag, then A, then B) and its audio by playing the existing clips in that same order. No new Gemini request, no
  new entry in any `fa.json`.
- **Done once, when the translated story is built**, so the text the reader shows is already in Farsi order and
  the voice, the highlight and the page text all follow that one order. Nothing reorders while reading.
- Each part keeps its own voice: the tag in the narrator's, A and B in the character's.
- The tag reuses its existing clip as is; any colon shown in the text is display only and never changes the key
  a clip is looked up by.
- Arabic was added to Slice 254 by me, not asked for by the user. It moves to the same translation-time join, so
  removing the read-time code does not send it back to the split order (coordinator call; change if wrong).

## Work

- [ ] A1. Translation-time join in Core: when a story is translated into a speaker-first language, build the
  translated story with each split quote (A, tag, B) and each quote-tag (A, tag) as tag + A (+ B), using only the
  existing translated pieces. The same function serves the app and the BookBuilder dump. Tests: quote-tag-quote,
  quote-tag, tag-quote and narration unchanged, a quote spanning two sentences, and every split quote in the 16
  store books' Farsi files joins without a missing piece.
- [ ] A2. Audio reuse: each part of a joined line plays the clip it already has (key unchanged, including the tag's
  original stop). Remove the colon-tag clips Slice 254 added if nothing else uses them. Test: Three Little Pigs
  Farsi needs zero new clips (the dump reports every piece found in `audio-cast.json`).
- [ ] A3. Remove the read-time regroup: `StoryTranslator` no longer calls `SpeakerFirst.Regroup`; `SpeakerFirst`
  keeps only what A1 needs (or goes). Animal sounds read the joined order from the translated story, not from
  `SpeakerFirst.SourceOrder`; Slice 254's placement tests still pass.
- [ ] A4. The highlight and pagination follow the joined text (piece ranges are those of the joined story).
  Android, iOS and the Windows head share this Core code; Android is checked on the emulator, iOS by compile
  (`-p:IncludeIos=true -p:IncludeWindows=false -p:IncludeAndroid=false`) and the same Core tests.
- [x] A5. Skill wording: `dino-storytelling` section 3 says the join is made from the existing pieces at
  translation time, no re-translation or re-recording (done in the planning PR). When A3 merges, drop its
  "Until Slice 280 ships" sentence.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- BookBuilder `dump-sentences three-little-pigs fa` shows «پیم گفت:» then both halves, with no missing clip.
- Android emulator: Three Little Pigs in Farsi shows and reads the tag first, highlight in order. iOS: compile.
