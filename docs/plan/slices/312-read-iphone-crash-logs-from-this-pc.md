# Slice 312 — Read the iPhone's crash logs from this PC

*In progress (wave 2026-11-07), draft PR.* Planned 2026-11-07. Untried: reading the iPhone 8's crash logs from this PC (libimobiledevice `idevicecrashreport`, or an equivalent) so iOS crashes can be checked without the user. Moved from plan.md's *Outstanding work*. Related: [Slice 243](243-crash-report-gaps.md) (dSYMs, symbolication, "did it crash?" command), [Slice 244](244-dino-ios-crashes-2026-11-01.md).

Rule check: the real phone is wireless-debugging only for Android. For an iPhone the transport is Apple's, so the first step is to find out whether a wireless route exists (libimobiledevice supports network pairing after a first USB pairing; Apple's tools use Wi-Fi sync). The user's rule about USB is for the Android phone; if a first iPhone pairing needs a cable, that is one plug-in, said plainly before it happens.

**Touches:** `tools/crashes/pull-iphone.ps1` (new), `tools/crashes/pull-iphone.test.ps1` (new), `tools/crashes/pull-crashes.mjs`, `tools/phone/README.md`, `tools/CLAUDE.md`, `docs/plan/slices/312-read-iphone-crash-logs-from-this-pc.md`

**Night decision (wave 2026-11-07, coordinator):** this night has no iPhone (no phone pre-flight) and the run installs no software on the PC (no driver, no libimobiledevice download: that is the user's machine and a one-time step for them). So the night builds items 1, 3 and 4: the docs lookup, the script (it finds `idevicecrashreport` on PATH or under a `-ToolPath`, and when it is missing says exactly what to install and stops with a clear message), its fixture test, and the README. Item 2 (the real pair and pull) and the real-pull check go to *Not yet verified*.

## Work

- [ ] 1. Context7 / `library-docs` for libimobiledevice (`idevicecrashreport`, `idevicepair`, Windows builds and the Apple Mobile Device Support driver it needs); check whether iTunes or Apple Devices is installed here. Write down what works on Windows 11 and what needs a one-time step from the user.
- [ ] 2. Pair and pull: run `idevicecrashreport` against the iPhone (network if the pairing allows, else the one first-time cable pairing), keep the `.ips` files under the worktree's `tmp`, and list them by app and time.
- [ ] 3. A script `pull-iphone.ps1` that pulls the reports, filters to Dino and RTST by bundle id, and prints the newest crash per app with its exception type and the faulting thread's top frames; symbolicate with the dSYMs from Slice 243 when they exist (say so when they do not).
- [ ] 4. Document in `tools/phone/README.md`: how to run it, what the phone must be (unlocked, trusted, same network).

## Verification

- A real pull from the iPhone returns the crash files from 2026-11-01 (Slice 244) or says there are none; the script's test runs on a fixture `.ips`.
