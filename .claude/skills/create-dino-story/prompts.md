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
Using exactly the same characters as in the line-up above (same colours, clothes and proportions),
make the book cover. <STYLE> Portrait 3:4. <Scene: the main characters together, the story's key
place>. <Villain, if any, shown silly not scary>. Keep all characters in the top half of the picture;
the bottom half is <plain ground> with nothing important in it.
```

## Page

```
Now page <n>, same characters, same style, portrait 3:4, no text or letters anywhere. Scene: <time of
day, place>. <Who does what, with each character's outfit in brackets>. <Who is NOT on this page>.
Exactly <k> <characters>, each appearing once. Nobody is hurt or in danger. The whole scene sits in the
TOP 40% of the picture, smaller in the frame; the lower 60% is <plain ground/floor> with nothing
important in it.
```

## Fix (after a failed check)

```
Almost: <what is wrong, concretely>. Please redo page <n> keeping everything else: <restate the
scene, the count and every outfit>.
```

## Restart (Gemini mixed scenes)

```
That one mixed up two scenes, please ignore it. Draw a brand-new single picture. <STYLE> Portrait 3:4.
<Full scene description from scratch, every character with outfit, the count, indoor/outdoor, the
top-40% rule.>
```
