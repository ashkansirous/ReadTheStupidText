# Slice 68 — Landing site served by a Cloudflare Worker

*Planned.*

**Batch:** 11 (Lets-Call-Mom parity)
**Depends on:** —
**Touches:** `wrangler.jsonc`, `worker/**`, `docs/index.html`, `docs/privacy.html`, `docs/CNAME`, `docs/.nojekyll`, `package.json`, `docs/DEPLOYING.md`, `README.md`, `CLAUDE.md`
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

- [ ] `wrangler.jsonc` at the repo root: Worker `readthestupidtext`, custom-domain route
      `readthestupidtext.sirous.uk` (the `sirous.uk` zone is already on the account),
      `workers_dev: true` as a fallback, static `assets` from the current landing files.
      Port the shape from Lets-Call-Mom's `wrangler.jsonc`, without the Durable Objects or D1.
- [ ] Minimal `worker/index.ts`: serve the assets, answer `/privacy` and `/privacy.html`
      (the Store listing links the `.html` URL, so it must keep working), and 404 on anything else.
- [ ] Move the landing files out of `docs/` into the folder the Worker serves (e.g. `site/`),
      keeping URLs unchanged. Remove `docs/CNAME` and `docs/.nojekyll`, then turn GitHub Pages
      off in the repo settings **after** the Worker answers on the domain.
- [ ] Hook Cloudflare Workers Builds up to the repo (dashboard step, guided one step at a
      time), building only on changes to the Worker or the site files.
- [ ] `docs/DEPLOYING.md`: how the Worker deploys, the DNS cut-over, the fallback URL.

## Done when

- `https://readthestupidtext.sirous.uk/` and `/privacy.html` return the same pages as today,
  served by the Worker (`cf-ray` header present, no GitHub Pages header).
- A push to `main` touching `site/**` redeploys within minutes. A push touching only
  `src/**` does not.

## Notes

- DNS cut-over order: deploy the Worker on `workers.dev`, check it, then add the custom
  domain. Cloudflare replaces the GitHub Pages CNAME record. Expect a short TLS issuance gap.
- Reference: Lets-Call-Mom `wrangler.jsonc`, `docs/DEPLOYING.md`.
