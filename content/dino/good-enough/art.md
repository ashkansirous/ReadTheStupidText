# Good Enough: art record

Bundled starter (`StarterBuilder.GoodEnoughId`, stem `good-enough`, file `good-enough.dino`, version 4), ages 5-8, author Ashkan Sirous.
The user's own picture book (Slice 94), built from its pictures and `content/dino/good-enough/lines.json`, not from a `story.md`.
An original story, not a retelling. Present tense throughout (the printed pages are present tense; pages 8-10 match).

**10 pages, all pictures kept** (shipped version; the 2026-11-13 dialogue draft below has 11 pages). Pages 1-7 have the English words **printed in the picture**, with a box per printed line in
`lines.json` (`x`/`y`/`w`/`h`) for the highlight. Their English must match the printed words exactly, so it is never reworded
unless the pictures are redrawn: Slice 146 Wave B (spoken English, 2026-10-27) reworded only pages 8-10, which have no printed
words (`"text"`, no `lines`). The translations of every page were redone in the spoken register (Decision 152).

**Dialogue draft (2026-11-13, not shipped, waiting for the user to read it):** the user asked that Carrot and Hazelnut say their own
lines instead of the narrator reporting them, then "add more lines to them. the referee can also talk". The reported speech on
pages 3, 5 and 10 became quotes, and every page now has spoken lines (Referee on page 10), tagged `says <Name>` in the present
tense like the book's existing `says Hazelnut`. The Referee is tagged `says Referee` (the repo's role-as-name convention, as
`said Woodcutter`): the attributor skips "the", so `says the Referee` gave his lines to Carrot. The user's narrator sentences
are kept word for word, line breaks included, so their translations still match. Every page is now text-only (`"lines": []`):
the printed words in pictures 1-7 no longer match what is read, until Slice 350 redraws the book without text. No audio,
translation or version bump yet.

