# Slice 467 — Dino: Sonos on dino.elinsoft.co.uk, and a Reporting Endpoint the portal can reach
**Product:** Dino

*Planned 2026-11-27 from the owner's answers in the starter session of night 2026-11-27-0154. Not started.*

**Depends on:** nothing (Slice 436's reporting endpoint and Slice 283's Sonos service are merged).

**Touches:** `wrangler.jsonc` (a `dino.elinsoft.co.uk` custom domain), the Worker's Host handling (wherever `dinoreader.sirous.uk` is chosen by Host, `worker/index.ts` and its helpers), `worker/sonos-reporting.ts`, `worker/sonos-reporting.test.mjs`, the Sonos paths in `run_worker_first` of `wrangler.jsonc`, `worker/CLAUDE.md`, `docs/DEPLOYING.md`, this file.

## Why

The Sonos Developer Portal refused the Reporting Endpoint `https://dinoreader.sirous.uk/sonos/v2.0` as unreachable (2026-11-27): the Worker answers only `POST /sonos/v2.0/timePlayed`, so a GET on the base address is a 404. The owner also wants Sonos on the Elinsoft domain.

## Decisions (settled, never re-ask)

- **Sonos uses `dino.elinsoft.co.uk`** (owner, 2026-11-27: "yes" to moving it to Elinsoft, then "actually, it should use dino.elinsoft.co.uk/sonos/v2"). The Reporting Endpoint set in the Sonos portal is `https://dino.elinsoft.co.uk/sonos/v2`, as the owner wrote it. `elinsoft.co.uk` is already a Cloudflare zone (the `elinsoft-site` Worker's custom domains), so the Dino Worker gets `dino.elinsoft.co.uk` as its own custom domain through the normal release pipeline, never a shell deploy.
- `dinoreader.sirous.uk` keeps working as it does today (apps and links already shipped use it); `dino.elinsoft.co.uk` serves the same Dino host behaviour.

## Work

- [ ] 1. Add `{ "pattern": "dino.elinsoft.co.uk", "custom_domain": true }` to `wrangler.jsonc` routes and treat that host exactly like `dinoreader.sirous.uk` wherever the Worker chooses by Host; tests.
- [ ] 2. Serve the reporting endpoint under both `/sonos/v2` and `/sonos/v2.0` (`POST .../timePlayed` as today), and answer `GET` and `HEAD` on the base (`/sonos/v2`, `/sonos/v2.0`, with or without a trailing slash) with 200 and an empty body, so the portal's reachability check passes; add the paths to `run_worker_first`; tests.
- [ ] 3. After the release pipeline deploys it: read live that `https://dino.elinsoft.co.uk/sonos/v2` returns 200 on GET and that `POST /sonos/v2/timePlayed` with an empty body returns 400 (as on the old host).
- [ ] 4. Sonos portal (integration a6BPs000000qWNBMA2): set the Reporting Endpoint to `https://dino.elinsoft.co.uk/sonos/v2`, and move the integration's other Dino URLs (SMAPI endpoint, images, AppLink) to `dino.elinsoft.co.uk` where the portal holds them; in the owner's signed-in Chrome, or one owner step with its direct portal link if the portal needs their sign-in; read the saved values back.
- [ ] 5. `worker/CLAUDE.md` and `docs/DEPLOYING.md`: the new host and the endpoint paths.

## Not yet verified

(none yet)
