# Slice 467 — Dino: Sonos on dino.elinsoft.co.uk, and a Reporting Endpoint the portal can reach
**Product:** Dino

*Built 2026-11-27 (items 1, 2, 5), merged as PR #1453 (90cb5a8d). Items 3 (live read after the release) and 4 (Sonos portal) are open.*

*Changed while building:* the `run_worker_first` entries are exact paths (no wildcard), so the trailing-slash forms (`/sonos/v2/`, `/sonos/v2.0/`) are listed as their own entries. The wrangler forms were checked on developers.cloudflare.com (custom-domains page; static-assets binding page) because the context7 quota was out.

**Depends on:** nothing (Slice 436's reporting endpoint and Slice 283's Sonos service are merged).

**Touches:** `wrangler.jsonc` (a `dino.elinsoft.co.uk` custom domain), the Worker's Host handling (wherever `dinoreader.sirous.uk` is chosen by Host, `worker/index.ts` and its helpers), `worker/sonos-reporting.ts`, `worker/sonos-reporting.test.mjs`, the Sonos paths in `run_worker_first` of `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `worker/downloads.ts`, `worker/download-keys.mjs`, `worker/download-keys.d.mts`, `worker/check-configs.mjs`, `worker/downloads.test.mjs`, `worker/CLAUDE.md`, `docs/DEPLOYING.md`, `docs/plan/unverified.md`, this file.

## Why

The Sonos Developer Portal refused the Reporting Endpoint `https://dinoreader.sirous.uk/sonos/v2.0` as unreachable (2026-11-27): the Worker answers only `POST /sonos/v2.0/timePlayed`, so a GET on the base address is a 404. The owner also wants Sonos on the Elinsoft domain.

## Decisions (settled, never re-ask)

- **Sonos uses `dino.elinsoft.co.uk`** (owner, 2026-11-27: "yes" to moving it to Elinsoft, then "actually, it should use dino.elinsoft.co.uk/sonos/v2"). The Reporting Endpoint set in the Sonos portal is `https://dino.elinsoft.co.uk/sonos/v2`, as the owner wrote it. `elinsoft.co.uk` is already a Cloudflare zone (the `elinsoft-site` Worker's custom domains), so the Dino Worker gets `dino.elinsoft.co.uk` as its own custom domain through the normal release pipeline, never a shell deploy.
- `dinoreader.sirous.uk` keeps working as it does today (apps and links already shipped use it); `dino.elinsoft.co.uk` serves the same Dino host behaviour.

## Work

- [x] 1. Add `{ "pattern": "dino.elinsoft.co.uk", "custom_domain": true }` to `wrangler.jsonc` routes and treat that host exactly like `dinoreader.sirous.uk` wherever the Worker chooses by Host; tests.
- [x] 2. Serve the reporting endpoint under both `/sonos/v2` and `/sonos/v2.0` (`POST .../timePlayed` as today), and answer `GET` and `HEAD` on the base (`/sonos/v2`, `/sonos/v2.0`, with or without a trailing slash) with 200 and an empty body, so the portal's reachability check passes; add the paths to `run_worker_first`; tests.
- [x] 3. After the release pipeline deploys it: read live that `https://dino.elinsoft.co.uk/sonos/v2` returns 200 on GET and that `POST /sonos/v2/timePlayed` with an empty body returns 400 (as on the old host).
- [ ] 4. Sonos portal (integration a6BPs000000qWNBMA2): set the Reporting Endpoint to `https://dino.elinsoft.co.uk/sonos/v2`, and move the integration's other Dino URLs (SMAPI endpoint, images, AppLink) to `dino.elinsoft.co.uk` where the portal holds them; in the owner's signed-in Chrome, or one owner step with its direct portal link if the portal needs their sign-in; read the saved values back.
- [x] 5. `worker/CLAUDE.md` and `docs/DEPLOYING.md`: the new host and the endpoint paths.

## Not yet verified

- Item 3 checked live 2026-11-28 02:50Z after release-dino `629a006d` (success): `GET` and `HEAD https://dino.elinsoft.co.uk/sonos/v2` answered 200, `POST /sonos/v2/timePlayed` with `{}` answered 400.
- [chrome] Item 4 (Sonos portal values), recorded in `docs/plan/unverified.md`.

#### From review

Two review passes: high (5 nits, 3 fixed) and medium (2 nits, none applied).

- Applied #2 (`reviews/pr-1453-high.md`): the stale doc comment in `worker/sonos-reporting.ts` and the `worker/CLAUDE.md` wording.
- Applied #4 (`reviews/pr-1453-high.md`): the Touches line no longer lists a file the slice does not change.
- Applied #5 (`reviews/pr-1453-high.md`): the live DNS read is recorded below.
- Skipped #1 (`reviews/pr-1453-medium.md`): "Home and Get-the-app links hard-code https://dinoreader.sirous.uk/" - `site/dino/sonos-link.html` is outside Touches, and the old host keeps working (Decision: `dinoreader.sirous.uk` keeps working). Follow-up: move the page's links to `location.origin` in a later change.
- Skipped #2 (`reviews/pr-1453-medium.md`): "GET/HEAD on the base returns 200 with empty body" - item 2 of this slice asks for exactly 200 and an empty body; whether the portal wants more is read in item 4.

- Skipped #1 (`reviews/pr-1453-high.md`): "`SiteHost` is still `dinoreader.sirous.uk`, so `CodeOf` rejects an https link-page URL from `dino.elinsoft.co.uk`." - `SonosLink.cs` is outside this slice's Touches and the parked s466 worktree edits that file; the app keeps the old host, which the Decisions keep working.
- Skipped #3 (`reviews/pr-1453-high.md`): "The policy names `dinoreader.sirous.uk` as the project's only server." - both hosts are the same Worker and operator, so the policy's processing statement stays true. Follow-up: name `dino.elinsoft.co.uk` in `site/dino/privacy.html` and `PRIVACY.md` in a later change.
- DNS read (finding 5): `nslookup dino.elinsoft.co.uk 1.1.1.1` at 22:57Z on 2026-11-27 answered "Non-existent domain", so no DNS record blocks the custom domain.
