# The Ugly Duckling: art record

Store story (`StoreTexts.UglyDuckling`, stem `ugly-duckling`, version 4). Retold as 14 pages with one
action each (`dino-storytelling` skill). **Art status (2026-10-26): cover and pages 1-14 drawn and accepted; full review PASS on 2026-10-26.** The
cover and all 14 pages are new; nothing from the text-only version is kept.

**Gentle retelling (Decision 130):** in Andersen the duckling is pecked, bitten and chased by the ducks
and hens, kicked by the girl who feeds them, and runs away; hunters shoot the wild geese around him; an old
woman's hen and cat scorn him; he nearly freezes into the ice and wants the swans to end his life. None of
that is here. The teasing is one mild line from his sister Dilly ("You do not look like a duck at all"),
answered at once by Mother Duck ("Dilly, that was not kind ... Everyone is different"), and Dilly says sorry.
He never runs away: he goes to explore the farm with his mother's blessing and a supper time, and comes home.
Andersen's hen ("Can you lay eggs?") stays, but she asks with a smile, calls him a very polite little bird
and sends him home for supper. Nobody is cold or alone in winter: the pond freezes and Hen invites the whole
duck family into her warm, dry barn, where they sleep together in the straw. The word "ugly" never appears in
the text (it is on `GentleTextTests`' banned list); only the classic title keeps it. The ending is warm and
safe: the swans welcome him, his duck family is welcome too, and he carries his sisters and brothers home on
his back at sunset.

**Why everyone does what they do:**
- **Duckling** wants to know what kind of bird he is because he looks different from his family, so he asks
  everyone he meets ("Am I a hen like you?", "Am I a swan like you?"). He is polite, so Hen likes him. He goes
  home for supper because his mother asked him to. When the swans welcome him he asks for his family to come
  too, because they are still his family.
- **Mother Duck** loves all her babies the same, big or small, so she tucks the big grey one under her wing
  at once, stops the teasing kindly, and lets him go and find out who he is, as long as he is home for supper.
- **Dilly** giggles because her big brother looks funny to her, not to be mean. When her mother explains,
  she is sorry, and she tells him what he is best at (swimming). At the end she is proud of him: "You are a
  swan, and you are still our brother."
- **Hen** laughs at the idea of a long-necked hen, but she is kind: she likes the polite little bird and
  sends him home for supper. In winter she remembers him and invites the whole duck family into her barn.
- **The swans** fly high over the pond in autumn because they are flying far away for the winter, so they
  cannot hear him. In spring they come back, see one of their own and welcome him, and they are happy to
  share the pond, which is "big enough for everyone".
- **He turns white** because young swans are born grey and grow white feathers as they grow up (true of real
  swans), so nothing magic happens: he simply grows up over the winter.

**Refrains (identical every time):** `"Big or small, I love you all," said Mother Duck.` on pages 2, 5 and 14
(`whispered Mother Duck` on page 10; the refrain words are the same piece). `Paddle, paddle, splish, splash!`
on pages 3 and 13. The question `"Am I a ___ like you?" said Duckling.` makes the three-step build-up: a hen
(page 7, no), the autumn swans (page 8, too high to hear), the spring swans (page 12, yes).

**Cast and voices:** Mother Duck, Duckling, Dilly, Hen, Swan (`StoreTexts` spec list). The app casts an animal
character with its own animal's roster character before its gender, and each language's duckling voices are one
gender (English and Chinese: girls, Spanish and Portuguese: one boy), so the builder stores **Duckling and Dilly as
no animal** (`StarterAnimals.cs`, `NotAnimal`): they are cast by gender in every language, at the cost of no quack.
Mother Duck plays the kind-mummy character and Hen the hen; Swan has no animal word, so a plain male character.
`StoreStoryTests.TheUglyDuckling_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster` checks all 14
languages. Pronouns cast the voices: Mother Duck, Dilly and Hen are "she", Duckling and Swan are "he". Translations
keep each character's gender (Swan masculine: Arabic البجع with قال, Russian Лебедь with сказал; Hen feminine
everywhere; Dilly feminine: آسفة, сказала, la plus petite).

**Style:** Children's picture book illustration, soft watercolour and coloured pencil, warm gentle
colours, simple rounded shapes, no text or letters anywhere. Portrait 3:4.

## Characters (canonical until a line-up is accepted; then rewrite from the line-up)

