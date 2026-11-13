# Slice 332: Listening page: Double animal sound when a quote names the animal
**Product:** Dino

*Verified (the user, 2026-11-08: all sound right; every item ticked).*

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 255's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Android:** A possible double sound when a quote already says the animal's word in a language without a sound table (check2 #1).

**Touches:** `docs/plan/slices/332-double-animal-sound-when-a-quote-names-the-anima.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it: the shipped premade Gemini clips (Git LFS, origin/main d12cb1a7, manifest order) plus the bundled `animal-sounds/frog.m4a` where the app adds it, joined with no gap (the app plays the animal clip right after the last premade piece). A first version of the page used on-device sherpa voices by mistake; the user noticed ("it seems to be using the old tts engine") and it was rebuilt. Which lines get an animal clip and the Farsi `SaysIt` no-match were worked out from the code, not run.
- [x] Publish one private listening page: https://claude.ai/artifact/3i6zzScCcsXVGkvZyRr6yi (version 2). Only two animal lines in the 15 store books say their own sound: the Frog in tortoise-and-hare and the Hen in ugly-duckling. The real gap is a sound spelled in a way the table does not list (every language has a sound table, so the slice's "a language without a sound table" does not occur): the Farsi Frog writes «قور قور!» as two words, the table has «قورقور», so the app plays frog.m4a after the voice.
- [x] When the user has answered, write every answer into this file, word for word (below).
- [x] Each "something is wrong" becomes a fix item in this slice. None was wrong, so the check is closed.

## The user's answers (2026-11-08, Done pressed 19:25Z, page version 2)

All four **sound right**, no notes: en-frog-ribbit "Ribbit! Ribbit! Ribbit!", en-hen-cluck "Cluck, cluck, cluck!", fa-frog-ghor «قور قور!» followed by the frog recording, fa-hen-ghod «قد قد قدا!». So the Farsi voice-plus-recording sounds fine to the user and needs no change. (Docs `en1`-`en3` in the page's database are answers to the withdrawn version 1 and do not count.)

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
