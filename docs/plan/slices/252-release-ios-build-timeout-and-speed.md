### Slice 252 — Release CI: a hung signed iOS build can't hold the release queue for hours, and the signed iOS build gets faster

*Parked 2026-11-02 by the final feedback loop of night 2026-11-01-2355: `parked: feedback loop, waits for the user`. Product/CI code found by the loop is never built unreviewed; the user un-parks it.*

*Planned again 2026-11-03: the user un-parked it ("Yes, un-park both") before a run-all-waves run.*

**Touches:** `.github/workflows/release.yml`, `.github/workflows/dinoreader-ios.yml`, `.github/workflows/ios*.yml`, `.github/CLAUDE.md`, `docs/plan/slices/252-release-ios-build-timeout-and-speed.md`

**Why.** On 2026-11-02 run 41409120502 (head 5e1b928b) sat in `build-dinoios / build`, step "Release build (signed .ipa)", from 08:30:41Z to past 10:55Z (read with `gh api .../actions/jobs/125171781436`). The four successful runs before it took 47-53 minutes for that job (read with `gh run view`). `release.yml` uses one concurrency group per ref with `cancel-in-progress: false` (`release.yml:120-122`), and the iOS build job sets no `timeout-minutes` (`grep -rn timeout-minutes .github/workflows/`: only `release.yml:131` for the version job, `dino-store.yml`, `appstore-submit.yml`, `testflight-external.yml`, `tests.yml`, `tools-selftests.yml`), so GitHub's 6-hour default applies. Every later release run waited behind it as `pending` with no jobs, and each new merge cancelled the waiting one: nothing from waves 5-6 (234 B/C, 235 A/B, 247) was released. Wave 6 and housekeeping 6 reported the waiting run as "pending, not stuck yet" without asking what it waited for. The same hang happened once earlier that night (91 minutes, cancelled and re-run by the interactive session). The user also asked that night whether the iOS build had been moved to Debug "so it takes minutes" and got "partly, not sure": PR checks build Debug (a few minutes, e.g. #745's `dinoreader-ios` check: 2m13s), `main` builds the signed Release (about 50 minutes).

#### Work

- [ ] **1. Timeouts.** `timeout-minutes` on every signed iOS build job (RTST and Dino), about 1.5x the normal time (75 for Dino on today's numbers; measure RTST's). A hung job then fails in time and frees the group.
- [ ] **2. Re-run only the hung job.** `.github/CLAUDE.md`: when an iOS job times out, re-run only the failed jobs (`gh run rerun <id> --failed`), and note that a `pending` release run with no jobs waits on the `in_progress` run in the same group.
- [ ] **3. Measure the signed build.** One `main` run with the build step's timing (binlog or `-v:n` summary) to name what takes the 50 minutes (AOT/LLVM, linking, the MAUI workload, signing). Then decide with the user whether TestFlight builds may drop any of it. No change is shipped on a guess. *The user, 2026-11-03: "Measure only, I decide": the run measures and writes down what each step costs and what skipping it would change, and ships no build change for item 3.*
- [ ] **4. Retry the .ipa upload.** Release run 41375909759 (03:07Z 2026-11-02) failed at "Upload .ipa to the draft release" with "error connecting to api.github.com" after a 47-minute build (found by the final loop's pass 2; no wave report mentioned it). Retry that step a few times with a back-off so a network blip doesn't throw away the build.
- [ ] **5. Check.** A run with a forced short timeout fails the job and the next pending run starts (`gh run list --workflow release.yml`).

#### Not yet verified

- All items (parked).