- **Mother Duck:** a white farm duck, the biggest duck in the book (until the Duckling grows up on page 11);
  orange beak and orange feet, kind dark eyes. She wears a small **sky-blue bonnet** tied with a ribbon under
  her chin (her prop), on every page. No other clothes.
- **Duckling, young (pages 2-10):** a fluffy **grey** cygnet, about twice the size of the yellow ducklings and
  about two thirds of Mother Duck's height; a longer neck than the others, big flat dark-grey feet, a
  dark-grey beak. The detail that tells him apart all book: **one little tuft of grey down sticking up on top
  of his head**. No clothes, no props.
- **Duckling, grown (pages 11-14):** a **young white swan**, now taller than Mother Duck but a little smaller
  than the three adult swans; snowy white feathers, a long curved neck, an orange beak with a black base.
  He still has the **one small grey tuft on top of his head** (the only swan with it), so readers know it is
  him. No clothes, no props. Every prompt from page 11 on asks for the tuft by name ("one small tuft of grey down on top of his head; the adult swans have none").
- **Dilly:** the **smallest** of the three yellow ducklings; fluffy bright yellow, orange beak and feet,
  with a **tiny pink daisy on top of her head** (her prop, every page).
- **The other two yellow ducklings (not speakers):** the same fluffy bright yellow as Dilly, a little bigger
  than her, plain: no flower, no clothes. There are always exactly three yellow ducklings.
- **Hen:** a plump, round **red-brown hen**, about Mother Duck's height; red comb and wattle, yellow beak and
  feet. She wears a small **green-and-white checked apron** (her prop), on every page. Smiling; never pecking
  at anyone.
- **Swan (the one who speaks) and the two other swans:** three large adult white swans, the biggest birds in
  the book; long curved necks, orange beaks with a black knob at the base. Swan is the biggest of the three
  and swims in front; the other two are a little smaller. No clothes, no props, no grey tuft.

There are no other characters: no people, no farmer, no cat, no dog, no fox, no hunters.

## Places (the same in every picture)

- **The pond:** a round farm pond with reeds and a few lily pads, a soft grassy bank and one big weeping
  willow. Mother Duck's nest is a round nest of dry reeds on the bank under the willow. Pages 1-5, 8, 11-14.
- **The meadow:** a green meadow with buttercups between the pond and the farm, a little path across it.
  Page 6.
- **The farmyard:** a packed-earth yard with scattered corn, a low wooden fence and a red barn with a big
  wooden double door. Pages 7 and 9.
- **Inside the barn:** soft heaps of golden straw and a few straw bales; pale moonlight through **one wide
  window with a single pane**. No lantern, candle, lamp or fire anywhere. Page 10.

## Picture plan

Every page: the whole scene sits in the top 70% of the picture; the bottom 30% is plain grass, water, snow,
earth or straw with nothing important in it. No text, letters, words, sound effects or writing anywhere
("Crack", "Paddle, paddle, splish, splash", "Cluck" and "Swoosh" are never drawn in). Nobody bites, pecks,
pushes or chases anyone. Every page is **new**; the pictures below are drawn, accepted and passed the full review on 2026-10-26.

