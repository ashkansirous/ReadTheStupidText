### Slice 350 — Dino: high-resolution pictures for a printed Amazon book, fixing the faulty pages
**Product:** Dino

*Park note (moved back to planned by the 2026-11-12-1337 feedback loop, user answer 2026-11-01): parts D and E need the user's Chrome and Gemini account (B and C merged #1013; A done 2026-11-13, model 3.1 Pro). A night wave that hits the same blocker parks it again with that reason.*

*Parts B and C merged as PR #1013 (01e91c0d); part A done 2026-11-13 (model: 3.1 Pro). D and E open, need the user's Chrome and Gemini account.*

**Touches:** `content/dino/*/high-res/**` (new), `content/dino/*/pictures/*.jpg` and `content/dino/*/cover.jpg` (item E, fixed pages only), `content/dino/*/book.json` (version bumps), `.gitattributes`, `.lfsconfig`, `.github/scripts/ship-targets.mjs`, `.github/workflows/dino-store.yml`, `.github/workflows/tools-selftests.yml`, `.github/scripts/ship-targets.test.mjs`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `.github/CLAUDE.md`, `tools/DinoReader.BookBuilder/**`, `tests/DinoReader.Tests/BookBuilderTests.cs`, `.claude/skills/create-dino-story/SKILL.md`, `.claude/skills/create-dino-story/prompts.md`, `.claude/agents/dino-story-illustrator.md`, `.claude/agents/dino-story-reviewer.md`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/146-dino-books-kid-friendly-language.md`, `docs/plan/slices/196-dino-default-view-per-book.md`, `docs/plan/slices/350-dino-high-res-pictures.md`

**Why.** The user wants to sell the books as printed Amazon books. Today's pictures are 896x1200 (a few are 1792x2400), which is too small for print. There is no rush: the app works as it is.

**Decisions locked with the user (2026-11-10):**

- New pictures are drawn in the Gemini web app in the user's Chrome. **Only one thread at a time**, so that every answer gets proper care. Speed doesn't matter.
- **The old pictures are never overwritten.** The new ones go in `content/dino/<book>/high-res/`, named after the picture they replace (`high-res/cover.<ext>`, `high-res/pictures/<n>.<ext>`, with Gemini's own file type, so `.jpg` today). Each file is kept exactly as Gemini downloads it: no shrinking and no re-encoding. They are stored in **Git LFS**.
- Only the cover and the page pictures are redrawn. The small cover thumbnail and the character sheets are skipped, because they are only references.
- The shape stays tall, 3 wide by 4 high. During the test phase, check whether this fits one of Amazon's print page sizes, and tell the user.
- **Copy, don't reinvent.** For each page, upload the old picture together with its prompt from the book's `art.md`, and ask for the same picture in higher quality. The pages listed under *Pages to fix* below are drawn again with the fix instead.
- **Test phase first.** On one book, take the cover and one busy page with several characters (lion-and-mouse page 5, which also needs its fix). Run both through every image model Gemini offers the user, one at a time. Put the results side by side on one private comparison page, with pixel size and file size. **Then stop and discuss the choice with the user.** The full run starts only after the user has picked a model.
- **Usage limit.** The user keeps the last 1% of their weekly Claude usage: check it with the `usage-check` skill (`node ~/.claude/skills/usage-check/usage-check.mjs --stop-at 99`) before every picture, and stop at exit 3. On 2026-11-10 it read 98%, resetting 2026-11-10 17:00 UTC. The session that runs the test phase stops when the test phase is done.
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

**Night scope (2026-11-12):** a headless night session builds only parts B and C (guard rails and rules). Parts A, D
and E need the user's Chrome and Gemini account and the user's model pick, so they wait for an interactive session;
the slice stays open after B and C merge.

**Premises checked (2026-11-12, ground-in-code on origin/main):** `.lfsconfig` has `fetchexclude = content/dino/*/audio/**`
(B1 adds `high-res`); `ship-targets.mjs:22-24` maps any `content/dino/<book>/**` change to `dinostore`, so B2 needs a
carve-out, tested in `ship-targets.test.mjs`; `BookHash.cs:42` and `StarterBuilder.cs:63` list a folder non-recursively by
extension (traced in B3: both are called on a book's `pictures` and `translations` folders only); `publish-store-books.mjs` reads only `book.json` and `audio-cast.json` inside a book folder (`join(CONTENT_ROOT, ...)`, lines 518-519 and 772), so `high-res/` is never read.

**Work:**

- [x] A. Test phase (one session, stops at the end):
  - [x] A1. List the image models Gemini offers the user's account, and note each one's maximum output size.
  - [x] A2. Draw lion-and-mouse's cover and page 5 (fixed) with each model, one chat per picture, and save the files to a scratch folder (not the repo).
  - [x] A3. Publish one private comparison page that shows them side by side, with model, pixel size and file size. Note whether 3:4 fits an Amazon print page size.
  - [x] A4. Stop and discuss with the user. Write their chosen model (and the output size to ask for) into this file.
    **User's pick (2026-11-13): 3.1 Pro** ("for the images, go with 3.1 pro"), chosen after round 3 (A5). Output size:
    none to ask for; every model returns 1792x2400 and the size line changes nothing. Pro sometimes lets a tail or paw dip
    below the 70% line (both page 5 tries in round 3), so every Pro picture gets the `line70.ps1` check and a redraw in a new chat when it crosses.
  - *Part A results so far (interactive session f7f59fb2, 2026-11-12, entries ~870-2439; copied here by the 2026-11-12-1337 feedback loop from that session's own reports, not re-checked; the pictures are in a temp scratchpad and a private comparison page):* the account offers 3.5 Flash-Lite, Hydrogen, 3.1 Pro, 4 Argon and 4 Carbon (Carbon is locked behind a GBP 79.99/month plan); "3.8 Flash" never appeared in about 16 page loads (followed by a CAPTCHA). Every model returns 1792x2400 with no size setting: sharp at 6x8 in at 300 dpi, about 210-240 dpi on larger Amazon trims. The first 3.1 Pro and Flash-Lite page-5 attempts were not real tests (the reference was sent with an empty prompt box; fixed by #1026) and were redrawn. The session recommended 3.1 Pro. User answer (2026-11-12): page 5's layout is fine ("there is no problem about text"); the fault to fix is the lion's legs. Page-5 redraws, as the agents and the session reported them (the main thread opened 4 pictures; it counted the legs on 4 redraws itself):
    - No model got everything right.
    - Hydrogen: the mouse faces left, towards the lion.
    - 3.1 Pro: the mouse faces left, so the action does not match the text, and the lion's back paw is unclear.
    - Flash-Lite: part of the action is missing.
    - The model pick, open at the time, was made on 2026-11-13: 3.1 Pro (A4).
    - The pictures are not kept anywhere durable. If the scratchpad is gone when A4 is picked up, redraw A2 before showing the comparison again.
  - *User answer (2026-11-13):* 3.5 Flash-Lite and 4 Argon are out; the pick is between 3.1 Pro and 4 Hydrogen. Pro's
    detail, drawing and bottom flowers are liked, but its page 5 put the Mouse's body, feet and berry and the Lion's front
    paws below the 70% line, where the app's words go. So the rule is now stricter: every character stays whole above the
    70% line (feet, tails and held things included), in `prompts.md`, SKILL.md steps 4-5, the illustrator and the reviewer,
    and the check measures it with `line70.ps1` (red line at 70%), never by eye. "This is important" (the user).
  - [x] A5. Next test, before the pick: page 5 again and one more page (page 2, copy mode: the Lion's paw down beside the Mouse,
    both low on the ground) on both 3.1 Pro and 4 Hydrogen with the stricter prompt, one chat per picture.
    Result (2026-11-13, checked by the main thread on the line copies): Pro page 5 fails, its Lion's tail tip ends about 73%
    (a second try was worse, about 75%); Hydrogen page 5 ends right on the line, about 70%; both page 2s pass, about 68%.
    Both models still draw the Mouse facing right, not looking back. All 4 legs on every Lion.
  - [x] A6. Before part D: `gemini-take-download.ps1` re-saves Gemini's download as JPEG q95 (about half the bytes), which
    breaks the decision "kept exactly as Gemini downloads it". Part D needs a save path that moves the file untouched.
    Fixed: `-Untouched` moves the download byte for byte (keeping Gemini's own file type) and only reads its size for the
    3:4 warning. Part D saves every high-res picture with `-Untouched`; the app's own pictures keep the JPEG q95 save.
- [ ] B. Guard rails, before the first picture is committed:
  - [x] B1. In `.gitattributes`: `content/dino/*/high-res/** filter=lfs diff=lfs merge=lfs -text`. Check that `.lfsconfig` keeps these files out of normal fetches and CI checkouts, as it does for audio.
  - [x] B2. In `.github/scripts/ship-targets.mjs`, make sure a change under `content/dino/<book>/high-res/**` maps to no ship target, so it doesn't start a store upload. Add a test case if the script has tests.
  - [x] B3. Check that `BookHash`, `StarterBuilder` and `publish-store-books.mjs` never read `high-res/`. Neither the app nor the store gets these files.
- [ ] C. Rules and quality checks (the user asked for this so the same faults never come back):
  - [x] C1. Write the one-prompt-per-chat rule (at most 3 picture requests per chat, ever) into `.claude/skills/create-dino-story/SKILL.md`, `prompts.md` and `.claude/agents/dino-story-illustrator.md`, replacing "a fresh chat every 3 pictures".
  - [x] C2. Add these checks to the full-size picture check in the skill and in `.claude/agents/dino-story-reviewer.md`. A picture fails if any of them fails:
    - every character has the right number of arms, hands, legs, paws and wings;
    - the scene fills the whole frame, with no plain white or empty strip at the top or bottom;
    - there are no words, letters or signs in the picture;
    - objects and tools have their real shape, not melted or twisted;
    - a page whose place matches the pages next to it repeats their background about 95% (same props, such as the berries); otherwise it is clearly a different place;
    - the picture doesn't contradict the story or put a character in danger the text doesn't have (a pig still building while the wolf is there);
    - the action matches the text exactly (a hug is a hug, not a bird sitting on a hat);
    - swimming animals are in the water, not standing on top of it.
  - [x] C3. Record the Good Enough reversal: `src/DinoReader.App/CLAUDE.md` line 28 ("never painted words"), Slice 146 line 48 and Slice 196 line 7 ("never redraw") each get a dated note that the decision was reversed on 2026-11-10 and points here.
- [ ] D. Full run, after the user has picked a model and the weekly usage has reset. Do one book at a time, and for each book:
  - [ ] D1. Draw the cover and every page (copy mode, or fix mode for the pages above), one thread, one chat per picture, on 3.1 Pro, saved with `gemini-take-download.ps1 -Untouched`. Check every picture at full size against the C2 checks and with `line70.ps1`, run on the candidate in the out folder before it is copied into `high-res/` (never leave a `.line70.png` in the repo); a picture with any part of a character below the 70% line is redrawn in a new chat. In round 3 Pro crossed the line on both page 5 tries, so expect redraws: at most 3 tries per picture, then keep the best one and list it for the user in the book's contact sheet.
  - [ ] D2. Run `dino-story-reviewer` over the book's high-res pictures. Its pass is required.
  - [ ] D3. Commit the book's `high-res/` folder (LFS) with a contact sheet for the user, and record the chats and sizes in the book's `art.md`.
  - Per-book progress (D stays open for the other books): **lion-and-mouse** D1, D2 and D3 done 2026-11-13 (16 files in `content/dino/lion-and-mouse/high-res/`: cover, 1-15 and `contact-sheet.jpg`; record in its `art.md`; pages 1, 4 and 13 decided by the user).
  - [x] **good-enough: finished 2026-11-14** (the user: "do it. finish the work"). The user had the background-character sheet rule reverted and the lost pictures recovered from their Gemini chat history instead of redrawn (113 downloads, none regenerated). Page 8's existing picture passed once background fans no longer had to match sheets; page 3 was the only redraw (new composition, line-up pasted, stadium in words). All 13 pictures are listed in `content/dino/good-enough/art.md` `## Accepted by the user`. Shipped as version 9: the 11-page dialogue text (Carrot, Hazelnut, Referee, Crowd; "front row"), 14 translations, audio in 15 languages, app copies made from `high-res/`, `textInPictures` removed, v8's page-text hash added to `EarlierPageTextHashes`. Full reviewer pass: PASS with non-blocking notes for the user (page 2's view is clear, page 3's far stands are empty, page 5 has no tall legs, page 9 is a different stadium, page 10 shows 4 players, the back cover's friends sit in the bottom 30%).
  - [ ] Listen check (not in unverified.md until the book ships): the crowd shouts "Goooaaal!" and "Ohhhhh!" in all 15 languages, and the new dialogue's wording for young children in the 14 translations.
  - [ ] Speaker attribution skips "the": `says the Referee` gave the Referee's lines to Carrot, so the book uses "says Referee" and "says Crowd" (noted in its uncommitted `art.md`). Fix the attribution in `src/DinoReader.Core` so "says the <name>" works, with a test, then decide whether the book goes back to "the referee".
  - [ ] Line-up tool: Gemini drew each line-up row at its own scale three times, so the true-size line-up was cut and rescaled by a scratch script (connected regions, one height per character in Carrot units). Turn it into `.claude/skills/create-dino-story/true-size-lineup.py` (or a PowerShell twin) and name it in SKILL.md's line-up step.
  - [ ] Guard script (from the 2026-11-14 feedback loop): the save step (`gemini-take-download.ps1` or a new `save-page.ps1` in `.claude/skills/create-dino-story/`) refuses to write a page listed in `art.md`'s `## Accepted by the user` list unless called with `-UserAskedRedo`, and copies any file it replaces to `<working folder>/previous/` first. Verify: save over an accepted page without the flag; it exits non-zero and the file is unchanged.
  - Book order (the user, 2026-11-14): freddy-and-the-slipper next ("do freddy"), then jack-and-the-beanstalk ("after freddy do Jack and the Beanstalk"). Each starts by recovering its earlier pictures from the Gemini chat history (SKILL.md rule 3).
  - Freddy recover run, 2026-11-14: 29 pictures from 13 of 17 found chats (scratchpad `freddy-recover/index.md`), stopped by a Google CAPTCHA at 22:41 after a burst of new-tab downloads. Left: chats 13 (third picture) to 17 and the "slipper"/"raccoon" searches. On small copies: 9, 10, 11, 12 pass; 6, 7, 8 borderline; cover, 1-5, 13, 14 have no passing picture yet. Tooling item: Chrome wrote only one download per tab (the second failed silently), which is likely Chrome's multiple-downloads block; the save step needs a way that doesn't need a new tab per picture.
  - Freddy recover part 2, 2026-11-15: all 17 chats and both searches done, 38 pictures. The illustrator passed c15-1, c15-2, c14-2 and c14-3 for cover, 1, 4 and 5, but the coordinator's look at the untinted contact sheet shows each has the plain white bottom the user flagged (the line70 tint hides it). Tooling item: line70 checks must also read the untinted small copy for a white or empty bottom. Kept from history (untouched downloads): 8 c13-3, 9 c04-1, 10-12 from chats 11 and 6 (reviewer to confirm which). Redraw on 3.1 Pro: cover, 1-7, 13, 14, in groups by place (cover+1 meadow; 2, 3, 14 evening kitchen; 4, 5 bedroom; 6, 7, 13 night kitchen), one group at a time.
  - Token cost, measured 2026-11-15 from the session logs (the user: "why the hell the biggest one used 15-20m?"): an illustrator agent costs about 5.5M tokens per picture. It starts at about 70k (it reads the slice file, the agent file, art.md and the whole gemini-grab.js), takes about 22 steps per picture (lock, two pastes, two attachment checks, send, wait, lock, grab, save, two Reads, tab close and reopen), and every step resends the whole context, which grows about 20-25k per picture to 200-260k. The new Gemini chat per picture doesn't shrink the Claude agent. Pictures themselves are about 3% of it; no full-size picture was opened after the small-copy rule. Tooling items: (a) one illustrator agent per picture, with a brief that names only that page's lines; (b) the agent doesn't read gemini-grab.js or the whole slice file; (c) one script call covers lock, paste check, send, wait, grab, save and small copy, so a picture takes about 6 steps, not 22. Measure the per-picture tokens again after the change. (d) Recover runs: the Good Enough recover agent (20M, 125 steps) spent 15 steps on bare `sleep` calls and about 60 on driving one chat at a time; the wait between downloads goes inside one in-page script call per chat (it lists every picture in the chat, downloads them about 20 s apart and returns the list), so a chat costs about 3 steps. Jack and the Beanstalk's recovery is the first to use it. (e) The feedback-loop audit (ClaudeSkills `feedback-loop`, 18M for pass 1): one auditor read the whole digest in 28 parts and ended at 370k; split the digest across 3-4 auditors, each with its own part, so none grows past about 130k. Built 2026-11-15 (the user: "do them"): (a)-(d) in PR #1193 (about 7 steps per picture, `gemini-step.ps1`, `__drawPicture`, `__modelLabel`, `__recoverChat`), (e) in ClaudeSkills #231. Not yet run against Gemini: the Freddy redraw of 13, 6, 7 is the first live run; measure its tokens per picture against the 5.5M above. First live run, page 13 (2026-11-15, chat a948cdf65c37a698, first try, 2724552 bytes, saved in scratchpad `freddy-redraw/g6/`): 25 agent calls, 2.77M tokens (context 71k to 131k), about half. One paste attached both pictures; prepare, save, __modelLabel worked. Two faults to fix in one tooling PR after Freddy: (f) `__drawPicture` returned a 79 KB webp preview's size, not the full jpeg's, so `save` found no download until a second `__grab(1)`; it must wait for the full-size jpeg. (g) The loader answered STALE (the stored copy predates the hash) and the agent then read and injected a comment-stripped copy, storing a wrong hash; the fallback should inject the file unaltered without reading it into the agent (a script that prints nothing back), and the 71k starting context is the next cut. Page 6 (chat 6b34782efd098d01, try 3 of 3, 2561222 bytes): passes the coordinator's look at the small copy (the white panel beside the fridge is its open door). Tries 1 and 2 (chats 8aac22a32d7676cf, bbcb1949246f31e2) did not show the pot's inside. The page 6 agent again injected a comment-stripped copy (hash 01f04f85) and needed `__grab(1)` after every `__drawPicture`; it also pasted once before the prompt box had focus (focus by script first). Page 7: try 1 (13 calls, 1.2M tokens, no re-inject) pointed at the pot; try 2 (chat 6965297be8919f81) accepted. The full review of the whole book passed on 2026-11-15 (record in the book's art.md). `high-res/` now holds the flat JPEG 85 pack that `HighResPack` reads (`high-res/<n>.jpg`, made by `prepare-story-pictures.ps1 -HighRes`), not the untouched `high-res/pictures/` files from the WIP commit: those would have built no high-res pack. Untouched downloads stay in the scratchpad `freddy-all/`.
  - Books: billy-goats-gruff, elves-and-shoemaker, freddy-and-the-slipper, gingerbread-man, goldilocks, good-enough, jack-and-the-beanstalk, lion-and-mouse, little-red-hen, red-riding-hood, three-little-pigs, tortoise-and-hare, town-mouse-country-mouse, ugly-duckling, x-marks-the-treasure.

- [ ] E. App copies (the user, 2026-11-10: yes). After the high-res run, the fixed pages (every page under *Pages to fix*, Good Enough included) replace the app's and store's own pictures as smaller copies made from the high-res files, through the normal picture-preparing step and a version bump per book. Good Enough's Picture-only default (Slice 196, `textInPictures`) is removed once its pictures have no text.

**Verification:**

- `git lfs ls-files` lists every `high-res` file, and `git check-attr` shows `filter: lfs` for one of them.
- `ship-targets.mjs` gives no target for a path under `high-res/`, and BookBuilder `check` gives the same book hashes before and after.
- Each book's contact sheet and its `dino-story-reviewer` pass are recorded in its `art.md`. Every fixed page is shown next to its old version.

*Changed while building:* B1 adds the LFS rule in `.gitattributes` and `high-res` to `fetchexclude` in `.lfsconfig`. B2 carves `content/dino/<book>/high-res/**` out of the `dinostore` group in `ship-targets.mjs` (tested in `ship-targets.test.mjs`) and adds `!content/dino/*/high-res/**` to the `dino-store.yml` push paths; `tools-selftests.yml` still runs on a high-res PR, so such a PR has a check to wait on. B3 was traced (see *Premises checked*) and is guarded by the C# test `AHighResFolder_ChangesNeitherTheHashNorThePictureList`. C1: one picture per new chat; a patch of a bad result may continue the same chat only while it is at or under 3 requests. C2: the eight checks are in `SKILL.md` step 5 and in the reviewer agent. C3: the reversal notes are written.

*From review:* high (`reviews/pr-1013-high.md`): all 9 findings applied. Medium (`reviews/pr-1013-medium.md`): findings 1, 4 and 5 applied; 2, 3, 6 and 7 skipped:
- Skipped #2 (`reviews/pr-1013-medium.md`): "High-res exclusion test is near-tautological" - low test-strength note; the C# fixture test is the guard.
- Skipped #3 (`reviews/pr-1013-medium.md`): "the PictureFilesIn assertion cannot fail" - low test-strength note; the BookHash assert and the C# fixture test are the guard.
- Skipped #6 (`reviews/pr-1013-medium.md`): "Touches lists publish-store-books.mjs" - Touches wider than the diff is harmless.
- Skipped #7 (`reviews/pr-1013-medium.md`): "Exclude is on the dinostore group only" - the `.lfsconfig` `fetchexclude` covers `high-res`, so a dino-store checkout never fetches the files.
check1 (`reviews/pr-1013-check1.md`): findings 1, 7 and 10 applied; the rest, one line each:
- Skipped #2 (`reviews/pr-1013-check1.md`): "Regex 'content/dino/' still matches high-res-only PRs, so the audio job and LFS pull run for nothing." - low CI cost.
- Skipped #3 (`reviews/pr-1013-check1.md`): "Guard test greps source text for join(CONTENT_ROOT; 'entry.name' on the line lets a high-res read pass" - low test-strength note.
- Skipped #4 (`reviews/pr-1013-check1.md`): "High-res exclusion is a push path filter only, duplicated with ship-targets.mjs exclude" - low; duplication is minor.
- Skipped #5 (`reviews/pr-1013-check1.md`): "both listings are non-recursive on pictures/, so the test cannot fail" - low test-strength note; the behaviour is non-recursive by design.
- Skipped #6 (`reviews/pr-1013-check1.md`): "States PR #1013, "all 9 findings applied" and review file paths that the diff cannot show" - the slice file records review history by design.
- Skipped #8 (`reviews/pr-1013-check1.md`): ""Repeats the background about 95%" is not measurable" - a reviewer judgement by eye, the user's own wording.
- Skipped #9 (`reviews/pr-1013-check1.md`): "a High-Res/Hi-Res folder is neither excluded nor LFS-tracked (case-sensitive)" - the folder name is fixed by the slice as `high-res`.

#### For CLAUDE.md (lifted 2026-11-12)

None beyond what the slice already edited (`.github/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, the create-dino-story skill).

#### Not yet verified

- The first real high-res push on `main` starts no `dino-store` run (needs part D).
- `gemini-take-download.ps1 -Untouched` on a real Gemini download (tested on a copied file only, md5 identical; the first part D picture exercises it).
