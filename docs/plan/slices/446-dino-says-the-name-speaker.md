# Slice 446 — Dino: "says the <name>" gives the line to the right speaker
**Product:** Dino
**Touches:** `src/Dino/DinoReader.Core/**`, `tests/Dino/DinoReader.Tests/**`

*In progress (wave 2026-11-27), draft PR.* Premise check at claim: the parser is `src/Dino/DinoReader.Core/SpeakerTagParser.cs:48` (verb then name pattern from `NameExtractor.cs`; "the" is a non-name word, `NameExtractor.cs:155`), likely why `says the Referee` fails.

*Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 350; not built).*

**Why.** Good Enough's text had `says the Referee`; the lines went to Carrot, so the book uses "says Referee" and "says Crowd" as a workaround (Slice 350 side finding, moved out 2026-11-25).

**Decision (Start pass 2026-11-27, obvious choice):** the last item (Good Enough text back to "the referee") is the owner's book lane: record the choice, change no book text.

**Work.**
- [ ] Reproduce in `tests/Dino/DinoReader.Tests`: a story with `says the Referee` attributes the line to the character named Referee (find the speaker parser in `src/Dino/DinoReader.Core`; a first grep found no obvious "says" parser, so locate it first).
- [ ] Fix it so a leading "the" is skipped; test passes; no change to other attributions.
- [ ] Then decide whether Good Enough goes back to "the referee" (text change and version bump, user's book lane: record the choice, do not redraw pictures).
