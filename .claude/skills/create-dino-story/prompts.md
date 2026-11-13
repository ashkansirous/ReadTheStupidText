# Gemini prompt templates for Dino books

`<STYLE>` = `Children's picture book illustration, soft watercolour and coloured pencil, warm gentle
colours, simple rounded shapes, no text or letters anywhere.`
`<SIZE>` = `Output size: small, 1K resolution, 896x1200 pixels (portrait 3:4).` It ends **every** prompt that
asks for a picture: nothing bigger is needed, and bigger pictures use more of the usage limit.


## Character reference sheet (one per character)

```
Children's picture book character reference sheet, <STYLE> Plain white background.
<Name>: <species, size, build>, <clothes and colours>, <prop>. Full body shown from front, side and
back, plus three small expressions: <calm>, <happy>, <surprised>. Friendly and gentle, suitable for
children aged 3-9. <SIZE>
```

## Line-up

```
Children's picture book character line-up, <STYLE> Plain white background. Standing side by side, full
body, facing forward: <Name 1> (<short look>), <Name 2> (<short look>), ... <Relative sizes, e.g. "Wolf
is about twice the height of the pigs">. Each character appears exactly once. <SIZE>
```

## Cover

```
Make the book cover. <STYLE> Portrait 3:4. The characters, with their outfits: <Name 1: look, what
they wear and do NOT wear>, <Name 2: ...>. Exactly <k> characters. <Scene: the main characters together, the story's key
place>. <Villain, if any, shown silly not scary>. Every character stays whole in the top 70% of the
picture: heads, bodies, legs, feet, paws, tails and anything they hold all end above the 70% line. The
bottom 30% is calm scene (<grass, flowers, floor or water>) with no character or any part of one in it,
never a plain empty strip. <SIZE>
```

## Back cover (Slice 359)

One picture per book, no words in it; the reader lays the credits over its middle. Paste the line-up. The user
liked three styles (2026-11-13): **bedtime** (fits a story's end; Lion and Mouse uses it), **frame** (funny) and
**picnic**. Pick the one that fits the book; the user says if they want other options.

```
The pasted picture is the cast of a children's picture book: keep the characters exactly as shown. <Name 1:
look, what they wear and do NOT wear>, <Name 2: ...>. Draw a single back-cover picture for the end of the book.
<STYLE> Portrait 3:4. Exactly <k> characters, each appearing once, no other animals or people, normal numbers of
arms, hands, legs and paws. Nobody is hurt; no teeth showing. No text, letters, numbers, words, signs or writing
anywhere. IMPORTANT LAYOUT: the whole middle of the picture (roughly the central 40 to 60 percent of the height,
full width) must be calm, open and empty of characters and detail, because text will be laid over it. Put the
characters and details only around the edges as described. <SCENE> <SIZE>
```

`<SCENE>`, one of (Lion and Mouse wording; swap in the book's characters, place and props):

- **Bedtime:** Scene: night time, an epilogue. The top two thirds of the picture is a calm deep-blue night sky
  with a few small soft stars and a pale crescent moon near the top corner, and the whole middle stays quiet and
  open. At the bottom edge, in soft dim grass under moonlight, the Lion is curled up asleep with his eyes closed
  and a peaceful smile, his crown resting beside his head, and the tiny Mouse is snuggled asleep in his mane. Both
  fully inside the bottom fifth of the picture.
- **Frame:** Scene: a gentle daytime frame, an epilogue by the river. Two slim trees stand at the left and right
  edges of the picture, and the old soft brown rope fishing net from the story (big square holes, hanging loosely
  like a hammock, no hooks, no floats) hangs between their upper branches across the very top edge like a garland,
  with some rope draped down the sides. The Lion peeks in cheerfully from the left edge, half behind a tree, and
  the Mouse peeks in from the right edge, half behind the other tree, both smiling at the viewer. A little river
  grass and flowers along the bottom edge. The whole middle of the picture is plain soft pale sky-and-meadow wash,
  quiet and open.
- **Picnic:** Scene: a warm sunny afternoon, an epilogue after the story. At the very bottom edge of the picture
  Lion and Mouse sit together on a red-and-white checked picnic blanket in the soft golden grass, sharing a small
  pile of red strawberries; the Mouse holds one berry and laughs, the Lion smiles with a berry on his paw. Their
  whole bodies stay inside the bottom fifth of the picture, with nothing cut off. Above them the whole middle and
  top of the picture is plain soft pale blue sky with just a faint watercolour wash and one or two tiny faint
  clouds, no characters.

Four-legged animals stay on four legs (a "walking away" sample had the Lion upright on two legs). The 70% line
rule does not apply to a back cover; its open area is the middle.

## Page

```
Page <n>. <STYLE> Portrait 3:4, no text or letters anywhere. Characters on this page, outfits exactly
as in the pasted picture: <Name: look, what they wear and do NOT wear>. Scene: <time of
day, place>. <Who does what, with each character's outfit in brackets>. <Who is NOT on this page>.
Exactly <k> <characters>, each appearing once. Nobody is hurt or in danger. Every character stays
whole in the TOP 70% of the picture: heads, bodies, legs, feet, paws, tails and anything they hold all
end above the 70% line. The bottom 30% is calm scene (<grass, flowers, floor or water>) with no
character or any part of one in it, never a plain empty strip. <SIZE>
```

## Fresh page chat opener (first message of every page chat, with the line-up pasted)

```
These are the characters for a children's picture book (the pasted picture is the cast). Keep their
outfits exactly as shown: <Name 1: what they wear, and what they do NOT wear, e.g. "no shirt, no
trousers, no bobble">; <Name 2: ...>; <Name 3: ...>. Same style every time: <STYLE> I will ask for
one picture in this chat.
```

Then send the Cover or Page template as the next message. One picture per new chat; a small patch of a bad result may continue that chat while it stays at or under 3 picture requests (the user's rule, Slice 350 decisions); if the bad picture would steer the patch, start a new chat instead. As in SKILL.md.

## Fix (after a failed check)

```
Almost: <what is wrong, concretely>. Please redo page <n> keeping everything else: <restate the
scene, the count and every outfit>. <SIZE>
```

## Restart (Gemini mixed scenes)

Do not argue in the same chat. One prompt per chat is the rule (Slice 350), and no chat ever gets more than 3 picture requests. Start a **new chat**, paste the line-up, send the opener above, then:

```
Draw a single picture. <STYLE> Portrait 3:4. <Full scene description from scratch, every character
with outfit, the count, indoor/outdoor, the top-70% rule: every character whole above the 70% line, feet and tails included.> <SIZE>
```
