# Slice 335: Listening page: Pigs Farsi: four re-rendered lines

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 246's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Live:** Pigs Farsi: the two `say` lines (گُنده‌ی, «شالاپ، شالاپ، شالاپ!») were re-rendered in the full book on 2026-11-03 but nobody has listened to them in the book; the user heard only the spelling-page variants. The same holds for the two other lines with the same word re-rendered after #766 (Pim's tub line and the narrator's "big smile" line): four lines in all.

**Touches:** `docs/plan/slices/335-pigs-farsi-four-re-rendered-lines.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it: the shipped premade Gemini clips from `content/dino/three-little-pigs/audio/fa/` (Git LFS, manifest v3), not re-rendered: entry 16 (Hali), 62 (Hali), 60 (Achird), 74 (Hali).
- [x] Publish one private listening page: https://claude.ai/artifact/Lh1NricoJb8pCsrT2CfRrY (2026-11-08).
- [x] When the user has answered, write every answer into this file, word for word (below).
- [ ] Each "something is wrong" becomes a fix item in this slice (cause, then fix, through the normal review gate). Every "sounds right" closes the check.
- [ ] **Fix 1 (from the user's answer):** entry 62, «شالاپ، شالاپ، شالاپ!» (`65b3e84e6c199a3f-Hali-gemini-tts-6de2d2f6.m4a`). The user: "reads is wrong. it says shalaap shalaap shalaap." Find why the premade clip says "shalaap" (the spoken form in the book's hidden how-to-read table or the text sent to Gemini), correct it, re-render the clip, bump the book's audio, and put the new clip on the same page for a second listen.

## The user's answers (2026-11-08, Done pressed 18:51Z)

- **Wrong:** clip 62 «شالاپ، شالاپ، شالاپ!»: "reads is wrong. it says shalaap shalaap shalaap."
- **Sounds right**, no notes: clip 16 «یه روز غروب، یه گرگ گُنده‌ی خاکستری داشت از جاده میومد.», clip 60 «من یه وان گُنده پر از آب سرد و کف‌کفی گذاشتم زیر دودکش.», clip 74 «پیم با یه لبخند گُنده گفت:».

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
