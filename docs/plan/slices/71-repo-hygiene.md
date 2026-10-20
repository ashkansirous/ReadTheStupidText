# Slice 71 — Repo hygiene and the Drive mirror

*Planned.*

**Batch:** 11 (Lets-Call-Mom parity)
**Depends on:** PR #178 (archive `plan.md` into the index layout) merged
**Touches:** `CLAUDE.md`, `plan.md` (index pass only)
**Story issue:** —

## Goal

ReadTheStupidText follows the same repo conventions as Lets-Call-Mom: merged branches clean
themselves up, the docs fit their budgets, and the phone sees the current state.

## Scope

- [ ] `gh repo edit --delete-branch-on-merge` (currently off).
- [ ] Prune the ~35 merged remote branches. List them first. Skip any head branch of an open
      PR (#98 `plan/macos-uno-port`) and any branch with unmerged commits. Delete only after
      the user confirms.
- [ ] After #178 merges: move the "Batch 6/7/9" history passages out of `CLAUDE.md` into
      `docs/plan/notes/claude-md-history.md`, leaving each rule with a `(Slice N: …)` pointer.
      `CLAUDE.md` stays under ~40 KB.
- [ ] Index pass: add Slices 68–71 to `plan.md`'s status table. They are not added here, so
      this branch doesn't conflict with #178's rewrite.
- [ ] Check that `Scope Inbox/_projects.md` maps ReadTheStupidText to this repo, then run
      `mirror-to-drive`.
- [ ] Check that `plan.md`'s Outstanding-work item "Slice 58 still has to add the Dino Reader
      build group to `build.yml`" is stale (`build.yml` already has `test-dinoreader`), and drop it.

## Done when

- `gh repo view --json deleteBranchOnMerge` prints `true`, and `git branch -r` shows only
  `main` plus live branches.
- `plan.md` ≤ ~50 KB with rows 68–71, and `CLAUDE.md` ≤ ~40 KB.
- `_context/` in Drive holds the current `plan.md`, `CLAUDE.md` and `README.md`.

## Out of scope

- Versioning: already at parity (GitVersion, Decision 17). Nothing to port.
- Repo-local `.claude/agents`: Lets-Call-Mom's two are app-specific. None is needed here yet.
