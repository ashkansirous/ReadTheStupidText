# Slice 306 — ClaudeSkills merge pipeline leftovers

*Planned 2026-11-07.* Built in the ClaudeSkills repo (`D:\ClaudeSkills`), not in this one: its own worktree, branch and PR there. This repo holds only the record. Moved from plan.md's *Outstanding work* (feedback loop 2026-11-01-0859 rounds 1-3, [note w1](../notes/2026-11-01-0859-feedback-loop-w1.md), [w2](../notes/2026-11-01-0859-feedback-loop-w2.md), [w3](../notes/2026-11-01-0859-feedback-loop-w3.md)).

**Touches:** (ClaudeSkills repo, not this one) `agents/ship-branch.md`, `skills/run-wave/**`, `hooks/merge-gate.mjs` or its current path under `skills/run-wave/`, `skills/feedback-loop/**`, `tests` next to each; here: `docs/plan/slices/306-claudeskills-merge-pipeline-leftovers.md`. Check each path with `git ls-files` in `D:\ClaudeSkills` at claim.

## Work

- [ ] 1. **Serial merges cost too much.** In wave 7 about 30 of 51 minutes went on it, because ship-branch M2 stops after merging a moved `main` and the coordinator runs `run-checks` and dispatches the merge again for every PR (entries 640, 824, 882 of session c42c62a9); round 2: about 52 minutes for three merges (#638, #673, #672); round 3: all three merges (#683, #682, #685) stopped by a merge-in, and every comment-only fix after the last review needed another check pass for the merge gate's blob shas (224 had four review files, 229 five). Options: let M2 run the fast checks and continue when the merge brought in no change to the PR's own files, or the coordinator merges in one batch at the end; weigh the combination breaks that path-filtered CI would miss. Batching cosmetic fixes before the last review pass belongs in the same design. Decide, build, and measure on the next wave.
- [ ] 2. **The merge gate checks only that review files exist for a merge pinned with `--repo` from another repo's folder** (high): ClaudeSkills #127 made the gate deny a merge after a `cd` unless `--repo` names the repo, and with `--repo` the coverage check (`Reviewed files:` against the PR's files) is skipped. Fix: read the PR's files and blob shas through the API (`gh pr view --json files,headRefOid`, `gh api .../git/trees/<sha>`) so coverage works for any repo. Also from #127's medium review: the here-string/unclosed-heredoc fail-open finding, a "known limit" in the gate's header comment. The gate cannot tell who wrote a review file (a `Reviewer:` line check would catch accidents, not forgery).
- [ ] 3. **Skipped-finding records are written from briefs instead of the review files** (Slices 222, 223, 231): add a check that each *From review* skip names a reason found in the findings file.

## Verification

- Tests in the ClaudeSkills repo: a `--repo`-pinned merge of a PR with an uncovered changed file is denied; a skip record whose reason is absent from its findings file is flagged; the merge-in case continues without a second dispatch. Record the PR numbers here.
