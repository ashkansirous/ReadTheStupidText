# Slice 254 — Dino: speaker first in Farsi and Arabic, and animal sounds next to the animal's own words

*Planned 2026-11-03.* Dino Reader only. From the user, listening to the first Gemini book (Three Little Pigs, Farsi):

- "we have a problem with farsi translation. In english, you say xxxx wolf said yyyy. All xxxx and yyyy are clearly for
  the wolf. In farsi, we only say Wolf said then we say xxxx and yyyy continuesly."
- "also, currently when you make animal noises, you make it in the wrong place. the sentense is xxx "wolf said". if the
  wolf is making a noise, it should be connected to his sentece. So either oooo xxx "wolf said" or xxx oooo "wolf said",
  the same if it goes after: "wolf said" oooo yyyy or wolf said yyyy oooo. Currently you say xxxx "wolf said" oooo.
  which is totally worng"

**Touches:** `src/DinoReader.Core/StoryTranslation.cs`, `src/DinoReader.Core/SpeakerFirst*.cs` (new),
`src/DinoReader.Core/AnimalSounds*.cs`, `tests/DinoReader.Tests/**`, `tools/DinoReader.BookBuilder/SentenceDump.cs`,
`content/dino/three-little-pigs/audio-cast.json`, `src/DinoReader.Core/CLAUDE.md`.

## Why (checked in code, 2026-11-03)

- A translation is read piece by piece in English order (`StoryTranslation.TranslateAsync`, `StoryTranslation.cs:30`);
  no language can put the speaker first, reorder or merge pieces. An empty translated piece falls back to English.
- About 64 split quotes (quote, tag, quote) across the 16 store books on main (approximate regex count): jack 8; billy
  goats, elves, freddy, peter rabbit, red riding hood, tortoise, town mouse 5 each; gingerbread, lion, ugly duckling 4;
  little red hen, three little pigs 3; goldilocks 2; x-marks 1.
- Animal sounds are inserted after the tag today ("xxx" wolf said "oooo"), cutting the sound off from the animal's words.

## Decisions (coordinator, from the user's words; change if wrong)

- **Speaker-first languages: Farsi and Arabic** to start (both put «X گفت» / «قال X» first). A1 checks the other
  translated languages (ja, zh and the rest) and adds any that read naturally speaker-first, writing the evidence here.
- In a speaker-first language a dialogue sentence reads **tag, then the whole quote**: quote-tag-quote becomes
  tag, quote1, quote2; quote-tag becomes tag, quote. Each piece keeps its own speaker (narrator for the tag, the
  character for the quote), voice and highlight. A tag moved first ends with a colon instead of a full stop.
- **An animal sound always sits next to its animal's own words, never across a tag**: before or after the quote, on
  the side with no tag between. Allowed: sound quote tag / quote sound tag / tag sound quote / tag quote sound. Never:
  quote tag sound. This applies in every language, English included.
- Pre-made Gemini audio follows automatically: the BookBuilder dump uses the same Core code, clips are keyed by text,
  so only reordered pieces whose text changed (the tag's colon) get a new clip. Three Little Pigs Farsi is re-rendered
  for those few lines (a few pence) and its `audio-cast.json` index directions re-checked.

## Work

- [ ] A1. Speaker-first rule in Core (`SpeakerFirst`), a per-language list (fa, ar, plus any A1 finds), applied in
  `StoryTranslation` after translation; unit tests for quote-tag-quote, quote-tag, tag-quote (unchanged), narration-only
  (unchanged), and a 200-character run split.
- [ ] A2. Sound placement fix in `AnimalSounds` placement: sound adjacent to its quote; tests for every pattern above
  in English and Farsi.
- [ ] A3. Highlight and pagination still follow each piece (tests on the piece ranges); the reading screen unchanged
  otherwise. Android, iOS and the Windows head share this Core code; the emulator run checks Android, and an iOS
  compile (`-p:IncludeIos=true -p:IncludeWindows=false -p:IncludeAndroid=false`) checks iOS builds.
- [ ] A4. BookBuilder `dump-sentences` gives the new order (test); re-dump Pigs fa, re-key `audio-cast.json`, re-render
  the changed lines, update the D1 listening page.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` with the new cases.
- Android emulator: Three Little Pigs in Farsi reads «پیم گفت:» then the whole quote, and the wolf's sound stays next to
  his words; English Pigs: sound next to the quote, not after "said Wolf". iOS: compile, and the same Core tests.
