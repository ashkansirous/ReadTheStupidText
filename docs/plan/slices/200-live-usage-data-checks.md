### Slice 200 — Check the live usage data after the store rollouts

*Built (wave 2026-10-30), PR #587.* *Planned 2026-10-29.* Both apps, read-only against live data. No app code; tooling and docs only. Gaps left in Slices 133, 134, 137, 140, 166, 167, 178 and 179 (*Not yet verified*).

**Touches:** `tools/dino-usage-report.ps1`, `tools/rtst-usage-report.ps1`, `docs/plan/slices/166-usage-device-context.md` (the column notes), `docs/plan/slices/200-live-usage-data-checks.md`. Read-only use of `D:\secrets` (`CF_ANALYTICS_READ_TOKEN`).

#### Why

Counting has been on since 2026-10-27 (PR #487), but so far the rows came from test devices. Several slices left a "check the live rows after release" item that nobody owns: the new fields, the test-row drops, and the report scripts against real data.

#### Decisions

- Every query goes through the Analytics Engine SQL API with the read-only token (`analytics-query` agent), never a dashboard in Chrome. Test rows (unstamped versions, Android/iOS `x64`/`x86`, the known test countries and devices) are left out and counted separately.
- This slice changes nothing live. If a check finds test rows still arriving, it records the version and source here and a `fix/` branch follows.
- Done when each check below has a result line here with the query, or "no store build with that change yet" plus the version it waits for.

#### Work

- [ ] Find the newest store versions per app and platform (Play, App Store, Microsoft Store) and which of these slices they carry.
- [x] Slice 166: rows from a store build carry country and device buckets (Dino `blob10-15`, RTST `blob18-20`, `double2-3`).
- [x] Slices 167 and 179: no rows from version `1.0`/unstamped, none Android/iOS `x64`/`x86`, none from the Play pre-launch robot, since that build.
- [ ] (no new-format rows yet) Slice 178: `tools/dino-usage-report.ps1` runs against the live API; `own_book_added` blob9 shows `kind:bucket`; add the blob9 shape to the column notes in the Slice 166 file.
- [x] Slices 133 and 134: `dino_usage` holds real rows for each event; `tools/dino-usage-report.ps1` output looks sane.
- [x] Slices 137 and 140: `rtst_usage` holds real phone rows and Windows rows; `tools/rtst-usage-report.ps1` runs.
- [x] Fix any report-script error found on real data (scripts only).

#### Verification

- Each check has its query and result written in this file.
- Both report scripts run end to end against the live API without errors.

#### Live results (2026-10-30, 90-day window, read-only SQL API, counts only)

Datasets hold rows from 2026-10-27 to 2026-10-29 only (counting started 2026-10-27). Every query used `SUM(_sample_interval)` and a `blob` filter; the report scripts hide groups under 10 events, so these checks used raw group-bys with no floor.

- **Report scripts:** both `tools/dino-usage-report.ps1` and `tools/rtst-usage-report.ps1` ran end to end against the live API with exit 0 and no query error (Dino 25 tables, 8 empty; RTST 20 tables, 5 empty; empty = under the 10-event floor). Fixed: the x64 filter date `$WorkerDropsIntelPhonesFrom` was `2026-10-28`, but x64/x86 phone-OS rows kept arriving until 2026-10-28 23:47 UTC (Dino 35, RTST 51 rows), so it is now `2026-10-29`; the filter also covers iOS (simulators), not only Android.
- **Slices 167 and 179 (test rows):** pass for unstamped versions (last `1.0` row 2026-10-27 20:59 UTC, Dino 77 rows, RTST 111; RTST `0.1.1` Windows 2 rows 2026-10-27) and for Android/iOS `x64`/`x86` (none after 2026-10-28 23:47 UTC; the gate shipped with Dino 1.75.0 / RTST 1.71.0 on 2026-10-29). Play pre-launch robot: cannot be told apart by any column; no sign of it (no x86/x64 and no new unstamped rows since the gate). Not proven: Windows rows from the user's own machine (Windows is not arch-dropped; RTST Windows `1.67.1` 33 reads on 2026-10-28 are likely the user's, inference only).
- **Slice 166 (country and device buckets):** pass for Dino 1.71.0 and later and RTST 1.67.0 and later (every row from those builds has blob10-15 / blob18-20 and double2-3 filled; older builds read empty as designed). Windows rows carry them too (`desktop`). Shapes seen: Dino cores `1-4|5-8`, RAM `4-6|8+`, OS major as digits; RTST cores `1|5|9`, RAM `3|4|6|8`.
- **Slice 178:** not yet. All 9 `own_book_added` rows are blob9 `typed` (old format, builds 1.68.1 to 1.71.2); no row from a build carrying the `kind:bucket` detail yet. Report runs (table empty under the floor).
- **Slices 133 and 134 (Dino events):** pass. Every event kind has real rows: `app_opened`, `book_opened`, `book_finished`, `store_download`, `own_book_added`, `recording_made`, `voice_changed` (narrator 73, flavour 28, character_voice 21), `reading_language_changed`, `wait_shown`, `wait_cancelled`, `intro_started/step/ended`, `story_language_added/switched/set` (story events blob9 `2:settings`, `3:intro`, else empty). Not yet: `voice pack download` events (the report table is empty).
- **Slices 137 and 140 (RTST):** 137 pass for phones (Android and iOS rows for `app_opened`, `read_started`, `timing`, `translate_offered/chosen`, `voice_changed`, `wait_shown`). 140 pass for Windows (rows for `app_opened`, `read_started` with source `selection` 29 / `panel` 5, `timing`, `wait_shown`, `voice_changed`, `translate_chosen`); no `clipboard` or `hotkey` source yet.
- **Latest store versions (releases, not store consoles):** `rtst-v1.73.2`, `dino-v1.78.2`; stores' own live versions were not read here.

Left for later: filter or flag the user's own-machine Windows rows (RTST Windows `1.67.1`; Windows is not arch-dropped, so the counts still include our own runs); re-run after more real traffic (under 10 events per group everywhere), and the voice pack download and Windows `clipboard`/`hotkey` source rows.
