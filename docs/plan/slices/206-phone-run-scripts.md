# Slice 206: phone scripts for unattended runs

*Built 2026-10-30. Fast tests pass; S21 (Android 15) device run done for lease, preflight, tap and the setting and clear resets; the items under Not yet verified are still open.*

**Why:** the 2026-10-29 S21 night (docs/plan/notes/2026-10-29-s21-night-runs.md) lost most of its first run to a PIN lock nobody checked,
skipped every language check because no agent knew how to tap through Settings, deleted a pack instead of moving it aside, stopped
when the Norsk crash broke the dev app, and once ran two harness scripts on the phone at the same time. Each agent improvised the fix.
These scripts make the fixes one command each.

**Touches:** `tools/phone/preflight.ps1`, `tools/phone/reset-dev-app.ps1`, `tools/phone/tap.ps1`, `tools/phone/lease.ps1`,
`tools/phone/common.ps1`, `tools/phone/audio-timing.ps1` (lease check only), `tools/phone/test-*.ps1`, `tools/phone/README.md`,
`tools/CLAUDE.md`, `.gitattributes`

**Platforms:** Android phone tooling only (no iPhone can be driven from this PC; iOS checks stay with TestFlight). No app code changes.

## Scripts (all dot-source `common.ps1`, wireless adb only, every adb call through `Invoke-Adb`, `-Serial` optional as in `connect.ps1`)

1. **`preflight.ps1 [-Serial s] [-App rtst|dino|both]`**: one go/no-go before an unattended run. Checks and prints one line each:
   - connected over wireless adb (else the `connect.ps1` ask, exit 2);
   - charging (`Get-PowerState`);
   - screen lock is None or Swipe: `locksettings get-disabled` / `dumpsys trust` or `cmd lock_settings`, whichever works on Android 15
     (confirm with library-docs / the adb docs; if no reliable read exists, check `dumpsys window | grep mDreamingLockscreen|isKeyguardSecure`
     style output for a *secure* keyguard). A PIN, pattern or password = NO-GO with "ask the user to set Screen lock to None or Swipe";
   - stay awake while charging (`settings get global stay_on_while_plugged_in`, nonzero), set it if it is 0 and say so (it is a dev setting,
     restored by `-Restore`? no: just report and set; the README says it stays on);
   - free space on `/data` (warn under 2 GB);
   - the `.dbg` dev app(s) installed and their versionName;
   - the phone lease (below) free or held by the caller.
   Exits 0 for GO, 1 for NO-GO, prints `PREFLIGHT GO` / `PREFLIGHT NO-GO: <reasons>` as its last line.
2. **`reset-dev-app.ps1 -App rtst|dino [-Level setting|clear|reinstall] [-Serial s]`**: the reset ladder for a broken dev install. Only ever
   touches `uk.sirous.readthestupidtext.dbg` / `uk.sirous.dinoreader.dbg`; refuses any other package (hard-coded allow-list, test it).
   - `setting` (default first step): force-stop, set the app language back to English in its shared_prefs via `run-as <pkg>` (the key the
     app really uses: read `src/*` settings constants, do not guess), relaunch, confirm the app logs within 60 s (by pid);
   - `clear`: before clearing, move the downloaded packs aside into `/sdcard/Download/rtst-packs-aside/<pkg>/` (or the app's external
     files dir) via run-as copy, then `pm clear`, then copy the packs back, skip first run with the existing first-run hook, confirm alive;
   - `reinstall`: `adb install -r` the cached APK from the build cache (`tools/emulator/build-cache.ps1`, same key logic as `audio-timing.ps1`),
     never uninstall the store app; then as `clear`.
   - `-Level auto`: try setting, then clear, then reinstall, stopping at the first level where the app comes up alive. Prints which level worked.
