# Slice 121 — Dino: pictures for the ten store stories

*In progress* (one book per PR; Billy Goats Gruff first). Dino Reader only; RTST is not touched.

**Batch:** 25 (story illustrations)
**Depends on:** Slice 119 (user checked the pigs on their phone), Slice 120 (the skill)
**Touches:** `tests/DinoReader.Tests/Store/<stem>/**`, `tests/DinoReader.Tests/StoreBuilder.cs`, `tests/DinoReader.Tests/StoreTexts.cs`, `tests/DinoReader.Tests/Store*Tests.cs`, `docs/design/story-art/**`, `.claude/skills/create-dino-story/**`, `.claude/agents/dino-story-*.md`

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
- [ ] The Lion and the Mouse (Lion, Mouse)
- [ ] Goldilocks and the Three Bears (Goldilocks, Papa, Mama and Baby Bear)
- [ ] Little Red Riding Hood (Red Riding Hood, Mother, Wolf, Grandmother, Woodcutter)
- [ ] The Ugly Duckling (Mother Duck, Duckling, Hen, Swan)
- [ ] The Little Red Hen (Hen, Dog, Cat, Duck)
- [ ] Jack and the Beanstalk (Jack, Mother, Giant, Peddler)
- [ ] The Elves and the Shoemaker (Hans, Greta, Nib, Nob)
- [ ] The Town Mouse and the Country Mouse (Town Mouse, Country Mouse)
- [ ] Peter Rabbit (Mrs. Rabbit, Peter, Mr. McGregor); new pictures, not Potter's, in the house style

## Verification

- Tests green; each book's `GET /api/stories/<id>/download?v=<version>` returns a package with N pictures.
- Emulator: each book shows its cover and pages in both orientations.
