### Slice 194 — A VM script that retakes the control panel screenshot

*Built (wave 2026-10-30), PR #591; the site image is not replaced yet.* *Planned 2026-10-29.* Tooling plus one site image. Gap found in Slice 170 (the panel was redesigned; `site/img/control-panel.webp` still shows the old one).

**Touches:** `tools/desktop-vm/checks/rtst-panel-screenshot.ps1` (new), `tools/desktop-vm/checks/_lib.ps1` (a window-only capture helper, if missing), `tools/desktop-vm/Invoke-DesktopTest.ps1` (opt-in `-Check` and `-CopyOut`), `tools/desktop-vm/README.md`, `site/img/control-panel.webp`, `docs/plan/slices/194-control-panel-screenshot-script.md`.

#### Why

The landing page shows the old control panel. A script means the picture can be taken again after any panel change, instead of by hand.

#### Work

- [ ] A VM check (built, not verified: never run in the VM) that installs the build under test, opens the control panel with sample text read aloud (playing state, English, 100% scale) and saves the panel window only as PNG; it raises the guest to 1920x1080 itself (the harness resets to 1024x768). The throw-away scripts from the 2026-10-29 Dino Store screenshots show the steps.
- [ ] Convert to WebP at the size the site uses and replace `site/img/control-panel.webp`.
- [x] README line: how to run it.

#### Verification

- The script runs inside 10 minutes and leaves the VM restored to `clean`.
- The landing page shows the current panel after deploy.

Not yet verified: one VM run of `-App rtst -Check rtst-panel-screenshot -CopyOut <folder>` (the 1920x1080 mode on Hyper-V's display, runs inside 10 minutes, VM restored to `clean`, panel fully on screen).

Note: the VM has no audio device, so the panel may show idle instead of playing; the shot is then taken with the sample text loaded and the read states are printed in the check message.

#### VM run (2026-10-30)
- `Invoke-DesktopTest.ps1 -App rtst -Check rtst-panel-screenshot -CopyOut <folder>` PASS on the first run (build 109 s, about 3.6 min). It captured the panel window only (392x550 PNG) at 100% scale with an English UI, and the VM was restored to `clean`.
- The picture is **not** good enough for the landing page, so `site/img/control-panel.webp` is unchanged. The guest is offline with no voice pack, so the panel shows "Preparing the voice..." and a cut-off "You are offline. The Windows voice reads..." note, and the Windows build watermark shows at the bottom right.
- Not yet verified / next: run the check on a guest with the English voice pack installed and network on (a snapshot with the pack, or a copy-in step), so the panel shows a real read. Crop or hide the watermark. Then convert to WebP at the site's 362x525 (keep the aspect ratio) and replace the site image.
