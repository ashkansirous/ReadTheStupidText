# Slice 243 — Crash reports: everything missing to explain a crash
**Product:** Shared

*In progress (wave 2026-11-14), draft PR. Items 3 and 5 built earlier; this wave builds 1, 2, 4, 6, 7 (8 waits on a real crash after 1-2 ship).* Planned 2026-11-01. From the user, after the 2026-11-01 iPhone test of Dino Reader: "the builds don't keep the files
that turn a crash into readable code lines - why?!", "we never created the key to read them - why?! I told you this is a
priority", "add slices for adding anything missing from the crash report".

**Depends on:** none (Slice 227 built the reports).
**Touches:** `.github/workflows/ios.yml`, `.github/workflows/dinoreader-ios.yml`, `.github/workflows/android.yml`,
`.github/workflows/dinoreader-android.yml`, `.github/workflows/release-shared.yml` (release-asset upload steps only),
`.github/CLAUDE.md`, `tools/crashes/**`, `tools/CLAUDE.md`,
`src/Shared/Sirous.Reading.Mobile.Shared/Crashes/**`, `src/Shared/Sirous.Reading.Application/Crashes/**`,
`src/Shared/Sirous.Reading.Mobile.Shared/Platforms/iOS/Crashes/**`, `src/Shared/Sirous.Reading.Mobile.Shared/Platforms/Android/Crashes/**`,
`src/Dino/DinoReader.App/DinoLog.cs`, `src/Dino/DinoReader.App/App.xaml.cs`,
`src/Rtst/ReadTheStupidText.Mobile/App.xaml.cs`, `worker/crashes.ts`, `worker/crashes.test.mjs`, `worker/CLAUDE.md`,
`tests/Shared/Shared.Tests/**`, `tests/Rtst/ReadTheStupidText.Tests/**`, `docs/plan/slices/227-crash-reports.md`
(paths updated 2026-11-14 for the Slice 371 folder move; `release.yml` no longer exists)

## What the 2026-11-01 crashes showed was missing (checked that day)

- **No iOS build keeps its debug symbols (dSYM).** `git grep -i dsym origin/main -- .github src` finds nothing. Apple's
  5 TestFlight crash logs for Dino (1.73.1 on 2026-10-28; 1.83.0 and 1.88.0 on 2026-11-01, iPhone 8 on iOS 16.7) show
  about 7 frames of our code with no names, so the crash cannot be tied to a method. Slice 227 item 9 assumed a dSYM
  for 1.73.1 existed; none was kept.
- **Our own reports could not be read by the tool.** `tools/crashes/pull-crashes.mjs` needs `CF_R2_CRASHES_READ_*`,
  which was never created (Slice 227 open item). Reading through wrangler's own login works: on 2026-11-01 the
  Cloudflare REST list `GET /accounts/<id>/r2/buckets/readthestupidtext-downloads/objects?prefix=crashes/dino/` with
  `wrangler auth token`, then `wrangler r2 object get <bucket>/<key> --remote`, returned 3 Dino iOS reports.
- **The rolling log has no navigation, Back, lifecycle or memory lines.** The two `unclean_exit` reports from the
  iPhone 8 end in speech lines (`chunk N ready`, `silent voice output rescued`), so whether Back was pressed, which
  page was showing, whether the app went to the background, or whether iOS warned about memory cannot be read.
- **An `unclean_exit` report has an empty stack** and is matched to Apple's crash log only by time by hand.
- **The `native` (MetricKit) report keeps binary UUIDs and offsets only** (the 1.85.3 store report,
  `0xdead10cc`), so it too needs the dSYM of that build to be read.

## Work

- [ ] 1. **Keep the dSYM of every iOS build, both apps.** The iOS release job zips the `.dSYM` bundles of the app and
  of `SherpaOnnxC.framework` and attaches them to that product's GitHub Release (`rtst-v…`/`dino-v…`; Release
  assets, never Actions artifacts: the storage quota rule in `.github/CLAUDE.md`). Name them with the build version
  and the binary UUID. Check the MAUI/.NET iOS properties that produce the dSYM in Release through `library-docs`
  first.
- [ ] 2. **`tools/crashes/symbolicate`** (runs on this Windows PC, no Mac): given an Apple crash log, a TestFlight
  crash submission or a MetricKit `native` report, download the matching dSYM from the release by binary UUID and
  resolve each frame (llvm-symbolizer or an equivalent that reads DWARF on Windows; checked through `library-docs`).
  Prints method, file and line. A unit test with a fixture.
- [x] 3. **`pull-crashes.mjs` reads through wrangler's login** (the route proven on 2026-11-01), with no new token.
  Drop the `CF_R2_CRASHES_READ` requirement from the script, `worker/CLAUDE.md` and Slice 227. The script only calls list + get; the login itself is the user's general wrangler login, not a read-only credential.
