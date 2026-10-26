# The Tortoise and the Hare: art record

Bundled starter (`StarterTexts.Tortoise`, stem `tortoise-and-hare`, version 4 in `StarterBuilder.StarterVersions`), also a
store book under the same id (`StoreBuilder.StarterEntries`). Retold in Slice 121 as 14 pages with one action each
(`dino-storytelling` skill); the earlier 5-page text shipped as versions 2 and 3, and its page hash
(`C0E318F0...C1405A`) is in `StarterUpdate.EarlierPageTextHashes`, so unedited installed copies update. **Art status
(2026-10-26): cover and pages 1-14 drawn and accepted; full review PASS 2026-10-26.**

**Starter, not store:** the pictures go to `tests/DinoReader.Tests/Starters/tortoise-and-hare/` (`pictures/1..14.jpg`,
`cover.jpg`, `cover-thumb.jpg`), not `Store/`. Rebuild with `DINO_BUILD_STARTERS=1` and put the other three starters' `.dino`
files back as they were. If the pictures land before version 4 ships (same PR), there is no second version bump.

**Gentle retelling (Decision 130):** Aesop's fable has no harm in it, so the work was on unkindness. In the old text the Hare
laughs at the Tortoise for being slow ("You walk so slowly ... the funniest thing I have ever heard") and at the end "hung his
head". Here the Hare only boasts about himself ("Look how fast I am!"), never mocks her, and is never made small: nobody
laughs at him when he loses, the Tortoise tells him he is still the fastest runner she knows, and he congratulates her and
says sorry for boasting on his own. The Tortoise tries to wake him when she passes ("Wake up, Hare! We're still racing!") and
her friends call too, so she wins fair and kind, not by sneaking past. The ending is calm: sunset under the oak, the lesson from Owl, a walk planned
for tomorrow, best friends.

**Why everyone does what they do:**
- **The Hare** loves running and is proud of being fast, so he tells everyone. He says yes to the race because he is sure he
  will win, and he naps because he is so far ahead it seems silly to hurry. When he wakes and sees the Tortoise near the top
  he runs as fast as he can, too late. He is out of breath but smiles, because the race was fun and she won it fairly; he
  says sorry because he sees that boasting was not nice. He uses her refrain at the end because he has learnt it from her.
- **The Tortoise** is slow and happy that way; her refrain is how she keeps going. She offers the race kindly, to show the
  Hare that slow can get there too, not to beat him. She calls to the sleeping Hare because waking a friend in a race is
  the fair thing to do; when he only rolls over, she walks on. She tells him he is still the fastest runner because it is
  true and she wants him to feel better.
- **Owl** is wise and old; he offers to be the judge so the race is fair, draws the start line, and flies ahead (owls can)
  to wait at the finish by the oak. He says the lesson at sunset because that is what wise old owls do.
- **Squirrel, Hedgehog and Frog** are the friends on the log watching the Hare run; a race is exciting, so they cheer at the
  start. They walk beside the Tortoise because she is the one they can keep up with, and cheering someone who is trying
  hard is fun; they are not against the Hare. They cheer the winner at the finish and stay for the sunset with everyone.

**Refrains (identical every time):** `"Slow and steady, step by step," said Tortoise.` on pages 2, 5, 8 and 10, and the same
words from the Hare on page 14 (`said Hare with a smile.`), one piece in every translation. `"Go, Tortoise, go!"` from Frog
(page 8) and Squirrel (page 10). `Zoom! Zoom! Zoom!` for the Hare running (pages 1, 5, 12). `Snore... snore... snore...` for
the nap (pages 7 and 9). The lesson, `"Slow and steady wins the race,"`, comes once, from Owl on page 14. The build-up has
three cheering steps (Frog by the pond, Squirrel on the hill, Hedgehog at the finish).