| Page | One action (who, count, where, when) | Picture |
| --- | --- | --- |
| Cover | On the pond in the morning sun, Mother Duck (1) swims ahead with Dilly and the two yellow ducklings (3) in a line behind her, and the big grey Duckling (1) paddles at the end of the line, looking up curiously; the willow behind | new |
| 1 | Summer morning, the nest under the willow by the pond: Mother Duck (1) sits on the nest, smiling down as three fluffy yellow ducklings (3, Dilly with her daisy among them) pop out of cracked eggshells; one big whole egg (twice the size of the others) still lies in the nest. No grey duckling yet | new |
| 2 | Summer morning, the same nest: the big egg has split in two, and the big grey Duckling (1) stands among the shell halves, blinking; Mother Duck (1) tucks him under her wing with a loving smile; the three yellow ducklings (3) peek round her, surprised | new |
| 3 | The next morning, on the pond: Mother Duck (1) leads a line through the water: the three yellow ducklings (3) behind her, little splashes at their feet, and the grey Duckling (1) gliding fastest, about to pass them | new |
| 4 | Late morning, on the grassy bank: the three yellow ducklings (3) stand together; Dilly, in front, giggles with a wing over her beak; the grey Duckling (1) stands a little apart, looking down at his big flat feet, a little sad. No Mother Duck | new |
| 5 | Late morning, the same bank: Mother Duck (1) has one wing around the grey Duckling (1); Dilly (1) stands in front of them, head bowed, saying sorry; the two other yellow ducklings (2) watch from beside her | new |
| 6 | Midday, the edge of the pond where the meadow path begins: Mother Duck (1) stands on the bank close beside him, smiling and waving a wing; the grey Duckling (1) has just stepped onto the path into the buttercups and looks back at her, waving; the red barn is small in the distance. No yellow ducklings | new |
| 7 | Afternoon, the farmyard: Hen (1), in her checked apron, stands among scattered corn, head tilted, smiling and asking her question; the grey Duckling (1) stands in front of her, looking up politely. Nobody else | new |
| 8 | Autumn afternoon, the pond with red and gold leaves on the willow and the water: Mother Duck (1), the three yellow ducklings (3) and the grey Duckling (1) float together, all looking up; high in the sky three big white swans (3) with long necks fly past in a line, wings spread; the grey Duckling stretches his long neck up towards them (the text: "birds with long necks like his") | new |
| 9 | Winter afternoon, the farmyard in snow, gentle snowflakes falling: Hen (1) stands in the open barn door, fluffed up, waving them in with a wing; Mother Duck (1), the grey Duckling (1) and the three yellow ducklings (3) waddle towards her in a line through the snow. The pond is not shown | new |
| 10 | Winter night inside the barn, moonlight through the one-pane window: Mother Duck (1), the grey Duckling (1), Dilly and the two yellow ducklings (3) and Hen (1) snuggle together asleep in a heap of golden straw; the grey Duckling sleeps between Mother Duck and Dilly; Mother Duck's eyes are half open, whispering. No lantern, candle or fire | new |
| 11 | Spring morning, the pond edge, the willow green again and a few flowers on the bank: the grown Duckling, now a young white swan with the one grey tuft on top of his head (1), bends his long neck over the still water and sees his own white reflection, surprised. Nobody else. His reflection is directly under him, not a second bird, and both stay above the 70% line | new |
| 12 | Spring morning, on the pond: three adult white swans (3) glide down onto the water with wings still half open, Swan (the biggest) in front, smiling and bowing his neck to the young white swan (1, the Duckling, with the one grey tuft on top of his head), who faces them, hopeful. The duck family is not shown | new |
| 13 | Spring midday, on the pond: the young white swan (1, the Duckling, with the one grey tuft on top of his head) and the three adult swans (3, no tufts) swim together in a loose circle while Mother Duck (1) and the three yellow ducklings (3) paddle out to join them, little splashes at their feet, everyone smiling | new |
| 14 | Sunset, pink and gold sky, on the pond near the nest under the willow: the young white swan (1, the Duckling, with the one grey tuft on top of his head) glides home with Dilly and the two yellow ducklings (3) riding on his back, nestled between his wings; Mother Duck (1) swims beside him, smiling up at them. The adult swans are not shown | new |

Check (dino-storytelling section 4):

- [x] Every page's picture differs from its neighbours (place, action or who is there).
- [x] No page needs two pictures.
- [x] Characters' faces and the action fit in the top 70% of a 3:4 portrait picture.
- [x] Each character's look is fixed in the line-up, not invented per page.

The last item is ticked for the plan: every look is fixed in the character lines above, and the illustrator
draws one line-up from them (both Duckling looks side by side) before any page.

Notes for the illustrator:

- The Duckling has two looks. Pages 2-10 are the grey cygnet; pages 11-14 the young white swan. Both keep the
  **one grey tuft on top of the head**, the only detail that tells him apart from the three adult swans. Draw
  both looks in the line-up.
