# Slice 428 — Dino: LinkedIn film A, "The feature I deleted"
**Product:** Dino

*Planned 2026-11-20. Third of three LinkedIn films (426 B, 427 C, 428 A); built after 427 ships.*

**Touches:** `tools/dino/promo/linkedin/a-deleted-feature/` (new), this slice file.

## Why

The user's order (2026-11-20): last comes "A, that explains that I deleted features". User testing showed the first idea was the wrong one, and the user removed it.

## Decisions (user, 2026-11-20, RefineScope; shared with Slice 426)

- The deleted first idea, in the user's words: "scanning your own paper book and using the internal voice engine to read the books". The user asked Claude to rewrite the wording. Draft: "My first idea: scan your own paper book and let the phone read it aloud. Testers didn't want that. I deleted it." It is shown once, at the start.
- Built with HyperFrames from 426's `STORYLINES.md`. The app's own voices only. A real recording of at most about 3 s, of a story being read. 1080×1350, 40–55 s, text on screen, free music. Preview first, then render. The user posts it.

## Storyline

It opens on the first idea in the real early app: the scan screen and the built-in phone voice. The idea is struck out. Five quick "week" cards each show what testers asked for and the screen it led to. It ends on today's app: a book page, voices, languages and recording, then the three store badges.

## Work

- [ ] The scan screen and the built-in-voice reading from an early release (the Story Reader / early Dino build before scanning was removed), captured on the emulator by script. Look up when and why scanning left Dino in the early Dino slice files and git history. The post must state only what those show.
- [ ] HyperFrames project in `a-deleted-feature/` with `SPEC.md`. Check is clean, review done and `REVIEW.md` written. The preview is opened for the user.
- [ ] After the user's OK: render the MP4 and store it the same way as 426. Write `POST.md`. The user posts it.

## Verification

- `npx hyperframes check --json` is clean. The "before" screens are real captures of a real early build.
- The reasons given for removing the feature trace to the slice files, the git history or the user's words.

## From film B (Slice 426)

Reuse `tools/dino/promo/linkedin/b-bedtime/` structure; bundle a CJK font with its OFL text in `assets/fonts/` if Chinese or Japanese is shown (film B used the PC-installed "Noto Sans JP"). (Slice 426: docs/plan/slices/426-dino-linkedin-film-b-bedtime.md)
