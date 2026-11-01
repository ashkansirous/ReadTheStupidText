# Plan: ReadTheStupidText — Windows 11 read-aloud tray app

Index of the plan. Every slice has its own file under `docs/plan/slices/`; the numbered Decisions live in [`docs/plan/decisions.md`](docs/plan/decisions.md); investigations and batch write-ups live in `docs/plan/notes/`. Slices 0-42 were moved here verbatim from the old long-form `plan.md` by `archive-docs` (2026-10-20).

## Context

The user wants a lightweight Windows 11 utility ("ReadTheStupidText") that reads text
aloud at a user-chosen speed (1x / 1.25x / 1.5x / 1.75x / 2x), with a tiny
control beside the clock for pause/resume and speed, that auto-reads selected
text where possible and falls back to a global hotkey for apps that don't
expose their selection (terminals, CLI, Claude Code). It must run at startup
and ship through the Microsoft Store. The shape was settled in `scope.md`;
this plan turns it into ordered, shippable vertical slices.

## Status

Open slices only. Finished slices (165 rows) are in [slices-done](docs/plan/notes/slices-done.md); each slice's full record is its own file under `docs/plan/slices/`.

| # | Slice | Status | PR | Story | File |
| --- | --- | --- | --- | --- | --- |
| 154 | Dino: vowel marks in Farsi and Arabic books (Farsi only: Arabic left as is; marks are a hidden "how to read" text, Decision 166) | in progress | #470 | | [154-dino-farsi-arabic-vowel-marks.md](docs/plan/slices/154-dino-farsi-arabic-vowel-marks.md) |
| 193 | German store listings for both apps | drafted; apply waits for the user | | | [193-german-store-listings.md](docs/plan/slices/193-german-store-listings.md) |
| 220 | Kokoro returns silent audio for short lines on arm64 (S21), so "Ann." and "Help!" still drop (Slice 210 follow-up) | planned (the S21 rejected the padding design: NaN output on arm64; starts with a new design) | #638 (draft) |  | [220-kokoro-silent-short-lines-arm64.md](docs/plan/slices/220-kokoro-silent-short-lines-arm64.md) |
| 221 | Device-test tooling leftovers from the 2026-10-30-2100 night (Dino read hook voice, one Dino dev package id, phone build-only switch, dispatch vs push concurrency) | planned | | | [221-device-test-tooling-leftovers-night-1.md](docs/plan/slices/221-device-test-tooling-leftovers-night-1.md) |
| 224 | Phone runner: a dead builder pid reads as a stall, and a cache miss builds inside a timed run (`-NoBuild`) | planned | | | [224-phone-runner-build-cache-gaps.md](docs/plan/slices/224-phone-runner-build-cache-gaps.md) |
| 225 | Device-test tooling leftovers from the 2026-10-31-1301 night (scripted S21 restore, device-wide change lint, emulator path for phone checks, run-flows two-package bug, per-checkout run status, a unit test for Slice 208's DecideAll) | planned | | | [225-device-tooling-leftovers-night-2026-10-31-1301.md](docs/plan/slices/225-device-tooling-leftovers-night-2026-10-31-1301.md) |
| 228 | Dino user test: kids' shelf vs grown-ups' corner made obvious (labelled Grown-ups pill, sticky "Back to my child's shelf", "Read now" in the book sheet, one intro card) | planned, needs design by Claude Code | | | [228-dino-grownups-corner-clarity.md](docs/plan/slices/228-dino-grownups-corner-clarity.md) |
| 229 | Dino user test: Done/Continue button top right on every grown-up page (Continue moves the intro wizard on) | planned, needs design by Claude Code | | | [229-dino-subpage-done-button.md](docs/plan/slices/229-dino-subpage-done-button.md) |
| 230 | Dino user test: recordings stay on the phone (warm copy), and restore merges recordings into a story already on the phone | planned | | | [230-dino-recordings-copy-and-restore-merge.md](docs/plan/slices/230-dino-recordings-copy-and-restore-merge.md) |
| 232 | Dino user test: proper landscape design for every screen, phone and tablet (after 228 and 229) | planned, needs design by Claude Code | | | [232-dino-landscape-every-screen.md](docs/plan/slices/232-dino-landscape-every-screen.md) |
| 234 | Dino: proper design for tablets held upright (shelf 3-4 columns, picture-above-text reading, tablet-sized cards; after 232) | parked: mock not drawn yet; waits for the user (skipped 2026-11-01) | | | [234-dino-tablet-portrait.md](docs/plan/slices/234-dino-tablet-portrait.md) |
| 235 | Dino on Windows: a real desktop window (size and minimum, width-driven layout, keyboard, hover, tooltips, title bar; after 232) | parked: mock not drawn yet; waits for the user (skipped 2026-11-01) | | | [235-dino-windows-window.md](docs/plan/slices/235-dino-windows-window.md) |
| 233 | "Download paused. Tap to finish." posts on the same events on Android and iOS (Slice 208 follow-up: Android also posts when the network drops in the background) | planned | | | [233-paused-notice-same-on-android-and-ios.md](docs/plan/slices/233-paused-notice-same-on-android-and-ios.md) |










M3 starts with a listening page and waits on the user's listening verdicts. 154 starts with a listening test, then adds vowel marks book by book (it also waits on the user's listening verdicts; draft PR #435) after each book's Slice 146 rewrite and open illustration PR merge. The Three Little Pigs Farsi part is built (PR #470): the marks are hidden, so kids see plain Farsi and only the speech uses the marked text (this supersedes Decision 159's "marks shown on screen" in part); the other books and Arabic are still open. Decisions 155-159 (157 reverses 132).







### Notes

- [Finished slices](docs/plan/notes/slices-done.md): the status rows of every built or verified slice, and the finished batches' lane-order paragraphs.
- [Out of scope, earlier batches](docs/plan/notes/out-of-scope-history.md): every earlier batch's out-of-scope lines, moved verbatim.
- [Decisions 1-99](docs/plan/notes/decisions-001-099.md): the archived numbered decisions, moved verbatim (100 onward: docs/plan/decisions.md).
- [2026-10-25 Dino usage insights](docs/plan/notes/2026-10-25-dino-usage-insights.md): what Play, Apple, COPPA and the UK Children's code allow for analytics in a kids' app, and the Cloudflare cost (Batch 28).
- [2026-10-25 Product separation](docs/plan/notes/2026-10-25-product-separation.md): how much RTST and Dino share, and which changes ship the other product (Batch 27).
- [2026-10-20 Android minSdk 28](docs/plan/notes/2026-10-20-android-min-sdk-28.md): why the Android floor is 28 (Decision 82).
- [Batch introductions, Batches 2-8](docs/plan/notes/batch-intros-1-8.md): the framing paragraph that opened each batch (2026-10-20 move).
- [Batch 9 and 10 indexes](docs/plan/notes/plan-batch-9-10-index.md): the former in-plan tables and lane order.
- [General verification lines](docs/plan/notes/verification-general.md): the verification lines that belong to no single slice.
- [Older outstanding work](docs/plan/notes/2026-10-25-outstanding-older.md): Slice 43-95 unverified items moved out of plan.md (2026-10-25).
- [2026-10-28 Download stall](docs/plan/notes/2026-10-28-download-stall.md): why a pack download hung on the phone and the fix (PR #537; Slices 182/183 follow).
- [2026-10-30 Feedback loop](docs/plan/notes/2026-10-30-feedback-loop.md): the first end-of-session feedback loop over the 2026-10-29/30 run-wave session; each finding and where it went.
- [2026-10-30 Feedback loop, S21 session](docs/plan/notes/2026-10-30-feedback-loop-s21-session.md): the feedback loop over the S21 night and its follow-ups (phone tooling, check runner, App Store); each finding and where it went.
- [2026-10-30 night, round 1 feedback loop](docs/plan/notes/2026-10-30-2100-feedback-loop-w1.md): the feedback loop over wave 1 and housekeeping 1 of the 2026-10-30-2100 run-all-waves night; each finding and where it went.
- [2026-10-31 night, round 1 feedback loop](docs/plan/notes/2026-10-31-1000-feedback-loop-w1.md): the feedback loop over wave 1 and housekeeping 1 of the 2026-10-31-1000 run-all-waves night; each finding and where it went.
- [2026-10-31-1301 night, round 1 feedback loop](docs/plan/notes/2026-10-31-1301-feedback-loop-w1.md): the feedback loop over wave 1 and housekeeping 1 of the 2026-10-31-1301 night (S21 network cut, stale phone mirror, duplicate Drive folders); each finding and where it went.
- [2026-10-31-1301 night, final feedback loop](docs/plan/notes/2026-10-31-1301-feedback-loop-final.md): the last loop of the run-all-waves run (the supervisor session and the night), mistakes that came back after a round's fix, and where each finding went.
- [Outstanding work, full text](docs/plan/notes/2026-10-30-outstanding-detail.md): the verbatim per-slice unverified items that plan.md's Outstanding work now shortens (2026-10-30).
- [CLAUDE.md history](docs/plan/notes/claude-md-history.md): the Batch 6-10 narrative blockquotes moved out of `CLAUDE.md` (2026-10-20).

## Outstanding work

- **Not yet tried on a device:** 114 checks across 107 slices, in [docs/plan/unverified.md](docs/plan/unverified.md).
- Decided 2026-10-30 (user): old RTST versions (up to rtst-v1.73.2) that still fetch the broken Norwegian pack are left as they are: no Norwegian use on them in the usage counts (corrected 2026-10-30: the counts then covered only about 3 days, 2026-10-27 to 10-29, exact `nb`/`no` codes only, small numbers; recheck with a longer window before relying on it). Slice 217 adds the forced update for next time ([Slice 201](docs/plan/slices/201-norwegian-pack-crash.md)).
- **User (App Store, both apps), only when going live (TestFlight needs none of this):** never submitted to the App Store. The 2026-10-30 `appstore-submit` run ([41078343356](https://github.com/ashkansirous/ReadTheStupidText/actions/runs/41078343356)) lists what Apple wants: store text, category, copyright, price, content rights, age rating, review contact (phone) and iPhone 6.5-6.9" + iPad 13" screenshots. Drafts of all but the screenshots: `docs/store-listings/appstore-rtst.md`, `docs/store-listings/dino/appstore.md` (age-rating enum names and the Kids-category rules there were written from memory: check on the first API write). The user decides content rights, age rating, the contact phone and email, and whether Dino goes in the Kids category; the screenshots need a macOS CI simulator job (no slice yet). On 2026-10-30 the user said "update the app store with the values"; writing them was blocked by the permission check, and it waits on the user confirming five values (content rights, the 4+ age-rating answers, reviewer contact `elinsoftuk@gmail.com` with no phone, Kids category off, the drafted text/category/copyright/free price). Then run the [workflow](https://github.com/ashkansirous/ReadTheStupidText/actions/workflows/appstore-submit.yml).
- **Play listings out of date (both apps):** RTST still says "no analytics" (wrong since Slice 137) and Dino says "3 to 8, nine languages" (the other stores say 3 to 9; check the language count against the app); Dino's privacy page also says "3 to 8". Fix through the `store-listing` agent (draft, user approves, apply).
- Slice 201 review leftovers that change behaviour (listed in the slice file): a previously installed bad `piper-nb` now reads as "not installed" with no release log; `packs.yml` filter uses `splitlines()` while C# splits on `
`; the pack-shape check hard-codes 5 files; `RequirePiperTokens` gates on VITS, not the Piper id list.
- Slice 202 merged on an incomplete second review pass (the forked review never finished); rerun `/code-review` medium on `ReaderPage` as merged. Slice 213's unreviewed half is Slice 216 item 9.
- Slice 202: the `InitWait`-before-text order is kept only by a comment, with no test ([slice](docs/plan/slices/202-reader-language-change-crash.md)).
- Slice 210 (PR #631): 3 of 68 short lines still drop on the S21, moved to Slice 220 ([slice](docs/plan/slices/210-kokoro-short-line-dropped.md)).
- Slice 218 (PR #630): Microsoft Store submission of rtst-v1.77.0 done (run 41137137107, marker tag `rtst-store-v1.77.0`); still open: Partner Center showing 1.77.0 once certification finishes ([slice](docs/plan/slices/218-microsoft-store-submission-skipped.md)).
- Slice 218 merged on an incomplete medium review (it read the diff without the review fork); the round 1 feedback loop reran it on the merged diff ([note](docs/plan/notes/2026-10-30-2100-feedback-loop-w1.md)). Why users stayed on 1.67.1 is still unproven (1.73.2 was committed to Partner Center).
- ClaudeSkills tooling still to build (final feedback loop of night 2026-10-31-1301): `note-claims.mjs` gives "merged" to the wrong PR in a list like "#113 (..., merged), #114 (...)" and treats a backticked `%VAR%` example as a path; then ClaudeSkills #114's own leftovers listed in its body ([note](docs/plan/notes/2026-10-31-1301-feedback-loop-final.md)).
- **Waiting on the user:** Slices 234 (tablet portrait) and 235 (Windows window) are skipped (2026-11-01) until you see and approve their mocks (Batch 45 and 46) ([234](docs/plan/slices/234-dino-tablet-portrait.md), [235](docs/plan/slices/235-dino-windows-window.md)).
- **Waiting on the user:** Slice 217's live `minVersion` (the forced update) is set only with your approval, once store builds above it are live on that platform ([slice](docs/plan/slices/217-rtst-forced-update.md)).
- Slice 208, Android vs iOS: decided 2026-11-01 (user): make them work the same, planned as [Slice 233](docs/plan/slices/233-paused-notice-same-on-android-and-ios.md). (The earlier line here said iOS also posts when the app is suspended; the code shows iOS posts for any resumable end in the background, Android only on the time limit, and neither on an OS kill.)
- **Waiting on the user:** `~/.claude/settings.json` holds the allow rule `Bash(git -C D:/ReadTheStupidText-worktrees/*:*)`, which mixes `*` with `:*` and so matches nothing; every headless session prints a warning about it. It is your local rule (not from ClaudeSkills); `Bash(git -C D:/ReadTheStupidText-worktrees:*)` is the prefix form, or delete it.
- **Waiting on the user:** the primary checkout `D:\ReadTheStupidText` has an uncommitted, corrupted edit to `docs/plan/slices/215-device-guard-gaps.md` (main has the right text); discard it with `git -C D:\ReadTheStupidText checkout -- docs/plan/slices/215-device-guard-gaps.md`. Agents may not touch the primary checkout.
- Untried: reading the iPhone 8's crash logs from this PC (e.g. libimobiledevice `idevicecrashreport` over USB); would let iOS crashes be checked without the user. Not tried yet.
- S21 RTST dev install may still hold `files/packs/zz-supertonic-3.bak` (~124 MB) from the 2026-10-29 night; harmless, gone on the next `reset-dev-app.ps1 -Level clear`. Nobody has checked.


**Store privacy declarations (legal; gates all usage counts, Decision 145).** No `telemetry.enabled` is set to `true` for an app until its rows are ticked. Claude drafts the answers and fills the forms in the user's signed-in Chrome; the user reviews and presses Submit/Publish.

- [x] RTST repo texts: `PRIVACY.md`, `site/privacy.html` (deployed), `STORE.md`, iOS `PrivacyInfo.xcprivacy` (Slice 137; Windows sources in Slice 140; checked 2026-10-26: phone and Windows covered). Claude.
- [x] RTST Google Play Console: Data safety (app activity and app info/performance, not linked to the user, not shared, optional to turn off). User submits. Submitted/published 2026-10-27.
- [x] RTST Apple App Store Connect: App Privacy (usage data and diagnostics, not linked, no tracking). User publishes. Submitted/published 2026-10-27.
- [x] RTST Microsoft Partner Center: privacy policy URL and the data-collection answers. User submits. Privacy URL already correct.
- [x] Dino Google Play Data safety and Apple App Privacy for its usage counts (Slices 133-134, still pending). User submits. Submitted/published 2026-10-27.
- [x] Then: enable Analytics Engine, add the bindings, deploy, set `telemetry.enabled` per app. Done 2026-10-27 (PR #487).

Built but not verified (details in each slice file):

- Slice 220 (draft #638, parked): the S21 run rejected the padding design (silent takes are full-length NaN samples on arm64). It needs a new design that starts by finding the NaN source; that is design work, not a user decision. Moved to planned by the user on 2026-11-01. The S21 record is in the slice file on main ([slice](docs/plan/slices/220-kokoro-silent-short-lines-arm64.md)).
- Slice 194 (PR #591): the panel shot is not good enough for the site (guest offline, no voice pack, watermark); retake it with the voice pack copied ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o002) ([slice](docs/plan/slices/194-control-panel-screenshot-script.md))
- Slice 193 (PR #589, drafts only): the user reads `docs/store-listings/de.md` and `dino/de.md` and approves, then the `store-listing` agent applies; ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o003) ([slice](docs/plan/slices/193-german-store-listings.md))
- Slice 203 (PR #578): the first automatic Dino Store submission (needs the next Dino release on `main`) ([slice](docs/plan/slices/203-dino-microsoft-store-live.md)).
- Slice 200 (live data checks): re-run the report after more real traffic (under 10 events per group everywhere on 2026-10-30); no rows yet for voice ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o013) ([slice](docs/plan/slices/200-live-usage-data-checks.md))
- `src/DinoReader.App/CLAUDE.md` is 41 KB (budget ~20 KB; promised split never planned): run `archive-docs` on it between waves, when no other branch edits it.
- RTST Microsoft Store English listing is stale (neutral English text from before the voice packs; found in Slice 193's live read, which covers ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o014) ([slice](docs/plan/slices/193-german-store-listings.md))
- PR #435 (Slice 154, Dino Farsi and Arabic vowel marks, open): Slice 192 bumped all 15 store books after it branched (Three Little Pigs is v13 on ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o015)
- User steps, Slice 183: native review of the new strings (13 machine drafts per app). The Play foreground-service declaration is filed for both apps (2026-10-29, demo videos PR #561).
- 182 design frame R12 (wait screen for translation downloads) is planned as Slice 187.
- Testing gap: Dino has no Debug hook to force a pack failure; testers corrupt the `.partial` file by hand. Worth adding with Slice 187.
- Slice 164 (PR #491): pure moves, unit-tested, no device run needed. Follow-up done (PR #557: SpeedPreset, EmptyPlayer, Dino LanguagePick, ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o027) ([slice](docs/plan/slices/164-more-logic-in-unit-tests.md))
- Slice 168 (packs-8): existing Supertonic users re-download ~145 MB (`pack_version` 4) ([slice](docs/plan/slices/168-german-in-both-apps.md))
- Slice 154 (Three Little Pigs Farsi part, PR #470; its review tooling data now lives under `content/dino/three-little-pigs/translations`, Slice ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o045) ([slice](docs/plan/slices/154-dino-farsi-arabic-vowel-marks.md))
- Slice 157 (book content out of the tests, PR #475): first main run of `publish-store` with the new builder; `ShippedTitles` edits do not reach live ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o046) ([slice](docs/plan/slices/157-dino-book-content-out-of-tests.md))
- Slice 158: `actionlint` on `release.yml` (runs in CI). The first `main` run (run 40500814826) uploaded all 15 books and `--verify` passed, so that item is done ([slice](docs/plan/slices/158-dino-publish-store-books.md)).
- Done 2026-10-24: packs-5 (packs run 40081509906; fixes #295 Python 3.12, #296 `no_dict` guard) carries Supertonic v2 with da/fi/sv/nl, piper-nb and ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o050)
- Slice 127: Dino's first Microsoft Store submission went in by hand on 2026-10-29 (1.78.1.0, x64 and ARM64, 15 listing languages, machine drafts ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o064) ([slice](docs/plan/slices/127-release-safety-fixes.md))
- Slice 132: seed R2 `config.json` after merge; emulator: Version card and "A new version is ready" row PASS, and the resume fetch (follow-up #366) ... [full text](docs/plan/notes/2026-10-30-outstanding-detail.md#o067) ([slice](docs/plan/slices/132-dino-remote-config-and-force-update.md))
- Dino cold start: fixed in Slice 177; Pixel cold first audio 3.2-3.3 s ([slice](docs/plan/slices/177-dino-cold-start-synthesis.md))
- Slice 227 (PR #654, crash reports) deployed and live: Worker deployed (Workers Builds success on b8c420d); `POST https://dinoreader.sirous.uk/api/crash` answers 204 with crash reports off; `/crashes/x.json` answers 404; R2 lifecycle rule `crash-reports-90d` (prefix `crashes/`, 90 days) added 2026-10-31. Still open: the read-only R2 token `CF_R2_CRASHES_READ` could not be created (no Cloudflare token in `D:\secrets` may create API tokens), so either the user creates an R2 read token in the Cloudflare dashboard or `tools/crashes/pull-crashes.mjs` is changed to read through the Cloudflare REST API with wrangler's own login; App Store App Privacy "Crash Data" published for both apps and Play Data safety "Crash logs" saved for both (2026-11-01); user: press "Submit 1 change for review" in each app's Play Publishing overview, then once live the main thread sets `crashReports.enabled` true in both remote configs; item 9 (symbolicate the 2026-10-28 TestFlight crash) open ([slice](docs/plan/slices/227-crash-reports.md)).

## Out of Scope

- **(Batch 31)** Animals and sounds for parent-provided books (photos, PDFs, typed: unchanged); robotic-voice checks beyond English unless English shows flavours are the cause; reading speeds above 1.2x in Dino; vowel marks for languages other than Farsi and Arabic.
- **(Batch 28)** Any install, device or advertising id; per-family profiles or parental-consent flows; third-party analytics SDKs; analytics in RTST; remote config for RTST.
- **(Batch 27)** Splitting into two repos or publishing the shared code as NuGet packages; renaming the shared projects or their namespaces beyond Mobile.Shared; a commit flag that skips one product's release.

Earlier batches' out-of-scope lines (still binding unless a later Decision reverses them): [out-of-scope-history](docs/plan/notes/out-of-scope-history.md).
