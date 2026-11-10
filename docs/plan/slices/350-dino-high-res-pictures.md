### Slice 350 — Dino: high-resolution pictures for a printed Amazon book, fixing the faulty pages

*Planned.*

**Touches:** `content/dino/*/high-res/**` (new), `.gitattributes`, `.github/scripts/ship-targets.mjs`, `.claude/skills/create-dino-story/SKILL.md`, `.claude/skills/create-dino-story/prompts.md`, `.claude/agents/dino-story-illustrator.md`, `.claude/agents/dino-story-reviewer.md`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/146-dino-books-kid-friendly-language.md`, `docs/plan/slices/196-dino-default-view-per-book.md`, `docs/plan/slices/350-dino-high-res-pictures.md`

**Why.** The user wants to sell the books as printed Amazon books. Today's pictures are 896x1200 (a few are 1792x2400), which is too small for print. There is no rush: the app works as it is.

**Decisions locked with the user (2026-11-10):**

- New pictures are drawn in the Gemini web app in the user's Chrome. **Only one thread at a time**, so that every answer gets proper care. Speed doesn't matter.
- **The old pictures are never overwritten.** The new ones go in `content/dino/<book>/high-res/`, named after the picture they replace (`high-res/cover.png`, `high-res/pictures/<n>.png`). Each file is kept exactly as Gemini downloads it: no shrinking and no re-encoding. They are stored in **Git LFS**.
- Only the cover and the page pictures are redrawn. The small cover thumbnail and the character sheets are skipped, because they are only references.
- The shape stays tall, 3 wide by 4 high. During the test phase, check whether this fits one of Amazon's print page sizes, and tell the user.
- **Copy, don't reinvent.** For each page, upload the old picture together with its prompt from the book's `art.md`, and ask for the same picture in higher quality. The pages listed under *Pages to fix* below are drawn again with the fix instead.
- **Test phase first.** On one book, take the cover and one busy page with several characters (lion-and-mouse page 5, which also needs its fix). Run both through every image model Gemini offers the user, one at a time. Put the results side by side on one private comparison page, with pixel size and file size. **Then stop and discuss the choice with the user.** The full run starts only after the user has picked a model.
- **Usage limit.** The user was at 98% of their weekly Claude usage and keeps the last 1%. So the session that runs the test phase stops when the test phase is done. The full run starts after the weekly reset.
- **One prompt per Gemini chat.** Each picture gets its own new chat, so one picture can't influence the next. The only exception is patching a bad result. Then decide whether a new chat is better than continuing the old one. **No chat ever gets more than 3 picture requests, without exception.** This replaces the old "fresh chat every 3 pictures" rule for all Dino illustration work, not only this slice.
- **Good Enough gets no words in its pictures (this reverses the 2026-10-29 decision).** The printed words were left over from the old feature that read text from pictures (OCR), which the app no longer has. Good Enough is **redrawn completely** with no text in any picture, signs included (its `art.md` line 22 names "Bright Stadium", "Great Views, Row C" and "Exit").

**Pages to fix** (the user, 2026-11-10; each is drawn again, not copied):

| Book | Pages | Problem to fix |
| --- | --- | --- |
| lion-and-mouse | 5 | the lion has three hands |
| billy-goats-gruff | 4 | an extra limb (same problem as lion page 5) |
| freddy-and-the-slipper | 1, 2, 3, 4, 5, 13, 14, and 6, 7 | the bottom of the page is plain white. It should continue the scene (page 1: grass at the bottom, the rest of the sky and tree at the top). Pages 6 and 7 could pass for a lit lamp, but get fixed too |
| good-enough | all | no text anywhere in the pictures |
| jack-and-the-beanstalk | 4, 15 | the user flagged these two; the rest are fine. Compare them with the other pages and the reviewer's checks to find what is wrong, and say what was fixed |
| little-red-hen | 14 | the tools are distorted |
| red-riding-hood | 4 | it uses the same background as 5, 6 and 7 but without the berries. Make the background match theirs about 95%, or make it clearly a different place |
| three-little-pigs | 5 | the wolf arrives while Pim is still building his brick house, so he could be caught. Show the wolf at the straw house, or all three houses finished |
| town-mouse-country-mouse | 1, 3, 4, 5, 6, 13, 14 | a white strip at the bottom of the page |
| ugly-duckling | 8, 9, 10, 11 | page 8: they stand on the water instead of swimming in it. Pages 8-11: the bottom is empty instead of proper water or ground |
| x-marks-the-treasure | 17 | Kathryn should hug the seagull, but it sits on her hat |

**Work:**

- [ ] A. Test phase (one session, stops at the end):
  - [ ] A1. List the image models Gemini offers the user's account, and note each one's maximum output size.
  - [ ] A2. Draw lion-and-mouse's cover and page 5 (fixed) with each model, one chat per picture, and save the files to a scratch folder (not the repo).
  - [ ] A3. Publish one private comparison page that shows them side by side, with model, pixel size and file size. Note whether 3:4 fits an Amazon print page size.
  - [ ] A4. Stop and discuss with the user. Write their chosen model (and the output size to ask for) into this file.
- [ ] B. Guard rails, before the first picture is committed:
  - [ ] B1. In `.gitattributes`: `content/dino/*/high-res/** filter=lfs diff=lfs merge=lfs -text`. Check that `.lfsconfig` keeps these files out of normal fetches and CI checkouts, as it does for audio.
  - [ ] B2. In `.github/scripts/ship-targets.mjs`, make sure a change under `content/dino/<book>/high-res/**` maps to no ship target, so it doesn't start a store upload. Add a test case if the script has tests.
  - [ ] B3. Check that `BookHash`, `StarterBuilder` and `publish-store-books.mjs` never read `high-res/`. Neither the app nor the store gets these files.
- [ ] C. Rules and quality checks (the user asked for this so the same faults never come back):
  - [ ] C1. Write the one-prompt-per-chat rule (at most 3 picture requests per chat, ever) into `.claude/skills/create-dino-story/SKILL.md`, `prompts.md` and `.claude/agents/dino-story-illustrator.md`, replacing "a fresh chat every 3 pictures".
  - [ ] C2. Add these checks to the full-size picture check in the skill and in `.claude/agents/dino-story-reviewer.md`. A picture fails if any of them fails:
    - every character has the right number of arms, hands, legs, paws and wings;
    - the scene fills the whole frame, with no plain white or empty strip at the top or bottom;
    - there are no words, letters or signs in the picture;
    - objects and tools have their real shape, not melted or twisted;
    - a page whose place matches the pages next to it repeats their background about 95% (same props, such as the berries); otherwise it is clearly a different place;
    - the picture doesn't contradict the story or put a character in danger the text doesn't have (a pig still building while the wolf is there);
    - the action matches the text exactly (a hug is a hug, not a bird sitting on a hat);
    - swimming animals are in the water, not standing on top of it.
  - [ ] C3. Record the Good Enough reversal: `src/DinoReader.App/CLAUDE.md` line 28 ("never painted words"), Slice 146 line 48 and Slice 196 line 7 ("never redraw") each get a dated note that the decision was reversed on 2026-11-10 and points here.
- [ ] D. Full run, after the user has picked a model and the weekly usage has reset. Do one book at a time, and for each book:
  - [ ] D1. Draw the cover and every page (copy mode, or fix mode for the pages above), one thread, one chat per picture. Check every picture at full size against the C2 checks.
  - [ ] D2. Run `dino-story-reviewer` over the book's high-res pictures. Its pass is required.
  - [ ] D3. Commit the book's `high-res/` folder (LFS) with a contact sheet for the user, and record the chats and sizes in the book's `art.md`.
  - Books: billy-goats-gruff, elves-and-shoemaker, freddy-and-the-slipper, gingerbread-man, goldilocks, good-enough, jack-and-the-beanstalk, lion-and-mouse, little-red-hen, red-riding-hood, three-little-pigs, tortoise-and-hare, town-mouse-country-mouse, ugly-duckling, x-marks-the-treasure.

**Not in this slice:** this slice doesn't change the pictures inside the app or the store, or Good Enough's Picture-only default (Slice 196). Whether the fixed pictures should replace the app's copies is the user's call. It is asked once at planning time.

**Verification:**

- `git lfs ls-files` lists every `high-res` file, and `git check-attr` shows `filter: lfs` for one of them.
- `ship-targets.mjs` gives no target for a path under `high-res/`, and BookBuilder `check` gives the same book hashes before and after.
- Each book's contact sheet and its `dino-story-reviewer` pass are recorded in its `art.md`. Every fixed page is shown next to its old version.
