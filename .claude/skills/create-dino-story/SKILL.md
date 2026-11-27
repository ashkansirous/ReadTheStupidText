---
name: create-dino-story
description: Make or illustrate a Dino Reader book - gentle story text for ages 3-9, cast descriptions, a cover and one picture per page drawn by driving the Gemini web app in the user's Chrome, a quality check of every picture on its small copy, then build, test, upload and set up its sale product in every store. Use when the user says "make a Dino story", "illustrate <story>", "add pictures to <story>", "add a story to the store", or works on Slices 119-121.
---

# Create or illustrate a Dino Reader story

Dino Reader's readers are **3 to 9 years old**. Two rules beat everything else in this skill, including
the classic tale and Gemini's output (Decision 130, Slice 119):

1. **Nobody gets hurt.** No eating, burning, boiling, drowning, beating, dying, falling to death, or
   threats of any of it; no fire or hot water near a character; no "licks his lips". Conflicts end
   gently: cold bubbly water, a fright that turns into a laugh, a sorry, running home, a new friend.
2. **No picture is accepted without the check on its small copy** (step 5). Nobody, the coordinator included, ever opens a whole original at full size (Slice 375): look at `<name>.small.jpg` (896x1200), and open a full-size crop only for one named fault. A rejected picture is never kept: candidates are saved only to be checked, and rejected ones are deleted.
3. **A picture the user accepted is theirs: never redraw it or save over it unless the user names that page.** When the user accepts a page ("OK", "accepted", "that one is fine"), write it in the same turn in `art.md`'s `## Accepted by the user` list (page, file name, size in bytes, date) and in the slice file. Every redraw brief lists those pages as do-not-touch, and a new rule (new character sheets, a new crowd) changes only pages not on the list unless the user says otherwise. Before any picture is saved over an existing file in `high-res/`, `pictures/` or the book folder, copy the old file to `<working folder>/previous/<name>` first. `gemini-take-download.ps1` enforces both (Slice 447): it refuses (exit 1, file untouched) a target listed in the nearest `art.md`'s `## Accepted by the user` table unless called with `-UserAskedRedo` (use it only when the user named that page), and copies any picture it replaces to the working folder's `previous/` first (always pass `-BackupFolder <working folder>/previous`; a target inside a book folder, one with an `art.md` above it, is refused with exit 1 and nothing touched when it is missing, and a `cand-*` target without it backs up to `previous/` beside it; never inside `content/` or `high-res/`). The list may be a table or bullets (any picture file name in the section counts, heading matched by prefix); a section with entries but no readable file name refuses every save, so fix the list. `gemini-step.ps1 save`/`collect` take the same `-UserAskedRedo` and `-BackupFolder`. A count of "missing" pages is read from that list and the folder, never from the last review. The list holds only the user's own words: a coordinator's "done and accepted" status line is not an acceptance until the user says so. Two more round rules from the same book: check each page against its plan line the moment it is saved and redraw it before starting the next, and compare every new place sheet with the book's other place sheets (same seats, walls, lights) before accepting it. (2026-11-14, Good Enough: a round I scheduled redrew nine pages the user had accepted and saved over them with no copy; the user's 2 missing pages became 10 until the scratch copies were found.)
   **Recover before you redraw.** Every picture Gemini made stays in the user's Gemini chat history. Before redrawing a page that is missing or was lost, open the book's earlier chats (the sidebar list at `https://gemini.google.com/app`), download the pictures that page already had, and check them on their small copies; redraw only a page whose every earlier picture failed the review. (The user, 2026-11-14: "no need to regenerate them. you should only regenerate the ones that was failing a review".)

Every book is one folder, `content/dino/<stem>/` (Slice 157): `story.md`, `book.json`, `pictures/`, `cover.jpg`, `cover-thumb.jpg`, `translations/`, `art.md` and `characters/`.
Per-book prompts, cast and check results live in `content/dino/<stem>/art.md`.

## Who does what

The main thread coordinates and never drives Gemini or writes a book's text itself; that keeps its
context free of screenshots, pictures and 14 translation files.

1. **`dino-story-writer`** (Opus, high effort): steps 1-2 with the `dino-storytelling` skill. Several may
   run at once, one book each, in their own worktrees.
2. **`dino-story-reviewer`** (Opus, high effort), **text** mode: safety, story quality for the age, one
   action per page, the picture plan. Failed pages go back to the writer, then review again, before any
   picture is drawn, since pictures cost Gemini quota.
   **Voices start right after this text review passes, not after the pictures** (the user, 2026-11-18: Love from Seagull
   Island stopped for hours mid-morning because the voice page went out only after every picture was drawn, while the user was
   away). In the same turn that the English goes to the user for their read, dispatch the `voice-pick` agent for every speaker
   and the narrator, and send the user both together: "read the English, and pick the voices". The cast rarely changes with a
   text edit, and clips of unchanged lines are reused. Once the user accepts the text, compare its speakers and tagged lines
   (a new speaker, a chorus line such as "they said together", a renamed character) with the picks, and ask about every new
   voice decision at once, in that same turn, never later. With the picks in, render the English audio while the pictures are
   drawn, and each translation's audio as soon as it is written, so nothing waits on the user at the end.
3. **`dino-story-illustrator`** (Sonnet, medium effort): steps 3-6. **Two at once across the machine, on separate books or on page groups of one book** (the user's choice,
   2026-11-17, after two of four tabs got stuck on Love from Seagull Island; it was four from 2026-10-26; page groups 2026-11-01; the user may raise it again). **Each illustrator opens its own tab with `tabs_create_mcp` and uses only that `tabId`**: never a tab `tabs_context_mcp` lists or hands it (other illustrators' chats are there; 2026-11-17 one was handed two foreign tabs), and it never closes a tab or tab group it didn't open (one closed another's tab group the same day). What makes it work: every illustrator acts only on its own `tabId`; prompts go in with
   `javascript_tool` (`execCommand('insertText')`) and Send is only `await window.__sendChecked(<prompt>, <pictures>)`
   from `gemini-grab.js` (it refuses an empty or partial prompt box or a missing line-up, and reports a sent message
   without the prompt; that chat is spoiled: new chat, never patch it), never keyboard typing or
   Enter (keystrokes land in whichever tab has focus); `gemini-lock.ps1` is held only around the line-up paste and
   the save; each illustrator closes its own tab when done (reset tab groups left ~20 orphans). Four in parallel
   drew about 39 accepted pictures an hour against 15-25 for one, but the limit is two (user, 2026-11-17, confirmed 2026-11-25). If Gemini answers 1095 or shows a CAPTCHA, report
   it; at most 3 reloads to look for a model or menu item that isn't there, then report it missing; never reduce the number of illustrators without asking the user. It ends with `contact-sheet.jpg`, every picture on one sheet.
   **One book is drawn by 2 illustrators at once whenever the machine has room** (the user, 2026-11-01: pages
   hardly depend on each other; each chat only needs the line-up and its page descriptions). The cap of two
   counts across the machine, so with two books in flight they share it (1 + 1). Three phases:
   - **Setup, one illustrator (`setup` mode, SKILL.md step 3's character chat, step 2 in the agent file):** the character sheets and line-up, plus one **place sheet** per
     setting used on several pages (kitchen, garden, bedroom...), drawn in the same chat right after the line-up, saved as
     `characters/place-<slug>.jpg` and listed in `art.md` with the pages that use each. Place sheets keep the
     same room or garden across parallel chats (Freddy and the Slipper drew three different gardens in three chats).
   - **Pages, 2 illustrators (`pages` mode), each with its own page group** (cover + pages 1-7, 8-14 ...):
     each pastes the line-up and the place sheet its pages use (two pictures: `Put` the line-up and paste, wait for the thumbnail,
     then `Put` the place sheet and paste, all inside one lock hold), works in its own tab with the lock owner
     `<stem>-<group>`, the group label a simple word without commas (`g1`..`g4`; the brief lists each group's pages), saves candidates to its own sub-folder of the working folder and writes its check rows to
     `<working folder>/<group>/checks.md`, never to `art.md` (one writer per file). Pages that really depend on
     another picture (the same pose continued, an object handed over) go in the same group. **No group is more than about 5
     pictures** (Slice 356): the context an agent re-reads grows about 15k per picture, so a long book is split into more groups,
     each a fresh agent that stops and reports when its group is done; run them as the four slots free up.
   - **Finish, one illustrator (`finish` mode):** first checks that every page and the cover has an accepted picture (from this run's groups, or the repo's existing ones for pages not redrawn) and stops naming any missing page, then merges every group's check rows into `art.md`, runs
     `prepare-story-pictures.ps1`, the contact sheet and `stamp`, then the full review runs as usual. Redraws after
     the review are split into groups the same way when there are more than three; each redraw group has at most three
     pictures (the redraw rule stays at three, below the general cap of 5 above).
4. **`dino-story-reviewer`**, **full** mode: every picture's small copy (a full-size crop only for one named fault) against its page and the line-up,
   and characters, places and time of day consistent across the book. Failed pages go back to the
   illustrator (or the writer), then review again until `PASS`. The coordinator may glance at the contact
   sheet but never accepts a book from it. Then step 7 goes through the usual gate and ship.

The reviewer never reviews its own work and changes nothing. Its pass is required before a book ships;
the illustrator's own step 5 check still runs on every candidate.

Version bump (starters only; Slice 239): the writer bumps it when the text changes; the illustrator bumps it when only pictures change
(`dotnet run --project tools/dino/DinoReader.BookBuilder -- stamp <stem>` raises `version` in the book's `book.json` and stores its content hash; CI's `check` fails when a starter changed without it).
A **store book needs no stamp**: `publish-store` compares its content hash with the live store and uploads a changed book as live + 1. A store book is content only: it never edits `src/`, `tests/`
or app files (its titles are `content/dino/<stem>/titles.json`, no row in `ShippedTitles.cs`; tests derive book counts from the folder), so its PR and merge run no test, no app build and no
PR check, only the store upload. No book is bundled in the app any more (Slices 261, 265), so every book change, audio clips included, runs only the store upload. Deleting a store book's folder delists it on the next merge (packages stay).
**A book reaches the store only with complete audio (Slice 277):** `dotnet run --project tools/dino/DinoReader.BookBuilder -- check-audio <stem>` must print no gap (after `git lfs pull --include "content/dino/<stem>/audio/*/manifest.json" --exclude ""`):
a pack in every one of the 15 languages, a clip for every sentence the app reads, no clip for text that changed, no missing or empty clip file. The app has no device-voice fallback, so after any text
change regenerate the clips with `tools/dino/premade-audio` before the PR (a whole book renders three languages at a time, each with its own `--out`: the premade-audio README, *Batch mode*). `publish-store` and `tools/dino/upload-store-story.ps1` refuse a book with a gap (`-CheckOnly` runs just the check); `check-audio --live` checks what the live store serves.
**The listening track is built on this PC and committed (Slice 409):** after the clips (or any change to them), pull them (`git lfs pull --include "content/dino/<stem>/audio/**" --exclude ""`) and run `dotnet run --project tools/dino/DinoReader.BookBuilder -- build-listen-all <stem>` (build-audio, then the `listen-<lang>-v<n>.m4a` track of every language, ffmpeg from the PATH); commit the tracks and `audio-cast.json`. `check-audio` fails a book whose track is missing or older than its clips, and CI installs no ffmpeg. **The work that makes existing books' store files stale (a new track, a pack format, a picture size) rebuilds, commits and uploads every affected book in its own PR and release run; a later unrelated upload never carries its backfill.**

Helpers in this folder: `gemini-grab.js` and `gemini-take-download.ps1` (step 6, save a picture),
`gemini-clipboard.ps1` (paste the line-up; saving through it is only the fallback), `gemini-lock.ps1` (two
illustrators sharing the clipboard and Downloads), `gemini-keep-awake.ps1` (screen on while drawing), `contact-sheet.ps1`, `true-size-lineup.py` (line-up step) and `accepted-pages.ps1` (the save guard `gemini-take-download.ps1` loads).

## Board card and steps (Slice 431)

The coordinator opens **one card per book** before any work and keeps its steps current, so the agent board's process
optimiser can see the book's flow. **Reports fail open: a board error, a missing token or an unknown flag is skipped and
never stops, delays or fails a book; never wait on a report.**

- Card: `report_progress` (the `agent-board` tool) with key `readthestupidtext/task-<id>` (`<id>` a short random id, the same for the whole book), type `unplanned`, agent `session`, status `in_progress`, title "Dino book <stem>", outcome "Book <stem> shipped", why "A new Dino Reader book in every store", and `steps` as below. Resend the whole list at every step change.
- Steps (id = the process key with `-` for `/`; one `process` key each, the same key for the same kind of step in every book):

  | Step id | Process key | Who reports it |
  |---|---|---|
  | `story-write` | `story/write` | writer |
  | `story-text-review` | `story/text-review` | reviewer (text) |
  | `story-voices` | `story/voices` | voice-pick (starts with the owner read, in parallel) |
  | `story-owner-read` | `story/owner-read` | coordinator (active at the same time as `story-voices`) |
  | `story-translate` | `story/translate` | writer (translations; only after the owner read is done) |
  | `book-illustrate-setup` | `book/illustrate-setup` | illustrator (`setup`) |
  | `book-illustrate-group-g1`, `-g2`, ... | `book/illustrate-group` | illustrator (`pages`, one step id per group, created by the coordinator and named in that illustrator's brief) |
  | `book-illustrate-finish` | `book/illustrate-finish` | illustrator (`finish`, or `all`) |
  | `book-review`, `book-review-r2`, ... | `book/review` | reviewer (full; **a rerun after a FAIL gets a new step id and label** such as `book-review-r2` / "Review 2", so the first run's `findings` count is kept; the same for `story-text-review-r2`, and any redo of a step) |
  | `book-build` | `book/build` | coordinator |
  | `book-store-product` | `book/store-product` | coordinator (step 7b) |
  | `book-farsi-review` | `book/farsi-review` | farsi-review (a later, separate flow, Slice 154; the coordinator adds the step when it starts the review and names it in that brief) |

- **`story/owner-read` is sent as a `blocked` step with `waits_on: "owner"`** and a note naming what it waits on, from the moment the English goes to the user until they approve it; put the question in the card's `needs_you` ("Read the English of <stem> and pick the voices", with the link) and its `after_answer`. **While the question is open the coordinator resends the same `needs_you` and `after_answer` in every `report_progress` (a report without them clears the question), keeps the step `blocked` with its note, and reports at least every 5 minutes.** The turn the user approves it sends `needs_you: null` and sets the step `done`.
- **`story/translate` starts only after `story/owner-read` is done** (rule of 2026-11-17, step 1 below): never mark it active, and never dispatch the writer's translation round, before that. The optimiser's "work before approval" check reads this order; this flow is its negative case.
- Each agent reports its own step with `--under-step <step id>` as a child of the card (the brief names the card key and the step id), through `node ~/.claude/skills/run-wave/board.mjs report-progress --key "<card key>" --under-step "<step id>" --steps "<label>" --step-active|--step-done "<label>" --step-process "<label>|<process key>" [--counts "findings=N"]` (run in the Bash tool, where `~` expands; the done report repeats `--steps` and `--step-process`; the plugin sends nothing for `--under-step` without a step list). The coordinator puts `Board parent: <card key>` and `Board step: <step id>` in every agent brief; with none in the brief the agent reports nothing.
- Counts: the reviewer sends `findings` (the number of failed pages) on its done step; `farsi-review` sends `findings` and `fixed`.
- At the end (book live and product set up) mark the card done (`mark_done`).

## 1. Text

- The text is `content/dino/<stem>/story.md`; `book.json`'s `kind` says whether the book is a former starter (`"Starter"`: served by the store like the rest, also kept as a Debug-only test book in the app, and still stamped) or a store book.
- **Write or retell the text with the `dino-storytelling` skill first**: one action per page, 10-18
  pages, split on lines holding only `---`, dialogue as `"...," said <Name>.` so speaker attribution
  casts voices.
- Read every page against rule 1 before anything else. Rewrite what fails.
- Give every character a voice choice in the `cast` of the book's `book.json` (Slice 156; `dotnet run --project tools/dino/DinoReader.BookBuilder -- dump-voices` prints a mechanical starting point): a suggested roster id, gender, age, what it is and voice qualities; record them in the art record.
- **Back cover (Slice 237).** Every book ends on a silent credits page that is never read. The builder makes it from `book.json`: set `storyBy` (who wrote the story, names as written;
  `"traditional"` for a tale nobody owns) and `retold: true` for every book that is not the user's own. Own books: `storyBy` is the user's name (plus a co-author) and no `retold`.
  A classic needs its real original author, checked, never guessed (Potter, Aesop, Andersen, the Brothers Grimm...). The illustrator is Ashkan Sirous unless `illustration` says otherwise,
  and the month is the book's `added` date. Nobody types the page into `story.md`; `check` fails a book without `storyBy`. It shows as the last page in every reading mode (an older app reads it aloud).
  **Slice 405: every name on it is written in the reading language.** `book.json` gets `namesByLanguage`: `{ "<lang>": { "<name as written in storyBy / the maker / illustration>": "<spelling>" } }` for
  each of the 14 non-English languages and every name the back cover shows (each `storyBy` name, `Ashkan Sirous` for a retold book and the illustration, nothing for `"traditional"`). Take a name from the
  same book's `creditsByLanguage` first, else the usual form in that language (de "Äsop", fr "Ésope", fa «ازوپ»); write it so it fits the app's label ("Story by {0}", so "den Brüdern Grimm", not "Brüder Grimm" after "von").
  A Latin-script language keeps a personal name as written unless it has its own form. The builder (`Books.ReadInfo`) refuses a book that misses a name or language; `stamp` a starter after.
  **Slice 359: every book also gets a drawn back-cover picture** (one per book, no words in it, the middle left calm because the reader lays the credit lines over it): `content/dino/<stem>/back-cover.jpg`
  (app size) and `content/dino/<stem>/high-res/back-cover.jpg` (the untouched Gemini file, LFS). `check` fails a book without `back-cover.jpg` (Good Enough alone until it is redrawn). Draw it with the
  `prompts.md` *Back cover* prompt in the style that fits the book; compare it with the book's story and its own pictures, not only its look (see the illustrator and reviewer rules).
- Translations, **only after the user has read and approved the English** (2026-11-17: a new book was translated before the user's read, then rewritten, and all 14 translations were lost). A new book's first round is English text and the picture plan only; the coordinator shows the user the English, and the writer translates once the user says it is right. `dotnet run --project tools/dino/DinoReader.BookBuilder -- dump-pieces <stem>` writes `translations/pieces.json`;
  translate into `content/dino/<stem>/translations/<lang>.json` (every language in `UiLanguage.Supported`, 14 besides English).
- Never move a `---` once pictures exist: the build fails when picture and page counts differ.

## 2. Art record

Create `content/dino/<stem>/art.md` with:
- The house style line (Decision 126): `Children's picture book illustration, soft watercolour and
  coloured pencil, warm gentle colours, simple rounded shapes, no text or letters anywhere.`
- One line per character: species, size relative to the others, clothes and colours, one prop.
  Name the distinguishing detail of each look-alike (three pigs: hat, scarf, dungarees, beanie).
- One scene per page: who is in it (with a count), what they do, where, time of day.
- A table for the check results (step 5).

Prompt templates: [prompts.md](prompts.md).

## 3. Gemini setup (Chrome)

- Use the claude-in-chrome tools on `https://gemini.google.com/app`. The user is signed in (free tier).
- **Gemini mixes scenes and outfits from earlier pictures in the same chat** (2026-10-25 pigs pilot: a
  page-3 wolf in page 5's bathroom, trousers appearing on pigs, a 4th pig). Never draw a book in one chat:
  - **Character chat** (one per book): one reference sheet per character, then a line-up of the whole
    cast. Save the accepted line-up and sheets to `content/dino/<stem>/characters/`; they are
    the reference every page chat gets.
    Sheets are for the story's named characters only. **Background extras (crowds, fans, players, villagers) get no
    sheets and no line-up place**: describe them in the page prompt in words ("a crowd of animal fans, no people, none
    looks like Carrot"). (The user, 2026-11-14, reversing the 2026-11-13 "draw every background group front and back"
    rule: Gemini handled the extra sheets badly and the Good Enough rounds got worse, not better.) The line-up shows the
    named cast side by side at true relative size so Gemini knows how big each one is. No page is drawn until the sheets
    pass the illustrator's own check; nobody waits for the user to approve them (the user, 2026-11-13). Still being tried: whether sheets stay more consistent in one
    chat or with a new chat every 2-3 characters. Note in the art record which you did and how it went,
    and update this line once it is clear.
    **True-size line-up (Slice 447):** Gemini draws the whole row at about one height. After the row is saved, run
    `python -I .claude/skills/create-dino-story/true-size-lineup.py <row.jpg> <lineup-true-size.jpg> --names Carrot,Mouse,... --heights 1,0.3,...`
    (names left to right, heights in Carrot units, Carrot = 1; Pillow only). It cuts the row into connected figures, rescales each
    to its own height on one baseline and saves the result; if it finds a different number of figures than names it writes
    nothing and prints the boxes (exit 2): tune `--gap`, `--bg-tol` or `--min-area` and rerun. Paste the true-size file as the line-up.
  - **Page chats**: **one prompt per chat**: every picture gets its own new chat, so one picture can't influence the next
    (the user, 2026-11-10, Slice 350; all Dino illustration work). A redo of a
    bad result also gets a new chat; the only exception is a small patch of a bad result, which may continue that chat while the
    chat stays at or under 3 picture requests (the patch counts as one). Past 3, or if the bad picture would steer the patch, start a new chat. Start each by giving it the line-up (plus the sheets of the characters on those pages), then
    "These are the characters. Keep their outfits exactly as shown", every outfit spelled out
    (including what they do NOT wear: "no shirt", "no trousers", "no bobble"), then one prompt per
    page. Gemini adds no file input until its native picker opens, which can't be driven, so **paste**
    the picture instead: `pwsh -STA .claude/skills/create-dino-story/gemini-clipboard.ps1 Put <line-up file>`,
    then find the prompt box (`find`, not a guessed position), click it, press `ctrl+v`.
  - If a chat draws an earlier scene again instead of the new one (pigs pilot: the second picture of a
    fresh chat repeated page 2 for page 6), that is why it is one picture per chat now.
    A fresh chat with the line-up pasted got every outfit right first time.
  - The art record's character lines are the canonical outfits, written from the accepted line-up,
    not from the first prompt (the pilot asked for a "red cap" but the line-up drew a knitted beanie;
    later prompts must follow the picture).
- **Image model: default to 3.5 Flash-Lite in the text-model picker** (except Slice 350's high-res run: Flash-Lite and 4 Argon are out there, and the model is the one written in that slice's part A4; a measured default, not a law; usage numbers and the failed Flash attempts are in `content/dino/lion-and-mouse/art.md`). The Images tool has no model menu; the text picker decides what draws. Flash-Lite cost about 0.3% of the 5-hour window per picture against +4% on 3.8 Flash, same 1792x2400 size and quality, and 3.8 Flash refused pictures with a pasted line-up. Set the picker before the first prompt (it persists across chats); keep 3.8 Flash for text-only prompts. If Flash-Lite gets the same picture wrong twice in a row, try 3.1 Pro for that picture and note its cost. Re-measure at each book and record each picture's size in the art record. The limit is compute-based (5-hour window plus a weekly cap, Settings > Usage Limits).
- **Per picture, one script call** (Slice 356): after the line-up paste is confirmed (`window.__attachmentCount()` equals the
  pictures pasted; the lock is held only from `Put` to that confirmation), `await window.__setupPicture(<prompt>, <pictures>)`
  opens **Upload & tools**, picks *Create image*, sets **Aspect ratio 3:4** (the control reads `Aspect ratio, 3:4` once set), puts the
  prompt in, checks its length and sends through `__sendChecked`. It finds every control by its label, never by position, and returns
  one line (`ok: create-image, 3:4, 1 attachment, prompt 812 chars, sent`). It re-checks Create image and 3:4 right before sending
  (after the prompt fill), never trusts a Create image chip that was already showing (it deselects it and picks the item again
  in the same call), and answers `MISSING: composer area` when it can't find the prompt area to count attachments
  (`__attachmentCount()` then returns `UNKNOWN`, never 0 or the expected number): screenshot once and go click by click.
  `{ dryRun: true }` sends nothing but leaves the chat in Create image + 3:4 with the prompt filled: clear the box
  (`selectAll` + `delete`) and deselect the Create image chip before any text-only message, or start a new chat. No screenshot is taken before 3:4 or after an `ok`
  line; look (screenshot or zoom) only when the line reports a problem. `MISSING: <item>` means a control wasn't found: that one
  picture goes click by click, one `find` ref at a time (the old steps: **+**, Create image, Aspect ratio, 3:4, a zoom to confirm
  3:4, `__sendChecked`). Pictures come out 896x1200 (1792x2400 on the Pro plan); a misplaced click once gave a 4:3 picture (Billy Goats), which
  is why the script confirms 3:4 before sending.
- **No coordinate clicks on a new chat page.** Its suggestion chips sit right under the prompt box, and a
  click on one sends a prompt you didn't write (three stray chats so far: "Script Doctor Refusal", "The Art of
  Doing Nothing", a film-history quiz). Click only through `find` refs (prompt box, Upload & tools, Create
  image, Aspect ratio, 3:4). Coordinate (`computer`) clicks are banned (saving needs no real click any more: step 6).
  If a stray prompt goes out anyway, say so in the report.
- In a fresh chat the first `ctrl+v` after clicking the prompt box often pastes nothing: click the box
  twice, then paste, and check the thumbnail appeared. Wait for the page to finish loading before any click.
- The free-tier image limit is shared with every other session drawing in this Chrome profile, and so are
  the clipboard and the `Downloads` folder. Check the open tabs first; another illustrator drawing in its own tab, on another book or another page group of this book, is normal (at most two): carry on, and use `gemini-lock.ps1` around the clipboard and download moments.
- **Keep the display on while drawing** (2026-10-26): with the screen merely off (not locked) Chrome stops rendering, so Gemini
  never finishes loading, pastes do nothing and screenshots time out. The caller starts
  `pwsh gemini-keep-awake.ps1 -Hours <n>` in the background before any illustrator runs (a temporary Windows request, no
  setting changed) and stops it afterwards by creating `Downloads\.gemini-keep-awake.stop`. An illustrator whose Gemini tab
  hangs stops and reports it rather than retrying.
- **The Windows clipboard fails while the PC is locked** (`Requested Clipboard operation did not succeed`, HRESULT 0x800401D0; Lion and the Mouse, 2026-10-26 00:15). Gemini still draws and saving through the download (step 6) still works, but the line-up can't be pasted into a new chat. Check first: if `LogonUI` is running, stop before the next paste and report; the caller rechecks every 15 minutes until the user unlocks. (Uploading the line-up as a file was tried, 2026-10-26: a file can be put into the page without the clipboard, but Gemini ignores synthetic paste and drop events, so pasting stays.)
- **Gemini refuses some well-known character names** ("third-party content providers"; Gingerbread Man, 2026-10-26): describe the character instead of naming it ("the biscuit boy, a small boy-shaped biscuit figure ..."). The filter is also flaky: a scene that passed once was refused three times in a row afterwards; wait and retry later rather than rewording the plan.
- **Refusal tips (Gingerbread Man, 2026-10-26):** scenes with only one or two characters were refused far more than scenes with three or more, so add small background characters to a one- or two-character scene (pages 7-10 gained the four chasers); call water "a calm stretch of water", never "river"; on a refusal use "Try again" from the Redo menu under the answer (up to twice) before rewording; pasting no line-up and describing every look in words also got through when pasted-line-up chats were refused.
- Usage limit: write the time of the first picture of each session in the art record (the 5-hour window
  runs from when usage started, not from when the limit hits). When Gemini says the limit is reached, stop,
  write where you got to, and report the reset time it shows (the message, or Settings > Usage Limits).
  The caller resumes at that time; if no time is shown, at the estimate from the first picture, rechecking
  every hour until drawing works again.

## 4. Page prompts

**Every image prompt (sheets, line-up, cover, pages) ends with the size line:** `Output size: small, 1K
resolution, 896x1200 pixels (portrait 3:4).` The app needs nothing bigger (the prepare script shrinks to
896x1200), and the usage cost is set by the model, not the size (Flash-Lite at 1792x2400 costs about 0.3% a picture). Record each
accepted picture's actual size in the art record. The size line never changed the size (Billy Goats, Lion and the Mouse: always 1792x2400), but it is harmless; 3.5 Flash-Lite makes the same size at a fraction of the usage.

Every page prompt repeats: same characters, the style line, portrait 3:4, no text, **exact count**
("exactly three pigs, each appearing once"), every character's outfit, and **"every character stays whole
in the top 68% of the picture: heads, bodies, legs, feet, paws, tails and anything they hold all end above
the 68% line. The bottom 32% is calm scene (grass, flowers, floor or water) with no character or any part
of one in it, never a plain empty strip"**. The prompt asks for 32% free and the check (step 5) passes at
27% free, the 73% line: 5 points of wiggle room (the user, 2026-11-15). "Faces and action" alone is not enough: 3.1 Pro kept the faces
up and still walked the Mouse's body, feet and berry into the bottom strip (Slice 350, 2026-11-13). When
the phone is upright the words panel covers up to 30% of the picture from the bottom (Slice 122; before
it, 58%, which is why the Slice 119 pigs pictures used the top 40%).

Two traps from the 16-page pigs (Slice 123): Gemini writes sound effects into a scene ("tink tink" on a
knocked door), so say "no text, letters, words, sound effects or writing anywhere"; and a window with
crossbars multiplies the faces behind it (four pigs in four panes), so ask for "one wide window with a
single pane" and name who stands where, left to right.

Two more from Billy Goats Gruff (Slice 121): Gemini dresses a character it thinks should be clothed once
they move (ragged trousers on a Troll who wears only a waistcoat, whenever he stamped or sat), so every
prompt with that character names what they do NOT wear ("no trousers, no shorts, nothing below the
waistcoat"); and it gives the "villain" teeth or fangs, so say "mouth closed or smiling, no teeth showing".
A chat that drew a wrong picture tends to repeat it when asked again: redo it in a fresh chat.

## 5. Check on the small copy (every picture, no exceptions)

Before viewing or reviewing a book's high-res files, fetch them: they are ~130-byte LFS pointers until then. Run the command from the comment in `.lfsconfig`: `git lfs pull --include "content/dino/<book>/high-res/**" --exclude ""`.

`gemini-take-download.ps1` writes `<name>.small.jpg` (896x1200, JPEG 85, `tools/dino/picture-review-copy.ps1`) beside every saved picture, and the two always move, rename and get deleted together. Read the small copy (not the chat preview, never the whole full-size original) and check each item; when one item needs a closer look, open a full-size crop of that one place only and name the fault. Any fail means: say what
is wrong, ask for that page again (a new chat; a small patch may continue the chat only while it stays at or under 3 picture requests), and check the new one
from the top. Compare outfits against the line-up picture, item by item (hat type, shirt or no shirt,
trousers or none).

- [ ] Exact number of characters; nobody appears twice; no extra characters.
- [ ] Each character's outfit and colours match the line-up (hat, scarf, dungarees, waistcoat...).
- [ ] The scene shows what the page text says, and nothing from another page.
- [ ] Rule 1: nobody hurt, in pain, crying in fear, near fire or hot water; no teeth-bared menace.
- [ ] No text, letters, words or signs anywhere (signatures included).
- [ ] **Every character is whole above the 73% line** (at least the bottom 27% free of characters; prompts ask for 32%, the user, 2026-11-15) (heads, bodies, legs, feet, paws, tails, anything held). Never judge it without the line: run `pwsh .claude/skills/create-dino-story/line70.ps1 <picture>` (for a scratch candidate it uses the `<name>.small.jpg` beside it and writes the overlay beside it, or into `-OutDir <folder>`; for a picture whose path has a `\content\dino\` segment, in any checkout, it writes the small copy and overlay to `%TEMP%\line70\<12-hex hash of the full path>\`, never into the repo; `-OutDir` overrides both) and read the `<picture>.line70.png` whose path it prints (red line at 73%, bottom 27% tinted blue). Any part of any character in the tint fails the picture: that strip is where the app's words go. The cover follows the same rule. Older books' `art.md` lines that say only "faces and action" are history; this checklist wins.
- [ ] The bottom of the picture is still scene (ground, grass, flowers, water, floor), never a plain white or empty strip; the scene fills the whole frame, top and bottom.
- [ ] Indoors/outdoors and time of day match the text (no grass on an indoor floor).
- [ ] Every character has the right number of arms, hands, legs, paws and wings (Slice 350: a lion with three hands, an extra limb).
- [ ] Objects and tools have their real shape, not melted or twisted.
- [ ] A page whose place matches the pages next to it repeats their background about 95% (same props, such as the berries); otherwise it is clearly a different place.
- [ ] The picture doesn't contradict the story or put a character in danger the text doesn't have (a pig still building while the wolf is there).
- [ ] The action matches the text exactly (a hug is a hug, not a bird sitting on a hat).
- [ ] Swimming animals are in the water, not standing on top of it.

Record each pass in the art record table (page, attempt number, why earlier attempts failed).

## 6. Save the pictures

- Each picture has a "Download full size image" button (aria-label); the newest picture is the last
  button (Gemini lazy-loads older ones). Clicking it writes the full-size JPEG to `%USERPROFILE%\Downloads`
  as `Gemini_Generated_Image_*.jpg` (every picture of 2026-10-26 arrived, 1792x2400).
- **Check the page is alive; never wait for a hung one** (2026-10-26): a Gemini tab can hang with no error, where the
  left menu (the sidebar chat list) never loads, and on reload it turns out to be Google's CAPTCHA (`google.com/sorry`).
  The left menu loads once, when the page loads, so check once: 5 s after opening or reloading the tab, read the tab URL
  and run `window.__geminiHealth()` (gemini-grab.js; `OK`, `CAPTCHA` or `HUNG: ...`; a script that can't be injected also
  means hung). Not `OK`: reload once and check again 5 s later; still not `OK`, stop and report (`CAPTCHA` stops at once).
  Claude never solves a CAPTCHA (not allowed, even on the user's own account). The coordinator alerts the user with a
  push notification the moment an illustrator reports `CAPTCHA`, and resumes once the user says it is solved.
- **A pasted line-up can leak characters into a picture** (Elves, Peter Rabbit, 2026-10-26): Gemini drew the elves on
  pages where the plan has none and turned the line-up into a picnic. When that happens, drop the paste for that
  picture and spell every look out in words, or paste a crop holding only the characters on that page.
- **Poll, never sleep, while Gemini draws** (2026-10-26: fixed `computer wait` sleeps left each finished picture
  idle for up to a minute). Inject `gemini-grab.js`, run `window.__markPictures()` just before sending, then
  `await window.__waitForPicture()` right after: it returns `READY <n>` the moment the new picture exists (call it again
  on `WAITING`). Grab and save at once. Wait for anything else the same way (a thumbnail after a paste, a menu): check
  for it in a short loop, don't sleep a fixed time.
- **The short per-picture sequence (Slice 350 tooling; every step resends the agent's whole context, so steps are the cost).
  One illustrator agent per picture; its brief gives only that page's prompt lines, and it never reads `gemini-grab.js` or the whole
  slice file.** Steps, each one assistant turn (calls in one bullet go out together):
  **Lock owner: `<stem>-<page>` (the cover is `<stem>-cover`), one per picture**, so two agents on one book never share an owner.
  1. Browser batch, no lock yet: open `https://gemini.google.com/app` in the same tab (new chat), then `javascript_tool` with the loader line
     below, then `window.__geminiHealth()` and `window.__modelLabel()`. **Model check:** `__modelLabel()` returns the mode picker's label (the
     button whose aria-label reads "Open mode picker, currently Pro"; 3.1 Pro shows as `Pro`) and must equal the model the brief names. A
     different label (Flash-Lite, anything else) or `NO PICKER` = stop, set the picker to the brief's model, check again, and send no prompt
     before it matches. Health `CAPTCHA` or `HUNG` = stop and report.
  2. `pwsh .claude/skills/create-dino-story/gemini-step.ps1 prepare -Owner <owner> -Picture <line-up>[,<place sheet>]` (usage check, lock
     Take, clipboard; two files go on the clipboard as a file list so one ctrl+v may attach both). From here the lock is HELD: release it
     (`gemini-step.ps1 release -Owner <owner>`) on every path out, including a stop or a failure. The lock goes stale 180 s after Take, so do step 3
     in the very next call (its wait is 15 s); if more than 2 minutes pass first, run `gemini-step.ps1 touch -Owner <owner>` (refreshes the lock only; never `put`, which would replace a two-picture clipboard) before pasting.
  3. One browser batch: ctrl+v (own `tabId`), then `await window.__waitAttached(<n>)` (a number = pasted; text = failed: click the prompt
     box once and paste again, or `gemini-step.ps1 put -Owner <owner> -Picture <second file>` (refused if you no longer hold the lock) and paste the
     second alone; still failing: release and report).
  4. `gemini-step.ps1 release -Owner <owner>` **and** `await window.__drawPicture(<prompt>, <n>)` together: one call that waits for the
     attachment(s), runs `__setupPicture`, `__waitForPicture` and `__grab(1)` and returns one line ending `bytes=<n>` (only a real blob list gives
     bytes; any other answer is an error to read); a line with a problem is `__setupPicture`'s or `__sendChecked`'s own answer, with their rules.
     `__drawPicture` refuses a chat that already has a sent message or a picture: never send again in a chat that sent.
  5. `gemini-step.ps1 save -Owner <owner> -Path <out folder>/cand-<page>.jpg -Bytes <n>` (`-Bytes` is required; it refuses without a positive size):
     lock, untouched save of a download of exactly that size, small copy, `line70`, unlock;
     prints one line `SAVED <path> <WxH> <bytes> | small <path> | line70 <overlay>` (exit 2 = saved, small copy failed: never download again).
  6. Read the small copy and the line70 overlay together (step 5's check); then rename or delete.
  The lock covers only steps 2-3 (clipboard Put to the confirmed paste) and step 5 (save); the `__grab` click inside step 4 is outside the lock, which is
  safe because `save` takes only a download of exactly the printed size. A rejected picture is a new chat: start again at step 1 (at most 3 tries).
  **Loader, one line, once per page load and again after every new chat** (the page reload of step 1 drops the script; the file stores itself
  in the profile's localStorage on its first injection; the version is a hash of the file's source): `localStorage.grabver==='8531151f'?(eval(localStorage.grabsrc),typeof window.__drawPicture):'STALE'`
  must answer `function`. `STALE` (or an error from `eval`) means the stored copy is missing or old: inject `gemini-grab.js` whole once with
  `javascript_tool` (it answers `ready 8531151f` and stores itself), then use the loader from then on. `gemini-grab.test.mjs` fails when this hash is not the file's.
  **Three known snags (2026-11-17, Love from Seagull Island):** (a) the loader's `eval` can fail on Gemini with a Trusted Types error; then run
  `const p=trustedTypes.createPolicy('dg'+Date.now(),{createScript:s=>s}); (0,eval)(p.createScript(localStorage.grabsrc))` instead. (b) `__drawPicture`'s
  grab can return a small webp preview (about 40-60 KB) that writes no download: call `__grab(1)` again and save with the real jpeg size it prints.
  (c) `javascript_tool` often times out at 45 s inside `__drawPicture` while the picture is still drawn: poll for the "Download full size image" button,
  then `__grab(1)`; never send again. (`__drawPicture(prompt, 0)` with no paste refuses with an UNKNOWN attachment count: use `__markPictures`,
  `__setupPicture(prompt, 0)`, `__waitForPicture`, `__grab(1)`.) After about 6 downloads a tab may stop writing files: a fresh own tab fixes it.
  **Recovery per chat** (rule 3, *Recover before you redraw*): open the chat, load, then one call
  `await window.__recoverChat()` (scrolls up to load every picture, then downloads each one oldest first, at least 20 s apart (a smaller `gapMs` is raised to 20 s), waiting inside the
  script; never use `sleep` calls) answers `RECOVER <n> pictures | 1: <size> | 2: <size> ...` (one size per picture, 0 = no blob); then one
  `gemini-step.ps1 collect -Owner <owner> -Path <folder>/<chat prefix> -Bytes <the sizes, comma-separated, in order>` saves them as `<prefix>-1`, `-2`...
  untouched with small copies, skips a name already saved (a re-run is safe) and prints MISSING for any download that never arrived. About 3 steps a chat. Chrome wrote only the first download of a tab
  in the 2026-11-14 Freddy run (probably its "multiple automatic downloads" block, which a real user click resets; unverified): if
  `collect` reports MISSING for the later ones, allow automatic downloads for gemini.google.com once in Chrome's site settings
  (`chrome://settings/content/automaticDownloads`, a user step) and run the chat again; a chat must not need a new tab per picture.
- **Save through the download, not the clipboard** (2026-10-26): inject `gemini-grab.js` with
  `javascript_tool` (once per page load; again after a reload or new chat), run `await window.__grab(1)` (clicks
  the newest picture's button), then
  `pwsh .claude/skills/create-dino-story/gemini-take-download.ps1 <working folder>/cand-<page>.jpg -Bytes <n> -BackupFolder <working folder>/previous` (add
  `-Untouched` for Slice 350 high-res pictures: the file is moved byte for byte, never re-saved, and keeps Gemini's own
  file type; use the path it prints), with `<n>` every size
  `__grab` printed, comma-separated if it printed several (`-Bytes 123,456`; other sessions' Gemini downloads land in the same folder; 2026-10-26 a pirate book's picture was
  picked up twice without it; if no download of that exact size arrives, run it once without `-Bytes` and check the
  saved picture is the one you asked for). It waits up to
  30 s for a `Gemini_Generated_Image_*` written in the last two minutes, saves it as JPEG quality 95 (or, with `-Untouched`, moves it byte for byte), prints
  `WxH` (a WARNING line means it isn't 3:4) and moves it out of `Downloads`, so the next call can't pick it up
  again. No clipboard and no real click, so it works while the PC is locked and can't collide with a paste.
- First fallback, if the button click writes no file (Freddy and the Slipper, 2026-11-01: after the first picture
  every click stopped writing one): fetch the newest picture's blob in the page and save it with a temporary
  `<a download>` link clicked from `javascript_tool`, then take it with `gemini-take-download.ps1 -Bytes <blob size>`.
  This saved every later picture of that book; one picture's full-size download failed twice and only the 896x1200
  version came (fine: the app uses 896x1200).
- Second fallback, only if no download arrives at all (Chrome dropped downloads silently on 2026-10-25): after `__grab`,
  run `await window.__grab(1, { clipboard: true })` (the clipboard write is armed only with that flag), then one real
  `computer` click on the newest picture itself (its `find` ref) makes the grabber copy the blob to the
  clipboard; poll `window.__clipResult` until it is `WxH`, then
  `pwsh -STA .claude/skills/create-dino-story/gemini-clipboard.ps1 Save <working folder>/cand-<page>.jpg`.
  Delete the stray download afterwards if it turns up late.
- Save each picture under a candidate name (`cand-5.jpg`), check it (step 5) by reading its small copy (`cand-5.small.jpg`), and
  only then rename it to `cover.jpg`, `1.jpg`... (rename `cand-5.small.jpg` to `1.small.jpg` with it). Delete rejected candidates with their small copies.
- Then run `pwsh tools/dino/prepare-story-pictures.ps1 <working folder> <target folder> <pageCount>` with the full target
  path: `content/dino/<stem>`. It checks the
  count, makes every picture 3:4 at most 896x1200 (never upscaled), JPEG quality 85, and writes `<target>/pictures/1..N.jpg`,
  `<target>/cover.jpg` and `<target>/cover-thumb.jpg` (288x384, the small cover the store list fetches).
- The back cover (Slice 359) goes through the same script, so its app copy is made the same way: copy the picture twice into an empty scratch folder as `cover.jpg` and `1.jpg`, run the script with page count 1 and a scratch
  target, and copy that target's `cover.jpg` to `content/dino/<stem>/back-cover.jpg`. The untouched original goes to `content/dino/<stem>/high-res/back-cover.jpg` (Slice 350 rules: LFS, never read by the build).
  Then `stamp` a starter (a store book needs none) and `build` a starter so its committed package carries the picture.
- Last, `pwsh .claude/skills/create-dino-story/contact-sheet.ps1 <working folder> <pageCount> <working folder>/contact-sheet.jpg`
  for the coordinator's second look.

## 7. Build, test, upload

- `dotnet run --project tools/dino/DinoReader.BookBuilder -- build <stem>` writes `artifacts/store/<id>/` and, for a starter, rewrites
  the Debug-only test book `src/Dino/DinoReader.App/Resources/Raw/testbooks/<stem>.dino` (only when its content changed, so the other starters stay untouched). Whenever a book's text or pictures change, run
  `dotnet run --project tools/dino/DinoReader.BookBuilder -- stamp <stem>` for a **starter** (raises its version and stores the content hash; also add the old text's page hashes to
  `StarterUpdate.EarlierPageTextHashes` if the text changed), or installed copies never update; `check` fails otherwise and runs in CI. A store book is not stamped (Slice 239).
- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Code goes through the repo's two-pass review gate and a PR (global CLAUDE.md).
- After merge, `dino-store.yml`'s `publish-store` job uploads every book whose content hash differs from the live store's, as live + 1 (Slice 158, 239; `tools/dino/upload-store-story.ps1 <id>` is the manual fallback: new `story-v<n>.dino`,
  `meta.json` last; never overwrite a version).

## 7b. Set up the book's sale product (the last step; the book is not delivered without it)

The user, 2026-11-18: Love from Seagull Island went live in the store but showed "cannot be bought right now", because
nothing creates a new paid book's in-app product. The session that makes a book does not end, or hand the book over as
done, until the book is in the live store **and** its product exists in every store. Right after `publish-store` uploads it:

- Confirm the book is live: `https://dinoreader.sirous.uk/api/stories` lists its id with `access` and `productId`. A book
  with `access: "free"` needs no product; stop here.
- For a paid book, run one command: `node .github/scripts/store-products-local.mjs --apply --book <stem>` (Slice 411). It loads the
  keys from `D:/secrets` itself without printing them, and creates the book's product in Play (one-time product, activated),
  App Store Connect (non-consumable, listing, price) and Partner Center (add-on with price `Tier1012` = USD 0.99, en-us listing,
  committed). `--book` never touches the subscription products. Existing products are left alone, a half-made one is completed.
  Its id is `uk.sirous.dinoreader.book.<stem>` unless `book.json` names one. A Microsoft `409` on the PUT means someone edited the
  pending submission in Partner Center: delete it there, rerun.
- Read it back: the run prints each store's outcome and, for Microsoft, the add-on's Store id and its displaycatalog address;
  Play's `oneTimeProducts` list shows the product with an `ACTIVE` buy option.
- App Store: add the review screenshot and submit the product with the next app version
  ([App Store Connect, Dino](https://appstoreconnect.apple.com/apps/6825053435)); the only step left by hand.
- Microsoft certification takes hours: the next day run `node .github/scripts/store-products-local.mjs --check-prices`, which reads
  every book's live price from the public catalog and fails on anything but USD 0.99
  ([Partner Center, Dino `9N9VHRLCZXN5`](https://partner.microsoft.com/en-us/dashboard/products/9N9VHRLCZXN5/overview) if it reports a problem).
- Report each store's state for the product in the final message. A step that only the user can do gets its direct link there.

## 8. Check in the app

Emulator, both orientations: the cover and every page show their picture, no character hidden under
the text panel, the cast voices still match. Then the user checks it on their phone before the next book.
