# Deploying the landing site

`https://readthestupidtext.sirous.uk/` is a Cloudflare Worker (`readthestupidtext`)
that serves the static files in `site/` (Slice 68). The Windows and Android apps are
unaffected: the landing page, the privacy page and the Android APK downloads live here.

| URL | Served from |
| --- | --- |
| `/`, `/index.html` | `site/index.html` |
| `/privacy`, `/privacy.html` | `site/privacy.html` |
| `/download` | page: newest Android version, "Download for Android", Store link (Slice 69) |
| `/download/android/latest` | the newest APK, streamed from R2 (Range supported) |
| `/download/android/<x.y.z>` | that version's APK; 404 if it was never published |
| anything else | plain 404 |

`/privacy.html` stays a real, non-redirecting URL because the Store listings link it.

## How it deploys

Until Workers Builds is connected (step 5 of the cut-over below), the site deploys from
the desk with `npm run deploy` (needs `wrangler login`). Once it is connected, Cloudflare
**Workers Builds** runs `npx wrangler deploy` on every push to `main`. There is no GitHub
Actions deploy.

Dashboard settings for Workers Builds (Workers & Pages -> `readthestupidtext` ->
Settings -> Builds):

- Git repository: `ashkansirous/ReadTheStupidText`, production branch `main`.
- Build command: empty. Deploy command: `npx wrangler deploy`. Root directory: `/`.
- **Build watch paths** (include): `worker/**`, `site/**`, `wrangler.jsonc`,
  `package.json`, `package-lock.json`. A push that only touches `src/**` or the
  other docs does not trigger a build.

Local commands: `npm install`, `npm run dev` (local preview), `npm run typecheck`,
`npm run types` (regenerate `worker-configuration.d.ts` after editing `wrangler.jsonc`;
the file is generated but committed so typecheck works without a Cloudflare login),
`npm run deploy` (deploy with the real config), `npm run deploy:workers-dev` (deploy
without the custom domain, see below).

Every response the Worker returns carries `X-Content-Type-Options: nosniff`,
`Referrer-Policy: strict-origin-when-cross-origin` and `X-Frame-Options: DENY`. The
Worker runs for the paths in `assets.run_worker_first` (`/`, `/index.html`, `/privacy`,
`/privacy/`, `/privacy.html`, `/download`, `/download/*`), which are every page that exists. Unlisted paths such as
`/nope` are answered by the assets layer as a plain 404 without those headers; that does
not matter because a 404 carries no content.

## The Android downloads bucket (Slice 69)

Every signed release APK is copied to the private R2 bucket `readthestupidtext-downloads`;
the Worker (`worker/downloads.ts`, binding `DOWNLOADS`) is the only reader and never talks
to GitHub. Windows stays Store-only (an MSIX needs a trusted certificate to sideload).
GitHub Releases remain the build store; R2 is an extra public copy.

Layout (`worker/downloads.ts` and `.github/scripts/publish-downloads.mjs` must agree):
`android/<version>/ReadTheStupidText-<version>.apk` and `manifests/android/<version>.json`
(`{version, fileName, sizeBytes, publishedAt}`). CI uploads the APK, then the manifest
last; the Worker offers a version only once its manifest exists, and "latest" is the
highest manifest version, so re-running an old build never moves the pointer back.
Nothing prunes old versions.

One-time setup (the order matters: `wrangler deploy` refuses a binding to a bucket that
does not exist, so create the bucket **before** the deploy that adds the binding):

1. `npx wrangler r2 bucket create readthestupidtext-downloads` (needs `wrangler login`;
   leave public access off).
2. Create a Cloudflare API token with **Account > Workers R2 Storage > Edit** (nothing
   else), store it through the `manage-secrets` skill, and set two GitHub Actions secrets
   (not Worker secrets): `gh secret set CLOUDFLARE_API_TOKEN` and
   `gh secret set CLOUDFLARE_ACCOUNT_ID`.
3. Deploy the Worker (`npm run deploy`, or merge to `main` with Workers Builds connected).

`release.yml`'s `publish-downloads` job runs `.github/scripts/publish-downloads.mjs` after
`release-publish` has published the GitHub Release (so only on `main`, and only when
Android was rebuilt). It reads the signed APK back from that published release, so it holds
no signing secrets, and nothing lists it in `needs`, so a bucket or token problem shows up
red there without blocking `store-submit`, `play-submit` or `testflight-submit`. Without
`CLOUDFLARE_API_TOKEN` it logs "skipped" and exits 0. A version already in the bucket is
never overwritten ("already published"). Check a release at `/download` (the new version
shows within a minute: the Worker caches the bucket listing) and by installing
`/download/android/latest` on a phone. Tests: `node --test .github/scripts/publish-downloads.test.mjs`
runs in `release-scripts.yml` (with every other `.github/scripts/*.test.mjs`) and
`node --test worker/downloads.test.mjs` in `site-checks.yml`; neither is in any job's `needs`.

Limits: `wrangler r2 object put` caps an upload at 300 MiB; the APK must stay under that.

## Two configs

- `wrangler.jsonc`: the real config, with the `readthestupidtext.sirous.uk` custom-domain
  route.
- `wrangler.workers-dev.jsonc`: the same Worker with **no `routes`**. Deploying it
  (`npm run deploy:workers-dev`) publishes the Worker on `workers.dev` and never touches
  the hostname. Keep the two files identical apart from the route; it exists only for
  the first step of the cut-over. `npm run typecheck` runs `worker/check-configs.mjs`,
  which fails if the two files differ in anything but `routes`.

