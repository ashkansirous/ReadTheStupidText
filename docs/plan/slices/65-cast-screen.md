# Slice 65 — Cast screen

*Planned.*

**Batch:** 10
**Decisions:** 74, 75, 77
**Depends on:** Slices 59, 60, 61
**Touches:** `src/DinoReader.App/**`, `src/DinoReader.Core/**`, `tests/DinoReader.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

Behind the grown-up gate, a parent sees the detected characters with gender and voice,
fixes mistakes and merges two names that are one character. Changes apply for the rest of
the book and are remembered.

## Steps

- [ ] Cast page (gated by Slice 61's gate): list of characters (name, gender, voice, words
      spoken), narrator picker on top, and a row for "unknown speaker".
- [ ] Per row: voice **preview** button (speaks a fixed line in that voice), gender toggle
      (re-runs `VoiceCaster` for that character within the gender), voice dropdown.
- [ ] **Merge** two characters: pick both, keep one name; the other's segments are
      re-attributed; the merged character keeps the first voice.
- [ ] Persist the edited cast in `story.json` (Slice 61). An edit is a **pin**: re-running the
      caster never overrides a pinned voice.
- [ ] Reading uses the pinned cast from the next segment on, even when the story is open
      mid-read (the Decision 29 chunk-boundary swap).
- [ ] Define the "unknown speaker" voice rule (default: the narrator voice, never a
      character's) and record it here.
- [ ] Tests (Core): pinning, merge re-attribution, a gender change re-casts without touching
      other pins.

## Acceptance

- Change Cotton Tail's voice, then read: her lines use the new voice from then on, also after
  an app restart.
- Merge "the rabbit" into "Peter": one entry, one voice, all lines re-attributed.
- The screen is unreachable without the gate.

## Risks / notes

- If common books need many fixes this screen becomes the main UX (the kill criterion in
  Decision 74): measure on 5 sample kids' books.
