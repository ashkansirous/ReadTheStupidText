# Slice 74 — Dino Reader site: landing, privacy and downloads on dinoreader.sirous.uk

*In progress (wave 2026-10-21), draft PR.*

**Batch:** 10 (Dino Reader)
**Depends on:** Slice 68 (the landing Worker), Slice 69 (APK downloads from R2)
**Touches:** `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `worker/index.ts`, `worker/downloads.ts`, `worker/download-keys.mjs`, `worker/*.test.mjs`, `worker/check-configs.mjs`, `worker/check-site-eol.mjs`, `site/dino/**` (new), `.github/scripts/publish-downloads.mjs`, `.github/workflows/release.yml` (publish-downloads step), `docs/DEPLOYING.md`
**Story issue:** —

## Goal

Dino Reader has a home on the web at `https://dinoreader.sirous.uk`: a landing page in its
own design, a privacy policy written for a children's app, and a direct Android download.
Play requires a privacy policy URL before Dino Reader can leave internal testing, and the
Families policy reviews kids' apps more strictly. Until now only
`readthestupidtext.sirous.uk` exists. It serves `site/index.html` and `site/privacy.html`
for Read The Stupid Text, and `dinoreader.sirous.uk` doesn't resolve.

## Findings (2026-10-21)

- The Worker (`wrangler.jsonc`, `worker/index.ts`) has one custom domain
  (`readthestupidtext.sirous.uk`). It serves static assets from `./site` with
  `html_handling: "none"`, and maps `/`, `/privacy` and `/download/*` through
  `run_worker_first` (`PAGE_FOR_PATH`, `isDownloadPath`) so every page gets the security
  headers.
- APK downloads (Slice 69) serve `android/<version>/ReadTheStupidText-<version>.apk` from
  the R2 bucket `readthestupidtext-downloads`, with a "latest" link. The key layout lives
  only in `worker/download-keys.mjs`, shared with `.github/scripts/publish-downloads.mjs`.
  Nothing publishes Dino Reader's APK yet, although `release.yml` already uploads
  `DinoReader-<v>.apk` to the GitHub Release.
- Dino Reader's design (colours, fonts Fredoka/Andika, "Book hug" icons) is in
  `docs/design/dino-reader/`. Read visual values from there, never guess them.
- Dino Reader is in Play Console as of 2026-10-21 (internal testing, app
  `4974567301865424326`). Its store listing is still empty.

## Decision

**One Worker, two hosts.** Add `dinoreader.sirous.uk` as a second custom domain on the
existing Worker, and choose the page set by `Host`. One deploy, one `site-checks.yml`, one
R2 bucket (key prefix per product). Rejected: a second Worker with its own folder, which
keeps the products apart but doubles the deploy, the CI checks and the secrets.

## Scope

- [ ] `wrangler.jsonc`: add `{ "pattern": "dinoreader.sirous.uk", "custom_domain": true }`.
      `sirous.uk` is already a zone on the account, so Cloudflare creates the DNS record.
      Keep `workers_dev` as the fallback; give it a way to reach the Dino pages (e.g. a
      `/dino/` path prefix on `*.workers.dev` only) so the pre-cut-over check still works.
- [ ] `worker/index.ts`: pick the site by host (`readthestupidtext.sirous.uk` → `site/`,
      `dinoreader.sirous.uk` → `site/dino/`). The host names are named constants, not
      inline strings. An unknown host gets the Read The Stupid Text site, as today. Same
      security headers on both. Unit-test the host routing beside the existing worker tests.
- [ ] `site/dino/index.html`: landing page in the Dino Reader design. What it is (a book
      reader for kids, a voice per character), the "Book hug" icon from
      `docs/design/dino-reader/icons/` (copied, never redrawn), a **Get it on Google Play**
      link (only once the app is public, until then the direct download), and the direct
      Android download.
- [ ] `site/dino/privacy.html`: privacy policy for a children's app. Nothing is collected,
      no accounts, no ads, no analytics, no third-party SDKs that collect data. Stories,
      photos and scans stay on the device and are processed offline (OCR, translation,
      speech). The only network use is downloading voice and language packs from GitHub
      Releases, with no personal data sent. Contact address. Effective date. Match the
      tone and structure of `site/privacy.html`.
- [ ] Downloads: add a Dino Reader key layout to `worker/download-keys.mjs`
      (`dinoreader/android/<version>/DinoReader-<version>.apk` + latest) and serve it at
      `dinoreader.sirous.uk/download/android` (+ `/latest`). Extend
      `publish-downloads.mjs` and its `release.yml` step to upload the Dino APK when the
      `dinoapp` target shipped, carrying the previous one forward otherwise. Same bucket.
- [ ] `worker/check-configs.mjs` / `check-site-eol.mjs` cover the new files and route.
- [ ] `docs/DEPLOYING.md`: the second domain, how to check it on `workers.dev` first.
- [ ] After deploy, the user pastes `https://dinoreader.sirous.uk/privacy` into Play
      Console (App content, Privacy policy). Claude checks the live URLs first.

## Done when

- `https://dinoreader.sirous.uk/` and `/privacy` load with the security headers, and
  `readthestupidtext.sirous.uk` is unchanged (same pages, same download links).
- `https://dinoreader.sirous.uk/download/android/latest` downloads the latest signed Dino
  Reader APK after the next release that ships Dino Reader.
- Worker unit tests cover host routing and the Dino download keys; `site-checks.yml` and
  `release-scripts.yml` are green.

## Version

Docs and site only for the apps, so the app version isn't affected by the site itself.
The `publish-downloads` and Worker changes are tooling: patch, no marker.
