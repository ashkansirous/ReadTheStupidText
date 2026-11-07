# Slice 307 — ClaudeSkills hooks and guards leftovers

*Planned 2026-11-07.* Built in the ClaudeSkills repo (`D:\ClaudeSkills`), in its own worktree and PR. This repo holds the record. Moved from plan.md's *Outstanding work*: every other ClaudeSkills tooling bullet (feedback loops 2026-10-31-1301 final, 2026-11-01-0859 rounds 1-2, 2026-11-03-2349 final, 2026-11-06-0042).

**Touches:** (ClaudeSkills repo, not this one) `skills/feedback-loop/**` (`note-claims.mjs`, `prune-reviews.mjs`, guards), `skills/run-wave/**`, `skills/run-all-waves/start-detached.ps1`, `skills/archive-docs/archive_docs.py`, `agents/*.md`, the hooks folder, tests next to each; here: `docs/plan/slices/307-claudeskills-hooks-and-guards-leftovers.md`. Check each path in `D:\ClaudeSkills` at claim.

## Work

- [ ] 1. **Interpreter-written worktree files.** Builder agents twice rewrote `UsageCounter.cs` through helper node scripts although `agents/fix-review-findings.md` and `agents/implement-slice.md` forbid it: a hook refuses an interpreter run of a just-written script that writes into a worktree. Verify: `node tmp_fix.mjs` that writes a tracked file is denied; `node tools/x.mjs` is allowed.
- [ ] 2. **Stray `python`/`python3` hangs** got past `stdin-interpreter-guard.mjs` (seven agent reports; about 4 of 24 minutes of one starter-session stretch on 2026-11-04): find how, close it, and guard inline-Python file edits.
- [ ] 3. **MSBuild nodes left after agents' `dotnet publish`/`dotnet test`**: check whether publish takes `--disable-build-servers`, and make the agents and `run-checks` use it.
- [ ] 4. **A check that builders queried context7** before library flags (one skipped it for `-clp:PerformanceSummary`).
- [ ] 5. Smaller guards from 2026-11-03: a builder ran `git checkout --` in its own worktree; a `sed -i` flipped `StoryVoicing.cs` to LF (the shell-write guard's coverage).
- [ ] 6. **`note-claims.mjs`** gives "merged" to the wrong PR in a list like "#113 (..., merged), #114 (...)" and treats a backticked `%VAR%` example as a path; then ClaudeSkills #114's own leftovers listed in its body.
- [ ] 7. **A script that names the process holding a folder that will not delete** (`D:\ReadTheStupidText-worktrees\s233`, the fifth "Permission denied" with no holder named; empty folders `s233`, `fl-0859-w1`, `fl-1301-final` were still there on 2026-11-01 22:43).
- [ ] 8. **A hook that refuses edits to a worktree while a device run holds it** (the agent-level guard exists: `device-test` writes `device-test-running-<id>` in the git dir; a hook is unbuilt; the leases would need to record the worktree path).
- [ ] 9. **`archive_docs.py` blank lines**: it leaves blank-line runs in `plan.md` (its `kept_text` keeps a segment's trailing blank lines; the 2026-10-30 pass made an 8-line run); collapse them and add a `verify` check.
- [ ] 10. **`phone-network-guard.mjs`** refuses a read-only `git grep "x\|maestro driver"` because its statement split cuts a quoted pattern at `\|`: split only outside quotes, with a test.
- [ ] 11. **Check the ClaudeSkills code PRs merged from this repo's sessions between 2026-10-31 16:08 and 2026-11-01 12:02** (the merge gate likely read the wrong repo's PR then, fixed by #127; #116's merged `shell-write-guard.mjs` differs from its reviewed blob): compare each against its review files and review #116's last change.
- [ ] 12. **A list-only `prune-reviews.mjs`** for the reviews folders (478 files in this repo's, about 90 in ClaudeSkills', kept since 2026-10-25; the loop deletes only its own round's).
- [ ] 13. **A hook that stops `start-detached.ps1`** unless the user's own message in that turn asks to start the run (d75d7cfc: "any reason not to run the waves?" was read as a go).
- [ ] 14. **Rule: a parked PR's park note** goes into the slice file on main through the index pass, never as a push to the PR branch (night 2026-11-03-2349: that push made #792 conflict with main).

## Verification

- Each item has a test or a recorded run in the ClaudeSkills PR; record the PR numbers here. Items already fixed when the work starts are ticked with the PR that fixed them, read from the repo, not from memory.
