# Love from Seagull Island: art record

Store story (stem `love-from-seagull-island`, version 1, added 2026-11-17), ages 3-6, author Ashkan Sirous, credit "A Dino Reader
picture book" (the user's own book: not free to share, no `retold`). `storyBy`: Ashkan Sirous (the user's own story, given beat by beat by
the user on 2026-11-17).

**Series: Captain Kathryn and Seagull**, book 2. Book 1 is `content/dino/x-marks-the-treasure/`; its cast lines are copied below word for
word so the cast looks the same across the series.

**Why this book (the user, 2026-11-17):** the user's daughter is Seagull, the user is Kathryn, the mum is Pancake Polly. All four
grandparents live far away "back home"; the mum's parents are staying for a couple of months and fly home soon. The book helps a small child
understand that grandparents visit and then go home, that you miss them, that they still love you and you see them on video calls, and that
you will see them again. The book never names Kathryn or Polly as Seagull's parents (as in book 1, they are her friends on the ship).

**Status (2026-11-17): English text only, waiting for the user's approval.** The user said "dont create voice or images yet, lets create the
text first": no pictures, character sheets, line-up or premade audio exist. Every page and the cover need a new picture.

**Second user revision (2026-11-17):** the treasure hunt is gone (Izzy, Billy, the rowing boat, the island, the chest and the shell gift),
so the book is only about playing with the grandparents; the other grandparents are Grandma and Grandpa (no longer Nana); the ending is one
video call with all four grandparents on the screen together, saying one line together. **The translations in `translations/` are from the
previous text and were deliberately not touched** (the user approves the English first); `pieces.json` and all 14 languages must be redone
after approval.

