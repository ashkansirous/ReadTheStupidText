# Slice 366 — Plan split by product: RTST, Dino and Shared tables, every slice file tagged
**Product:** Shared

*Built 2026-11-13 (wave 55), merged as PR #1088 (f76b9d62). Needs the ClaudeSkills PR (branch `chore/plan-product-tables`) merged and installed first.* Planned 2026-11-13. First of the tidy-by-product slices (366-371). The user chose to keep both apps in one repository and tidy it instead of splitting it (2026-11-13, "lets change it to tidy up", then "I want all 5").

**Touches:** `plan.md`, `docs/plan/slices/*.md` (one added line each), `docs/plan/notes/slices-done.md`, `docs/plan/unverified.md`; ClaudeSkills `skills/update-docs/`, `skills/parallel-check/`, `skills/archive-docs/` only if they cannot read three tables (item 4)

## Why

`plan.md` has one status table mixing RTST, Dino and shared work. About 38% of the 361 slice files carry "dino" in the name, and more are Dino's. An agent working on one app reads the other's rows too.

## Decisions (user, 2026-11-13)

- One repository stays. The plan is split into three tables: **Read The Stupid Text**, **Dino Reader**, **Shared** (engine, CI, tooling, docs, anything both apps ship).
- Every slice file says which product it belongs to.

## Work

- [x] 1. Add a `**Product:** RTST | Dino | Shared` line under the title of every slice file in `docs/plan/slices/`, by a small script: filename or title starting with "Dino" is Dino; "RTST"/tray/Windows tray is RTST; workflows, tooling, feedback loops, packs, engine, both-app store work is Shared. List every file the script could not place and decide each one by reading it (no default guess).
- [x] 2. Replace `plan.md`'s single *Status* table with three tables under `### Read The Stupid Text`, `### Dino Reader`, `### Shared`, same columns. Move every open row into its product's table, keep the order inside each.
- [x] 3. Same split for `docs/plan/notes/slices-done.md` and the blocks in `docs/plan/unverified.md` (a heading per product; count line in *Outstanding work* stays one total plus a per-product split).
- [x] 4. (update-docs dry run not done: its instructions were changed, not run) Check the tooling that reads or writes the table: `parallel-check` (reads the table for the next free slice number), `update-docs` index mode (adds and moves rows), `archive-docs` (moves finished rows), `run-all-waves` housekeeping. Run each against the new `plan.md` in this worktree (parallel-check's "next free slice number" must still be right; update-docs dry run adds a row to the right table). Any that fail are fixed in ClaudeSkills in the same pass (its own PR), taught to pick the table from the slice's `**Product:**` line.
- [x] 5. `to-plan`'s slice template gets the `**Product:**` line (ClaudeSkills), so new slices carry it.

## Verification

- Every slice file has exactly one `**Product:**` line: `grep -L '^\*\*Product:\*\*' docs/plan/slices/*.md` prints nothing.
- Every open row from the old table appears exactly once across the three new tables (count before and after).
- `parallel-check` reports the same next free slice number before and after the change.
- `plan.md` stays under its ~50 KB budget (46.6 KB before this slice).

*Changed while building:* 369 slice files tagged (RTST 96, Dino 161, Shared 112). The 10 files with no `Touches` (slices 0-9) were read by hand; about 150 more were overridden by title/Touches judgment (tooling, CI, packs go to Shared; Farsi engine, Windows, RTST phone go to RTST). `plan.md` has three tables, open rows 18 before and after (1 RTST / 5 Dino / 12 Shared), order kept, 43.9 KB. `slices-done.md` keeps 351 rows under per-product headings. `unverified.md` keeps 149 blocks / 362 checks under per-product headings; the count line now carries a per-product split. `parallel-check` gives next free slice number 374 before and after (same 18 row ids). `plan.md`'s stale "Finished slices (174 rows)" became 351. In ClaudeSkills (PR on branch `chore/plan-product-tables`): `remaining-slices.mjs` reads every table under the `###` sub-headings of `## Status` (it would otherwise have stopped run-all-waves), `count-unverified.mjs` writes the per-product split, the `to-plan` template gets the Product line, and `update-docs`, `archive-docs`, `run-all-waves` wording changed. That PR must merge and be installed before this one merges.

*From review:* no review gate here (doc-only in this repo); the ClaudeSkills code gets its gate in its own PR.

#### For CLAUDE.md (lifted 2026-11-13)

- Every slice file carries one `**Product:** RTST | Dino | Shared` line under its title, and its status row goes in the matching `plan.md` table (`### Read The Stupid Text`, `### Dino Reader`, `### Shared`). (Slice 366: docs/plan/slices/366-plan-split-by-product.md)

#### Not yet verified

- `update-docs` index mode adding a row to the right product table was not run (instructions changed only, no dry run).
