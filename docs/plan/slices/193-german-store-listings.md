### Slice 193 — German store listings for both apps
**Product:** Shared

*Built 2026-11-14, merged as PR #1128 (46641cc8): Play RTST de-DE pushed and read back, App Store de-DE (both apps) applied 2026-11-01, Partner Center German built into the RTST Microsoft Store pipeline (reaches the Store with the next RTST release). Left: the native German read and the post-release Partner Center read-back.* *Drafted 2026-10-30 (PR #589); approved by the user 2026-11-01; App Store applied, Play and Partner Center open.* *Planned 2026-10-29.* Store text only, no app code. Gap found in Slice 168 (both apps read German since then).

**Touches:** `docs/store-listings/de.md` (new, Read The Stupid Text), `docs/store-listings/dino/de.md` (new, Dino), `STORE.md` (where listings are kept), `docs/plan/slices/193-german-store-listings.md`, `.github/scripts/store-listings.mjs` and `.github/scripts/store-listings.test.mjs` (RTST German in the Microsoft Store build).

#### Why

Neither app has a German listing on Google Play or the App Store, and Read The Stupid Text has none on the Microsoft Store. Dino's Microsoft Store listing already has German (first submission, 2026-10-29).

#### Claim notes (2026-10-30)

- User, 2026-10-30: draft only tonight; nothing goes live until the user approves the German text.
- **User, 2026-11-01: approved** ("193 should not be waiting for me. I approved"). Apply started the same day through the `store-listing` agent.

#### Status 2026-11-07: nothing waits on the user except one permission

- App Store: done (below).
- **Google Play needs one permission:** "Manage store presence" for the service account `auto-deploy-to-google-play@read-the-stupid-text.iam.gserviceaccount.com` on both apps (Play Console, Users and permissions). Granting it from Claude's Chrome was refused by the auto-mode permission check on 2026-11-07, so it is the user's one click, or an allow from the user. Then the apply is rerun (Slice 291 item 1 corrects the same listings in the same pass).
- **RTST Partner Center German** was planned through Chrome; replaced 2026-11-14 by the pipeline route (see *Push results*).
- Native German read of both texts is still to do; it does not block the apply.

#### Apply result (2026-11-01)

