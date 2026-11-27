# Deploying the landing site

`https://readthestupidtext.sirous.uk/` is a Cloudflare Worker (`readthestupidtext`)
that serves the static files in `site/` (Slice 68). The Windows and Android apps are
unaffected: the landing page and the privacy page live here. APKs are not served from here; they stay on GitHub Releases, and `/download` redirects to the platforms section.

| URL | Served from |
| --- | --- |
| `/`, `/index.html` | `site/index.html` |
| `/privacy`, `/privacy.html` | `site/privacy.html` |
| `/download`, `/download/*` | 302 to `/#platforms` (the Play, TestFlight and Store links) |
| anything else | plain 404 |

`/privacy.html` stays a real, non-redirecting URL because the Store listings link it.

## Two hosts, one Worker (Slice 74)

The same Worker also serves `https://dinoreader.sirous.uk/` (a second custom domain in
`wrangler.jsonc`). The `Host` header picks the site: `dinoreader.sirous.uk` serves
`site/dino/`, anything else serves the Read The Stupid Text files above. The host names
are constants (`Host` in `worker/downloads.ts`, routing in `resolveSite`).

**Third host (Slice 467):** `https://dino.elinsoft.co.uk/` is a third custom domain in `wrangler.jsonc` (`elinsoft.co.uk` is already a Cloudflare zone on this account, so the release pipeline creates the DNS record; never a shell deploy) and serves the same Dino site as `dinoreader.sirous.uk`: `DINO_HOSTS`/`isDinoHost` in `worker/download-keys.mjs` are the one list, and `check-configs` fails when either custom domain is missing. The Sonos integration's host. Its Reporting Endpoint is `https://dino.elinsoft.co.uk/sonos/v2` (Sonos portal, an owner step): `POST /sonos/v2/timePlayed` and `/sonos/v2.0/timePlayed` count plays, and `GET`/`HEAD` on `/sonos/v2` or `/sonos/v2.0` (with or without a trailing slash) answer 200 with an empty body for the portal's reachability check.

| URL on `dinoreader.sirous.uk` | Served from |
| --- | --- |
| `/`, `/index.html` | `site/dino/index.html` |
| `/privacy`, `/privacy.html` | `site/dino/privacy.html` (the Play Console privacy policy URL) |
| `/download`, `/download/*` | 302 to `/#platforms` |
| `/dino/icon.png` | `site/dino/icon.png` (the "Book hug" icon, copied from `docs/design/dino-reader/icons/android/play-store-512.png`) |
| `/img/control-panel.webp` (on `readthestupidtext.sirous.uk`) | `site/img/control-panel.webp` (the hero screenshot of the Windows control panel) |
| `/dino/shot-reading.webp` (on `dinoreader.sirous.uk`) | `site/dino/shot-reading.webp` (the hero screenshot of the reading screen) |

**Check it on `workers.dev` first.** There the Dino site is under a `/dino/` prefix:
`https://readthestupidtext.<account-subdomain>.workers.dev/dino/`, `/dino/privacy` and
`/dino/download` (only on `*.workers.dev`; on the real hosts the prefix is not
special). `/dino/download` redirects to `/dino/#platforms`. Locally, `npm run dev` and `curl -H "Host: dinoreader.sirous.uk" localhost:8787/`
shows the Dino pages. Both `wrangler*.jsonc` files list the `/dino/...` paths in
`assets.run_worker_first`; `npm run typecheck` fails if either lacks them.

**Attaching the domain.** `sirous.uk` is already a zone on the account, so the deploy
creates the DNS record. If a record named `dinoreader` already exists, Cloudflare refuses
(error 100117, as in the cut-over below): delete it first. On 2026-10-21 `dinoreader.sirous.uk`
returned NXDOMAIN from 1.1.1.1 and 8.8.8.8, so there is none to remove. The Workers Builds
watch paths already cover `worker/**`, `site/**` and `wrangler.jsonc`.

**After the first deploy** the user pastes `https://dinoreader.sirous.uk/privacy` into Play
Console (Dino Reader, App content, Privacy policy). Fetch `/` and `/privacy` first and check
the three security headers.

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

## The downloads bucket

The private R2 bucket `readthestupidtext-downloads` (binding `DOWNLOADS`) holds the Dino story
store, the remote config files and the usage-count flag (`worker/stories.ts`, `appconfig.ts`,
`events.ts`; key layout in `worker/download-keys.mjs`). It is no longer written with APKs: CI does not copy
them and the Worker answers `/download` and
`/download/*` with a 302 to `#platforms`. Release APKs are on the GitHub Release. Old `android/` and `dinoreader/android/` APKs and `manifests/android/*.json` remain in the bucket, but nothing serves them. `wrangler deploy`
refuses a binding to a bucket that does not exist, so create it before the deploy that adds the
binding: `npx wrangler r2 bucket create readthestupidtext-downloads` (needs `wrangler login`; leave
public access off). The `publish-store` job in `dino-store.yml` writes to it with the GitHub secrets
`CLOUDFLARE_API_TOKEN` (Account > Workers R2 Storage > Edit) and `CLOUDFLARE_ACCOUNT_ID`.
Tests: `node --test .github/scripts/*.test.mjs` runs in `release-scripts.yml` and
`node --test worker/*.test.mjs` in `site-checks.yml`.

## Two configs

- `wrangler.jsonc`: the real config, with the `readthestupidtext.sirous.uk` and
  `dinoreader.sirous.uk` custom-domain routes.
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
1. **Detach the custom domain from the Worker.** (Dino Reader's `dinoreader.sirous.uk` is
   detached the same way, independently.) Dashboard: Workers & Pages ->
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

## The Dino usage dashboard: D1 database and secret (Slice 287)

