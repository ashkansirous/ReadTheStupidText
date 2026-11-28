# Slice 469: Feedback loop 2026-11-27-0154 leftovers

**Product:** Shared

*Planned 2026-11-28 by the night 2026-11-27-0154 feedback loop (`docs/plan/notes/2026-11-27-0154-feedback-loop.md`). Each item names the digest entry it came from; the repo it changes is named where it is not this one. Items in another repo are built there on a `chore/` branch and ticked here with the PR.*

**Touches:** `tools/` (item 3), `docs/plan/` (items 4, 5), ClaudeSkills `skills/`, `agents/`, `hooks/` (items 1, 2, 6-11), agent-progress-board `plugin/scripts/sync-docs.mjs` (item 12).

## Work

- [ ] 1. **Secret reads (security, entries 3412-3662).** The Slice 462 `analytics-query` agent ran an unanchored grep on `secrets.env` and printed the Play service-account JSON into its session log. (a) Rotate that service-account key through `manage-secrets` (tool-run, the key is service-issued; update the store and every CI secret that holds it, read back one Play API call). (b) ClaudeSkills: a PreToolUse guard that allows only `grep '^KEY='`-shaped reads of any `secrets.env`, and the `analytics-query` agent text pins that form. Verify: the guard test refuses `grep play secrets.env` and allows `grep '^CF_ANALYTICS_READ_TOKEN=' secrets.env`.
- [ ] 2. **Bare-label questions (entries 2416, 3294).** `question-stop.mjs` blocks a turn that ends with "Reply 1, 2 or 3" (or similar) unless each option's text is in the same message. Verify: hook test with a bare-label and a full-text message.
- [ ] 3. **`first-books-cancel-real-store` on main (entries 3925-3977; plan.md Outstanding line).** Run the flow on main's build; if it fails, the likely cause is Slice 459's intro prefetch (book downloads in about 0.1 s, so Cancel never shows): fix the flow or the prefetch test hook, and correct the Outstanding line's "seen on the Slice 455 branch".
- [ ] 4. **Parked reviewed work only on disk (s403, s466).** Commit and push the reviewed, uncommitted work in worktrees `s403` (30 paths) and `s466` (32 paths) to draft PRs #1430 and #1437, so a stale cleanup cannot lose it. Also decide the `s433` worktree (42 uncommitted paths on a closed slice): diff against main, keep what is new as an item, then remove it.
- [ ] 5. **Open items in built slices with no row (entry 3412 area).** Slice 436 A7 (secret path token on `/timePlayed`: tool-generated, so not an owner step), Slice 453 follow-ups 4 and 5 (Debug stall mode, auto-reload flow), and the 459, 443, 441 follow-ups: move each into a planned slice or an *Outstanding work* line.
- [ ] 6. **Release-only Windows build in briefs (entries 3534-3571).** `tools/CLAUDE.md` line 6 needs `Build-TestPackage.ps1` for slices touching `DinoReader.App` or `Mobile.Shared`; it ran only for 464 and 441 tonight. ClaudeSkills `run-wave` brief template (or `run-checks`) adds it from the touched paths; `code-review-pass` checks a "Debug-only API outside #if DEBUG" finding before dropping it.
- [ ] 7. **Mirror staging and status body (entries 6331-6991, 4462, 5604, 5706).** One staging folder unattended sessions always have (the night `tmp`), named in `agents/mirror-to-drive.md` and the run-all-waves briefs; a status-body template with `## Where it's live` for coordinators; `skills/mirror-to-drive/upload.py trash` refuses any `*.main.md` name (it trashed `scope.main.md` again, entry 754).
- [ ] 8. **Board step reports refused about 40 times (entry 4282 on):** helpers' `under_step` reports land on cards with no steps. Find why the coordinator's card has no step tree when helpers report, and fix the brief or the board.
- [ ] 9. **Worktree command with unset `$W` (entries 277, 3377, 3722, 4210, 4684, 5736, 5901).** The run-wave snippet uses an absolute path.
- [ ] 10. **Stale cleanup denied by auto mode (entry 6848)** while D: was at 1.9 GB: find why the same `clean-stale.mjs --stale` call passed in wave 9 and was denied in wave 10; offer the owner the allow line if one is needed (never edit `autoMode`).
- [ ] 11. **Windows VM and Chrome live checks never ran (entries 7153, 7191).** The VM needs 8 GB free and the night never had it; `.claude/live-checks.local.json` does not exist so no `[chrome]` line ran. Plan a VM window (the driver runs VM checks at a wave boundary after stopping the emulator) and create the local file with the domains the owner's earlier answers already allow.
- [ ] 12. **agent-progress-board `sync-docs` "could not read docs/plan/slices/<N>.md" (about 30 times, a different file each time).** Print the `SourceError` cause (lines 837, 981), then fix the likely 30 s `GIT_DEADLINE_MS` over two git calls per file (one `git cat-file --batch`), with a bounded retry.
- [ ] 13. **context7 re-checks owed.** Builders wrote code without context7 after the monthly quota ran out: Slice 403 (`Span.BackgroundColor`, `Xamarin.AndroidX.Window`), 424, 466 (`SecureStorage`), 442, 458 (`isTrialConversion`), 435 (ffmpeg `+faststart`, `-shortest`). Re-check each through `library-docs` and fix what differs.
- [ ] 14. **Slice 439 purchase token stored plain in D1.** The decision says encryption needs a Worker secret a night cannot set; `manage-secrets` can generate and set one. Plan the encryption (one Worker secret, encrypt on write, read old plain rows) and run `security-review` on 439, 466 and 424.
- [ ] 15. **Slice 432 iOS bundle 157.4 MB (was 84.6 MB)**, unexplained: find the cause from the IPA contents.

## Not yet verified

(Nothing built yet.)
