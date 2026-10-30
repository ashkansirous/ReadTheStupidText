### Slice 219 — Usage counts drop dev builds below version 1.0

*Built 2026-10-30, not yet verified after deploy (see Not yet verified).* Planned 2026-10-30. From a read-only check of `rtst_usage` (2026-10-30).

**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `src/ReadTheStupidText.Application/Usage/UsageEligibility.cs` (if the client rule should match), `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/219-usage-drops-dev-versions.md`

#### Why

- The worker drops only the exact unstamped versions `1.0`, `1.0.0`, `1.0.0.0`, `0.0.0`, `0.0.0.0` (`worker/events.ts:197`). A dev build with any other version below 1.0 (2 rows from Windows `0.1.1` on 2026-10-27, before Slice 167) would still be counted today.
- History checked: the 1.0 and Android x64 rows all arrived before the Slice 167 and 179 drops went live (two `1.0` rows 8 minutes after Slice 167's commit, likely deploy lag). No live leak.
- Can't be fixed, only reported: Android builds older than the device object (before 2026-10-27, about 1.57-1.66) send no arch, so an emulator on an old build looks like a phone. Reports already keep those rows; they must say so.

#### Work

- [x] Worker: drop any version whose major part is 0 (and keep the exact list), both the Dino and RTST paths (`events.ts:276`, `:461`); tests in `events.test.mjs`.
- [x] Client: `UsageEligibility` treats a version below 1.0 as unstamped too (unit test), Android, iOS and Windows alike.
- [x] Own-machine Windows rows (from Slice 200's live check, left for later there): RTST Windows `1.67.1` rows from our own runs are still counted, because Windows is not arch-dropped. Find what marks our own machine (the `no-usage` switch not set by every Windows harness, or a dev install), close that hole, and say in the report which stored rows are ours. Verify: a read-only query after a local Windows run shows no new row.
  - Finding (review, 2026-10-30): no code hole found. `WindowsUsage.Facts` counts only Store-signed, non-dev installs since Slice 179. The stored Windows `1.67.1` rows predate that gate and are likely ours, but this is not verified. Nothing to close in code; left open for a read-only query after deploy.
- [ ] `query-analytics` caveat line for old Android rows with no arch (in the reports the agent writes, not code). The skill lives in `D:\ClaudeSkills`, outside this repo, so it is handed to the coordinator and not done in this PR.

#### Verification

- Fast: `npm test` for the worker tests; `dotnet test` for `UsageEligibility`.
- After deploy: a read-only query shows no rows with a 0.x version.

*Changed while building:* the worker's `isUnstampedVersion` (`worker/events.ts`) drops any version whose major parses to 0, on both the Dino and RTST envelopes; the exact list is kept. The client's `UsageEligibility.IsPreRelease` uses the same rule (major parsed as a number == 0 gives `UnstampedBuild`), shared by Android, iOS and Windows. Known behaviour: a dev build with a non-zero major (e.g. `2.0`) still counts. Work item 3 found no code hole (finding above). Work item 4 (the `query-analytics` caveat) is handed to ClaudeSkills and is not in this PR.

*Tests:* worker `events.test.mjs` 95 pass (leading-zero, huge and invalid version cases); `UsageEligibilityTests` 1689 pass. No device run: pure rule plus worker.

*From review:* high pass: findings 2 and 6 applied; 3, 4, 5 and 7 skipped as cleanups. Medium pass: finding 7 applied; finding 1 dismissed because all `0.x` tags predate `v1.0.0` (2026-09-02), before usage counting existed.

#### For CLAUDE.md (lifted 2026-10-30)

In `worker/CLAUDE.md`: versions with major 0 are dropped as unstamped, on the worker (`worker/events.ts` `isUnstampedVersion`) and on the client (`UsageEligibility.cs`); keep the two rules equal. (Slice 219: docs/plan/slices/219-usage-drops-dev-versions.md)

#### Not yet verified

- After deploy, a read-only `rtst_usage` / `dino_usage` query shows no new `0.x` rows.
- A local Windows run adds no new row.
