# SPEC: B, "One bedtime, three languages"

Slice 426 (`docs/plan/slices/426-dino-linkedin-film-b-bedtime.md`) is the source; where they differ, the slice wins.

```text
GOAL:    a LinkedIn viewer, sound off or on, understands in 44 s what Dino is (picture books read aloud with a voice
         per character, in many languages), why it was built (kids growing up between two or three languages) and that
         a grown-up can record their own voice; then sees "Built in 5 weeks with Claude" and the three stores.
OUTPUT:  HyperFrames, 1080 x 1350 (LinkedIn 4:5 feed), 30 fps.
TIME:    44 s x 30 fps = 1320 frames (49 s until the app scene was cut, user 2026-11-20); the voices are the clock.
LOOK:    the app's own system, not a film look.
         ground #F3EFE6 (the site's warm paper, flat, no texture); ink #2B2540; soft ink #5A5470;
         card #fffdf8, no border line; accent Dino violet #5B57E8, only on: the language chip that is
         speaking, the word "Claude", the store row. Reading highlight #FBE38A behind the sentence being read
         (the app's yellow), with a 6 px radius.
         display: Fredoka 600, 56-96 px by scene (the "15" numeral 220 px), tracking -1%; quiet lines Fredoka 500, 40 px.
         page text: Andika 700, 44 px, line height 1.45; Farsi page text: Vazirmatn 700, 46 px, direction rtl.
         medium: the book's painted pictures (high-res page 13, 1792 x 2400) and real phone screens
         (1080 x 2400 store screenshots), always cropped to fill the frame.
         shadow on the page text card: 0 12 px 32 px rgba(43,37,64,0.18). No grain, no vignette.
OBJECTS: the book page (picture + text card at the bottom, exactly as the reading screen lays it out);
         the language chips (the app's rounded chip, label in its own script);
         the phone screen (a real recording or screenshot, full-bleed, never a drawn phone);
         the app icon (site/dino/icon.png); the three store names as text buttons, as the site shows them.
MOTION:  enter: expo.out 0.6 s; move/camera: power2.inOut; exit: power3.in 0.25 s.
         language swap: blur swap on one stage (out 0.18 s to 8 px blur, in 0.22 s from 8 px), the picture never cuts.
         picture: one slow push 1.00 -> 1.06 over the whole page scene (camera, not a title).
         highlight: grows left-to-right (right-to-left in Farsi) over 0.25 s when its clip starts.
         words: stagger 80 ms; titles land and hold dead still.
SHOTS:
  1  f0-156     (0.0-5.2 s)  page 13 full-bleed, English text card already on screen (frame 0 is the thumbnail).
                Pop's clip at 0.3 s, "Uh-oh! He's going to come down the chimney!" lights up; Pim's "Don't worry,"
                at 3.55 s lights up. Chip row top: "English" violet. Sound: en Puck, en Achird.
                From frame 0 to 5.1 s: "A voice for every character" + quiet line "made with Google's Gemini voices"
                on a paper card at x 108, y 222 (below the chips, over the bricks, above the pigs' heads and the
                text card); leaves with exit 0.25 s before the Farsi swap.
  2  f156-357   (5.2-11.9 s) same picture; text card blur-swaps to Farsi, rtl, Vazirmatn; chip "فارسی" joins and
                turns violet. fa Puck at 5.7 s, fa Achird at 9.95 s. Whoosh on the swap.
  3  f357-546   (11.9-18.2 s) same; German text, chip "Deutsch". de Puck at 12.4 s, de Achird at 15.55 s.
                From 16.4 s: line "One page. Three languages." lands top on paper and holds; the scene exits to paper
                at 17.95 s.
  -  (cut 2026-11-20: the S21 recording shot, 18.2-23.2 s; the page scene already shows the voices.)
  4  f546-672   (18.2-22.4 s) type carries it: "15 languages" lands big, "for the app, the books and the audio",
                the 15 language names in their own scripts pop in 60 ms apart.
  5  f672-870   (22.4-29.0 s) why: page 15 (moonlit night) behind, the ground colour floods over its lower part; lines
                "I built Dino for kids growing up" / "between two or three languages" (accent on "two or three") /
                quiet "like second-generation kids in migrant families".
  6  f870-1095  (29.0-36.5 s) the real "Your voice" screen at 0.78; Pim's row ringed at 1.8 s; no zoom: at 3.7 s the
                screen slides down (power2.inOut 0.8 s) until "Your voice stays on this phone. It is never uploaded."
                sits on the frame's vertical centre (y 675), then lights yellow; title "Grown-ups record their voice /
                for any character" under the screen first, then at the top of title-safe (y 150).
  7  f1095-1320 (36.5-44.0 s) end card: "Built in 5 weeks with Claude" lands (Claude in violet), the app icon is
                carried in, "Dino / Kids Books & Stories", then the three stores pop in one by one; hold 3 s.
BANNED:  every default in the skill's spec.md list (dark void, glow, purple/blue gradients, glass cards, cream paper with
         serif and mono labels, everything centred and fading up, three cards in a row, docked logo, titles that drift);
         plus for this film: a drawn phone mockup; a made-up screen; any voice not from the app's own clips; a
         recorded "parent" voice that nobody recorded; claims beyond the slice's facts list ("with feelings",
         "Google TTS", user counts).
QA:      f0 (thumbnail: picture + caption + English text), f100 (Pim highlight), f240 (Farsi rtl shaped, joined
         letters), f420 (German), f515 (three chips + line), f590 (15 languages), f750 (why lines inside title-safe),
         f1035 (record screen centred, readable at 360 px), f1270 (end card, all three stores).
```

## Sound

The voices are the app's own pre-made clips for page 13 (`content/dino/three-little-pigs/audio/<lang>/`, Gemini
voices Puck for Pop, Achird for Pim): en 56 and 58, fa 57 and 59, de 56 and 58 in the book's manifest. Effects come
from the HyperFrames bundled set (whoosh on the language swaps, pop on each store). The music bed is HeyGen's free
catalog track "Astral Generated Music: 3602ebd4" (cheerful upbeat preschool pop, 43 s; the user's pick 11 of 11,
2026-11-20; `assets/bgm/cheerful-kids-11.m4a`). Source and licence: HeyGen free music catalog via HyperFrames; the
catalog gives no separate licence text. It plays 0.14 under every voice (to 16.8 s), rising to 0.45 by 18.0 s where
nobody speaks, faded out over its last 3.5 s (the end card).