Third round (the user: "add more reasons to see ... someone scores, people shout goaaaal, but they cannot see ... when they are
in the middle of the field, the same people should shout ohhhhhhh! like being surprised. nothing bad"): a **new page 3**, the
goal they miss, so the book is now **11 pages**; the Crowd speaks there ("Goooaaal!") and on page 10 ("Ohhhhh!"). The new page
points at `2.jpg` only as a placeholder so the draft builds: it needs its own picture in the redraw. Pages 4-11 keep their
pictures `3.jpg`-`10.jpg` until the redraw renumbers them.

Fourth round (the user: "use better wording ... it is a dialog by children"): every Carrot and Hazelnut line rewritten the way
3-4-year-olds talk (two to five short words, repetition, "Huh?", "Aww!", "I wanna see!", "Closer! Closer!", "Oops!"), the
Referee's lines simplified for small listeners. Narrator sentences unchanged. Hazelnut's last line, the user's own words
("The best seat is the one where we're together!"), became the child's "The best seat is next to you!", keeping the
existing pieces "The best seat," and "says Hazelnut,".

**Gentle story (Decision 130):** nobody is hurt or told off. Carrot runs onto the pitch out of excitement; the players only stop
and stare and the crowd gasps a surprised, friendly "Ohhhhh!" (nobody cross); the referee walks them back and explains kindly
("The grass is for the players, little ones"), Carrot says sorry, the referee says "That's okay. Enjoy the game!"; Hazelnut
closes with the point of the book. Calm ending, together. Hazelnut's "Carrot, stop! It's good enough!" on page 8 names the title.

**Crowd sound words:** "Goooaaal!" (page 3) and "Ohhhhh!" (page 10) are lowercase stretched words, no capitals (the voices would
spell capitals). Each is one piece, so every language writes its own stretched cheer and gasp. Both need a listen once
rendered: an engine may read a stretched word short or spell it.

**Sound words (Decision 151):** `Zoom, zoom, zoom!` (page 8), one piece so each language writes its own triple. Measured
languages: en, es, zh, fa. Not yet listened to in ar, da, fi, fr, ja, nb, nl, pt, ru, sv (flag for a listen).

## House style

Children's picture book illustration, soft watercolour and coloured pencil, warm gentle colours, simple rounded shapes, no text
or letters anywhere. (The existing pictures predate this line: they carry the printed story words on pages 1-7 and signs with
"Bright Stadium", "Great Views, Row C" and "Exit". The redraw (Slice 350) leaves out all of them. The story no longer names Row C: page 11 says "the front row" (user, 2026-11-13: "row c was never intentional. front row makes more sense").)

## Characters

- **Carrot**: a lop-eared rabbit, tan with a white tummy, the taller of the two; blue-and-white striped scarf; no clothes, no prop.
  Speaks on pages 1, 3, 4, 6, 7, 9, 10 and 11 (dialogue draft). Told apart from Hazelnut by the long floppy ears and the blue scarf.
- **Hazelnut**: a russet golden-brown squirrel (never grey) with a cream tummy, dark-tufted pointed ears and a big bushy brown tail held up in an S-curve, a head shorter than Carrot (0.8 of his height); orange-and-white striped scarf; no clothes.
  Speaks on pages 1, 2, 3, 4, 5, 6, 8, 10 and 11. Told apart by the bushy tail and the orange scarf.
- **Referee** (the speaker name; the narrator says "the referee"): a badger, much taller than both, black-and-white striped
  referee shirt, white shorts, striped socks, black boots; a whistle. Speaks twice on page 11, kindly.
- **The players** (sheet `characters/players.jpg`, drawn 2026-11-13, no numbers or words): five upright cats, same size, small black boots, one tail each. Red team, plain red shirt, shorts and socks: a ginger tabby cat and a grey tabby cat. Light-blue team, plain light-blue shirt, shorts and socks: a black cat and a white cat. **Goalkeeper**: a black-and-white tuxedo cat in a plain bright green long-sleeved shirt, dark green shorts, green socks and big soft yellow gloves. Background only. About 20 cat players on page 10 use these looks (red, light blue, one green goalkeeper).
- **Fans group 1** (sheet `characters/fans-group-1.jpg`, grown-up animals, smiling, mouths closed, plain clothes): brown bear with a red scarf and a yellow knitted bobble hat; grey elephant with a green-and-white striped scarf and a green cap; tall yellow giraffe with a purple scarf and a purple beanie; orange fox with a blue jumper and a yellow scarf; black-and-white panda with a red shirt and a white scarf.
- **Fans group 2** (sheet `characters/fans-group-2.jpg`): brown moose with antlers, a mustard-yellow scarf and an orange knitted hat between the antlers; pink hippo with a purple hat and a purple-and-white striped scarf; grey-brown kangaroo with a plain orange shirt; fluffy white sheep with a blue scarf and a blue bobble hat; brown owl with a green jumper and a green-and-yellow striped scarf. No animal repeats between the two groups; no rabbit, badger, squirrel or human among the fans.
- **Whole-cast line-up** (`characters/lineup.jpg`, 2026-11-13): four rows on shared ground lines, every character at true relative size. The fronts are in rows 1-2 and the backs in rows 3-4. Rows 1 and 3 hold the friends, the referee, the cats, fox, sheep and owl; rows 2 and 4 hold the bear, elephant, giraffe, panda, moose, hippo and kangaroo. Heights in Carrot-heights: Carrot 1, Hazelnut 0.8 (a head shorter), cats 1.75, keeper 1.8, owl 1.7, sheep 1.9, fox 2.0, referee 2.3, hippo 2.4, panda 2.5, bear and elephant 2.7, kangaroo 2.9, moose 3.4, giraffe 3.9. The coordinator assembled it from Gemini's drawn line-up (try 3), cutting out each figure and scaling it to those heights on one canvas (scratch script compose.py, 2026-11-13), because Gemini drew each row at its own scale. Page prompts still say "Carrot and Hazelnut are tiny next to the grown-up fans" in words.
- **Crowd** (the speaker name; the narrator says "everybody"): the stadium fans: only the grown-up animals of fans groups 1 and 2
  (no people, no tan rabbits, no badgers besides the referee), in their own scarves and hats as on the sheets. Shouts "Goooaaal!" on page 3 and gasps "Ohhhhh!" on page 10; both times happy or surprised, never cross.

## Voices (Slice 156, `StarterVoices`)

- **Hazelnut**: suggested roster voice `bee` (Busy Bee, "bubbly and quick"), female, young, no person kind (a squirrel has no
  animal sound, so no kind is stated), qualities Bright, Warm. Female as the shipped cast already had her (supertonic F1) and as
  Slice 94's test calls her; the Arabic translation now uses her feminine verbs (تقترح، تقول). Where `bee` is missing or sits on
  the narrator's voice (Farsi, mana) the app matches the traits.
- **Carrot** (dialogue draft, 2026-11-13): suggested roster voice `puppy` (Playful Puppy, am_liam with the Bouncy flavour,
  "bouncy and bright"), male, young, qualities Bouncy, Bright; animal `Rabbit` in `book.json`'s `animals` (the name doesn't say
  it; Rabbit has no sound clip, so no animal sound plays). Male as every translation already treats him (Arabic يتمنّى، يعتذر).
  The young, bouncy kid voice the user asks for (Freddy and X Marks' Billy have the same pick); different engine voice and
  gender from Hazelnut's `bee`, so the two friends never sound alike. Premade audio (`audio-cast.json`): Puck, the young boy
  voice the other books give their eager boys (Jack, Freddy, Billy); Hazelnut keeps Leda. No clip rendered yet.
- **Referee** (dialogue draft, 2026-11-13): suggested roster voice `bear` (Grandpa Bear, bm_daniel, "deep and kind"), male,
  adult, qualities Warm, Soft; no animal kind (a badger has none) and no `being`. A warm, gentle grown-up so his two lines
  never sound like a telling-off; a different engine voice from Carrot's and Hazelnut's. Premade audio: Orus, the big, warm,
  kind grown-up voice of Big Billy, Papa Bear and the Woodcutter. No clip rendered yet.
- **Crowd** (dialogue draft, 2026-11-13): there is no group or chorus voice in the app or the premade-audio tool, so the Crowd is
  an ordinary speaker (`says Crowd`, the role-as-name convention), cast as one big voice. Suggested roster voice `giant`
  (Friendly Giant, am_santa with the Giant flavour, "big, deep and slow"), male, adult, qualities Booming, Bright; no animal, no
  `being`. Premade audio: Fenrir (the booming, excitable voice of the Hare and Tummy) with a style of "a big, loud, happy
  stadium crowd shouting together" and a direction for a joyful cheer and a surprised, friendly "oh". A single voice told to
  sound like a crowd, not a real many-voice crowd. No clip rendered yet.
- The players have no lines, so no cast entry.

## Picture plan (one line per page; 11 pages since the goal page; Slice 350 redraws all of them without text)

The "Keep" notes are for the draft only: the whole book is redrawn text-free in Slice 350, and these lines are what each redrawn
picture must show.

1. Carrot and Hazelnut stand at the stadium gate, excited; 2 friends plus small fans behind; outside the arch; day. Draft: keeps `1.jpg`.
2. They sit in the back row and can't see the ball; the players look tiny far below; 2 friends, crowd; top of the stands; day. Draft: keeps `2.jpg`.
3. **New.** Someone scores: the crowd in front of them jumps up cheering with arms in the air, scarves waving, mouths open in a happy shout, while Carrot and Hazelnut sit behind them, peeking and puzzled, unable to see; the ball in the far-off net may show as a tiny dot; 2 friends, crowd; back rows; day. Draft: placeholder `2.jpg`, **needs a new picture**.
4. Carrot wishes aloud to see better while Hazelnut points down; 2 friends; back rows; day. Draft: keeps `3.jpg` (printed words are the old reported speech).
5. They wiggle through tall legs down a few rows; 2 friends, crowd legs; the stands' stairs; day. Draft: keeps `4.jpg`.
6. They have a good view and Carrot turns to Hazelnut wanting closer; 2 friends; middle rows; day. Draft: keeps `5.jpg` (printed words are the old ones).
7. They sneak closer down the rows; 2 friends; lower rows; day. Draft: keeps `6.jpg`.
8. They reach the front row and Carrot keeps going while Hazelnut calls after him; 2 friends; front row by the barrier; day. Draft: keeps `7.jpg`.
9. Carrot runs past the front row onto the grass; 2 friends, crowd behind; edge of the pitch just past the front row; day. Draft: keeps `8.jpg`.
10. The players all stop and stare at the two in the middle of the field, and **the same crowd in the stands gasps in friendly surprise** (round "oh" mouths, hands to cheeks, some smiling; nobody angry or pointing); 2 friends, about 20 cat players, crowd in the stands behind; centre circle; late afternoon light. Draft: keeps `9.jpg`, whose crowd is too far and small to show the gasp: **the redraw must bring the stands closer**.
11. The referee walks them back to the front row and talks kindly with them; 2 friends, 1 referee, crowd; front row by the barrier; day. Draft: keeps `10.jpg` (the referee has his whistle in his mouth while he talks; the redraw shows it hanging on its cord, mouth free and smiling).

Checklist (`dino-storytelling` section 4):

- [x] Every page's picture differs from its neighbours (place, action or who is there). (Page 3 differs from page 2 by the
      crowd standing and cheering; in the draft it borrows `2.jpg` until it is drawn.)
