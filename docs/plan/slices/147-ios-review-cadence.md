# Slice 147 — iOS review cadence: one review at a time, the newest build, never cancel

*Built (2026-10-26). Not yet verified: first scheduled run and the first App Store submission.*

**Batch:** none (pipeline, Decision 154)
**Touches:** `.github/scripts/ios-review-cadence.mjs`, `.github/scripts/ios-review-cadence.test.mjs`, `.github/workflows/testflight-external.yml`, `.github/workflows/appstore-submit.yml`, `.github/workflows/release-scripts.yml`, `.github/scripts/testflight-upload.sh` (comment only), `.github/CLAUDE.md`, `docs/plan/decisions.md`

## Why

Every merge that touches iOS uploads a build to TestFlight (Slices 48, 67). The internal "Family" group gets
each one at once with no review. External testers (group "Rofagha", public link) and the App Store need
Apple's review, and so far that was done by hand: only three builds ever went to external testers, and
neither app was ever submitted to the App Store (checked 2026-10-26 via the API). The user does not want
Apple to review every change, and cancelling a waiting review to swap in a newer build would starve: each
cancel goes to the back of Apple's queue.

## Decisions (locked with the user, 2026-10-26)

- **Decision 154:** iOS reviews follow a fixed cadence, the same for every iOS app the user owns:
  - **Never cancel** a waiting review and never expire a build to make room. A newer build waits for the next run.
  - **At most one review waiting per app.** If one is waiting or in review, the run does nothing for that app.
  - **Always the newest processed build** (`VALID`, not expired), so whatever passes is the latest at that moment.
  - **TestFlight external testers: daily, automatic** (`testflight-external.yml`, cron 05:17 UTC + 07:17 backup since 2026-10-28, plus manual
    dispatch). Adds the newest build to the external group and submits it for Beta App Review, unless it is
    already in the group or already submitted.
  - **App Store: manual button** (`appstore-submit.yml`, `workflow_dispatch`, input `app`: `rtst`, `dino` or
    `both`). Picks the newest build, makes the editable App Store version carry that build's marketing version
    (renames it, or creates a new version when the last one is live), attaches the build and submits it.
    Release type stays "after approval". The first submission's listing (screenshots, privacy, age rating) is
    done by the user once in App Store Connect; until then the job reports what Apple says is missing and fails.
  - Internal "Family" testers are unchanged: every merge, no review.
  - Supersedes Decision 70's "adding the build to a tester group stays manual".

**Amendment 2026-10-28:** the TestFlight busy gate was removed at the user's direction. The rule is batching, not one review at a time: each morning the newest build is submitted even while an older review waits (never cancelled, never expired). A TestFlight 409 is now a failure carrying Apple's text. The crons moved off the hour (05:17 UTC, backup 07:17). The App Store button still skips while a review is in progress.

## Design

- One Node script, no dependencies, `node:test` tests (run by `release-scripts.yml` with the others).
  ASC client and JWT adapted from Lets-Call-Mom's `.github/scripts/testflight-distribute.mjs`
  (fresh token per request, HTML error bodies, transient vs fatal errors, 409 = "not now" returned as data).
- Apps are a constant table: RTST `6825052929`, Dino `6825053435`, external group name `Rofagha`
  (named constant; a missing group fails loudly).
- Modes: `node ios-review-cadence.mjs testflight <app...>` and `node ios-review-cadence.mjs appstore <app...>`.
  Each app is handled independently; one app's failure does not skip the other, but fails the job at the end.