**Cast and voices:** six speakers, genders chosen deliberately, three of each: Hare (he, the boaster), Tortoise (she), Owl
(he), Squirrel (she), Hedgehog (he), Frog (she). Owl is a he because the Owl's roster character is male in English and Farsi
(an animal takes its own animal's voice before its gender). Hedgehog is a he and Frog a she so the translated names agree
with the nouns' grammatical gender (es el Erizo / la Rana, fr le Hérisson / la Grenouille, ru Ёж / Лягушка, ar القنفذ /
الضفدعة, pt o Ouriço / a Rã). The builder casts Hare M2, Tortoise F1, Squirrel F2, Owl M3, Frog F3, Hedgehog M4 (narrator M1); each
gender comes from the pronouns near the name: "She walked very slowly" (Tortoise), "and she jumped"/"she waved her bushy tail"
(Squirrel), "and he drew a line ... with his stick" (Owl), "and she did a happy hop" (Frog), "and he clapped his little
paws" (Hedgehog). Page 5 says "her three friends" rather than naming them, because "Frog walked beside her" gave the Frog the
Tortoise's "her". Reword those lines only with the test below running.
`StoreStoryTests.TheTortoiseAndTheHare_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster` checks the stored
genders and every language's roster. Owl and Tortoise get the slow "sleepy" voice flavour from their names; Squirrel the small
one; Frog says "Ribbit!" as an animal.

**Translation names (for the reviewer):** es and pt call the Hare **el Conejo / o Coelho** (rabbit), as many Latin American
and Brazilian tellings do ("La tortuga y el conejo"), because liebre/lebre are feminine nouns and he is a he; ja, zh and fa
use their one word for both (ウサギ, 兔子, خرگوش). pt also uses **o Corujão** (Owl, since coruja is feminine) and **a
Esquilinha** (Squirrel, since esquilo is masculine). ru uses **Филин** (masculine owl) and the proverb «Тише едешь — дальше
будешь» for the lesson; pt uses "Devagar e sempre". The refrain is rhythmic in each language (fa «یواش یواش، قدم به قدم»,
es "Despacito, despacito, paso a paso", fr "Doucement, doucement, pas à pas", ja 「ゆっくり、ゆっくり、一歩ずつ」).

**Style:** Children's picture book illustration, soft watercolour and coloured pencil, warm gentle colours, simple rounded
shapes, no text or letters anywhere. Portrait 3:4.

## Characters (canonical until a line-up is accepted; then rewrite from the line-up)

