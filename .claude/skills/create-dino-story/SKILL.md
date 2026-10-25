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
2. **No picture is accepted without the full-size check** (step 5). A rejected picture is never saved.

Per-book prompts, cast and check results live in `docs/design/story-art/<stem>.md`.

## 1. Text

- Store story: a constant in `tests/DinoReader.Tests/StoreTexts.cs`. Starter (bundled): `StarterTexts.cs`.
- 8-15 pages for a store story, split on lines holding only `---`. Short sentences, dialogue as
  `"...," said <Name>.` so speaker attribution casts voices.
- Read every page against rule 1 before anything else. Rewrite what fails.
- Translations: `DINO_DUMP_PIECES=1 dotnet test tests/DinoReader.Tests` writes the pieces;
  translate into `tests/DinoReader.Tests/Starters/translations/<stem>.<lang>.json` (all 13 languages).
- Never move a `---` once pictures exist: the build fails when picture and page counts differ.

## 2. Art record

Create `docs/design/story-art/<stem>.md` with:
- The house style line (Decision 126): `Children's picture book illustration, soft watercolour and
  coloured pencil, warm gentle colours, simple rounded shapes, no text or letters anywhere.`
- One line per character: species, size relative to the others, clothes and colours, one prop.
  Name the distinguishing detail of each look-alike (three pigs: hat, scarf, dungarees, cap).
- One scene per page: who is in it (with a count), what they do, where, time of day.
- A table for the check results (step 5).

Prompt templates: [prompts.md](prompts.md).

## 3. Gemini setup (Chrome)

- Use the claude-in-chrome tools on `https://gemini.google.com/app`. The user is signed in (free tier).
- **One chat per book**, titled after it. Every later picture is asked for in the same chat so Gemini
  keeps the characters consistent.
- Set **Aspect ratio 3:4** with the button under the prompt box before the first picture. Pictures
  come out 896x1200.
- Order: one reference sheet per character, a line-up of the whole cast, the cover, then pages 1..N.
- If a chat gets long and Gemini starts mixing earlier scenes into a new one, say "ignore the last
  picture" and describe the whole scene again from scratch with every character's outfit.
- Free tier: when Gemini refuses or says the image limit is reached, stop, write where you got to in
  the art record, and resume next day in the same chat.

## 4. Page prompts

Every page prompt repeats: same characters, the style line, portrait 3:4, no text, **exact count**
("exactly three pigs, each appearing once"), every character's outfit, and **"the whole scene sits in
the top 40% of the picture; the lower 60% is plain ground/floor with nothing important in it"**. When
the phone is upright the story text panel covers up to 58% of the page from the bottom.

## 5. Full-size check (every picture, no exceptions)

Zoom into the picture at full size (`computer` action `zoom` over the image) and check each item. Any
fail means: say what is wrong, ask Gemini to fix that, and check the new one from the top.

- [ ] Exact number of characters; nobody appears twice; no extra characters.
- [ ] Each character's outfit and colours match the line-up (hat, scarf, dungarees, waistcoat...).
- [ ] The scene shows what the page text says, and nothing from another page.
- [ ] Rule 1: nobody hurt, in pain, crying in fear, near fire or hot water; no teeth-bared menace.
- [ ] No text, letters or signatures anywhere.
- [ ] Faces and action are in the top half; the lower part is plain.
- [ ] Indoors/outdoors and time of day match the text (no grass on an indoor floor).

Record each pass in the art record table (page, attempt number, why earlier attempts failed).

## 6. Save the pictures

- Each picture has a "Download full size image" button (aria-label). Download only accepted ones.
- Chrome must allow automatic downloads for `gemini.google.com` (the user added it on 2026-10-25). If a
  download doesn't arrive in `%USERPROFILE%\Downloads` (`Gemini_Generated_Image_*.jpg`), reload the
  chat and click again; Gemini lazy-loads older images, so the newest are the last buttons.
- Move each file straight away to a working folder outside the repo, named `cover.jpg`, `1.jpg`...
- Then run `pwsh tools/prepare-story-pictures.ps1 <working folder> <target folder> <pageCount>` with the full target
  path: `tests/DinoReader.Tests/Starters/<stem>` (starter) or `tests/DinoReader.Tests/Store/<stem>` (store). It checks the
  count, makes every picture 3:4 at most 896x1200 (never upscaled), JPEG quality 85, and writes `<target>/pictures/1..N.jpg`,
  `<target>/cover.jpg` and `<target>/cover-thumb.jpg` (288x384, the small cover the store list fetches).

## 7. Build, test, upload

- Starter: `DINO_BUILD_STARTERS=1 dotnet test tests/DinoReader.Tests` rewrites
  `src/DinoReader.App/Resources/Raw/starters/<stem>.dino`.
- Store: `DINO_BUILD_STORE=1 dotnet test tests/DinoReader.Tests` writes `artifacts/store/<id>/`; bump the
  story's `Version` whenever text or pictures change.
- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Code goes through the repo's two-pass review gate and a PR (global CLAUDE.md).
- After merge, upload store books with `tools/upload-store-story.ps1 <id>` (new `story-v<n>.dino`,
  `meta.json` last; never overwrite a version).

## 8. Check in the app

Emulator, both orientations: the cover and every page show their picture, no character hidden under
the text panel, the cast voices still match. Then the user checks it on their phone before the next book.
