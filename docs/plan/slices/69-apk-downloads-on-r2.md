# Slice 69 — Android APK downloads from R2, with a "latest" link

*Built (wave 2026-10-20c), PR #187.*

**Batch:** 11 (Lets-Call-Mom parity)
**Depends on:** Slice 68 (the Worker)
**Touches:** `.github/scripts/publish-downloads.mjs`, `.github/scripts/publish-downloads.test.mjs`, `.github/workflows/build.yml`, `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `worker-configuration.d.ts`, `worker/**` (incl. `worker/download-keys.mjs`, `worker/download-keys.d.mts`, `worker/downloads.ts`, `worker/downloads.test.mjs`), `site/**`, `docs/DEPLOYING.md`, `README.md`, `CLAUDE.md`
**Story issue:** —

## Goal

Every release build uploads the signed Android APK to a private R2 bucket. The Worker serves
`readthestupidtext.sirous.uk/download`: a page with the current version, a "Download for
Android" button that always points at the latest APK, and a Microsoft Store button for
Windows. Users get a stable link that never needs a GitHub account or a release page.

## Decisions (settled 2026-10-20)

- **Android APK only on R2.** Windows stays Store-only: a downloaded MSIX won't install
  without a trusted signing certificate. The Windows button links to the Store listing.
- **GitHub Releases stay** the build store that `store-submit.yml` and the carry-forward
  logic read. R2 is an extra public copy, not a replacement.

## Scope

- [x] R2 bucket `readthestupidtext-downloads`, private, bound to the Worker as `DOWNLOADS`.
- [x] Port Lets-Call-Mom's `.github/scripts/publish-downloads.mjs` and its tests. It uploads
      the file under a versioned key, then writes the per-platform manifest last. It never
      overwrites a live version, and it skips with a warning (exit 0) when
      `CLOUDFLARE_API_TOKEN` is unset.
- [x] Call it from the Android release path after the APK is signed (in `build.yml` for now;
      Slice 70 moves it into `android.yml`).
- [x] Worker routes: `/download` (page reading the manifest), `/download/android/latest`
      (302 or a stream of the current APK, with range support), `/download/android/<version>`.
      Port from Lets-Call-Mom's `worker/downloads.ts` and its tests.
- [x] Secrets: `CLOUDFLARE_API_TOKEN` (scope: Workers R2 Storage Edit) and
      `CLOUDFLARE_ACCOUNT_ID`, recorded through `manage-secrets` and set as GitHub Actions secrets.
- [x] Landing page "Download" buttons point at `/download`.

## Done when

- After a release build, `/download` shows the new version, and `/download/android/latest`
  installs on a real Android phone (sideload allowed).
- A rerun of the same version does not overwrite the uploaded APK.
- Without the token, the build stays green and logs the skip.

## Built

- Private R2 bucket `readthestupidtext-downloads` (created 2026-10-20 by the coordinator), bound as `DOWNLOADS` in both wrangler configs.
- Worker routes: `/download` (HTML page: newest version from the listing, size from the manifest, Download button; "no build published yet" when empty), `/download/android/latest` (no-cache) and `/download/android/<x.y.z>` (max-age 86400). They stream the APK with Content-Type `application/vnd.android.package-archive`, attachment disposition, Accept-Ranges/ETag, single-range 206/416, If-None-Match 304, If-Range (mismatch gives a full 200), HEAD with no body on errors, and JSON error codes.
- Key layout lives in the shared `worker/download-keys.mjs`, used by both the Worker and `.github/scripts/publish-downloads.mjs`.
- New `publish-downloads` job (ubuntu, needs `release-publish`, no signing secrets): downloads the released APK and uploads `android/<v>/ReadTheStupidText-<v>.apk`, then `manifests/android/<v>.json`, with `wrangler r2 object put --remote`. A version already published is skipped.
- New `test-downloads` job: typecheck, the EOL check and 34 node tests.
- Landing page has an "Android — direct download (APK)" button.
- GitHub secrets `CLOUDFLARE_API_TOKEN` (Lets-Call-Mom's R2-edit token, shared, recorded in the secrets store) and `CLOUDFLARE_ACCOUNT_ID` set 2026-10-20.

*Changed while building / From review:*

- High review: shared key module, safe Range forwarding, page version taken from the listing, input validation, typecheck in CI, header constants, wrangler bin cached, redundant ref check removed.
- Medium review: whole-object 200 when R2 omits `range`, robust "not published" detection, upload moved to its own job (no keystore present, visible failure, can't block store submissions), conditional requests, HEAD bodies, honest guarantee wording, error-code constants.

#### For CLAUDE.md

- Android downloads: the Worker serves `/download`, `/download/android/latest` and `/download/android/<x.y.z>` from the private R2 bucket `readthestupidtext-downloads` (binding `DOWNLOADS`; `worker/downloads.ts`). The `publish-downloads` job copies each released APK there after `release-publish` (needs no signing secrets; uses `CLOUDFLARE_API_TOKEN` / `CLOUDFLARE_ACCOUNT_ID`). The key layout lives only in `worker/download-keys.mjs`. A version already published is skipped; the manifest is written last. Windows stays Store-only. A deploy needs the bucket to exist (wrangler fails on a missing R2 binding). (Slice 69: docs/plan/slices/69-apk-downloads-on-r2.md)

#### For README.md

- Android users can install the latest APK directly from https://readthestupidtext.sirous.uk/download (sideloading must be allowed). Windows is Store-only.

#### Not yet verified

- The first real release upload to R2 and `/download/android/latest` serving it.
- Installing that APK on a phone or emulator.
- Workers Builds deploying the Worker with the R2 binding on merge.
