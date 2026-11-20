# REVIEW: film C (2026-11-20; updated after the week 3 rebuild, the week 1 caption fix and the final render)

`check --json`: ok, no errors, warnings or infos in lint, runtime, layout, contrast, craft and audio. A first run
had five `dead_stretch` craft warnings (each week scene sat still for 3-4 s); fixed by an 18 px upward drift of the
screens over each scene. `snapshot --review` (beat sheet, seam strips, phone sheet) read; frames at 0, 3, 8, 14, 19,
24, 30, 35, 38, 43 s read at contact-sheet size; text and screens sit inside title-safe (y 135-1215), the rail's
dots end at y 1196.

## What a stranger sees, beat by beat (44 s)

1. 0-4.5 s: "Dino, built in / 5 weeks" (on frame 0), "A new round with users almost every week", "Coding with Claude,
   testing with users", five dots dated 26 Oct to 20 Nov.
2. 4.5-10.5 s: Week 1 (26 Oct, release v1.53.0): "The first build: a voice for every character"; first run, shelf,
   reading on phone screens.
3. 10.5-16.5 s: Week 2 (2 Nov, dino-v1.96.1): "Fixes from the first user test"; first run on a phone, shelf and reading
   in Windows windows.
4. 16.5-21.5 s: Week 3 (9 Nov, dino-v1.123.3): "Redesign: kids shelf, grown-ups corner" over three phone captures:
   kids shelf, grown-ups corner (Books), Store tab.
5. 21.5-27.5 s: Week 4 (16 Nov, dino-v1.131.3): book picker, quick tour, shelf.
6. 27.5-33.5 s: Week 5 (20 Nov, dino-v1.136.3): book picker, shelf with a lock, reading.
7. 33.5-37 s: "Over 150 releases in 5 weeks".
8. 37-44 s: film B's end card, "Built in 5 weeks with Claude", the three stores; music fades out under it.

A week rail (dots 1-5, the current one violet, passed ones ink) rides under every scene from 4.5 to 37 s, so the weeks
read as one run.

## What stands in for what could not be got as real

- **Week 3** now shows three real phone captures from dino-v1.123.3 (kids shelf, grown-ups corner Books tab, Store
  tab), in the same phone frames as weeks 1, 4 and 5; the Settings tab is visible in the corner's bottom bar but not
  a fourth screen (four would be too small). The Windows "connect to the internet" window is dropped.
- **Week 2 shelf and reading** are Windows windows from the VM; the reading one still carries the app's offline line
  ("Waiting for the internet") because the VM is offline.
- **Weeks 4 and 5** are Debug x86_64 builds of the tags' own commits (captures.md); week 4 and 5 reading look the same
  (same first page). Week 5's book picker shows the state after the Pigs tap (captures.md note).
- Week 1's date is the shared tag's (26 Oct), as the slice's Findings give it.
- Store badges: text buttons as in film B (official badge files not in the repo).

## Numbers

Only the slice's Findings: 5 weeks; "over 150 releases" (155 `dino-v*` releases in all, the first on 2026-10-26, plus 65
earlier shared `v*` ones); the dates are the five anchor releases' tag dates; "a new round with users almost every week" is the user's
words, with no round counter. The per-week captions are the Findings' changes-per-week line in plain words.

## Sound

Music and effects as in film B (see SPEC.md). Measured on the final render (`ffmpeg -af ebur128=peak=true`):
integrated -20.4 LUFS, loudness range 1.5 LU, true peak -6.8 dBFS. Music-only, so it sits a little under LinkedIn's
usual -14 to -16 LUFS; left as is, close to film B's render (-19.1 LUFS).

## Second review (medium) findings left as they are

- The intro's five dots span 25 days (26 Oct to 20 Nov, last gap 4 days): they are the five anchor releases' real dates.
- The 18 px drift starts with the phones' entrance; accepted by the user in the preview.
- Week 2's offline line on the Windows reading window and scene hosts without `class="clip"`: no effect on the
  accepted render.

## Weakest shot and what I would change

The phone screens are small (266 px wide); their text is not meant to be read, the layout change is.

## Warnings kept on purpose

None.
