---
name: create-dino-story
description: Make or illustrate a Dino Reader book - gentle story text for ages 3-9, cast descriptions, a cover and one picture per page drawn by driving the Gemini web app in the user's Chrome, a full-size quality check of every picture, then build, test and upload. Use when the user says "make a Dino story", "illustrate <story>", "add pictures to <story>", "add a story to the store", or works on Slices 119-121.
---

# Create or illustrate a Dino Reader story

Dino Reader's readers are **3 to 9 years old**. Two rules beat everything else in this skill, including
the classic tale and Gemini's output (Decision 130, Slice 119):

1. **Nobody gets hurt.** No eating, burning, boiling, drowning, beating, dying, falling to death, or
   threats of any of it; no fire or hot water near a character; no "licks his lips". Conflicts end
   gently: cold bubbly water, a fright that turns into a laugh, a sorry, running home, a new friend.
2. **No picture is accepted without the full-size check** (step 5). A rejected picture is never kept: candidates are saved only to be checked, and rejected ones are deleted.

Per-book prompts, cast and check results live in `docs/design/story-art/<stem>.md`.

## Who does what

The main thread coordinates and never drives Gemini or writes a book's text itself; that keeps its
context free of screenshots, pictures and 13 translation files.

1. **`dino-story-writer`** (Opus, high effort): steps 1-2 with the `dino-storytelling` skill. Several may
   run at once, one book each, in their own worktrees.
2. **`dino-story-reviewer`** (Opus, high effort), **text** mode: safety, story quality for the age, one
   action per page, the picture plan. Failed pages go back to the writer, then review again, before any
   picture is drawn, since pictures cost Gemini quota.
3. **`dino-story-illustrator`** (Sonnet, medium effort): steps 3-6. **One at a time across the machine**
   (one Chrome, one clipboard). It ends with `contact-sheet.jpg`, every picture on one sheet.
4. **`dino-story-reviewer`**, **full** mode: every picture at full size against its page and the line-up,
   and characters, places and time of day consistent across the book. Failed pages go back to the
   illustrator (or the writer), then review again until `PASS`. The coordinator may glance at the contact
   sheet but never accepts a book from it. Then step 7 goes through the usual gate and ship.

The reviewer never reviews its own work and changes nothing. Its pass is required before a book ships;
the illustrator's own step 5 check still runs on every candidate.

