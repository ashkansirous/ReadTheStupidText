# Slice 449 — Tooling: every Maestro launcher picks an installed JDK, not only run-flows.ps1
**Product:** Shared
**Touches:** `tools/emulator/store-screenshots.ps1`, `tools/phone/**` (Maestro launchers), `tools/emulator/common.ps1`, `tools/emulator/test-jdk-pick.ps1`, `tools/CLAUDE.md`

*Built wave 4, night 2026-11-27-0154; merged as PR #1410 (b320b15). No device run: the change is a pre-check and the test scripts cover it.* (Planned 2026-11-25, moved out of Slice 389.)

**Why:** Slice 389 made `run-flows.ps1` call `Set-ProcessJavaHome` (`tools/emulator/common.ps1`) so a dead `JAVA_HOME` is replaced for the process. `tools/CLAUDE.md` line 71 says `store-screenshots.ps1` and the `tools/phone` Maestro launchers still do not call it (read on origin/main 1a8bf725: the only caller outside the self-test is `run-flows.ps1:318`). A dead `JAVA_HOME` still breaks them.

## Work items
- [x] `store-screenshots.ps1` and each `tools/phone` script that starts Maestro dot-source `common.ps1` and call `Set-ProcessJavaHome`; no JDK exits with the same one-line `JDK not found` (exit 7).
- [x] Extend `test-jdk-pick.ps1` (or a phone self-test) so each launcher's call is covered; drop the "do not call it yet" sentence in `tools/CLAUDE.md`.

*Changed while building:* `tools/phone` has no script that starts Maestro, so no phone launcher needed the call. Only `store-screenshots.ps1` lacked `Set-ProcessJavaHome`; it now calls it after the `-DryRun` branch and before any emulator change. A missing JDK exits 7, passed through, and the script header lists it. `test-jdk-pick.ps1` now checks that `run-flows.ps1` and `store-screenshots.ps1` call it and fails if a `tools/phone` script starts Maestro (comment lines ignored). The `tools/CLAUDE.md` sentence is updated.

*From review:* (`reviews/pr-1410-high.md` and `reviews/pr-1410-medium.md`, both read in full)
- High 1 (guard regex and comment handling) and high 3 (exit 7 missing from the header and mapped to 1): fixed.
- Skipped #2 (`reviews/pr-1410-high.md`): "Both work items are still unticked" - update-docs writes the built note and ticks the items.
- Skipped #4 (`reviews/pr-1410-high.md`): "has no docs/plan/slices/449-... path" - a source-list path changes no behaviour.
- Skipped #5 (`reviews/pr-1410-high.md`): "after the `-DryRun` exit" - `-DryRun` starts no Maestro, so it needs no JDK.
- Skipped #1 (`reviews/pr-1410-medium.md`): "scans only tools/phone and two named emulator scripts" - only tools/phone and emulator scripts start Maestro today; the slice's scope is those.
- Skipped #2 (`reviews/pr-1410-medium.md`): "behaviour-neutral reformat" - a reformat that breaks the match fails the test loudly, which is the intended guard.

#### Not yet verified
None. The change is a pre-check covered by `test-jdk-pick.ps1`; the slice names no device verification.
