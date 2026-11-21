# Slice 428 — Dino: LinkedIn film A, "The feature I deleted"
**Product:** Dino

*In progress 2026-11-20 (feat/s428). Planned 2026-11-20. Third of three LinkedIn films (426 B, 427 C, 428 A); built after 427 ships (427 merged 2026-11-20, #1328).*

**Touches:** `tools/dino/promo/linkedin/a-deleted-feature/` (new), `tools/dino/promo/linkedin/README.md`, this slice file.

## Why

The user's order (2026-11-20): last comes "A, that explains that I deleted features". User testing showed the first idea was the wrong one, and the user removed it.

## Decisions (user, 2026-11-20, RefineScope; shared with Slice 426)

- The deleted first idea, in the user's words: "scanning your own paper book and using the internal voice engine to read the books". The user asked Claude to rewrite the wording. Draft: "My first idea: scan your own paper book and let the phone read it aloud. Testers didn't want that. I deleted it." It is shown once, at the start.
- Built with HyperFrames from 426's `STORYLINES.md`. The app's own voices only. A real recording of at most about 3 s, of a story being read. 1080×1350, 40–55 s, text on screen, free music. Preview first, then render. The user posts it.

## Storyline

It opens on the first idea in the real early app: the scan screen and the built-in phone voice. The idea is struck out. Five quick "week" cards each show what testers asked for and the screen it led to. It ends on today's app: a book page, voices, languages and recording, then the three store badges.

## Work

- [x] The scan screen and the built-in-voice reading from an early release (the Story Reader / early Dino build before scanning was removed), captured on the emulator by script. Look up when and why scanning left Dino in the early Dino slice files and git history. The post must state only what those show.
  - Built 2026-11-21, in `a-deleted-feature/raw/captures/` (git-ignored) with `captures.md`. `dino-v1.107.1` crashes on the x86_64 emulator about 40 s after launch (like film C's week 2), so the captures come from the first Dino build, `v1.53.0` (2026-10-26), which has the same add-your-own-book wizard: the grown-up shelf with "Add a story" and the "Narrator" button (`a-v1.53.0-shelf-add-a-book.png`), the four ways to add pages, "Scan pages" first (`a-v1.53.0-add-choice.png`), the pages step with one page (`a-v1.53.0-pages-step.png`, a gallery picture, not a scan), a typed page (`a-v1.53.0-page-editor-typed.png`) and the own book "My Pigs" on its reading screen (`a-v1.53.0-own-book-reading-playing.png`). Not reached: Google's document scanner shows only "Something went wrong" on the emulator (not used in the film), and speech with its highlight was not seen, so the film does not claim to show the old voice speaking.
- [x] HyperFrames project in `a-deleted-feature/` with `SPEC.md`. Check is clean, review done and `REVIEW.md` written. The preview is opened for the user.
  - Built 2026-11-21: 50.1 s (not the Film plan's 45 s: week beats are 2.4 s and the end card 6.6 s, because shorter ones left text unread per `check`). Scenes: title 0-3.5, first idea 3.5-14.5 (four v1.53.0 screens, rings on "Add a story" and "Scan pages"), struck out 14.5-19.5, weeks 19.5-31.5 (two captures each), page 13 in English, Farsi, German 31.5-39.9 (Farsi and German play Pim's line only), "Your voice" 39.9-43.5, end card 43.5-50.1. `check --json` ok; frames read by Claude at contact-sheet size. Preview on port 3428.
  - The user accepted the preview as built: "render" (2026-11-21).
- [x] After the user's OK: render the MP4 and store it the same way as 426. Write `POST.md`. The user posts it.
  - Built 2026-11-21: 19.7 MB (19,740,528 bytes), 1080x1350, 50.1 s, H.264 + AAC, -20.0 LUFS, peak -1.7 dBFS; over 15 MB, so it stays in `a-deleted-feature/renders/dino-film-a-deleted-feature.mp4` (git-ignored) and is attached to GitHub Release `promo-dino-linkedin-a`. `POST.md` is in the film folder, with where each claim comes from.

## Verification

- `npx hyperframes check --json` is clean. The "before" screens are real captures of a real early build.
- The reasons given for removing the feature trace to the slice files, the git history or the user's words.

## Findings (2026-11-20, from git, origin/main)

- Scanning (camera + OCR) and the on-device voice engine were removed from Dino together on 2026-11-06 by Slice 278 (`docs/plan/slices/278-dino-store-books-only.md`): part A PR #859 (merge 09e2e892), part B (voice engine out) PR #863, part C PR #862. Slice 349 item 4 (2026-11-12) dropped the scanner and camera strings from the iOS build. RTST keeps scanning; the shared code stays.
- Dino used the shared scanner (Slice 45, 2026-10-20) from Slice 64 (2026-10-21, picture mode on scanned pages) and Slice 78 (2026-10-22, page-review wizard).
- The only written reason is the user's (Slice 278 Why, 2026-11-06): "remove the path to add your own books. I dont care about it. that's not the main product anymore." Slice 278 also records live usage: own books were added in one app session (about 9 books, all typed in the wizard; no scans, no file imports). No tester quote about scanning is on record.
- Last release with scanning: `dino-v1.107.1` (2026-11-06 14:42Z, two minutes before #859 merged); it has an APK (arm64), AAB, IPA and x64/arm64 MSIX. The first without it: `dino-v1.108.0`. No screenshot or recording of Dino's scan screen is in git, so it is captured fresh from `dino-v1.107.1`.

## Wording (Claude, 2026-11-20, as the user asked: "rewrite it yourself")

- The draft line "Testers didn't want that" is replaced: the record shows real usage (no one scanned), not a tester's words. The opening reads: "My first idea: scan your own paper book and let the phone read it aloud. Almost nobody used it. So I deleted it." The post says the same, with the dates above (built 21-22 Oct, deleted 6 Nov), and the user's own framing that the app changed with user feedback.

## Review (2026-11-21)

- High pass: `D:\ReadTheStupidText-worktrees\reviews\s428-high.md`. Fixed: the post's closing paragraph now quotes the user's own words, and "16 days" (Slice 64 to Slice 278) replaces "days, not months"; "the phone's own voice" is now "the app's built-in voice"; SPEC.md matches the film (the Wolf row is ringed, as in film B); `linkedin/README.md` lists films C and A. Left as accepted in the preview: the ring on the Wolf row, "So I deleted it" (the user's framing, 2026-11-20), the music ending at 43 s, the "Your voice" screenshot's header above title-safe. Work item 1 stays ticked; its note says what was not captured.
- Medium pass: `D:\ReadTheStupidText-worktrees\reviews\s428-medium.md`. Fixed: REVIEW.md now says the ring lands on the Wolf row (the code aims at Pim's; the frames show Wolf, as in film B); POST.md sources "the app's built-in voice". Left as they are: week 1's beat reuses the first build after it is struck out (the weeks are the app's whole run); "16 days" counts from the feature reaching the code (21 Oct), which the post's sources say; "Almost nobody used it" rests on the production usage counts Slice 278 records, the only usage on record; the Storyline's "what testers asked for" is replaced by the Film plan's per-week captions (no tester claims are on record); the silent last 7 s of the end card.

## Film plan (Claude, 2026-11-21, within the Storyline and Decisions above)

1. 0-4 s: title card "The feature I deleted".
2. 4-15 s: "My first idea: scan your own paper book and let the phone read it aloud." over the v1.53.0 captures in phone frames: shelf with "Add a story", the add-choice screen with "Scan pages" marked, the pages step, the own book on its reading screen. Label "Week 1, 26 Oct" (the first Dino release, Slice 427 Findings).
3. 15-20 s: "Almost nobody used it. So I deleted it." The early screens are struck out. Small line: "Removed 6 Nov" (Slice 278).
4. 20-30 s: the five "week" cards, one quick beat each, using film C's week captures and its per-week captions (Slice 427 Findings, "Changes per week"; no tester claims, no counter).
5. 30-38 s: today's app: the Three Little Pigs page with the same line in English, Farsi, German with the app's own Gemini clips (film B's assets), and "Your voice" recording.
6. 38-45 s: film B's end card ("Built in 5 weeks with Claude", the three stores). Music: film B and C's track 11, same levels.
Reuse `c-five-weeks/` (structure, rail, end card, Fredoka font, bgm, sfx) and `b-bedtime/` (page 13 picture, en/fa/de clips). No Chinese or Japanese is shown, so no CJK font.

## From film B (Slice 426)

Reuse `tools/dino/promo/linkedin/b-bedtime/` structure; bundle a CJK font with its OFL text in `assets/fonts/` if Chinese or Japanese is shown (film B used the PC-installed "Noto Sans JP"). (Slice 426: docs/plan/slices/426-dino-linkedin-film-b-bedtime.md)
