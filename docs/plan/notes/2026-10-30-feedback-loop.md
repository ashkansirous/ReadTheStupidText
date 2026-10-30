# Feedback loop, 2026-10-30

The first run of the `feedback-loop` skill (ClaudeSkills #83), over the session that ran waves on the night of
2026-10-29 and was reviewed with the user on 2026-10-30 (transcript `2fe27d17`, one session; the parallel phone
and Store threads were not included).

**Phase:** the session started as `/run-wave` (implementation). Later the user said "I dont need to build any
of the slices in this thread. It'll be a separate work", so product work found here is planned only. Tooling
fixes were done during the loop.

**Passes:** pass 1 found 8 items not properly tracked. Pass 2 found 2 new ones (plus the pass 1 items still on
unmerged branches). Pass 3 was run after this note was written; its counts are in the PR body.

## Findings and where they went

| Entry | Finding | Where it went |
| --- | --- | --- |
| loop | The installed digest script printed nothing: the skills folder is a link and the entry check compared two spellings of one file | ClaudeSkills #85 (real-path check, test) |
| 3153, 3294 | Slice 200's "left for later" items were only in a built slice's file | *Outstanding work* (Slice 200 line); own-machine Windows rows into Slice 219 |
| 2452, 4228 | "I'll split Dino's CLAUDE.md between waves" was never planned; the file is 41 KB | *Outstanding work* line (run `archive-docs` between waves) |
| 4879 | The Slice 192 line still said the store refresh was unconfirmed | Checked live: all 15 books in `/api/stories` carry `creditsByLanguage`; line updated |
| 4748 | Slice 195's "Dino narrator on device not run" was missing from *Not yet verified* | Slice 195 file and its *Outstanding work* line |
| 7353 | `install.sh`'s jq merge was never run (no jq here) | ClaudeSkills #86: a Windows CI job runs every node test with jq |
| 7185, 7199 | A builder ran `install.ps1` from a worktree against the real profile | ClaudeSkills #86: both installers refuse a linked worktree (test) |
| 66, 228 | The Slice 111 Play Data safety item came back after the user had answered it twice | ClaudeSkills #86: rule, a user's answer to a human step is written down in the same turn |
| 4636 | The fix for stating code behaviour without reading it was a rule sentence only | Checked: one rule ("Check before you state"), no duplicate; not script-checkable; the audit pass is the check. No change |
| 3695, 4107 | "The Dino page-turn check was already broken" was said as fact | Slice 205 states only what was seen (failed in every run on 2026-10-30). No change |
| 6840, 7085, 7535 | Turns ended on "/compact first?", "do it now or track it?" and "say audit and I'll..." | The *Feedback loop* rule in `~/.claude/CLAUDE.md` (#83): in-scope, reversible work is done, not offered |
| 7535, 7740 | Auto mode blocked merges of ClaudeSkills #83, #84 and #86 ("merge without review"); #85 went through | Not a settings rule; waits on the user's choice |
