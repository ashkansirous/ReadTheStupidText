# Freddy and the Slipper: art record

Store book (not a starter), by Ashkan Sirous, for about age 4. Source: the user's phone scope doc of 2026-11-01 ("Approved
English text", 12 pages). Freddy the fox won't eat his dinner, his talking Tummy keeps him awake, he sneaks next door hoping
for a leftover kebab, chomps a smelly brown slipper instead, eats cold potatoes, and the next night eats all his stew. The
book teaches four shoe words (slipper, sandal, clog, flip-flop).

## Changes to the approved text (each one forced by a rule)

Page numbers in this list are the approved text's (12 pages); the book now has 14.

- **Page 4 split into pages 4 and 5.** One action per page: Mummy's goodnight with the light on, then Tummy rumbling in the
  dark, are two moments one picture can't show. No word changed.
- **Page 7 split into pages 8 and 9** after "...with rumbly cries." (text review): Freddy creeping up to the grill, then
  jumping for joy at the brown thing, are two moments. No word changed; every piece and translation is the same.
- **Speaker attribution** (checked with `SpeakerAttributor` and `StoryPieces.Of` on this text; every quote now goes to the
  right speaker). The attributor reads only a bare capitalised name next to a speech verb in its list, so:
  - p2 "his mummy called" -> "his **M**ummy called" (lower-case "mummy" is no name; the quote went to the narrator).
  - p3 `It's chicken and peas!" "No room in my tummy!"` -> `It's chicken and peas!" **Said Freddy,** "No room in my tummy!"`
    (the untagged reply went to Mummy).
  - p3 "his tummy said" -> "**Tummy said**"; p6 "cheered his tummy" -> "cheered **Tummy**"; p8 "cried his tummy" -> "cried
    **Tummy**"; p12 (now 13) "sang his tummy" -> "sang **Tummy**" (lower-case "his tummy" is no name; each went to Freddy).
  - p4 "yawned Freddy" -> "**said** Freddy" ("yawned" is not a speech verb; the line went to Mummy).
  - p4 `came a RUMBLE-GRUMBLE! "Freddy! I want to EAT!"` -> `came a RUMBLE-GRUMBLE! **Tummy cried,** "Freddy! I want to eat!"`
    (untagged, it went to Freddy).
  - p8 `"Ewww! Yuck!" Freddy spat.` -> `"Ewww! Yuck!" **yelled Freddy, and spat it out.**` ("spat" is not a speech verb; the
    line went to Tummy).
  - p9 `"A clog?" asked his tummy. "A flip-flop?" "Oh, how should I know?` -> `"A clog?" asked Tummy. "A flip-flop?"
    **Said Freddy,** "Oh, how should I know?` (Tummy asks both shoe words, Freddy answers; same "Said Freddy," shape as page 5).
  - p11 "groaned Tummy" -> "**grumbled** Tummy" ("groaned" is not a speech verb; both of Tummy's lines went to Freddy).
  - Kept: p10 "moaned Tummy" ("moaned" is no speech verb either, but the turn-taking rule gives both lines to Tummy, checked).
- **Rule 1:** p7 (now 8) "He looked at the grill" -> "He looked at the **cold** grill": Freddy goes right up to it and takes
  something from under it, so the text says it is cold, as every picture of it is.
- **TTS:** "I want to EAT!" -> "I want to **eat**!". Kokoro read the capitals "EAT" about 0.2-0.45 s longer than "eat" on all
  three voices tried (am_santa, am_liam, af_heart), most likely letter by letter; every other capitals word (BLEURGH,
  RUMBLE-GRUMBLE, SO, NOT, EMPTY, YUM-YUM, KEBAB, YUCKY, NO, CHOMP) was the same length as its lower-case spelling, and
  "BLEURGH!" (0.49-0.63 s) is read as one word, not spelled (letters take 1.4-1.9 s). Measured, not listened to.
- **No capitals left (text review):** EMPTY, RUMBLE-GRUMBLE (twice), KEBAB, CHOMP, BLEURGH, NOT, YUCKY, NO, SO and YUM-YUM are
  now lower case ("Kebab!", "Bleurgh!", "Rumble-grumble," and "Yum-yum!" keep a capital where they start a sentence); "gave
  it a CHOMP." became "gave it a chomp!" to keep the punch. Only Kokoro was measured, not Supertonic (English before the
  storyteller pack lands), so no capitals is the safe text. A speak table could not do it: `speak:<lang>` tables are read
  only for a translated read (`StoryVoicing.VoiceAsync` -> `StoryTranslator.TranslateAsync(forSpeech: true)` ->
  `BundledTranslations.ComposeSpoken`); an English book read in English goes through `VoicedInOwnLanguage`, which uses the
  text as written. Same pieces, same order, same speakers (checked); the translations are unchanged.

