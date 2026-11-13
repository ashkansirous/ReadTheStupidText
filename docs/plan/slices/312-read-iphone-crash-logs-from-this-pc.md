# Slice 312 — Read the iPhone's crash logs from this PC
**Product:** Shared

*Built 2026-11-07 (wave 2026-11-07, PR #924, merged as ca8b00cf), not yet verified on a real iPhone (item 2 open).* Planned 2026-11-07. Untried: reading the iPhone 8's crash logs from this PC (libimobiledevice `idevicecrashreport`, or an equivalent) so iOS crashes can be checked without the user. Moved from plan.md's *Outstanding work*. Related: [Slice 243](243-crash-report-gaps.md) (dSYMs, symbolication, "did it crash?" command), [Slice 244](244-dino-ios-crashes-2026-11-01.md).

Rule check: the real phone is wireless-debugging only for Android. For an iPhone the transport is Apple's, so the first step is to find out whether a wireless route exists (libimobiledevice supports network pairing after a first USB pairing; Apple's tools use Wi-Fi sync). The user's rule about USB is for the Android phone; if a first iPhone pairing needs a cable, that is one plug-in, said plainly before it happens.

**Touches:** `tools/crashes/pull-iphone.ps1` (new), `tools/crashes/pull-iphone.test.ps1` (new), `tools/crashes/pull-crashes.mjs`, `tools/phone/README.md`, `tools/CLAUDE.md`, `docs/plan/slices/312-read-iphone-crash-logs-from-this-pc.md`

**Night decision (wave 2026-11-07, coordinator):** this night has no iPhone (no phone pre-flight) and the run installs no software on the PC (no driver, no libimobiledevice download: that is the user's machine and a one-time step for them). So the night builds items 1, 3 and 4: the docs lookup, the script (it finds `idevicecrashreport` on PATH or under a `-ToolPath`, and when it is missing says exactly what to install and stops with a clear message), its fixture test, and the README. Item 2 (the real pair and pull) and the real-pull check go to *Not yet verified*.

## Work

- [x] 1. Context7 / `library-docs` for libimobiledevice (`idevicecrashreport`, `idevicepair`, Windows builds and the Apple Mobile Device Support driver it needs); check whether iTunes or Apple Devices is installed here. Write down what works on Windows 11 and what needs a one-time step from the user.
- [ ] 2. Pair and pull: run `idevicecrashreport` against the iPhone (network if the pairing allows, else the one first-time cable pairing), keep the `.ips` files under the worktree's `tmp`, and list them by app and time.
- [x] 3. A script `pull-iphone.ps1` that pulls the reports, filters to Dino and RTST by bundle id, and prints the newest crash per app with its exception type and the faulting thread's top frames; symbolicate with the dSYMs from Slice 243 when they exist (say so when they do not).
- [x] 4. Document in `tools/phone/README.md`: how to run it, what the phone must be (unlocked, trusted, same network).

## Verification

- A real pull from the iPhone returns the crash files from 2026-11-01 (Slice 244) or says there are none; the script's test runs on a fixture `.ips`.

## Built (night 2026-11-07)

Item 1 (context7 `/libimobiledevice/libimobiledevice`): `idevicecrashreport` options `-k` keep, `-n` network, `-u` udid, `-f` filter, `-e` extract; `idevicepair` network pairing works for Apple TV only (`-w`), so an iPhone needs one cable pairing, then `-n` only with Wi-Fi sync on. Windows needs the Apple Mobile Device Support driver (Apple Devices or iTunes). Neither the tools nor Apple Devices were found on this PC (no AppX package, no service, no command), and nothing was installed (night decision). Items 3 and 4 built: `tools/crashes/pull-iphone.ps1`, its fixture test, README line.

*Changed while building:* `pull-iphone.ps1` puts each run in its own timestamped pull folder, counts only crash reports (`bug_type` 309 or an exception block), has `-NoPathFallback`, and exits 0 (ok), 2 (tool missing), 3 (pull failed), 4 (bad `-InputDir`). `-DsymDir` is not used for symbolication yet (warning stays until Slice 243).

*Tests:* `tools/crashes/pull-iphone.test.ps1` 17 checks pass (run twice); `node --test tools/crashes/*.test.mjs` 13/13.

*From review* (files under `reviews/`): high (`reviews/s312-high.md`): all 8 applied. Medium (`reviews/s312-medium.md`): 1, 2, 3, 5, 6, 8 applied; the rest:
- Skipped #4 (`reviews/s312-medium.md`): "Read-Ips assumes one-line header; format unverified against a real file" (needs a real `.ips`; goes to Not yet verified).
- Skipped #7 (`reviews/s312-medium.md`): "termination.reason/code and asi text (MAUI/Mono aborts, watchdog) omitted" (an addition, not a defect).
- Skipped #9 (`reviews/s312-medium.md`): "Get-CrashTime re-parsed inside Sort-Object and again for display" (trivial).

Check pass 1 (`reviews/s312-check1.md`): #1 applied; the rest:
- Skipped #2 (`reviews/s312-check1.md`): "Only `.ips` files read; other extensions (.crash) are ignored" (low; format unverified).
- Skipped #3 (`reviews/s312-check1.md`): "Bundle id and exception/signal/faultingThread field names unverified against a real report" (needs a real file).
- Skipped #4 (`reviews/s312-check1.md`): "Every pull makes a new timestamped folder re-copying all reports" (low).
- Skipped #5 (`reviews/s312-check1.md`): "With -InputDir, -ToolPath/-OutDir/-NoPathFallback are silently ignored" (low).
- Skipped #6 (`reviews/s312-check1.md`): "Touches line lists tools/crashes/pull-crashes.mjs, which this change does not touch" (low; left in the Touches line).

Check pass 2 (`reviews/s312-check2.md`), all low, none applied:
- Skipped #1 (`reviews/s312-check2.md`): "-Frames 0 or negative. `Select-Object -First -1` throws" (low).
- Skipped #2 (`reviews/s312-check2.md`): "Find-CrashTool falls back silently to the PATH copy when an explicit -ToolPath is wrong" (low).
- Skipped #3 (`reviews/s312-check2.md`): "unparsable captureTime returns MinValue silently" (low, unconfirmed).
- Skipped #4 (`reviews/s312-check2.md`): "ConvertFrom-Json on real .ips bodies with keys differing only by case would throw" (low, unconfirmed).

#### For CLAUDE.md (lifted 2026-11-07)

None beyond the `tools/CLAUDE.md` edit already made in this slice.

#### Not yet verified

- Item 2: a real pair and pull on the iPhone (needs the user's one-time Apple Devices/driver and libimobiledevice install, and one cable pairing).
- The real `.ips` field names and header layout: the parser was written from the known iOS crash format and checked only against a synthetic fixture, so the first real file may need a tweak.