`/stats` (public, no sign-in) reads the D1 table `daily_stats`, which the hourly cron (`worker/stats-rollup.ts`,
`triggers.crons` in both wrangler configs) fills from Analytics Engine. With no
`STATS` binding the page's API answers 503 `data_unavailable` and the cron logs `stats rollup skipped` and returns.

**Done 2026-11-08** (PR #950): database `dino-stats` (id `e7fe3a48-af07-4331-ab1c-15882b802599`) created and its schema
applied, secret `CF_ANALYTICS_READ_TOKEN` set, the `STATS` binding and the `ANALYTICS_ACCOUNT_ID` var are in both configs,
and the Worker is deployed. The steps below are only for rebuilding it (a new account or a deleted database). Keep the
account id as the var; don't also set it as a secret (a secret and a var with one name clash). Setup,
once, from the desk (`wrangler login` needs `d1 (write)` and `workers_scripts (write)`):

1. `npx wrangler d1 create dino-stats` (prints the `database_id`).
2. `npx wrangler d1 execute dino-stats --remote --file worker/schema/stats.sql`
3. Add this block to **both** `wrangler.jsonc` and `wrangler.workers-dev.jsonc` (the same text in each; `check-configs` fails otherwise), next to `r2_buckets`, with the real id:
   `"d1_databases": [{ "binding": "STATS", "database_name": "dino-stats", "database_id": "<id from step 1>" }],`
4. `npx wrangler secret put CF_ANALYTICS_READ_TOKEN` (the read-only Analytics Engine token, `D:\secrets`, `manage-secrets`).
5. The account id: add `"ANALYTICS_ACCOUNT_ID": "<account id>"` to `vars` in both configs (the id is not secret).
6. Deploy (`npm run deploy`, or merge: Workers Builds). The first cron run (minute 7 of the hour) backfills every day Analytics Engine still holds; wait for it (`wrangler dev --test-scheduled` runs against a local, empty D1 with no token, so it does not fill the live table).
7. Check: `https://dinoreader.sirous.uk/api/stats?from=2026-11-01&to=2026-11-08` answers 200 with `updatedAt` set, and `/stats` shows the page.

## The Sonos home link (Slice 438)

Every Sonos home links once through the Dino app (`worker/sonos-link.ts`); the homes live in the `STATS` D1 database, tables `sonos_homes` and `sonos_pending_links`
(`worker/schema/sonos-homes.sql`, applied by hand like `stats.sql`). With no `STATS` binding, or while the tables are missing, every Sonos call faults (`Server.ServiceUnavailable`): the service
fails closed. In this order, once:

1. **Before the merge deploys the Worker:** run `npx wrangler d1 execute dino-stats --remote --file worker/schema/sonos-homes.sql` (safe to run twice; `wrangler login` needs `d1 (write)`).
2. Merge (Workers Builds deploys). From then on the anonymous Slice 283 calls (no login token) fault with `Client.AuthTokenExpired`, so the sandbox integration must be switched at once.
3. In the Sonos developer portal, set the sandbox integration's authentication to **AppLink** (same SMAPI endpoint `https://dinoreader.sirous.uk/sonos`; no extra URL fields: the Worker answers `getAppLink` with the Dino link page and the `dinoreader://sonos-link` app link). Then rerun the Sonos Self Test.
4. Ship the Dino app build that carries the Link screen (the app link `dinoreader://sonos-link?code=...` is handled from that build on); a phone without it is sent to its store page.
5. Check from the S21: add the service in the Sonos app, the Dino app opens on the Link screen, Connect, back in Sonos the root shows "Story language: ...".

The page Sonos opens is `https://dinoreader.sirous.uk/dino/sonos-link.html` (a static file; no Worker route and no `run_worker_first` entry). No secret and no new binding are needed.

### The subscription proof column and the spoken message (Slice 439)

A linked home that holds a Dino subscription plays the paid books; the Worker asks the store with the proof the app sent, so the row keeps it in a `proof` column. In this order, once:

1. **Before the merge deploys the Worker, on the database where the Slice 438 tables already exist** (a fresh database gets the column from `worker/schema/sonos-homes.sql`): run both lines, each once (a second run says "duplicate column name", which is fine):
   `npx wrangler d1 execute dino-stats --remote --command "ALTER TABLE sonos_homes ADD COLUMN proof TEXT"` and
   `npx wrangler d1 execute dino-stats --remote --command "ALTER TABLE sonos_pending_links ADD COLUMN proof TEXT"`.
   The Worker reads a home without the column too (the home then has no proof and its paid books play the message, logged as `fallback=no-proof-column`), so a deploy that runs first breaks nothing, but linking a home with a subscription needs the column: apply it first.
   Optional, once: `npx wrangler d1 execute dino-stats --remote --command "CREATE INDEX IF NOT EXISTS sonos_homes_token ON sonos_homes (token_hash)"` (the paid-book media route looks a home up by its token hash; without the index it scans the table).
2. Upload the 15 spoken "subscribe in the Dino app" clips to R2 as `dinoreader/sonos/subscribe-<language>.m4a` (languages: `en ar da de es fa fi fr ja nb nl pt ru sv zh`; AAC in an `.m4a`, the apps' narrator voice, text in `SUBSCRIBE_MESSAGES` in `worker/sonos-labels.ts`). A language with no clip answers 404 on `/sonos/subscribe/<language>.m4a`, so a paid book of that language plays nothing instead of the message.
3. Homes linked before this slice have no proof: their paid books play the message until the grown-up links again.

## Deploying from Windows

A Windows checkout with `core.autocrlf=true` writes `site/` with CRLF line endings, which would upload different bytes than git (and than Workers Builds on Linux). `npm run deploy` runs `worker/check-site-eol.mjs` first and refuses; fix with `git -c core.autocrlf=false checkout -- site/`.
