# Dino Reader design assets

## Screens mock — `Dino Reader v2.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`,
  file `Dino Reader v2.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+v2.dc.html>).
- **Copied:** 2026-10-20, verbatim from `DesignSync get_file` (refreshed again on
  2026-10-24 with the Batch 17-18 import; refreshed the
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

## Batch 16 mock — `Dino Reader Batch 16.dc.html`

- **Source:** the same Claude Design project, file `Dino Reader Batch 16.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+16.dc.html>).
- **Copied:** 2026-10-22, verbatim from `DesignSync get_file` (refreshed 2026-10-24).
- **Covers:** Batch 16, Slices 91-93 (first run, starter gallery, free stories
  store, language, backup). Everything it doesn't show follows `Dino Reader v2`
  and Slice 80's tile overlay. Read it the same way as the v2 file; it imports
  the same `support.js` and app icon.

| # | Screen | Line |
|---|---|---|
| — | Intro (Batch 16, Slices 91-93, Decisions 94-99) | 18 |
| — | **First run · Slices 91 and 93** (group) | 23 |
| 16 | First run · language | 28 |
| 17 | First run · download | 55 |
| 18 | First run · ready | 81 |
| 19 | First grown-up | 100 |
| 20 | Starter gallery | 134 |
| — | **Grown-up · Free stories · Slice 92** (group) | 147 |
| 21 | Store | 152 |
| 22 | Store offline | 184 |
| 23 | Store empty | 199 |
| — | **Grown-up · Language** (group) | 213 |
| 26 | Language | 217 |
| — | **Grown-up · Backup · Slice 93** (group) | 239 |
| 24 | Backup | 244 |
| 25 | Restore result | 268 |

(Generated from the group captions and `data-screen-label` attributes.)

**Import prompt (verbatim):**

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+16.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 16.dc.html`

Also read these files the selection imports:
- `dino_reader_icons/ios/AppIcon-1024-light.png`
- `support.js`

Implement: `Dino Reader Batch 16.dc.html`
```

## Batches 17-18 mock — `Dino Reader Batch 17-18.dc.html`

- **Source:** the same Claude Design project, file `Dino Reader Batch 17-18.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+17-18.dc.html>).
- **Copied:** 2026-10-24, verbatim from `DesignSync get_file` (import prompt in
  `docs/design/rtst-phone/README.md`).
- **Covers:** Slices 94-98 (Good Enough, the add-a-book button, the never-mirroring layout, picture
  for each page) and Slice 102 (store credit lines). Read it the same way as the v2 file.

| # | Screen | Line |
|---|---|---|
| — | **Shelf and Good Enough · Slices 94, 95, 97** (group) | 23 |
| 27 | Kids’ shelf · Good Enough first | 27 |
| 28 | Good Enough · picture mode | 44 |
| — | **Layout never mirrors · Slice 96 · Decision 100** (group) | 63 |
| 29 | Shelf in Farsi · still left to right | 67 |
| 30 | Farsi book · reader keeps its layout | 84 |
| — | **A picture for each page · adding · Slice 98 · Decision 102** (group) | 96 |
| 31 | Text-only file · add a picture per page | 100 |
| 32 | File with its own pictures | 118 |
| — | **A picture for each page · reading · Slice 98** (group) | 136 |
| 34 | Upright · text over the picture | 140 |
| 35 | Sideways · picture left, text right | 157 |
| — | **Batch 20 · Ten open stories in the store · Slice 102 · Decision 106** (group) | 172 |
| 36 | Free stories · with credit lines | 175 |

(Generated from the captions with a script; regenerate it when the file is refreshed.)

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

## Batches 22-23 mock — `Dino Reader Batch 22-23.dc.html`

- **Source:** the same Claude Design project, file `Dino Reader Batch 22-23.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+22-23.dc.html>).
- **Copied:** 2026-10-24, verbatim from `DesignSync get_file`.
- **Covers:** Slices 107-114 (Decisions 111-119). Frames continue from Batch 17-18 (36). Frame 41
  supersedes the frame 28 note "nothing on screen changes": the translated text is shown.
- **Images it imports:** `assets/good-enough/2.jpg` and `assets/good-enough/cover.jpg` are the Good
  Enough starter's pictures (`tests/DinoReader.Tests/Starters/good-enough/`);
  `dino_reader_icons/ios/AppIcon-1024-light.png` is `icons/ios/AppIcon-1024-light.png`.
  Read it the same way as the v2 file.

| # | Screen | Line |
|---|---|---|
| — | **Translate by default · narrator for all books · Slices 107, 109 · Decisions 111, 114** (group) | 23 |
| 37 | New book · Text step · translate is on | 27 |
| 38 | Grown-up mode · Narrator and Record | 50 |
| 39 | Narrator · one per language | 82 |
| — | **A cast per reading language · Slice 108 · Decision 113** (group) | 106 |
| 40 | Who’s speaking · Farsi cast of an English book | 109 |
| — | **Translated text on screen · Slice 110 · Decision 112** (group) | 128 |
| 41 | English book, Farsi on screen | 132 |
| 42 | Scanned page, translated · picture + text | 144 |
| 43 | This book · grown-up options | 161 |
| — | **Parents read in their own voice · Slice 111 · Decision 115** (group) | 182 |
| 44 | Record · recording a page | 186 |
| 45 | Record · page recorded | 204 |
| 46 | Reading a recorded page | 219 |
| — | **Store filters · Slice 112 · Decision 116** (group) | 231 |
| 47 | Free stories · ages 6–8 | 234 |
| — | **Batch 23 · Voices that sound like a story · Slices 113, 114 · Decisions 117–119** (group) | 255 |
| 48 | Who’s speaking · flavours and Storyteller offer | 259 |
| 49 | Storyteller voices installed | 285 |