Version bump: the writer bumps it when the text changes; the illustrator bumps it when only pictures change
(`StarterBuilder.StarterVersions` for a starter, the story's `Version` for a store book; no text hash needed).

Helpers in this folder: `gemini-grab.js` (step 6), `gemini-clipboard.ps1` (paste the line-up, save a
picture) and `contact-sheet.ps1`.

## 1. Text

- Store story: a constant in `tests/DinoReader.Tests/StoreTexts.cs`. Starter (bundled): `StarterTexts.cs`.
- **Write or retell the text with the `dino-storytelling` skill first**: one action per page, 10-16
  pages, split on lines holding only `---`, dialogue as `"...," said <Name>.` so speaker attribution
  casts voices.
- Read every page against rule 1 before anything else. Rewrite what fails.
- Translations: `DINO_DUMP_PIECES=1 dotnet test tests/DinoReader.Tests` writes the pieces;
  translate into `tests/DinoReader.Tests/Starters/translations/<stem>.<lang>.json` (all 13 languages).
- Never move a `---` once pictures exist: the build fails when picture and page counts differ.

## 2. Art record

Create `docs/design/story-art/<stem>.md` with:
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
    cast. Save the accepted line-up and sheets to `docs/design/story-art/<stem>/characters/`; they are
    the reference every page chat gets. Still being tried: whether sheets stay more consistent in one
    chat or with a new chat every 2-3 characters. Note in the art record which you did and how it went,
    and update this line once it is clear.
  - **Page chats**: a **new chat for every 3 pictures at most** (cover + pages 1-2, pages 3-5, ...).
    Start each by giving it the line-up (plus the sheets of the characters on those pages), then
    "These are the characters. Keep their outfits exactly as shown", every outfit spelled out
    (including what they do NOT wear: "no shirt", "no trousers", "no bobble"), then one prompt per
    page. Gemini adds no file input until its native picker opens, which can't be driven, so **paste**
    the picture instead: `pwsh -STA .claude/skills/create-dino-story/gemini-clipboard.ps1 Put <line-up file>`,
    then find the prompt box (`find`, not a guessed position), click it, press `ctrl+v`.
  - If a chat draws an earlier scene again instead of the new one (pigs pilot: the second picture of a
    fresh chat repeated page 2 for page 6), drop to **one picture per chat** for the rest of the book.
    A fresh chat with the line-up pasted got every outfit right first time.
  - The art record's character lines are the canonical outfits, written from the accepted line-up,
    not from the first prompt (the pilot asked for a "red cap" but the line-up drew a knitted beanie;
    later prompts must follow the picture).
- Set **Aspect ratio 3:4** with the button under the prompt box in every new chat. Pictures come out
  896x1200.
- Free tier: when Gemini refuses or says the image limit is reached, stop, write where you got to in
  the art record, and resume next day.

## 4. Page prompts

Every page prompt repeats: same characters, the style line, portrait 3:4, no text, **exact count**
("exactly three pigs, each appearing once"), every character's outfit, and **"the whole scene sits in
the top 70% of the picture; the bottom 30% is plain ground/floor with nothing important in it"**. When
the phone is upright the words panel covers up to 30% of the picture from the bottom (Slice 122; before
it, 58%, which is why the Slice 119 pigs pictures used the top 40%).

Two traps from the 16-page pigs (Slice 123): Gemini writes sound effects into a scene ("tink tink" on a
knocked door), so say "no text, letters, words, sound effects or writing anywhere"; and a window with
crossbars multiplies the faces behind it (four pigs in four panes), so ask for "one wide window with a
single pane" and name who stands where, left to right.

## 5. Full-size check (every picture, no exceptions)

Read the saved full-size file (not the chat preview) and check each item. Any fail means: say what
is wrong, ask for that page again (a fresh chat if this one has already drawn 3), and check the new one
from the top. Compare outfits against the line-up picture, item by item (hat type, shirt or no shirt,
trousers or none).

- [ ] Exact number of characters; nobody appears twice; no extra characters.
- [ ] Each character's outfit and colours match the line-up (hat, scarf, dungarees, waistcoat...).
- [ ] The scene shows what the page text says, and nothing from another page.
- [ ] Rule 1: nobody hurt, in pain, crying in fear, near fire or hot water; no teeth-bared menace.
- [ ] No text, letters or signatures anywhere.
- [ ] Faces and action are in the top 70%; the bottom 30% is plain.
- [ ] Indoors/outdoors and time of day match the text (no grass on an indoor floor).

Record each pass in the art record table (page, attempt number, why earlier attempts failed).

## 6. Save the pictures

- Each picture has a "Download full size image" button (aria-label); the newest picture is the last
  button (Gemini lazy-loads older ones). Clicking it fetches the full-size JPEG
  (`lh3.googleusercontent.com/rd-gg/...`), but Chrome often drops the file silently, so don't rely on
  `%USERPROFILE%\Downloads`.
- What works (2026-10-25): inject `gemini-grab.js` (this folder) with `javascript_tool`. It wraps
  `window.fetch` to keep the image blob of any `rd-gg` response. Run `await window.__grab(1)` (clicks the
  newest picture's button), then make one real `computer` click on empty page space: its click handler
  copies the blob to the clipboard as PNG (needs a real click; the user granted gemini.google.com clipboard
  access). `window.__clipResult` then reads `896x1200`. Save it with
  `pwsh -STA .claude/skills/create-dino-story/gemini-clipboard.ps1 Save <working folder>/cand-<page>.jpg`
  (JPEG quality 95). Re-inject the script after any reload or new chat.
- Save each picture under a candidate name (`cand-5.jpg`), check it (step 5) by reading the file, and
  only then rename it to `cover.jpg`, `1.jpg`... Delete rejected candidates.
- Then run `pwsh tools/prepare-story-pictures.ps1 <working folder> <target folder> <pageCount>` with the full target
  path: `tests/DinoReader.Tests/Starters/<stem>` (starter) or `tests/DinoReader.Tests/Store/<stem>` (store). It checks the
  count, makes every picture 3:4 at most 896x1200 (never upscaled), JPEG quality 85, and writes `<target>/pictures/1..N.jpg`,
  `<target>/cover.jpg` and `<target>/cover-thumb.jpg` (288x384, the small cover the store list fetches).
- Last, `pwsh .claude/skills/create-dino-story/contact-sheet.ps1 <working folder> <pageCount> <working folder>/contact-sheet.jpg`
  for the coordinator's second look.

## 7. Build, test, upload

- Starter: `DINO_BUILD_STARTERS=1 dotnet test tests/DinoReader.Tests` rewrites
  `src/DinoReader.App/Resources/Raw/starters/<stem>.dino`. Whenever its text or pictures change, bump its version in
  `StarterBuilder.StarterVersions` (and add the old text's page hashes to `StarterUpdate.EarlierPageTextHashes` if
  the text changed), or installed copies never update. A rebuild rewrites all four starters: `git checkout` the
  other starters' `.dino` files.
- Store: `DINO_BUILD_STORE=1 dotnet test tests/DinoReader.Tests` writes `artifacts/store/<id>/`; bump the
  story's `Version` whenever text or pictures change.
- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Code goes through the repo's two-pass review gate and a PR (global CLAUDE.md).
- After merge, upload store books with `tools/upload-store-story.ps1 <id>` (new `story-v<n>.dino`,
  `meta.json` last; never overwrite a version).

## 8. Check in the app

Emulator, both orientations: the cover and every page show their picture, no character hidden under
the text panel, the cast voices still match. Then the user checks it on their phone before the next book.
