# Slice 443 — Dino: a parent's part take plays in place of only its words
**Product:** Dino
**Touches:** `src/Dino/DinoReader.App/**` (`ReadingSession`, `RecordSplitBook.cs`, `StarterUpdate.cs`), `tests/Dino/DinoReader.Tests/**`, `.maestro/dino/**`

*Planned 2026-11-25 (plan-cleanup of that day; not built).*

**Starts after:** Slice 395 (part keys and per-character cards) is merged.

Second half of Slice 395, split out by the plan-cleanup check 2026-11-25. Read Slice 395's Decisions 1-6, its park note and the faults below first; the slice file wins.

## Work
- [ ] 1. Reading screen (`ReadingSession`): a part-keyed clip stands in for its segments only; segments before and after it play the book's premade per-segment track (settled 2026-11-18: no speech engine; a segment with no premade track is silent as today). A sentence-keyed clip plays as today.
- [ ] 2. Store each clip's sentence cover and part range in the clip record when saved; never re-derive covers from story text at load, merge or starter-move time; the overlap rule compares stored covers only.
- [ ] 3. "Listen and change" plays a part's take alone.
- [ ] 4. Fix the known faults from `D:/ReadTheStupidText-worktrees/reviews/pr-1233-starter-medium3.md` (copy them into this file first): `StarterUpdate.cs:944` a part-only move drops the sentence clip; `RecordSplitBook.cs:658` an animal-sound-only take is ignored; an unknown cover must not drop valid recordings; load-time overlap must not drop a newer take. A sentence clip wins where it and a part clip cover the same words; a moved part clip whose page splits differently keeps its cover cleared.
- [ ] 5. Tests on Freddy's pages 2 and 3; a Maestro record flow with the Debug record hooks reading the `[RecordedClip] playing` lines.
- Fresh review gate, both passes reading every file.