## House style

Children's picture book illustration, soft watercolour and coloured pencil, warm gentle colours, simple rounded shapes, no
text or letters anywhere.

## Characters (canonical until a line-up is accepted; then rewrite from the line-up)

- **Freddy:** a young red fox standing on his hind legs, the smaller fox: his head reaches Mummy's shoulder. **Bright
  orange-red fur**, a white chest and a white tip on his big fluffy tail, black paws like socks, round friendly eyes, a
  fluffy tuft on top of his head. His detail: **a blue-and-white striped T-shirt** (no trousers, no shoes), worn on every
  page, in bed too. No prop: his paws stay free to hold his tummy. Mouth closed or smiling, no teeth showing.
- **Mummy:** a grown-up fox, the taller fox (a head and a half taller than Freddy). **Darker russet-brown fur** with a cream
  chest, long eyelashes, no tuft. Her detail: **a lilac dress under a yellow apron with white polka dots**. Her prop: **a
  wooden spoon**. She must never look like Freddy: taller, darker fur, dress and apron, no stripes.
- **Tummy:** Freddy's own tummy. **Never drawn as a separate creature**: no face, eyes or mouth on Freddy's belly, no little
  creature, no speech bubble, no sound-effect lettering, no rumble lines. The only hint is a paw on Freddy's tummy where it fits
  the action; it is not needed every time Tummy talks.
- **The slipper** (cover and pages 9-12): **one brown slipper**, a worn brown felt house slipper with a closed toe and a soft
  heel, a bit scuffed, no laces, no face. **Its size is fixed: about as long as Freddy's forearm** (elbow to paw), on every
  page and the cover, never bigger or smaller. Never a pair, never another kind of shoe.

There are no other characters: the neighbours never appear (they are asleep), no other animals, no live chicken or hen
anywhere (chicken is only a word for dinner).

## Places (the same in every picture)

- **Freddy's garden** (page 1): a sunny lawn with one tall tree on the left and a small cardboard box on the grass.
- **The kitchen** (pages 2, 3, 6, 7, 13, 14): a cosy kitchen, a round wooden table with a blue-and-white checked cloth,
  cream cupboards, a white fridge, a pot with a lid on the worktop, **one wide window with a single pane** looking out on the
  neighbours' garden. **No lit stove, no flame, no steam, no kettle**; food in bowls is shown without steam. **The plate of
  potatoes:** a white plate of four plain boiled potatoes; it sits on a fridge shelf on page 6 and on the table on page 13.
- **Freddy's bedroom** (pages 4, 5): a small bed with a white sheet, an electric bedside lamp (no candle), one wide window with
  a single pane and the moon outside.
- **The neighbours' garden** (pages 8-12): moonlit grass, a low wooden fence between the two gardens, **a small round grey
  barbecue grill on three legs, cold: no fire, no embers, no glow, no smoke**, its lid open; a garden shed behind.
- Time: late afternoon (page 1), evening (pages 2-4), night (pages 5-13, moonlight; the kitchen lit by the open fridge on
  page 6 and a ceiling lamp on page 13), the next evening (page 14).

## Picture plan

Every page: the whole scene sits in the top 70% of the picture; **the bottom 30% is plain floor or grass with nothing
important in it**. No text, letters, words, sound effects or writing anywhere. Freddy is the same on every page (striped
T-shirt, bright orange-red fur). Nobody is hurt; Freddy's yuck faces are funny, never pained.

