# Read The Stupid Text phone design

## Batch 19 mock — `RTST Phone Batch 19.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file
  `RTST Phone Batch 19.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=RTST+Phone+Batch+19.dc.html>).
- **Copied:** 2026-10-24, verbatim from `DesignSync get_file`, with the image it imports
  (`design_handoff_tray_panel/assets/glyph.png`, the header watermark) at the same relative path.
- **Covers:** Batch 19, Slices 99-101 (main page, Settings, the two wizard pages). Android and
  iOS only; the Windows tray app is unchanged.

`support.js` (it only renders the mock inside Claude Design) is not copied, so open the file
in a text editor. Every screen is a 360x760 block headed by a caption `RN · Title`; search for
the caption to jump to it. Each screen's note underneath states its behaviour.

| # | Screen | Line |
|---|---|---|
| — | **Main page and Settings · Slice 99 · Decision 103** (group) | 21 |
| R1 | Main page · reading | 25 |
| R2 | Main page · first launch, nothing to read | 52 |
| R3 | Settings | 76 |
| — | **Wizard page 1 · Take a photo of each page · Slice 100 · Decision 104** (group) | 102 |
| R4 | Three pages taken | 106 |
| R5 | Back with pages taken | 128 |
| — | **Wizard page 2 · Check the text · Slice 101 · Decision 105** (group) | 148 |
| R6 | Another language · translate ticked | 152 |
| R7 | Translating · model downloading | 167 |

(Generated from the captions with a script; regenerate it when the file is refreshed.)

**Refresh:** fetch the file again with `DesignSync get_file` (main thread only), write its
`content` field over this copy with a script, regenerate the table, and re-run
`design-to-plan` for the changed screens.

**Import prompt (verbatim):**

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=RTST+Phone+Batch+19.dc.html

Focus on these files (the whole project is readable):
- `RTST Phone Batch 19.dc.html`
- `assets/app-tile.png`
- `assets/glyph.png`
- `Dino Reader Batch 16.dc.html`
- `Dino Reader Batch 17-18.dc.html`
- `Dino Reader Icons.dc.html`
- `Dino Reader v2.dc.html`
- `Dino Reader.dc.html`
- `github.md`
- `Play Feature Graphic.dc.html`
- `store_assets_preview.html`
- `store_hero_preview.html`
- `store_listing_preview.html`
- `Tray Control Panel.dc.html`

Also read these files the selection imports:
- `design_handoff_tray_panel/assets/glyph.png`

Implement: the selected files
```

The prompt also named the Dino mocks (copied to `docs/design/dino-reader/`) and the icon,
tray-panel and store-listing files; those were already imported for earlier slices or feed no
planned slice, so they were not re-copied.

## Batch 29 mock — `RTST Batch 29.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `RTST Batch 29.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=RTST+Batch+29.dc.html>).
- **Copied:** 2026-10-26, verbatim from `DesignSync get_file`. Its images are `assets/glyph.png` (the tray glyph), `assets/app-tile.png` (the control-panel tile) and `design_handoff_tray_panel/assets/glyph.png` (the header watermark), at the same relative paths.
- **Covers:** Batch 29, Slices 137-142 (wait overlay R8-R9, counts switch R10, first-run line R11, Windows flyout W1 and control panel W2).

Open it in a text editor (`support.js` is not copied). Each screen is headed by a caption
`N · Title` with its behaviour in the note underneath; the spinners and the hopping dino are
built in the `<script>` block at the end (sizes, colours, the `dinoHop`/`rtstSpin` keyframes).

| # | Screen | Line |
|---|---|---|
| — | **Phone · wait overlay · Slice 138 · Decision 146** (group) | 21 |
| R8 | Main page · getting the translator | 25 |
| R9 | Main page · preparing the voice | 58 |
| — | **Phone · usage counts · Slice 137 · Decisions 143–145** (group) | 89 |
| R10 | Settings · counts switch | 93 |
| R11 | First run · language, with the counts line | 124 |
| — | **Windows · wait flyout and counts switch · Slices 140–141** (group) | 146 |
| W1 | Hotkey read · flyout by the tray | 150 |
| W2 | Control panel · waiting, counts switch | 172 |

To refresh: fetch the file again with `DesignSync get_file` and overwrite this copy, then
regenerate this table. Never call `DesignSync` to read visual values; read this copy.

Import prompt (verbatim):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=RTST+Batch+29.dc.html

Focus on these files (the whole project is readable):
- `RTST Batch 29.dc.html`
- `Dino Reader Batch 30.dc.html`

Also read these files the selection imports:
- `assets/app-tile.png`
- `assets/glyph.png`
- `design_handoff_tray_panel/assets/glyph.png`

Implement: the selected files
```