`workers_dev` is `true` in both, so
`https://readthestupidtext.<account-subdomain>.workers.dev` always answers, even if the
custom domain is misconfigured. Use it to check a deploy.

## Cut-over from GitHub Pages (once, from the desk, before merging)

**Note (verified 2026-10-20):** Cloudflare will **not** attach a Worker custom domain
while the hostname has an externally managed DNS record. `wrangler deploy` (even
non-interactive, despite its `override_existing_dns_record` flag) and the Workers Domains
API both fail with error 100117. So the old CNAME to `ashkansirous.github.io` must be
deleted in the dashboard (DNS → Records) first. The wrangler OAuth login has no DNS
scope, so it can't do that step. Deleting it takes the site offline until the next
deploy attaches the domain, which takes seconds; HTTPS answered within ~10 s.

1. **Deploy without the domain.** On this branch run `npm ci`, then
   `npm run deploy:workers-dev` (= `npx wrangler deploy --config wrangler.workers-dev.jsonc`).
   If the account has no `workers.dev` subdomain yet, wrangler prompts for one: run this
   first deploy in an interactive terminal, or register the subdomain in the dashboard
   first (Workers & Pages -> Account details).
   Verify `https://readthestupidtext.<account-subdomain>.workers.dev`: `/`, `/index.html`,
   `/privacy`, `/privacy.html` return 200 with the security headers; `/nope` returns 404.
2. **Switch the hostname.** Delete the `readthestupidtext` CNAME in the Cloudflare
   dashboard (DNS → Records), then immediately run `npm run deploy` (= the CRLF check +
   `npx wrangler deploy`, real config), which attaches the custom domain. Verify
   `https://readthestupidtext.sirous.uk/` and `/privacy.html`: the response has a
   `cf-ray` header and no `server: GitHub.com`, and the bodies are byte-identical to the
   Pages copies. Compare against the committed LF blobs on `origin/main`, **not** the
   working-tree files (on Windows those are CRLF and hash differently):
   ```bash
   curl -s https://readthestupidtext.sirous.uk/ | sha256sum
   git show origin/main:docs/index.html | sha256sum
   curl -s https://readthestupidtext.sirous.uk/privacy.html | sha256sum
   git show origin/main:docs/privacy.html | sha256sum
   ```
   Each pair of hashes must match. Expect a short gap while the TLS certificate is issued.
3. **Disable GitHub Pages** (the hostname already points at the Worker, so nothing
   visible changes). Do this *before* the merge, otherwise deleting `docs/.nojekyll` and
   `docs/index.html` on `main` triggers a junk Jekyll build:
   `gh api -X DELETE repos/ashkansirous/ReadTheStupidText/pages`
4. **Merge the Slice 68 PR.** It removes `docs/index.html`, `docs/privacy.html`,
   `docs/CNAME` and `docs/.nojekyll`.
5. **Connect Workers Builds** (settings above) for future deploys. From then on a push to
   `main` that touches the watch paths deploys the site.

## Rollback (in this order)

0. **Stop CI from re-attaching the domain**, before touching DNS. Workers Builds runs
   `wrangler deploy` on every push and would re-take the hostname. Either disconnect it
   (dashboard: Workers & Pages -> `readthestupidtext` -> Settings -> Builds ->
   Disconnect) or first commit and merge the removal of the `routes` entry from
   `wrangler.jsonc` (the config check ignores `routes`, so the two files stay valid).
1. **Detach the custom domain from the Worker.** Dashboard: Workers & Pages ->
   `readthestupidtext` -> Settings -> Domains & Routes -> the `readthestupidtext.sirous.uk`
   custom domain -> Remove. Deleting the `routes` entry and redeploying is **not**
   enough: wrangler warns that previously deployed routes "have not been deleted" and
   leaves them, so the domain must be detached explicitly. Via the API instead: list with
   `GET /accounts/<account-id>/workers/domains?hostname=readthestupidtext.sirous.uk`, then
   `DELETE /accounts/<account-id>/workers/domains/<domain-id>`. Removing it also removes
   the DNS record Cloudflare created for it.
2. **Re-add the DNS-only CNAME** in the `sirous.uk` zone: name `readthestupidtext`,
   target `ashkansirous.github.io`, proxy status **DNS only** (grey cloud).
3. **Restore the Pages files** from the commit before the Slice 68 merge (replace
   `<merge-sha>` with the merge commit of the Slice 68 PR):
   ```bash
   git checkout <merge-sha>^ -- docs/index.html docs/privacy.html docs/CNAME docs/.nojekyll
   git commit -m "revert: restore GitHub Pages site"
   ```
   Open it as a PR against `main` as usual.
4. **Re-enable Pages** from `main:/docs` with the custom domain:
   ```bash
   gh api -X POST repos/ashkansirous/ReadTheStupidText/pages      -f "source[branch]=main" -f "source[path]=/docs"
   gh api -X PUT repos/ashkansirous/ReadTheStupidText/pages      -f cname=readthestupidtext.sirous.uk
   ```
   Step 4 needs the restored files on `main`, so merge the PR from step 3 first; wait for
   the certificate, then re-enable "Enforce HTTPS" in repo Settings -> Pages.

## Deploying from Windows

A Windows checkout with `core.autocrlf=true` writes `site/` with CRLF line endings, which would upload different bytes than git (and than Workers Builds on Linux). `npm run deploy` runs `worker/check-site-eol.mjs` first and refuses; fix with `git -c core.autocrlf=false checkout -- site/`.
