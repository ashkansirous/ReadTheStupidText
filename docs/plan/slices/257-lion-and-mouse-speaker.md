# Slice 257 — Dino: a quote after "said Mouse." is the Mouse's, not the Lion's (Lion and the Mouse)

*Planned 2026-11-03.* Dino Reader only (Core), so Android, iOS and the Windows head alike. Found while casting the store
books for Slice 246: in Lion and the Mouse the Mouse's line "I didn't mean to wake you up. If you're kind to me today,
I'll be kind to you one day, too!" (the part after "said Mouse.", in the paragraph where she says "Mister Lion") is given
to the Lion by the app's own speaker attribution, in both English (dump index 14) and Farsi (13). So the on-device voice
reads it in the Lion's voice today, and the Gemini clip rendered on 2026-11-03 uses the Lion's voice (Algenib).

*In progress (wave 2026-11-03), draft PR.*

**Touches:** `src/DinoReader.Core/SpeakerAttributor.cs`, `src/DinoReader.Core/AttributionRun.cs`,
`src/DinoReader.Core/SpeakerTagParser.cs`, `src/DinoReader.Core/SentenceBoundaries.cs`, `src/DinoReader.Core/*Rules.cs`,
`tests/DinoReader.Tests/SpeakerAttributor*Tests.cs`, `docs/plan/slices/257-lion-and-mouse-speaker.md`.

**Claim note (coordinator, night 2026-11-03-2349 wave 1):** likely cause, read from code, not yet run: after `said Mouse.` the
narration ends a sentence, so `TagBefore` is null (`SentenceBoundaries.UnfinishedLast`), and `FromTag` then takes `TagAfter` from
the next narration "The Lion laughed ... said Lion." (`AttributionRun.cs:49-59`, `laughed` is a speech verb in `EnglishRules.cs`).
The builder proves the cause with the failing test first. Fix direction: a quote that directly follows a finished tag sentence
in the same paragraph ("..." said Mouse. "...") continues that speaker before any tag after it is tried; it must hold in Farsi
too (fa index 13). Every other book's attribution is compared before and after with the BookBuilder dump; each change is
listed here. Item 2's re-render needs the Gemini key and the s246-audio folder; if either is missing, item 2 goes to *Not yet
verified*.

## Work

- [ ] 1. Find why the continuation quote after a mid-paragraph tag ("...," said Mouse. "I didn't mean ...") is attributed to the
  wrong speaker; fix it in Core with a test built from this paragraph, and run the whole Dino suite (other books' attributions
  must not change, or each change is listed and checked).
- [ ] 2. Re-dump Lion and the Mouse (fa and en) and re-render the changed line(s) in Farsi with `generate.py --batch` into
  `D:/ReadTheStupidText-worktrees/s246-audio/books-lion-and-mouse/` (the cache re-renders only pieces whose voice changed).

## Verification

- The new Core test; the full `tests/DinoReader.Tests` suite; the re-render ending with 0 left.
