# Slice 446 — Dino: "says the <name>" gives the line to the right speaker
**Product:** Dino
**Touches:** `src/Dino/DinoReader.Core/**`, `tests/Dino/DinoReader.Tests/**`

*Built 2026-11-27 (wave 2), PR #1396, not merged. Pure Core change, nothing to check on a device.* Premise check at claim: the parser is `src/Dino/DinoReader.Core/SpeakerTagParser.cs:48` (verb then name pattern from `NameExtractor.cs`; "the" is a non-name word, `NameExtractor.cs:155`), likely why `says the Referee` fails.

*Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 350; not built).*

**Why.** Good Enough's text had `says the Referee`; the lines went to Carrot, so the book uses "says Referee" and "says Crowd" as a workaround (Slice 350 side finding, moved out 2026-11-25).

**Decision (Start pass 2026-11-27, obvious choice):** the last item (Good Enough text back to "the referee") is the owner's book lane: record the choice, change no book text.

**Work.**
- [x] Reproduce in `tests/Dino/DinoReader.Tests`: a story with `says the Referee` attributes the line to the character named Referee (find the speaker parser in `src/Dino/DinoReader.Core`; a first grep found no obvious "says" parser, so locate it first).
- [x] Fix it so a leading "the" is skipped; test passes; no change to other attributions.
- [x] Then decide whether Good Enough goes back to "the referee" (text change and version bump, user's book lane: record the choice, do not redraw pictures). Recorded: left to the owner's book lane per the Decision above; no book text changed in this slice.

*Changed while building:* English `NameArticle` (`(?:the\s+)?`) added to `EnglishRules` / `LanguageRules` (empty for other languages, so their regexes are unchanged). `TagPatterns.Named` accepts "the <Name>" except before a colon or when it would cut a capitalised run; used by `SpeakerTagParser` and `NameExtractor`. Tests in `SpeakerAttributorTests.cs`: says/said the Referee, "the Referee says", multi-word (Little Red Hen), across a line break, and "said the Magic Words:" is not a speaker. Dino tests: 4938 passed. All 15 English shipped books give the same speaker for every quote with the rule on and off (medium review check).

*Known limit:* "Lucy said the Pledge." (no colon) still adds Pledge as a name.

#### From review

High review (`reviews/pr-1396-high.md`): findings 1, 2 and 3 fixed. Medium review (`reviews/pr-1396-medium.md`): none applied. Lines below give the reasons.

- Skipped #1 (`reviews/pr-1396-medium.md`): "still yields a name" - the known limit of this slice; no shipped book has the pattern (medium check over the 15 English books).
- Skipped #2 (`reviews/pr-1396-medium.md`): "misses a newline before the colon" - no shipped book puts a line break between the name and its colon.
- Skipped #3 (`reviews/pr-1396-medium.md`): "Reword the comment" - doc comment wording only, no behaviour changes.
- Skipped #4 (`reviews/pr-1396-high.md`): "The article is lowercase-only while the verbs use `(?i:...)`." - no failing input; a capitalised "The" is already stripped as a non-name word.
