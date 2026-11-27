# SPEC: A, "The feature I deleted"

Slice 428 (`docs/plan/slices/428-dino-linkedin-film-a-deleted-feature.md`) is the source; where they differ, the slice wins.
Look, music, sfx, rail and end card come from films B and C (`../b-bedtime/SPEC.md`, `../c-five-weeks/SPEC.md`).

```text
GOAL:    a LinkedIn viewer, sound off or on, sees the first idea (scan your own paper book and let the phone read it
         aloud) in the real early app, hears that almost nobody used it and that it was deleted on 6 Nov, sees the five
         weeks that followed, then today's app (a page in three languages, "Your voice") and the three stores.
OUTPUT:  HyperFrames, 1080 x 1350 (LinkedIn 4:5 feed), 30 fps.
TIME:    50.1 s x 30 fps = 1503 frames. Voices only in the page scene (the app's own clips); music and pops are the clock.
LOOK:    film B and C's: ground #F3EFE6 flat; ink #2B2540; soft ink #5A5470; card #fffdf8; accent Dino violet #5B57E8 only on:
         "deleted", the week number, the active rail dot, "Week 1, 26 Oct", "scan your own paper book", "Claude", the rings on the phone screens, the language chip, "for any character".
         display Fredoka 600 (title 136 px, "So I deleted it." 104 px, week label 96 px); captions Fredoka 600 46-50 px;
         meta Fredoka 500 32-38 px. Shadow on screens 0 12 px 32 px rgba(43,37,64,0.18).
MEDIUM:  real captures only: v1.53.0 screens (raw/captures/captures.md lists each route), film C's week captures,
         film B's page 13 picture, voice clips and "Your voice" screen. Never the scan screen (an error screen on the
         emulator), never a mock. Phone captures sit in a plain ink border, not a drawn device.
MOTION:  enter expo.out 0.45-0.7 s; each early screen replaces the last with a 60 px rise; exits power3.in 0.25 s to
         paper; screens drift up 14-18 px; one ink bar strikes the four early screens (0.55 s, power3.out).
SHOTS:
  0  0-3.5 s     intro: icon, "The feature / I deleted" from frame 0 (thumbnail).
  1  3.5-14.5    "Week 1, 26 Oct"; "My first idea: scan your own paper book and let the phone read it aloud." One phone,
                 four screens: shelf with "Add a story" (ringed 1.7 s), add-your-pages with "Scan pages" first (ringed),
                 pages step, own book on its reading screen.
  2  14.5-19.5   "Almost nobody used it." / "So I deleted it." / "Removed 6 Nov"; the four screens dim and are struck out.
  3  19.5-31.5   five week beats, 2.4 s each (film C's captions, dates, release tags and captures), rail 1-5 under them.
  4  31.5-39.9   today's app: Three Little Pigs page 13, chips English / Farsi / Deutsch, "Today's app / voices,
                 languages and recording"; English Pop and Pim lines, then Pim's line in Farsi and German (app's own clips).
  5  39.9-43.5   film B's "Your voice" screen, the Wolf row ringed (as in film B); "Grown-ups record their voice / for any character".
  6  43.5-50.1   end card as film C: "Built in 5 weeks with Claude", icon, "Dino / Kids Books & Stories", three stores.
BANNED:  film B's and C's lists; any claim about why testers left it (the record shows usage, not a tester's words); any
         claim that the old voice was seen speaking (not captured); the scan screen; a counter; a number not in the slice.
QA:      f3 (thumbnail), f60, f180, f300, f390, f510, f570, f700, f780, f900, f960, f1020, f1140, f1230, f1320, f1490.
```

## Sound

Music: film B and C's track (`assets/bgm/cheerful-kids-11.m4a`, 43 s), volume 1 with an automation lane: 0.45, 0.14 under the page
voices (31.7-39.7 s), back to 0.45; fade in 0.6 s, fade out over its last 3.5 s (the file ends at 43 s, so the last 7 s
carry only the effects). Voices: film B's clips, `en-56-Puck`, `en-58-Achird`, `fa-59-Achird`, `de-58-Achird` at 31.8, 35.05, 36.6, 38.4 s.
Effects: pop as the idea lands (3.6 s), click on the ringed tile (7.9), whoosh into the strike (16.0), a pop per week
(19.6, 22.0, 24.4, 26.8, 29.2), the three store pops (47.13, 47.33, 47.53).
