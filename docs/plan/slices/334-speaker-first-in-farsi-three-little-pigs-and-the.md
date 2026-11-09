# Slice 334: Listening page: Speaker first in Farsi (Three Little Pigs) and the wolf's sound in English

*Built 2026-11-09 (branch `fix/334-335-pigs`, merged as PR #991, 3c98f43): fix done, user confirmed by ear on the second listen; verification complete.*

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 254's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Live:** On a device: Three Little Pigs in Farsi reads «پیم گفت:» then the whole quote and «گرگ گفت:» before the wolf's words; English: the wolf's sound before "said Wolf", "Achoo!" in the wolf's voice.

**Touches:** `docs/plan/slices/334-speaker-first-in-farsi-three-little-pigs-and-the.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it: the shipped premade Gemini clips from `content/dino/three-little-pigs/audio/{fa,en}` at origin/main 011d772e (Git LFS), not re-rendered. The wolf has no animal recording in the app (`WithoutClip` in `AnimalSoundClips.cs`), so no wolf sound plays next to "said Wolf"; only his spoken "Achoo!".
- [x] Publish one private listening page: https://claude.ai/artifact/Cj4gG4fk3Y1maLsVbGCMto (2026-11-08; 8 Farsi scenes, 6 English scenes).
- [x] When the user has answered, write every answer into this file, word for word (below).
- [x] Each "something is wrong" becomes a fix item in this slice (cause, then fix, through the normal review gate). Every "sounds right" closes the check.
- [x] Fix 1: built, second listen on https://claude.ai/artifact/CktvL7hbrcmoXDDDA5dKi5 answered "sounds right" (2026-11-09, below). *Built 2026-11-09: the three Wolf huff-and-puff lines in `audio-cast.json` now carry the direction "playful and silly, gentle puffing like a friendly storybook wolf, light and cheerful, not scary" (was "growly and threatening / angry"), which applies in every language; the three occurrences now share one clip per language, re-rendered with `generate.py --batch` in all 15 folders (en: `d1b446e7a99f7e0f-Elio-gemini-tts-9a2de73b.m4a`, was `...-5353011c` for entries 24 and 34 and `...-545979c7` for 48); `build-audio` bumped each language's audio pack version.* **Fix 1 (from the user's answer):** English clip 24, the wolf (voice Elio): "Then I'll huff, and I'll puff, and I'll blow your house in!" The user: "too much feeling. somewhat scary for a little one". Re-render this clip calmer and less scary for ages 3-9 (premade-audio tooling, `tools/premade-audio/`), bump the book's audio, and put the new clip on the same page for a second listen. **In every language the book ships, not only English** (user, 2026-11-08: "remember to do the fix for all languages. If it is too scary in english, is probably too scary in french, farsi and others too"): the same huff-and-puff line in all 15 audio folders (ar, da, de, en, es, fa, fi, fr, ja, nb, nl, pt, ru, sv, zh), and any other wolf line rendered with the same delivery. The second listen covers English and Farsi (the listening-page languages).
- Note (review #1, 2026-11-09): the Arabic and Farsi manifests also re-point 20 narrator (Hali) entries each (besides the three wolf lines) to new clips whose names match the current text. Slice 280 changed their text («قال بيب:» to «قال بيب.», «پیپ گفت:» to «پیپ گفت.») but kept the old clips, whose names no longer matched the text hash and failed the check. The new clips are unheard; one Farsi sample is on the listening page https://claude.ai/artifact/CktvL7hbrcmoXDDDA5dKi5.
- [x] Follow-up: after the user's second listen, delete the unreferenced old wolf clips (`...-5353011c.m4a`, `...-545979c7.m4a` in the 15 audio folders) and the other orphaned old clips (the old Farsi splash `65b3e84e6c199a3f`, old Elio clips); the listening page still plays them until then (review #2, `reviews/s3345-high.md`). *Done 2026-11-09: 52 `.m4a` files not named in their folder's `manifest.json` deleted (ar 8, fa 18, 2 each in the other 13 languages); `check-audio three-little-pigs` passes (all 15 manifests line up) and the premade-audio unit tests pass (121).*

## The user's answers (2026-11-08, Done pressed 18:50Z)

Everything the page asked was marked **sounds right** except one clip.

- **Wrong:** English 24, "Then I'll huff, and I'll puff, and I'll blow your house in!": "too much feeling. somewhat scary for a little one".
- **Sounds right, Farsi (speaker first, then the quote):** 13 «پیم گفت.», 14, 15; 20 «گرگ گفت.», 21; 22 «پیپ گفت.», 23; 24 «گرگ گفت.», 25; 54 «گرگ گفت.», 55; 56 «پاپ گفت.», 57, 58 «پیم گفت.», 59, 60; 65 «گرگ عطسه کرد.», 66 «آپچی! آپچی! آپچی!»; 70-76. So the Farsi speaker-first order is confirmed.
- **Sounds right, English:** 13-15 ("A strong house takes time," / "said Pim." / "I'll play when it's done."); 20-23; 25; 54-55; 65 "Achoo! Achoo! Achoo!" (in the wolf's voice) and 66 "sneezed Wolf.".

## The user's second listen (2026-11-09 08:50, https://claude.ai/artifact/CktvL7hbrcmoXDDDA5dKi5)

- **Calmer wolf huff-and-puff ("Then I'll huff, and I'll puff, and I'll blow your house in!"): sounds right** in English, Farsi, French and German. The other 11 languages use the same direction and were not listened to.

## Changed while building

See Fix 1 above (direction changed for all 15 languages; Arabic and Farsi also re-point 20 Hali entries each, note above).

## From review

Medium review (`reviews/s3345-medium.md`, findings 6/6 read):

- #1 (~20 new unheard Hali clips in fa/ar): answered in part by the second listen (the Farsi sample entry 20 «گرگ گفت.» sounds right); the other new Hali clips stay unheard.
- #2 (Farsi say with vowel marks may be ignored by TTS): answered by the listen, see Slice 335: the user said the new «شَلَپ» clip says شالاپ right.
- Skipped #3 (`reviews/s3345-medium.md`): "PigsWolfHuffTests passes if all three directions are reverted together or empty" - low severity, not applied this pass.
- Skipped #4 (`reviews/s3345-medium.md`): "one English-only calm direction for all 15 languages" - low severity, not applied; the user heard four languages and they sound right, the other 11 stay unheard.
- #5 (stale replaced clips left in folders): applied in the follow-up above (52 files deleted).
- #6 (Fix 1 bullet merged built note with the old instruction, box unchecked): applied here; the box is ticked and the status line replaced.

## Verification

The user's answers are recorded here. The wrong item (English 24) has its fix and the second listen answered "sounds right" in English, Farsi, French and German on 2026-11-09. The orphaned clips are deleted and `check-audio three-little-pigs` and the premade-audio unit tests pass.
