### Slice 359 — Dino: a drawn back-cover picture for every book, with the credits shown over it
**Product:** Dino

*Built 2026-11-13, merged as PR #1048 (b9e545b2). Android emulator flows pass; iOS and Windows compile only. The 2026-11-13 Missing States mock (section 1) then drew the credits as a cream card in the middle of the picture at every size, so the layout built here is changed by work item 6 below.*

**Touches:** `content/dino/*/back-cover.jpg` (new), `content/dino/*/high-res/back-cover.jpg` (new, LFS), `content/dino/*/art.md`, `content/dino/*/book.json`, `tools/DinoReader.BookBuilder/**`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson*.cs`, `src/DinoReader.Core/BackCover*.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/TextPages.cs`, `src/DinoReader.App/PicturePages.cs`, `src/DinoReader.App/BackCoverPages.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/Resources/Raw/testbooks/*.dino`, `.maestro/dino/back-cover*.yaml`, `src/ReadTheStupidText.Application/Usage/FallbackKind.cs`, `worker/events.ts`, `tests/DinoReader.Tests/**`, `.claude/skills/create-dino-story/SKILL.md`, `.claude/skills/create-dino-story/prompts.md`, `.claude/agents/dino-story-illustrator.md`, `.claude/agents/dino-story-reviewer.md`

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
- [x] 2. Smallest end to end, one book: Lion and Mouse's `back-cover.jpg` (app size) and `high-res/back-cover.jpg`;
  the builder packs it; the reader shows it as the back cover with the credit lines centred over the middle,
  never voiced, never counted. Android and iOS share the MAUI code; Windows head too.
- [x] 3. Every other book: draw its back cover in the picked style (illustrator, page groups of at most 5), full-size
  check, reviewer pass, version bump so installed copies update, store upload.
  Drawn 2026-11-13 (3.1 Pro, 1792x2400, untouched). The user rejected 6 of the first round with reasons (billy-goats:
  "the troll is only half"; freddy: slipper on his head "not funny nor relevant"; jack: giant eye, show him small on
  a cloud like the last page; three-little-pigs: they don't sleep outside together, pigs on one side and the wolf on
  the other; ugly-duckling: the duckling small like the rest, not a swan; x-marks: chest "too small and is empty"),
  then approved the full set: "all good now". The coordinator had checked only look and safety, not the story and the
  book's own pictures; the redraw briefs required both. Approved source files, all under
  `C:\Users\ashka\AppData\Local\Temp\claude\717394277bc5a198\a87d275d-c94c-4e10-91f5-de31b25da6a7\scratchpad\`:
  - lion-and-mouse: `backcover-samples\sample-2.jpg` (bedtime)
  - billy-goats-gruff, jack-and-the-beanstalk, three-little-pigs, ugly-duckling, x-marks-the-treasure,
    freddy-and-the-slipper: `backcovers\<book>.v2.jpg` (Freddy's has a fridge-note scribble painted out; the
    unretouched file is `freddy-and-the-slipper.v2-orig.jpg`)
  - elves-and-shoemaker, gingerbread-man, goldilocks, little-red-hen, red-riding-hood, tortoise-and-hare,
    town-mouse-country-mouse: `backcovers\<book>.jpg`
  - good-enough: none yet. Its pictures are redrawn without text under Slice 350 E and its text is being rewritten
    as dialogue (draft waiting for the user); it gets a back cover then. Until then the reader keeps Slice 237's
    live-drawn credits page for a book without a back-cover picture, and `check` allows that only for good-enough.
  - Built: the pictures are drawn and in the books. The store upload happens after merge by the dino-store workflow,
    which versions store books by content hash. Three starters are stamped: three-little-pigs v15, gingerbread-man v12,
    tortoise-and-hare v12.
- [x] 4. Builder `check` fails a book without a back-cover picture (good-enough excepted until it has one); the skill,
  writer, illustrator and reviewer rules make one for every new book, and the full-size check of a back cover compares
  it with the book's story and its own pictures (characters whole, sizes and props as in the book), not only its look.
- [x] 5. Tests: builder (picture packed, page not counted), Core (never in the sentence list).
  - **From review:**
    - Skipped #1 (`reviews/s359b-high.md`): "Work items 2, 4 and 5 are built but unticked; header still says 'step 2 next'" - update-docs ticks items.
    - Skipped #10 (`reviews/s359b-high.md`): "two sources for one value" - the name is written twice on purpose: old apps read the last page's picture, new apps read the `Picture` field.
    - Skipped #3 (`reviews/s359b-check.md`): "DrawnBackCovers static cache never evicted; a hit skips File.Exists" - an installed book's files do not vanish during a run; a re-download is a new process or story id.
    - Skipped #4 (`reviews/s359b-check.md`): "AspectProblem ignores EXIF orientation" - Gemini JPEGs carry no EXIF rotation.
    - Skipped #5 (`reviews/s359b-check.md`): "no zero guard on SOF width/height, and Of(string) reads the whole file" - low severity, a corrupt picture still fails the check; not worth more code.
    - Skipped #6 (`reviews/s359b-check.md`): "side-by-side FitFontSize reserves 2*ScrimPadding though there is no scrim; credits loop duplicated" - cosmetic.
    - Skipped #8 (`reviews/s359b-check.md`): "asserts fixed 'October 2026' credits month; flow not run on a device" - the flow was run on the emulator and passed.
    - `reviews/s359b-check2.md`, all 6 skipped, no code changed after it:
      - #1 landscape puts the credits beside the picture, not over its middle: the coordinator's call after the first
        emulator run (in landscape the overlay hid the characters), following Slice 237's Design frame 148. Portrait
        keeps the user's "text in the middle". The user may overrule it. (The user did, through the 2026-11-13 mock: item 6.)
      - #2 the grown band is a font estimate: it only grows when the text would not fit, and the emulator run checks
        Farsi and Arabic.
      - #3 a book with credit fields but no back-cover.jpg ships its live page: `check` fails it, and the Dino test `BookBuilderTests.Check_PassesOnTheCommittedBooks...` (run in CI by tests.yml) runs it over every committed book.
      - #4 a blank `Picture` "" skips the old-file fallback: the builder never writes "".
      - #5 side-by-side padding and the duplicated credits loop: cosmetic (same as check #6).
      - #6 no unit test for the side-by-side threshold or the band clamp: MAUI layout, covered by the emulator flows.
    - `reviews/s359b-high.md` (10 findings read 10/10): #2-#9 applied.
    - `reviews/s359b-medium.md` (9 findings read 9/9): #2-#5, #7 and #8 applied.
    - Skipped #1 (`reviews/s359b-medium.md`): "version and contentHash not stamped for 14 books" - did not reproduce: the store build versions books by content hash after merge (dino-store workflow); only the three starters are stamped in the repo.
    - Skipped #6 (`reviews/s359b-medium.md`): "long languages (Farsi, German) may overflow the scrim" - covered by the emulator run: back-cover-fa and back-cover-ar pass after the layout fix.
    - Skipped #9 (`reviews/s359b-medium.md`): "items 2, 4, 5 built but unticked" - a docs finding, done by this update-docs pass (same as high #1).
    - #5 applied as: `check` fails a book that has back-cover.jpg but no credit fields in book.json. #6: `check` fails a picture that is not 3:4 (1% tolerance). #9: reason `back_cover_missing` (area `library`) when a book names its picture and the file is missing.

- [ ] 6. **Match the 2026-11-13 mock** (Design below). The built layout differs in these ways, each to be changed:
  - Credits sit on a **cream card in the middle of the picture at every size and view**: 300 wide (never wider than the picture
    less 16 each side), radius 22, padding 18 22 20, `rgba(255,246,230,.9)`, shadow 0 8 28 rgba(43,37,64,.22), centred both ways.
    Built: a light box in the 30-62% band (`BandTop`/`BandBottom`, `ScrimAlpha` 0.6, `ScrimCorner` 16, `ScrimPadding` 14, grown
    band `BandFor` in `BackCoverPages.cs`): remove the band and its growth.
  - **Landscape no longer puts the credits beside the picture.** Built: side by side (`BuildSideBySide`, "Changed while building").
    Mock BC2: the picture fills the stage and the 340 wide card stays in the middle of the picture. Remove the side-by-side branch
    and its threshold.
  - **The picture fills the stage** (cover, focused a little above centre; stage radius 18, fill `#F0E8D8`) instead of the 3:4
    picture shown whole and centred on the card (`BuildDrawnPage`, `BackPictureAspect`). The title row above the picture stays.
  - **Credit text sizes:** title 20 / 600 (19 landscape), line 1.25; a 36 x 3 amber `#FFC24D` rule, margin 10 auto; lines 16 / 400
    (15 landscape; tablet and Windows card 360 wide at 18 / 400), line 1.55, gap 2, centred. Built uses `LineFontSize` 20 shrinking
    to 13: replace by these fixed sizes; the card grows in height only.
  - **View switch hides** on the back cover in every view (picture, picture and text, text only): all views show the same page.
  - Read again replaces Play and next is dimmed (as before).
  - **Device checks it needs** (Android emulator, Dino Debug): rewrite the `back-cover*.yaml` flows to expect the card in the middle in
    portrait, landscape (852 x 393), tablet, English and Farsi and Arabic (right to left, Persian digits), picture-only,
    picture-and-text and text-only, with screenshots checked against BC1, BC2 and BC3. Windows head: the 360 card at 18. iOS compile,
    then TestFlight. Unit test in Core for the card-width rule (300 / 340 / 360, never wider than the picture less 32).

#### Design (Dino Reader 2026-11-13 Missing States mock, section 1, added 2026-11-13)

Three frames (BC1 phone upright, BC2 phone landscape 852 x 393, BC3 Farsi), no simplified UI. The picture in the frames is a stand-in
(the last Lion and Mouse page; the real one is sample 2, bedtime). The mock **disagrees with the built slice** (portrait band,
landscape beside the picture); the mock wins and item 6 lists the changes. Text keys (`docs/plan/notes/2026-11-13-dino-copy.md`,
*Missing States*, all existing): `BackCover_OriginalStoryBy`, `BackCover_RetoldBy`, `BackCover_Illustration`, `BackCover_Date`,
`Reading_ReadAgain`. Hex as in the mock (use the `DinoTokens` key with the same value, else add one). The phone bezel is canvas
furniture. Layout never mirrors (`src/DinoReader.App/CLAUDE.md`): the card is the same in Farsi; only its text runs right to left.

- **Stage (all views, all sizes):** the drawn back cover fills the picture area, cover, focused a little above centre. Portrait: margin
  6 14 0 under the title row, radius 18, fill `#F0E8D8`. Landscape: margin 14 0 right of the 62 wide nav column, radius 18. The title row
  above stays: flag pill 46 high, radius 16, fill `#F0E8D8`, then the book's title 20 / 600, line 1.4, in the reading language.
- **Credits card (BC1, BC3):** centred in the picture, width 300, at most the picture's width less 32, radius 22, padding 18 22 20, fill
  `rgba(255,246,230,.9)` with a light blur behind where the platform has one (else the 90% fill alone), shadow 0 8 28 rgba(43,37,64,.22),
  ink `#2b2540`. Title: the book's title 20 / 600, line 1.25, centred. Rule 36 x 3, radius 2, `#FFC24D`, margin 10 auto. Credit lines 16 / 400,
  line 1.55, gap 2, centred, the book's own lines as Slice 237 shows them: "Original story by Aesop", "Retold for young readers by Ashkan
  Sirous", "Illustration: Ashkan Sirous", "November 2026".
- **BC2 (landscape):** the card keeps its place in the middle of the picture, not the screen; 340 wide, title 19 / 600, lines 15 / 400, so four
  lines fit at 393 high. The controls column (150 wide, centred, gap 16: flag pill, Read again 72 round `#FF7A59`, two 52 round `#ECE7FF` /
  `#5B57E8` buttons, the second at opacity .4) is where the landscape reading page has it (Batch 44); only the stage is new. Tablet and Windows:
  same stage, card 360 wide at 18 / 400.
- **BC3 (Farsi):** Vazirmatn, right to left, Persian digits ("نوامبر ۲۰۲۶"); the card is the same size; the lines come from
  `creditsByLanguage`. The flag on the language button follows the reading language.
- **Controls (BC1):** row centred, gap 28, padding 12 0 30: previous 64 round `#ECE7FF` / `#5B57E8`, Read again 84 round `#FF7A59`
  (`Reading_ReadAgain`, replaces Play), next 64 round, same colours, opacity .4. The view switch is hidden: the back cover is a picture
  page in every view. Nothing is read aloud and nothing is counted.
- The pictures are told to keep the middle calm (in `prompts.md`); the card is there in case one does not.

No new strings.

**Plan changed to match the mock (2026-11-13):**
- Portrait "credits on a light box in the 30-62% band" becomes a 300 wide cream card centred in the picture (BC1). The older text is
  kept above, marked superseded.
- Landscape "picture left, credits right, no overlay" becomes the 340 wide card over the middle of the picture (BC2).
- Picture "shown whole (3:4), centred" becomes cover filling the stage (the mock: "The picture fills the stage with cover").
- Credit sizes: fixed 20 / 16 (19 / 15 landscape; 18 / 18 with a 360 card on tablet and Windows), not shrink-to-fit.
- View switch hides on the back cover; Read again replaces Play.
- Work item 6 added, unticked, with every difference and the device checks. The cream card is kept (decided by the user).
- Slice 237's side-by-side landscape layout (its frame 148) is no longer built. No `CLAUDE.md` rule clashes; "For CLAUDE.md" below stays true.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and `BookBuilder check`.
- Android emulator: Lion and Mouse to the end in portrait and landscape, English and Farsi, picture-and-text,
  picture-only and text-only: the picture shows, the credits sit readable in the middle, nothing is spoken.
- iOS compile check; Windows head shows the back cover.

*Changed while building:*

- (The mock supersedes this; see item 6. The mock's cream card is small, a 340 wide card of about four lines, and the user kept it in the middle.) Landscape shows the picture left and the credits right (no overlay): the first emulator run showed the overlay hid the picture. Portrait puts the credits on a light box in the picture's 30-62% band (grows only if the text does not fit). Side-by-side only when the card is at least twice as wide as the 3:4 picture's height needs.
- `check` also fails a back cover that is not 3:4 and a book with a picture but no credit fields.
- New fallback reason `back_cover_missing` (area `library`), reported once per book per run.
- Text-only: the app was right; the flow now swipes until the back cover shows.

*Device run (Android emulator-5554, Dino Debug, 2026-11-13):* back-cover-picture (Three Little Pigs portrait, landscape, picture-only, text-only), back-cover-fa and back-cover-ar all PASS after the layout fix. The first run failed (portrait box over the characters, landscape overlay hid the picture, text-only flow wrong); fixed, screenshots checked by the coordinator.

#### For CLAUDE.md (lifted 2026-11-13)

In `src/DinoReader.App/CLAUDE.md`: every Dino book has `content/dino/<book>/back-cover.jpg` (3:4, no words, drawn like the cover, checked against the book's story and its own pictures) and `high-res/back-cover.jpg` (LFS); the reader lays the credits over it (`BackCoverPages.cs`); only good-enough is exempt until it has one. (Slice 359: docs/plan/slices/359-dino-back-cover-picture.md)

#### Not yet verified

- iOS on a device (compile only).
- Windows head run (compiled only).
- Tablet layout and the Batch 45 22/17 sp credit sizes (wait for the tablet slice, 234).
- Long-language credits in the grown band on a narrow phone beyond fa/ar.
- Older installed apps showing the picture page.
- Good Enough's back cover (after its text-free redraw, Slice 350 part E, and its text rewrite).
- The store upload of the 11 store books and their new versions on the live store.
