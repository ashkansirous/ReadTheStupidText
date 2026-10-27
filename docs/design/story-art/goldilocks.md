# Goldilocks and the Three Bears: art record

Store story (`StoreTexts.Goldilocks`, stem `goldilocks`, version 7). Retold as 14 pages with one action each
(`dino-storytelling` skill, Slice 121). Version 6 (Slice 146 Wave A, 2026-10-27): Farsi rewritten in spoken Farsi, sound words as
plain repeats, no vowel marks; English, page breaks and every picture unchanged (all 14 pages keep their picture).

Version 7 (Slice 146 Waves B+C and Slice 156, 2026-10-27): English reworded in the spoken register (contractions such as
"Somebody's been eating my porridge!", "oops", "Uh-oh!", "And guess what?", "never ever", "fix" for "mend") and every
sound word a triple ("Pitter-patter, pitter-patter, pitter-patter!", "Drip, drip, drip!", "Creak, creak, crack", "Dab, dab, dab!",
and a new "Ha, ha, ha!" on page 11). Same 14 page breaks and the same one action per page: **every page keeps its picture**
(checked against the pictures on 2026-10-27; page 3's longer line still shows her calling at the open door, page 11 the hug and
the laugh). The 12 other languages were redone for every reworded piece in the same register with their own triples; Farsi
changed only where the meaning moved (oops, the split rain sentence, uh-oh, opened her eyes, never ever, okay, ha ha ha, guess what). **Art status (2026-10-26): pictures accepted, full review PASS (2026-10-26); shipped in `Store/goldilocks/`.** The cover and all
14 pages are new; the old 9-page text had no pictures, so nothing is kept. Text review: the first pass failed pages 3,
4, 11 and 14 (`reviews/goldilocks-text.md`); all four are rewritten as asked.

**Gentle retelling (Decision 130):** in the classic, Goldilocks walks into a stranger's house uninvited, eats,
breaks a chair, sleeps, then wakes to three bears, is frightened and jumps out of the window and runs away; nobody
says sorry. None of that is here:
- **She is not where she should be, and the text says so.** She has wandered too far from her mum's garden picking
  flowers when a rain shower catches her. She finds a house with its door wide open (Baby Bear forgot to shut it on
  page 1), calls "Hello? Is anybody home?", hears nothing, and "knew she should wait outside". "But she went
  inside to keep dry": the text marks going in as wrong at the moment she does it. She still does the naughty
  classic things (eats Baby Bear's porridge, sits in the chairs, sleeps in his bed), and later says she should have
  asked first.
- **The chair breaks gently and can be mended:** one leg comes off and she slides onto the soft rug. Nobody falls
  or is hurt.
- **Nobody is frightened.** The bears find her asleep and are *surprised*; she wakes and says "Oh!", surprised too.
  No running, no window, no chase.
- **She owns up and says sorry** for everything, unprompted: "I ate your porridge, and I broke your chair. I
  should've asked first."
- **The bears are kind.** Mama Bear hugs her and thanks her for telling the truth, then gives her the safety rule
  kindly, a rule that never depends on a stranger inviting her in: "But next time, never ever go into a house you
  don't know. Find your grown-up first, okay?" Baby Bear owns his part with a laugh ("And next time, I'll shut the door!").
  Goldilocks helps fix the chair, and they share Papa Bear's porridge, which has cooled to just right.
- **The ending is calm and safe:** the sun and a rainbow come out, the three bears walk her home to her mum, who is
  waiting at the garden gate, and she promises: "Next time, I'll stay with my mum, and I'll never ever go into a house I
  don't know." Baby Bear's "Come back soon, with your mum!" keeps the friendship without inviting her back alone.
- No fire, stove, steam or hot water anywhere: "too hot" is only the porridge's temperature (allowed by
  `GentleTextTests`), and no picture shows a stove, fireplace, candle or steam.

**Why everyone does what they do:**
- **The bears** go for a walk because their porridge is too hot to eat yet. They shelter under a big oak deep in the
  woods because of the rain shower, which is why they are away long enough for everything to happen, and set off
  home when it stops.
- **Nobody answers Goldilocks** because the bears are far away under that oak, out of sight and earshot of the
  cottage.