| Page | One action (who, count, where, when) | Picture |
| --- | --- | --- |
| Cover | Freddy (1), late afternoon on the grass, holds the brown slipper (forearm-long) away from him by its heel between two claws, nose wrinkled, one paw on his tummy, funny face. Nobody else | new |
| 1 | Late afternoon, Freddy's garden: Freddy (1) stands proudly showing off his fluffy tail, the tall tree on one side and the small box on the other. Nobody else | new |
| 2 | Evening, the kitchen: Freddy (1) sits at the table nibbling a carrot (a cucumber slice and a bread crust on his plate), his bowl of stew untouched; Mummy (1) stands by the table with her wooden spoon, calling him | new |
| 3 | Evening, the kitchen: Mummy (1) holds out a spoonful of stew; Freddy (1) shakes his head, both paws on his tummy | new |
| 4 | Evening, the bedroom: Mummy (1) at the open door waves goodnight; Freddy (1) in bed under the white sheet reaches to switch off the bedside lamp | new |
| 5 | Night, the dark bedroom, moonlight through the window: Freddy (1) sits up in bed, one paw on his tummy, the other finger to his lips (shh). Nobody else | new |
| 6 | Night, the kitchen: Freddy (1) peeks into the open fridge (its light on him); on a fridge shelf, the white plate of plain boiled potatoes; behind him cupboard doors stand open and the pot on the worktop has its lid off, **empty: its inside visible, no stew in it**. Nobody else | new |
| 7 | Night, the kitchen: Freddy (1) at the window points out at the neighbours' garden (fence and cold grill small outside), whispering, one paw on his tummy. Nobody else | new |
| 8 | Night, the neighbours' garden: Freddy (1) tiptoes from the fence towards the cold grill, one paw on his tummy, eyes on the grill; the fence just behind him. Nobody else | new |
| 9 | Night, the neighbours' garden: Freddy (1) jumps for joy beside the cold grill, pointing at a small brown thing on the grass under it. Nobody else | new |
| 10 | Night, the neighbours' garden: Freddy (1) spits the brown slipper out, eyes squeezed shut, tongue out in a funny "yuck" face; the slipper flies a little way out in front of him; the cold grill behind. Nobody else | new |
| 11 | Night, the neighbours' garden: Freddy (1) holds the brown slipper up at arm's length by its heel and stares at it, puzzled; **the slipper is clearly a brown house slipper, about as long as his forearm**. Nobody else | new |
| 12 | Night, the neighbours' garden: Freddy (1) sits on the grass by the fence, hungry and grumpy, both paws on his tummy; the slipper lies on the grass beside him. Nobody else | new |
| 13 | Night, the kitchen under the ceiling lamp: Freddy (1) sits alone at the table eating cold plain boiled potatoes from the white plate (the one on the fridge shelf on page 6), frowning. Nobody else | new |
| 14 | The next evening, the kitchen: Freddy (1) at the table holds up his empty stew bowl for more, smiling big; Mummy (1) beside him smiles, holding the pot and her wooden spoon. Calm, happy ending | new |

Check (dino-storytelling section 4):

- [x] Every page's picture differs from its neighbours (place, action or who is there).
- [x] No page needs two pictures.
- [x] Characters' faces and the action fit in the top 70% of a 3:4 portrait picture.
- [x] Each character's look is fixed in the line-up, not invented per page.

The last item is ticked for the plan: every look is fixed in the character lines above, and the illustrator draws one line-up
(Freddy, Mummy and the slipper) from them before any page.

Notes for the illustrator:

- Every page with the grill (7-12) says: "the grill is cold and unlit: no fire, no embers, no glow, no smoke".
- Pages 3, 5, 7, 8, 10 and 12 are Tummy's moments: a paw on Freddy's tummy where it fits the action (as each page's plan says), never a face or creature on it.
- Pages 8-12 share the neighbours' garden; tell them apart by the action (tiptoe, jump, spit, hold up, sit).
- Every prompt with the slipper says "one brown felt house slipper, about as long as Freddy's forearm".
- Mummy and Freddy side by side (pages 2, 3, 4, 14): spell out both looks; Mummy taller, darker, in her apron.
- Gemini refuses one-character scenes more often (see the skill); if a Freddy-alone page is refused, the fence, shed and moon
  carry the scene, never an extra character.

## Animals (Slice 152, Decision 157)

`AnimalKind` has no fox, so Freddy and Mummy are stored as **no animal** (no animal sound) and say what they are through
`being: Fox` in the cast, as the Gingerbread Man's Fox does. Tummy is **no animal**. All three are listed in `notAnimals`
in `book.json` so the "none" is explicit. Narrator: the app's narrator (no cast entry).

## Voices (Slice 156, the `cast` in `book.json`)

`dump-voices` cast this book mechanically (Freddy `knight`, Mummy `princess`, Tummy `mummy`); none fit, so all three were
chosen by hand (English roster, `CharacterRosterEnglish.cs`).

- **Freddy** (male, child; bouncy, bright; being Fox): `puppy`, Playful Puppy, am_liam with the Bouncy flavour (+4 semitones,
  quicker), "bouncy and bright". The young, bouncy kid voice the user asked for.
- **Mummy** (female, adult; warm, soft; being Fox): `mummy`, Kind Mummy, af_sarah, "warm and kind".
- **Tummy** (male, adult; gruff, booming): `dragon`, Rumbly Dragon, am_santa with the Big flavour (-3 semitones, slower),
  "rumbly and loud". A deep rumbly voice coming out of a small fox is the joke, and it is nothing like Freddy's (different
  engine voice, pitched down where his is pitched up). No roster flavour wobbles (only the sheep and goat bleats do), so
  "wobbly" is not available; the grumble comes from the low pitch. **Kept by the user for now (2026-11-01)**; a `voice-pick`
  listening can still change it later if the user wants one.
  `being: Dragon` is a casting kind only, not a claim: `CharacterBeing` has no "none", and a test requires a person or
  fantasy kind for every non-animal; Dragon keeps the fallback in languages without the `dragon` id on a big rumbly voice.
  The alternatives to play at the listening: `troll` (Grumpy Troll, am_michael, gruff and low), `giant` (Friendly Giant,
  am_santa, big and slow).

## Translations

All 13 languages, written for this text (108 pieces). No existing book rhymes, so the translations follow the store books'
practice: meaning first, spoken kid register (Decision 152); a rhyme is kept only where it fell out naturally (Dutch page 1).
The shoe words are real shoe words in each language (for example de Hausschuh/Sandale/Holzschuh/Flipflop, nl
pantoffel/sandaal/klomp/slipper, fr pantoufle/sandale/sabot/tong, fa دمپایی روفرشی/صندل/کفش چوبی/دمپایی لاانگشتی, so slipper and flip-flop differ; the Farsi
title is «فردی و دمپایی روفرشی»). Tummy is a
kid word for tummy used as a name (de das Bäuchlein, es Barriguita, fr Bidou, nl Buikje, pt a Barriguinha, ru Животик, fi
Masu, sv/nb Magen, da Maven, ja おなか, zh 肚子, fa and ar "his tummy", شکمش / بطنه). Sound words are repeated triples or
hyphenated runs in each language's own sounds (fa قار و قور, ru урч-урч-урч, es ruge-ruge-ruge). No shouting capitals
anywhere (English included): capitals were read letter by letter once in English (EAT), and no other voice was measured, so
lower case is the safe choice; all sound words are untested by ear (skill section 3) and flagged for a
listen. The Farsi vowel-mark review is a separate later step and has not been run. Speech-tag pieces that sit mid-sentence
after a quote ("sagte Freddy,", "dijo Barriguita.") start lower case, as in the other books.

## Decided by the user (2026-11-01: "keep the 3, it is ok for now")

Kept for now, not open:

- **Tummy's voice against "Tummy squeaked"** (page 5): Tummy keeps the booming `dragon` voice and "Tummy squeaked" stays.
- **Freddy sneaks out alone at night** (pages 6-12), over the fence into the neighbours' garden, and Mummy never knows: stays.
- **Two awkward lines** stay as approved: "Er... not." (page 6) and "What kebab is this meat?" (page 10).

## Still open

- The Farsi pronunciation review (`farsi-review-book`, through the `farsi-review` agent), with the vowel marks.
- A `voice-pick` listening for Tummy, only if the user wants one later.
- A listen on a phone before shipping (rhythm, the sound words, the four voices).

