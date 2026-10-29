### Slice 194 — A VM script that retakes the control panel screenshot

*Planned 2026-10-29.* Tooling plus one site image. Gap found in Slice 170 (the panel was redesigned; `site/img/control-panel.webp` still shows the old one).

**Touches:** `tools/desktop-vm/checks/rtst-panel-screenshot.ps1` (new), `tools/desktop-vm/checks/_lib.ps1` (a window-only capture helper, if missing), `tools/desktop-vm/README.md`, `site/img/control-panel.webp`, `docs/plan/slices/194-control-panel-screenshot-script.md`.

#### Why

The landing page shows the old control panel. A script means the picture can be taken again after any panel change, instead of by hand.

#### Work

- [ ] A VM check that installs the build under test, opens the control panel with sample text read aloud (playing state, English, 100% scale) and saves the panel window only as PNG; it raises the guest to 1920x1080 itself (the harness resets to 1024x768). The throw-away scripts from the 2026-10-29 Dino Store screenshots show the steps.
- [ ] Convert to WebP at the size the site uses and replace `site/img/control-panel.webp`.
- [ ] README line: how to run it.

#### Verification

- The script runs inside 10 minutes and leaves the VM restored to `clean`.
- The landing page shows the current panel after deploy.
