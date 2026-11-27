### Slice 431 — Dino: the story skill reports its steps to the agent board, with process keys
**Product:** Dino

*Built wave 4 of night 2026-11-27-0154 (PR #1409); items A and B built, item C (one real book run) not yet verified.* The start condition was met: agent-progress-board Slice 55 (process keys and step counts in the agent-board plugin) merged as PR #209 on 2026-11-25.

**Touches:** `.claude/skills/create-dino-story/SKILL.md`, `.claude/agents/dino-story-writer.md`,
`.claude/agents/dino-story-illustrator.md`, `.claude/agents/dino-story-reviewer.md`, `.claude/agents/farsi-review.md`,
`.claude/agents/voice-pick.md`

Source: the owner's phone scope "Process optimiser and true per-step telemetry" (agent-progress-board, 2026-11-21),
refined at the desk 2026-11-22; the owner approved every answer that day ("agree with all. plan it"), including that the
book flows in this repo report their steps so the optimiser can see them.

## Why

The optimiser on the agent board can only judge work it sees. Before this slice (checked 2026-11-22): no Dino skill or agent reports to the
board (only the cloud usage hooks in `.claude/settings.json`). The owner's example "the book was translated into every
language before I had read it" is already guarded: translations start only after the owner approves the English
(`create-dino-story/SKILL.md:107`, rule of 2026-11-17). The board should be able to show that gate holding, and any step
that costs more than it should (illustrator groups, review rounds).

## Decisions

From the approved answers (2026-11-22):

1. The skill opens one card per book (`report_progress`, key `<project>/task-<id>`, outcome "Book <stem> shipped") and
   each agent reports under its step (`under_step`). Process keys: `story/write`, `story/text-review`,
   `story/owner-read` (sent as a blocked step waiting on the owner, with the question in `needs_you`),
   `story/translate`, `story/voices`, `book/illustrate-setup`, `book/illustrate-group`, `book/review`, `book/build`,
   `book/store-product`.
2. `story/translate` starts only after `story/owner-read` is done; the optimiser's "work before approval" check uses that
   order, and this flow is its negative case.
3. The reviewer reports `counts: { findings }`; `farsi-review` reports `counts: { findings, fixed }`.
4. Reports fail open: a board error never stops a book.

## Work

- [x] **A.** Card and step reporting in `create-dino-story/SKILL.md` with the keys above (section "Board card and steps (Slice 431)").
- [x] **B.** Each agent file reports under its step and sends its counts.
- [ ] **C.** One real book run, read back with `get_item`: the steps, keys and the owner-read wait show on the card.

## Verification

- Live: the next new Dino book's card on the board shows every step with its process key, the owner-read step as
  waiting on the owner until approved, and `story/translate` starting after it.

## Not yet verified

- [owner] Item C: when the owner next makes a new book, read the book card back with `get_item` on the board (steps, process keys, the owner-read wait).

*Changed while building:*

- `create-dino-story/SKILL.md`, section "Board card and steps (Slice 431)": one card per book, key `readthestupidtext/task-<id>`. The step table uses process keys; voices and owner read run in parallel, and translate starts only after owner read is done. `story/owner-read` is a blocked step waiting on the owner; `needs_you` is resent with each report and set to null on approval. One step id per illustrator group plus `book-illustrate-finish` (key `book/illustrate-finish`); reruns get `-r2` ids. A `book/farsi-review` row was added; its key was chosen by the coordinator because the slice named none.
- The five agent files report under the step their brief names (`Board parent:` / `Board step:`); the end report repeats `--steps`; the reviewer counts findings, `farsi-review` counts findings and fixed; the commands run in the Bash tool.
- The blocked owner-read step and `needs_you` go through the agent-board `report_progress` MCP tool, because `report-progress.mjs` cannot set them.

#### From review

High pass: 7 findings, 6 fixed. Medium pass: 5 findings, 4 fixed. The other two have their lines below.

- Skipped #7 (`reviews/pr-1409-high.md`): "tick A and B (C stays under Not yet verified)" - update-docs writes the slice ticks and built note.
- Skipped #2 (`reviews/pr-1409-medium.md`): "voice-pick and the illustrator report only at start and end; no heartbeat, no failure behaviour." - voice-pick's wait is the owner listening, already shown on the card through the SKILL's blocked owner step; a heartbeat adds nothing.