- **App Store Connect, both apps: done.** de-DE added to app info (name, subtitle) and to the version in `PREPARE_FOR_SUBMISSION` (RTST 1.76.1, Dino 1.81.1: description, keywords, promotional text); read back matches the drafts. Dino's "auf dem Handy" became "auf dem iPhone oder iPad". Nothing submitted; the German goes to Apple with the next submission.
- **Google Play, both apps: blocked.** `edits.listings.update` de-DE returned 200 but `edits.commit` returned 403 "The caller does not have permission" (twice on RTST, once on Dino); edits deleted, Play unchanged. The service account `auto-deploy-to-google-play@read-the-stupid-text.iam.gserviceaccount.com` needs "Manage store presence" for both apps in Play Console, *Users and permissions* (an owner's change). Then rerun the apply.
- **Microsoft Partner Center, RTST: not done.** Its API secrets are GitHub Actions secrets only (not in `D:\secrets`); the only API path is `store-submit.yml`, which submits a package too. Not checked: whether that flow carries listing text.
- Native German read of both texts still to do.

#### Agents

The `store-listing` agent (global, Sonnet): **draft** writes both German files and diffs them against the live listings; after the user approves, **apply** pushes them through the Play Developer API and the App Store Connect API (keys in `D:\secrets`) and Partner Center (Chrome up to the final Submit if no API path).

#### Work

- [x] Write the German listing for Read The Stupid Text from `docs/store-listings/en.md` (title, short and full description, keywords, what's new). Drafted in `docs/store-listings/de.md`, 2026-10-30.
- [x] Write the German listing for Dino from its live Play listing text, updated for today (15 languages, ages 3-9). Drafted in `docs/store-listings/dino/de.md`, 2026-10-30; Dino's Microsoft Store German listing is already live and stays untouched.
- [x] Fill them in the consoles: Play RTST de-DE pushed 2026-11-14 (Dino Play de-DE already live), App Store Connect both apps 2026-11-01, Microsoft Partner Center RTST built in this PR (`.github/scripts/store-listings.mjs`, ships with the next RTST release; read-back is in *Not yet verified*).

#### Draft result (2026-10-30)

Live read (read-only): Play has only `en-US` (RTST) and `en-GB` (Dino); App Store Connect has only `en-US` for both, version 1.0 in `PREPARE_FOR_SUBMISSION` with empty description and keywords (German can be staged there); Microsoft Store: Dino already has a live German listing, RTST has only the stale neutral English one. **Apply waits for the user's approval.**

#### Not yet verified

- Native German read of both files (machine-written).
- After the next RTST release's certification: the Partner Center submission shows the German listing with the `docs/store-listings/de.md` text (it replaces the existing `de` listing).

#### Verification

- Each console shows a German listing; the public Play and App Store pages in German show the new text after review.
- Machine-written: a native German read stays on *Outstanding work*.

## Push results (2026-11-14 night)

Live read first (2026-11-14): Play RTST had 16 locales, no de-DE; App Store RTST 1.76.1 (`PREPARE_FOR_SUBMISSION`) had de-DE with a description and an en-US with an empty description (promotional text and keywords also empty). No permission denial.

- [x] **Google Play, RTST de-DE:** title (20), short (67) and full description (1583, long text plus the Play download note) from `docs/store-listings/de.md` pushed in one edit and committed; a fresh read-only edit read back equal on all three fields; that edit deleted. (The Play API works for this service account since Slice 374; the 2026-11-01 403 is gone.)
- [x] **App Store, RTST 1.76.1 en-US description** (Slice 377 item 3): filled from `docs/store-listings/appstore-rtst.md` section 2 (1177 chars), read back equal. Version not submitted. Still empty in the same en-US localization: promotional text and keywords (the draft has both); fill them before submission.
- [x] **Microsoft Store, RTST German: route A is built in this PR; it reaches the Store with the next RTST release.** Credentials exist only as GitHub secrets (usable only inside `store-submit.yml`, which publishes a package with each run). `.github/scripts/store-listings.mjs` now builds `de` for RTST only (`languagesFor`; `de.md` is sectioned, read by a fallback parser, with the 7-term and 21-word limits checked by the tests); Dino's German stays out. The Partner Center `de` listing is replaced wholesale by `de.md` (title, short and full description, search terms): the user approved the German text on 2026-11-01. To confirm after the next RTST release: the Partner Center submission shows the German listing with the `de.md` text (the `Check the repo's listing text` step fails the run early on a bad file).
- Machine draft; the native German read stays open (`docs/plan/unverified.md`). Dino German: App Store done 2026-11-01, Dino Play de-DE already live; not touched.
- [x] **App Store, RTST 1.76.1 en-US promotional text (152 chars) and keywords (87 bytes)** filled from `docs/store-listings/appstore-rtst.md` section 2, read back equal; description still 1177 chars; version still `PREPARE_FOR_SUBMISSION`, not submitted.

*Changed while building:* the plan said Partner Center German would go in through Chrome; instead `.github/scripts/store-listings.mjs` builds `de` for RTST (Dino unchanged), so the next RTST release's submission carries it and replaces the existing `de` listing with the user-approved text (2026-11-01). Checks: `.github/scripts` node tests 411 pass, actionlint 0.

*From review:* (`reviews/s193-high.md` findings 8/8 read, `reviews/s193-medium.md` findings 4/4 read) high 1-4 and 8 applied; medium 3 applied by this docs pass.
- Skipped #5 (`reviews/s193-high.md`): "takes the first non-empty line of the Short description section with no check that it is real text" - low, no seen failure with our files.
- Skipped #6 (`reviews/s193-high.md`): "a stray label-style line" overriding the numbered section - low, no seen failure with our files.
- Skipped #7 (`reviews/s193-high.md`): "permanent special case" of per-app `languagesFor` plus a second parser layout - low design note, no seen failure.
- Skipped #1 (`reviews/s193-medium.md`): "a trailing "(...)" is stripped from every term" (so a term like "text zu sprache (tts)" loses its note) - low, no seen failure with our files.
- Skipped #2 (`reviews/s193-medium.md`): "labelValue(...) ?? section is positional" - low, no seen failure with our files.
- Skipped #4 (`reviews/s193-medium.md`): "pre-publish check pipes output to Out-Null" - low, pre-existing line, no seen failure.
