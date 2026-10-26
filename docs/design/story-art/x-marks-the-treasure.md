# X Marks the Treasure!: art record

Store story (`StoreTexts.Treasure`, id `TreasureId`, stem `x-marks-the-treasure`, version 1, added 2026-10-26), ages 3-6,
author Ashkan Sirous, credit "A Dino Reader picture book · free to share". An original story, not a retelling.

**Series: Captain Kaatrin and Seagull** (the user's own series about a pirate captain and her seagull friend). This is book 1.
Keep every character line below for the later books, so the cast looks the same across the series.

**17 pages** (one action each, `dino-storytelling` skill). The user's limit for this book is 16-18 pages, which overrides the
skill's 10-18; `StoreStoryTests.MaxPages` is 18. **Art status: cover and all 17 pictures drawn and accepted by the illustrator (2026-10-26), prepared into `tests/DinoReader.Tests/Store/x-marks-the-treasure`; the full-mode reviewer passed (2026-10-26), after redraws of pages 6, 7, 8, 11, 13, 16 and 17 and a second redraw of page 6.** Known non-blocking variances to fix in the next book of the series: the crow's nest is a solid tub on pages 7 and 13 but a low wicker basket on pages 9 and 12; the distant ship on page 14 has several masts and a different hull; the cover has a second small cross. Saved line-up and Kaatrin's sheet: `docs/design/story-art/x-marks-the-treasure/characters/` (`lineup.jpg`, `kaatrin.jpg`).

**The captain's name is spelled "Kaatrin"** so the English voices say KAA-trin. English runs on Kokoro, which looks a word up
in `lexicon-us-en.txt` and otherwise uses espeak en-us: "Katrin" is not in the lexicon and comes out /kˈætɹɪn/ (KAT-rin, a as
in cat), while "Kaatrin" and "Kahtrin" both come out /kˈɑːtɹɪn/ (KAA-trin); "Kaatrin's" is /kˈɑːtɹɪnz/ (checked on the
Kokoro pack from the `packs-7` release, 2026-10-26). The translations spell her the way each language reads KAA-trin:
Kaatrin in Dutch and Finnish (aa is a long a there), Katrin in Danish, Norwegian and Swedish (aa would read as å), Spanish,
Portuguese and French, Катрин, كاترين, کاترین, カートリン (the ー makes the long a), 卡特琳.

**Gentle story (Decision 130):** pirates with no swords, cannons, skulls, planks or baddies. "Too hungry" means slow, yawning,
sighing and rumbling tummies, never ill or weak. Pancake Polly is never shown cooking over a flame: she stirs batter in a bowl,
and the pancakes come up already made. Billy sits safely inside a high-sided basket at the top of the mast. The water the
pirates wade through is knee-deep and calm. The party at sunset has no bonfire, torches or flames.

**Why everyone does what they do:** Kaatrin runs because she has just found the map; Seagull lands on her hat because they
are friends; Seagull stays on the hat because Kaatrin asked her to sit there until they find the treasure; Billy and Izzy
don't answer because they are too hungry; Seagull leaves the hat to find out what is wrong, sees that every tummy is
rumbling and goes to the one person who can help, the cook; with full tummies Billy can look again and spots the island;
Seagull reaches the X first because she can fly; the ending pays the start back (she landed on the hat on page 2 and
snuggles down on it on page 17).

**Refrains:** the crew's song, on pages 5, 12 and 16: `"Yo ho, yo ho!" sang Izzy.` / `"Yo ho, yo ho!" sang Billy.` /
`"We're going to find the lost treasure!" sang Pancake Polly.`, identical on pages 5 and 12; on page 16 Polly's line turns
into `"We found the lost treasure!"` as the payoff. `"Squawk! Squawk! Squawk!"` opens Seagull's line on pages 2, 9 and 14.
`"X marks the treasure!"` on page 2 (Kaatrin) and page 14 (Seagull). `tummy went rumble, rumble, rumble!` on pages 7, 8 and 9.
Sound words are triples (Decision 151): Flap, flap, flap; Yum, yum, yum; Splish, splash, splosh; Dig, dig, dig.

**Text review fixes (2026-10-26, reviewer text pass):** page 3's crew introduction is four short sentences; page 7 says the
crew feel "slow and grumbly, and nobody knew why" (a small mystery Seagull solves on page 9); page 8 drops the double "too";
the wading moved to page 14 (the pirates splash ashore behind Seagull) and page 15 is only the digging and the chest.

**Speakers** (the cast the builder finds; each a bare capitalised name): Kaatrin ("Captain Kaatrin" once in the narration
on page 1, merged with "Kaatrin"), Seagull, Izzy, Pancake Polly, Billy. Kaatrin, Seagull, Izzy and Polly are cast female
(from "she"/"her" after their names), Billy male (name table). Seagull's name has no animal word the guess knows
("seagull" is not in the bird list), so no "Tweet tweet!" is added after her lines; she says her own "Squawk!".

**Style:** Children's picture book illustration, soft watercolour and coloured pencil, warm gentle colours, simple rounded
shapes, no text or letters anywhere. Portrait 3:4.

## Characters (accepted line-up saved in `x-marks-the-treasure/characters/lineup.jpg`; the lines below still hold)

- **Captain Kaatrin:** a grown-up woman, the pirate captain and the biggest character: tall, big and round, cheerful, with
  round rosy cheeks, freckles and a wide smile. Long curly copper-red hair in one thick plait over her shoulder. A **big black
  tricorn pirate hat with a gold edge**, wide and flat enough on top for Seagull to sit on (no skull, crossbones or feather).
  A long red coat with big gold buttons, a white shirt, a wide sky-blue sash, dark-blue trousers, black boots. Her prop: a
  brass telescope, tucked in her sash when she is not using it. Never an eyepatch, hook, peg leg, sword or pistol. Draw her
  round and jolly, never as a joke about her size. Tells her apart: the only tricorn hat and the only red coat.
- **Seagull:** a white seagull, her body about as wide as Kaatrin's hat: white head and body, soft grey wings with black wing
  tips and little white spots, a yellow beak with a small red spot, pink-orange webbed feet, round friendly eyes. Her prop: a
  small blue-and-white striped neckerchief tied at her neck. She is the only bird in the book: no other gulls or birds anywhere.
- **Izzy:** a girl of about seven, the smallest of the people (she reaches Kaatrin's waist). Brown skin, black curly hair in
  two round puffs, a purple bandana, a yellow T-shirt under a purple waistcoat, green knee-length shorts, brown sandals. Her
  prop: a round brass compass on a cord around her neck. She looks after the map. Tells her apart: purple bandana and waistcoat.
- **Pancake Polly:** a grown-up woman, the ship's cook: as tall as Kaatrin but thin. Pale freckled skin, blonde hair in a high
  bun under a white headscarf with red polka dots, a light-green dress with the sleeves rolled up, a white apron with one big
  front pocket, brown shoes. Her prop: a wooden spatula in her apron pocket. Tells her apart: the polka-dot headscarf and apron.
- **Billy:** a boy of about twelve, between Izzy and Polly in height. Light-brown skin, short spiky black hair under a green
  bandana tied as a headband, a red-and-white striped T-shirt, blue trousers rolled up to mid-calf, bare feet. His prop: a coil
  of rope worn across his chest (he is the climber). Tells him apart: the striped T-shirt and green headband.

There are no other characters: no other people, pirates, birds or animals in any picture.

## Places (the same in every picture)

- **The harbour:** a stone quay, a row of little painted houses behind it, two or three small empty rowing boats, a sandy
  beach at one end, blue morning sky. Pages 1-2 (and behind the ship on pages 3-6).
- **The ship:** a small, friendly wooden pirate ship. Honey-brown hull with one blue stripe; one tall mast with a round
  high-sided basket (crow's nest) at the top; one big square cream sail; the ship's wheel at the back on a low raised deck; a
  few wooden barrels on deck; a hatch with steps going down to the kitchen; a blue flag with a white seagull shape on it (no
  skull, crossbones, letters or text). No cannons. Pages 3-13.
- **The ship's kitchen:** a small, cosy wooden room below deck with a round porthole, shelves of jars and a wooden table with
  a big blue mixing bowl. No stove, oven, fire, flames, pots on a heat or steam. Page 10.
- **The open sea:** calm blue sea with gentle waves and no land, sunny. Pages 7-12; on page 13 a small island far away.
- **Treasure Island:** a small round island with three palm trees and a wide sandy beach; calm, shallow turquoise water in
  front; the ship waiting a little way out. Pages 13 (far away), 14-17.
- **The X:** the only mark allowed in any picture. On the map it is two crossed red strokes; on the beach it is two long
  sticks of driftwood lying crossed on the sand. Ask for "a red cross mark" / "two sticks crossed", never "the letter X".

## Picture plan

Every page: the whole scene sits in the top 70% of the picture; the bottom 30% is plain deck, sand, sea or floor with nothing
important in it. No text, letters, words, sound effects or writing anywhere ("Squawk!", "Flap", "Yo ho" and "rumble" are never
drawn in). Every page is **new** (no pictures exist).

| Page | One action (who, count, where, when) | Picture |
| --- | --- | --- |
| Cover | Kaatrin (1) on the ship's deck holding up the open map and beaming, Seagull (1) on her hat; Izzy, Polly and Billy (3) around her smiling at the viewer; sunny morning sea behind | new |
| 1 | Morning, above the harbour: Seagull (1) flies big in the upper picture, looking down; below on the stone quay Kaatrin (1) runs towards her ship, waving a rolled-up paper and smiling | new |
| 2 | Morning, on the quay beside the ship: Seagull (1) has just landed on top of Kaatrin's hat, wings still half open; Kaatrin (1) holds up the open map (a little island and a red cross mark) and beams | new |
| 3 | Morning, on deck in the harbour: Kaatrin (1) stands with one arm raised, calling, Seagull (1) on her hat; Izzy, Pancake Polly and Billy (3) come running across the deck towards her | new |
| 4 | Morning, on deck, close up: Seagull (1) flaps her wings happily on Kaatrin's hat; Kaatrin (1) laughs up at her. Only these two | new |
| 5 | Morning, on deck in the harbour: Izzy, Pancake Polly and Billy (3) dance a pirate dance in a row, singing; Kaatrin (1), with Seagull (1) on her hat, claps at the side | new |
| 6 | Morning, sailing out to sea: Billy (1) pulls the rope and the big sail opens above him; Kaatrin (1) turns the ship's wheel, Seagull (1) on her hat; the harbour small and shrinking far behind them | new |
| 7 | Midday, open sea: Billy (1) sits in the crow's nest at the top of the mast, chin on the rim, a hand on his tummy, giving a big yawn; the top of the sail below him and sea all around. Only Billy | new |
| 8 | Midday, on deck at sea: Izzy (1) sits on a barrel with the map open on her lap and the compass on its cord, eyes half shut, giving a big droopy sigh. Only Izzy | new |
| 9 | Midday, over the deck at sea: Seagull (1), bright and alert, flies low over the deck looking down; below, Kaatrin (1) at the wheel with a hand on her tummy, Izzy (1) slumped on her barrel, Billy (1) drooping in the crow's nest | new |
| 10 | Midday, the ship's kitchen: Pancake Polly (1) stirs batter in her big blue bowl at the table and looks up; Seagull (1) stands on the table edge talking to her. No stove or fire | new |
| 11 | Early afternoon, on deck at sea: Pancake Polly (1) hands out plates of pancakes with banana slices, strawberries and honey; Kaatrin, Izzy and Billy (3) sit on barrels eating happily; Seagull (1) pecks a pancake on a plate | new |
| 12 | Afternoon, on deck at sea: Izzy and Pancake Polly (2) dance across the deck; Billy (1) sings and waves from the crow's nest; Kaatrin (1) smiles at the wheel, Seagull (1) on her hat; empty plates stacked by a barrel | new |
| 13 | Afternoon, at sea: Kaatrin (1) at the rail looks through her brass telescope, Seagull (1) on her hat; Billy (1) points from the crow's nest; Izzy and Pancake Polly (2) cheer with their arms up; a small island with three palm trees far off | new |
| 14 | Late afternoon, Treasure Island beach: Seagull (1) stands on the sand beside two long sticks crossed in a big X, wings up, beak open in a happy squawk; behind her Kaatrin, Izzy, Pancake Polly and Billy (4) splash ashore through knee-deep water carrying spades; the ship waits further out | new |
| 15 | Late afternoon, on the beach by the crossed sticks: Kaatrin, Izzy, Pancake Polly and Billy (4) dig with spades around a hole; an old wooden chest comes up out of the sand; Seagull (1) perches on one of the sticks | new |
| 16 | Late afternoon, on the beach: Kaatrin (1) holds the chest lid open, gold coins, sparkly jewels and pearls glowing inside; Izzy, Pancake Polly and Billy (3) lean in, singing; Seagull (1) on the chest's edge | new |
| 17 | Sunset, pink and gold, on the beach: Seagull (1) is snuggled down on top of Kaatrin's hat; Kaatrin (1) reaches up and holds her gently with both hands, both with eyes closed and smiling; behind them Izzy, Pancake Polly and Billy (3) dance; the open chest at the side. No bonfire, torches or flames | new |

Check (dino-storytelling section 4):

- [x] Every page's picture differs from its neighbours (place, action or who is there).
- [x] No page needs two pictures.
- [x] Characters' faces and the action fit in the top 70% of a 3:4 portrait picture.
- [x] Each character's look is fixed in the line-up, not invented per page.

Notes for the illustrator:

- Page 6 holds two people acting at the same moment (Billy hauls the sail up while Kaatrin turns the wheel): one picture of the
  ship setting off. Page 13 likewise (Billy points while Kaatrin looks through the telescope): one picture of the island sighting.
- Pages 5 and 12 are both dancing on deck: tell them apart by the harbour behind and all three dancing (5) against open sea,
  Billy up in the crow's nest and the empty plates (12).
- Pages 7, 8 and 9 build up: Billy alone (7), Izzy alone (8), then everyone sleepy seen from Seagull above (9). Sleepy means
  droopy eyelids, yawns and sighs, never pale, sick or crying.
- Seagull is on Kaatrin's hat on pages 2-6, 12, 13 and 17; she flies on 1 and 9, is in the kitchen on 10, eats on 11, is on
  the beach (by the X or on a stick or the chest) on 14-16. Kaatrin's telescope is in her sash except on page 13.
- Ask in every prompt for "no weapons, no swords, no cannons, no skulls or crossbones, no eyepatch, no fire or flames",
  "no other birds", and "no text, letters, words, sound effects or writing anywhere".

## Check results

| Page | Attempts | Size | Why earlier attempts failed |
| --- | --- | --- | --- |
| Kaatrin sheet | 3 | 1792x2400 | 1: a wooden peg leg in the side view (the model turned 'pirate' into a peg leg); 2: peg leg again; 3: asked for front and back views only plus faces, round and jolly, two boots: accepted. Gemini wrote the words calm/happy/surprised on the sheet (reference only, never used on a page) |
| Line-up | 2 | 1792x2400 | 1: name labels (Izzy twice), Izzy had no waistcoat and blue shorts, Polly no headscarf and a striped dress, Billy brown trousers and no bandana; 2: no labels, every outfit right. No separate sheets for Seagull, Izzy, Polly and Billy: the line-up served as their sheet |
| Cover | 1 | 1792x2400 | the first message (outfit opener plus cover prompt in one) got "I don't know"; the same cover asked alone passed. Map shows two small red crosses |
| 1 | 1 | 1792x2400 | |
| 2 | 2 | 1792x2400 | 1: Gemini replied with text and no picture; asked again, passed |
| 3 | 1 | 1792x2400 | |
| 4 | 1 | 1792x2400 | |
| 5 | 1 | 1792x2400 | Izzy's hair puffs hidden under the bandana (minor) |
| 6 | 5 | 1792x2400 | REDRAW after the full review and re-review. Earlier tries: seam and two sails; a second ship behind (twice); a slim Kaatrin with gold braid, a sash across the chest, a lighthouse and hills, a bow post. Attempt 5 (kept): big round Kaatrin, plain red coat with round gold buttons, sash round the waist; Billy with rope coil hauling the sail rope; one square sail; painted-house harbour small behind; empty bow; soft watercolour line |
| 7 | 2 | 1792x2400 | REDRAW. Original: seam and a barred nest. Redraw: solid tub nest, Billy inside yawning, one continuous scene to the bottom edge |
| 8 | 3 | 896x1200 | REDRAW. Original: seam. Redraw 1: white blank sky and a black cross on the map; 2 (kept): blue sky, red cross on the map, deck to the bottom edge |
| 9 | 3 | 1792x2400 | 1: hills and land on the horizon (open sea needed); 2: Billy perched on the basket rim instead of inside it; 3: passed |
| 10 | 1 | 1792x2400 | |
| 11 | 2 | 1792x2400 | REDRAW. Original: blank top and plain bottom. Redraw: blue sky, Polly with a tall pile of pancake plates, everyone eating, Seagull pecking |
| 12 | 1 | 1792x2400 | |
| 13 | 5 | 1792x2400 | REDRAW. Earlier: 2 drifted; 3: pointed the wrong way; redraw 4: an extra striped child and no Polly; 5 (kept): exactly five characters, solid tub, Billy pointing left to the island, telescope aimed at it, three palm trees |
| 14 | 1 | 1792x2400 | |
| 15 | 1 | 1792x2400 | |
| 16 | 2 | 1792x2400 | REDRAW. Original: Polly and Billy dug. Redraw: chest open with gold, jewels and pearls, nobody digging, spades on the sand, Seagull on the driftwood |
| 17 | 3 | 1792x2400 | REDRAW. Earlier: palms up, no hug. Redraw: Seagull asleep on the hat, Kaatrin's hands raised round her, eyes closed on both, crew dancing, chest at the side, continuous sand; Kaatrin is barefoot (boots missing) |

Cover: two redraw tries for ONE red cross failed (try 1 still two crosses, try 2 drew a second seagull); the first cover is kept (its map has a big and a small red cross).

Session notes (2026-10-26): Gemini shows at most about two pictures per chat before the third send is silently ignored (the request returns 200 at once and nothing draws); a fresh chat fixed it every time. The page often looks frozen after Return for 20-60 s; check `StreamGenerate` in the network log (pending = drawing) before sending again. Starting a book's pictures with 'Lovely, page n is accepted' follow-ups in one chat worked for 2 pictures. Another illustrator's download was taken by mistake once at the start (restored). All saves were done through the clipboard grab, not the download folder.

Redraw session (after the full-mode review): a send is really going through only when the page shows a Stop response button; when Return leaves the text in the box, click the Send message button once and check again. Keep each picture in a fresh chat.
