# Slice 69 — Android APK downloads from R2, with a "latest" link

*Planned.*

**Batch:** 11 (Lets-Call-Mom parity)
**Depends on:** Slice 68 (the Worker)
**Touches:** `.github/scripts/publish-downloads.mjs`, `.github/scripts/publish-downloads.test.mjs`, `.github/workflows/build.yml`, `wrangler.jsonc`, `worker/**`, `site/**`, `docs/DEPLOYING.md`, `README.md`, `CLAUDE.md`
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

- [ ] R2 bucket `readthestupidtext-downloads`, private, bound to the Worker as `DOWNLOADS`.
- [ ] Port Lets-Call-Mom's `.github/scripts/publish-downloads.mjs` and its tests. It uploads
      the file under a versioned key, then writes the per-platform manifest last. It never
      overwrites a live version, and it skips with a warning (exit 0) when
      `CLOUDFLARE_API_TOKEN` is unset.
- [ ] Call it from the Android release path after the APK is signed (in `build.yml` for now;
      Slice 70 moves it into `android.yml`).
- [ ] Worker routes: `/download` (page reading the manifest), `/download/android/latest`
      (302 or a stream of the current APK, with range support), `/download/android/<version>`.
      Port from Lets-Call-Mom's `worker/downloads.ts` and its tests.
- [ ] Secrets: `CLOUDFLARE_API_TOKEN` (scope: Workers R2 Storage Edit) and
      `CLOUDFLARE_ACCOUNT_ID`, recorded through `manage-secrets` and set as GitHub Actions secrets.
- [ ] Landing page "Download" buttons point at `/download`.

## Done when

- After a release build, `/download` shows the new version, and `/download/android/latest`
  installs on a real Android phone (sideload allowed).
- A rerun of the same version does not overwrite the uploaded APK.
- Without the token, the build stays green and logs the skip.

## Not yet verified (fill in when built)