- **Baby Bear** leaves the door open because he skips out last, excited, and forgets; so the open door is a little
  mistake, not an invitation, and he owns it at the end.
- **Goldilocks** is alone because she wandered too far from her mum's garden picking flowers. She goes in to keep
  dry after calling out, knowing she should wait outside. She eats because the porridge is there and she is hungry
  from picking flowers, which is the naughty bit she says sorry for. She sleeps because she is tired and waiting for
  the rain to stop.
- **Mama Bear** hugs her because Goldilocks told the truth without being asked; the rule comes after the hug.
- **Papa Bear's porridge** is the one left to share because it was too hot for Goldilocks and has cooled while
  everyone was busy: the "too hot" of page 4 pays off as "just right" on page 13.
- **The bears walk her home** because she is small and far from her mum, and **Mum** is waiting at the gate because
  she has been looking for her.

**Refrains (identical every time; since v7 with "Somebody's"):** `"Oh, this porridge is too hot!"`, `"Oh, this porridge is too cold!"`,
`"Mmm, this porridge is just right!"` (page 4, and Papa Bear on page 13); `"This chair is too hard!"`,
`"This chair is too soft!"`, `"This chair is just right!"` (page 5, and Baby Bear on his fixed-up chair on page 13).
The beds use the same pattern in narration (page 6). The bears' three-step build-up: `"Somebody's been eating my
porridge!"` (page 7), `"...sitting in my chair!"` (page 8), `"...sleeping in my bed!"` (page 9), Papa and Mama
identical, Baby Bear's line different each time. "Next time" closes the story: Mama Bear's rule, Baby Bear's door,
and Goldilocks' promise ("I'll never ever go into a house I don't know" answers "never ever go into a house you don't know").

**Page shape note for the reviewer:** the three bowls are one page and the three chairs one page (each picture shows
the moment that ends the trio: eating the little bowl, sitting on the rug by the little chair), not a page per bowl.
A page per bowl, chair and bed would need 9 pages and push the sorry, the mending and the walk home past the store's
15-page limit (`StoreStoryTests.MaxPages`). The text review accepted this.

**Cast and voices (Slice 156, the book's own choices, `StarterVoices.cs`):** Goldilocks (girl, child, a person; bright and
bouncy) suggests `bee`; Papa Bear (man, adult bear; booming and warm) `bigbear`; Mama Bear (woman, adult bear; warm and soft)
`mummy`; Baby Bear (boy, child bear; soft and bouncy) `cub`. All four ids are the right gender in every language, so **Mama Bear is
a bear again** (`StarterAnimals.cs` no longer stores her as no animal): the suggestion keeps her on a woman's voice while she
growls like the other bears. Goldilocks' **Mum** appears only on page 14 and has no line, so the caster has no Mum to voice; the
voice-gender test still lists her as a woman. Real casts dumped for every language on 2026-10-27 (each suggestion is kept
everywhere; no two characters share a voice and flavour; no whisper flavour; every gender right): en am_michael Big / af_sky
Bouncy / af_sarah Normal / bm_daniel Small; fa gyro Big / mana Small / mana Normal / gyro Small; zh yunxi Big / xiaoni Bouncy /
xiaobei Normal / yunxi Small; es and pt alex Big / dora Bouncy / dora Normal / **santa Normal**. That last one is a roster
limit, not a clash: the Spanish and Portuguese `cub` sits on Santa (the deep voice) with no flavour, so Baby Bear sounds deeper
than Papa Bear there; only a Core roster change (moving `cub` in `ThreeVoiceSlots` to `M1` with a free young flavour) fixes it.
`StoreStoryTests.Goldilocks_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster` checks all 14 languages.
Pronouns: Goldilocks and Mama Bear "she", Papa Bear and Baby Bear "he". Translations keep each gender (Arabic قالت
for Goldilocks and Mama Bear, قال for Papa Bear and دبدوب; Russian сказала / сказал; Portuguese "Obrigada" from both
females).

