# Slice 449 — Tooling: every Maestro launcher picks an installed JDK, not only run-flows.ps1
**Product:** Shared
**Touches:** `tools/emulator/store-screenshots.ps1`, `tools/phone/**` (Maestro launchers), `tools/emulator/common.ps1`, `tools/emulator/test-jdk-pick.ps1`, `tools/CLAUDE.md`

*Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 389; not built).*

**Why:** Slice 389 made `run-flows.ps1` call `Set-ProcessJavaHome` (`tools/emulator/common.ps1`) so a dead `JAVA_HOME` is replaced for the process. `tools/CLAUDE.md` line 71 says `store-screenshots.ps1` and the `tools/phone` Maestro launchers still do not call it (read on origin/main 1a8bf725: the only caller outside the self-test is `run-flows.ps1:318`). A dead `JAVA_HOME` still breaks them.

## Work items
- [ ] `store-screenshots.ps1` and each `tools/phone` script that starts Maestro dot-source `common.ps1` and call `Set-ProcessJavaHome`; no JDK exits with the same one-line `JDK not found` (exit 7).
- [ ] Extend `test-jdk-pick.ps1` (or a phone self-test) so each launcher's call is covered; drop the "do not call it yet" sentence in `tools/CLAUDE.md`.
