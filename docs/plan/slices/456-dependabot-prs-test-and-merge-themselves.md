# Slice 456 — Dependabot PRs test and merge themselves
**Product:** Shared
**Touches:** `.github/workflows/pr-gate.yml` (new), `.github/workflows/dependabot-auto-merge.yml` (new), `.github/scripts/pr-gate.mjs` (new), `.github/scripts/pr-gate.test.mjs` (new), `.github/CLAUDE.md`, the `Protect` ruleset on `main` (live setting), this slice file.

*Planned and built 2026-11-25 (user: "implement it").*

**Why:** four Dependabot PRs (#1177 from 2026-11-15, #1332, #1333, #1334 from 2026-11-21) sat open with no checks and no owner. Dependabot never merges its own PRs, no workflow turned on auto-merge, and `main`'s ruleset required no check, so auto-merge would have merged untested. No skill or slice looks at Dependabot PRs. The Actions run history before 2026-11-24 is gone for every PR (every PR merged before then shows 0 checks), so whether CI ran on those PRs back then cannot be read.

**Decisions:**
- One always-run check, `all-checks` (`pr-gate.yml`, no path filter), waits for every other check run and commit status on the PR head and fails if any failed. It is the only required check, because every other PR workflow is path-filtered and would block PRs that never start it.
- A red check does not fail the gate at once: it keeps polling and fails only when nothing is still running and the same failure has stood for 10 minutes, so re-running the red job within that time lets the gate pass (cap 170 minutes).
- Each `gh api` read is retried up to 5 times, 15 seconds apart, before an error fails the gate.
- The gate also waits on any other workflow run on the head commit that has not completed (its own run excluded), so a workflow that has not yet created its check run is still waited for.
- The gate job checks out the PR's base commit, so a PR cannot change the gate script it is judged by.
- Every Dependabot PR (majors too) gets GitHub auto-merge with a merge commit, never squash.
- Limits of the built-in token, accepted: a merge by auto-merge starts no push workflow, so the bump ships with the next release; a PR changing `.github/workflows/**` (the github-actions group) may be refused by the token and then needs a session to merge it once green.

**Work:**
- [x] 1. `pr-gate.mjs` + node tests (pure `verdict`), `pr-gate.yml` running it on every PR.
- [x] 2. `dependabot-auto-merge.yml`: `gh pr merge --auto --merge` on Dependabot PRs.
- [x] 3. `all-checks` (integration 15368, GitHub Actions) added as a required status check to the `Protect` ruleset on `main`, 2026-11-25 09:31Z, before #1357 merged (09:33Z).
- [x] 4. Re-triggered the four PRs on 2026-11-25. #1332 (wrangler) and #1334 (WindowsAppSDK 2.7.0) ran their checks and auto-merged. #1333 (github-actions group) first had `version` red: `gittools/actions` v5 needs GitVersion 7.0.x, and the repo asks for 6.x. `all-checks` held the merge. A Dependabot ignore for `gittools/actions` major versions went in, the PR was recreated (checkout 7, setup-dotnet 6, gittools 4.7.2), went green and auto-merged at 09:52Z, although it changes `.github/workflows/**`. #1177 was closed by Dependabot as no longer needed: `main` already has DependencyInjection 11.0.0 (Slice 379).

**Built:** PR #1357 (merged 2026-11-25). As expected, the three auto-merges started no release run on `main`. The bumps ship with the next release.

**Verified:** `all-checks` passed on #1357 after every other check finished, and it held #1333 while `version` was red.

**Later:** moving to GitVersion 7 (`gittools/actions` v5) is not planned. Dependabot ignores that major version until someone unignores it.
