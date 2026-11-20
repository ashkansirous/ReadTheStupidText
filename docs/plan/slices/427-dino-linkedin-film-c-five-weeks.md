# Slice 427 — Dino: LinkedIn film C, "Five weeks, fast-forward" (how it was built)
**Product:** Dino

*Planned 2026-11-20. Second of three LinkedIn films (426 B, 427 C, 428 A); built after 426 ships.*

**Touches:** `tools/dino/promo/linkedin/c-five-weeks/` (new), this slice file.

## Why

The user's order (2026-11-20): after film B says what Dino is, "C explains how". The story is that the user and Claude wrote the app in five weeks. User testing plus coding with AI made a new round with users possible almost every week. Over those weeks the UI, the onboarding and the features changed with the feedback.

## Decisions (user, 2026-11-20, RefineScope; shared with Slice 426)

- Built with HyperFrames, starting from the `STORYLINES.md` written in 426.
- Real app screenshots, built into the film. A real screen recording only where it is needed, at most about 3 s.
- The app's own voices only.
- 1080×1350, 40–55 s, text on screen, free music.
- Preview first, then render. The post text is drafted by Claude. The user posts it themselves.

## Storyline

The app's real screens change week by week in one continuous zoom: old menus, old onboarding, then the new ones. A counter shows the testing rounds. It ends on "Built in 5 weeks with Claude" and the three store badges.

## Work

- [ ] Old screens: build or install earlier Dino releases (`dino-v*` tags and Releases, one per week since 2026-10-20) on the emulator. Capture the shelf, onboarding and reading screens for each by script. Count the real number of testing rounds and real release counts from git and the Releases; no invented numbers.
- [ ] HyperFrames project in `c-five-weeks/` with `SPEC.md`. Check is clean, review done and `REVIEW.md` written. The preview is opened for the user.
- [ ] After the user's OK: render the MP4 and store it the same way as 426. Write `POST.md` (how: weekly rounds, coding with Claude, what changed). The user posts it.

## Verification

- `npx hyperframes check --json` is clean. Every screen shown is a real capture of a real release, with its week label taken from that release's tag date.
- Every number in the film or post traces to git, the Releases or the user's words.

## From film B (Slice 426)

Reuse `tools/dino/promo/linkedin/b-bedtime/` structure; bundle a CJK font with its OFL text in `assets/fonts/` if Chinese or Japanese is shown (film B used the PC-installed "Noto Sans JP"). (Slice 426: docs/plan/slices/426-dino-linkedin-film-b-bedtime.md)