- **Hare:** a slim, long-legged brown hare, the tallest of the small animals (standing up he reaches Owl's head); very long
  ears with black tips, a white tummy and a white fluffy tail; his detail: **a red sweatband round his forehead, between his
  ears**. No other clothes. Big friendly eyes, mouth closed or smiling.
- **Tortoise:** a small, round tortoise with a high dome shell, the shell green-brown with a honeycomb pattern of
  yellow-edged plates, soft olive-green head and legs; about as tall as the Hare's knee; her detail: **a little yellow
  flower on top of her head**. No clothes. Gentle smile, half-closed calm eyes.
- **Owl:** a round tawny owl, brown and cream speckled feathers, big round amber eyes, two small ear tufts; a little taller
  than the Tortoise when standing; his detail: **small round gold spectacles**. He holds **a short brown stick** (his only
  prop). No clothes.
- **Squirrel:** a small red squirrel, orange-red fur, a cream tummy, tufted ears and a huge bushy tail curling over her back;
  her detail: **a blue ribbon bow on her tail**. No clothes.
- **Hedgehog:** a small round hedgehog, brown spines, a pale beige face and tummy, a shiny black button nose; about the
  Squirrel's size; his detail: **a green-and-white striped scarf**. Spines soft and rounded, never sharp. No other clothes.
- **Frog:** a small bright green frog, a pale yellow tummy, big round eyes on top of her head; the smallest character; her
  detail: **a pink flower on her head**. No clothes.

There are no other characters: no people, no other animals anywhere.

## Places (the same in every picture)

- **The meadow and the log:** a sunny green meadow with daisies and a fallen log at the edge of a wood; the sandy path starts
  here and winds away towards a round green hill. Pages 1-5 (and the cover).
- **The start line:** a line drawn across the sandy path by the log. Pages 4-5.
- **The path and the big tree:** halfway along, the path passes **a big leafy beech tree** (not an oak) with cool shade at its
  foot, where the Hare naps. Pages 6, 7, 9, 11.
- **The pond:** a small round pond with reeds and lily pads beside the path, and a bend after it. Page 8.
- **The hill and the old oak:** the round green hill with **one huge old oak tree** at the top; the finish line is a line in
  the grass under the oak. Pages 10, 12, 13, 14. Page 14 at sunset, pink and gold sky.

## Picture plan

Every page: the whole scene sits in the top 70% of the picture; the bottom 30% is plain grass or path with nothing important in
it. No text, letters, words, sound effects or writing anywhere ("Zoom", "Snore", "Ribbit" and the refrain are never drawn).
Every page has 3 or more characters (Gemini refuses some 1-2 character scenes). Every page is **new** (no pictures exist).

| Page | One action (who, count, where, when) | Picture |
| --- | --- | --- |
| Cover | Morning, the meadow path: the Hare (1) and the Tortoise (1) side by side at the start line, smiling; Owl (1) behind them with his stick raised; Squirrel, Hedgehog and Frog (3) small on the log behind | new |
| 1 | Morning, the meadow: the Hare (1) zooms round the meadow, ears flying, with speed lines; Squirrel, Hedgehog and Frog (3) sit on the log watching him | new |
| 2 | Morning, the meadow path: the Tortoise (1) walks slowly along the path, smiling; the Hare (1) stands proudly in front of her, chest out, one paw pointing to himself; the three (3) on the log behind | new |
| 3 | Morning, by the log: the Tortoise (1) and the Hare (1) face each other, agreeing to race; Squirrel (1) jumps up and down on the log; Hedgehog and Frog (2) beside her, delighted | new |
| 4 | Morning, the start line: Owl (1) draws a line across the path with his stick; the Hare and the Tortoise (2) stand side by side on it; Squirrel, Hedgehog and Frog (3) clap by the log | new |
| 5 | Morning, the start line: Owl (1), one wing raised high; the Hare (1) dashes off along the path; the Tortoise (1) takes her first step; Squirrel, Hedgehog and Frog (3) set off walking beside the Tortoise | new |
| 6 | Late morning, halfway up the path by the big beech tree: the Hare (1) stops and looks back down the path, hand over his eyes; far below, tiny, the Tortoise and her three friends (4) | new |
| 7 | Midday, the shade under the big beech: the Hare (1) lies asleep on his back, eyes shut, smiling; far down the path, small, the Tortoise and her three friends (4) coming | new |
| 8 | Midday, beside the pond: the Tortoise (1) walks past the pond; Frog (1) hops for joy in front of her; Squirrel and Hedgehog (2) walk beside her, cheering | new |
| 9 | Early afternoon, under the big beech: the Tortoise (1) stops beside the sleeping Hare (1) and calls to him, one foot raised; Squirrel, Hedgehog and Frog (3) just behind her call to him too, paws cupped round their mouths. The Hare is curled on his side, asleep and smiling | new |
| 10 | Afternoon, the hillside: the Tortoise (1) climbs the hill; Squirrel (1) waves her bushy tail beside her; Hedgehog and Frog (2) cheer; Owl (1) waits at the top by the finish line (a plain line in the grass) under the old oak, having flown on ahead | new |
| 11 | Afternoon, the meadow under the big beech: the Hare (1) sits up at the foot of the beech, paws up, mid-yawn, surprised not scared; far up the hill, small, the old oak with Owl (1) on the finish line under it and the Tortoise (1) a few steps below the line, walking uphill on all four legs (3 characters in total; the other friends are not drawn on this page) | new |
| 12 | Afternoon, the finish under the old oak: the Tortoise (1) steps over the finish line; Owl (1) raises a wing; Hedgehog (1) claps his paws; Squirrel and Frog (2) cheer; the Hare (1) dashes up the hill behind, a little way back | new |
| 13 | Afternoon, under the old oak: the Hare (1), out of breath and smiling, holds out his paw to the Tortoise (1), who smiles back; Owl, Squirrel, Hedgehog and Frog (4) stand round, smiling | new |
| 14 | Evening sunset, under the old oak on the hilltop: all six (6) sit together in a row, the Hare and the Tortoise side by side in the middle, watching a pink-and-gold sunset | new |

Check (dino-storytelling section 4):

- [x] Every page's picture differs from its neighbours (place, action or who is there).
- [x] No page needs two pictures.
- [x] Characters' faces and the action fit in the top 70% of a 3:4 portrait picture.
- [x] Each character's look is fixed in the line-up, not invented per page.

The last item is ticked for the plan: every look is fixed in the character lines above, and the illustrator draws one
line-up from them before any page.

Notes for the illustrator:

- Pages 6, 7, 9 and 11 share the big beech: 6, the Hare standing and looking back; 7, lying asleep with the others far
  away; 9, the Tortoise beside him calling, the friends close behind; 11, sitting up and yawning with everyone far up the hill.
  Keep the beech's shape the same, and keep it clearly not the oak (the oak is only ever on the hilltop).
- Pages with a "far away" group (6, 7, 11): name the small group in the prompt with its count so Gemini draws them, small
  but recognisable.
- Page 12 is one moment: the Tortoise's front foot over the line in the grass while the Hare is still a few strides behind.
  The finish line is a line in the grass, no ribbon, banner or flags with writing.
- The Hare sleeps peacefully, never looks ill or hurt; when he wakes he is surprised, not scared. Nobody laughs at him on
  pages 12-13: everyone smiles.
- The Hedgehog's spines are soft and rounded; nobody is near anything sharp. Owl's stick is short and held, never waved at
  anyone.
- Ask for "no text, letters, words, sound effects or writing anywhere".

## Check results

| Page | Attempts | Size | Why earlier attempts failed |
| --- | --- | --- | --- |

## Progress (2026-10-26, illustrator run 3)

All 14 pages and the cover accepted and prepared into `Starters/tortoise-and-hare/` (3.5 Flash-Lite, every picture 1792x2400, saved through the download route). Pages 8-10 one chat, 11 and 12-13 their own chats, 14 its own chat. Pages 8-14 attempts and sizes:

| Page | Attempts | Size | Why earlier attempts failed / notes |
| --- | --- | --- | --- |
| 8 | 1 | 1792x2400 | Pass. Painted scene has white paper bands top and bottom (soft edges), acceptable. Squirrel's bow sits at the ear, not on the tail. |
| 9 | 1 | 1792x2400 | Pass, five characters, beech shade. |
| 10 | 1 | 1792x2400 | Pass, five characters; Tortoise walks on her hind legs, shell looks a little like a cape; tree reads as the beech, not clearly the oak. |
| 11 | 5 | 1792x2400 | Four fails: seven tiny figures on the hill (an extra Frog or bird) in three drawings, and a blank white band at the bottom in two; two "I encountered an error" answers (Try again twice). Pass drew six characters but without the beech (a meadow, Hare in the open) and the Hare reaches into the bottom 30%. |
| 12 | 2 | 1792x2400 | First try: two Owls and an extra chipmunk (8 characters), Tortoise not on the line. Pass in a fresh chat after naming positions left to right and "only ONE Owl". |
| 13 | 1 | 1792x2400 | Pass, six characters. |
| 14 | 1 | 1792x2400 | Pass, six characters at sunset. |

Lesson: listing five tiny animals "in a row" makes Gemini add a sixth; split them into two groups left and right and say "only ONE of each, no other animals". Usage: no limit was hit (time of first picture not recorded).

## Redraw after the full review (2026-10-26, run 4)

Pages 10, 11 and 8 redrawn (3.5 Flash-Lite, 1792x2400). p10: old oak as on 12-14 with Owl, stick and a plain line, Tortoise on four legs, five characters, passed first try. p8: full-bleed pond scene with sky and grass, four characters, passed on the second try (the first try in the p10 chat repeated the oak hill; fresh chat fixed it). p11: Hare under the beech, mid-yawn with a round mouth, blue sky, old oak with the far group; six attempts over three chats. Attempts that drew a seventh figure (doubled Squirrel, a second Owl, an owl-tortoise hybrid) or a white band at the bottom were rejected. The kept picture still shows the Squirrel's tail and ribbon next to a second squirrel-like shape (it may read as seven tiny figures); it needs a look. Owl's stick on p5/13/14 not redone.


Run 4b: p11 redrawn with only Owl and Tortoise on the hill (the friends were dropped to stop Gemini drawing a duplicate Squirrel); passed on the second send (the first answer was an error, Try again worked). Plan line 11 updated to match.
