# Slice 307 — ClaudeSkills hooks and guards leftovers
**Product:** Shared

*Built 2026-11-07, merged as PR ashkansirous/ClaudeSkills#186 (cdbc54b7; `install.ps1` exit 0). Item 10 not done. Not yet verified: see the end of this file.* Built in the ClaudeSkills repo (`D:\ClaudeSkills`), in its own worktree and PR. This repo holds the record. Moved from plan.md's *Outstanding work*: every other ClaudeSkills tooling bullet (feedback loops 2026-10-31-1301 final, 2026-11-01-0859 rounds 1-2, 2026-11-03-2349 final, 2026-11-06-0042).

**Touches:** (ClaudeSkills repo, not this one; actual paths) `skills/feedback-loop/**`, `skills/run-wave/**`, `skills/run-all-waves/**`, `skills/archive-docs/**`, `agents/*.md`, `home/settings.json`, `.github/scripts/select-tests.mjs` (`phone-network-guard.mjs` is under `skills/unattended-device-run/` and is unchanged from main); here: `docs/plan/slices/307-claudeskills-hooks-and-guards-leftovers.md`. Check each path in `D:\ClaudeSkills` at claim.

## Work

- [x] 1. **Interpreter-written worktree files.** Builder agents twice rewrote `UsageCounter.cs` through helper node scripts although `agents/fix-review-findings.md` and `agents/implement-slice.md` forbid it: a hook refuses an interpreter run of a just-written script that writes into a worktree. Verify: `node tmp_fix.mjs` that writes a tracked file is denied; `node tools/x.mjs` is allowed.
- [x] 2. **Stray `python`/`python3` hangs** got past `stdin-interpreter-guard.mjs` (seven agent reports; about 4 of 24 minutes of one starter-session stretch on 2026-11-04): find how, close it, and guard inline-Python file edits.
- [x] 3. **MSBuild nodes left after agents' `dotnet publish`/`dotnet test`**: check whether publish takes `--disable-build-servers`, and make the agents and `run-checks` use it.
- [x] 4. **A check that builders queried context7** before library flags (one skipped it for `-clp:PerformanceSummary`).
- [x] 5. Smaller guards from 2026-11-03: a builder ran `git checkout --` in its own worktree; a `sed -i` flipped `StoryVoicing.cs` to LF (the shell-write guard's coverage).
- [x] 6. **`note-claims.mjs`** gives "merged" to the wrong PR in a list like "#113 (..., merged), #114 (...)" and treats a backticked `%VAR%` example as a path; then ClaudeSkills #114's own leftovers listed in its body.
- [x] 7. **A script that names the process holding a folder that will not delete** (`D:\ReadTheStupidText-worktrees\s233`, the fifth "Permission denied" with no holder named; empty folders `s233`, `fl-0859-w1`, `fl-1301-final` were still there on 2026-11-01 22:43).
- [x] 8. **A hook that refuses edits to a worktree while a device run holds it** (the agent-level guard exists: `device-test` writes `device-test-running-<id>` in the git dir; a hook is unbuilt; the leases would need to record the worktree path).
- [x] 9. **`archive_docs.py` blank lines**: it leaves blank-line runs in `plan.md` (its `kept_text` keeps a segment's trailing blank lines; the 2026-10-30 pass made an 8-line run); collapse them and add a `verify` check.
- [ ] 10. **NOT DONE (open).** **`phone-network-guard.mjs`** refuses a read-only `git grep "x\|maestro driver"` because its statement split cuts a quoted pattern at `\|`: split only outside quotes, with a test.
- [x] 11. **Check the ClaudeSkills code PRs merged from this repo's sessions between 2026-10-31 16:08 and 2026-11-01 12:02** (the merge gate likely read the wrong repo's PR then, fixed by #127; #116's merged `shell-write-guard.mjs` differs from its reviewed blob): compare each against its review files and review #116's last change.
- [x] 12. **A list-only `prune-reviews.mjs`** for the reviews folders (478 files in this repo's, about 90 in ClaudeSkills', kept since 2026-10-25; the loop deletes only its own round's).
- [x] 13. **A hook that stops `start-detached.ps1`** unless the user's own message in that turn asks to start the run (d75d7cfc: "any reason not to run the waves?" was read as a go).
- [x] 14. **Rule: a parked PR's park note** goes into the slice file on main through the index pass, never as a push to the PR branch (night 2026-11-03-2349: that push made #792 conflict with main).

## Verification

- Each item has a test or a recorded run in the ClaudeSkills PR; record the PR numbers here. Items already fixed when the work starts are ticked with the PR that fixed them, read from the repo, not from memory.

## User answer (2026-11-07, before the night run)

- **Hook edits allowed.** The user approved changing the hooks and guards named in this slice, including the
  `start-detached.ps1` hook of item 13 (through the normal two-pass review gate and a ClaudeSkills PR). It covers
  only the hooks this slice names, not `autoMode`, `permissions` or other settings.

## Built (2026-11-07, ClaudeSkills PR #186, merge `cdbc54b7`)

1. `worktree-script-guard` (advisory): warns on an interpreter run of a just-written script that writes into a worktree.
2. `stdin-interpreter-guard`: options-only and `& python` forms are denied.
3. Agents pass `--disable-build-servers` to `dotnet publish`.
4. context7: a report-line rule, no enforcement.
5. `sed -i` on a CRLF file is denied (Git for Windows sed flips CRLF; the evidence test runs on Windows only). A source-less `git checkout -- f` / `git restore f` is warned (advisory).
6. `note-claims.mjs` fixes. The "ClaudeSkills #114's own leftovers" part was skipped: too vague.
7. `skills/run-wave/folder-holders.ps1` names the process holding a folder.
8. `device-run-edit-guard` (advisory; exempts the `device-test` agent).
9. `archive_docs.py` collapses blank-line runs and `verify` checks them (fenced blocks keep their blanks).
10. **Not done.** Four review passes found safety gaps in every `phone-network-guard.mjs` variant, so it stays as on main. A read-only grep with a quoted `\|adb` pattern is still wrongly denied. Open.
11. Investigation: only #116's `$'..'` quoting fix was changed after its check pass. The review files for #120-#123 were already deleted, so no blob comparison was possible.
12. `skills/feedback-loop/prune-reviews.mjs` (lists only, deletes nothing).
13. `skills/run-all-waves/start-guard.mjs` (advisory).
14. Park-note rule added to `skills/run-all-waves/SKILL.md`.

*Changed while building:* two-strikes design decision: the new heuristic hooks (items 1, 5 checkout/restore, 8, 13) are advisory only. They return `additionalContext`, never a `permissionDecision` (an "allow" would skip the user's prompt), and log to `~/.claude/logs/advisory-hooks.log`. Promoting one to deny is a later slice, decided from that log. Item 10's `isSimpleSearch` exception and wrapper detection were dropped after pass 4 (design file `reviews/s307-design.md`: origin/main's logic kept).

*From review:* files `reviews/s307-high.md` (10 findings), `reviews/s307-medium.md` (8), `reviews/s307-check1.md` (10), `-check2.md` (6), `-check3.md` (7), `-check4.md` (6), `-check5.md` (4), all read in full. The brief states every finding not listed below was applied; this file did not re-check that against the code. The `reviews/` files are under `D:/ClaudeSkills-worktrees/reviews`, so `check-review-skips.mjs` passes these lines only with `--root D:/ClaudeSkills-worktrees` (wave 1, night 2026-11-08-0106). Skips:

- Skipped #1 (`reviews/s307-medium.md`): "treats every untracked script" (worktree-script-guard became advisory by the design decision).
- Skipped #2 (`reviews/s307-medium.md`): "powershell -NoProfile -ExecutionPolicy Bypass" (phone-network-guard reverted to main by the design).
- Skipped #3 (`reviews/s307-medium.md`): "writes to a path held in a variable" (inline-Python write check is advisory by the design).
- Skipped #4 (`reviews/s307-medium.md`): "Go/no-go is whether the last typed human message ends in" (start-guard is advisory by the design).
- Skipped #5 (`reviews/s307-medium.md`): "no automatic clearance" (device-run-edit-guard is advisory by the design).
- Skipped #6 (`reviews/s307-medium.md`): "including harmless uses" (checkout/restore is advisory by the design).
- Skipped #1 (`reviews/s307-check1.md`): "It is a heuristic deny, not advisory, against the design" (resolved: made advisory).
- Skipped #6 (`reviews/s307-check1.md`): "The plain-split view now runs only when" (phone-network-guard reverted to main).
- Skipped #1 (`reviews/s307-check2.md`): "text-only statement skip lets a Wi-Fi cut piped into a shell through" (phone-network-guard reverted to main).
- Skipped #3 (`reviews/s307-check2.md`): "treats any path containing tmp/ or temp/" (low; advisory only, a missed warning).
- Skipped #4 (`reviews/s307-check2.md`): "takes first plain word as script" (low; uncommon form).
- Skipped #5 (`reviews/s307-check2.md`): "assumes each segment starts outside a fence" (low; usually the safe direction).
- Skipped #1 (`reviews/s307-check3.md`): "isSimpleSearch ignores backslash-escaped quotes" (phone-network-guard reverted to main, `isSimpleSearch` removed).
- Skipped #2 (`reviews/s307-check3.md`): "still gets the fake-statement split" (low; same as origin/main).
- Skipped #3 (`reviews/s307-check3.md`): "keeps the old spurious deny" (low; same as origin/main).
- Skipped #6 (`reviews/s307-check3.md`): "drops its whole batch of 1000" (low; reported as PARTIAL, not silent).
- Skipped #7 (`reviews/s307-check3.md`): "advisory-missing fallback only writes stderr" (low; style only).
- Skipped #1 (`reviews/s307-check4.md`): "isSimpleSearch treats backslash as escape in every shell" (phone-network-guard reverted to main).
- Skipped #2 (`reviews/s307-check4.md`): "counts as a simple search" (phone-network-guard reverted to main).
- Skipped #3 (`reviews/s307-check4.md`): "Returns true when a quote is still open at loop end" (phone-network-guard reverted to main).
- Skipped #5 (`reviews/s307-check4.md`): "sed -i CRLF check only sees literal filenames" (low).
- Skipped #6 (`reviews/s307-check4.md`): "counts redirections like" (low, unconfirmed).
- Skipped #1 (`reviews/s307-check5.md`): "the sed -i CRLF deny is not gated on win32" (low).
- Skipped #2 (`reviews/s307-check5.md`): "evidence test accepts any win32 sed" (low).
- Skipped #3 (`reviews/s307-check5.md`): "never removed; leaves stale temp dirs each run" (low).
- Skipped #4 (`reviews/s307-check5.md`): "evidence covers only one path form" (low).

Note: the brief puts check4 #4 (the `-MaxFiles` range check) among the low findings left alone, but that findings file itself reports it fixed, so it is not listed above.

#### For CLAUDE.md (lifted 2026-11-07; not added to this repo's CLAUDE.md: both rules govern ClaudeSkills tooling and already live there)

- New heuristic hooks are advisory (`additionalContext` only, never a `permissionDecision`; an "allow" would skip the user's prompt) and log to `~/.claude/logs/advisory-hooks.log`; promote one to deny only from that log. Applies to `skills/feedback-loop/advisory.mjs` users. (Slice 307: docs/plan/slices/307-claudeskills-hooks-and-guards-leftovers.md)
- A parked PR's park note goes into the slice file on main through the index pass, never as a push to the PR branch (that push made #792 conflict with main). (Slice 307: docs/plan/slices/307-claudeskills-hooks-and-guards-leftovers.md)

#### Not yet verified

- Whether Claude Code shows `additionalContext` from a PreToolUse hook that returns no `permissionDecision` (the SDK type marks the field optional; not seen live).
- Slice 307 item 10 (open, not built): `phone-network-guard.mjs` still wrongly denies a read-only grep with a quoted `\|adb` pattern.
