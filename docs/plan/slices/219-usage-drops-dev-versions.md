### Slice 219 — Usage counts drop dev builds below version 1.0

*Planned 2026-10-30.* From a read-only check of `rtst_usage` (2026-10-30).

**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `src/ReadTheStupidText.Application/Usage/UsageEligibility.cs` (if the client rule should match), `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/219-usage-drops-dev-versions.md`

#### Why

- The worker drops only the exact unstamped versions `1.0`, `1.0.0`, `1.0.0.0`, `0.0.0`, `0.0.0.0` (`worker/events.ts:197`). A dev build with any other version below 1.0 (2 rows from Windows `0.1.1` on 2026-10-27, before Slice 167) would still be counted today.
- History checked: the 1.0 and Android x64 rows all arrived before the Slice 167 and 179 drops went live (two `1.0` rows 8 minutes after Slice 167's commit, likely deploy lag). No live leak.
- Can't be fixed, only reported: Android builds older than the device object (before 2026-10-27, about 1.57-1.66) send no arch, so an emulator on an old build looks like a phone. Reports already keep those rows; they must say so.

#### Work

- [ ] Worker: drop any version whose major part is 0 (and keep the exact list), both the Dino and RTST paths (`events.ts:276`, `:461`); tests in `events.test.mjs`.
- [ ] Client: `UsageEligibility` treats a version below 1.0 as unstamped too (unit test), Android, iOS and Windows alike.
- [ ] `query-analytics` caveat line for old Android rows with no arch (in the reports the agent writes, not code).

#### Verification

- Fast: `npm test` for the worker tests; `dotnet test` for `UsageEligibility`.
- After deploy: a read-only query shows no rows with a 0.x version.
