# The Town Mouse and the Country Mouse: art record

Store story (stem `town-mouse-country-mouse`, version 7). Retold as 15 pages with one action each
(`dino-storytelling` skill, Slice 121).

Version 7 (Slice 146 Waves B+C and Slice 156, 2026-10-27): English reworded in the spoken register ("far, far away", "guess who
came to visit? Town Mouse!", "never ever", "Wow!", "teeny-tiny", "right past", "started to giggle", "Bye-bye!", "okay?") and every
sound word is now its own triple piece: "The wind blew in the grass. Swish, swish, swish!" (pages 4 and 14), "The crickets sang. Chirp,
chirp, chirp!", "Clatter, clatter, clatter! The plates rattled in the sink.", "Outside her window, the town hummed. Hum, hum, hum!", and a
new "Ha, ha, ha!" on page 12. Same 15 page breaks and the same one action per page: **every page keeps its picture** (checked against
pages 2, 12, 13 and 15 on 2026-10-27: the hug, the two laughing on the sill, the wave from the step, her bedtime). The 12 other
languages were redone for every reworded piece in the same register with their own triples (French tags now "a dit", Finnish
dialogue colloquial: mä, sun, tuu, vaan); Farsi changed only where the meaning moved (the split sound sentences, guess who, never,
teeny-tiny, wow, ha ha ha, bye-bye, okay). **Art status (2026-10-26): cover and 15 pictures accepted by the illustrator and the full review passed on 2026-10-26 (fixes: zh hoover sound 嗡嗡嗡, fr bedtime sound Vrrr, ja shared line neutral and evening word).** The cover and all 15 pages are new; the
text-only version 4 had none.

**Gentle retelling (Decision 130):** in Aesop the Town Mouse sneers at the country food, and in town a cat and then a barking
dog burst in, so the mice flee and hide while the guest decides "better a nut in peace than a cake in a fuss". Here:
- **No cat, no dog, no chase, nobody hiding or fleeing.** The town is simply loud and busy: plates clattering in the sink while
  the cook washes up and hums, a round hoover rolling past all by itself, the wind banging the big front door. Each noise makes
  Country Mouse jump (a startle, never fear), Town Mouse calmly says "It's only the ___", and at the third he giggles at himself.
- **Nobody is mocked.** Town Mouse loves the country lunch ("Yum, yum, yum!", her first blackberry); she just finds the country
  too quiet. Country Mouse loves the town feast ("Yum, yum, yum!"); he just finds the town too busy. Each says the same kind
  refrain to the other: "Your home is lovely, but I love mine."
- **They stay best friends**: they laugh together on the windowsill, wave goodbye ("Come back soon!" / "You come and visit me
  too!"), each goes to bed happy in their own home, and they visit each other every summer and every winter.
- Rule 1 details: the cook's sink has no steam; there is no oven, stove, fire, candle or flame anywhere (the town lights are house
  windows; Town Mouse's room has no lamp flame). The mice ride in the soft hay at the back of the cart, never near the wheels or
  hooves. The hoover rolls *past* them, never towards them, and has no face.

**Why everyone does what they do:**
- **Town Mouse visits** because Country Mouse is her best friend. **She wriggles** in the long grass because she is used to bustle
  and the quiet makes her fidgety; that is why she invites him to her house, where it is busy.
- **Country Mouse lays out his best food** because she is his guest. **He jumps** at each town noise because his field is quiet and
  he has never heard plates, a hoover or a banging door. **He giggles** at the third because his hat flying off is funny, and he
  finds out (by himself, nobody tells him) that town is "a bit too busy" for him.
- **The cook** is washing up after the family's breakfast and never notices the mice; **the family** has just finished eating,
  which is why the crumbs are on the table. **The farmer** is driving his hay to town and back, so the mice can ride in the hay.
- **The lesson**, said by each friend in turn and never as a put-down: your home is lovely, but I love mine; and both are
  right ("Home, sweet home", said by each in their own bed).

**Refrains (identical every time):**
- `"Welcome to my home!" said Country Mouse.` (page 2) / `said Town Mouse.` (page 7).
- `"Yum, yum, yum!" said Town Mouse.` (page 3, country lunch) / `said Country Mouse.` (page 8, town table).
- `"Your home is lovely, but I love mine," said Town Mouse.` (page 5) / `said Country Mouse.` (page 12).
- The three-step build-up, pages 9-11: `"What's that noise?" said Country Mouse.` (pages 9 and 10) and `"It's only the plates /
  hoover / door," said Town Mouse. "Town is always busy!"` The third is different: no question, Country Mouse giggles instead.
- `The wind blew in the grass. Swish, swish, swish!` (pages 4 and 14). `Clip-clop, clip-clop, clip-clop!` (pages 6 and 13).
- `"Home, sweet home," said Country Mouse.` (page 14) / `said Town Mouse.` (page 15).

**Cast and voices (Slice 156, the book's own choices, `StarterVoices.cs`):** Town Mouse and Country Mouse (`book.json` `characters`),
the only speakers. Genders are chosen: **Town Mouse is "she", Country Mouse is "he"** (as in version 4's text and every translation,
so the two friends sound different).
- **Town Mouse:** female, young, a mouse (book animal); bright and bouncy (lively, used to bustle). Suggests `bee` (Busy Bee, "bubbly and quick").
- **Country Mouse:** male, young, a mouse (book animal); soft and warm (gentle, loves the quiet). Suggests `piglet` (Cheeky Piglet,
  "small and quick": the small boy's voice nearest to a mouse; in English it is the mouse's own voice, am_puck, one flavour over).
The roster's `mouse` voice is a boy in English and Farsi and a girl elsewhere, so neither mouse can suggest it; `bee` is a girl and
`piglet` a boy in every language, so **both are mice again** (`StarterAnimals.cs` no longer stores them as no animal) and squeak.
Real casts dumped for every language and all three narrators on 2026-10-27 (each suggestion kept everywhere; no shared voice and
flavour; no whisper flavour; every gender right): en am_puck Small / af_sky Bouncy; fa gyro Chirpy / mana Small; zh yunxia Small /
xiaoni Bouncy; es and pt alex Small / dora Bouncy; nb mnn Small / knv Bouncy; the eight Supertonic languages M2 Small / F5 Bouncy.
Farsi has one woman's voice (mana), so with the default night narrator Town Mouse shares mana a flavour apart, and with the
"storyteller" narrator Country Mouse shares gyro a flavour apart: a voice-count limit, as in every Farsi book, not a clash.
`StoreStoryTests.TheTownMouseAndTheCountryMouse_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster` checks all 14
languages. Everyone else is silent and never named in a speech tag. Translations keep each gender: Russian городская мышка
(сказала) and деревенский мышонок (сказал); Arabic فأرة المدينة and فأر الريف; French la Souris des villes and le Rat des champs
(La Fontaine's names, kept from version 4); Spanish la Ratona de ciudad / el Ratón de campo; Portuguese a Ratinha da cidade /
o Rato do campo; Japanese Town Mouse says わたし, Country Mouse ぼく, and the shared refrains are neutral. Pieces said by both
(welcome, "Your home is lovely") are written addressee-neutral in Spanish and Portuguese («¡Pasa, pasa, esta es mi casa!»).
Sound words in every language but en/es/zh/fa are the repeat form and untested aloud: flag the ru, ar, ja and Nordic sounds (and the rest) for a listen. Chinese «砰砰砰！» and «嗡嗡嗡！» and Farsi «بوم بوم بوم!» / «ووم‌ووم‌ووم!» follow the text review (2026-10-26). Version 7 adds plain Farsi triples «خش خش خش!», «ها ها ها!» and «وز وز وز!» (no lone sound word; «وای» only inside «وای، چقدر خوراکی!»), Chinese «哇！太棒了！» for "Wow!" (no lone character) and «哈哈哈！»; the new "Ha, ha, ha!", "Hum, hum, hum!" and the split sounds need the same listen in every language.

**Style:** Children's picture book illustration, soft watercolour and coloured pencil, warm gentle colours, simple rounded
shapes, no text or letters anywhere. Portrait 3:4.

## Characters (canonical until a line-up is accepted; then rewrite from the line-up)

- **Country Mouse (he):** a small, round **warm-brown field mouse** with a cream tummy, big round ears with pink insides, a pink
  nose and a long thin tail; a little shorter and rounder than Town Mouse. He wears **green dungarees** and a **small straw hat with
  a red band** (his prop; it flies off on page 11 and hangs on a peg on page 14). No shirt, no shoes.
- **Town Mouse (she):** a slim **silver-grey mouse**, a little taller than Country Mouse, white tummy, long whiskers, big round
  ears. She wears a **lilac (light purple) dress with a white collar** and a **yellow ribbon bow between her ears**. Her prop: a
  **little pink bag** on a strap across her body (pages 2-7; hanging on a hook by her bed on page 15). No hat, no shoes.
- Look-alikes: brown fur + straw hat + green dungarees (Country) against grey fur + yellow bow + lilac dress (Town).

Silent extras (never speak, no clothes unless stated):
- **Hedgehog** (country only): one friendly brown hedgehog with soft rounded spines, about three times a mouse's height, smiling.
- **Two robins** (country only): small brown birds with orange-red breasts.
- **The farmer and his horse** (pages 6 and 13): a big gentle brown cart horse with a white blaze and shaggy white feet, pulling a
  wooden cart piled with hay; a friendly farmer in blue overalls and a brown flat cap at the front, seen from behind or the side.
- **The cook** (page 9 only): a round, cheerful woman in a white apron and a blue headscarf, seen from behind at the sink.
- **Three sparrows** (town only): small brown-and-grey town sparrows, always exactly three.
- **The hoover** (page 10, an object, not a character): a round, flat, grey robot hoover with no face, no eyes, no mouth.

There is **no cat, no dog and no other animal or person** anywhere in the book.

## Places (the same in every picture)

- **The oak and the field:** a big old oak tree at the edge of a quiet green field of long grass and wildflowers; between its roots a
  round mouse hole with a little wooden door left open, a flat stone in front of it; a low hawthorn branch where the robins sit.
  Pages 1-5 and 14 (page 14 shows the hole in a cutaway: a cosy round room with a bed of moss and a leaf blanket).
- **The country lane:** a dirt lane between hedges, the town's rooftops far ahead. Page 6.
- **The tall town house:** a tall narrow house in a busy street of tall houses, a big blue front door at the top of three stone
  steps. Pages 11 (from inside) and 13.
- **The kitchen:** a big kitchen with a long wooden table, a white sink under **one wide window with a single pane**, a
  black-and-white tiled floor, and low in the wall a **tiny round red door** to Town Mouse's room. No oven, stove or fire visible.
  Pages 7-9 and 12.
- **The hall:** a long hall with a wooden floor, the bottom of a staircase, the big blue front door and one wide single-pane window.
  Pages 10-11.
- **Town Mouse's room:** a tiny cosy room behind the kitchen wall, a little bed with a patchwork blanket, a hook with her pink bag,
  and **one small round window** looking out on the town. No candle or lamp flame. Page 15.

## Picture plan

Every page: the whole scene sits in the top 70% of the picture; the bottom 30% is plain grass, lane, floor tiles or wood with nothing
important in it. No text, letters, words, sound effects, music notes or writing anywhere ("Tweet", "Clatter", "Vroom", "Bang",
"Clip-clop", "Swish", "Hum" and "Ha, ha, ha" are never drawn in). Every page has **three or more characters** (Gemini refuses some 1-2 character
scenes): silent extras fill the scenes with only the mice. Version 7 keeps every picture (same actions; column "Picture" says keep).

| Page | One action (who, count, where, when) | Picture |
| --- | --- | --- |
| Cover | Sunny afternoon, on a wooden stile where the field meets the lane: Country Mouse (1) and Town Mouse (1) sit side by side, arm in arm, smiling at the viewer; Hedgehog (1) at the foot of the stile, two robins (2) on the top rail; far behind, the oak on one side and the town's rooftops on the other | keep (v7) |
| 1 | Morning, the oak: Country Mouse (1) sits on the flat stone outside his hole in the sunshine, eyes closed, listening, a happy smile; two robins (2) sing on the low branch above; Hedgehog (1) sits nearby in the grass | keep (v7) |
| 2 | Sunny morning, in front of the hole: Country Mouse (1) hugs Town Mouse (1), who has just arrived with her pink bag; Hedgehog (1) and two robins (2) watch, smiling | keep (v7) |
| 3 | Midday, at the flat stone: lunch is laid out (seeds, blackberries, one hazelnut); Town Mouse (1) tastes a big blackberry, eyes closed with delight; Country Mouse (1) watches, pleased; Hedgehog (1) and two robins (2) look on | keep (v7) |
| 4 | Afternoon, in the long grass: Country Mouse (1) lies on his back, paws behind his head, watching the clouds; Town Mouse (1) lies beside him, wriggling, legs kicking, restless; one robin (1) on a grass stalk, Hedgehog (1) dozing nearby | keep (v7) |
| 5 | Sunset, pink and gold, in front of the hole: Town Mouse (1), pink bag on, points towards the far-off town rooftops on the horizon, inviting; Country Mouse (1) nods happily; Hedgehog (1) and two robins (2) on their branch | keep (v7) |
| 6 | Morning, the country lane: the big brown horse (1) pulls the hay cart; the farmer (1) at the front, seen from behind; Country Mouse (1) and Town Mouse (1) peep out of the hay at the back, smiling | keep (v7) |
| 7 | Morning, the kitchen, mouse-eye view low by the wall: Town Mouse (1) holds open the tiny round red door, welcoming; Country Mouse (1) looks at it, amazed; above them, three sparrows (3) on the outside sill of the wide window | keep (v7) |
| 8 | Morning, on the kitchen table among breakfast crumbs, cheese bits and grapes: Country Mouse (1) holds a grape, munching happily; Town Mouse (1) spreads her paws, "help yourself"; three sparrows (3) on the outside sill. No people | keep (v7) |
| 9 | Morning, the kitchen: the cook (1) at the sink, seen from behind, washing plates, no steam; on the table Country Mouse (1) jumps in the air, surprised; Town Mouse (1) beside him, calm and smiling | keep (v7) |
| 10 | Morning, the hall: the round grey robot hoover (no face) rolls along the floor past the stairs; on the bottom step Country Mouse (1) covers his ears with his paws; Town Mouse (1) smiles; three sparrows (3) on the hall window sill outside | keep (v7) |
| 11 | Late morning, the hall: the big blue front door swings in the wind, a few leaves blowing in; Country Mouse (1) giggles mid-jump, his straw hat flying in the air above him; Town Mouse (1) laughs beside him; three sparrows (3) at the hall window | keep (v7) |
| 12 | Evening, the kitchen windowsill (inside the single pane): Country Mouse (1), straw hat back on his head, and Town Mouse (1) sit side by side laughing; outside, dusk, town house windows lit warm yellow; three sparrows (3) sleepy on a ledge outside | keep (v7) |
| 13 | Morning, the street in front of the tall house: the horse (1) and the farmer (1) drive the hay cart away; Country Mouse (1), straw hat back on his head, waves from the hay at the back; Town Mouse (1) waves from the top step by the big blue door | keep (v7) |
| 14 | Night, moon and stars, the oak in cutaway: Country Mouse (1) in his moss bed under a leaf blanket, smiling sleepily, eyes closing, straw hat on a peg; outside by the roots Hedgehog (1) curled up asleep; two robins (2) asleep on their branch | keep (v7) |
| 15 | Night, Town Mouse's room: Town Mouse (1) snuggled in her little bed under the patchwork blanket, smiling, pink bag on its hook; through the small round window the town at night, windows lit; three sparrows (3) asleep on the ledge outside her window | keep (v7) |

Check (dino-storytelling section 4):

- [x] Every page's picture differs from its neighbours (place, action or who is there).
- [x] No page needs two pictures.
- [x] Characters' faces and the action fit in the top 70% of a 3:4 portrait picture.
- [x] Each character's look is fixed in the line-up, not invented per page.

The last item is ticked for the plan: every look is fixed in the character lines above, and the illustrator draws one line-up
(the two mice side by side, plus Hedgehog, a robin and a sparrow for scale) before any page.

Notes for the illustrator:

- Every prompt: "exactly one brown mouse in green dungarees and a straw hat, and exactly one grey mouse in a lilac dress with a
  yellow bow", plus the exact count of each extra. Gemini doubles look-alikes: list each character in its own place, left to right.
- **No cat, no dog, anywhere**, not even in the background, a picture or a window. Say so in every town prompt.
- Pages 9-11 are a startle, never fear: Country Mouse surprised or giggling, never cowering or crying; Town Mouse always calm.
- Page 9: the cook's back only, no steam; page 10: the hoover has no face; page 11: the door swings, nobody is near it.
- Pages 7 and 15 are both small-room views: page 7 is the outside of the round red door in the big kitchen (morning); page 15 is
  inside her tiny room (night).
- Pages 14 and 15 are both bedtime: keep them apart by place (oak cutaway with moon, against her room with the round window).
- Ask for "no text, letters, words, sound effects, music notes or writing anywhere".

## Check results

| Page | Attempts | Size | Why earlier attempts failed |
| --- | --- | --- | --- |
| Cover | 2 | 1792x2400 (3454671 B) | 1: two hedgehogs. Hedgehog partly in the bottom 30% (cover has no text panel) |
| 1 | 2 | 1792x2400 | 1: music notes drawn above the robins |
| 2 | 1 | 1792x2400 | hedgehog sits on the robins' branch (odd, harmless) |
| 3 | 2 | 1792x2400 | 1: second bird was a sparrow, hedgehog up in the tree |
| 4 | 1 | 1792x2400 | |
| 5 | 1 | 1792x2400 | |
| 6 | 1 | 1792x2400 | mice small in the hay, fine |
| 7 | 1 | 1792x2400 | window has crossbars (no multiplied faces) |
| 8 | 2 | 1792x2400 | 1: Town Mouse's bow became a neck tie, snow outside, fireplace hood |
| 9 | 1 | 1792x2400 | cook mostly back view (side profile), mice on the worktop; Country Mouse startled, mouth open, not cowering |
| 10 | 2 | 1792x2400 | 1: hoover had two dots that read as eyes |
| 11 | 1 | 1792x2400 | soft vignette-style painted panel on white (reviewer: redo if unwanted) |
| 12 | 2 | 1792x2400 | 1: sparrows drawn indoors in the bottom 30% plus jug and bowl clutter |
| 13 | 1 | 1792x2400 | |
| 14 | 1 | 1792x2400 | |
| 15 | 1 | 1792x2400 | hard-edged framed panel with white margins, like page 11 (reviewer: redo if unwanted) |

Model 3.5 Flash-Lite, one picture per chat with the line-up pasted. Drawn 2026-10-26 about 19:50-21:50. Line-up: 1 attempt, accepted
(Country Mouse draws ginger-brown rather than warm-brown; keep as the canonical look). Sheets not drawn.
Full review (2026-10-26) failed pages 2, 7, 11 and 15; all four redrawn and accepted: 2 (hedgehog on the ground, two robins alone on the branch, full-bleed; mice sit a little low, feet reach about 83%), 7 (round-topped red door above her ears, one single-pane window, three sparrows at the glass), 11 (hinged open door, stairs right, sash window left, three sparrows, full-bleed; first redraw attempt of 2 was a framed panel and was rejected), 15 (full-bleed, warm smooth-walled room).

Page 2 redrawn a third time at the coordinator's request (mice and hedgehog in the upper 65%, face-to-face hug); accepted.

## Slice 350 high-res run (2026-11-19, part D1)

`high-res/` holds the flat JPEG q85 pack (cover, 1-15 and `back-cover.jpg`, each under 1 MB, made with `prepare-story-pictures.ps1 -HighRes`) and `contact-sheet.jpg`. The untouched Gemini downloads (1792x2400, 2.7-3.6 MB each, plus the untouched back cover) are kept in the scratchpad folder `C:/Users/ashka/AppData/Local/Temp/claude/717394277bc5a198/c1bcd59c-ab93-4eda-a774-ce8a5778c961/scratchpad/tm/final/` (candidates and recovered chat files beside it in `tm/new/`, `tm/keep/`).
- **Recovered from the Gemini history, untouched** (the accepted pictures, each matched to its app picture by pixel comparison, difference under 0.5), chat id and size: cover 4c644fd4e7394418 (3424366 B), 2 263e697d0cab39d0 (3548750), 7 808771f706eec64c (2824332), 8 cae9ed1242976abb (2903210), 9 bcfaca0efa320721 (2739588), 10 831214fafc43dece (3184923), 11 c3a5235b54c80572 (3043036), 12 b96d2968668299e4 (2985098), 15 aac5493d1c156e14 (2982794).
- **Redrawn on 4 Hydrogen** (the user's pick of 2026-11-19; Gemini no longer offers 3.1 Pro), one new chat each, full bleed, as copies of the accepted picture (the originals of 1, 3, 4 and 14 were not in the history, so the app picture was the reference): 1 chat 55c43b8bb9e3c758 (3296240 B, try 1), 3 511ecbe02d0198c3 (3386161, try 1), 4 6dad18ea3279a87b (3201585, try 1), 5 6ccb019f2263e701 (3439702, try 1), 6 13be0f970b1bf14d (3245488, try 1), 13 c6dbc36df2b81203 (3045776, try 3 of 3), 14 19cd2d53fe026806 (3258271, try 1). Each small copy was checked for counts, outfits, limbs, no words, scene to the bottom edge and line70.
- **For the user:** page 13's front hoof tip reaches about 74% (line 73%) in all three tries, since Hydrogen keeps the accepted composition; try 3 kept because its bottom is full cobbles (try 2 had a washed-out bottom). Page 14's hedgehog nose touches the 73% line. Pages 2 and 8 (not on the fix list, kept as accepted) still have a plain flat strip at the bottom (2 green, 8 beige).

## Reviewer, 2026-11-19 (Slice 350 part D2, full mode, high-res)

**PASS.** The `dino-story-reviewer` checked the cover, pages 1-15 and the back cover. It used small copies and line70 overlays made from the untouched originals in `tm/final/`, plus full-size crops of page 13's hoof and page 14's hedgehog. It also checked that the repo's `high-res/` files are the same pictures: all 17 are 1792x2400, every file is under 1 MB, and the pixel difference is 0.16-0.27 on a 0-255 scale.
- **The fix pages (1, 3, 4, 5, 6, 13, 14)** now run the scene to the bottom edge with grass, lane, cobbles or roots, and none has a white strip. Each is a faithful copy of its accepted app picture, checked side by side. Counts, outfits, limbs, props and time of day are all right, and none has words.
- **Page 13:** the horse's planted front hoof ends at about 74.4% (measured on a full-size crop), 1.4 points past the 73% line. The horse has four legs. The slice's D1 rule allows at most 3 tries and then the best is kept and listed, so the page is listed for the user, not redrawn.
- **Page 14:** the hedgehog's nose sits exactly on the 73% line (72.9%) and is not inside the strip, so it passes.
- **Accepted pages listed for the user (never redrawn without their word):** on the cover the hedgehog sits mostly in the bottom 27%. On page 11 both mice's legs and Town Mouse's dress are below the line, at about 80%. On pages 2 and 7 the feet touch the line. Pages 2 and 8 have a plain flat strip at the bottom.
- **Consistency notes (accepted pictures, not blocking):** the mouse-hole door moves between pictures. It is in the roots on page 1, under the flat stone on page 2, a large door in the trunk on page 5, and a high round cutaway room on page 14. The kitchen window has crossbars on pages 8 and 9, but the plan asks for a single pane.
- **Back cover:** both mice are whole and dressed as in the book, there are no words, the middle is open sky for the credits, and it matches the app's `back-cover.jpg`. `book.json`'s credit (`storyBy` Aesop, `retold`) and its `namesByLanguage` spellings in all 14 languages are correct. The back cover was not checked in the running app.
