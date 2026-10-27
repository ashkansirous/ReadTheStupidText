# The Little Red Hen: art record

Store story (`StoreTexts.RedHen`, stem `little-red-hen`, version 6). Retold as 15 pages with one action each
(`dino-storytelling` skill, Slice 121). **Art status (2026-10-26): cover and pages 1-15 drawn and accepted; full review PASS 2026-10-26.** The cover
and all 15 pages are new; the text-only version 3 had none. Version 6 (Slice 146 Wave A, 2026-10-27): Farsi
rewritten in spoken Farsi, sound words as plain triples without commas or vowel marks; English, page breaks
and every picture unchanged.

**Gentle retelling (Decision 130):** in the classic the Hen's three neighbours are "lazy", she says "No, you will
not!" at the end and eats the bread alone with her chicks while the others watch, hungry. Nobody is shut out here:
- The three friends are not called lazy, sleepy or noisy. They each simply love one thing (Dog his nap in the sun,
  Cat sitting on the fence licking her paws, Duck splashing in his puddle), and that is why they keep saying
  "Not me!" (the classic "Not I!" in its everyday form, Decision 152).
- When the bread is ready and they all say "I will!", **Hen does not say no**. Dog looks at the bread and then at
  Hen and works it out by himself; Dog, Cat and Duck each say it in their own words ("You did all the work by
  yourself", "we didn't help you at all", "We're sorry, Hen ... We promise!"). Nobody scolds them.
- Hen thanks them for saying sorry and shares: "Bread is yummier when we share it." All four have a picnic.
- They keep the promise at once (the washing-up, everyone wanting to help, Duck splashing in the bubbles) and
  again the next spring, when "I will!" answers the very first question and they plant the wheat together.
- Rule 1 details: the wheat is cut with small garden shears ("Snip, snip, snip!"), never a sickle or knife. There
  is **no oven, stove, fire or flame anywhere**: Hen mixes and squishes the dough on page 9 and the loaf is "ready"
  on page 10, cooling on the table ("oven" and "fire" are on `GentleTextTests`' banned list). The washing-up is in
  a tub of bubbly water outdoors, never hot or steaming. The loaf is broken by hand, no knife.

**Why everyone does what they do:**
- **Hen** asks for help every time because jobs are nicer with friends. When nobody comes, she does not give up
  or get cross: she wants bread, so she does it herself ("Then I'll do it myself"). She shares at the end because
  the friends are sorry and because bread is better shared; she is never trying to teach them a lesson.
- **Dog** says "Not me!" because he would rather snooze in the sun. He is the first to notice that Hen did all the
  work, because he looks from the warm bread to Hen; that is why he speaks first on page 11.
- **Cat** says "Not me!" because she is busy sitting on the fence licking her paws. She adds "we didn't help you at
  all", owning it for the three of them.
- **Duck** says "Not me!" because he is busy splashing. He says sorry and makes the promise; he loves splashing,
  so the washing-up turns out to be his favourite job ("Washing-up is fun when we do it together!").
- **They wake up and run to the table** on page 10 because they smell the warm bread, and **they have their spades ready** on page
  14 because they promised to help next time.
- **The lesson**, said last by Hen and agreed by everyone: "Helping is more fun together."

**Refrains (identical every time):**
- `"Not me!" said Dog. "Not me!" said Cat. "Not me!" said Duck.` on pages 2, 4, 6 and 8.
- `"Then I'll do it myself," said Hen.` on pages 2, 4, 6 and 8, answered by `So she did it all by herself.` at the
  start of pages 3, 5, 7 and 9.
- `"Who will help me ___?" said Hen.` on pages 2, 4, 6, 8, 10 and 14 (plant, cut, take to the mill, bake, eat,
  plant again). The three-step build-up ends differently: `"I will!" said Dog. "I will!" said Cat. "I will!" said
  Duck.` on page 10 (for eating) and page 14 (for helping).
- `Dig, dig, pat! Dig, dig, pat!` on page 3 (Hen alone) and page 15 (all four together).

**Cast and voices:** Hen, Dog, Cat, Duck (`StoreTexts` spec list). Genders are chosen, two and two: **Hen and Cat
are "she", Dog and Duck are "he"** (the pronouns in the text set them: her friends / his nose / her paws / his
puddle / he looked). The app casts an animal character with its own animal's roster voice before its gender: the
hen voice is a woman and the puppy voices are boys in every language, so Hen and Dog keep their animal (Dog says
"Woof" now and then). The kitten and duckling voices are girls in most languages but boys in Farsi, Spanish and
Portuguese, so the builder stores **Cat and Duck as no animal** (`StarterAnimals.cs`, `NotAnimal`): they are cast
by gender in every language, at the cost of no meow or quack. `StoreStoryTests.TheLittleRedHen_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster`
checks all 14 languages. Translations keep each gender: Russian Пёс and Утёнок with сказал, Кошка and Курочка with
сказала; Arabic البطّ with قال; Spanish and Portuguese la Gata / a Gata; French calls the Cat "la chatte Minette",
then "Minette" (avoiding "la Chatte"). Japanese "Not me!" and "I will!" are one piece each for boys and girls, so
they are neutral (やらないよ！, やる、やる！); Cat says わたしたち, Duck ぼくたち.

**Style:** Children's picture book illustration, soft watercolour and coloured pencil, warm gentle
colours, simple rounded shapes, no text or letters anywhere. Portrait 3:4.

## Characters (canonical until a line-up is accepted; then rewrite from the line-up)

- **Hen:** a small, plump **russet-red hen**, the smallest of the four; bright red comb and wattle, short yellow
  beak, yellow feet, a few darker red tail feathers. She wears a **sky-blue apron with white polka dots** and one
  front pocket (her outfit, on every page). Her prop changes with the job and is named on each page (grains, garden
  shears, cart, flour sack, mixing bowl, loaf, spade). Always smiling or busy; never cross, never pecking.
- **Dog:** a friendly, shaggy, **sandy-brown dog**, the biggest of the four (about twice Hen's height); floppy ears,
  **one dark-brown patch around his left eye**, a black nose, a waggy tail. He wears a **red collar with a round
  gold tag** and nothing else.
- **Cat:** a slim **grey tabby cat** with darker grey stripes, **four white paws** and a white chest, green eyes,
  a long striped tail; a little taller than Hen. She wears a **pink ribbon tied in a bow around her neck** and
  nothing else.
- **Duck:** a **white farm duck** (a plump white Pekin) with an orange beak and orange webbed feet, a little
  taller than Hen and shorter than Cat. He wears a **small green bow tie** and nothing else.

There are no other characters: no chicks, no farmer, no miller, no people, no other animals. Exactly four animals
on every page that has all four, each appearing once.

## Places (the same in every picture)

- **The farmyard:** a packed-earth yard in front of a small red farmhouse with a green door, a low wooden fence
  along one side, one round puddle near the fence, and **one big apple tree** at the corner where the yard meets
  the field. Pages 1, 2, 6, 13 and 14.
- **The field:** a flat field right beside the farmyard, the wooden fence and the apple tree at its edge; bare
  brown earth in spring (pages 3 and 15), tall golden wheat in late summer (pages 4 and 5). The picnic on page 12
  is on the grass under the apple tree.
- **The hill and the windmill:** a gentle green hill behind the farm with a white windmill with four big sails on
  top. No miller, no people. Page 7.
- **The kitchen:** a cosy farmhouse kitchen with a big wooden table, a round braided rug on the floor, a wooden
  dresser with plates and **one wide window with a single pane**. **No oven, stove, fireplace, fire, candle or
  flame anywhere.** Pages 8-11.

## Picture plan

Every page: the whole scene sits in the top 70% of the picture; the bottom 30% is plain earth, grass, floor or rug
with nothing important in it. No text, letters, words, sound effects or writing anywhere ("Dig, dig, pat", "Snip",
"Whoosh", "Sniff", "Munch" and "Splish, splash, splosh" are never drawn in). Nobody pecks, pushes, points at or
turns away from anyone. Every page has **three or more characters** (Gemini refuses some 1-2 character scenes); on
pages where the three friends do not help, they are in the picture doing their favourite thing. Every picture is
**new** (no pictures exist).

| Page | One action (who, count, where, when) | Picture |
| --- | --- | --- |
| Cover | Sunny spring morning in the farmyard: Hen (1) stands in front holding up a few grains of wheat with a happy smile; behind her Dog (1), Cat (1) and Duck (1) stand side by side smiling, the apple tree and the red farmhouse behind them. Exactly four animals | new |
| 1 | Spring morning, the farmyard: Hen (1) sweeps the yard with a small broom, busy; Dog (1) snoozes in the sun with his nose on his paws; Cat (1) sits on the fence licking a white paw; Duck (1) splashes in the round puddle | new |
| 2 | Spring morning, the farmyard: Hen (1) holds out a few grains of wheat in her wing, asking; Dog (1) lying down, Cat (1) on the fence and Duck (1) in his puddle each shake their heads, relaxed and smiling. Nobody looks cross | new |
| 3 | Spring, late morning, the bare brown field: Hen (1) plants grains in a long straight row, patting the earth with her foot, a little bag of grains at her side; in the background by the fence Dog (1), Cat (1) and Duck (1) are fast asleep in a heap | new |
| 4 | Late-summer afternoon, at the edge of the field of tall golden wheat (taller than Hen): Hen (1) stands holding small garden shears, asking; Dog (1), Cat (1) and Duck (1) lounge in the grass beside the field, shaking their heads | new |
| 5 | Late-summer afternoon, the wheat field: Hen (1) snips the wheat with small round-tipped garden shears; a few neat tied golden bundles stand beside her; under the apple tree at the field's edge Dog (1), Cat (1) and Duck (1) sit in the shade watching | new |
| 6 | Late-summer morning, the farmyard: Hen (1) stands beside her little red wooden cart piled with golden wheat bundles, asking; Dog (1), Cat (1) and Duck (1) lie in a row against the fence, shaking their heads | new |
| 7 | Late-summer midday, the hilltop: Hen (1) stands beside her little red cart, now holding one full white sack of flour, in front of the white windmill whose four big sails are turning. Far off to one side at mid-height (never in the bottom 30%), small, the red farmhouse with Dog (1), Cat (1) and Duck (1) dozing in the farmyard. No miller, no people | new |
| 8 | Afternoon, the kitchen: Hen (1) stands by the wooden table with the white flour sack and a big mixing bowl, asking; Dog (1) lies on the rug, Cat (1) sits on the window sill, Duck (1) sits on a kitchen chair, each shaking their heads. No oven or stove anywhere | new |
| 9 | Afternoon, the kitchen: Hen (1) at the wooden table squishes a big round ball of dough with both wings, flour dusting the table, the mixing bowl beside her; on the rug Dog (1), Cat (1) and Duck (1) are asleep together in a cosy heap. No oven or stove | new |
| 10 | Late afternoon, the kitchen: a big round golden loaf sits on a wooden board on the table; Hen (1) stands beside it, smiling; Dog (1), Cat (1) and Duck (1) have just jumped up from the rug and hurry to the table, noses up, eager. No oven, stove or steam | new |
| 11 | Late afternoon, the kitchen: Dog (1), Cat (1) and Duck (1) stand in a row by the table, heads a little bowed, looking sorry (Dog's ears down, Duck with a wing on his chest promising); Hen (1) faces them by the loaf, listening kindly. Nobody cries | new |
| 12 | Late afternoon, on the grass under the apple tree: a picnic: Hen (1), Dog (1), Cat (1) and Duck (1) sit in a circle on a red-and-white checked blanket with cups and plates on it, each holding a piece of bread, munching and smiling | new |
| 13 | Early evening, the farmyard by the green kitchen door: a big wooden tub of white bubbles on a low bench, a pile of cups and plates beside it; Dog (1) washes a plate, Cat (1) dries a cup with a tea towel, Duck (1) splashes in the bubbles; Hen (1) laughs, wings up. No steam | new |
| 14 | The next spring, morning, the farmyard at the edge of the bare field: Hen (1) holds out a few grains of wheat; Dog (1), Cat (1) and Duck (1) jump up eagerly, each holding a little spade. Exactly four animals and three spades | new |
| 15 | Spring sunset, pink and gold sky, the field: Hen (1), Dog (1), Cat (1) and Duck (1) work side by side in a row, digging and planting, each with their own little spade (four spades, Hen's too), all smiling; the apple tree and the farmhouse behind them | new |

Check (dino-storytelling section 4):

- [x] Every page's picture differs from its neighbours (place, action or who is there).
- [x] No page needs two pictures.
- [x] Characters' faces and the action fit in the top 70% of a 3:4 portrait picture.
- [x] Each character's look is fixed in the line-up, not invented per page.

The last item is ticked for the plan: every look is fixed in the character lines above, and the illustrator draws
one line-up from them (all four side by side, Dog biggest, Hen smallest) before any page.

Notes for the illustrator:

- Four animals and their one detail each, in every prompt: Hen's sky-blue polka-dot apron, Dog's red collar with
  gold tag and left-eye patch, Cat's pink bow and white paws, Duck's green bow tie. Nobody wears anything else
  ("no hats, no shoes, no trousers, no scarves").
- Pages 2, 4, 6 and 8 are all "asking" pages: keep them apart by place and prop (farmyard with grains, wheat field
  with shears, farmyard with the loaded cart, kitchen with the flour sack). The friends shake their heads relaxed
  and smiling, never with crossed arms or turned backs.
- Page 7 is the one page where Hen works far from the others: the friends are small in the distance, off to one side at mid-height, never in the bottom 30%.
  If Gemini drops them, a picture with Hen alone at the windmill is acceptable (her only solo scene).
- Pages 8-11: no oven, stove, fireplace, kettle, steam or flame. The loaf on page 10 sits on a board on the table.
- Page 11 is sorry, not sad: heads a little bowed, small smiles, no tears; Hen's face is kind, never stern.
- Page 13: the bubbles are white foam in a wooden tub outdoors; no steam, no sink, no tap.
- Ask for "no text, letters, words, sound effects or writing anywhere".

## Check results

Line-up, cover and pages 1-15 accepted 2026-10-26 (3.5 Flash-Lite, 1792x2400 each, one picture per chat after page 2 repeated
page 1's scene). Prepared into `Store/little-red-hen` (cover, cover-thumb, pictures 1-15). Gemini once showed an "Agree"
dialog on a fresh page (not accepted); it did not return. Lesson: Gemini often draws a second Dog or Cat when the friends
"look at" Hen; list the four animals as numbered items in four different places and say "exactly one hen, one dog, one cat,
one duck". Pages 12-14 fell in the 70-77% band slightly (blanket, Hen's feet); accepted.

| Page | Attempts | Size | Why earlier attempts failed |
| --- | --- | --- | --- |
| 1 | 1 | 1792x2400 | |
| 2 | 2 | 1792x2400 | 1: same chat as page 1, repeated page 1's scene (friends asleep/splashing instead of awake) |
| 3 | 3 | 1792x2400 | 1: Duck stood awake; 2: fix in same chat repeated it; 3 fresh chat passed |
| 4 | 1 | 1792x2400 | |
| 5 | 2 | 1792x2400 | 1: bottom 24% left as blank white paper (full review) |
| 6 | 6 | 1792x2400 | 1: two dogs; 2: white band at 68% and a barn; 3: white top 22%; 4: two dogs; 5 passed (friends listed on the right, Hen alone left) |
| 7 | 1 | 1792x2400 | |
| 8 | 2 | 1792x2400 | 1: two dogs, two cats, "FLOUR" on the sack, Hen on the table |
| 9 | 1 | 1792x2400 | |
| 10 | 2 | 1792x2400 | 1: Hen shown from behind with no apron, standing on the table |
| 11 | 1 | 1792x2400 | |
| 12 | 2 | 1792x2400 | 1: house was a two-storey clapboard with a porch and yellow door (full review) |
| 13 | 1 | 1792x2400 | |
| 14 | 3 | 1792x2400 | 1: four spades, Hen cut off by a hard edge; 2: two-storey brick house, friends posed not eager (full review); 3 passed, spring blossom only |
| 15 | 1 | 1792x2400 | |
