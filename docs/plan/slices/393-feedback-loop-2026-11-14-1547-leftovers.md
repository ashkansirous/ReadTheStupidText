# Slice 393 — Feedback loop 2026-11-14-1547 leftovers
**Product:** Shared

*In progress (wave 2026-11-17, night 2026-11-17-2105 wave 2), draft PR. ClaudeSkills items go in their own ClaudeSkills PR (worktree `D:/ClaudeSkills-worktrees/cs393`).*

*Parked: feedback loop, waits for the user (2026-11-15; item 25 added, merged as PR #1184, 4e59bd1f). Found by the night 2026-11-14-1547 feedback loop ([note](../notes/2026-11-14-1547-feedback-loop.md)); nothing here is built until the user has looked at it.*

*Planned 2026-11-16 (plan.md row set to `planned`, "none needs the user", 13dc9a0d): the park line above is history; waves build these items.*

**Touches:** `D:\ClaudeSkills` `skills/feedback-loop/check-review-skips.mjs`, `skills/voice-pick/SKILL.md`, `skills/run-all-waves/`, `tools/phone/`, `tools/desktop-vm/`, `.maestro/dino/`, slice files 243, 291, 317, 330, 346, 384, this slice file.

Entry numbers are the digests' entries (`D:\ReadTheStupidText-worktrees\run-all-waves\2026-11-14-1547\fl\`, outside the repo, may be deleted).

## Work

Tooling (ClaudeSkills code; goes through the review gate):

- [ ] 1. `check-review-skips.mjs` prints the exact expected skip line on a failure, exempts skip lines older than the checker (wave 3 rewrote four 2026-11-02 lines in the Slice 244 file to pass it), and requires one Applied or Skipped line per finding number of each cited findings file (wave 4's 378 record lacks Applied lines for medium #3, #4, #8). Entries dC 200, 609, 1036-1071, 785-813, 1116, 1338. Verify: the checker's tests cover all three.
- [ ] 2. Skipped findings of a ClaudeSkills PR are recorded in a slice file: ClaudeSkills #218's skips (s317cs-high #6, #7; s317cs-medium #3, #5, #6, #8; s317cs-check lows) are in no slice file. Copy them into the Slice 317 file if the review files are still in `D:\ReadTheStupidText-worktrees\reviews\`, and make the gate check it. Entries dB 884-1181.
- [ ] 3. Low follow-ups that live only in PR bodies go into their slice files: #1120 (346 check5: Update state shown as Downloaded after Cancel, a stale run's usage count, a truncated quote on a `*_FILE` key), #1122 (243 check3/check4: a background launch's exit, unknown launch start, boundary tests), #1121 (317 check3: a 502 after a real commit fails the job, the retry fires on 4xx), #1071 (phone: `Select-ConnectedAddress` can print ready after a failed connect, `-Serial` has no shape check, hard-coded S21 serial). Verify: each line is in its slice file.
- [ ] 4. Phone lease and preflight quirks: `lease.ps1` with no parameters errors, `-Release` without `-Owner` fails, preflight reports a foreign lease as FAIL/NO-GO instead of WARN, one adb stall not investigated. Entries dA 1885, 2131.
- [ ] 5. Low memory: when free memory drops below the VM minimum, the driver writes a snapshot of the top processes into the night's status; the VM checks for Slices 364 and 373 were skipped at 5.8 GB free and the starter's timer was killed about 9 times. Entries dA 956-9071, 1002, 3026.
- [ ] 6. `voice-pick` and the `run-all-waves` start step check "already answered or measurable?" before publishing a listening page (listening pages for Slices 334 and 335 came back after they were answered). Entries dA 1046, 1214.
- [ ] 7. `plan-not-needed: N` goes into the wave prompt or brief template: `brief-plan-guard` denied about 15 briefs across waves 1-9 for a context slice named without it. Entries dB 245-772, digest2 272-109.
- [ ] 8. Shell writes to tracked files: the guard blocked some `sed -i` and inline-python edits but not others (digest2 wave 7 107, 569; wave 8 136; wave 9 83); builders still edit through helper scripts that change line endings (dB 846-1676). Make the guard consistent.
- [ ] 9. plan.md's budget is stated in bytes in one place (49.7 KB, 50.9 KB and 50,207 bytes were reported in hk7-hk9), and housekeeping runs `archive-docs` when it is over.
- [ ] 10. The device-test agent refuses a hand-driven emulator run unless `run-flows.ps1` was tried, and stops at 10 minutes (wave 2: 377 run by hand without a lease; 376 run 11 minutes). Entries dB 1464, 1758.
- [ ] 11. A slice file never cites a worktree `tmp/` path that `worktree remove --force` deletes (wave 7 lost Slice 383's evidence files). digest2 1107, 1204.

Product and harness:

- [ ] 12. Why `store_unreachable` on the Debug emulator in the 377 item 16 run (`HOOK open-book failed no book`, with mobile data off and a changed `wm size`): the test setup or a store-client bug. dB 1464.
- [ ] 13. The Windows VM check chain leaves first-run unfinished after `dino-onboarding-keys`, so `dino-launch` fails and `dino-reorder` (382) is never reached. digest2, Slice 391 Not yet verified line.
- [ ] 14. Slice 291's Android shots: RTST 1-home shows a download notification and a shield icon and a different clock (reviews `s291-check5.md` #1, #3); slice 291 line 172 says "clean". Retake with status-bar demo mode, fix the wording. dC 1137, 1315.
- [ ] 15. emulator-5554's Dino dev install was left with its in-app language on German by wave 5's device run. Reset it, and the device-test brief resets the in-app language too. wave 5 final report (session fbc7ee91, entry 1471).
- [ ] 16. Slice 330 was closed "by measurement" while its four items and Verification line stay unticked; the measuring agent could not tell a clip from a fast fade. Tick or strike the items and add the caveat. dA 1122.
- [ ] 17. plan.md's *Waiting on the user* lines for Slice 384 (store subscription setup) and Slice 291 have no deep links to App Store Connect and Partner Center; look up the ids, and check whether `store-products.mjs --apply` can do the Play and App Store Connect parts so the user does only the rest. digest2 1519, dC.

- [ ] 18. A drop of more than 20% in a test project's total between a PR's before and after counts needs a one-line reason in the slice file (Slice 383: `BeatCoverFactsTests` went from 2,809 cases to 34, DinoReader.Tests from 7,288 to 4,513; nobody checked that the 34 still cover the "gate or sheet over the page" rows). digest2 wave 7 532, 777.
- [ ] 19. Slice 387's file says item 3 was built and merged (#1168) but its checkbox is still `[ ]` and line 68 says not to mark it fixed until an iOS run shows the laid-out title width; settle which is true and make the file agree. hk8 1042.

- [ ] 20. When the user must paste a settings line (an `autoMode` allow line), the session gives them the file in a ClaudeSkills worktree on a branch (`code -g <worktree>/home/settings.json:<line>`), never the primary checkout: the 2026-11-14 paste into `D:\ClaudeSkills\home\settings.json` left the primary dirty, the guard blocked the restore, and the user ran `git restore` by hand twice. dA 4023-4049, 5843, 5914.

- [ ] 21. ship-branch's post-commit check also greps the whole message for `\+semver *:` when the caller named no marker (wave 4 entry 797: the agent added `+semver: patch` to the body of 6064ae21 by itself; ClaudeSkills #223 checks only "patch intended"). The GitVersion prose-token footgun.
- [ ] 22. `agents/update-docs.md` and the ship-branch brief give the full path `~/.claude/skills/run-wave/count-unverified.mjs` (wave 3 entry 1569: an agent reported it missing and left PR #1139's test-plan line unticked).
- [ ] 23. One item hardens the shared iOS simulator harness (`.github/scripts/ios-sim-*.sh`: a driver-ready retry, a longer first wait) instead of three slices recording the symptoms (244: StoryTile 60 s timeout and XCUITestServerError Unreachable; 387: `IOSDriverTimeoutException`; 291: the Dino iPad flow). dC wave 4 1201, 1217, wave 5 1324.
- [ ] 24. Compare the Google Play en-US download note's "crash reports" sentence (`docs/store-listings/`) with the code; Slice 291's code check says there is no separate crash reporter on Windows. Re-push the Play text only if it is wrong. dC wave 3 328.
- [ ] 25. ClaudeSkills `skills/feedback-loop/illustrator-brief-guard.mjs`, wired 2026-11-15 (ClaudeSkills #224), findings left open by `D:/ClaudeSkills-worktrees/reviews/wireig-high.md`: #1 the global guard names `dino-story-illustrator`, which exists only in this repo, so a Gemini brief in another repo is denied with a hint to an agent it does not have (scope it to repos that have the agent); #2 the bare `\bgemini\b` match denies briefs that only mention Gemini; #4 `fork` and `claude` agents pass unchecked. Item 20 happened again on 2026-11-15: the paste was opened in the primary `D:\ClaudeSkills`, not a worktree.

Item 9 names the unit: 50,000 bytes.

## Decisions

- Parked for the user: the loop ran outside the implementation phase, and these were not reviewed by the user.
  - Checked 2026-11-16: no item names an ask for the user. The row goes back to planned (items 12-15 need an emulator or VM).
