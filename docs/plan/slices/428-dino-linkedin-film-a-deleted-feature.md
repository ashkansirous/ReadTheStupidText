# Slice 428 — Dino: LinkedIn film A, "The feature I deleted"
**Product:** Dino

*In progress 2026-11-20 (feat/s428). Planned 2026-11-20. Third of three LinkedIn films (426 B, 427 C, 428 A); built after 427 ships (427 merged 2026-11-20, #1328).*

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

## Findings (2026-11-20, from git, origin/main)

- Scanning (camera + OCR) and the on-device voice engine were removed from Dino together on 2026-11-06 by Slice 278 (`docs/plan/slices/278-dino-store-books-only.md`): part A PR #859 (merge 09e2e892), part B (voice engine out) PR #863, part C PR #862. Slice 349 item 4 (2026-11-12) dropped the scanner and camera strings from the iOS build. RTST keeps scanning; the shared code stays.
- Dino used the shared scanner (Slice 45, 2026-10-20) from Slice 64 (2026-10-21, picture mode on scanned pages) and Slice 78 (2026-10-22, page-review wizard).
- The only written reason is the user's (Slice 278 Why, 2026-11-06): "remove the path to add your own books. I dont care about it. that's not the main product anymore." Slice 278 also records live usage: own books were added in one app session (about 9 books, all typed in the wizard; no scans, no file imports). No tester quote about scanning is on record.
- Last release with scanning: `dino-v1.107.1` (2026-11-06 14:42Z, two minutes before #859 merged); it has an APK (arm64), AAB, IPA and x64/arm64 MSIX. The first without it: `dino-v1.108.0`. No screenshot or recording of Dino's scan screen is in git, so it is captured fresh from `dino-v1.107.1`.

## Wording (Claude, 2026-11-20, as the user asked: "rewrite it yourself")

- The draft line "Testers didn't want that" is replaced: the record shows real usage (no one scanned), not a tester's words. The opening reads: "My first idea: scan your own paper book and let the phone read it aloud. Almost nobody used it. So I deleted it." The post says the same, with the dates above (built 21-22 Oct, deleted 6 Nov), and the user's own framing that the app changed with user feedback.

## From film B (Slice 426)

Reuse `tools/dino/promo/linkedin/b-bedtime/` structure; bundle a CJK font with its OFL text in `assets/fonts/` if Chinese or Japanese is shown (film B used the PC-installed "Noto Sans JP"). (Slice 426: docs/plan/slices/426-dino-linkedin-film-b-bedtime.md)
