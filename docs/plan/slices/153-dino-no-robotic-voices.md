# Slice 153 — Dino: no robotic-sounding character voices

*Planned 2026-10-26.* Dino Reader only. **Waits on the user's listening flags** for its second half.

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
- [ ] Rule in `src/DinoReader.Core/CLAUDE.md`: the flavours' pitch limits.

## Verification

- The user's second listen: no flagged line still sounds robotic.
- Emulator: the Ugly Duckling with Mother Duck recast, on an upgraded install.