- [x] No page needs two pictures.
- [ ] Characters' faces and the action fit in the top 70% of a 3:4 portrait picture. (Not for this book: pages 1-7 print the
      words in the bottom band themselves, and on pages 8 and 10 the friends stand low in the frame. Accepted as the user's
      own pictures; a redraw would fix it.)
- [x] Each character's look is fixed in the line-up, not invented per page.

## Character sheets and line-up (Slice 350 redraw, setup mode, 2026-11-13)

Model 3.1 Pro, 1792x2400, saved with `-Untouched`, no text anywhere; files in `characters/`: `carrot.jpg`, `hazelnut.jpg`, `referee.jpg`, `lineup.jpg` (Carrot, Hazelnut, the Referee; the Crowd and the cat players have no sheet and are described in words).
Each sheet in its own new chat. Carrot's chat pasted page 10 as the look reference and came out right. Hazelnut: try 1 and 2 pasted the same reference and a ghost badger leaked into the middle of the sheet (rejected), try 3 with no paste (words only) was clean. Referee: words only, clean (whistle on its cord, mouth closed). Line-up: one montage of the three sheets pasted, clean.
Canonical looks from the sheets: Hazelnut is a plainer grey squirrel with a cream tummy and a big curled tail (cuter than the old pictures); the Referee has the whistle hanging, never in his mouth. No place sheets drawn (not in the brief); the stadium places (gate, stands, front row, pitch) are best described per page.

