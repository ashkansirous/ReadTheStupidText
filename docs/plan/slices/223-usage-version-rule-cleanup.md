### Slice 223 — Usage counts: one pre-1.0 version rule, tested on both sides (Slice 219 review cleanups)

*Built 2026-11-01 (wave 7, night 2026-11-01-0859), merged as PR #665 (5391db6). No device check needed (no UI). No deploy in this slice: the worker change ships with the next Worker deploy (Workers Builds deploys `main` on merge, watch paths `worker/*`; `worker/CLAUDE.md`).* Written 2026-10-30 by the round 1 feedback loop ([note](../notes/2026-10-30-2100-feedback-loop-w1.md)) and parked; the user moved it to `planned` on 2026-11-01. It comes from the Slice 219 review findings that were skipped as cleanups (listed in [Slice 219](219-usage-drops-dev-versions.md), *From review*). No user-visible change; the risk is the worker and the client drifting apart.

**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `src/ReadTheStupidText.Application/Usage/UsageEligibility.cs`, `tests/ReadTheStupidText.Tests/UsageEligibilityTests.cs`, `docs/plan/slices/223-usage-version-rule-cleanup.md`

#### Work

- [x] One rule per side: keep only the `1.0` defaults in the exact lists and let the major-0 check cover `0.0.0`/`0.0.0.0` (219 high 4, medium 2).
- [x] `isUnstampedVersion` validates its input itself instead of relying on `VERSION_PATTERN` having matched first (219 high 3); `IsPreRelease` takes `string?` and guards null (219 high 5).
- [x] Shared test cases, run on both sides: leading zeros, overflow, leading junk, malformed (219 medium 4); a 0.x version on another event type through `hasValidEnvelope` (219 high 7).

#### Verification

- `node --test worker/*.test.mjs` (worker) and `dotnet test` (`UsageEligibilityTests`) with the same case table; both reject and accept the same versions.
- Run 2026-11-01: `node --test worker` 115/115; `UsageEligibilityTests` 53/53.

*Changed while building:*

- Exact lists now hold only `1.0`/`1.0.0`/`1.0.0.0`; the major-0 check covers `0.0.0`(`.0`) on both sides.
- Worker `isUnstampedVersion(unknown)` checks `VERSION_PATTERN` itself (malformed or non-string = unstamped).
- Client `IsMalformedOrPreRelease`: ASCII-only regex `^[0-9]{1,6}(\.[0-9]{1,6}){1,3}\z` (no Unicode digits, no trailing newline), no trimming; null/empty dropped.
- Behaviour change: the client now drops overflow (`1234567.1`), malformed (`1.x`, `100.x`), whitespace-padded (`" 2.0 "`) and suffixed versions, which the worker already dropped, so nothing counted on the server changes. CI stamps plain x.y.z (android/ios workflows).
- Shared case table (unstamped incl. `" 2.0 "`, `"2.0\n"`, Arabic-Indic and fullwidth digits; stamped) in both `events.test.mjs` and `UsageEligibilityTests`; a C# null test.

*From review:* (`reviews/s223-high.md`, `reviews/s223-medium.md`, outside the repo)

- High: applied 1, 2, 8. Skipped 3-7: cleanup/perf, no failure path.
- Medium: first pass was incomplete (REVIEW INCOMPLETE); rerun read. All 6 skipped: cleanups; the suffix drop is already the worker's rule.

#### Not yet verified

- No UI; the rule itself is covered by the shared case tables on both sides.
- Live (added by the round's feedback loop, 2026-11-01): `worker/events.ts` changed and Workers Builds deployed it, so a read-only `rtst_usage` / `dino_usage` query (through the `analytics-query` agent) should show event counts per real version unchanged in the days after the 2026-11-01 deploy, with no real version newly counted as unstamped.
