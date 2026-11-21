# REVIEW: film A (2026-11-21, before the user's preview)

`check --json`: ok, no errors or warnings in lint, runtime, layout, contrast, craft and audio; one info (`container_overflow`
on `#pg-cam`, the page picture's slow push, as in film B). First runs had one error (the title wrapped to two lines and
overlapped) and five `line_unread` warnings (week captions and the unread Pop lines in Farsi and German were on screen under
2 s); fixed by 136 px no-wrap title, 2.4 s week beats, a longer end card, and showing only Pim's line in Farsi and German.
`snapshot` contact sheets read at 0.1, 2, 6, 10, 13, 17, 19, 21, 23.5, 26, 29, 31, 34, 36, 38, 41, 42, 47, 49 s; text and
screens sit inside title-safe (y 135-1215), the rail's dots end at y 1196, nothing overlaps.

## What a stranger sees, beat by beat (50.1 s)

1. 0-3.5 s: "The feature / I deleted" with the app icon (on frame 0).
2. 3.5-14.5 s: "Week 1, 26 Oct", "My first idea: scan your own paper book and let the phone read it aloud." Four real v1.53.0
   screens in one phone: shelf with "Add a story", the add-pages choice with "Scan pages" ringed, the pages step, an own book.
3. 14.5-19.5 s: "Almost nobody used it. So I deleted it." and "Removed 6 Nov"; the four screens dim and one bar strikes them out.
4. 19.5-31.5 s: five 2.4 s week beats with film C's captions, dates, release tags and captures; the rail runs 1 to 5.
5. 31.5-39.9 s: Three Little Pigs page 13; English Pop and Pim lines, then Pim's line in Farsi and German, each with the
   app's own clip and reading highlight; chips English, Farsi, Deutsch; "Today's app, voices, languages and recording".
6. 39.9-43.5 s: "Your voice" screen, a row ringed (the code aims at Pim's row; the rendered frames show it on the Wolf
   row, as film B does; accepted in the preview), "Grown-ups record their voice for any character".
7. 43.5-50.1 s: end card: "Built in 5 weeks with Claude", the three stores.

## What stands in for what could not be got as real

- The "before" screens are v1.53.0 (26 Oct, the first Dino build), not `dino-v1.107.1` (the last with scanning), which crashes on
  the emulator after about 40 s (captures.md). v1.53.0 has the same add-your-own-book wizard.
- No scan or camera screen: Google's scanner shows only an error on the emulator. The pages screen shows a gallery picture.
- The old voice speaking was not captured, so the film does not claim it; the reading screen is shown only as an own book.
- Page scene: Farsi and German show Pim's line only (the Pop line would sit unread), the same line in the three languages.

## Numbers and dates

Only the slice's: 26 Oct (first Dino release), 6 Nov ("Removed 6 Nov", Slice 278), five weeks and their dates and release tags
(film C). The reason given is the record's: "Almost nobody used it" (Slice 278: about 9 own books, all typed, no scans).

## Sound

Film B and C's track, ducked to 0.14 under the four voice clips; effects from the same set (SPEC.md). Not measured (no render).
The music file is 43 s, so the last 7 s of the end card carry only the store pops.

## Warnings kept on purpose

None.

## Weakest shot and what I would change

The first idea shows a phone at 360 px wide, so the tiles read but the small text does not; the ring on "Scan pages" does the work.
Scene 1 is the longest hold (11 s); one screen change every 2.2-3.3 s keeps it moving.
