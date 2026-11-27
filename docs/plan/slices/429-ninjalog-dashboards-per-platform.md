### Slice 429 — Dino: Ninjalog dashboards get device and app data, one dashboard per platform, charts split by version
**Product:** Dino

*Built 2026-11-22, merged as PR #1335 (e54f746); dashboards uploaded and checked live the same day.*

**Touches:** `docs/analytics/ninjalog-dashboard.template.json` (new, replaces `ninjalog-dashboard.json`), `docs/analytics/ninjalog-dashboards/*.json` (new, generated), `tools/dino/ninjalog-dashboards.mjs` (new), `tools/dino/ninjalog-dashboards.test.mjs` (new), `docs/analytics/ninjalog-queries.md`, `worker/CLAUDE.md`

## Why

Owner, 2026-11-22: "my ninja dashboard is missing device/app data. It should be added there and I want to be able to
filter on app version and platform". The "New users" dashboard (Slices 419, 421) had no device or version widgets.

## Decisions

- **Ninjalog dashboards have no filters.** Its dashboard format (`app.ninjalog.dev/docs/dashboards`, read 2026-11-22 in
  the owner's signed-in Chrome) has widgets and one time range only; the app's dashboard page has no other control.
- **Owner picked "Dashboard per platform"** (2026-11-22): one template makes four dashboards (All, Android, iOS, Windows);
  the platform filter is the dashboard you open, and in a platform dashboard the first-launch, funnel, launch and device
  charts are split by app version (listening seconds, where-stopped, fallbacks and the span tables are not). A
  table row's key links to Search narrowed to it (one version). All keeps the `new-users` slug.
- **Upload with an MCP token, not Chrome** (owner: "do 2", then "do the token via google chrome ext"): token
  `rtst-dashboards-claude`, scopes `logs:read`, `dashboards:read`, `dashboards:write` on `ashkan-sirous/dino-book-reader`,
  stored as `NINJALOG_MCP_TOKEN` in `D:\secrets\dinoreader\secrets.env`.
- Device and app widgets count `app_opened` events (launches, not people: no id, Decision 140).

## Work items

- [x] 1. Template `docs/analytics/ninjalog-dashboard.template.json` with tokens `{{app}}`, `{{any}}`, `{{split}}`; the 7
      old widgets kept, plus launches by version (time chart), launches and first launches per version, and OS
      version, phone/tablet/desktop, CPU architecture, RAM, CPU cores, app language and country.
- [x] 2. `tools/dino/ninjalog-dashboards.mjs`: writes `docs/analytics/ninjalog-dashboards/<slug>.json`; `--check` fails
      on a stale file; `--upload` runs every widget query of all four dashboards through Ninjalog's MCP `execute` in one
      script and saves nothing if any fails, then saves the four.
- [x] 5. Review pass 1 (high, `D:/ReadTheStupidText-worktrees/reviews/s429-high.md`): 1 (CRLF: `.gitattributes` LF rule
      and `--check` ignores CR), 2 (`public-name.yml` path filter now lists the template and generated files), 3
      (wording), 4 (all queries checked before any save), 5 (missing folder), 6 (SSE/empty reply) and 7 (the
      `worker/CLAUDE.md` edit left to the index pass) fixed. Pass 2 (medium, `reviews/s429-medium.md`): 4 nits left: 1
      is the index pass's line below; 2 (no template query has a quoted double space) and 3 (one save failing mid-way is
      reported by the script's error) not worth code; 4 is wrong (Ninjalog's dashboard docs: "Each row's key links to
      Search narrowed to it").
- [x] 3. Tests `tools/dino/ninjalog-dashboards.test.mjs` (filters, split, grid overlap, unknown token, committed files
      up to date).
- [x] 4. Uploaded 2026-11-22: all four dashboards saved with 17 widgets; on iOS every table widget returned rows over 7
      days. https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/d/new-users-ios

## Rules to lift

- Ninjalog dashboards are generated: edit the template, run `node tools/dino/ninjalog-dashboards.mjs --upload` with
  `NINJALOG_MCP_TOKEN` set; never edit `docs/analytics/ninjalog-dashboards/` or a dashboard in the app by hand (the next
  upload replaces it).
- Index pass: `worker/CLAUDE.md` line 20 still names `ninjalog-dashboard.json`; replace it with the template, the script
  and this slice.
