# Slice 366 — Plan split by product: RTST, Dino and Shared tables, every slice file tagged

*In progress (wave 2026-11-13), draft PR.* Planned 2026-11-13. First of the tidy-by-product slices (366-371). The user chose to keep both apps in one repository and tidy it instead of splitting it (2026-11-13, "lets change it to tidy up", then "I want all 5").

**Touches:** `plan.md`, `docs/plan/slices/*.md` (one added line each), `docs/plan/notes/slices-done.md`, `docs/plan/unverified.md`; ClaudeSkills `skills/update-docs/`, `skills/parallel-check/`, `skills/archive-docs/` only if they cannot read three tables (item 4)

## Why

`plan.md` has one status table mixing RTST, Dino and shared work. About 38% of the 361 slice files carry "dino" in the name, and more are Dino's. An agent working on one app reads the other's rows too.

## Decisions (user, 2026-11-13)

- One repository stays. The plan is split into three tables: **Read The Stupid Text**, **Dino Reader**, **Shared** (engine, CI, tooling, docs, anything both apps ship).
- Every slice file says which product it belongs to.

## Work

- [ ] 1. Add a `**Product:** RTST | Dino | Shared` line under the title of every slice file in `docs/plan/slices/`, by a small script: filename or title starting with "Dino" is Dino; "RTST"/tray/Windows tray is RTST; workflows, tooling, feedback loops, packs, engine, both-app store work is Shared. List every file the script could not place and decide each one by reading it (no default guess).
- [ ] 2. Replace `plan.md`'s single *Status* table with three tables under `### Read The Stupid Text`, `### Dino Reader`, `### Shared`, same columns. Move every open row into its product's table, keep the order inside each.
- [ ] 3. Same split for `docs/plan/notes/slices-done.md` and the blocks in `docs/plan/unverified.md` (a heading per product; count line in *Outstanding work* stays one total plus a per-product split).
- [ ] 4. Check the tooling that reads or writes the table: `parallel-check` (reads the table for the next free slice number), `update-docs` index mode (adds and moves rows), `archive-docs` (moves finished rows), `run-all-waves` housekeeping. Run each against the new `plan.md` in this worktree (parallel-check's "next free slice number" must still be right; update-docs dry run adds a row to the right table). Any that fail are fixed in ClaudeSkills in the same pass (its own PR), taught to pick the table from the slice's `**Product:**` line.
- [ ] 5. `to-plan`'s slice template gets the `**Product:**` line (ClaudeSkills), so new slices carry it.

## Verification

- Every slice file has exactly one `**Product:**` line: `grep -L '^\*\*Product:\*\*' docs/plan/slices/*.md` prints nothing.
- Every open row from the old table appears exactly once across the three new tables (count before and after).
- `parallel-check` reports the same next free slice number before and after the change.
- `plan.md` stays under its ~50 KB budget (46.6 KB before this slice).
