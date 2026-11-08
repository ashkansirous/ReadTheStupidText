# Slice 334: Listening page: Speaker first in Farsi (Three Little Pigs) and the wolf's sound in English

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 254's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Live:** On a device: Three Little Pigs in Farsi reads «پیم گفت:» then the whole quote and «گرگ گفت:» before the wolf's words; English: the wolf's sound before "said Wolf", "Achoo!" in the wolf's voice.

**Touches:** `docs/plan/slices/334-speaker-first-in-farsi-three-little-pigs-and-the.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it: the shipped premade Gemini clips from `content/dino/three-little-pigs/audio/{fa,en}` at origin/main 011d772e (Git LFS), not re-rendered. The wolf has no animal recording in the app (`WithoutClip` in `AnimalSoundClips.cs`), so no wolf sound plays next to "said Wolf"; only his spoken "Achoo!".
- [x] Publish one private listening page: https://claude.ai/artifact/Cj4gG4fk3Y1maLsVbGCMto (2026-11-08; 8 Farsi scenes, 6 English scenes).
- [x] When the user has answered, write every answer into this file, word for word (below).
- [ ] Each "something is wrong" becomes a fix item in this slice (cause, then fix, through the normal review gate). Every "sounds right" closes the check.
- [ ] **Fix 1 (from the user's answer):** English clip 24, the wolf (voice Elio): "Then I'll huff, and I'll puff, and I'll blow your house in!" The user: "too much feeling. somewhat scary for a little one". Re-render this clip calmer and less scary for ages 3-9 (premade-audio tooling, `tools/premade-audio/`), bump the book's audio, and put the new clip on the same page for a second listen. **In every language the book ships, not only English** (user, 2026-11-08: "remember to do the fix for all languages. If it is too scary in english, is probably too scary in french, farsi and others too"): the same huff-and-puff line in all 15 audio folders (ar, da, de, en, es, fa, fi, fr, ja, nb, nl, pt, ru, sv, zh), and any other wolf line rendered with the same delivery. The second listen covers English and Farsi (the listening-page languages).

## The user's answers (2026-11-08, Done pressed 18:50Z)

Everything the page asked was marked **sounds right** except one clip.

- **Wrong:** English 24, "Then I'll huff, and I'll puff, and I'll blow your house in!": "too much feeling. somewhat scary for a little one".
- **Sounds right, Farsi (speaker first, then the quote):** 13 «پیم گفت.», 14, 15; 20 «گرگ گفت.», 21; 22 «پیپ گفت.», 23; 24 «گرگ گفت.», 25; 54 «گرگ گفت.», 55; 56 «پاپ گفت.», 57, 58 «پیم گفت.», 59, 60; 65 «گرگ عطسه کرد.», 66 «آپچی! آپچی! آپچی!»; 70-76. So the Farsi speaker-first order is confirmed.
- **Sounds right, English:** 13-15 ("A strong house takes time," / "said Pim." / "I'll play when it's done."); 20-23; 25; 54-55; 65 "Achoo! Achoo! Achoo!" (in the wolf's voice) and 66 "sneezed Wolf.".

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
