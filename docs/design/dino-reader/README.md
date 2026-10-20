# Dino Reader design assets

## Screens mock — `Dino Reader v2.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`,
  file `Dino Reader v2.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+v2.dc.html>).
- **Copied:** 2026-10-20, verbatim from `DesignSync get_file` (refreshed the
  same day: added 6b, 8b, 14, 15 and the fonts note).
- **Covers:** Batch 10, Slices 58-67 (each slice's `#### Design` subsection
  points at the screens below).
- **Fonts:** Fredoka (UI), Andika (story text), Vazirmatn (Farsi/Arabic), all
  SIL OFL 1.1; Chinese/Japanese use the system font.

The mock is one HTML canvas. `support.js` (it only renders the mock inside
Claude Design) is not copied, so open the file in a text editor, not a browser.
Every screen is an inline-styled block headed by a caption `N · Title`; search
for the caption to jump to it. The image it imports,
`dino_reader_icons/ios/AppIcon-1024-light.png`, is already here as
`icons/ios/AppIcon-1024-light.png`.

| # | Screen | Line |
|---|---|---|
| — | Intro + fonts note (Fredoka, Andika, Vazirmatn; OFL 1.1; pages never tilted) | 18 |
| — | **Child** (group) | 23 |
| 1 | Gallery (home) | 24 |
| 2 | Reading · picture mode, scanned page | 28 |
| 3 | Reading · picture mode, generated page | 30 |
| 4 | Reading · text mode | 31 |
| 5 | Wandered to another page | 32 |
| 12 | Bedtime colours · picture mode | 35 |
| — | **Grown-up** (group) | 37 |
| 6 | Grown-ups gate · step 1: hold | 38 |
| 7 | Grown-ups gate · step 2: sum | 40 |
| 8 | Gallery · grown-up mode | 41 |
| 9 | Add a story | 47 |
| 10 | Cast | 57 |
| 11 | Merge two characters | 64 |
| 6b | Gate with a screen reader on | 70 |
| 8b | Edit a story | 75 |
| — | **Right-to-left (fa / ar)** (group) | 83 |
| 14 | Farsi · picture mode, right-to-left | 84 |
| 15 | Farsi · cast with few voices | 88 |
| — | **Tablet** (group) | 95 |
| 13 | Tablet · picture mode on a real page | 96 |

(Generated from the captions and `data-screen-label` attributes; regenerate
it with a script if the file is refreshed.)

**Refresh:** fetch the file again with `DesignSync get_file` (main thread
only), write its `content` field over this copy with a script, regenerate the
table, and re-run `design-to-plan` for the changed screens.

**Import prompt (verbatim):**

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+v2.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader v2.dc.html`

Also read these files the selection imports:
- `dino_reader_icons/ios/AppIcon-1024-light.png`
- `support.js`

Implement: `Dino Reader v2.dc.html`
```

## Icons — `icons/`

Source: the user's "Tray and feature panel design.zip" (folder
`dino_reader_icons/`, design "4b · Book hug"), added 2026-10-20.

| Folder | What | Used by |
|---|---|---|
| `icons/android/` | Legacy + adaptive launcher icons per density, `mipmap-anydpi-v26` XML, `play-store-512.png` | Slice 58 |
| `icons/ios/` | iOS 18 light/dark/tinted 1024 px + `Contents.json`, `legacy/` sizes | Slice 67 |
| `icons/windows/` | MSIX visual assets (scale 100-400, targetsize unplated) | Slice 67 |
| `icons/tray/` | Tray `.ico`/PNGs | Unused: Dino Reader has no tray |

See `icons/README.md` for the designer's notes. Copy these into the app
project when its slice runs; don't redraw or regenerate them.
