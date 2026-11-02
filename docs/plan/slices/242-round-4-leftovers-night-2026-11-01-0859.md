### Slice 242 — Leftovers from night 2026-11-01-0859 round 4 (run markers, emulator no-build, a raw separator byte, a tablet AVD, 232's mock deviations, a workflow permission check)

*Planned.* Written 2026-11-01 by the round 4 feedback loop ([note](../notes/2026-11-01-0859-feedback-loop-w4.md)). Parked until 2026-11-01 by the round 4 feedback loop: waits for the user; moved back to planned by the night's final feedback loop.

*In progress (wave 2026-11-02, night 2026-11-01-2355 wave 2), draft PR: items 2, 3, 4, 5 and 7. Item 6 is not in this claim: it edits Slice 232's reading-screen files (`ReadingPage.xaml.cs`, `PictureTextPages.cs`, `LandscapeLayout.cs`) while 232 part C is open, so it stays open for a later wave.*

**Touches, this claim (2026-11-02):** `tools/phone/common.ps1`, `tools/phone/test-*.ps1` (item 2 self-test), `tools/emulator/**` (items 3 and 5: `install-debug.ps1`, `run-flows.ps1`, `new-avd.ps1`, `start-warm.ps1`, `common.ps1`, self-tests), `tools/CLAUDE.md`, `src/DinoReader.App/TextStep.xaml.cs` (item 4, the one character only), `.github/scripts/check-called-workflow-permissions.mjs` and its `.test.mjs` (new, item 7), `.github/workflows/release-scripts.yml` and `.github/workflows/tools-selftests.yml` (path filters only, if needed), `.github/CLAUDE.md`, `docs/plan/slices/242-round-4-leftovers-night-2026-11-01-0859.md`. Read-only: `.github/workflows/*.yml` (item 7's test reads them).

**Item 3 kept (coordinator, claim):** Slice 241 only overlaps the build with a cold emulator boot (`install-debug.ps1:31`); on a warm emulator a cache miss is still a full build, so `-NoBuild` stays useful. **Item 5:** if creating the AVD under the Android SDK / `%USERPROFILE%\.android\avd` is denied at night, park item 5's AVD creation only (record the denied step here), still ship the script change.

**Touches:** `.github/scripts/` (item 7's check, if it is a script), `.github/CLAUDE.md`, `tools/phone/common.ps1`, `tools/emulator/**`, `tools/CLAUDE.md`, `src/DinoReader.App/TextStep.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.Core/LandscapeLayout.cs`, `.github/workflows/release.yml`, `docs/plan/slices/242-round-4-leftovers-night-2026-11-01-0859.md`

#### Work

- **1. Dropped (user, 2026-11-01 21:48):** a read-only Store submission-status workflow existed only to answer whether the 2026-10-30 run submitted Dino for Windows and whether it passed certification. The user: "why do you need me on this and why do you fucking care?! it was 2 days ago and a lot has been merged since!" Not built.
- [ ] **2. Run-marker owners can collide.** `Get-RunMarkerPath` (`tools/phone/common.ps1:540`) replaces unsafe characters with `_`, so owners differing only there (`night/1`, `night:1`) share one marker file and the second claim overwrites the first run's marker (Slice 221 second check pass, finding 2). Fix: add a short hash of the raw owner to the file name. Verify: a self-test with the two owners gets two files.
- [ ] **3. A timed emulator run never builds.** Wave 4's emulator agent for Slice 225 spent about 5 of its 10 minutes building the Dino Debug APK (wave 4 digest entry 1983), because the coordinator had not built first. The phone runner already stops on a cache miss with `-NoBuild`; `install-debug.ps1` and `run-flows.ps1` have no such switch. Add `-NoBuild` (and `RTST_NO_BUILD=1`) there: on a cache miss print `cache missed for <app>, key <k>: run install-debug.ps1 -BuildOnly first` and exit with its own code. Verify: a self-test with an empty cache exits at once with that line. Before building, compare with Slice 241 (merged as PR #708 on 2026-11-01: on a cache miss it builds while the emulator boots); if 241 makes the build cost small, drop this item.
- [ ] **4. A raw separator byte in source.** `src/DinoReader.App/TextStep.xaml.cs:74` joins the analysis key with a raw 0x1f byte in a char literal. It is deliberate (a unit separator), but it is the only hit of `control-chars.mjs --all` on the repo and hides real hits. Write it as `'\u001f'`. Verify: `control-chars.mjs --all` on the repo exits 0; Dino tests pass.

- [ ] **5. A tablet AVD.** Slices 232 (parts B-E), 234 and 171 need "a tablet AVD", but only `RTST_Test` and `RTST_Test2` exist (wave 4 digest entry 1723). Add a tablet AVD (short side at least 600 dp) to the emulator setup script, and add `-Instance tablet` to `run-flows.ps1`. Verify: `dino-landscape-reading` runs on it and takes the row branch.

- [ ] **6. Slice 232 part A's deviations from the approved mock** : tablet frame 127's title above the card, phone side margins 34/26 dp (now about 14), and the picture-only chip on the picture per frame 114. The user approved that mock, so build to match it; no further question (decided by the night's final feedback loop, 2026-11-01). Verify: screenshots against frames 113-115 and 127.
- [ ] **7. A called workflow's job permissions checked against its caller.** PR #702 shipped a `dinoreader-windows.yml` job asking `actions: write` while `release.yml`'s `build-dinowin` granted only `contents: write`. Every release on main then ended `startup_failure` until hotfix #704 (two runs, 41351722978 and 41353452971). actionlint did not catch it, and #704 added only a prose rule to `.github/CLAUDE.md`. Add a script test under `.github/scripts/` that, for each `uses: ./.github/workflows/*.yml` job, fails when a called job asks for a permission the caller does not grant. Verify: the test fails on #702's version and passes on main.

#### Not yet verified

(none yet; nothing built)

## User answers before the night run (2026-11-01, evening)

- Night sessions run in permission mode auto. Creating the tablet AVD (item 5) writes into the Android SDK outside the repo; if auto mode denies it, record the denied step here and park only item 5 and the checks that need the tablet.
