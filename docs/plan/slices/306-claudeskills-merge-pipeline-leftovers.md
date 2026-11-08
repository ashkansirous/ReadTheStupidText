# Slice 306 — ClaudeSkills merge pipeline leftovers

*Built 2026-11-07, merged as ClaudeSkills PR #184 (f77284a).* Planned 2026-11-07. Built in the ClaudeSkills repo (`D:\ClaudeSkills`), not in this one: its own worktree, branch and PR there. This repo holds only the record. Moved from plan.md's *Outstanding work* (feedback loop 2026-11-01-0859 rounds 1-3, [note w1](../notes/2026-11-01-0859-feedback-loop-w1.md), [w2](../notes/2026-11-01-0859-feedback-loop-w2.md), [w3](../notes/2026-11-01-0859-feedback-loop-w3.md)).

**Touches:** (ClaudeSkills repo, not this one) `agents/update-docs.md` (added while building), `agents/ship-branch.md`, `skills/run-wave/**`, `hooks/merge-gate.mjs` or its current path under `skills/run-wave/`, `skills/feedback-loop/**`, `tests` next to each; here: `docs/plan/slices/306-claudeskills-merge-pipeline-leftovers.md`. Check each path with `git ls-files` in `D:\ClaudeSkills` at claim.

## Work

- [x] 1. **Serial merges cost too much.** In wave 7 about 30 of 51 minutes went on it, because ship-branch M2 stops after merging a moved `main` and the coordinator runs `run-checks` and dispatches the merge again for every PR (entries 640, 824, 882 of session c42c62a9); round 2: about 52 minutes for three merges (#638, #673, #672); round 3: all three merges (#683, #682, #685) stopped by a merge-in, and every comment-only fix after the last review needed another check pass for the merge gate's blob shas (224 had four review files, 229 five). Options: let M2 run the fast checks and continue when the merge brought in no change to the PR's own files, or the coordinator merges in one batch at the end; weigh the combination breaks that path-filtered CI would miss. Batching cosmetic fixes before the last review pass belongs in the same design. Decide, build, and measure on the next wave.
- [x] 2. **The merge gate checks only that review files exist for a merge pinned with `--repo` from another repo's folder** (high): ClaudeSkills #127 made the gate deny a merge after a `cd` unless `--repo` names the repo, and with `--repo` the coverage check (`Reviewed files:` against the PR's files) is skipped. Fix: read the PR's files and blob shas through the API (`gh pr view --json files,headRefOid`, `gh api .../git/trees/<sha>`) so coverage works for any repo. Also from #127's medium review: the here-string/unclosed-heredoc fail-open finding, a "known limit" in the gate's header comment. The gate cannot tell who wrote a review file (a `Reviewer:` line check would catch accidents, not forgery).
- [x] 3. **Skipped-finding records are written from briefs instead of the review files** (Slices 222, 223, 231): add a check that each *From review* skip names a reason found in the findings file.

## Verification

- Tests in the ClaudeSkills repo: a `--repo`-pinned merge of a PR with an uncovered changed file is denied; a skip record whose reason is absent from its findings file is flagged; the merge-in case continues without a second dispatch. Record the PR numbers here.

## Built

ClaudeSkills PR #184 (merge `f77284a`, 9 files): `agents/ship-branch.md`, `agents/update-docs.md`, `skills/feedback-loop/check-review-skips.mjs` (new, with test), `skills/run-wave/SKILL.md`, `skills/run-wave/merge-gate.mjs`, `skills/run-wave/merge-touched-own-files.mjs` (new, with test). Items ticked from that file list; the PR's tests are in the files above.

*Changed while building:* Touches widened to `agents/update-docs.md`.

*Known limits (from the brief):* a quoted prose line starting with `gh pr merge` in command position is denied (write such text via a file); a merge after `then`/`do`/`{` is not caught by the raw scan.

*From review:* (findings files under `D:/ReadTheStupidText-worktrees/reviews/`)
- `s306-high.md`: applied 2, 3, 4, 5 (check-review-skips parser), 6 (`--no-renames`), 7, 9 (both later replaced, see check1), plus the update-docs skip-record format.
- Skipped #1 (`reviews/s306-high.md`): "M2 skips the caller's run-checks and merges on CI alone" - the slice's design: M2 continues when the merge-in touched none of the PR's files; the PR's checks still run where the repo has them.
- Skipped #8 (`reviews/s306-high.md`): "Untested for this case; denial message does not say it is stricter." - stricter, so safe.
- Skipped #10 (`reviews/s306-high.md`): "so verdict may come from wrong data" - no GHES in use.
- `s306-medium.md`: applied 1, 3, 5, 6, 7; 4 by a new design (per-path contents API blob reads, no recursive tree, so no truncation).
- Skipped #2 (`reviews/s306-medium.md`): "and runs before mergeTargets; no merge need exist" - an unclosed block next to a merge is already a broken command.
- `s306-check1-medium.md`: the heredoc check and the API own-commit lookup had been raised in three rounds, so both were redesigned instead of patched: 5, 6 by a raw-text scan for a merge in command position (no heredoc parsing); 1, 2, 3 by taking the own commit from the PR's commit list and treating only stderr `HTTP 404` as deleted. Applied 4 (comments), 10 (own files from the three-dot merge base).
- Skipped #7 (`reviews/s306-check1-medium.md`): "repoSlug and the API path assume github.com" - github.com only.
- Skipped #8 (`reviews/s306-check1-medium.md`): "The quoted-reason lookup normalises only case and whitespace" - low, no path that blocks a merge.
- Skipped #9 (`reviews/s306-check1-medium.md`): "isSkip applies SKIP_WORDS to the whole item" - low, no path that blocks a merge.
- `s306-check2-medium.md`: no code change. 1 (quoted prose line starting `gh pr merge` in command position is denied) and 2 (`then`/`do`/`{` not caught) kept as the known limits above; 3, 4 repeat high 1 and high 2-5.

## User answer (2026-11-07, before the night run)

- **Hook edits allowed.** The user approved changing the hooks and guards named in this slice (through the normal
  two-pass review gate and a ClaudeSkills PR). This is the user's explicit ask for those hook edits; it covers only
  the hooks this slice names, not `autoMode`, `permissions` or other settings.
