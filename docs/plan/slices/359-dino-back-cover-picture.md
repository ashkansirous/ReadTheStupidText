### Slice 359 — Dino: a drawn back-cover picture for every book, with the credits shown over it

*Planned 2026-11-13. Step 1 done (style picked); step 2 next.*

**Touches:** `content/dino/*/back-cover.jpg` (new), `content/dino/*/high-res/back-cover.jpg` (new, LFS), `content/dino/*/art.md`, `content/dino/*/book.json`, `tools/DinoReader.BookBuilder/**`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson*.cs`, `src/DinoReader.Core/BackCover*.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/TextPages.cs`, `src/DinoReader.App/PicturePages.cs`, `tests/DinoReader.Tests/**`, `.claude/skills/create-dino-story/SKILL.md`, `.claude/skills/create-dino-story/prompts.md`, `.claude/agents/dino-story-illustrator.md`, `.claude/agents/dino-story-reviewer.md`

## Why

Slice 237 (merged #688, 2026-11-01) gave every book a credits page that the app draws live: the book's colour, a
small cover picture and the credit lines. The user had meant a real back-cover picture, like the `Back cover.jpg`
of their own sample book. I (the planner) did not think of a picture or of printing, so no back-cover image exists.
Slice 237's landscape, Farsi and reading-mode checks were also never run after the first portrait check.

## Decisions (the user, 2026-11-13)

- **One back-cover image per book**, not one per language: "1 back cover image! the text is a separate thing like
  the rest of the books."
- **The picture is drawn like the front cover**: "the real image generation should generate something interesting,
  or somewhat funny, given the same pictures/character/environment of the story, just like the front cover."
  No words in the picture (the no-printed-text rule for Dino pictures stands).
- **The credit text is shown over it, in the middle, by the reader**: "the expectation is that the text will be
  shown in the middle in the book reader." So the picture keeps its middle calm and open.
- **Printing is handled differently** (print-the-damn-book's own later work, not this slice).
- **Style:** five samples for Lion and Mouse first; the user picks the style, then every book follows it.
- **The picture replaces Slice 237's live-drawn page** in the app (answer 3: "yes"), so the app shows the drawn
  picture with the credit lines over its middle. The landscape, Farsi (right to left, Persian digits), picture-only
  and text-only checks left open by Slice 237 are run on the new page in this slice.

## Work

- [x] 1. Five back-cover samples for Lion and Mouse (picnic, bedtime, walking off, crown swap, frame), saved to
  scratch, shown to the user. Write the picked style and its prompt into `prompts.md` and this file.
  The user, 2026-11-13: "they all look good. sample-2.jpg fits the story as the end. sample-5.jpg is funny. for
  this one lets go with sample2. I like sample1 as well, but less than these 2. save the prompt for these 3
  options. the lion and the mouse goes with sample-2. for the rest you can pick by yourslef which style to choose
  and I will let you know if I dont like it to generate other options."
  So: Lion and Mouse uses sample 2 (bedtime), from `scratchpad\backcover-samples\sample-2.jpg` of session
  a87d275d (1792x2400, 3.1 Pro, untouched); it is copied into the build branch in step 2. The bedtime, frame and
  picnic prompts are in `prompts.md` (*Back cover*). For every other book the coordinator picks the style that
  fits; the user asks for other options if they dislike one. Sample 3 (walking off) had the Lion upright on two
  legs, which the drawing agent passed and the coordinator caught.
- [ ] 2. Smallest end to end, one book: Lion and Mouse's `back-cover.jpg` (app size) and `high-res/back-cover.jpg`;
  the builder packs it; the reader shows it as the back cover with the credit lines centred over the middle,
  never voiced, never counted. Android and iOS share the MAUI code; Windows head too.
- [ ] 3. Every other book: draw its back cover in the picked style (illustrator, page groups of at most 5), full-size
  check, reviewer pass, version bump so installed copies update, store upload.
- [ ] 4. Builder `check` fails a book without a back-cover picture; the skill, writer, illustrator and reviewer rules
  make one for every new book.
- [ ] 5. Tests: builder (picture packed, page not counted), Core (never in the sentence list).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and `BookBuilder check`.
- Android emulator: Lion and Mouse to the end in portrait and landscape, English and Farsi, picture-and-text,
  picture-only and text-only: the picture shows, the credits sit readable in the middle, nothing is spoken.
- iOS compile check; Windows head shows the back cover.