- Each run writes one line per app to `GITHUB_STEP_SUMMARY`: submitted (version + build), skipped (why:
  review already waiting / newest already submitted / no processed build) or failed (Apple's error text).
- Secrets: the existing `ASC_API_KEY_P8_BASE64`, `ASC_API_KEY_ID`, `ASC_API_ISSUER_ID` (App Manager key).
- `usesNonExemptEncryption`: both Info.plists set `ITSAppUsesNonExemptEncryption`, so nothing to set via API.

### App Store Connect API calls (checked against Apple's docs, 2026-10-26)

- Builds: `GET /v1/builds?filter[app]=<id>&sort=-uploadedDate&limit=50&include=preReleaseVersion,buildBetaDetail,betaGroups,betaAppReviewSubmission`
  (`processingState`: `PROCESSING|FAILED|INVALID|VALID`; attribute `expired`). Marketing version is the
  included `preReleaseVersions.attributes.version` (e.g. `1.54.1`); `attributes.version` is the build number.
- Beta review: `GET /v1/betaAppReviewSubmissions?filter[build]=<id,id,...>&limit=200` — **`filter[build]` is
  required**, there is no per-app filter, so pass the ids of the recent builds listed above. States
  `WAITING_FOR_REVIEW|IN_REVIEW|REJECTED|APPROVED`. No cancel/delete exists. Submit:
  `POST /v1/betaAppReviewSubmissions` `{data:{type:'betaAppReviewSubmissions',relationships:{build:{data:{type:'builds',id}}}}}`
  (409 = not now → skip). Add to group: `POST /v1/betaGroups/<gid>/relationships/builds` `{data:[{type:'builds',id}]}`.
  Group: `GET /v1/apps/<id>/betaGroups`, match `attributes.name`.
- App Store submissions: `GET /v1/reviewSubmissions?filter[app]=<id>&filter[platform]=IOS` (`state`:
  `READY_FOR_REVIEW|WAITING_FOR_REVIEW|IN_REVIEW|UNRESOLVED_ISSUES|CANCELING|COMPLETING|COMPLETE`). Busy =
  `WAITING_FOR_REVIEW|IN_REVIEW|UNRESOLVED_ISSUES|CANCELING|COMPLETING`. A leftover `READY_FOR_REVIEW` (draft,
  never submitted) is reused rather than creating another.
  Create `POST /v1/reviewSubmissions` `{data:{type:'reviewSubmissions',attributes:{platform:'IOS'},relationships:{app:{data:{type:'apps',id}}}}}`;
  item `POST /v1/reviewSubmissionItems` `{data:{type:'reviewSubmissionItems',relationships:{reviewSubmission:{data:{type:'reviewSubmissions',id}},appStoreVersion:{data:{type:'appStoreVersions',id}}}}}`;
  submit `PATCH /v1/reviewSubmissions/<id>` `{data:{type:'reviewSubmissions',id,attributes:{submitted:true}}}`. The
  script never sends `canceled:true`.
- Versions: `GET /v1/apps/<id>/appStoreVersions?filter[platform]=IOS&include=build` (`appVersionState`). Editable:
  `PREPARE_FOR_SUBMISSION|DEVELOPER_REJECTED|REJECTED|METADATA_REJECTED|INVALID_BINARY`. If one editable version
  exists: `PATCH /v1/appStoreVersions/<id>` `attributes.versionString` = build's marketing version (skip when equal),
  then `PATCH /v1/appStoreVersions/<id>/relationships/build` `{data:{type:'builds',id}}`. If none is editable (last
  is live/accepted): `POST /v1/appStoreVersions` `{data:{type:'appStoreVersions',attributes:{platform:'IOS',versionString,releaseType:'AFTER_APPROVAL'},relationships:{app:{data:{type:'apps',id}}}}}`.
  Skip (not fail) when the newest build is already the one on the newest version that is waiting/in review/live.
- Today's state: both apps have a `1.0` version in `PREPARE_FOR_SUBMISSION` with no build, no review submissions;
  newest builds are marketing `1.54.1`.

## Work

- [x] `ios-review-cadence.mjs` + tests (pure decision functions tested with a fake client).
- [x] `testflight-external.yml` (schedule + dispatch, concurrency group, ubuntu, node 22).
- [x] `appstore-submit.yml` (dispatch with `app` choice).
- [x] `release-scripts.yml` path filter covers the new files; `.github/CLAUDE.md` rule; Decision 154.
- [ ] After merge: dispatch `testflight-external.yml` once and check both apps' state.

## Built

- Reviews: high pass (12 findings, 10 applied; kept: cron fails loudly without secrets, an existing version's
  release type is left as set in App Store Connect) and medium pass (8 findings, 3 applied: `include=build` on beta
  submissions, `filter[build]` in chunks of 50, `limit=200` on versions). The stale "group add is manual" comments in
  `release.yml` and `testflight-upload.sh` stay until a change touches those files anyway, because editing either
  rebuilds every platform.
- Live dry run (GETs only, 2026-10-26): both apps' newest build 1.54.1 is in "Family" only, so the TestFlight run
  adds it to "Rofagha" and submits it; the App Store run renames version 1.0 to 1.54.1 and attaches that build.
- Not yet verified: the first real `testflight-external.yml` run, and the first `appstore-submit.yml` run (expected to
  report the listing items Apple still needs: screenshots, privacy, age rating).
