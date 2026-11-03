# Slice 254 — Dino: speaker first in Farsi and Arabic, and animal sounds next to the animal's own words

*In progress 2026-11-03.* Dino Reader only. From the user, listening to the first Gemini book (Three Little Pigs, Farsi):

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

- [x] A1. Speaker-first rule in Core (`SpeakerFirst`), a per-language list (fa, ar, plus any A1 finds), applied in
  `StoryTranslation` after translation; unit tests for quote-tag-quote, quote-tag, tag-quote (unchanged), narration-only
  (unchanged), and a 200-character run split.
  **Other languages checked (existing Pigs and all-book zh tables), none added:** ja tags are "と、ピムは言いました。" (a
  quotative that only reads after the quote); de, da, nb, sv, nl, fi, fr, es, pt and ru tags are verb-first ("sagte der
  Wolf.", "dijo Pim.", "сказал Волк."), which moved to the front would read "Said the wolf: ..."; zh is subject-verb
  ("皮姆说。") but many tags carry an action clause ("山妖说着，咚咚咚地爬上了桥。") that a colon would make wrong. So only fa and ar.
- [x] A2. Sound placement fix in `AnimalSounds` placement: sound adjacent to its quote; tests for every pattern above
  in English and Farsi.
- [x] A3. Highlight and pagination still follow each piece (tests on the piece ranges); the reading screen unchanged
  otherwise. Android, iOS and the Windows head share this Core code; the emulator run checks Android, and an iOS
  compile (`-p:IncludeIos=true -p:IncludeWindows=false -p:IncludeAndroid=false`) checks iOS builds.
- [ ] A4. BookBuilder `dump-sentences` gives the new order (test); re-dump Pigs fa, re-key `audio-cast.json`, re-render
  the changed lines, update the D1 listening page. *Code part done (dump test); re-key, re-render and listening page are the coordinator's.*
- [x] A5. Attributor fix: sound-making verbs (sneezed, coughed, growled, barked, oinked, ...) added to the English speech verbs, so `"Achoo!" sneezed Wolf.` (Pigs) is Wolf's, not Pop's; tests added.

**Changed while building:** the regroup runs in `StoryTranslator` (App) right after `StoryTranslation.TranslateAsync`, not inside it, because the language and the story's sentences are only known there; a quote's pieces are regrouped across sentences (the Pigs' "A strong house takes time," said Pim, "I'll play..." is two sentences), but a tag never jumps in front of a quote piece of an earlier sentence, and `AnimalSounds.Placements` lost its `sentences` argument (the sound now sits straight after the quote, the last piece of a same-speaker run).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` with the new cases.
- Android emulator: Three Little Pigs in Farsi reads «پیم گفت:» then the whole quote, and the wolf's sound stays next to
  his words; English Pigs: sound next to the quote, not after "said Wolf". iOS: compile, and the same Core tests.

## Built (2026-11-03)

*Built, not yet verified on a device.* A1-A3 and A5 done; A4's re-render waits for the merge. 2,320 Dino tests pass;
Android and iOS builds 0 errors. The BookBuilder dump of Three Little Pigs (same Core code as the app) shows: fa
«پیم گفت:» before both halves of Pim's line and «گرگ گفت:» before the wolf's words; no sound after a tag in fa or en;
"Achoo!" now Wolf's (A5: only that speaker changed across all 16 books; Pop loses one planned "Oink!", 86 pieces).
*From review:* `D:/ReadTheStupidText-worktrees/reviews/s254-review-high.md` (11; 1-3, 5-8, 11 applied, 4 and 9 skipped
low), `s254-review-medium.md` (5; 2, 3, 5 applied, 1 and 4 not bugs), `s254-review-check-medium.md` (2 low, skipped:
the dump regroups only on a complete table while the app also does with an incomplete one; non-speech verbs such as
"huffed" as tags, checked by the 16-book dump diff).
Emulator check not run: no log marker carries the spoken piece text or voice, and voice start crashes the Dino Debug
x86_64 build (`tools/CLAUDE.md`).

#### Not yet verified

- On a device: Three Little Pigs in Farsi reads «پیم گفت:» then the whole quote and «گرگ گفت:» before the wolf's words;
  English: the wolf's sound before "said Wolf", "Achoo!" in the wolf's voice.

#### Outstanding

- A Debug marker that logs each spoken piece (index, text, voice id), so device runs can check reading order and voice.
