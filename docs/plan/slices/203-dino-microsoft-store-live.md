### Slice 203 — Dino on the Microsoft Store: turn on automatic submissions and link it

*In progress (wave 2026-10-30), copy-only PR.* The user reported Dino live on the Microsoft Store on 2026-10-30 and asked for this tonight.

**Touches:** `site/dino/index.html`, `STORE.md`, `docs/plan/slices/203-dino-microsoft-store-live.md`, repository variable `DINOREADER_STORE_APP_ID`.

#### Why

Slice 127 left one step for after the first hand-made Store submission was published: set `DINOREADER_STORE_APP_ID=9N9VHRLCZXN5` so `release.yml`'s `store-submit-dino` job submits every Dino release. The Dino site still said "coming to the Store" and "submission pending".

#### Work

- [x] Checked live: https://apps.microsoft.com/detail/9N9VHRLCZXN5 serves "Dino Book Reader - Free download and install on Windows | Microsoft Store" (2026-10-30).
- [x] Dino site: the hero's Windows pill and the Windows card link to the Store page (a plain link; the Dino site still makes no third-party request on load).
- [x] `STORE.md`: the Dino submission paragraph says it is live and automatic.
- [ ] After merge: set the repository variable `DINOREADER_STORE_APP_ID=9N9VHRLCZXN5` (`gh variable set`).
- [ ] The next Dino release runs `store-submit-dino` green and its submission shows in Partner Center (watch the release run; `ci-status` on failure).

#### Verification

- `gh variable list` shows the variable; the next `release.yml` run with a Dino change runs `store-submit-dino` instead of skipping it.
- The Dino site's Windows buttons open the Store page.

#### Not yet verified

- The first automatic Dino submission (needs the next Dino release on `main`).
