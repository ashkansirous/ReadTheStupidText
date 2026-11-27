# Slice 462 — Dino: drop the test and review traffic that still reaches dino_usage and Ninjalog
**Product:** Dino

*Planned 2026-11-27 from the 2026-11-26 usage analysis (Ninjalog `ashkan-sirous/dino-book-reader`, index `dino_usage`, 19-26 Nov).*

**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `worker/ninjalog.ts`, `worker/ninjalog.test.mjs`, `src/Shared/Sirous.Reading.Application/Usage/UsageEligibility.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/Usage/UsageGate.cs`, `tests/Shared/Shared.Tests/**`, `worker/CLAUDE.md`, `docs/analytics/ninjalog-queries.md`, `site/dino/privacy.html`

**Why:**
- About 20 of 53 first launches in 19-26 Nov were test or review traffic. They had US region, non-production builds, 1-second sessions, and Play pre-launch-style crawlers with 11-12 cancelled downloads. They make the raw funnel look far worse than it is: 21 of 27 real new users finish the intro on 1.136.0.
- What already filters, from the code (origin/main, 2026-11-27):
  - The client gate (`UsageGate`, `UsageEligibility`) drops debug builds, unstamped builds, emulators, Firebase Test Lab, test installs, non-Store Windows MSIX builds and TestFlight/App Review (sent to `dino_usage_beta`).
  - The Worker drops unstamped or pre-1.0 versions, and x64/x86 Android and iOS (`events.ts:172, 217-219, 352-363`). Windows is not covered by that arch rule.
  - Ninjalog gets only rows already written to `DINO_USAGE` (`ninjalog.ts:10`), so the same filters apply.
- So the leak is traffic these rules miss. Which rule misses it isn't known yet.

**Decisions (owner, 2026-11-27: "4- yes", to my recommendation to drop, not tag):**
- Leaking test traffic is dropped, not tagged.
- No install id, device id or new device field is added (Decision 140; the privacy page's "no install identifier"). Ninjalog is a third party, so nothing new goes there.
- If a case can't be told apart without a new field, it goes to `dino_usage_beta` (Slice 251's pattern), or stays, and is written down here.

**Open for the owner:** none.

**Work:**
- [ ] 1. Find what leaks. Use Ninjalog and `analytics-query` over 19-26 Nov for first-launch sessions under 2 s, sessions with 10 or more cancelled downloads, US non-production version strings, and Windows rows. Group them by platform, version, device class, arch, OS major and form factor. Write down which existing rule each group slips past.
- [ ] 2. Close each gap at the cheapest correct place. That's the client gate when the device can tell (for example Play pre-launch's test-lab flag or robot UI automation). It's the Worker when the row shows it (for example an unreleased version string, or the arch rule extended to Windows if the leak is there). Each rule gets a test.
- [ ] 3. Write the rules into `worker/CLAUDE.md` and `docs/analytics/ninjalog-queries.md`. Update `site/dino/privacy.html` only if what is kept changes.

**Verification:**
- Worker and Shared tests are green.
- Ninjalog, after release, for a 7-day window: `index=dino_usage event=first_launch | stats dc(session) by region, version`. Test-looking sessions (under 2 s, 10 or more cancelled downloads, non-production versions) should be under 5% of first launches, against about 38% (20 of 53) in 19-26 Nov.
