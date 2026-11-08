# Slice 331: Listening page: Animal sound word already in the text (moo)

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 255's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Live:** User listening check (2026-11-06, "I need to think about it"): a line whose own text already says the animal's sound word (e.g. "moo", `SaysIt`) is read by the voice with no clip, per the slice's recorded decision; the user's quote says animal noises should not be AI-voiced, so the user listens and confirms or changes it.

**Touches:** `docs/plan/slices/331-animal-sound-word-already-in-the-text-moo.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it: the store books' shipped premade Gemini clips (voice Laomedeia), found by scanning the en and fa premade-audio manifests of all 15 store books; the bundled real frog and hen recordings were on the page for comparison only.
- [x] Publish one private listening page: https://claude.ai/artifact/FTSUBWyRguC6ALM1nHA8dh (2026-11-08; four lines: Frog "Ribbit!" and Hen "Cluck, cluck, cluck! Can you lay an egg?" in English, Frog «قور قور!» and Hen «قد قد قدا! تو می‌تونی تخم بذاری؟» in Farsi).
- [x] When the user has answered, write every answer into this file, word for word (below).
- [x] Each "something is wrong" becomes a fix item in this slice. None was wrong, so the check is closed.

## The user's answers (2026-11-08, Done pressed 18:44Z)

All four **sound right**, no notes: en-frog "Ribbit!", en-hen "Cluck, cluck, cluck! Can you lay an egg?", fa-frog «قور قور!», fa-hen «قد قد قدا! تو می‌تونی تخم بذاری؟». The voice saying an animal's own sound word, with no recording, is confirmed.

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
