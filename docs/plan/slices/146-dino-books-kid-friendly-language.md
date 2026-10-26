# Slice 146 — Rewrite every Dino book in spoken, kid-friendly language

*Planned. Starts after Slice 145 (the sound-word list).*

**Batch:** 30 (Decisions 149-153)
**Touches:** `tests/DinoReader.Tests/StarterTexts.cs`, `tests/DinoReader.Tests/StoreTexts.cs`, `tests/DinoReader.Tests/Starters/translations/**`, `tests/DinoReader.Tests/Store/**`, `tests/DinoReader.Tests/StarterBuilder*.cs`, `src/DinoReader.App/StarterUpdate*.cs`, `src/DinoReader.App/Resources/Raw/starters/*.dino`

## Why

Every book reads formally, and the Farsi translations "as if someone from 500 years ago wrote them":
«بچه خوک بچه خوک بگذار بیایم تو» where a parent would say «خوک کوچولو! خوک کوچولو! بزار بیام تو!».
The texts are read aloud by the voices, so sound words must also be long enough to hear (Slice 145).

## Decisions (locked with the user, 2026-10-26)

- **Decision 152:** every language is written the way a parent talks to a 4-6-year-old: spoken
  (colloquial) forms, diminutives and exclamations, never literary or written-only forms. The rules are
  in `.claude/skills/dino-storytelling/SKILL.md` section 3 and the writer and reviewer agents.
- **Decision 153:** rewrite all existing books, one PR per book, through `dino-story-writer` and a
  `dino-story-reviewer` pass: Farsi first for every book, then English, then the other 12 languages.
  Starters and store stories alike; each bumps its version (`StarterVersions` +
  `EarlierPageTextHashes`, or the store `Version` and a new `story-v<n+1>.dino` upload).

#### Design (Dino Reader Batch 30 mock, added 2026-10-26)

No screens. The Batch 30 mock intro says 146 (books rewritten in everyday spoken language) changes no screens. Nothing to build from the mock.

## Work

- [ ] Wave A: Farsi for the 4 starters, then each store story.
- [ ] Wave B: English for every book (reworded English pieces need their 13 translations redone in
      the new register).
- [ ] Wave C: the other 12 languages for every book.
- [ ] Each book: version bump, rebuild, reviewer pass, upload (store), store copies checked live.

## Verification

- `dotnet test tests/DinoReader.Tests` (coverage, gentle-text and speech-tag tests) per book.
- Native read by the user for Farsi; installed copies update and keep recordings on unchanged pages.
