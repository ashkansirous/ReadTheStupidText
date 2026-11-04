# Slice 266 — Waves: stop wasting the minutes between fix attempts

*In progress (wave 2026-11-04), draft PR; the ClaudeSkills half goes in its own PR on fix/s266-stdin-empty-heredoc.* *Planned 2026-11-04.* Tooling only (ClaudeSkills hooks and skills, this repo's device-test tools); no app change. The user, on seeing Slice 264's Windows attempts take 12 minutes apart: "plan fixing this shit!"

**Touches:** `tools/emulator/build-cache.ps1`, `tools/emulator/install-debug.ps1`, `tools/desktop-vm/Build-TestPackage.ps1`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/emulator/run-flows.ps1`, `tools/emulator/README.md`, `tools/desktop-vm/README.md` (last three added at claim: the Android build call sites pass the tree scope, and both READMEs document the build commands), `tools/CLAUDE.md`, `tools/desktop-vm/CLAUDE.md`, `docs/plan/slices/266-wave-fix-attempt-speed.md`; ClaudeSkills: `skills/feedback-loop/stdin-interpreter-guard.mjs`, `skills/feedback-loop/stdin-interpreter-guard.test.mjs`, `skills/run-wave/SKILL.md`, `agents/fix-review-findings.md`, `agents/implement-slice.md`.

## What was measured (2026-11-04, night 2026-11-04-1441 wave 1, Slice 264's Windows reorder crash)

Read from the wave session `bb3514da` and its subagent transcripts, and from `reviews/s264-vm-diag*`:

- Two gaps between VM attempts, 16:15:41-16:27:12 (11.5 min) and 16:29:53-16:42:18 (12.4 min). Of those 24 minutes: about 4 were reading and editing code; **4.1 were two hung `python3` commands** (killed after about 2 min each); **4.8 were the wave waiting for the parallel emulator run** before it started the next fix; the rest were builds (Android Debug twice, a Windows Release publish as the fixer's fast check, then the VM run rebuilding the Windows package again: 47 s warm, 204-234 s cold).
- Each VM run itself took 1.4-1.7 min. Twice the Windows build first waited on "Waiting for another build of another app in this checkout" (`tools/emulator/build-cache.ps1:81`, the per-checkout lock: two builds in one worktree share its `obj` folders).

**The python hang, reproduced:** on this PC `python3 - < /dev/null`, `python - < /dev/null`, `py - < /dev/null` and an empty quoted heredoc (`python3 - <<'EOF' 2>/dev/null || echo nopy` then `EOF`) never return (killed by `timeout` at 8-20 s); `printf '' | python3 -` returns at once, and so does a heredoc with a body. Agents use exactly that empty heredoc as a "is Python here?" probe: seven such calls in tonight's wave transcripts. `stdin-interpreter-guard.mjs` lets every heredoc through on purpose, and its refusal text even recommends `python3 - <<'EOF'`. Cause of the hang (Windows Python reading `NUL` as a console) is likely, not proven.

## Work

- [ ] **Python probe hang (do first; it is a ClaudeSkills hook).** `stdin-interpreter-guard.mjs` also denies an interpreter whose stdin is an EMPTY heredoc (body has no line), `< /dev/null` or `< NUL`; the refusal says Python is installed (`python` works), so never probe for it, and use `-c` or a script file. Tests use the seven real commands from the 2026-11-04 transcripts plus the passing forms (`printf '' | python3 -`, a heredoc with a body, `-c`). Verify: the exact probe is refused, and a rerun of the reproduction above shows the passing forms still pass.
- [ ] **One Windows build per attempt.** The fixer's Windows fast check builds the same package the VM run uses, through `Build-TestPackage.ps1` (its build-cache key), instead of a separate `dotnet publish -c Release`; the VM run then finds it in `_builds` (cache hit, no second build). `fix-review-findings` and `implement-slice` briefs say so for Dino and RTST Windows. Verify: in one attempt, the VM run's log says it reused the cached package.
- [ ] **No cross-app build wait in one worktree.** The emulator (Android) and Windows test builds each build into their own folder (`--artifacts-path`, as the phone builds already do, `build-cache.ps1:94-96`), so they pass their own tree scope and stop waiting on each other. Verify: start an Android and a Windows test build in the same worktree together; neither prints "Waiting for another build".
- [ ] **Do not hold a fix for an unrelated device run.** `run-wave` SKILL: when a device run fails, the next fixer starts at once with that failure; a parallel run on another device is not waited for unless it tests the same file or behaviour; its result goes to the same fixer as a follow-up message. Verify: the rule is in the skill, and the next wave transcript shows a fixer starting within a minute of a VM failure.
- [ ] **Before and after numbers.** After the four items above, one Windows fix attempt (code change, build, VM run) on a real slice is timed from the fixer's first edit to the VM result, and written here next to the measured 11.5-12.4 minutes. Target: under 6 minutes.

## Out of scope

- The Windows `LayoutCycleException` itself (Slices 235 part C and 264 hit it on the shelf): that needs its own design pass, recorded in those slices.

## Verification

- `node --test` for the guard in ClaudeSkills; the build-wait check in two shells; the timed attempt above.
