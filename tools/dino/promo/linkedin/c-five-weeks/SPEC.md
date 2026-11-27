# SPEC: C, "Five weeks, fast-forward"

Slice 427 (`docs/plan/slices/427-dino-linkedin-film-c-five-weeks.md`) is the source; where they differ, the slice wins.
Look, music, sfx and end card come from film B (`../b-bedtime/SPEC.md`).

```text
GOAL:    a LinkedIn viewer, sound off or on, sees Dino's real screens change week by week over 5 weeks (26 Oct to 20 Nov),
         learns that testing rounds with users happened almost every week and that there were over 150 releases, and
         ends on "Built in 5 weeks with Claude" and the three stores.
OUTPUT:  HyperFrames, 1080 x 1350 (LinkedIn 4:5 feed), 30 fps.
TIME:    44 s x 30 fps = 1320 frames. No voices; the music and the week pops are the clock.
LOOK:    film B's: ground #F3EFE6 flat; ink #2B2540; soft ink #5A5470; card #fffdf8; accent Dino violet #5B57E8 only on:
         the week number, the active rail dot, "150", the "5" of the intro, "Claude".
         display: Fredoka 600 (week label 96 px, "5 weeks" 220 px, "150" 260 px); captions Fredoka 600 46 px;
         meta and screen labels Fredoka 500 30-32 px. Shadow on screens 0 12 px 32 px rgba(43,37,64,0.18).
MEDIUM:  real captures only (raw/captures.md lists each one's route); never a mock, never a design file.
         Phone captures sit in a plain ink border (8 px, radius 34, not a drawn device); the Windows captures already
         include their own title bar and sit in a 2 px ink-bordered window with radius 10.
OBJECTS: week label + date + release tag; one caption per week (changes-per-week line of the slice's Findings);
         2-3 screens per week with a label under each; the week rail (5 dots, fill line) over the week scenes.
MOTION:  enter expo.out 0.5-0.7 s, screens rise 120 px with 120 ms stagger; exit power3.in 0.25 s to paper;
         the screens drift up 18 px over the scene (so nothing is still for 3 s); the rail dot lands with back.out.
SHOTS:
  0  0-4.5 s    intro: "Dino, built in / 5 weeks" from frame 0 (thumbnail), "A new round with users almost every week"
                (the user's words), "Coding with Claude, testing with users", five dated dots 26 Oct ... 20 Nov.
  1  4.5-10.5   Week 1, 26 Oct, release v1.53.0: first run, shelf, reading (emulator, release APK).
                "The first build: a voice for every character".
  2  10.5-16.5  Week 2, 2 Nov, dino-v1.96.1: first run (phone), shelf and reading (Windows windows).
                "Fixes from the first user test".
  3  16.5-21.5  Week 3, 9 Nov, dino-v1.123.3: three phones: kids shelf, grown-ups corner (Books), Store tab, each with its label.
                "Redesign: kids shelf, grown-ups corner".
  4  21.5-27.5  Week 4, 16 Nov, dino-v1.131.3: book picker, quick tour, shelf. "Onboarding that shows what Dino can do".
  5  27.5-33.5  Week 5, 20 Nov, dino-v1.136.3: book picker, shelf with a lock, reading.
                "One shelf with a lock, five-step onboarding".
  6  33.5-37    "Over 150 releases in 5 weeks" (155 `dino-v*` releases in all, the first on 2026-10-26, plus 65 earlier shared `v*` ones).
  7  37-44      end card as film B: "Built in 5 weeks with Claude", icon, "Dino / Kids Books & Stories", three stores.
BANNED:  film B's list; plus: any number not in the slice's Findings (no round counter); a drawn phone mockup; a
         design mock; the superseded `*-store-*` captures; claims about what the weeks' changes did for users.
QA:      f0 (thumbnail), f90 (intro), f240 (week 1), f420 (week 2 two windows), f560 (week 3), f720 (week 4),
         f900 (week 5), f1060 (150), f1290 (end card).
```

## Sound

Music: the same HeyGen free catalog track as film B (`assets/bgm/cheerful-kids-11.m4a`, "Astral Generated Music:
3602ebd4", 43 s, via HyperFrames, the user's pick for B; no separate licence text), at 0.45 (no voices), fade in 0.6 s, fade out over its last 3.5 s. Effects from
film B's set: a pop as each week lands (4.6, 10.6, 16.6, 21.6, 27.6 s), a whoosh into the 150 scene (33.6 s), the three
store pops (40.63, 40.83, 41.03 s).
