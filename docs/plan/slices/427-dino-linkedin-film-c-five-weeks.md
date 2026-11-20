# Slice 427 — Dino: LinkedIn film C, "Five weeks, fast-forward" (how it was built)
**Product:** Dino

*Built 2026-11-20 (feat/s427). Planned 2026-11-20. Second of three LinkedIn films (426 B, 427 C, 428 A); built after 426 ships.*

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

- [x] Old screens: build or install earlier Dino releases (`dino-v*` tags and Releases, one per week since 2026-10-20) on the emulator. Capture the shelf, onboarding and reading screens for each by script. Count the real number of testing rounds and real release counts from git and the Releases; no invented numbers.
  - Built 2026-11-20, in `c-five-weeks/raw/captures/` (git-ignored) with `captures.md` naming each file's route: W1 release APK on the emulator (all three screens); W2 release APK on the emulator (onboarding; it crashes ~40 s after the voice download starts) and the x64 MSIX on the Hyper-V VM (shelf, reading; landscape window); W3 only the first-run book picker's "connect to the internet" screen (VM, which has no network; the tag predates .NET 11, so no Debug rebuild); W4 and W5 Debug x86_64 builds of the tags' own commits on the emulator (every onboarding step, shelf, reading). Numbers: see Findings.
- [x] HyperFrames project in `c-five-weeks/` with `SPEC.md`. Check is clean, review done and `REVIEW.md` written. The preview is opened for the user (2026-11-20).
- [x] After the user's OK: render the MP4 and store it the same way as 426. Write `POST.md` (how: weekly rounds, coding with Claude, what changed). The user posts it. Built 2026-11-20: 15.9 MB (16,674,804 bytes; re-rendered after review fixes), 1080x1350, 44 s, H.264 + AAC, so it stays in `c-five-weeks/renders/dino-film-c-five-weeks.mp4` (git-ignored) and is attached to GitHub Release `promo-dino-linkedin-c`; `POST.md` is in the film folder.

## Verification

- `npx hyperframes check --json` is clean. Every screen shown is a real capture of a real release, with its week label taken from that release's tag date.
- Every number in the film or post traces to git, the Releases or the user's words.

## Findings (2026-11-20, from git and the Releases)

- Weekly anchor releases (last Dino release of each week): W1 `v1.53.0` (2026-10-26, shared tag), W2 `dino-v1.96.1` (11-02), W3 `dino-v1.123.3` (11-09), W4 `dino-v1.131.3` (11-16), W5 `dino-v1.136.3` (11-20). Each has an APK (likely arm64-only), an AAB, an IPA and x64/arm64 MSIX; none pruned.
- Real captures already in git start 2026-11-14 (store screenshots); weeks 1-3 have none, so they are captured from the old releases (emulator, else the Hyper-V VM with the x64 MSIX). The S21 is not used: an old version there would need uninstalling the store install.
- Numbers: 155 `dino-v*` releases in all (the first on 2026-10-26), plus 65 earlier shared `v*` releases that also carried Dino (so "over 150 releases" holds). The first Dino commit is 2026-10-21 (95eaf534, Slice 58). Only one testing round has a date in the record (the user test of 2026-11-01, Slices 228-232); a round count is not in git, so the film uses the user's words ("a new round with users almost every week") instead of a counter number unless the user gives one.
- Changes per week, from slices: W1 per-character voices only (58, 60); W2 the first-run intro (155, built 10-27) and the 11-01 user-test fixes (228-232); W3 first-run book picker (261), smaller app (263), redesign with corner tabs, kids shelf and onboarding (325); W4 onboarding that shows what Dino can do (362), "Books and buying" settings (399); W5 one shelf with a lock and five-step onboarding (418).

## Preview feedback (user, 2026-11-20)

- "good job! I am happy with most things." Everything else in the first preview is accepted.
- Week 3: "why week3 is only an error page? what was the redesign? shouldnt we show that?" Week 3 must show the Slice 325 redesign (grown-ups corner with Books, Store and Settings tabs; the matching kids shelf; the new first-run screens), captured from `dino-v1.123.3`, which contains all three redesign merges (9da97a53, 26bdf473, 54fc0cd9). The "connect to the internet" screen is dropped.
- Built: week 3 (16.5-21.5 s, same timing, title/date/release/caption and rail) now shows three phone captures from `dino-v1.123.3` in the weeks 1/4/5 frame style: "Kids shelf", "Grown-ups corner" (Books tab) and "Store tab"; a fourth (Settings) would not be legible at 266 px, though the Settings tab shows in the corner's bottom bar. The Windows picker window image is removed.
- The user accepted the preview with week 3 rebuilt: "render" (2026-11-20).

## Review (2026-11-20)

- High pass: `D:\ReadTheStupidText-worktrees\reviews\s427-high.md`. It caught week 1's caption crediting the first-run intro (Slice 155, built 10-27) to week 1; the caption is now "The first build: a voice for every character", the post matches and the film was re-rendered.
- Medium pass: `D:\ReadTheStupidText-worktrees\reviews\s427-medium.md`. Stale REVIEW.md (fixed, loudness now recorded: -20.4 LUFS, true peak -6.8 dBFS) and this file's status (fixed); the visual nits are left as they are, since the user accepted the preview (listed in `REVIEW.md`).
- Final `check --json`: ok, 0 errors, 0 warnings. Render: ffprobe h264 1080x1350, 44.0 s, aac.

## Not yet verified

- [owner] The audio of the rendered file heard end to end (no tool can listen; the user heard the same mix in the preview).

## From film B (Slice 426)

Reuse `tools/dino/promo/linkedin/b-bedtime/` structure; bundle a CJK font with its OFL text in `assets/fonts/` if Chinese or Japanese is shown (film B used the PC-installed "Noto Sans JP"). (Slice 426: docs/plan/slices/426-dino-linkedin-film-b-bedtime.md)
