# REVIEW: film B (2026-11-20, third pass: the user's notes on the 49 s preview)

`check --json`: ok, no errors or warnings (three `container_overflow` infos: the page and why cameras push past the
frame on purpose). Third pass: `snapshot --review` (beat sheet, four seam strips, phone sheet), plus `--at 2` (the
caption over the pigs) and `--at 33.1,34.5,36.2` (the record screen before and after its move). Accepted by the user
and rendered 2026-11-20 to `renders/dino-film-b-bedtime.mp4` (44 s, 19.3 MB; git-ignored), going to GitHub Release
`promo-dino-linkedin-b`. The sound was placed from the clips' measured lengths.

## What a stranger sees, beat by beat (44 s)

1. 0-5.2 s: a painted page of three pigs at a fireplace, an "English" chip, "A voice for every character / made with
   Google's Gemini voices" on a paper card over the bricks (from frame 0, so the thumbnail carries it), the page's
   text; Pop's line lights up yellow while his voice says it, then Pim's "Don't worry,". The card leaves at 5.1 s.
2. 5.2-11.9 s: same picture; the text turns into Farsi, right to left; a "فارسی" chip joins; the same two lines play
   in Farsi in the same character voices.
3. 11.9-18.2 s: German; "One page. Three languages." lands on paper at the top; the scene exits to paper.
4. 18.2-22.4 s: "15 languages for the app, the books and the audio", the 15 names in their own scripts.
5. 22.4-29 s: the moonlit page 15; "I built Dino for kids growing up between two or three languages / like
   second-generation kids in migrant families".
6. 29-36.5 s: the real "Your voice" screen; Pim's row is ringed (pick a character); the screen slides down, no zoom,
   until "Your voice stays on this phone. It is never uploaded." sits on the frame's vertical centre, lit yellow;
   "Grown-ups record their voice / for any character" moves from under the screen to the top of title-safe.
7. 36.5-44 s: "Built in 5 weeks with Claude", the app icon carried in, "Dino / Kids Books & Stories", the three
   stores as the site names them; the music fades out under it.

## Third pass (user, 2026-11-20, after the 49 s preview)

- App scene removed (was 18.2-23.2 s) with its two voice clips (en 57, en 58) and its whoosh; `compositions/app.html`,
  `assets/app-reading.mp4` and `assets/images/app-reading-last.jpg` deleted (nothing else used them);
  `raw/s21-page13-en.mp4` kept for films C and A. Every later scene, the store sounds (now 40.13-40.93 s) and the
  root length (44 s) moved 5 s earlier.
- The scene's line now sits over the opening: paper card at x 108, y 222, about 760 x 140 px, between the chips
  (y 135-199) and the pigs' heads (from about y 500); the text card starts at y 905. Title Fredoka 600 56 px, quiet
  line Fredoka 500 34 px.
- Music: 0.14 under every voice (last voice ends 16.61 s), rising 16.8-18.0 s to 0.45 where nobody speaks; the clip
  is the track's 43 s, fade in 0.6 s, fade out over its last 3.5 s (39.5-43 s, on the end card).
- Record screen: the 1.55x push-in is gone. At 3.7 s the screen slides down 352 px so the privacy line (frame y 323)
  lands on y 675, the centre; a `clip-path` keeps the phone's status bar hidden in its new place. The caption fades
  out at the bottom (where the list now runs) and fades back in at y 150-271, inside title-safe (y 135-1215).
- Page scene exit: it ended under the app's whip before; it now fades to paper over 17.95-18.2 s (power3.in 0.25 s,
  as the other scenes leave), so the cut into "15 languages" is paper to paper.

## Notes fixed in earlier passes

- Second pass: the S21 store-app take (`raw/s21-page13-en.mp4`, build 1.133.1; Pop's sentence lit until 5.75 s, Pim's
  from 6.7 s) replaced the muted emulator take in the app scene; that scene was dropped in the third pass, and the take
  stays in `raw/` for films C and A. Music bed added (see Sound).
- First pass: the record screen's status bar moved out of frame; record title shortened to two lines; the why scene's
  quiet line moved below the wrapped title; overlapping store sounds split across lanes, slots set to file lengths.

## Sound

- Voices: the app's own Gemini clips for page 13 (`assets/audio/`), en/de 56 + 58 and fa 57 + 59 in shots 1-3; no other
  voice in the film.
- Music: HeyGen's free music catalog, through HyperFrames (catalog `GET /v3/audio/sounds?type=music`). The user picked
  candidate 11 of 11 on 2026-11-20: "Astral Generated Music: 3602ebd4" (id 3602ebd4595648bca217c1a50e542505,
  "cheerful upbeat preschool pop, energetic and bright", 43 s), in `assets/bgm/cheerful-kids-11.m4a` (AAC). Licence: the
  catalog's answer has no licence or terms field (`raw/music-candidates/candidates.json`), so the terms are those of
  HeyGen's free music catalog via HyperFrames; no separate licence text was published with the track. The first
  piano pick (e1e41141) was replaced and is kept only in `raw/previous/bgm/` (ignored).
- Mix: the bed sits at 0.14 under every voice (0-16.8 s), rises to 0.45 by 18.0 s where nobody speaks, fades in over
  0.6 s and out over the last 3.5 s of its 43 s (the end card). Effects: whooshes on the two language swaps, pops on the stores.
  Loudness of the whole mix is measured only after the render (`npx hyperframes audio`).

## Creative calls

- The line played in each language is Pop's "Uh-oh! He's going to come down the chimney!" and Pim's "Don't worry,"
  (Puck and Achird): two characters, two feelings, the same voices in all three languages. The narrator's "said Pop" /
  "said Pim" clips are left out of shots 1-3 to keep each language under 6 s.
- The Farsi card shows the speaker first ("پاپ گفت: «...»"), the order the Farsi audio speaks it in; the quotation
  marks are a presentation choice, not copied from an app screen.
- Storylines C and A are written in `STORYLINES.md` for Slices 427 and 428; not built.

## What stands in for what could not be got as real

- **A parent recording a line:** nobody has recorded one, so no recorded voice is played; shot 6 shows the real setup
  screen (pick Pim) and the privacy line instead of a recording screen.
- **Store badges:** the official Google Play, App Store and Microsoft Store badge files are not in the repo; the end card
  uses the site's own text buttons ("Android · Google Play" ...). Official badges can replace them.
- No app recording is in film B (user, 2026-11-20); the page scene stands for the app reading with a voice per
  character.

## Seams (third pass)

All four cuts (18.2, 22.4, 29, 36.5 s) now read the same way: the outgoing scene drops to the paper ground over
0.25 s and the next one builds on paper. The new seam at 18.2 s: the German page with "One page. Three languages."
holds until 18.1 s, one paper frame at 18.2 s, then "15" pops in (18.3 s faint, 18.4 s full).

## Weakest shot and what I would change

The seam at 18.2 s: power3.in keeps the page almost fully visible until about 18.1 s, so it plays close to a hard cut
from the bright painted page to flat paper. It matches the other cuts on the sheet; if it jars in playback, lengthen
the page's exit to 0.4 s (from 17.8 s).

The record shot's list now runs off the bottom of the frame after the slide (the Wolf row is half cut). That is the
price of centring the privacy line without zooming; the list is context there, not the point.

## Warnings kept on purpose

None besides the three `container_overflow` infos (the page and why camera pushes).