## Check results

Drawn 2026-11-01 with 3.5 Flash-Lite, 3:4, one character chat (Freddy sheet, Mummy sheet, line-up; saved in `characters/`) and page chats of at most 3 pictures, each started by pasting `characters/lineup.jpg`. First picture about 14:15 (usage window runs from then). The Mummy sheet's three small expression heads came out with orange fur and a tuft (the full-body views are right); the line-up, which the pages follow, has her russet fur and no tuft. Freddy's paws and legs are dark brown, not black, in the line-up. Chrome's own "Download full size image" click stopped writing a file after the first picture, so pictures were saved by an anchor download of the same blob, taken with `gemini-take-download.ps1 -Bytes`. Gemini drew the chat's earlier scene again on the page 12 retry inside the same chat (and the page 13 picture there had two Freddies), so those went to fresh chats.

| Page | Attempts | Size | Why earlier attempts failed |
| --- | --- | --- | --- |
| Cover | 1 | 1792x2400 | |
| 1 | 1 | 1792x2400 | |
| 2 | 4 | 1792x2400 | Attempt 1: starry night window for "One evening". Attempt 2: lilac sky but faint stars and a white seam above the bottom band. Attempt 3 (review): plain lilac dusk but the round table was bare wood, not the checked cloth. Attempt 4 accepted: checked cloth, lilac dusk without stars, blank-label jars, soft paper edge. |
| 3 | 1 | 1792x2400 | |
| 4 | 1 | 1792x2400 | |
| 5 | 1 | 1792x2400 | |
| 6 | 2 | 1792x2400 | Attempt 1: night scene stopped at 69% on a white seam above a band of daylight wooden floor; stool. Redo accepted: dark floor fades to plain paper; plate holds about five potatoes, not four. |
| 7 | 5 | 896x1200 | Attempt 1: white seam and daylight floor band, pot on the table. Attempt 2: seam fixed but the kitchen walls looked like bright daylight. Attempt 3 (review): dim night kitchen but a hard straight seam at about 75% cut the table legs off flat. Attempt 4: smooth fade (p6 pasted as reference) but Freddy stood on the counter and the table was gone. Attempt 5 accepted: Freddy on the floor beside the checked-cloth table whose legs fade into paper, empty pot with lid off on the worktop, cold grill and fence outside, paw on tummy. Gemini's full-size download failed twice for this one, so the saved file is its 896x1200 version. |
| 8 | 3 | 1792x2400 | Attempt 1: faint haze above the grate and a yellow glow on the grass under the grill (breaks rule 1). Attempt 2: postcard inset with white margins and a different garden (review). Redo accepted: full-width, the garden of pages 10-11 (p10 pasted as setting reference), cold clean grill. |
| 9 | 4 | 1792x2400 | Attempt 1: Freddy pointed at the sky. Attempt 2: postcard inset, different garden (review). Redo 1: full-width, right garden, but his feet stayed on the ground. Redo 2 accepted: mid-air jump, pointing down at the slipper by the grill. The first send of redo 2 gave an empty answer and was sent again. |
| 10 | 1 | 1792x2400 | |
| 11 | 2 | 1792x2400 | Attempt 1: slipper held against his face and seen edge-on, not clear. |
| 12 | 4 | 1792x2400 | Attempts 1 and 2: red-orange ember glow under the grate (breaks rule 1; the retry in the same chat drew the same scene). Attempt 3: clean grill but a postcard inset with white margins and a different garden (review). Redo accepted: full-width in the garden of pages 10-11, grumpy, both paws on his tummy, no tears. |
| 13 | 2 | 1792x2400 | Attempt 1: two Freddies at the table. A small kettle shows on the counter in the accepted one (cold, no steam). |
| 14 | 4 | 1792x2400 | Attempt 1: dark starry night in the window for "the next evening". Attempt 2: orange jar on the left worktop with a letter-like scribble (review). Redo 1: plain jars but Freddy cheered with the bowl on the table. Redo 2 accepted: dusk window, jars with blank labels, Freddy holds the empty bowl up, Mummy holds the pot and spoon. |