**Fans, players and line-up (setup mode, 2026-11-13, after the 23:05 reset, model 3.1 Pro, `-Untouched`, 1792x2400, each in its own new chat, words only for the sheets):** `fans-group-1.jpg` (2.6 MB), `fans-group-2.jpg` (2.4 MB) and `players.jpg` (2.2 MB) each passed first try (ten drawings, front row above back row, outfits as the cast lines say, no text). Line-up: pasted one 3x2 montage of the six sheets; try 1 left the fox out of the fans' front row and the cats from behind in row 1 (rejected); try 2 had the cats' front missing again; try 3 passed on count, front/back and outfits (18 characters in each of rows 1-2 and 3-4), but the scale is weak (see the line-up line above; three tries is the limit). Sheets were drawn in a new chat each (not one chat). First picture 23:08; no place sheet drawn (not asked).

**Hazelnut redone (coordinator check against the book's pictures, 2026-11-13):** the first sheet drew her plain grey with a curled grey tail, but the book's Hazelnut is warm russet golden-brown with a cream tummy, tufted ears and a big bushy brown tail. Redrawn from words only (no paste: a whole-page paste leaks other characters) in a new chat, then the line-up was redrawn from a montage of the three sheets in another new chat; both replace the grey versions in `characters/`. Canonical look: russet golden-brown squirrel, cream tummy and chest, dark-tufted pointed ears, big bushy brown tail held up in an S-curve, orange-and-white striped scarf with fringe. The sheet's fur is a little more orange than the book's tan-brown.

**Place sheets (setup mode, 2026-11-14, model 3.1 Pro, `-Untouched`, 1792x2400, each in its own new chat, words only, no paste, first try each; EMPTY: no animals, people or players, no text; stone arches and wall, terracotta-peach and blue seats, green pitch, pale sky, bunting, floodlights):**
- `characters/place-back-rows.jpg`: top back rows looking down on a far small pitch (pages 2, 3, 4).
- `characters/place-middle-rows.jpg`: middle rows, pitch clearly nearer (pages 5, 6).
- `characters/place-front-row.jpg`: front row, low stone barrier wall, grass and a goal right behind it (pages 7, 8, 9, 11).
- `characters/place-centre-circle.jpg`: standing in the centre circle with its white markings, stands close behind (page 10).
