### Slice 223 — Usage counts: one pre-1.0 version rule, tested on both sides (Slice 219 review cleanups)

*In progress (wave 2026-11-01), draft PR.* Written 2026-10-30 by the round 1 feedback loop ([note](../notes/2026-10-30-2100-feedback-loop-w1.md)) and parked; the user moved it to `planned` on 2026-11-01. It comes from the Slice 219 review findings that were skipped as cleanups (listed in [Slice 219](219-usage-drops-dev-versions.md), *From review*). No user-visible change; the risk is the worker and the client drifting apart.

**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `src/ReadTheStupidText.Application/Usage/UsageEligibility.cs`, `tests/ReadTheStupidText.Tests/UsageEligibilityTests.cs`, `docs/plan/slices/223-usage-version-rule-cleanup.md`

#### Work

- [ ] One rule per side: keep only the `1.0` defaults in the exact lists and let the major-0 check cover `0.0.0`/`0.0.0.0` (219 high 4, medium 2).
- [ ] `isUnstampedVersion` validates its input itself instead of relying on `VERSION_PATTERN` having matched first (219 high 3); `IsPreRelease` takes `string?` and guards null (219 high 5).
- [ ] Shared test cases, run on both sides: leading zeros, overflow, leading junk, malformed (219 medium 4); a 0.x version on another event type through `hasValidEnvelope` (219 high 7).

#### Verification

- `node --test worker/*.test.mjs` (worker) and `dotnet test` (`UsageEligibilityTests`) with the same case table; both reject and accept the same versions.
