### Slice 251 — Dino usage from TestFlight goes to its own beta dataset, never to the production counts

*Built 2026-11-04 (night 2026-11-03-2349 wave 1), merged as PR #777 (ced3a02a). Unit and Worker tests only; no device run (the beta path needs a real TestFlight install), see Not yet verified.* *Planned 2026-11-02. After [Slice 250](250-dino-playback-events-and-session-id.md).*

**Claim note (coordinator, night 2026-11-03-2349 wave 1):** built before Slice 250: neither needs the other's behaviour (250 adds events and a session id, 251 a channel), and 250 is claimed after this merges so the shared `UsageBatch.cs` and `worker/events.ts` never change in two branches at once. Premise check: `UsageGate`, `UsageQueueSender` and `UsageFacts` are shared with RTST mobile (`RtstUsage.cs`) and `CrashGate.cs` reads `UsageGate.Facts`. **Design:** the gate returns a closed `UsageChannel` (None, Production, Beta) built on the existing `InstallChannels.FromIosReceipt` (`Application/Crashes/InstallChannel.cs`); only Dino maps Beta to sending (its sender stamps `channel: beta`), RTST maps Beta to no send exactly as today, and crash reports keep today's yes/no (CrashGate unchanged in behaviour; a test proves RTST and crash results are equal to main's for every input). Android item 2: the code has no way to tell a Play testing track from production (`InstallChannels.FromAndroidInstaller` sees the same Play installer for every track), so item 2 writes down what Android testing installs send today and ships no guess. Both wrangler configs (`wrangler.jsonc`, `wrangler.workers-dev.jsonc`) get the binding (`worker/CLAUDE.md`: bindings live in both).

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Usage/UsageGate.cs`, `src/ReadTheStupidText.Mobile.Shared/Usage/UsageQueueSender.cs`, `src/ReadTheStupidText.Mobile.Shared/Usage/RtstUsage.cs`, `src/ReadTheStupidText.Mobile.Shared/Crashes/CrashGate.cs`, `src/ReadTheStupidText.Application/Usage/UsageBatch.cs`, `src/ReadTheStupidText.Application/Usage/UsageEligibility.cs`, `src/ReadTheStupidText.Application/Usage/UsageChannel.cs` (new), `src/ReadTheStupidText.Application/Crashes/InstallChannel.cs`, `src/DinoReader.App/UsageCounter.cs`, `src/DinoReader.Core/UsageEvent.cs`, `tests/DinoReader.Tests/**`, `worker/events.ts`, `worker/*.test.mjs`, `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `worker/CLAUDE.md`, `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html`, `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/251-dino-beta-usage-dataset.md`

**Why.** The user tested Dino iOS 1.94.0 from TestFlight on 2026-11-02 and the usage data held nothing of it: TestFlight and App Review installs send no usage (`UsageGate.cs:121-126`, Slice 179). The user, 2026-11-02: "any data that is in my analytics database is mine and I can look at it". Production counts must still hold only real store users (`~/.claude/CLAUDE.md`, *Analytics count only real production users*), so beta rows go to a separate dataset instead of being dropped.

#### Work

- [x] **1. The beta channel (iOS).** `UsageGate` returns a channel instead of yes/no: `production` (App Store receipt), `beta` (TestFlight `sandboxReceipt`), or none (Debug, unstamped version, simulator, App Review if it can be told apart from TestFlight, else it counts as beta). Every other block (Debug, emulators, robots, Test Lab, the harnesses' `READTHESTUPIDTEXT_NO_USAGE=1`) stays a block.
- [x] **2. Android.** (Recorded as a finding below; no code.) Find out whether an install from a Play testing track (internal, closed) can be told from production on the device; if it can, it is `beta` too. If it cannot, write down what Android testing installs do today (whether they reach `dino_usage` now) and stop there; no guess is shipped. (Not checked in planning.)
- [x] **3. Wire and Worker.** The batch carries the channel (`UsageBatch.cs`, optional, absent means production for old builds). `wrangler.jsonc` gets a `DINO_USAGE_BETA` binding (dataset `dino_usage_beta`); `worker/events.ts` writes beta batches there with the same rules and columns, production ones to `dino_usage` as today.
- [x] **4. Text.** `PRIVACY.md` and both `site/` pages: test builds send the same usage counts to a separate beta store. `worker/CLAUDE.md`: the beta dataset rule (never mixed into production reports; the `analytics-query` agent names the dataset it reads).
- [x] **5. Check.** Unit tests of the channel choice for each receipt name and block. Worker tests: a beta batch lands only in the beta binding, a production one only in production. (Unit and Worker tests done.) On the next TestFlight build: the user plays a book and `dino_usage_beta` shows the rows (Not yet verified until then).

**Item 2 finding (built 2026-11-04, from the code, not tried on a device).** The device cannot tell a Play testing track from production: `InstallChannels.FromAndroidInstaller` sees `com.android.vending` for every track, and no other fact the gate reads (`UsageGate.ReadFacts`) differs. So an Android internal or closed-track install is today an ordinary store install: it passes the gate and its counts reach `dino_usage` like production's (unless it is an emulator, robot or harness build, which stay blocked). No guess is shipped: Android is `Production` or `None` only, never `Beta`.

**Changed while building:**

- `UsageChannel` (None, Production, Beta) lives in Application; `UsageEligibility.ChannelOf` and `UsageGate.Channel` build on the unchanged `Check`/`CheckBuild`. `UsageChannels.PathFor` picks the route once. Only Dino sets `SendsBeta` (its `UsageCounter`); RTST leaves it false.
- `PRIVACY.md` and `site/privacy.html` are Read The Stupid Text's policy and RTST sends no beta, so they are unchanged; only `site/dino/privacy.html` got the text.
- Touches was widened with `src/DinoReader.Core/UsageEvent.cs` (Dino's own `UsageBatch`; later reverted to its original, so it is unchanged in the diff) and `tests/DinoReader.Tests/**`.
- No `channel` field on the wire (changed after review 1.2): a TestFlight batch posts to `POST /api/events/beta` (and `/dino/api/events/beta` on workers.dev), which the Worker writes only to `DINO_USAGE_BETA` (dataset `dino_usage_beta`, declared in both wrangler configs, route in `run_worker_first`). The production route `/api/events` writes only `DINO_USAGE` and refuses (400) a body carrying `channel: "beta"`. An old or rolled-back Worker has no beta route (404, the app drops the batch: fail closed), so beta rows can never reach `dino_usage`. RTST and crash gating are proven equal to main's for every input (`UsageChannelTests`). `dino_usage_beta` also holds Apple App Review traffic (same `sandboxReceipt`), which the Dino privacy page says.

**From review** (files in `D:/ReadTheStupidText-worktrees/reviews/`, not in the repo):

- `s251-high.md`: applied 1 (privacy text names App Review), 2 (by its own route: the separate beta path, instead of a marker), 3 (`DINO_USAGE_BETA` target checked before `DINO_USAGE`), 7 (sender-branch test). Skipped: 4 (any other `channel` value gets 400; low, a new channel needs the Worker first), 5 (send decision pairs Reason with Channel; low, superseded by `PathFor` picking the route once from one `ChannelOf` result), 6 (nit, `Trace.WriteLine` per beta flush).
- `s251-medium.md`: applied 1 (exact beta path match), 3 (`PathFor` from one `ChannelOf` result), 7 (stray blank line), 9 (Android finding tracked under Waiting on the user). Skipped: 2 (production guard refuses only `"beta"`; a backstop, the separate route is the isolation), 4 (nit, `ChannelOf` runs the checks twice), 5 (404 on the beta path drops the batch: handled by the Deploy order line, not a retry limit), 6 (nit, RTST answers 404 on the beta path), 8 (low, test name claims more than it asserts).
- `s251-check1-medium.md` (check pass after the fixes): 6 findings, none applied (low or nit): the `DINO_PATH_PREFIX` redeclared in `worker/events.ts`, the `"beta"` refusal as a backstop only, 404 on the beta path with the Worker not yet deployed (same as medium 5, Deploy order), 503 and retry when `DINO_USAGE_BETA` is missing (both configs declare it), the privacy sentence not repeating retention for the beta store, and a lookalike-path test that calls `handleEvents` rather than the router.

**Deploy order.** The Worker deploys on merge through Workers Builds, before any app build from the same merge reaches TestFlight, so the beta route exists when the first beta batch is posted.

**Waiting on the user.** Android Play internal and closed-track installs cannot be told from production on the device (the installer is `com.android.vending` for every track), so they count in production `dino_usage` today. Whether to accept that or plan a server-side split is the user's call; today's behaviour is kept.

**User, 2026-11-03:** The night may merge this slice; its Worker change goes live on merge through Workers Builds (production deploy approved). For 251 that includes the new DINO_USAGE_BETA Analytics Engine binding. (This does not approve the store privacy form steps.)

**Not in this slice.** RTST (same gate; a later slice if wanted). The Windows head (Store-signed only counts today; unchanged).

#### For CLAUDE.md (lifted 2026-11-04)

- `worker/CLAUDE.md`: the beta dataset rule (own route `/api/events/beta`, `DINO_USAGE_BETA` only, never mixed into production reports, `analytics-query` names the dataset). Already written in `worker/CLAUDE.md` on this branch. (Slice 251: docs/plan/slices/251-dino-beta-usage-dataset.md)

#### Not yet verified

- A TestFlight build of Dino sends rows that land in `dino_usage_beta` and none in `dino_usage` (needs a real TestFlight install; no device run was made).
- The Workers Builds deploy after the merge carries the `DINO_USAGE_BETA` binding.
