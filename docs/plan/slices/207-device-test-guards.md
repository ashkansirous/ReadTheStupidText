# Slice 207: guards for emulator, VM and flow mistakes

*Built 2026-10-30. Verified on the emulator and the VM (see below); one flow failure on the emulator is not yet checked on main.*

**Why:** the 2026-10-30 night wave lost device runs to mistakes a script could have caught. Slice 197's emulator check ran
while Slice 191's was still on the same emulator, and its `clearState` wiped 191's own book (nothing stops two
`run-flows.ps1` on one serial: the only mutex, `Lock-Emulator`, guards start/stop). Builders wrote flows that failed on
the device for reasons a text check sees: `openNotifications` (not a Maestro command), a `\+` escape YAML rejects, and
`clearState` that also wiped the voice pack (187). Slice 194's VM run had no network and no voice pack, so its
screenshot was useless, and nothing checked that before the run. Slice 190's checks were briefed for RTST only although
the change was in Mobile.Shared, so the Dino compile error showed up in CI. Two emulators running flows at once
overloaded the PC. Slice 206 already gave the phone a lease, a preflight, taps and a reset ladder; this slice gives
the emulator, the VM and the flows the same kind of guard.

**Touches:** `tools/lease-common.ps1`, `tools/phone/lease.ps1`, `tools/phone/common.ps1`, `tools/emulator/common.ps1`,
`tools/emulator/run-flows.ps1`, `tools/emulator/activity.ps1`, `tools/emulator/lint-flows.ps1`,
`tools/emulator/test-lint-flows.ps1`, `tools/emulator/test-lease.ps1`, `tools/emulator/fixtures/**`,
`tools/emulator/README.md`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/desktop-vm/_common.ps1`,
`tools/desktop-vm/checks/*.ps1` (a `needs` header line only), `tools/desktop-vm/README.md`, `tools/test-targets.ps1`,
`.maestro/**` (comments only), `.maestro/README.md`, `tools/CLAUDE.md`, `.github/workflows/tests.yml` (the existing pwsh
step only)

**Platforms:** test tooling only, no app code. Android emulator, Windows VM.

## Design (settled; build exactly this)

1. **One lease implementation for every device.**
   - Move the key-agnostic lease helpers (`Get-LeasePath`, `Invoke-WithLeaseLock`, `Read-Lease`, take/renew/release)
     out of `tools/phone/lease.ps1` into `tools/lease-common.ps1`, parameterised by kind (`phone`, `emulator`) and key.
     Phone keeps its `.phone/lease-<ro.serialno>.json` path and behaviour exactly (its self-tests must still pass).
   - Emulator lease: `<worktrees>/.emulator/lease-<adb serial>.json` (e.g. `emulator-5554`). Owner from
     `$env:RTST_DEVICE_OWNER`, else `$env:RTST_PHONE_OWNER`, else `run-flows-<pid>`. Expiry 30 min, renewed between flows.
     A lease whose owner pid (store the pid in the file) is no longer running counts as free, as does an expired one.
   - `run-flows.ps1` takes the lease right after `Set-EmulatorInstance`, before install and Maestro. If it's held, it
     waits up to `-LeaseWaitMinutes` (default 15), printing `LEASE waiting on <owner>` once a minute, then exits 4
     naming the holder. Release in `finally`. `tools/phone/audio-timing.ps1 -Emulator` takes the same emulator lease.
2. **One emulator running flows at a time by default.** In `activity.ps1` `Get-ConcurrencyConflict`, the default
   limit becomes one emulator running flows (plus the VM). A second emulator's flows wait as the VM does today.
   `$env:RTST_ALLOW_TWO_EMULATORS=1` restores today's two-emulator limit. Update the rule in `tools/CLAUDE.md` and
   `tools/emulator/README.md`.
3. **`tools/emulator/lint-flows.ps1 [-Path .maestro]`**: a text lint that needs no device and runs in a second.
   Exit 0 clean, 1 with findings as `file:line: message`. Rules:
   - every command after the `---` line is one of the Maestro command names (list in the script, from
     https://docs.maestro.dev/api-reference/commands; the 48 names are in the PR notes), including commands nested
     under `runFlow`/`repeat`/`retry` `commands:`. Unknown name means an error, e.g. `openNotifications`.
   - inside double-quoted YAML strings, a backslash escape outside YAML's set (`0 a b t n v f r e space " / \ N _ L P x u U`)
     is an error (catches `\+`). Single-quoted and plain strings are not checked.
   - a `clearState` line needs a comment on the line above that starts `# clearState:` and says what it wipes
     (the voice/OCR packs, own books) and why that's fine for this flow. Add those comments to the existing flows
     (comments only, don't change any step).
   - the header has an `appId:` that is one of the two dev packages.
   `test-lint-flows.ps1` checks each rule against small fixtures under `tools/emulator/fixtures/lint/`, plus the real
   `.maestro` passing clean. `run-flows.ps1` runs the lint first and stops before any build when it fails.
   The self-tests run by hand and from the slice's checks, not in CI: `tests.yml` stays untouched (changing it ships every app); wiring them into CI is a later slice.
4. **VM preflight in `Invoke-DesktopTest.ps1`.** Before installing, in the guest: check the network (an HTTPS request
   to `https://readthestupidtext.sirous.uk/` with a 10 s timeout) and, for checks that declare it, the voice pack.
   Each check script may carry a header line `# needs: network, voice-pack`. If a needed item is missing, print
   `VM PREFLIGHT NO-GO: <items>` and exit 3 within the first minute, without running the check. `-SkipPreflight`
   bypasses it. Read where the Windows voice pack lives from Infrastructure (pack install folder under the package's
   LocalState) and check its folder for the app the check runs. Add the header to `rtst-panel-screenshot.ps1`
   (voice-pack; the VM is offline by design, so `network` is for checks that truly need it) and any other check that waits on a pack.
5. **`tools/test-targets.ps1 -Checks`**: from the same diff, print the fast-check commands to run: one
   `dotnet build` line per app it names (RTST Android, Dino Android, RTST Windows, Dino Windows, with the flags from
   root `CLAUDE.md`) and one `dotnet test` line per test project the diff reaches (Mobile.Shared, Application, Domain
   reach both test projects; DinoReader.Core reaches `tests/DinoReader.Tests`; Windows-only projects reach
   `tests/ReadTheStupidText.Tests`). A change in `src/ReadTheStupidText.Mobile.Shared/**` always names both apps.
   Add unit cases to the existing test-targets tests if any exist, else a small `tools/test-test-targets.ps1`.

## Not in scope

- Matching flow text against app strings (dynamic text gives false positives).
- A Maestro syntax check through the CLI (the docs show no such command).

## Verification

- Self-tests: `test-lint-flows.ps1`, `test-lease.ps1`, the phone `test-phone-lib.ps1`, all green in `pwsh` and `powershell`.
- `lint-flows.ps1` on `.maestro` is clean after the comments; on a copy with `openNotifications` it fails naming the line.
- One emulator run: start `run-flows.ps1 -App rtst` twice on the same instance; the second prints `LEASE waiting` and
  starts only after the first ends.
- One VM run of `rtst-panel-screenshot.ps1` with no voice pack in the guest prints `VM PREFLIGHT NO-GO: voice-pack (supertonic-3)` within a minute (the guest is offline by design, so no network line).

## Built

- [x] Shared `tools/lease-common.ps1` (phone and emulator); emulator lease in `run-flows.ps1` and `audio-timing.ps1 -Emulator`; one emulator running flows by default (`RTST_ALLOW_TWO_EMULATORS=1` restores two).
- [x] `lint-flows.ps1` plus self-test and fixtures; 16 `# clearState:` comments in the flows.
- [x] VM preflight (`# needs:` headers, `VM PREFLIGHT NO-GO` exit 3, `-SkipPreflight`); `test-targets.ps1 -Checks`.

*Changed while building:* `tests.yml` was deliberately not changed (it is a core path and would ship every app); the self-tests run by hand and from `run-flows.ps1`. The first VM run showed the VM is offline by design, so the screenshot check's header is `needs: voice-pack` only.

*From review:* High: 9 findings applied (`run-flows.ps1` was missing the lease include, a blocker; voice-pack probe before install; lease timing; lint edge cases; `-App auto` exit codes; shared build inputs in `-Checks`). Medium: 6 applied (lease pid on renew, `pack.json` manifest check, lease renew during the activity wait, `Wait-ForLease` spin, `audio-timing.ps1` cleanup, Infrastructure/Documents reach both test projects); 3 low findings skipped.

*Device results:* emulator: lint on 18 flows clean; a second `run-flows.ps1` printed `LEASE waiting on 'run-flows-15292'` and started after the first, no lease left behind. VM: a clean checkpoint gives `VM PREFLIGHT NO-GO: voice-pack (supertonic-3)` with exit 3 in about 60 s; `-SkipPreflight` runs the check.

#### For CLAUDE.md

Already in `tools/CLAUDE.md` in this slice's diff (one emulator running flows by default, leases, lint, VM preflight). Nothing for the root file.

#### Not yet verified

- `.maestro/rtst/cancelled-voice-pack-stays-cancelled.yaml` fails on the emulator (`Download stopped.*` not visible) in both runs. Unrelated to 207 (comment-only change there); not checked on main. Slice 204 was verified on the phone only.
