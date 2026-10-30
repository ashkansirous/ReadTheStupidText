# Feedback loop, S21 session (2026-10-30)

Session read: `db0cdbf1` (the S21 night of 2026-10-29/30 and its follow-ups: phone tooling, the check runner, the App Store run, the feedback-loop skill fixes). The other session's loop over `2fe27d17` is [2026-10-30-feedback-loop.md](2026-10-30-feedback-loop.md).

**Phase:** product slices were planned only in this session (Slices 201 and 202 were built during the night under the user's "you dont need my decision"); tooling (phone scripts, check runner, skills, rules) was ordered and built.

## Passes

| Pass | Tracked | Pending | Weak | Untracked | False claim | Should have acted |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 31 | - | 4 | 4 | 1 | 1 |
| 2 | 36 | - | 4 | 6 | 1 | 2 |
| 3 | 34 | 7 | 3 | 6 | 1 | 1 |
| 4 | see the final reply | | | | | |

## Findings and where they went

**Tooling fixed now (ClaudeSkills):**

- The digest dropped permission denials, agent reports and failed calls; no check for "did the wrong task" or "handed the user manual work"; slow stretches invisible; "confirm the goal" rule. [#87](https://github.com/ashkansirous/ClaudeSkills/pull/87)
- Slice-number collisions (212 against PR #600, 215 against another session): `parallel_check.py --check-slice`, run by `ship-branch` before push; reviews returned before the forked review finished; a fix agent ran the phone runner; slices built before a claim PR; `!` commands in the digest. [#88](https://github.com/ashkansirous/ClaudeSkills/pull/88)
- Worktree removal failed on long paths (`core.longpaths`); mirror-to-drive reported counts it called unreliable and a deletion was relayed as fact (now verified by listing Drive); python tests not in CI; turns ending on in-scope questions (Stop hook). [#89](https://github.com/ashkansirous/ClaudeSkills/pull/89)
- The user asked twice what happened in the past hour (a status line per 10-minute check); heredocs corrupted Windows paths twice; the auditor had no Write tool. [#90](https://github.com/ashkansirous/ClaudeSkills/pull/90)

**Plan and docs (this PR):**

- Slice 139: "a tap before language detection reads untranslated, probably by design" and "Next step: Slice 198" were wrong. It is a bug, planned as Slice 211; Slice 198 closed with no change. Fixed in `plan.md` and the night note.
- Slice 201: the review leftovers were recorded as "none changes behaviour"; at least two do. Itemised in the slice file and *Outstanding work*.
- Slice 202 merged on an incomplete second review pass; Slice 213's PR #610 had about half its code written after both reviews. Slice 202 line in *Outstanding work*; Slice 216 items 9 and 10.
- Slice 213: "AOT saves about 1.8 s per cold start" was stated as fact; the numbers were not taken under the same conditions and one JIT fresh install read 2.8 s. Corrected to "unproven" (Slice 214 owns the measurement).
- Slice 105: the no-voice check was "PASS, weak" (every app language has a voice); `plan.md` said PASS. Corrected.
- *Outstanding work* gained: Slices 201 and 202 unverified items, Slice 213 `notification-text` never seen failing, the S21 `zz-supertonic-3.bak`, Samsung Auto Blocker back on, the untried iPhone 8 crash-log read, and the App Store "update with the values" request waiting on the user's confirmation of five values.
- App Store drafts and the stale Play listings: [RTST #619](https://github.com/ashkansirous/ReadTheStupidText/pull/619).

**Corrections told to the user:**

- Slice 212 was described to the user as "planned, not built". It is the other session's slice, built and verified in PR #600.
- The App Store run: the user wanted TestFlight only; I sent them to the public App Store submission and had a full listing drafted. Rule in #87.

**Recorded, no tool fix:**

- An agent's read of the App Store Connect key was blocked by the permission check; two hours later the store-listing agent read the same secrets store and called the API. What allowed the second read is unknown; not guessed.
- Review findings were fixed wholesale on Slices 206 and 213 without triage; the gate's step 3 already requires triage.
- At least eight "check before you state" slips this session (for example offering a forced update RTST doesn't have). The rule exists; no script can check it.
- Review findings skipped on purpose: #87 (denial detection relies on English phrases; agent results keep head and tail only), #88 (closed-PR exclusion capped at 300; a same-named branch in another clone), #89 (the long-paths text repeated in three places; no per-file detail when the post-sync Drive listing fails).
