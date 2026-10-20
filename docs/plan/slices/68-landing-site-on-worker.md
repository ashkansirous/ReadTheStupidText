# Slice 68 — Landing site served by a Cloudflare Worker

*Built and live (wave 2026-10-20b), PR #182.*

**Batch:** 11 (Lets-Call-Mom parity)
**Depends on:** —
**Touches:** `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `worker/**`, `worker-configuration.d.ts`, `site/**`, `package.json`, `package-lock.json`, `tsconfig.json`, `.gitignore`, `STORE.md`, `docs/index.html`, `docs/privacy.html`, `docs/CNAME`, `docs/.nojekyll`, `docs/DEPLOYING.md`, `docs/plan/slices/54-on-device-translation.md`, `docs/plan/slices/56-windows-packs-language-headstart.md`, `README.md`, `CLAUDE.md`
**Story issue:** —

## Goal

`readthestupidtext.sirous.uk` is served by a Cloudflare Worker instead of GitHub Pages,
deployed by Cloudflare Workers Builds on every push to `main`, the same way Lets-Call-Mom
serves `talk.sirous.uk`. Visitors see the same landing page and privacy page as today.
This is the host that Slice 69's downloads page plugs into.

## Why

Cloudflare is where downloads will live (Slice 69). One Worker serving the landing page,
`/privacy` and `/download` keeps a single domain and avoids splitting it between GitHub
Pages and Cloudflare.

## Scope

- [x] `wrangler.jsonc` at the repo root: Worker `readthestupidtext`, custom-domain route
      `readthestupidtext.sirous.uk` (the `sirous.uk` zone is already on the account),
      `workers_dev: true` as a fallback, static `assets` from the current landing files.
      Port the shape from Lets-Call-Mom's `wrangler.jsonc`, without the Durable Objects or D1.
- [x] Minimal `worker/index.ts`: serve the assets, answer `/privacy` and `/privacy.html`
      (the Store listing links the `.html` URL, so it must keep working), and 404 on anything else.
- [x] Move the landing files out of `docs/` into the folder the Worker serves (e.g. `site/`),
      keeping URLs unchanged. Remove `docs/CNAME` and `docs/.nojekyll`, then turn GitHub Pages
      off in the repo settings **after** the Worker answers on the domain.
- [ ] Hook Cloudflare Workers Builds up to the repo (dashboard step, guided one step at a
      time), building only on changes to the Worker or the site files.
- [x] `docs/DEPLOYING.md`: how the Worker deploys, the DNS cut-over, the fallback URL.

## Done when

- `https://readthestupidtext.sirous.uk/` and `/privacy.html` return the same pages as today,
  served by the Worker (`cf-ray` header present, no GitHub Pages header).
- A push to `main` touching `site/**` redeploys within minutes. A push touching only
  `src/**` does not.

## Notes

- DNS cut-over order: deploy the Worker on `workers.dev`, check it, then add the custom
  domain. Cloudflare replaces the GitHub Pages CNAME record. Expect a short TLS issuance gap.
- Reference: Lets-Call-Mom `wrangler.jsonc`, `docs/DEPLOYING.md`.

## Built

*Workers Builds is not connected yet: it needs Cloudflare's GitHub app authorised on the repo in the dashboard. Until then, site changes deploy with `npm run deploy` from the desk.*

- Worker `readthestupidtext` (wrangler 4, `compatibility_date` 2026-10-01) serves `./site` with `html_handling: none`, `not_found_handling: none` and `run_worker_first` for every page.
- `worker/index.ts` maps `/` to `index.html`, `/privacy` and `/privacy/` to `privacy.html`, passes `/index.html` and `/privacy.html` through, 404s anything else, and adds `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin` and `X-Frame-Options: DENY` to every page.
- `worker-configuration.d.ts` is generated (`npm run types`).
- `wrangler.workers-dev.jsonc` is the same Worker without routes; `worker/check-configs.mjs` (run in `npm run typecheck`) keeps it in sync with `wrangler.jsonc`.
- `worker/check-site-eol.mjs` runs before both deploy scripts and refuses CRLF site files.

**Cut-over, 2026-10-20 (coordinator):**

1. Deployed to https://readthestupidtext.lets-call-mom.workers.dev and verified status codes, headers and bytes against git.
2. The custom-domain deploy failed while the GitHub CNAME existed (Cloudflare error 100117: wrangler's non-TTY DNS override and the Workers Domains API `override_existing_dns_record` both refuse an externally managed record, and the wrangler OAuth login has no DNS scope). The CNAME was deleted in the Cloudflare dashboard and the redeploy attached `readthestupidtext.sirous.uk`. HTTPS answered in about 10 s. `/`, `/index.html`, `/privacy` and `/privacy.html` return 200, `/nope` returns 404, both pages are byte-identical to the former Pages copies (sha256 against git), `Server: cloudflare`, no GitHub headers.
3. GitHub Pages disabled via the API.

*Changed while building:* the first workers.dev deploy uploaded CRLF bytes from the Windows checkout. Fixed by re-checking out `site/` with `autocrlf=false` and adding the CRLF guard.

*From review:*

- High: cut-over order (a non-interactive deploy takes over the hostname, so desk deploys happen before merge); rollback order; Touches line; generated types; security headers.
- Medium: rollback step 0 (stop Workers Builds re-attaching), compare against git blobs, disable Pages before merge, workers.dev subdomain note, config-sync check.

#### For DEPLOYING.md fix (applied 2026-10-20)

For the coordinator to apply (outside this slice file's remit): the belief that wrangler overrides the DNS record when run non-interactively proved false against Cloudflare's API. DEPLOYING.md must say the GitHub Pages CNAME has to be deleted first (in the Cloudflare dashboard; wrangler has no DNS scope) before the custom-domain deploy, otherwise it fails with error 100117.

#### For CLAUDE.md

- Landing site (Slice 68): `readthestupidtext.sirous.uk` is the Cloudflare Worker `readthestupidtext` (`wrangler.jsonc`, `worker/index.ts`), static files in `site/` (not `docs/`); GitHub Pages is off. Deploy with `npm run deploy` (refuses CRLF site files; `npm run typecheck` keeps `wrangler.jsonc` and `wrangler.workers-dev.jsonc` in sync) until Workers Builds is connected (build watch paths `worker/**`, `site/**`, `wrangler*.jsonc`, `package*.json`). A Worker custom domain can't replace an existing DNS record: delete it first (wrangler has no DNS scope). Privacy policy edits touch both `PRIVACY.md` and `site/privacy.html`. Don't add `docs/CNAME` or a GitHub Actions deploy. See `docs/DEPLOYING.md`. (Slice 68: docs/plan/slices/68-landing-site-on-worker.md)

#### For README.md

The landing page and privacy policy live in `site/` and are served by a Cloudflare Worker; `npm run dev` previews them, `npm run deploy` publishes. See `docs/DEPLOYING.md`.

#### Not yet verified

- Workers Builds connected, and a push touching `site/**` redeploys while a `src/**`-only push doesn't.
