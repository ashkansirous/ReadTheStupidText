### Slice 123 — Dino: The Three Little Pigs retold with one action per page, plus a storytelling skill

*Built 2026-10-25, not yet verified.*

**Touches:** `.claude/skills/dino-storytelling/SKILL.md`, `.claude/skills/create-dino-story/*`, `tests/DinoReader.Tests/StarterTexts.cs`, `tests/DinoReader.Tests/StarterBuilder.cs`, `tests/DinoReader.Tests/Starters/translations/three-little-pigs.*.json`, `tests/DinoReader.Tests/Starters/three-little-pigs/**`, `src/DinoReader.Core/StarterUpdate.cs`, `src/DinoReader.App/Resources/Raw/starters/three-little-pigs.dino`, `docs/design/story-art/three-little-pigs.md`

**Why.** The user liked the Slice 119 pictures, but each of the six pages held two actions (the pig's act and the Wolf's answer), so one picture could not show the page. They asked for about twice as many pages, one action per page, and for a storytelling skill that makes this the rule for every book (2026-10-25).

**Work.**
- [x] New project skill `dino-storytelling`: one action per page, 10-16 pages, cause and answer on separate pages, a picture plan checked before drawing, and how to change a book without losing translations. `create-dino-story` now starts from it and composes pictures for the 30% words panel (Slice 122), with the action in the top 70%.
- [x] The pigs retold as 16 pages (14 first; review split the Wolf deciding to climb, and his run home, onto their own pages). Every existing sentence is kept, so the translations still match. Three new sentences (waving goodbye, running out of the stick house, peeking from the brick house) are translated into all 13 languages.
- [x] 12 new pictures drawn in fresh Gemini chats, each checked at full size. Pages 5, 8, 14 and 16 and the cover reuse Slice 119 pictures that show exactly that page's action. The record is in `docs/design/story-art/three-little-pigs.md`.
- [x] Pictures prepared (`tools/prepare-story-pictures.ps1`) and the starter rebuilt. Pigs starter version 3 to 4, with the shipped v3 text hash (v2 had the same text) added to `StarterUpdate.EarlierPageTextHashes` so unedited installed copies update. Tests green.
- [ ] After merge: store upload of the pigs at version 4.

**Verification.** `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green. Then, on the phone, the pigs book shows 16 pages, each with its own picture, and an installed v3 copy updates itself.

**Review.** Two passes. Fixed: the Wolf's roof plan and his run home moved onto their own pages (14 to 16 pages), a stored "cover = page N" pick is dropped when the page count changes (`StarterUpdate.KeepsOtherCover`), Brazilian Portuguese, Japanese spacing, Arabic agreement, Norwegian and French wording in the new lines, Decision 127 now says top 70%, and the hash recipe in the skill. Left: a page-index remap for cover picks (the pick falls back to the bundled cover).

#### Not yet verified
- On the phone: the pigs show 16 pages, each with its own picture, words panel at most 30%; an installed v3 copy updates itself to v4 and keeps its recordings on unchanged pages.
