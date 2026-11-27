### Slice 431 — Dino: the story skill reports its steps to the agent board, with process keys
**Product:** Dino

*Planned.* The start condition is met: agent-progress-board Slice 55 (process keys and step counts in the agent-board plugin) merged as PR #209 and has been live since 2026-11-25 (stated in the Start pass brief of 2026-11-27; not re-read in that repo).

*Plan-cleanup check 2026-11-25:* `git grep process_key` found nothing in the plugin then; no `report_progress`, `under_step` or `story/` key exists in `.claude/` here. A-C are unbuilt and the claims hold. Verdict: keep open, not started; nothing to move out.

**Touches:** `.claude/skills/create-dino-story/SKILL.md`, `.claude/agents/dino-story-writer.md`,
`.claude/agents/dino-story-illustrator.md`, `.claude/agents/dino-story-reviewer.md`, `.claude/agents/farsi-review.md`,
`.claude/agents/voice-pick.md`

Source: the owner's phone scope "Process optimiser and true per-step telemetry" (agent-progress-board, 2026-11-21),
refined at the desk 2026-11-22; the owner approved every answer that day ("agree with all. plan it"), including that the
book flows in this repo report their steps so the optimiser can see them.

## Why

The optimiser on the agent board can only judge work it sees. Checked 2026-11-22: no Dino skill or agent reports to the
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

- [ ] **A.** Card and step reporting in `create-dino-story/SKILL.md` with the keys above.
- [ ] **B.** Each agent file reports under its step and sends its counts.
- [ ] **C.** One real book run, read back with `get_item`: the steps, keys and the owner-read wait show on the card.

## Verification

- Live: the next new Dino book's card on the board shows every step with its process key, the owner-read step as
  waiting on the owner until approved, and `story/translate` starting after it.

## Not yet verified

- Item C: one real book run, read back with `get_item` on the board (steps, process keys, the owner-read wait).