- The yellow ducklings stay small and fluffy for the whole book, even in spring (picture-book time). There are
  always exactly three, and only Dilly has the pink daisy. Ask for the count in every prompt ("exactly three
  yellow ducklings, only the smallest has a pink daisy").
- Mother Duck's sky-blue bonnet and Hen's checked apron are on every page they appear on. The swans wear
  nothing. No character wears anything else ("no hats, scarves or clothes on the swans or the ducklings").
- Pages 4 and 5 share the bank and differ by who acts: page 4, Dilly giggling and the Duckling alone and sad,
  no Mother Duck; page 5, Mother Duck's wing round him and Dilly head-down, sorry. Keep the teasing soft: Dilly
  giggles behind a wing, nobody points, pecks or turns away.
- Page 7: Hen smiles; her beak never points at the Duckling as if to peck. Page 9: snow is soft and nobody
  looks cold or shivers; the open barn behind Hen shows a warm golden straw glow (from the straw colour, not a
  lamp). Page 10: moonlight only, no flame of any kind.
- Page 8: the swans are small but clearly white swans with long necks, flying high; the family on the water
  keeps their faces in the top 70%.
- Page 11: one bird and his reflection. Say "his reflection in the water, not a second swan".
- Page 14: three ducklings on his back, nestled safely, not standing or about to fall.
- Ask for "no text, letters, words, sound effects or writing anywhere".

## Check results

**Art status (2026-10-26): cover and pages 1-14 drawn and prepared (all accepted at full size, full review PASS on 2026-10-26).** Minor accepted deviations: p4 Dilly wears two flowers; p8 and p9 a blue band under the text panel; p9 Mother Duck overlaps Hen slightly; p10 a two-pane window; p13 a grey beak; p14 a thin tuft. Line-up: `ugly-duckling/characters/lineup.jpg` (one chat, no separate sheets: 8 birds; the cygnet, the young white swan and the adult swan side by side).

| Page | Attempts | Size | Why earlier attempts failed |
| --- | --- | --- | --- |
| Line-up | 1 | 1792x2400 | none |
| Cover | 3 | 1792x2400 | 1: only 2 yellow ducklings; 2: same chat drew the same picture; 3 (fresh chat, "one line, each separate"): accepted |
| 1 | 1 | 1792x2400 | |
| 2 | 5 | 1792x2400 | earlier 4 as before; review redo (fresh chat, "twice as tall", full bleed): accepted, grey Duckling clearly bigger with long neck |
| 3 | 1 | 1792x2400 | |
| 4 | 3 | 1792x2400 | review redo: first vignette with cream top and a scribble in the corner; redo full bleed had two flowers on Dilly; fix accepted, bottom corners checked, no signature |
| 5 | 2 | 1792x2400 | review redo: vignette, Dilly covering her beak; redo full bleed, Dilly head bowed and beak uncovered, accepted, corners clean |
| 6 | 2 | 1792x2400 | review redo: vignette and scribble; redo full bleed accepted, corners clean |
| 7 | 1 | 1792x2400 | |
| 8 | 5 | 1792x2400 | 1: Mother Duck grey, leaves fill the bottom, nobody looks up; 2: ducklings with two beaks; 3: a sixth, white swan on the water; 4: same picture again; 5 (fresh chat, one row): accepted, flat blue band at the bottom |
| 9 | 2 | 1792x2400 | 1: only 2 yellow ducklings; 2: accepted (Mother Duck overlaps Hen's apron a little) |
| 10 | 1 | 1792x2400 | accepted; the window has a centre mullion (two panes, plan said one pane) |
| 11 | 1 | 1792x2400 | |
| 12 | 4 | 1792x2400 | earlier 3 as before; review redo (fresh chat): four white swans, young swan with tuft and long neck, a little smaller than the adults, accepted, full bleed |
| 13 | 8 | 1792x2400 | extra grey cygnet; two tufted swans; five swans; only 2 yellow ducklings; repeated pictures; accepted: two rows (4 swans back, Mother Duck and 3 yellow ducklings front), tufted swan with a dark grey beak |
| 14 | 4 | 1792x2400 | 1, 2: a grey cygnet hides behind the wing; 3: "tuft of grey down" drawn as a tiny bird on his head; 4: accepted ("a few grey feather strands") |

Method notes: click the newest picture by its `find` ref right after `__grab(1)`, then poll `__clipResult` (1-3 s). A fix prompt that returns the same picture needs a fresh chat. Counting traps: "grey Duckling" next to "young swan" makes Gemini add a grey cygnet or a second small swan; "tuft of grey down" can become a tiny bird; "loose circle" gives 5-6 swans. What worked: numbered rows ("back row swans 1-4, front row Mother Duck then three yellow ducklings"). Every picture came back 1792x2400 although every prompt carried the size line (the text-model picker stayed on 3.5 Flash-Lite).

Usage meter (Settings > Usage Limits, Pro plan): start 6% current / 6% weekly (current resets 10:05 AM, weekly Oct 27 10:05 AM); after about 30 pictures 11%; end 16% current / 6% weekly. The old Lion-and-Mouse Gemini tabs were closed before the first prompt (none of their chats were opened or changed).

Review redo (2026-10-26, pages 2, 4, 5, 6, 12): each in a fresh chat with the line-up pasted, 3.5 Flash-Lite, full-bleed wording ("no border, no vignette, no blank paper, no signature"); bottom 30% strips cropped and checked for marks. Usage meter after the redo: not re-read.
