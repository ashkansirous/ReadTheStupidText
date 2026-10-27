# Slice 153 — Dino: no robotic-sounding character voices

*In progress (wave 2026-10-26b), draft PR.* Dino Reader only. **Waits on the user's listening flags** for its second half.

**Batch:** 31 (tester review, 2026-10-26; Decision 158)
**Touches:** `tools/voice-listen/**` (new), `src/DinoReader.Core/VoiceFlavour.cs`, `src/DinoReader.Core/FlavourTransform.cs`, `src/DinoReader.Core/CharacterRoster*.cs`, `src/DinoReader.Core/StarterUpdate.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`

## Why

The tester heard robotic voices, even in English. One example is Mother Duck in the Ugly Duckling store book. The likely
cause is the flavours' pitch shift on top of a base voice: Slice 113 already noted "pitch by ear, drop it if
robotic" as not yet verified. Which roster slots sound robotic is a judgement made by ear.

## Decisions locked with the user (Decision 158)

1. Claude builds a **listening page**: one short line from every English roster character (Supertonic-3 and
   Kokoro `kokoro-en`, each voice and flavour as cast), labelled by character name. It also includes the voice
   Mother Duck is actually cast to.
2. The **user flags** the robotic ones.
3. Claude tones down or removes the pitch shift of the flagged flavours, or moves the character to a cleaner
   slot. Any character cast to a flagged slot is recast, Mother Duck included. Un-edited starter members are
   recast by `StarterUpdate`; grown-up picks (`Edited`) are kept.
- The other languages get the same page later, only if English shows flavours are the cause.

## Work

- [ ] `tools/voice-listen`: render the English roster lines to WAV (reuse `tools/sound-words-test` engines),
      plus an HTML page to play them. Publish the page for the user. Record the Ugly Duckling cast.
- [ ] Ask the user for flags (blocker: user listen).
- [ ] Apply: change the flavour transforms or roster slots. The roster tests (`StorytellerCharacterTests`) stay green.
- [ ] Re-render the flagged lines for a second listen.
- [x] Pitch-limits rule in `src/DinoReader.Core/CLAUDE.md`: dropped, the listen showed the base voice (`bf_emma`), not the flavours, was the cause.

## Verification

- The user's second listen: no flagged line still sounds robotic.
- Emulator: the Ugly Duckling with Mother Duck recast, on an upgraded install.

#### Design (Dino Reader Batch 31 mock, no screens, added 2026-10-26)

Batch 31 (`docs/design/dino-reader/Dino Reader Batch 31.dc.html`) has no screen for this slice. The listening page it builds is a dev tool, not app UI. Nothing to implement from the mock.

## User verdict and fix (2026-10-27)

The user listened: "Splashy Duckling" (`duckling2`) and "Kind Mummy" (`mummy`) are robotic both as cast and with no flavour, so the base voice `bf_emma` is the problem, not the flavours. Everything else, including the five Ugly Duckling voices, is fine.
- English roster: `mummy`, `hen` (the third `bf_emma` place) and `duckling2` moved off `bf_emma` to the best-graded unused Kokoro English girls (VOICES.md, all C+): `mummy` -> `af_sarah` (sid 9), `hen` -> `af_aoede` (sid 1), `duckling2` -> `af_kore` (sid 5). `bf_emma` stays in the storyteller picker (`KokoroVoiceTable`) but no roster place uses it; a grown-up's Edited pick of it is kept (tested), an un-edited one is re-picked. An Edited pick stands only in the Kokoro language it was made for (the book's choices share that language's voice pool); an edited English voice left on a Spanish book is re-picked (tested).
- Recast: books whose authored cast names those places got a version bump so installed copies refresh. Starters: Gingerbread Man 7 -> 8, Tortoise and Hare 8 -> 9 (`.dino` rebuilt). Store (built with `DINO_BUILD_STORE=1`, not uploaded): Goldilocks 9, Peter Rabbit 9, Red Riding Hood 8, Ugly Duckling 8, Little Red Hen 8, Jack and the Beanstalk 8, Elves and the Shoemaker 8, X Marks the Treasure! 6.
- [ ] The user's second listen of the moved places (`tools/voice-listen/out/index.html`).
