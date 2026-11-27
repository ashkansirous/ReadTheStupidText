### Slice 430 — Dino: a Ninjalog dashboard that shows whether people see and act on the forced update, by platform
**Product:** Dino

*Built 2026-11-22, merged as PR #1336 (a8b1121); dashboard uploaded the same day.*

**Touches:** `docs/analytics/ninjalog-forced-update.template.json` (new), `docs/analytics/ninjalog-dashboards/forced-update.json` (new, generated), `tools/dino/ninjalog-dashboards.mjs`, `tools/dino/ninjalog-dashboards.test.mjs`, `docs/analytics/ninjalog-queries.md`, `.gitattributes`, `.github/workflows/public-name.yml`

## Why

Owner, 2026-11-22: "add a dashboard that shows if anyones sees the update to the new verson, because I increase the min
version, group it by platform". Live config read the same day: `minVersion` android 1.131.1, ios 1.136.0, windows
1.136.3.

## Decisions

- The signal is the app's own: below `minVersion` it shows `UpdatePage` (Slice 132), counted as `screen=update`
  (`UiUsage.cs`, `UiScreens.Update`); its button is `tap` with detail `update:update`. No version comparison in the query,
  so the dashboard stays right when `minVersion` moves.
- Builds older than the screen counts (1.118.0, 1.123.3 seen 2026-11-22) carry `screen=none` (no screen counts); they show only as old
  versions in the per-platform launch charts.
- No `defaultRange`, so the "last 24 hours" table keeps its own 1-day range.

## Work items

- [x] 1. Template `docs/analytics/ninjalog-forced-update.template.json`, variant `forced-update` in
      `tools/dino/ninjalog-dashboards.mjs` (each variant names its template), test, LF and CI path rules widened to
      `ninjalog-*.template.json`.
- [x] 2. Uploaded with `--upload`: all 5 dashboards' queries ran, `forced-update` saved with 7 widgets.

- [x] 3. Review pass 1 (high, `D:/ReadTheStupidText-worktrees/reviews/s430-high.md`): 2, 3 and 5 fixed; 1 left (live
      `telemetry.sampleRate` is 1, read 2026-11-22, so counts are exact; weight with `sampleInterval` if it drops); 4
      left (the assertion fails if the template gains a `defaultRange`); 6 left (Play testing-track builds show as their
      own version, which is what the version charts are for; see `ninjalog-queries.md` "Play testers").
- [x] 4. Review pass 2 (medium, `reviews/s430-medium.md`): 1 fixed (presses count only `detail="update:update"`), 5
      done (re-uploaded after the fixes); 2 is pass 1's item 1; 3 and 4 nits left.

## Not yet verified

- [session] Once someone below `minVersion` opens the app, the "Saw the please update page" chart shows them (on
  2026-11-22 no `screen=update` event existed yet in 30 days).
