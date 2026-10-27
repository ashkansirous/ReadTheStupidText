---
name: dino-storytelling
description: Write or retell a Dino Reader picture-book story for ages 3-9 - one action per page so every page gets its own picture, 10-18 pages, gentle endings, speaker-tagged dialogue for voices, repetition children can join in with, and a page-by-page picture plan. Use before illustrating any Dino book, when the user says "write a story", "retell <tale>", "split the story into pages", "more pages", or a book has pages that hold two actions. Then hand over to create-dino-story for the pictures.
---

# Telling a Dino Reader story

A Dino book is a picture book: the reader looks at the picture while a voice reads the page. The page
breaks are the story's beats, so where the `---` lines fall matters as much as the words.

The user reviewed the first illustrated Three Little Pigs (6 pages, Slice 119) and said each page held
two actions, the pig's act and the wolf's answer, so one picture could not show the page. The fix was 16
pages with one action each (Slice 123). Gentle-story rules (nobody gets hurt) are in `create-dino-story`
and Decision 130. Read them first: they beat the classic tale.

## 1. One action per page

- **A page is one moment a picture can show.** Test each page: can you describe its picture in one
  sentence with one verb ("the Wolf blows the straw house away")? If you need "and then", split it.
- **Split what one picture cannot show.** A sequence goes on separate pages: arriving, then later leaving;
  deciding, then doing. A cause and its answer may share a page when one picture shows both at the same
  moment (the Wolf blowing while the pig runs). The Wolf knocks and the pig answers on one page; the
  house falls on the next.
- **Each place change gets its own page**: leaving home, arriving at the brick house.
- **A setup gets its own page**: each pig building a house is a page, each traveller met is a page,
  each bowl tried is a page.
- A page may hold a short dialogue exchange, as long as it all happens in one picture.

## 2. Length and rhythm

- **10-18 pages** for a classic tale; never fewer than 8. A page holds 1-4 short sentences, about 15-60
  words. A page over ~60 words is two pages.
- With the phone upright the words sit in a panel over the bottom of the picture (at most 30%,
  Slice 122), so short pages keep it small.
- **Repetition is the point.** Keep the refrain identical every time ("Little pig, little pig, let me
  come in"). Children join in, and the translations reuse the same piece.
- Give the build-up three steps (three houses, three bowls, three goats), the last one different.
- Close with a calm page: home, together, a sorry, a laugh, bedtime. No cliffhanger.

## 3. Words

- Short sentences, everyday words, past tense. One idea per sentence.
- **Spoken, not written (Decision 152).** Write the way a parent talks to a 4-6-year-old at bedtime, in
  every language: colloquial forms, diminutives, exclamations, contractions. Never literary, old or
  written-only forms. Farsi is the worst offender: «خوک کوچولو! خوک کوچولو! بذار بیام تو!», never
  «بچه خوک بچه خوک بگذار بیایم تو» (spoken verb forms: بذار، بیام، میخوام، نمیشه; کوچولو, not بچه‌ی).
  English: "Little pig, little pig, let me come in!", "Uh-oh!", "Yay!", not "Pray let me enter".
- **Sounds are read aloud by a voice, so make them long enough to hear (Decision 151).** Never a single short "Bang!".
  Measured only in English, Spanish, Chinese and Farsi, by length and a weak speech-recognition check, **not yet confirmed by a
  human listen** (Slice 145; numbers and per-voice detail: docs/plan/notes/2026-10-26-sound-words-aloud.md). Any other language
  is untested: use the repeat form and flag the sound for a listen.
  - **Repeat the sound with `!`, `,` or `...` between** ("Dang! Dang! Dang!", «¡Pum! ¡Pum! ¡Pum!», 砰砰砰！). A triple is the
    only form measured long enough on every voice; a double is uneven.
  - **Little or no letter stretching.** It lengthened a little but cost recognition ("Whooosh!", "Daaang!", "Knoooock!"), and
    stretch inside a repeat was spelled out by Kokoro.
  - **Farsi: never a lone short sound word** (the default voice, mana, drops «تق!», «دنگ!», «ووش!»); prefer a plain triple
    («تق تق تق!»), and avoid «دَنگ» forms. **Chinese: never a lone character**; repeat it.
- Dialogue as `"...," said <Name>.` or `<Name> said.`, with the name spelled the same everywhere, so the
  caster gives each character their own voice. Give every character in a group a name (Pip, Pop, Pim).
- Feelings are gentle: cross, grumpy, surprised, soggy, sorry. Never terrified, hurt or crying in pain.

## 4. Picture plan (one line per page)

Before any picture, write the plan into the book's art record (`content/dino/<stem>/art.md`): page
number, the one action, who is in it with a count, where, and time of day. Then check it:

- [ ] Every page's picture differs from its neighbours (place, action or who is there).
- [ ] No page needs two pictures.
- [ ] Characters' faces and the action fit in the top 70% of a 3:4 portrait picture.
- [ ] Each character's look is fixed in the line-up, not invented per page.

## 5. Changing an existing book

- The translations are keyed by sentence (piece). **Moving page breaks or splitting a paragraph keeps
  every existing translation**; only new or reworded sentences need translating into all 13 languages
  (`content/dino/<stem>/translations/<lang>.json`; dump the pieces with
  `dotnet run --project tools/DinoReader.BookBuilder -- dump-pieces <stem>`).
- Reword a sentence only when it must change, since each reworded sentence costs 13 translations.
- A new page count needs one picture per page. Reuse an earlier picture only when it shows exactly that
  page's one action. Otherwise draw it again.
- Raise the version with `dotnet run --project tools/DinoReader.BookBuilder -- stamp <stem>` (`book.json`'s `version` and content hash) and add the old text's page
  hashes to `StarterUpdate.EarlierPageTextHashes` for a starter, so installed copies update.
  Get the old hash before rebuilding: `StarterUpdate.PageTextHash` is SHA-256 over the page texts joined
  with `"\n\n"` (two newline characters, `Story.PageSeparator`). Compute it from the currently shipped .dino's `story.json` (`Pages[].Text`).

Then illustrate it with the `create-dino-story` skill.