**Translations:** all 13 written for this text in the spoken register (Decision 152). French narrates in the passé
composé ("Boucle d'or est entrée", tags "a dit Boucle d'or"); Farsi spoken forms (خونه، می‌کردن، داغه، بازم بیا) with
the refrain «اندازه‌ست»; Brazilian Portuguese ("pra", "a gente"); Finnish Goldilocks speaks colloquially ("Mä söin
sun puuron"); Japanese hiragana, no spaces, with Goldilocks' mum as ママ to keep her apart from おかあさんぐま; the
rain is gentle (ja パラパラ, zh 淅沥沥). "Find your grown-up" is "your mum or dad" in each language (Japanese
おうちのひと). French and Spanish say soup (soupe, sopa), the word those tellings use; every other language says its
own porridge word. Names: Boucle d'or / Papa Ours / Maman Ours / Petit Ours; Ricitos de Oro / Papá Oso / Mamá Osa /
Osito; Cachinhos Dourados / Papai Urso / Mamãe Ursa / Ursinho; Goudlokje / Papa Beer / Mama Beer / Kleine Beer;
Guldlok / Far Bjørn / Mor Bjørn / Lille Bjørn; Gullhår / Pappa Bjørn / Mamma Bjørn / Lille Bjørn; Guldlock / Pappa
Björn / Mamma Björn / Lillebjörn; Kultakutri / Isä Karhu / Äiti Karhu / Pikku Karhu; Златовласка / Папа-медведь /
Мама-медведица / Медвежонок; 金发姑娘 / 熊爸爸 / 熊妈妈 / 熊宝宝; ゴルディロックス / おとうさんぐま / おかあさんぐま /
こぐま; ذات الشعر الذهبي / بابا دب / ماما دبة / دبدوب; موطلایی / بابا خرسه / مامان خرسه / خرس کوچولو.

**Style:** Children's picture book illustration, soft watercolour and coloured pencil, warm gentle colours, simple
rounded shapes, no text or letters anywhere. Portrait 3:4.

## Characters (canonical until a line-up is accepted; then rewrite from the line-up)

- **Goldilocks:** a girl of about six; a little taller than Baby Bear (her head reaches Mama Bear's shoulder). Long,
  curly golden hair with **one red ribbon bow** on top (her prop, every page). A sky-blue dress with a white collar,
  white socks, red shoes. On page 3 only she holds a small bunch of white daisies. No coat, no hat, no backpack.
- **Mum (Goldilocks' mother, page 14 only, no line):** a grown woman, about Mama Bear's height; the same golden hair
  as Goldilocks, worn in a loose bun; a long **lavender dress** with a **white apron**; no hat, no bag. She looks
  relieved and happy, never cross or tearful.
- **Papa Bear:** the biggest character, about twice Goldilocks' height; **dark chocolate-brown** fur, round ears,
  kind eyes. He wears only a **green knitted waistcoat with two gold buttons** (his prop): no shirt, no trousers,
  no shoes, nothing below the waistcoat. On page 12 he holds a small pot of glue and a brush.
- **Mama Bear:** a little shorter than Papa Bear; **honey-brown** fur (clearly lighter than Papa's), round ears.
  She wears only a **white apron with red polka dots** and has **a small blue flower behind her left ear** (her
  prop): no dress, no trousers, no shoes.
- **Baby Bear:** the smallest bear, a head shorter than Goldilocks; **light caramel-brown** fur, round ears, a
  round tummy. He wears only a **red-and-white striped scarf** (his prop): no shirt, no trousers, no hat, no shoes.
  Bears walk on two legs like people and never show teeth or claws.

There are no other characters: no people besides Goldilocks and her Mum, no other animals (a songbird or butterfly
is not needed; leave them out so counts stay exact).

## Places (the same in every picture)

- **The bears' cottage, outside:** a little cottage of honey-coloured wood with a rounded red roof, a **green front
  door** and **one wide window with a single pane** beside it; a short stone path with flowers either side leads
  into the woods. Pages 1 and 3.
- **Downstairs (one cosy room):** a round wooden table with three bowls (big, middle, little) and wooden spoons;
  three chairs by the wide single-pane window: a big wooden armchair, a middle chair with a fat cushion, a little
  wooden chair; a soft round red rug in the middle; wooden stairs going up at the back. **No fireplace, stove,
  oven, candle, lamp or flame anywhere; no steam over the bowls.** Through the window: trees only. Pages 4, 5, 7, 8,
  12, 13.
- **Upstairs (the bedroom):** a sloping wooden ceiling, three beds in a row: a big tall bed, a middle bed with a
  lumpy quilt, a little bed with a patchwork quilt; **one wide window with a single pane** looking over the path
  and the woods. Pages 6, 9, 10, 11.
- **The big oak:** a huge oak with wide, low, leafy branches **deep in the woods, far from the cottage and out of
  sight of it**. Page 2 only; it is never seen from the cottage.
- **Goldilocks' home:** a small white cottage with a blue door and a flower garden behind a low white picket fence
  with a **garden gate**, at the edge of the woods. Page 14 only; it must look nothing like the bears' wooden
  cottage.
- **Rain (pages 2-5):** a gentle shower drawn as soft light lines and puddles, never a storm: no lightning, no
  dark sky. Page 6 shows the rain stopped and the sky clearing; pages 7-13 are dry; page 14 has sun and a rainbow.

## Picture plan

Every page: the whole scene sits in the top 70% of the picture; the bottom 30% is plain grass, path, floor or rug
with nothing important in it. No text, letters, words, sound effects or writing anywhere ("Pitter-patter",
"creak, crack", "Drip, drip", "Dab, dab, dab", "Ha, ha, ha" are never drawn in). Nobody is frightened, cries or runs away.

**Pages 3, 4 and 5 are 1-character pages** (Goldilocks alone; the coordinator accepted this). The bears are far away
deep in the woods, so they are not shown at all: a child who saw them nearby would ask why she didn't see them or
why nobody answered. Gemini refuses some 1-character scenes; the illustrator uses its refusal tips. Page 6 shows the
bears again, small through the window, walking home after the rain (they are on their way back, so seeing them is
right). Every page is **new** (no pictures exist yet).

| Page | One action (who, count, where, when) | Picture |
| --- | --- | --- |
| Cover | Early afternoon, downstairs at the round table: Goldilocks (1) and the three bears (3) sit together smiling over four little bowls of porridge, Baby Bear on his little chair; facing the reader. Different from page 13 by framing: close, all four faces large | new |
| 1 | Morning, outside the bears' cottage: the three bears (3) walk away down the path into the woods in a line, Papa Bear first, Mama Bear next, Baby Bear skipping last; behind them the green door stands **wide open** | new |
| 2 | Morning, deep in the woods at the big oak, in a gentle rain shower: the three bears (3) snuggle close together under the low leafy branches, Papa Bear's arm round the other two, all looking out at the rain, dry and cosy. No cottage in sight | new |
| 3 | Late morning, outside the bears' cottage in the gentle rain: Goldilocks (1, alone), hair damp, holding her small bunch of daisies, stands at the wide-open green door and leans in, one hand by her mouth, calling. No bears, no oak; only the cottage and the woods behind | new |
| 4 | Late morning, downstairs: Goldilocks (1, alone) sits at the round table happily eating from the little bowl with a wooden spoon, the big and middle bowls pushed aside; the window shows only rain and trees. No steam, no stove | new |
| 5 | Late morning, downstairs: Goldilocks (1, alone) sits on the soft red rug beside the little chair, whose one leg lies on the rug next to it; she has an "oops!" face, a hand over her mouth, not hurt; the big and middle chairs stand behind; the window shows only rain and trees | new |
| 6 | Midday, upstairs: Goldilocks (1) fast asleep under the patchwork quilt of the little bed, the big and middle beds beside it; through the wide single-pane window the rain has stopped and, small in the distance, the three bears (3) walk along the path towards the cottage | new |
| 7 | Midday, downstairs: the three bears (3) stand round the table; Papa Bear and Mama Bear peer into their bowls, and Baby Bear holds up his empty little bowl with a surprised face. Goldilocks is not shown | new |
| 8 | Midday, downstairs by the window: the three bears (3) look at the three chairs; Baby Bear holds up the loose chair leg beside his little chair, surprised; Papa Bear and Mama Bear look at their own chairs | new |
| 9 | Midday, upstairs: the three bears (3) stand on tiptoe at the foot of the little bed, peering in, surprised and curious; Goldilocks (1) still fast asleep in it | new |
| 10 | Midday, upstairs: Goldilocks (1) sits up in the little bed, hands pressed together, saying sorry with a sorry face; the three bears (3) stand round the bed, surprised but gentle, listening | new |
| 11 | Midday, upstairs beside the little bed: Mama Bear (1) gives Goldilocks (1) a big soft hug; Papa Bear (1) and Baby Bear (1) stand beside them laughing | new |
| 12 | Early afternoon, downstairs on the rug: Goldilocks (1) holds the little chair steady, Baby Bear (1) holds its leg in place, and Papa Bear (1) dabs glue on the joint from a small pot with a brush; Mama Bear (1) watches from the table, smiling | new |
| 13 | Early afternoon, downstairs at the round table: all four (4) sit eating porridge from four little bowls; Papa Bear in the big armchair, Mama Bear on the cushioned chair, Baby Bear on his mended little chair, Goldilocks on a little wooden stool | new |
| 14 | Afternoon, sunshine and a rainbow, at Goldilocks' home (white cottage, blue door, picket fence): Mum (1) kneels at the open garden gate hugging Goldilocks (1); the three bears (3) stand together on the path outside the gate, smiling and waving. Differs from page 1: a different house, the bears facing the reader, Mum and the rainbow | new |

Check (dino-storytelling section 4):

- [x] Every page's picture differs from its neighbours (place, action or who is there).
- [x] No page needs two pictures.
- [x] Characters' faces and the action fit in the top 70% of a 3:4 portrait picture.
- [x] Each character's look is fixed in the line-up, not invented per page.

The last item is ticked for the plan: every look is fixed in the character lines above, and the illustrator draws
one line-up from them (Mum included) before any page.

Notes for the illustrator:

- The three bears are look-alikes: tell them apart by fur shade (dark chocolate, honey, light caramel), size and
  prop (green waistcoat, white red-dotted apron plus blue flower, red-and-white striped scarf). Ask for the props by
  name in every prompt, and what each does NOT wear ("no trousers, no shirt, no shoes on any bear").
- Bears never show teeth or claws: "mouths closed or smiling, no teeth showing".
- Pages 3-5: exactly one character, Goldilocks; no bears anywhere, not even far away or through the window.
- Page 6: the bears outside are small but clearly three bears in their props; count them in the prompt ("exactly
  four characters: Goldilocks asleep inside, and three small bears outside seen through the window"). Ask for "one
  wide window with a single pane" so the bears are not multiplied by window panes.
- Pages 4, 7, 12, 13: no stove, fireplace, candle, lamp or steam. The porridge is plain cream-coloured in round
  wooden bowls.
- Page 5: the leg lies on the rug beside the chair; Goldilocks sits safely on the rug, surprised, not crying or
  rubbing anything.
- Pages 9-11: Goldilocks is in the little bed with the patchwork quilt, never the big or middle bed. On page 10
  nobody looks scared: the bears' faces are surprised and gentle.
- Page 13: there are only three chairs, so Goldilocks sits on a little wooden stool.
- Page 14: exactly five characters; Mum's hug is happy, not tearful. Her cottage must not look like the bears'.
- Ask for "no text, letters, words, sound effects or writing anywhere".

## Check results

| Page | Attempts | Size | Why earlier attempts failed |
| --- | --- | --- | --- |
| Line-up | 2 (1st: Gemini error, Redo drew it) | 896x1200 (not shipped) | accepted; Mama Bear has a blue flower behind each ear (two), not one |

## Illustrator run 2026-10-26 (stopped: Gemini tab hung)

- Usage meter at start (Settings > Usage limits): 5-hour window 9% used (resets 3:05 PM), weekly 7% used (resets Oct 27 10:05 AM). Shared with the other illustrators; no end value taken because the tab froze.
- Model 3.5 Flash-Lite, Images tool, 3:4 confirmed by zoom.
- Line-up drawn directly in one prompt (no separate sheets): `goldilocks/characters/lineup.jpg`. Later prompts say "a small blue flower behind each ear" for Mama Bear to follow the picture.
- Cover and pages 1-14 not drawn. The tab froze (renderer unresponsive, CDP timeouts) right after the line-up was pasted and the cover prompt typed; nothing was sent. Resume at the cover in a fresh tab.

## Illustrator run 2026-10-26 (second, stopped: Google bot check)

- Model 3.5 Flash-Lite, Images tool, 3:4 confirmed by zoom, line-up pasted (chat "Porridge with the Bears", my own).
- Cover attempt 1: rejected, five people (Mum from the line-up drawn in). Attempt 2 ("Mum must NOT be in this picture"): looks right (Goldilocks + 3 bears, props correct) but faces small and Papa Bear turned away; not checked at full size because it could not be saved.
- Saving failed: "Download full size image" gave the toast "Couldn't download image." three times (JS click and real click by ref; no file reached Downloads). Reloading the chat then redirected to `google.com/sorry` (Google's automated-traffic check; other illustrators' tabs hit it too). A CAPTCHA is not to be solved by the agent.
- Nothing accepted in this run (superseded by the third run below, which finished the book).

## Illustrator run 2026-10-26 (third, complete)

- Model 3.5 Flash-Lite, Images tool, 3:4 confirmed by zoom in every chat. Cover and pages 1-14 accepted; every picture saved through the download route and prepared into `Store/goldilocks/` (cover, cover-thumb, pictures 1-14) at 896x1200. Usage meter not read at the end (shared with the other illustrators).
- Chats that repeated an earlier scene (page 6's four-bear window, page 12's attic) were redone in a fresh chat.

| Page | Attempts | Size | Why earlier attempts failed |
| --- | --- | --- | --- |
| Cover | 1 | 896x1200 shipped | accepted; toy teddies on a shelf are decor; Mum absent |
| 1, 2, 4, 7, 9, 10, 11, 13, 14 | 1 each | 896x1200 shipped | accepted (page 13 was later redrawn to exactly four bowls) |
| 3 | 3 | 896x1200 shipped | 1st chat: refused twice, then drawn with the word "hello!" and a white border; same picture without the word still framed with a different cottage; 3rd (fresh chat, cottage described like page 1): accepted |
| 5 | 1 | 896x1200 shipped | accepted; the little chair is an upturned stool with a snapped leg beside it |
| 6 | 3 | 896x1200 shipped | two tries drew four bears in the window; fresh chat with Baby Bear holding Mama's paw: three bears. Window is two open casements |
| 8 | 2 | 896x1200 shipped | first chat answered with text only twice; fresh chat drew it |
| 12 | 3 | 896x1200 shipped | attic ceiling and bedpost, Mama's flowers missing; then flowers fixed but still the attic; fresh chat: downstairs room |

Two saves picked up another illustrator's finished picture from `Downloads` (a pirate book) because its download landed in the same two-minute window; both were put back into `Downloads` as `Gemini_Generated_Image_returned*_from_goldilocks.jpg`. After that each save waited 15 s for its own file. Every saved picture was looked at before acceptance.

## Redraw after the full review (2026-10-26, same day)

The full review failed cover, 5, 7, 10 and 13 (`reviews/goldilocks-full.md`). All five were redrawn, each in a fresh chat with the line-up pasted (an edit in the old chat only repeated the old picture for 10 and 13): cover (page 13's honey-plank room, four wooden bowls, no shelf, armchair or toys), 5 (small spindle-back chair upturned, its leg on the rug), 7 (all three bears standing, wooden bowls, Baby Bear holds up his empty one), 10 (Mama Bear's whole apron white with red dots; Gemini refused it twice, a retry drew it; the scene is a hard strip, with a thin white line above the plain floor at 71%), 13 (exactly four wooden bowls). Pictures were saved at 1792x2400 (page 5 at 896x1200) and shipped at 896x1200. Saves now use `-Bytes`.

Shipped sizes: every picture in `Store/goldilocks/` is 896x1200 (cover-thumb 288x384).

Page 10, fourth redraw (2026-10-26): accepted. Fresh chat, line-up pasted only, short three-bed prompt inserted and sent with JS: exactly three beds, one girl sitting up in the little patchwork bed with a sorry face, Mama Bear's white apron with red dots, surprised bears, the floor runs to the bottom edge with no line. `pictures/10.jpg` is now this picture.