3. **`tap.ps1 (-Text t | -Id resource-id | -Desc content-desc) [-Index n] [-Scroll] [-WaitSeconds 10] [-Serial s]`**: `uiautomator dump`
   to `/sdcard/window.xml`, pull it, find the node (exact match first, then case-insensitive contains), tap the centre of its `bounds`, dump
   again and print what changed (the new screen's distinct texts). `-Scroll` swipes up and retries until found or 6 swipes. Exit 3 when not
   found, listing the clickable texts on screen. Also `tap.ps1 -List` prints the visible texts/ids without tapping. The XML parse is a pure
   function with a no-device self-test (`test-tap.ps1`) on a saved fixture dump.
4. **`lease.ps1 -Take -Owner <name> [-Minutes 120] | -Release -Owner <name> | -Show [-Serial s]`**: one session or agent holds the phone at a
   time. Lease file `<worktrees>/.phone/lease-<serial-sanitised>.json` (`Get-WorktreesRoot` from `tools/emulator/activity.ps1`) with owner,
   taken, expires. An expired lease is free. `-Take` by another owner while held exits 4 naming the holder and its expiry. Write via temp
   file + rename. `common.ps1` gets `Assert-PhoneLease $Serial` used by `audio-timing.ps1`, `reset-dev-app.ps1` and `tap.ps1`: when a lease
   exists and `$env:RTST_PHONE_OWNER` differs from its owner, exit 4. No lease = allowed (backward compatible).

## Also

- `.gitattributes`: `*.md text eol=lf` plus keep existing files' current endings safe (check which endings `docs/plan/**` use on main
  first and pin those, so no future docs edit rewrites a whole file as PR #579 did with `slices-done.md`). Do not renormalise existing files
  in this PR.
- README: one bullet per new script; `tools/CLAUDE.md`: replace the unattended-run bullet's improvised steps with "run `preflight.ps1`;
  take `lease.ps1`; on a broken dev app run `reset-dev-app.ps1 -Level auto`; switch settings with `tap.ps1`".

## Checks

- Fast, no device: `test-tap.ps1` (fixture parse, bounds centre, not found), a lease self-test (take/held/expired/release in a temp dir),
  the reset allow-list refusing `uk.sirous.readthestupidtext`; run each in `pwsh` and Windows PowerShell 5.1.
- Device (run on the S21 2026-10-30, see Device run below): preflight on the S21 with the lock None and with a PIN; tap through
  RTST Settings to Norsk and back; `reset-dev-app.ps1 -Level auto` after forcing the Norsk crash on a pre-201 build.

## Changed while building

- `.gitattributes` pins `*.md` and `docs/plan/**` to LF (existing files not renormalised).
- `lease.ps1` is keyed by `ro.serialno`, uses a named mutex, fails closed on a corrupt lease file, and auto-renews when under 15 minutes remain.
- `preflight.ps1` is read-only; `-Fix` sets stay-awake. Its last line is `PREFLIGHT GO` or `PREFLIGHT NO-GO: <reasons>`.
- `tap.ps1` also takes `-ExpectText` and checks the foreground app is the `.dbg` app. Exit codes: 3 not found or ambiguous, 5 expected text missing, 6 wrong app, 7 tap not confirmed.
- `reset-dev-app.ps1 -Level setting|clear|reinstall|auto` touches only the two `.dbg` packages. Before `pm clear` it writes an aside archive and verifies it; the newest 3 are kept; the `no-usage` and first-run markers are restored.
- Self-tests: `tools/phone/test-phone-lib.ps1` and `tools/phone/test-tap.ps1`.

## From review

- High pass: 12 findings; medium pass: 12 of 13 fixed. The one skipped is the `.gitattributes` change, which is intended.

## Found and fixed on the S21

- A `tar` redirect to `/sdcard` wrote 0 bytes; the archive is now piped through `cat`.
- The prefs file is `<pkg>_preferences.xml`, not a fixed name; the script now finds it by its language key.

## Device run (S21, Android 15, 2026-10-30)

- Lease, preflight and tap passed. Preflight gave NO-GO on battery; it read lock None correctly from `KeyguardServiceDelegate secure=false` and `lock_settings` disabled. Tap went Settings > language picker > cancel > back.
- Rerun after the fixes: setting level nb -> en confirmed; clear level with a 153 MB / 735-file archive restored the packs, the app stayed alive and the `no-usage` marker was present.

#### For CLAUDE.md

- The `tools/CLAUDE.md` unattended-run bullet is already updated in this branch's diff (preflight, lease, reset-dev-app, tap). Nothing further to lift.

#### Not yet verified

- `preflight.ps1` reading a PIN or pattern lock as NO-GO.
- `reset-dev-app.ps1 -Level reinstall`.
- Dino resets (`-App dino`).
- `tap.ps1` on Dino Reader.
- The lease used by two real sessions at once.
