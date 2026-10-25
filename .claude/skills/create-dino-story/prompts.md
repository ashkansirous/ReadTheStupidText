# Gemini prompt templates for Dino books

`<STYLE>` = `Children's picture book illustration, soft watercolour and coloured pencil, warm gentle
colours, simple rounded shapes, no text or letters anywhere.`

## Character reference sheet (one per character)

```
Children's picture book character reference sheet, <STYLE> Plain white background.
<Name>: <species, size, build>, <clothes and colours>, <prop>. Full body shown from front, side and
back, plus three small expressions: <calm>, <happy>, <surprised>. Friendly and gentle, suitable for
children aged 3-9.
```

## Line-up

```
Children's picture book character line-up, <STYLE> Plain white background. Standing side by side, full
body, facing forward: <Name 1> (<short look>), <Name 2> (<short look>), ... <Relative sizes, e.g. "Wolf
is about twice the height of the pigs">. Each character appears exactly once.
```

## Cover

```
Make the book cover. <STYLE> Portrait 3:4. The characters, with their outfits: <Name 1: look, what
they wear and do NOT wear>, <Name 2: ...>. Exactly <k> characters. <Scene: the main characters together, the story's key
place>. <Villain, if any, shown silly not scary>. Keep all characters in the top 70% of the picture;
the bottom 30% is <plain ground> with nothing important in it.
```

## Page

```
Page <n>. <STYLE> Portrait 3:4, no text or letters anywhere. Characters on this page, outfits exactly
as in the pasted picture: <Name: look, what they wear and do NOT wear>. Scene: <time of
day, place>. <Who does what, with each character's outfit in brackets>. <Who is NOT on this page>.
Exactly <k> <characters>, each appearing once. Nobody is hurt or in danger. The whole scene sits in the
TOP 70% of the picture; the bottom 30% is <plain ground/floor> with nothing
important in it.
```

## Fresh page chat opener (first message of every page chat, with the line-up pasted)

```
These are the characters for a children's picture book (the pasted picture is the cast). Keep their
outfits exactly as shown: <Name 1: what they wear, and what they do NOT wear, e.g. "no shirt, no
trousers, no bobble">; <Name 2: ...>; <Name 3: ...>. Same style every time: <STYLE> I will ask for
up to 3 pictures in this chat, one at a time.
```

Then send the Cover or Page template as the next message.

## Fix (after a failed check)

```
Almost: <what is wrong, concretely>. Please redo page <n> keeping everything else: <restate the
scene, the count and every outfit>.
```

## Restart (Gemini mixed scenes)

Do not argue in the same chat. Start a **new chat**, paste the line-up, send the opener above, then:

```
Draw a single picture. <STYLE> Portrait 3:4. <Full scene description from scratch, every character
with outfit, the count, indoor/outdoor, the top-70% rule.>
```