(Generated from the captions with a script; regenerate it when the file is refreshed.)

Import prompt (verbatim):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+22-23.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 22-23.dc.html`

Also read these files the selection imports:
- `assets/good-enough/2.jpg`
- `assets/good-enough/cover.jpg`
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: `Dino Reader Batch 22-23.dc.html`
```

## Batch 24 mock — `Dino Reader Batch 24.dc.html`

- **Source:** the same Claude Design project, file `Dino Reader Batch 24.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+24.dc.html>).
- **Copied:** 2026-10-25, verbatim from `DesignSync get_file`.
- **Covers:** Batch 24, Slices 115-118 (Decisions 120-124). **Supersedes frames 48-49** of the
  Batch 22-23 mock: the offer card, "Hear one", "Use Dino's first voices", the Sounds row and raw
  voice names are gone.
- Imports: `dino_reader_icons/ios/AppIcon-1024-light.png` is `icons/ios/AppIcon-1024-light.png`
  here; `assets/good-enough/cover.jpg` is only the sample cover of the shipped starter and is not copied.

| # | Screen | Line |
|---|---|---|
| — | **Storyteller voices by default · Slice 115 · Decisions 120, 121** (group) | 23 |
| 50 | Grown-up mode · getting the voices | 27 |
| 51 | Voices ready · every book re-cast | 56 |
| — | **Storyteller characters · Slice 116 · Decision 122 · animal sounds · Slice 117 · Decision 123** (group) | 84 |
| 52 | Who’s speaking · characters | 88 |
| 53 | Pick a character | 107 |
| 54 | Who’s speaking · Farsi characters | 136 |
| 55 | Narrator · narrator characters | 155 |

(Generated from the captions with a script; regenerate it when the file is refreshed.)

Import prompt (verbatim):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+24.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 24.dc.html`

Also read these files the selection imports:
- `assets/good-enough/cover.jpg`
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: `Dino Reader Batch 24.dc.html`
```

## Batch 25 mock — `Dino Reader Batch 25.dc.html`

- **Source:** the same Claude Design project, file `Dino Reader Batch 25.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+25.dc.html>).
- **Copied:** 2026-10-25, verbatim from `DesignSync get_file`.
- **Covers:** Batch 25, Slice 122 (Decision 127). **Supersedes frame 34** (58% panel cap) of the
  Batch 17-18 mock. Slices 119-121 change no screens.
- Imports: `assets/three-little-pigs/3.jpg` and `5.jpg` are the starter's page pictures, already in
  the repo at `tests/DinoReader.Tests/Starters/three-little-pigs/pictures/` (not copied again).

| # | Screen | Line |
|---|---|---|
| — | **Words over pictures · Slice 122 · Decision 127** (group) | 23 |
| 56 | Upright · panel at most 30% | 27 |
| 57 | Words switched off · picture only | 44 |
| 58 | Who’s speaking · photo book, words off | 58 |

(Generated from the captions with a script; regenerate it when the file is refreshed.)

Import prompt (verbatim):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+25.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 25.dc.html`

Also read these files the selection imports:
- `assets/three-little-pigs/3.jpg`
- `assets/three-little-pigs/5.jpg`

Implement: `Dino Reader Batch 25.dc.html`
```

## Batches 26-28 mock — `Dino Reader Batch 26-28.dc.html`

- **Source:** the same Claude Design project, file `Dino Reader Batch 26-28.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+26-28.dc.html>).
- **Copied:** 2026-10-25, verbatim from `DesignSync get_file`.
- **Covers:** Slice 125 as changed by Slice 136 (Decision 132 changed: no cast page Animal line), Slice 132
  (forced update, Decision 139) and Slice 133 (usage counts switch and first-run line, Decisions 140-141).
  The imported icon `dino_reader_icons/ios/AppIcon-1024-light.png` is already here as
  `icons/ios/AppIcon-1024-light.png`.

| # | Screen | Line |
|---|---|---|
| — | Character animals · Slice 125 · Decision 132 (changed) | 23 |
| 59 | Who's speaking · animal comes from the character | 27 |
| — | Forced update and usage counts · Slices 132–133 · Decisions 139–141 | 54 |
| 61 | Please update | 58 |
| 62 | Grown-up mode · counts switch, update line | 76 |
| 63 | First run · ready, with the counts line | 96 |

To refresh: `DesignSync get_file` on the same project and path, overwrite this file, regenerate the table.

The user's import prompt, verbatim:

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+26-28.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 26-28.dc.html`

Also read these files the selection imports:
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: `Dino Reader Batch 26-28.dc.html`
```

## Batch 30 mock — `Dino Reader Batch 30.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 30.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+30.dc.html>).
- **Copied:** 2026-10-26, verbatim from `DesignSync get_file`. The dino is `dino_reader_icons/ios/AppIcon-1024-light.png`, already here as `icons/ios/AppIcon-1024-light.png`; the background pictures (`assets/three-little-pigs/3.jpg`, `assets/good-enough/*.jpg`) are story pictures, not copied.
- **Covers:** Batch 30, Slice 143 (screens 64-65, the shared wait overlay with the hopping dino). Slices 144-146 have no screens.

Open it in a text editor (`support.js` is not copied). Each screen is headed by a caption
`N · Title` with its behaviour in the note underneath; the spinners and the hopping dino are
built in the `<script>` block at the end (sizes, colours, the `dinoHop`/`rtstSpin` keyframes).

| # | Screen | Line |
|---|---|---|
| — | **Shared wait overlay · Slice 143 · Decisions 146, 149** (group) | 23 |
| 64 | Opening a book · first audio not ready | 27 |
| 65 | Own book · reading the pictures | 48 |

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
