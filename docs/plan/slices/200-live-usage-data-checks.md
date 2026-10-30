### Slice 200 — Check the live usage data after the store rollouts

*In progress (wave 2026-10-30, wave 3), draft PR.* *Planned 2026-10-29.* Both apps, read-only against live data. No app code; tooling and docs only. Gaps left in Slices 133, 134, 137, 140, 166, 167, 178 and 179 (*Not yet verified*).

**Touches:** `tools/dino-usage-report.ps1`, `tools/rtst-usage-report.ps1`, `docs/plan/slices/166-usage-device-context.md` (the column notes), `docs/plan/slices/200-live-usage-data-checks.md`. Read-only use of `D:\secrets` (`CF_ANALYTICS_READ_TOKEN`).

#### Why

Counting has been on since 2026-10-27 (PR #487), but so far the rows came from test devices. Several slices left a "check the live rows after release" item that nobody owns: the new fields, the test-row drops, and the report scripts against real data.

#### Decisions

- Every query goes through the Analytics Engine SQL API with the read-only token (`analytics-query` agent), never a dashboard in Chrome. Test rows (unstamped versions, Android/iOS `x64`/`x86`, the known test countries and devices) are left out and counted separately.
- This slice changes nothing live. If a check finds test rows still arriving, it records the version and source here and a `fix/` branch follows.
- Done when each check below has a result line here with the query, or "no store build with that change yet" plus the version it waits for.

#### Work

- [ ] Find the newest store versions per app and platform (Play, App Store, Microsoft Store) and which of these slices they carry.
- [ ] Slice 166: rows from a store build carry country and device buckets (Dino `blob10-15`, RTST `blob18-20`, `double2-3`).
- [ ] Slices 167 and 179: no rows from version `1.0`/unstamped, none Android/iOS `x64`/`x86`, none from the Play pre-launch robot, since that build.
- [ ] Slice 178: `tools/dino-usage-report.ps1` runs against the live API; `own_book_added` blob9 shows `kind:bucket`; add the blob9 shape to the column notes in the Slice 166 file.
- [ ] Slices 133 and 134: `dino_usage` holds real rows for each event; `tools/dino-usage-report.ps1` output looks sane.
- [ ] Slices 137 and 140: `rtst_usage` holds real phone rows and Windows rows; `tools/rtst-usage-report.ps1` runs.
- [ ] Fix any report-script error found on real data (scripts only).

#### Verification

- Each check has its query and result written in this file.
- Both report scripts run end to end against the live API without errors.
