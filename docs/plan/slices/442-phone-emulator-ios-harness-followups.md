# Slice 442 — Phone, emulator and iOS harness follow-ups from Slice 393
**Product:** Shared
**Touches:** `tools/emulator/store-screenshots.ps1`, `tools/emulator/reset-dino-language.ps1`, `tools/phone/**`, `.github/scripts/ios-sim-*.sh`, `.github/workflows/dinoreader-ios-sim.yml`

*Planned 2026-11-25 (plan-cleanup of that day).*

*In progress (wave 2026-11-27), draft PR.*

Small, low findings left open by the Slice 393 reviews, all in this repo's tooling (full text in the Slice 393 file under *From review*, *Follow-ups from PR #1071* and *Follow-ups from the check pass after the CI fix*):

- `tools/emulator/store-screenshots.ps1`: read `$LASTEXITCODE` per adb call so the demo-mode early stop can fire; fail with start-warm's exit code when `Confirm-EmulatorForDemo` cannot start the emulator.
- `tools/emulator/reset-dino-language.ps1`: do not clear the whole logcat per try (use `-T`), check `pm list packages` and the app pid first, report a missing install correctly.
- `.github/scripts/ios-sim-driver-ready.sh`, `ios-sim-store-shots.sh`, `ios-sim-boot.sh`, `dinoreader-ios-sim.yml`: time-left guard against `SCRIPT_BUDGET_SECONDS`, step timeout versus 2x330 s worst case, `kill_tree` between attempts, the 20 s settle only for Maestro callers.
- `tools/phone/lease.ps1`, `preflight.ps1`, `common.ps1`, `connect.ps1`, `test-phone-leftovers.ps1`: move `Find-Phone` below the `-Clear` block, remove the unreachable second `if ($Clear)`, `-Serial` shape check and `Select-ConnectedAddress` honouring `-Serial`, `-Release` restoring `stay_on_while_plugged_in`, a runnable preflight test with a stubbed foreign lease, try/finally in `test-phone-lib.ps1`.

Verification: each tool's own self-test (`test-store-screenshots.ps1`, `test-reset-dino-language.ps1`, `test-phone-leftovers.ps1`) stays green in `tools-selftests.yml`.
