# Slice 121 — Dino: pictures for the ten store stories

*In progress* (one book per PR; Billy Goats Gruff first). Dino Reader only; RTST is not touched.

**Batch:** 25 (story illustrations)
**Depends on:** Slice 119 (user checked the pigs on their phone), Slice 120 (the skill)
**Touches:** `tests/DinoReader.Tests/Store/<stem>/**`, `tests/DinoReader.Tests/StoreBuilder.cs`, `tests/DinoReader.Tests/StoreTexts.cs`, `tests/DinoReader.Tests/Store*Tests.cs`, `docs/design/story-art/**`, `.claude/skills/create-dino-story/**`, `.claude/agents/dino-story-*.md`; starter books (the Gingerbread Man): `tests/DinoReader.Tests/StarterTexts.cs`, `tests/DinoReader.Tests/StarterBuilder.cs`, `src/DinoReader.Core/StarterUpdate.cs`, `tests/DinoReader.Tests/StarterUpdateTests.cs`, `src/DinoReader.App/Resources/Raw/starters/*.dino`, `tests/DinoReader.Tests/Starters/<stem>/**`, `tests/DinoReader.Tests/Starters/translations/<stem>.*.json`, `src/DinoReader.Core/EnglishRules.cs`, `tests/DinoReader.Tests/GenderGuesserTests.cs`

## Why

The ten store stories (Slice 102) are text only. Each gets a picture cover and a picture per page, made
with the `create-dino-story` skill.

## Decisions

Decisions 125-127 apply. Each illustrated package ships at the book's current Version + 1 (Billy Goats: `story-v5.dino`, `version: 5`, 14 pages). One book per PR, merged and uploaded as it is done, so the free-tier
daily cap only slows the work down and never holds a finished book back.

## Work

One item per book: art record, Gemini chat, pictures through the prepare script into
`tests/DinoReader.Tests/Store/<stem>/pictures/` and `cover.jpg`, `StoreBuilder` picks them up the way
`StarterBuilder` does (first book adds that), build, upload, emulator check.

- [x] Three Billy Goats Gruff (Little, Middle and Big Billy, Troll)
- [x] The Lion and the Mouse (Lion, Mouse)
- [x] Goldilocks and the Three Bears (Goldilocks, Papa, Mama and Baby Bear)
- [x] Little Red Riding Hood (Red Riding Hood, Mother, Wolf, Grandma, Woodcutter, Button)
- [x] The Ugly Duckling (Mother Duck, Duckling, Hen, Swan)
- [x] The Little Red Hen (Hen, Dog, Cat, Duck)
- [x] Jack and the Beanstalk (Jack, Mother, Giant, Peddler)
- [ ] The Elves and the Shoemaker (Hans, Greta, Nib, Nob)
- [x] The Town Mouse and the Country Mouse (Town Mouse, Country Mouse)
- [x] Peter Rabbit (Mrs. Rabbit, Peter, Mr. McGregor, Robin, Flopsy); new pictures, not Potter's, in the house style

## X Marks the Treasure! (2026-10-26)

An original store book, the first of the user's "Captain Kaatrin and Seagull" series (cast: Kaatrin, Seagull, Izzy, Pancake Polly, Billy; 17 pages, author Ashkan Sirous, art record `docs/design/story-art/x-marks-the-treasure.md`). It is the first store book above 16 pages, so `StoreStoryTests.MaxPages` is now 18 and the page-count guidance in the dino skills and agents says 10-18. The captain is spelled "Kaatrin" so the English voices say KAA-trin (reasons in the art record).

**Touches:** `tests/DinoReader.Tests/StoreTexts.cs`, `tests/DinoReader.Tests/StoreBuilder.cs`, `tests/DinoReader.Tests/StoreStoryTests.cs`, `tests/DinoReader.Tests/Store/x-marks-the-treasure/**`, `tests/DinoReader.Tests/Starters/translations/x-marks-the-treasure.*.json`, `docs/design/story-art/x-marks-the-treasure*`, `.claude/skills/create-dino-story/**`, `.claude/skills/dino-storytelling/**`, `.claude/agents/dino-story-*.md`

## Verification

- Tests green; each book's `GET /api/stories/<id>/download?v=<version>` returns a package with N pictures.
- Emulator: each book shows its cover and pages in both orientations.
