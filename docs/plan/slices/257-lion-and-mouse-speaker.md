# Slice 257 — Dino: a quote after "said Mouse." is the Mouse's, not the Lion's (Lion and the Mouse)

*Planned 2026-11-03.* Dino Reader only (Core), so Android, iOS and the Windows head alike. Found while casting the store
books for Slice 246: in Lion and the Mouse the Mouse's line "I didn't mean to wake you up. If you're kind to me today,
I'll be kind to you one day, too!" (the part after "said Mouse.", in the paragraph where she says "Mister Lion") is given
to the Lion by the app's own speaker attribution, in both English (dump index 14) and Farsi (13). So the on-device voice
reads it in the Lion's voice today, and the Gemini clip rendered on 2026-11-03 uses the Lion's voice (Algenib).

**Touches:** `src/DinoReader.Core/SpeakerAttributor.cs`, `src/DinoReader.Core/AttributionRun.cs`,
`src/DinoReader.Core/SpeakerTagParser.cs`, `tests/DinoReader.Tests/**`.

## Work

- [ ] 1. Find why the continuation quote after a mid-paragraph tag ("...," said Mouse. "I didn't mean ...") is attributed to the
  wrong speaker; fix it in Core with a test built from this paragraph, and run the whole Dino suite (other books' attributions
  must not change, or each change is listed and checked).
- [ ] 2. Re-dump Lion and the Mouse (fa and en) and re-render the changed line(s) in Farsi with `generate.py --batch` into
  `D:/ReadTheStupidText-worktrees/s246-audio/books-lion-and-mouse/` (the cache re-renders only pieces whose voice changed).

## Verification

- The new Core test; the full `tests/DinoReader.Tests` suite; the re-render ending with 0 left.