**16 pages** (one action each, `dino-storytelling` skill; book 1 had 17; the user's range is 16-18; `StoreStoryTests.MaxPages` is 18).

**Setup note (illustrator, 2026-11-17, model 3.1 Pro):** sheets, line-up and one place sheet are drawn and accepted by my own check
(`characters/`), each in its own new chat, 1792x2400 (7 pictures, 7 chats, about 1 picture each; the place sheet needed a second try:
the first drew the ship from the quay). Tool notes: `__drawPicture` first grabs a small webp preview (40-58 KB, no download appears);
call `await window.__grab(1)` again for the real jpeg size and save with that size. `__drawPicture(prompt, 0)` refuses (attachment count
UNKNOWN): use `__markPictures()` + `__setupPicture(prompt, 0)` + `__waitForPicture()` + `__grab(1)`. After about 6 downloads a tab stopped
writing files; a fresh tab fixed it (`__grab` in a new tab on the same chat URL). Five files paste in one ctrl+v.

## Accepted by the user

- **English text, all 16 pages (2026-11-17):** the user, on the 16-page text with pages 5 and 14 added: "good. wirte it. make the book. do the pictures in parallel 4 at a time". The English is settled; change it only if the user names a page.
- Pictures: none yet.

## Gentle story (rule 1, Decision 130)

The pretend games are the child's own real games, kept recognisable, with nothing that hurts:

- **Bee game (page 6):** Seagull's "sting" is a tickly pretend sting with a wingtip: "It was as soft as a feather. It tickled!" The user's
  line "Help, I'm on fire!" became `"Ooh! Help, help! I'm too hot! I need a firefighter!"`, said giggling and followed at once by "It was all
  just pretend, of course." It keeps the game's chain (sting, "hot", call the firefighter, Granny with the hose) without the word "fire",
  which `GentleTextTests` bans in every English store text (as it bans "hurt" and "pie"; the first draft had all three). No flames, smoke,
  red glow or heat anywhere in any picture.
- **Firefighter Granny (page 7):** a red toy helmet and the ship's deck hose, sprinkling **cool** water; Grampy says "Ahh, that's much
  better!" and everybody laughs. No fire in the picture.
- **Blanket game (pages 8-9):** Grampy hides under a soft blanket on purpose and is pretend-stuck, wiggling and giggling; Seagull "calls"
  the police with her wing held to her ear like a phone (no prop); Police Granny in a blue toy cap whisks the blanket off and Grampy is
  laughing. Nobody is trapped or scared.
- **Granny's cake (page 10):** a sea-berry cake (the first draft said "pie", a banned word). The text says she made it; the picture shows
  only the finished cake set on a table on deck. No stove, oven, flame, steam or pot on heat (as with Pancake Polly in book 1).
- **The plane:** a small friendly seaplane landing, waiting on calm water and flying away; nobody falls or is in danger. Planes are fine
  (the user, 2026-11-17).
- **Goodbye sadness (pages 11-14):** gentle only: a drooping head, "she would miss them", soothed at once by Grampy ("We'll miss you too"),
  then Kathryn and Polly's hug and Kathryn's words ("And you'll see them again!"); Seagull "felt much better". The grandparents leave for a
  reason that has nothing to do with Seagull: "Their cosy home on Seagull Island was waiting." At the plane Grampy promises "We'll call you
  on the phone, and we'll see you again!" and Seagull says her own goodbye ("Bye-bye, Granny! Bye-bye, Grampy! I love you!"). Never crying
  or upset faces in a picture; soft, wistful faces.
- No weapons, skulls, cannons or baddies (book 1's pirate rules still hold).

## Why everyone does what they do

Polly finds the bottle because she is fishing with her net by the ship; she calls Seagull because the letter is hers. Seagull is happy but
unsure because she has not played with her grandparents for a long time; Kathryn's answer ("Give them a big hug, and you'll know just what to
play!") is paid back on page 6 ("And now Seagull knew just what to play!"), right after she has stuck to them like a little shadow (page 5).
Seagull Island is much too far for wings, which is why the grandparents come and go by plane (page 4). Grandma and Grandpa are first named on
page 11 as "her other grandparents far away", before they appear on the video call on page 15. Kathryn's phone, promised on page 12, rings
on page 15, and Grampy's promise at the plane ("We'll call you on the phone") comes true. On the call all four say together that they all
miss her: the point is that they all miss her, not that they live apart (the user, 2026-11-17). The ending pays back book 1 (Seagull
snuggles down on Kathryn's hat) and the start of this book (Seagull Island, "Lots of love" in the letter, the title).

## Refrains and sound words

- `"We love you more than the whole wide world!"`: Granny Seagull on page 4; on page 15 all four grandparents say it together as `"We all
  miss you, and we love you more than the whole wide world!"`, and Seagull answers `"I love you more than the whole wide world too!"`.
- `"Squawk! Squawk! Squawk!"` opens Seagull's happy line on page 2, as in book 1; the ending reuses book 1's exact piece
  `"Squawk, squawk, squawk," said Seagull softly`.
- "just what to play" (pages 3 and 6); "Mwah!" (pages 13 and 15); "laughed and laughed" (page 7) / "laughing and laughing" (page 9).
- Sound words: splash, Bzzz, Psssh, Nee-naw, whoosh, Yum, yum, yum, Mwah, Brrrm, Ring, ring. No ALL-CAPS.

## Speakers and animals

Speakers (each a bare capitalised name in every tag, spelled the same everywhere): Pancake Polly, Seagull, Kathryn, Granny Seagull, Grampy
Seagull. Inside quotes and narration the short forms "Granny and Grampy" are used for the visiting pair; every speech tag uses the full
name. Grandma and Grandpa never speak alone: the page 15 line is tagged `they said together` (all four grandparents), so they have no cast
entry. **Open for the voice pass:** which voice reads that chorus line (a tag with no name; probably the narrator). Izzy and Billy are not in
this book any more and were removed from `characters` and `cast`.

Animals (Slice 152, Decision 157): the three speaking gulls (Seagull, Granny Seagull, Grampy Seagull) are listed in `notAnimals`, so they
are cast like people and no "Tweet tweet!" is added after their lines (the bird sound would be wrong for a gull; book 1's Seagull was stored
as none for the same reason, with no entry). "Seagull" is not in any language's animal word list, so nothing is guessed from the names.
Kathryn and Pancake Polly are people (being `Pirate`), stored as none as in book 1. Granny Seagull and Grampy Seagull carry `being: Person`
in their cast traits: `AuthoredVoiceTests` requires a person or fantasy kind for every no-animal character except the names on its list of
kindless animals ("Seagull" is on it, so Seagull keeps book 1's entry with no `being`). `Person` only steers the voice fallback towards
people's voices; it changes nothing in the pictures, where all five grandparents and Seagull are gulls.

## Voices (Slice 156; `book.json` `cast`)

Book 1's entries for Seagull `duckling2`, Kathryn `princess` and Pancake Polly `bee` are kept exactly, so the series sounds the same. New:

- **Granny Seagull = `hen`** (Granny Hen, "slow and cosy", af_aoede Sleepy in English): an old, warm woman's voice that is a bird already;
  Female, Old, Warm + Soft. Her base voice differs from Seagull's (af_kore) and Polly's (af_sky), who share pages with her.
- **Grampy Seagull = `bear`** (Grandpa Bear, "deep and kind", bm_daniel Sleepy): the roster's grandad-like male speaker; Male, Old, Warm +
  Booming. He is the only male speaker in the book.

**To settle when voices are done (the user said no voices yet, 2026-11-17):** Seagull's cast age is `Adult`, copied from book 1, although
she is the grandchild in this book; and who reads the grandparents' chorus on page 15. (The earlier note about Nana's `mummy` voice is gone
with Nana: Grandma and Grandpa have no lines of their own.)

No two characters share a roster id. `dump-voices`'s mechanical suggestion was ignored: it has no idea who these characters are. As in book
1, Spanish, Portuguese and Farsi have one woman's voice, so every woman shares it there (flavours differ). Not listened to yet (no audio exists).

## Names in the translations

**Translated 2026-11-17 from the approved English** (95 pieces, all 14 languages; the 16 new or reworded pieces translated, the rest
reused unchanged, the 25 pieces of the older draft removed). Book 1's forms for Seagull, Kathryn and Pancake Polly are reused in every
language. Granny Seagull / Grampy Seagull / Seagull Island keep their names. Grandma and Grandpa (the dad's parents, never speaking alone)
need a word that differs from the visiting pair's in every language, so they keep the earlier round's second pair: de/nl Omi und Opi /
Omi en Opi, fr Mémé et Pépé, es Abuelita y Abuelito, pt a Vozinha e o Vozinho, fi Fammu ja Pappa, ru Бабуля с Дедулей, ar تيتا وجدو,
fa مادرجون و آقاجون, ja ばあば/じいじ; da/nb/sv Farmor og/och Farfar and zh 奶奶/爷爷 name the father's side (the visitors are Mormor/Morfar,
外婆/外公, the mother's parents). The page 15 chorus tag "they said together" is all four: de "sagten sie alle zusammen", fr "ont-ils dit
tous ensemble", fa «همه با هم گفتن» (joined speaker first), ja 「とみんなはいっしょに言いました」, zh 「他们一起说」. "You're our little
shadow!" uses the local idiom where one exists: ru «Ты наш маленький хвостик!», zh 「你是我们的小尾巴！」 (both "little tail", said of a child
who follows you everywhere); pt avoids "sombrinha" (also "umbrella" in Brazil) and says "sombra pequenininha".

Earlier forms of the visiting pair and the island:

- de Oma Möwe / Opa Möwe / die Möweninsel; fr Mamie Mouette / Papi Mouette / l'île aux Mouettes; es Abuela Gaviota / Abuelo Gaviota / la
  Isla de las Gaviotas; pt Vovó Gaivota / Vovô Gaivota / a Ilha das Gaivotas; nl Oma Meeuw / Opa Meeuw / het Meeuweneiland.
- da Mormor Måge / Morfar Måge / Mågeøen; nb Mormor Måke / Morfar Måke / Måkeøya; sv Mormor Mås / Morfar Mås / Måsön (these three name the
  side: the visiting pair are the mother's parents, as in the family the book is for); fi Mummi Lokki / Vaari Lokki / Lokkisaari.
- ru Бабушка Чайка / Дедушка Чайка / Чаячий остров; ar الجدة نورسة / الجد نورس / جزيرة النوارس (simple Modern Standard, no vowel marks, as
  book 1); fa مامان‌بزرگ مرغ دریایی / بابابزرگ مرغ دریایی / جزیره‌ی مرغای دریایی (spoken forms); ja カモメおばあちゃん / カモメおじいちゃん /
  カモメじま; zh 海鸥外婆 / 海鸥外公 / 海鸥岛 (外婆/外公 are the mother's parents).

Not yet read by a native speaker in any language.

## Style

Children's picture book illustration, soft watercolour and coloured pencil, warm gentle colours, simple rounded shapes, no text or letters
anywhere. Portrait 3:4.

## Characters

Book 1's lines, word for word (Izzy and Billy are not in this book):

- **Captain Kathryn:** a grown-up woman, the pirate captain and the biggest character: tall, big and round, cheerful, with
  round rosy cheeks, freckles and a wide smile. Long curly copper-red hair in one thick plait over her shoulder. A **big black
  tricorn pirate hat with a gold edge**, wide and flat enough on top for Seagull to sit on (no skull, crossbones or feather).
  A long red coat with big gold buttons, a white shirt, a wide sky-blue sash, dark-blue trousers, black boots. Her prop: a
  brass telescope, tucked in her sash when she is not using it. Never an eyepatch, hook, peg leg, sword or pistol. Draw her
  round and jolly, never as a joke about her size. Tells her apart: the only tricorn hat and the only red coat.
- **Seagull:** a white seagull, her body about as wide as Kathryn's hat: white head and body, soft grey wings with black wing
  tips and little white spots, a yellow beak with a small red spot, pink-orange webbed feet, round friendly eyes. Her prop: a
  small blue-and-white striped neckerchief tied at her neck. She is the only bird in the book: no other gulls or birds anywhere.
- **Pancake Polly:** a grown-up woman, the ship's cook: as tall as Kathryn but thin. Pale freckled skin, blonde hair in a high
  bun under a white headscarf with red polka dots, a light-green dress with the sleeves rolled up, a white apron with one big
  front pocket, brown shoes. Her prop: a wooden spatula in her apron pocket. Tells her apart: the polka-dot headscarf and apron.

**Book 2 change to Seagull's last sentence:** in this book the only birds are the five gulls (Seagull, Granny Seagull, Grampy Seagull, and
Grandma and Grandpa, who appear only on the phone screen on page 15 and on the back cover). No other gulls or birds anywhere. Seagull is the
smallest gull and the only one with the blue-and-white striped neckerchief.

New in book 2:

- **Granny Seagull:** a seagull a little bigger and rounder than Seagull (about one and a half times her size), softer and fluffier: white
  head and body with a few silvery-grey feathers on the crown, soft grey wings with black wing tips, yellow beak with a small red spot,
  pink-orange webbed feet, kind crinkly eyes. A **lilac knitted shawl** round her shoulders and **a pink flower tucked above one eye**.
  Her prop: a small lilac handbag with a round clasp (pages 4 and 13 only). On page 7 she wears a red toy firefighter's helmet over the
  flower, on page 9 a blue toy police cap; the shawl stays on. Tells her apart: lilac shawl and pink flower; never a neckerchief, glasses,
  hat (outside pages 7 and 9) or cap.
- **Grampy Seagull:** the biggest gull, a little taller than Granny: white head and body, soft grey wings with black wing tips, yellow
  beak with a small red spot, pink-orange webbed feet, a **fluffy white tuft of feathers on top of his head** and bushy white "eyebrow"
  feathers, twinkly eyes. A **brown flat cap** and a **bottle-green knitted waistcoat** with three round wooden buttons. His prop: none
  besides the cap. Tells him apart: flat cap and green waistcoat; never a neckerchief, glasses or bow tie.
- **Grandma** (on the phone screen on page 15, whole only on the back cover): a seagull the size of Granny Seagull: white head and body,
  soft grey wings with black wing tips, yellow beak with a small red spot, pink-orange webbed feet. A **sky-blue cardigan** and **a string
  of round white pearl beads** at her neck. Tells her apart from Granny: blue cardigan and pearls, no shawl, no flower.
- **Grandpa** (on the phone screen on page 15, whole only on the back cover): a seagull the size of Grampy Seagull: white head and body,
  soft grey wings with black wing tips, yellow beak with a small red spot, pink-orange webbed feet. **Round black-rimmed glasses** and **a
  red bow tie**, no hat. Tells him apart from Grampy: glasses and red bow tie, no cap, no waistcoat.

There are no other characters: no other people, pirates, birds or animals in any picture, and **no pilot** in the plane (it is always seen
empty, from outside, or with only Granny and Grampy at its door or windows).

**Accepted looks (setup, 2026-11-17; files in `characters/`).** Written from the accepted pictures; the lines above still hold, with these
points as drawn: Granny Seagull's lilac handbag hangs on a thin strap over her shoulder (it can also be held on a wing); her flower is
pink with a yellow centre, a few silvery feathers on her crown. Grampy Seagull's white feather tuft sticks out at the side of his brown flat
cap, with bushy white eyebrows, green knitted waistcoat, three wooden buttons. Grandma: smooth white head (no tuft), sky-blue cardigan with
a slightly darker edge, one string of white pearls. Grandpa: round black glasses, red bow tie, smooth white head, no hat. Sizes in the
line-up, small to big: Seagull, Granny and Grandma, Grampy and Grandpa; Kathryn and Polly are people about three times the gulls' height.
Kathryn, Seagull and Pancake Polly are as in book 1 (`x-marks-the-treasure/characters/lineup.jpg`).

- `characters/lineup.jpg`: the seven, left to right: Kathryn, Polly, Seagull, Granny, Grampy, Grandma, Grandpa (no Izzy or Billy).
- `characters/seagull.jpg`, `granny.jpg`, `grampy.jpg`, `grandma.jpg`, `grandpa.jpg`: sheets (front, side, back, three faces). Seagull's
  sheet is new (drawn from book 1's line-up); Kathryn's and Polly's sheets are book 1's.
- Known flaws, harmless: the sheets' size differences are small (the line-up is the size reference); Grandpa's calm face on his sheet lacks
  the glasses and bow tie (the other views have them); in the line-up Granny stands turned a little to the side, overlapping Seagull.
- If book 1's line-up is ever pasted, the prompt must say "leave out the girl with the purple bandana and the boy with the green
  headband" (it worked first time); pages paste only the book-2 `lineup.jpg`, which has neither.

## Places and things (the same in every picture)

**Place sheet (accepted 2026-11-17): `characters/place-ship-deck.jpg`**: the ship's deck by day with the harbour behind, empty, no
characters. Used by every page set on the deck: the cover and pages 1-16 (pages at another time of day paste it too and name the light in
the prompt: sunset on 10, dusk on 11, evening 12 and 15, night 16). Not used by the back cover (Seagull Island, one picture, no sheet).
Page prompts paste `lineup.jpg` plus this sheet (two files in one lock hold).

- **The harbour:** book 1's: a stone quay, a row of little painted houses behind it, two or three small empty rowing boats, a sandy beach
  at one end, blue sky. The ship is moored just off the quay for the whole visit. Behind the ship on pages 1-14.
- **The ship:** book 1's, with its variances fixed: a small, friendly wooden pirate ship, honey-brown hull with one blue stripe, **exactly
  one** tall mast with a **solid round wooden tub crow's nest** at the top (wooden planks, high sides; never a wicker basket, never a
  barred cage), one big square cream sail, the ship's wheel at the back on a low raised deck, a few wooden barrels on deck, a hatch with
  steps going down to the kitchen, a blue flag with a white seagull shape on it (no skull, crossbones, letters or text). No cannons. The
  ship's deck hose: a plain green garden-style hose coiled by a barrel (page 7). A small round wooden table on deck (pages 5 and 10).
- **The seaplane:** a small, round, friendly yellow seaplane with two white floats, one propeller on its nose, three round windows and a
  round door on its side. No writing, numbers, logos or pilot. It floats on calm water beside the ship (pages 4 and 13) and is small in the
  sky over the sea on page 14.
- **The photo:** a small square photo with a white border showing Granny and Grampy Seagull smiling and waving (their heads and shoulders,
  their outfits as above). Pages 1 (in Polly's hand with the letter), 2, 16. The letter is a cream sheet seen from the back or folded: no
  writing visible.
- **The blanket:** a big soft patchwork blanket in pastel squares (pink, yellow, sky-blue). Pages 8-9.
- **The cake:** a round golden sponge cake topped with deep blue-purple berries, on a blue plate, one slice cut out. Page 10 and the back
  cover.
- **Kathryn's phone:** a small phone in a plain sky-blue case, no logo. Its screen shows only the four callers (faces and shoulders, side by
  side in one picture, not in separate boxes) against their background, no buttons, icons, text or numbers. Pages 12 (in her hand, screen
  dark) and 15.
- **Seagull Island** (only on the phone screen and the back cover): grassy green cliffs over a calm sea, a little white cottage with a round
  blue door, clumps of sea pinks. All four grandparents squeeze together in front of the cottage for the call (page 15).

## Picture plan

Every page: the bottom 32% is scene, every character whole above 68% (the scene there is deck planks, sand or calm water, never a plain
empty strip). No text, letters, words, sound effects or writing anywhere ("Squawk!", "Bzzz", "Nee-naw", "Mwah", "Brrrm", "Ring, ring" are
never drawn in; no writing on the letter, the phone or the plane). No fire, flames, smoke or steam in any picture. Every picture is new.

| Page | One action (who, count, where, when) | Picture |
| --- | --- | --- |
| Cover | Sunny morning, on the ship's deck: Granny Seagull and Grampy Seagull (2) hug Seagull (1) between them, all three smiling; Kathryn (1) and Pancake Polly (1) beam behind them; the little yellow seaplane floats on the water behind the rail. 5 characters | new |
| 1 | Sunny morning, on deck by the rail, harbour behind: Pancake Polly (1) holds up the opened letter and the photo, calling out happily; her net and an open dripping glass bottle lie at her feet; Seagull (1) swoops down towards her. 2 characters | new |
| 2 | Morning, on deck, close up: Seagull (1) sits on a barrel holding the photo of Granny and Grampy in her wings, head tilted, a small unsure smile. Only Seagull (the photo shows two tiny gull faces, not characters) | new |
| 3 | Morning, on deck: Kathryn (1) and Pancake Polly (1) dance round in a circle, holding Seagull (1) in a hug between them, all three laughing. 3 characters | new |
| 4 | Morning, a few days later, on deck at the rail: Granny Seagull and Grampy Seagull (2) hug Seagull (1), who snuggles in between them, eyes closed with joy; Granny's handbag on her wing; the yellow seaplane floats just beyond the rail. 3 characters | new |
| 5 | Late morning, on deck at the small round table with two breakfast bowls: Seagull (1) sits pressed right up between Granny and Grampy Seagull (2); Grampy tickles her under the wing with a wingtip and she giggles; Granny smiles. 3 characters | new |
| 6 | Afternoon, on deck: Seagull (1) zooms in a little loop around Grampy Seagull (1) like a bee, wings a blur, tapping his tummy with a wingtip; Grampy flings his wings up in pretend alarm and giggles, eyes squeezed shut. Only these two. No fire, smoke or red glow. Each gull has exactly two wings and two feet. (User, 2026-11-17: "in 6 there seems to be an extra wing right behind the seagal"; the first page 6 had a third wing reaching to Grampy's vest and is replaced.) | redraw |
| 7 | Afternoon, on deck: Granny Seagull (1), in a red toy firefighter's helmet, holds the deck hose with both wings, a gentle sparkly arc of cool water sprinkling over Grampy Seagull (1), who laughs with his eyes closed; Seagull (1) claps her wings beside them. 3 characters. No fire anywhere | new |
| 8 | Late afternoon, on deck in the shade of the sail: a big lump under the patchwork blanket, only Grampy Seagull's (1) orange feet and the tip of his flat cap peeking out, wiggling; Seagull (1) beside it holds one wing up to the side of her head like a phone, beak open, calling. 2 characters. No phone or shell in the picture | new |
| 9 | Late afternoon, same spot: Granny Seagull (1), in a blue toy police cap, whisks the blanket off; Grampy Seagull (1) sits up laughing; Seagull (1) cheers with her wings up. 3 characters | new |
| 10 | Sunset, on deck at the small round table: Granny Seagull (1) sets down the berry cake; Seagull (1) bites a slice, eyes shut with delight; Pancake Polly (1) tastes a forkful of her own slice, eyebrows up in happy surprise. 3 characters. No stove, oven, flame or steam | new |
| 11 | Dusk, another day, at the rail with the first star out: Seagull (1) sits with her head drooping; Granny and Grampy Seagull (2) sit on each side with a wing gently round her, soft wistful faces. 3 characters. Nobody crying | new |
| 12 | Same evening, on deck: Kathryn (1) kneels and hugs Seagull (1) gently, talking to her with a warm smile, her phone (screen dark) in her other hand; Pancake Polly (1) hugs them from the side; Seagull's face brightens. 3 characters | new |
| 13 | Next morning, the seaplane on calm water beside the ship: Granny and Grampy Seagull (2) stand in the plane's open round door and kiss Seagull (1) on each cheek as she hovers there; Kathryn and Pancake Polly (2) wave from the ship's rail above. 5 characters. No pilot | new |
| 14 | Morning, at the ship's rail, seen from behind and to the side: Kathryn (1) and Pancake Polly (1) wave, Seagull (1) on Kathryn's hat waves a wing; far out over the sea the little yellow plane is small in the sky, flying away. 3 characters. Nobody visible in the plane, no pilot | new |
| 15 | Evening, on deck, soft pink sky: Kathryn (1) holds up her phone, smiling; Seagull (1) on her hat blows a kiss from her wing towards it; the screen shows all four grandparents squeezed together in front of the white cottage on Seagull Island, Granny and Grampy Seagull with Grandma and Grandpa, all waving and smiling. 2 characters, plus the 4 on the screen | new |
| 16 | Night, on deck under stars and a crescent moon: Kathryn (1) sits on a barrel by the rail, eyes half closed, smiling; Seagull (1) is snuggled down on top of her hat, eyes closed; Pancake Polly (1) sits close beside Kathryn, smiling, one arm round her. On a second barrel beside them stand **two framed pictures**: one of Granny and Grampy Seagull, one of Grandma and Grandpa (outfits as the line-up, no text). Exactly 3 characters on deck plus the 4 grandparents only inside the two frames. No lamps with flames. (User, 2026-11-17: "16 should be redrawn ... there should be 2 images of the grand parents. also seagal should be with katryn and panckage polly, not just the 2 of them"; the first page 16 is replaced.) | redraw |
| Back cover | Picnic style (`prompts.md`), a sunny epilogue on Seagull Island: at the very bottom edge Seagull (1) sits on a red-and-white checked blanket on the grassy cliff top, between Granny Seagull, Grampy Seagull, Grandma and Grandpa (4), sharing the berry cake; all laughing. Whole bodies inside the bottom fifth; the middle and top are plain soft pale blue sky with one or two faint clouds, open for the credit lines. 5 characters, no words. It shows the "see them all again" of the last page | new |

Check (dino-storytelling section 4):

- [x] Every page's picture differs from its neighbours (place, action or who is there).
- [x] No page needs two pictures.
- [x] Every character, whole (feet, tails and held things included), and the action fit in the top 68% of a 3:4 portrait picture (the prompt asks for 32% free at the bottom; the picture check passes at 27%).
- [x] Each character's look is fixed in the line-up, not invented per page.

Notes for the illustrator:

- Draw sheets for Granny Seagull, Grampy Seagull, Grandma and Grandpa; reuse book 1's `x-marks-the-treasure/characters/lineup.jpg` for
  Kathryn, Seagull and Pancake Polly and make a new line-up of all seven at true relative size. Five gulls look alike: the outfits (lilac
  shawl and flower; flat cap and green waistcoat; blue cardigan and pearls; glasses and red bow tie; Seagull's striped neckerchief) and the
  sizes tell them apart.
- One place sheet: the deck with the harbour behind (every page except the screen and the back cover).
- Pages 8 and 9 are one place in a row: same sail shade, same blanket, same barrels. Pages 11 and 12 are the same evening: tell them apart by
  who is with Seagull. Pages 13 and 14 are the same morning: the plane on the water at its door, then small in the sky.
- Pages 6-9 are pretend play: everyone's face is laughing or playful. Ask for "a gentle arc of cool water", never "spraying", and for "no
  fire, flames, smoke, sparks or red glow" on pages 6 and 7.
- Page 15: the screen shows four faces together in one picture, no split boxes, buttons, icons, clock or text; ask for "the phone screen
  shows only a picture of ...".
- Ask in every prompt for "no weapons, no swords, no cannons, no skulls or crossbones, no eyepatch, no fire or flames", "no other birds",
  "no pilot" (pages with the plane) and "no text, letters, words, sound effects or writing anywhere".

## Check results

| Page | Attempts | Size | Why earlier attempts failed |
| --- | --- | --- | --- |
| cover | 1 | 3442757 | rejected: webbed feet crossed the 73% line |
| cover | 2 | 3317617 | replaced after full review 1 (lfsi\review-1.md); was accepted: 5 characters, outfits right, flag seagull shape only, yellow seaplane empty, bottom third deck |
| 1 | 1 | 3482204 | accepted: Polly + Seagull whole, outfits right, letter/photo no writing, net+bottle at feet, 2 wings 2 feet, line70 clear |
| 2 | 1 | 2908173 | accepted: Seagull alone on barrel with photo (two tiny gull faces), 2 wings 2 feet, neckerchief, line70 clear |
| 3 | 1 | 3400260 | rejected: Kathryn's boots and Polly's shoe crossed the 73% line |
| 3 | 2 | 3460773 | rejected: line70 ok, action wrong (Seagull hangs between held hands, no hug), odd stool |
| 3 | 3 | 3423726 | replaced after full review 1 (lfsi\review-1.md); was accepted: 3 characters, group hug laughing, 2 wings 2 feet, outfits right, line70 clear; small stool-like shape behind Polly is minor |
| 4 | 1 | 3393916 | accepted: Granny (shawl, flower, handbag), Grampy (cap, waistcoat), Seagull hug, yellow plane empty, no pilot; 2 wings 2 feet each on a zoomed crop; line70 clear |
| 5 | 1 | 3407301 | accepted: 3 gulls, outfits right, Seagull between Granny and Grampy, line70 pass |
| 6 | 1 | 3388604 | replaced (user, 2026-11-17): first page 6 had a third wing; redrawn in g3 |
| 6 | g3 try 1 | 3410789 | rejected: Grampy's feet reach about 75%, below line70 |
| 6 | g3 try 2 | 3427067 | rejected: Seagull with three wings again |
| 6 | g3 try 3 | 3421460 | replaced after full review 1 (lfsi\review-1.md); was accepted: Seagull exactly 2 wings 2 feet, Grampy 2 wings 2 feet, all above line70; small stool under Grampy (harmless) |
| 7 | 1 | 3367877 | accepted: 3 gulls, red toy helmet, green hose, cool water arc, no fire, line70 pass |
| 8 | 1 | 3312642 | replaced after full review 1 (lfsi\review-1.md); was accepted: blanket lump, cap and orange feet peek out, Seagull wing at head, no phone/shell, line70 pass |
| 9 | 1 | 3330411 | replaced after full review 1 (lfsi\review-1.md); was accepted: 3 gulls, blue toy police cap, same pastel blanket, Grampy laughing, line70 pass |
| 10 | 1 | 3424429 | accepted: finished cake only, no stove/flame/steam, sunset light; table leg tip just over line70, no character below |
| 11 | 1 | 3074738 | accepted: Seagull between Granny and Grampy, wings round her, dusk star, 2 wings 2 feet each, no tears |
| 12 | 1 | 3392958 | accepted: Kathryn kneels hugging Seagull, phone dark, Polly hugs from side; Seagull's feet tips at line70, not over |
| 13 | 1 | 3154794 | replaced after full review 1 (lfsi\review-1.md); was accepted: Granny and Grampy in the open plane door kiss Seagull, Kathryn and Polly wave from rail, 5 characters, no pilot |
| 14 | 1 | - | rejected: Polly's shoes just below the 73% line |
| 14 | 2 | 3190216 | accepted: 3 characters, outfits ok, empty plane, all above line |
| 15 | 1 | - | rejected: second floating phone-screen frame plus island in background; Kathryn's phone showed no call |
| 15 | 2 | - | rejected: phone screen faced away; boots below 73% line |
| 15 | 3 | 3326311 | replaced after full review 1 (lfsi\review-1.md); was accepted: Kathryn + Seagull, one phone showing the 4 grandparents before the cottage; no text |
| 16 | g3 try 1 | 3386080 | rejected: Kathryn's boots and barrels below line70 (about 77%) |
| 16 | g3 try 2 | 3382750 | accepted (redraw): night, Kathryn on barrel, Seagull asleep on hat, Polly beside, exactly 3 on deck; second barrel with TWO frames; all above line70 |
| 16 | g4 try 1 | 3428947 | replaced (user, 2026-11-17): first page 16 had one photo and no Polly; redrawn in g3 |
| back cover | 1 | - | rejected: six gulls (Grandpa drawn twice) |
| back cover | 2 | 2457106 | accepted: exactly 5 gulls as in the book, picnic on cliff, middle open, no words |
| r1/r3/r4 redraws | after full review 1 (lfsi\review-1.md) | - | rows below copied from lfsi\r1, r3, r4 checks.md; pages 3, 6, 8, 9, 13, 15 and cover in them are the current pictures |
| cover | 1 | 3381783 | accepted: Granny, Grampy, Seagull hug, 3 gulls 2 grey black-tipped wings visible each side, 2 feet each, feet above 73% line; Kathryn + Polly whole behind; yellow high-wing seaplane with propeller and 2 floats in water; 5 characters |
| 13 | 1 | 3057440 | accepted: yellow high-wing seaplane with nose propeller and floats; Granny, Grampy, Seagull kiss in open door, 2 feet each, no extra wings; Kathryn + Polly whole at rail; no pilot; only plane floats cross the 73% line (not characters) |
| 15 | - | - | not drawn in r1 (Gemini limit until 03:05 on 2026-11-18); drawn and accepted in r3 below. |
| 15 | r3 try 1 | 3441181 | rejected: boots below line70 (~77%), phone held facing herself so screen not visible, boots drawn blue |
| 15 | r3 try 2 | 3399741 | rejected: phone and screen good, but boots end at ~75% (below line70); open hatch on deck |
| 15 | r3 try 3 | 3427938 | accepted: Kathryn whole to boots (boots end ~68%, line70 clear), normal hand-held phone screen to viewer showing 4 gulls in front of cottage, no text; zoomed crop of original: Seagull 2 wings 2 feet, Kathryn 2 arms 2 legs 2 boots |
| 3 | r3 try 1 | 3467206 | accepted: Kathryn + Polly + Seagull cuddled between their hands, laughing; zoomed crop of original: Seagull one wing visible folded at her side (other behind her body, no third shape), 2 feet, tail down; Kathryn 2 arms 2 boots, Polly 2 shoes; hatch closed; all above line70 (lowest shoe ~71%) |
| 6 | r3 try 1 | 3446901 | rejected: on zoomed crop Seagull shows THREE wing shapes (big raised wing, small extra wing tip low on her left, tapping wing); Grampy fine, feet tucked, line70 ok |
| 6 | r3 try 2 | 3370799 | accepted: zoomed crops of original: Seagull exactly 2 wings (one raised, one stretched toward Grampy) + tail, 2 feet (dangling below, not touching him), Grampy 2 wings + tail, 2 feet; no foot on his tummy; weak point: Seagull's tapping wingtip lands near Grampy's raised wing/shoulder, not clearly his tummy; all above line70 (lowest foot ~71%) |
| 9 | 1 | rejected: zoomed crop: Granny had THREE wings (grey one up holding blanket, grey one across her shawl, a white one out to the left); 3296846 bytes | pro |
| 9 | 2 | rejected: Granny exactly 2 wings, good scene, but Grampy's right foot tip goes to about 74%, below line70; 3331389 bytes | pro |
| 9 | 3 | replaced after full review 2 (open deck hatch beside Seagull and Granny, a danger the text doesn't have); was accepted: zoomed crop of the original: Granny exactly 2 grey black-tipped wings both raised holding the blanket's top edge, 2 feet; Seagull 2 wings 2 feet; Grampy standing, 1 wing visible + 1 behind, 2 feet; one square sail, crow's nest, flag; blue toy police cap over flower; all feet above line70 (about 66%); 3358224 bytes. Note: Grampy stands rather than sits | pro |
| 8 | 1 | accepted: zoomed crop of the original: Seagull exactly 2 wings (one raised to her head like a phone, one folded at her side) and 2 feet, no phone/shell; Grampy only a lump: brown cap and 2 orange feet peek out (a bit of green waistcoat shows by the cap); same pastel blanket as page 9; ONE square sail, crow's nest, flag; everything above line70 (lowest feet and blanket edge about 71%, under the 73% line); 3365674 bytes | pro |
| 9 | r5 1 | rejected: dark hatch opening with steps still visible behind Seagull/Granny's feet at left; Grampy sits with a foot touching the 73% line; 3351186 bytes | pro |
| 9 | r5 2 | accepted (r5): zoomed crops of the original: no hatch or opening anywhere, solid planks; Granny exactly 2 grey black-tipped wings (one raised holding the blanket top, one across her body at the blanket edge) + white tail, 2 feet, blue toy police cap over flower; Seagull 2 wings 2 feet, cheering; Grampy standing, 1 grey wing in front, tail at right (other wing behind), 2 feet, laughing; same pastel blanket; one square sail, crow's nest, flag; all feet about 70%, above line70; 3358109 bytes (untouched, 1792x2400) | pro |