- [ ] 4. **One command answers "did it crash?"**: `tools/crashes/report.(ps1|mjs) -App dino|rtst -Since <date>` runs
  the R2 pull, `pull-apple.py` and `pull-play.py`, pairs each `unclean_exit` with the Apple/Play crash closest in time
  for the same version, symbolicates, and prints one grouped summary.
- [x] 5. (Built in Slice 249 item 1, PR #781: `Mobile.Shared/Crashes/TraceLines` + `AppTrace`; page shown/left/popped, Back (both platforms log the pop, `page popped`; no system Back hook, it would switch off predictive back), reading events with a fixed reason, app foreground/background, memory warning, synthesis queue length. Not yet seen in a device log: see Slice 249.) **Rolling-log lines (Android and iOS, both apps), no user text:** page shown/left (page type name), Back
  pressed (toolbar and system/gesture back), reading started/paused/stopped/detached and why, app to background and
  foreground, memory warning (iOS `DidReceiveMemoryWarning`, Android `OnTrimMemory` level), synthesis queue length
  when it changes by a step. Same names on both platforms.
- [ ] 6. **Report fields:** the binary UUID of the main executable and the build number in every report (so item 2 can
  match), available memory bucket and the app's own memory use at the time of the report, foreground/background
  state. Worker validation (`worker/crashes.ts`) accepts the new fields; tests.
- [ ] 7. **Android parity:** Android reports already carry managed exception names; add the same lines and fields
  (items 5-6) and keep the native `.so` symbols for sherpa/onnxruntime crashes (release asset, as item 1).
- [ ] 8. Close Slice 227 item 9 here: once items 1-2 work, symbolicate the next new crash; builds before this slice
  have no dSYM, so their crashes are explained by Slice 244 instead.

## Verification

- Unit tests: symbolicate fixture, report pairing, Worker field validation, log-line names.
- A Release iOS build in CI attaches the dSYM zip; `symbolicate` resolves a frame from a test crash
  (`crash-test`, Slice 227) of that build to a C# method.
- `pull-crashes.mjs` lists and downloads the 2026-11-01 reports with no token in `D:\secrets`.
- Android emulator and iPhone: the rolling log of a test crash shows the page, Back and lifecycle lines.

## From review

- Skipped #9 (`reviews/s243-medium.md`): "Facts flattened into five parallel nullable wire fields kept in step in Wire, Serialize, FactsOf and the Worker's parseFacts. Scenario: a sixth fact added in one place is silently dropped; nest CrashFacts on the wire." - a wire-shape refactor with no failure shown today.
- Skipped #9 (`reviews/s243-high.md`): "`inspectElf` indexes `sections[nameTable]` unchecked. ELF with more than 0xFF00 sections (e_shnum 0, e_shstrndx = SHN_XINDEX) silently gets a hash name; a corrupt header with `nameTable >= count` throws a TypeError surfacing as `::error::Cannot read properties of undefined`." - speculative: the input is only the two known package .so files; no failure path shown.
- Third round (`reviews/s243-check.md` #1-#3, #5): which build a report belongs to is no longer guessed from a launch history (`LaunchHistory`, `launches.txt` and its time rule are gone). A managed crash and a user report read the running launch's version, build and UUID from memory (no wait, no file read in the crash hook); an unclean exit, and an Android native crash, take them from the crashed launch's own run marker (`running.marker`: version, build, UUID, written when the app comes to the front with reporting on, rewritten when the UUID is known); an iOS MetricKit diagnostic takes version (`MXDiagnostic.applicationVersion`), build (`MXMetaData.applicationBuildVersion`) and the app binary's UUID from its own call-stack tree. A missing field is null, never another launch's value. `DiscardAll` deletes the marker.
- Third round #6 (`reviews/s243-check.md`): the Worker's Intel-device filter never looks at the version, so a report with no version still goes through it; a no-version report from a real device is kept and counted as `unknown`.
- Third round #7 (`reviews/s243-check.md`): `collect-symbols.mjs` refuses an empty, missing or flag-like value instead of shifting the following flags.
- Skipped #8 (`reviews/s243-check.md`): "AppTrace.cs:77, low: foreground flag only set by Activated/Resumed/Stopped; null on background cold start, Deactivated ignored." - a background cold start reports no foreground state; unknown is correct.

## Waiting on the user

- Parity difference for the user to decide (`reviews/s243-check.md` #4): Android has no per-process memory headroom API; `memoryHeadroom` on Android is system free RAM minus the low-memory threshold, on iOS os_proc_available_memory.
