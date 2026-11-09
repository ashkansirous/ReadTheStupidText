# Slice 335: Listening page: Pigs Farsi: four re-rendered lines

*Built 2026-11-09 (branch `fix/334-335-pigs`, merged as PR #991, 3c98f43): fix done, user confirmed by ear on the second listen; verification complete.*

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 246's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Live:** Pigs Farsi: the two `say` lines (گُنده‌ی, «شالاپ، شالاپ، شالاپ!») were re-rendered in the full book on 2026-11-03 but nobody has listened to them in the book; the user heard only the spelling-page variants. The same holds for the two other lines with the same word re-rendered after #766 (Pim's tub line and the narrator's "big smile" line): four lines in all.

**Touches:** `docs/plan/slices/335-pigs-farsi-four-re-rendered-lines.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it: the shipped premade Gemini clips from `content/dino/three-little-pigs/audio/fa/` (Git LFS, manifest v3), not re-rendered: entry 16 (Hali), 62 (Hali), 60 (Achird), 74 (Hali).
- [x] Publish one private listening page: https://claude.ai/artifact/Lh1NricoJb8pCsrT2CfRrY (2026-11-08).
- [x] When the user has answered, write every answer into this file, word for word (below).
- [x] Each "something is wrong" becomes a fix item in this slice (cause, then fix, through the normal review gate). Every "sounds right" closes the check.
- [x] Fix 1: built, second listen on https://claude.ai/artifact/CktvL7hbrcmoXDDDA5dKi5 answered "yes" (2026-11-09, below). *Built 2026-11-09: cause: the `say` for this line in `audio-cast.json` was «شالاپ، شالاپ، شالاپ!» with no vowel marks, so Gemini read the long "shaalaap"; it is now «شَلَپ، شَلَپ، شَلَپ!» ("shalap"), re-rendered as `d9ac949dbca6e52e-Hali-gemini-tts-6de2d2f6.m4a` (was `65b3e84e6c199a3f-...`); fa audio pack v3.* **Fix 1 (from the user's answer):** entry 62, «شالاپ، شالاپ، شالاپ!» (`65b3e84e6c199a3f-Hali-gemini-tts-6de2d2f6.m4a`). The user: "reads is wrong. it says shalaap shalaap shalaap." Find why the premade clip says "shalaap" (the spoken form in the book's hidden how-to-read table or the text sent to Gemini), correct it, re-render the clip, bump the book's audio, and put the new clip on the same page for a second listen.
- Note (review #1, 2026-11-09): the Arabic and Farsi manifests also re-point 20 narrator (Hali) entries each (besides the three wolf lines) to new clips whose names match the current text. Slice 280 changed their text («قال بيب:» to «قال بيب.», «پیپ گفت:» to «پیپ گفت.») but kept the old clips, whose names no longer matched the text hash and failed the check. The new clips are unheard; one Farsi sample is on the listening page https://claude.ai/artifact/CktvL7hbrcmoXDDDA5dKi5.
- Note: the book text and manifest `text` still read «شالاپ»; only the `say` (spoken form) carries the vowels. That is how `say` works (it changes what is spoken, not what is shown), so there is no text change. Whether Gemini honours the marks is decided by the second listen.

## The user's answers (2026-11-08, Done pressed 18:51Z)

- **Wrong:** clip 62 «شالاپ، شالاپ، شالاپ!»: "reads is wrong. it says shalaap shalaap shalaap."
- **Sounds right**, no notes: clip 16 «یه روز غروب، یه گرگ گُنده‌ی خاکستری داشت از جاده میومد.», clip 60 «من یه وان گُنده پر از آب سرد و کف‌کفی گذاشتم زیر دودکش.», clip 74 «پیم با یه لبخند گُنده گفت:».

## The user's second listen (2026-11-09 08:50, https://claude.ai/artifact/CktvL7hbrcmoXDDDA5dKi5)

- **New entry 62 «شَلَپ، شَلَپ، شَلَپ!»:** "yes" (it says شالاپ right).
- **Farsi narrator sample, entry 20 «گرگ گفت.» (new clip):** "sounds right".

## From review

Medium review (`reviews/s3345-medium.md`, findings 6/6 read):

- #1 (~20 new unheard Hali clips in fa/ar): answered in part by the sample listen above (entry 20 sounds right); the other new Hali clips stay unheard.
- #2 (Farsi say with vowel marks may be ignored by TTS): answered by the listen: entry 62 says it right.
- Skipped #3 (`reviews/s3345-medium.md`): "PigsWolfHuffTests passes if all three directions are reverted together or empty" - low severity, not applied this pass (the finding concerns Slice 334's wolf test).
- Skipped #4 (`reviews/s3345-medium.md`): "one English-only calm direction for all 15 languages" - low severity, not applied (Slice 334's wolf direction).
- #5 (stale replaced clips left in folders): applied, see Slice 334's follow-up (52 unreferenced clips deleted, including the old `65b3e84e6c199a3f` splash).
- #6 (Fix 1 bullet merged built note with the old instruction, box unchecked): applied here; box ticked and status line replaced.

## Verification

The user's answers are recorded here. The wrong item (clip 62) has its fix and the second listen answered "yes" on 2026-11-09. `check-audio three-little-pigs` and the premade-audio unit tests pass after the orphaned clips were deleted.
