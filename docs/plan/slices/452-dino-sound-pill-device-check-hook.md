# Slice 452 — Dino: sound pill device check hook and Part B leftovers
**Product:** Dino
**Touches:** `src/Dino/DinoReader.App/Testing/**`, `src/Dino/DinoReader.App/**/SoundStatusPill.cs`, `src/Dino/DinoReader.App/**/SoundPillText.cs`, `.maestro/dino/**`, `tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj`, `tests/Dino/DinoReader.Tests/SoundPill*Tests.cs`, `tools/emulator/screenshot-frames.json` (widened at build, wave 4 of night 2026-11-27-0154)

*Built 2026-11-27 (wave 4, night 2026-11-27-0154), merged as PR #1407 (1d2412ff). Emulator run PASS; iOS simulator and the mock comparison not yet verified.* (Planned 2026-11-25, moved out of Slice 290.)

**Why.** Slice 290 (built, #889 and #895) cannot be compared with frames 197-199 on a device: no flow stops on the reading page while a pill shows. Moved out of Slice 290 and plan.md on 2026-11-25.

**Work.**
- [x] A Debug-only test hook (`src/Dino/DinoReader.App/Testing/**`) that opens a store book and holds the pill in each state (Getting with a count, Wi-Fi, Preparing, no sound), and a Maestro flow that screenshots each state. Restart the emulator cold first (the Maestro driver crash after boot).
- [x] Low leftovers: `SoundStatusPill.cs` `Label.Text` is null under FormattedText; `SoundPillText.cs` line 12 `IndexOf` marks the first occurrence of the name; the FormattedString is rebuilt on each changed call (the ring redraws while hidden); ja/zh pill text uses the system font (the last one is a decision, not built: see below).

*Changed while building:*
- Hook (Debug): `sound-pill state=getting|wifi|preparing|nosound|off [percent=] [lang=]` (`Testing/DinoTestCommands.SoundPill.cs`, parsing in `Testing/SoundPillHookPlan.cs`; iOS launch keys `state` and `percent` added). It waits for the reading page and logs `HOOK sound-pill ok <state>` or `failed no reading page open`. While held, page `Show` calls are ignored (logged once per hold).
- Flow `.maestro/dino/reading-sound-pill-states.yaml`: order wifi, preparing, nosound, getting, off; `waitForAnimationToEnd` before the first hook (the first run's first shot was taken before the page picture loaded). Screenshots are listed in `tools/emulator/screenshot-frames.json` (preparing maps to frame 198, as the table says).
- Leftovers: `Label.Text` is plain when there is no name run; `IndexOfName` prefers a whole word, and combining marks count as word characters (Farsi); the ring redraws only while visible.
- ja/zh pill text keeps the platform system font. Decision (coordinator, 2026-11-27): no CJK font is bundled and the system font renders ja/zh correctly.
- Touches widened at build: the test csproj, `SoundPill*Tests.cs`, `screenshot-frames.json`.

*Device:* emulator run 2026-11-27 PASS (first run `reviews/s452/flow2.log`; after the reorder `reviews/s452/rerun/`: PASS 51.9 s, all four shots show the picture). Those files are outside the repo.

#### From review

Findings read: `reviews/pr-1407-high.md` 6/6, `reviews/pr-1407-medium.md` 4/4.
- Applied (high): #1 (iOS launch keys), #4 (hook waits for the reading page), #5 (percent parsing), #6 (dedupe state after release). Applied (medium): #2 (hold log once), #3 (combining marks), #4 (`ShowNow` split).
- Skipped #2 (`reviews/pr-1407-high.md`): "ja/zh pill text uses the system font" - ja/zh keeps the system font: no CJK font is bundled and the system font renders ja/zh correctly (coordinator decision recorded in this file).
- Skipped #3 (`reviews/pr-1407-high.md`): "Slice file records no outcome" - update-docs writes the ticks, review and Not yet verified.
- Skipped #1 (`reviews/pr-1407-medium.md`): "`ReleaseTestHold` (the hook's `off`) hides the pill and clears caches without asking the page what it should show now." - the hold is a Debug-only test hook; the page's next status change redraws the real pill, and no user build has the hold.
- Follow-ups (check pass `reviews/pr-1407-check.md`, 5 nits, none applied: a third pass reopens code only for crash, data-loss, security, privacy, parity or loading-state findings): #1 re-apply the page status after `state=off`; #2 auto-release a hold after an aborted flow (Debug only); #3 confirm a theme switch while the pill is hidden redraws the ring on a device; #4 `state=off percent=abc` fails before release, and a modal page is not found; #5 CJK/Thai names fall back to the first occurrence (add a test).

#### For CLAUDE.md (lifted 2026-11-27)

- Dino sound pill device check: the Debug hook `sound-pill state=getting|wifi|preparing|nosound|off [percent=] [lang=]` (`src/Dino/DinoReader.App/Testing/DinoTestCommands.SoundPill.cs`) holds the pill while a page `Show` is ignored; run `off` to release it. A new hook launch key must also be added to `AppLaunchKeys` or iOS ignores it. (Slice 452: docs/plan/slices/452-dino-sound-pill-device-check-hook.md)

#### Not yet verified

- [session] Compare the four shots (`reading-sound-pill-states`) with mock frames 197-199 when `docs/design/dino-reader/frames` exists (only the HTML mock is in the repo today).
- [session] iOS simulator run of the same flow (iOS is not compiled on this PC).
