# Slice 120 — Dino: a repo skill for making a story

*Planned 2026-10-25.* Dino Reader only; RTST is not touched.

**Batch:** 25 (story illustrations)
**Depends on:** Slice 119 (the loop it writes down is the one the pilot proved)
**Touches:** `.claude/skills/create-dino-story/**`, `CLAUDE.md`

## Why

The user asked for the whole story-making procedure to live in the repo as a skill, so any session can
make the next book the same way: text, cast, pictures, build, upload.

## Decisions (2026-10-25, agreed with the user)

- **Decision 129: story making is a project skill, `.claude/skills/create-dino-story/SKILL.md`.** It
  holds the procedure; the per-book prompts stay in `docs/design/story-art/<stem>.md`.

## Work

- [ ] Write `SKILL.md` (trigger: "make a Dino story", "illustrate <story>", "add a story to the store").
      Steps, each pointing at the file or command, not retyping it:
  1. Text: retell for ages 3-8, 8-15 pages split on `---`, in `StoreTexts.cs` (store) or
     `StarterTexts.cs` (starter); translations via `DINO_DUMP_PIECES=1` and `Starters/translations/`.
  2. Cast: one line per character (species, size, clothes, colours, one prop) in
     `docs/design/story-art/<stem>.md`, with the house style line (Decision 126).
  3. Gemini loop (Decision 125): new chat named after the book, aspect ratio 3:4, one sheet per
     character, a line-up, the cover (no text), then one prompt per page in the same chat; the prompt
     templates; how to save each picture; the daily-cap stop-and-resume rule; what to redo.
  4. `tools/prepare-story-pictures.ps1`, then the build (`DINO_BUILD_STORE=1` or
     `DINO_BUILD_STARTERS=1`) and the tests.
  5. Upload with `tools/upload-store-story.ps1 <id>` (new `story-v<n+1>.dino`, `meta.json` last).
  6. Emulator check on both orientations.
- [ ] Prompt templates in `.claude/skills/create-dino-story/prompts.md`: character sheet, line-up,
      cover, page scene (characters in the top half, no text).
- [ ] One line in the root `CLAUDE.md` pointing at the skill.

## Verification

- A fresh session asked "illustrate Lion and Mouse" loads the skill and follows it without being told
  the steps (checked at the start of Slice 121).
