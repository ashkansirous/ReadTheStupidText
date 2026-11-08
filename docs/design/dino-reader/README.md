# Dino Reader design assets

**Retired by Slice 278 (store books only).** Rows below marked "retired by Slice 278" (or "partly retired") show
screens that no longer exist in the app: add a book, scan, page editor, translation, voice download, cast and
narrator, the old Record. The older `.dc.html` files are verbatim copies of the Claude Design project and keep
those frames; do not build from them. The list of what leaves is Batch 48 frame 186 (line 349 of
`Dino Reader Batch 48 Store Only and Recording.dc.html`); the replacements are Batch 48 frames 177 and 185, and
the recording frames 178-184 and 188-191 (Slice 279).

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
| 9 | Add a story — retired by Slice 278, see Batch 48 frame 186 | 47 |
| 10 | Cast — retired by Slice 278, see Batch 48 frame 186 | 57 |
| 11 | Merge two characters — retired by Slice 278, see Batch 48 frame 186 | 64 |
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
| 17 | First run · download — retired by Slice 278, see Batch 48 frame 186 | 55 |
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
| 31 | Text-only file · add a picture per page — retired by Slice 278, see Batch 48 frame 186 | 100 |
| 32 | File with its own pictures — retired by Slice 278, see Batch 48 frame 186 | 118 |
| — | **A picture for each page · reading · Slice 98** (group) | 136 |
| 34 | Upright · text over the picture — retired by Slice 278, see Batch 48 frame 186 | 140 |
| 35 | Sideways · picture left, text right — retired by Slice 278, see Batch 48 frame 186 | 157 |
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
  Enough starter's pictures (`content/dino/good-enough/pictures/`);
  `dino_reader_icons/ios/AppIcon-1024-light.png` is `icons/ios/AppIcon-1024-light.png`.
  Read it the same way as the v2 file.

| # | Screen | Line |
|---|---|---|
| — | **Translate by default · narrator for all books · Slices 107, 109 · Decisions 111, 114** (group) | 23 |
| 37 | New book · Text step · translate is on — retired by Slice 278, see Batch 48 frame 186 | 27 |
| 38 | Grown-up mode · Narrator and Record — retired by Slice 278, see Batch 48 frame 186 | 50 |
| 39 | Narrator · one per language — retired by Slice 278, see Batch 48 frame 186 | 82 |
| — | **A cast per reading language · Slice 108 · Decision 113** (group) | 106 |
| 40 | Who’s speaking · Farsi cast of an English book — retired by Slice 278, see Batch 48 frame 186 | 109 |
| — | **Translated text on screen · Slice 110 · Decision 112** (group) | 128 |
| 41 | English book, Farsi on screen — retired by Slice 278, see Batch 48 frame 186 | 132 |
| 42 | Scanned page, translated · picture + text — retired by Slice 278, see Batch 48 frame 186 | 144 |
| 43 | This book · grown-up options — retired by Slice 278, see Batch 48 frame 186 | 161 |
| — | **Parents read in their own voice · Slice 111 · Decision 115** (group) | 182 |
| 44 | Record · recording a page — retired by Slice 278, see Batch 48 frame 186 | 186 |
| 45 | Record · page recorded — retired by Slice 278, see Batch 48 frame 186 | 204 |
| 46 | Reading a recorded page — retired by Slice 278, see Batch 48 frame 186 | 219 |
| — | **Store filters · Slice 112 · Decision 116** (group) | 231 |
| 47 | Free stories · ages 6–8 | 234 |
| — | **Batch 23 · Voices that sound like a story · Slices 113, 114 · Decisions 117–119** (group) | 255 |
| 48 | Who’s speaking · flavours and Storyteller offer — retired by Slice 278, see Batch 48 frame 186 | 259 |
| 49 | Storyteller voices installed — retired by Slice 278, see Batch 48 frame 186 | 285 |

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
| 50 | Grown-up mode · getting the voices — retired by Slice 278, see Batch 48 frame 186 | 27 |
| 51 | Voices ready · every book re-cast — retired by Slice 278, see Batch 48 frame 186 | 56 |
| — | **Storyteller characters · Slice 116 · Decision 122 · animal sounds · Slice 117 · Decision 123** (group) | 84 |
| 52 | Who’s speaking · characters — retired by Slice 278, see Batch 48 frame 186 | 88 |
| 53 | Pick a character — retired by Slice 278, see Batch 48 frame 186 | 107 |
| 54 | Who’s speaking · Farsi characters — retired by Slice 278, see Batch 48 frame 186 | 136 |
| 55 | Narrator · narrator characters — retired by Slice 278, see Batch 48 frame 186 | 155 |

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
  the repo at `content/dino/three-little-pigs/pictures/` (not copied again).

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

## Batch 31 mock — `Dino Reader Batch 31.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 31.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+31.dc.html>).
- **Copied:** 2026-10-26, verbatim from `DesignSync get_file`. The dino is `icons/ios/AppIcon-1024-light.png`. The two story pictures it shows (`assets/three-little-pigs/3.jpg`, `5.jpg`) are copied from the starter book `content/dino/three-little-pigs/pictures/` (DesignSync caps files at 256 KiB, so its copies were truncated).
- **Covers:** Batch 31 (Slices 150-154) and Slice 148. Screens 66-68 (three view modes, Slice 151) replace the words-over switch of frames 57-58 (Batch 25); 69 (cast page without merge or animal, Slices 151-152) supersedes frame 59 (Batch 26-28); 70 (Reading speed and app-wide Animal sounds, Slices 150/152); 71 (mid-read stall, Slice 148, Batch 30 overlay with Cancel); 72 (Farsi vowel marks, Slice 154). Slice 153 has no screens.

Open it in a text editor (`support.js` is not copied). Each screen is headed by a caption
`N · Title` with its behaviour in the note underneath; the hopping dino is built in the
`<script>` block at the end.

| # | Screen | Line |
|---|---|---|
| — | **Three view modes · Slice 151 · Decision 156** (group) | 23 |
| 66 | Picture + text (default for picture books) | 28 |
| 67 | Picture only | 54 |
| 68 | Text only | 77 |
| — | **Cast page and settings · Slices 151, 152, 150 · Decisions 155–157** (group) | 100 |
| 69 | Who's speaking · no merge, no animal — retired by Slice 278, see Batch 48 frame 186 | 105 |
| 70 | Grown-up settings · Reading speed, Animal sounds | 122 |
| — | **Mid-read stall · Slice 148 · Vowel marks · Slice 154** (group) | 151 |
| 71 | Mid-read · next part not voiced yet | 156 |
| 72 | Farsi · words with vowel marks | 186 |

To refresh: fetch the file again with `DesignSync get_file` and overwrite this copy, then
regenerate this table. Never call `DesignSync` to read visual values; read this copy.

Import prompt (verbatim):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+31.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 31.dc.html`

Also read these files the selection imports:
- `assets/three-little-pigs/3.jpg`
- `assets/three-little-pigs/5.jpg`

Implement: `Dino Reader Batch 31.dc.html`
```

## Batch 32 mock — `Dino Reader Batch 32.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 32.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+32.dc.html>).
- **Copied:** 2026-10-26, verbatim from `DesignSync get_file`. The dino is `icons/ios/AppIcon-1024-light.png`. The story pictures (`assets/good-enough/1.jpg`, `2.jpg`, `3.jpg`, `cover.jpg`, `assets/three-little-pigs/5.jpg`) are copied from the starter books under `content/dino/` (DesignSync truncates files over 256 KiB).
- **Covers:** Batch 32, Slice 155 (first-run intro: screens 73-77). Frame 73 is Batch 16's frame 18 with a new bottom; 77 extends frame 70 (Batch 31).

Open it in a text editor (`support.js` is not copied). Each screen is headed by a caption
`N · Title` with its behaviour in the note underneath.

| # | Screen | Line |
|---|---|---|
| — | **The way in** (group) | 23 |
| 73 | Ready to read · starts the intro | 27 |
| — | **The three steps · in this order** (group) | 48 |
| 74 | Step 1 · the free store | 53 |
| 75 | Step 2 · record your voice | 77 |
| 76 | Step 3 · add your own book | 99 |
| — | **Replay from grown-up mode** (group) | 122 |
| 77 | Grown-up settings · Show the intro again | 126 |

To refresh: fetch the file again with `DesignSync get_file` and overwrite this copy, then
regenerate this table. Never call `DesignSync` to read visual values; read this copy.

Import prompt (verbatim):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+32.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 32.dc.html`

Also read these files the selection imports:
- `assets/good-enough/1.jpg`
- `assets/good-enough/2.jpg`
- `assets/good-enough/3.jpg`
- `assets/good-enough/cover.jpg`
- `assets/three-little-pigs/5.jpg`
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: `Dino Reader Batch 32.dc.html`
```

## Batch 37 mock — `Dino Reader Batch 37.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 37.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+37.dc.html>).
- **Copied:** 2026-10-28, verbatim from `DesignSync get_file`; **refreshed 2026-10-29** (the grown-ups button
  replaces the shelf gear, the gear moves to grown-up mode, new frame 85b, the reading card capped at 30%).
  The refresh is Slice 189. Its imports are in this folder: `assets/good-enough/cover.jpg`,
  `assets/three-little-pigs/{3,5,cover}.jpg`, `assets/gingerbread-man/cover.jpg`,
  `assets/tortoise-and-hare/{6,cover}.jpg` (the 2026-10-29 additions were copied from the books in
  `content/dino/`, which is where the mock's pictures come from) and the dino `icons/ios/AppIcon-1024-light.png`.
- **Covers:** Slices 172-174 (gear and settings sub-pages, kid story languages, the calmer reading screen and back
  button): frames 79-94, Decisions 167-169; the 2026-10-29 changes to frames 79, 80, 85, 85b, 91 and 94 are Slice 189.

Open it in a text editor (`support.js` is not copied). Each screen is headed by a caption
`N · Title` with its behaviour in the note underneath.

| # | Screen | Line |
|---|---|---|
| 79 | Kids’ shelf · the grown-ups button | 27 |
| 80 | Settings · one row per setting | 45 |
| 81 | Language · only languages | 70 |
| 82a | Narrator voice · one per story language | 93 |
| 82b | Narrator voice · one language | 109 |
| 83 | Reading speed · a sub-page | 127 |
| 84 | About & updates | 147 |
| 85 | Grown-up mode · books only | 170 |
| 85b | Grown-up mode · a book’s ⋯ | 187 |
| 86 | Intro · last step · story languages — partly retired by Slice 278, see Batch 48 frame 186: the machine-translation copy | 215 |
| 87 | Settings · Story languages · downloading — partly retired by Slice 278, see Batch 48 frame 186: the machine-translation copy | 239 |
| 88 | Reading · the language button, open | 259 |
| 89 | Reading · switched to Farsi | 291 |
| 90 | Changing the app language · which one goes | 317 |
| 91 | Portrait · flat controls, a card capped at 30% that follows the reading | 347 |
| 94 | Kids’ shelf · Android back | 373 |
| 92 | Landscape · Picture + text | 401 |
| 93 | Landscape · Text only | 423 |

To refresh: fetch the file again with `DesignSync get_file` and overwrite this copy, then
regenerate this table. Never call `DesignSync` to read visual values; read this copy.

Import prompt (verbatim, 2026-10-29):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+37.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 37.dc.html`

Also read these files the selection imports:
- `assets/gingerbread-man/cover.jpg`
- `assets/good-enough/cover.jpg`
- `assets/three-little-pigs/5.jpg`
- `assets/three-little-pigs/cover.jpg`
- `assets/tortoise-and-hare/6.jpg`
- `assets/tortoise-and-hare/cover.jpg`
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: `Dino Reader Batch 37.dc.html`
```

## Batch 43 mock — `Dino Reader Batch 43 Grown-ups Corner.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 43 Grown-ups Corner.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+43+Grown-ups+Corner.dc.html>).
- **Copied:** 2026-11-01, verbatim from `DesignSync get_file`. Its imports were already in this folder
  (`assets/...` covers and pages, `icons/ios/AppIcon-1024-light.png`).
- **Covers:** Slices 228-229 (kids’ shelf vs grown-ups’ corner, Done/Continue top right), with Record frame 106 for Slices 230-231: frames 97-109, portrait.

Open it in a text editor (`support.js` is not copied). Each screen is headed by a caption
`N · Title` with its behaviour in the note underneath.

| # | Screen | Line |
|---|---|---|
| 97 | Kids’ shelf · the Grown-ups pill | 33 |
| 98 | Grown-ups’ corner — partly retired by Slice 278, see Batch 48 frame 186: the Add a book button | 50 |
| 99 | Corner · first run, before the hand-over | 75 |
| 100 | Book sheet · Read now first — partly retired by Slice 278, see Batch 48 frame 186: the Cast row | 100 |
| 101 | Intro · new first card · two places — partly retired by Slice 278, see Batch 48 frame 186: the add step (steps are now place, read, record, languages: "of 4") | 125 |
| 102 | The Grown-ups icon and pill | 182 |
| 103 | Grown-ups’ corner · dark (needs the theme pin lifted) | 198 |
| 104 | Store · opened from the intro · Continue | 227 |
| 105 | Settings · from the corner · Done | 242 |
| 106 | Record · Done, the words shown, where recordings live — partly retired by Slice 278, see Batch 48 frame 186: the words and note (replaced by Batch 48 frames 177-184, Slice 279) | 267 |
| 107 | Settings · Farsi · Done on the left | 285 |
| 108 | Sub-page · from the intro · Continue | 303 |
| 109 | Store · Continue · dark | 321 |

To refresh: fetch the file again with `DesignSync get_file` and overwrite this copy, then
regenerate this table. Never call `DesignSync` to read visual values; read this copy.

## Batch 44 mock — `Dino Reader Batch 44 Landscape.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 44 Landscape.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+44+Landscape.dc.html>).
- **Copied:** 2026-11-01, verbatim from `DesignSync get_file`. Its imports were already in this folder
  (`assets/...` covers and pages, `icons/ios/AppIcon-1024-light.png`).
- **Covers:** Slice 232 (landscape for every screen, phone 760×360 dp and tablet 1164×804 dp): frames 110-132; 113 and 115 replace Batch 37 frames 92 and 93.

Open it in a text editor (`support.js` is not copied). Each screen is headed by a caption
`N · Title` with its behaviour in the note underneath.

| # | Screen | Line |
|---|---|---|
| 110 | Kids’ shelf · one row of big covers | 45 |
| 113 | Reading · Picture + text (replaces 92) | 58 |
| 114 | Reading · Picture only | 80 |
| 115 | Reading · Text only (replaces 93) | 94 |
| 121 | Intro · two places | 110 |
| 122 | Gate · the sum | 127 |
| 123 | First run · language | 143 |
| 111 | Grown-ups’ corner · the header becomes a rail | 166 |
| 112 | Book sheet · a centred card | 182 |
| 116 | Record · picture, words, mic | 201 |
| 117 | Store · two columns | 218 |
| 118 | Settings · two panes | 229 |
| 119 | Cast · who’s speaking | 250 |
| 120 | Page editor · own book | 261 |
| 124 | Backup | 274 |
| 125 | Kids’ shelf · tablet | 291 |
| 126 | Grown-ups’ corner · tablet | 302 |
| 127 | Reading · Picture + text · tablet | 319 |
| 128 | Settings · tablet | 334 |
| 129 | Record · tablet | 350 |
| 130 | Intro step · tablet | 364 |
| 131 | Reading · night | 387 |
| 132 | Reading · Farsi | 401 |

To refresh: fetch the file again with `DesignSync get_file` and overwrite this copy, then
regenerate this table. Never call `DesignSync` to read visual values; read this copy.

Import prompt for both (verbatim, 2026-11-01):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+43+Grown-ups+Corner.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 43 Grown-ups Corner.dc.html`
- `Dino Reader Batch 44 Landscape.dc.html`

Also read these files the selection imports:
- `assets/gingerbread-man/cover.jpg`
- `assets/good-enough/cover.jpg`
- `assets/three-little-pigs/5.jpg`
- `assets/three-little-pigs/cover.jpg`
- `assets/tortoise-and-hare/6.jpg`
- `assets/tortoise-and-hare/cover.jpg`
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: the selected files
```

## Batch 45 mock — `Dino Reader Batch 45 Tablet Upright.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 45 Tablet Upright.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+45+Tablet+Upright.dc.html>).
- **Copied:** 2026-11-01, verbatim from `DesignSync get_file`. Its imports were already in this folder.
- **Covers:** Slice 234 (tablet held upright, 804×1164 dp; 150 is a 10" Android at 800×1280) and Slice 237's
  back cover (147-149): frames 133-150, drawn on top of Batch 44.

| # | Screen (from `data-screen-label`) | Line |
|---|---|---|
| 133 | Shelf tablet portrait | 46 |
| 134 | Reading tablet portrait | 57 |
| 135 | Reading text only tablet portrait | 74 |
| 137 | Gate tablet portrait | 90 |
| 138 | First run tablet portrait | 104 |
| 141 | Intro tablet portrait | 118 |
| 139 | Corner tablet portrait | 138 |
| 136 | Book sheet tablet portrait | 155 |
| 140 | Settings tablet portrait | 171 |
| 142 | Record tablet portrait | 190 |
| 143 | Store tablet portrait | 202 |
| 144 | Cast tablet portrait | 213 |
| 145 | Page editor tablet portrait | 225 |
| 146 | Backup tablet portrait | 231 |
| 147 | Back cover tablet portrait | 249 |
| 148 | Back cover phone landscape | 267 |
| 149 | Back cover Farsi phone | 284 |
| 150 | Reading Android 10 inch portrait | 302 |

## Batch 46 mock — `Dino Reader Batch 46 Windows.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 46 Windows.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+46+Windows.dc.html>).
- **Copied:** 2026-11-01, verbatim from `DesignSync get_file`. Its imports were already in this folder.
- **Covers:** Slice 235 (Dino on Windows as a real desktop window: size and minimum, title bar, keyboard,
  hover, focus, wheel, tooltips; layouts reuse Batches 44 and 45 by window size): frames 151-160, 1 dp = 1 px.

| # | Screen (from `data-screen-label`) | Line |
|---|---|---|
| 151 | Shelf Windows | 46 |
| 152 | Reading Windows | 61 |
| 153 | Reading Windows minimum | 90 |
| 154 | Shelf Windows maximised | 110 |
| 155 | Corner Windows | 130 |
| 156 | Book sheet Windows | 152 |
| 157 | Settings Windows | 174 |
| 158a | Gate hold Windows | 196 |
| 158b | Gate sum Windows | 203 |
| 159 | States Windows | 221 |
| 160 | Keyboard map Windows | 252 |

Open both in a text editor (`support.js` is not copied). To refresh: fetch again with `DesignSync get_file`,
overwrite the copy with a script, regenerate the table. Never call `DesignSync` to read visual values; read this copy.

Import prompt for both (verbatim, 2026-11-01):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+46+Windows.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 46 Windows.dc.html`
- `Dino Reader Batch 45 Tablet Upright.dc.html`

Also read these files the selection imports:
- `assets/good-enough/cover.jpg`
- `assets/three-little-pigs/5.jpg`
- `assets/three-little-pigs/cover.jpg`
- `assets/tortoise-and-hare/6.jpg`
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: the selected files
```

## Batch 47 mock — `Dino Reader Batch 47 Onboarding.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 47 Onboarding.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+47+Onboarding.dc.html>).
- **Copied:** 2026-11-04, verbatim from `DesignSync get_file` (the `content` field, saved by a script). Its imports
  were already in this folder (`assets/*/cover.jpg`, `assets/good-enough/1.jpg`; the app icon is `icons/ios/AppIcon-1024-light.png`,
  which the mock names `dino_reader_icons/ios/`).
- **Covers:** Slices 260-264 (store tags, the first-run book picker with no starter books, story languages fetching
  book audio, the AI page reader download, the shelf reorder mode): frames 161-176. Slice 265 (Git LFS) has no screens.

| # | Screen (from `data-screen-label`) | Line |
|---|---|---|
| 161 | Store tags | 36 |
| 162 | Tag badges | 52 |
| 163 | First books empty | 75 |
| 164 | First books picked | 98 |
| 165 | First books download | 121 |
| 166 | First books offline | 144 |
| 167 | Ready to read | 163 |
| 168 | Intro read 2 of 5 — partly retired by Slice 278, see Batch 48 frame 186: the counter reads "of 4" now | 182 |
| 169 | Story languages audio — partly retired by Slice 278, see Batch 48 frame 186: the reading-voice line | 211 |
| 170 | AI reader download — retired by Slice 278, see Batch 48 frame 186 | 241 |
| 171 | AI reader offline — retired by Slice 278, see Batch 48 frame 186 | 264 |
| 172 | Book sheet Reorder — partly retired by Slice 278, see Batch 48 frame 186: the Cast row | 289 |
| 173 | Reorder wiggle | 314 |
| 174 | Reorder drag | 327 |
| 175 | Corner new order | 341 |
| 176 | Reorder Windows | 368 |

Open it in a text editor (`support.js` is not copied). To refresh: fetch again with `DesignSync get_file`,
overwrite the copy with a script, regenerate the table. Never call `DesignSync` to read visual values; read this copy.

Import prompt (verbatim, 2026-11-04):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+47+Onboarding.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 47 Onboarding.dc.html`

Also read these files the selection imports:
- `assets/gingerbread-man/cover.jpg`
- `assets/good-enough/1.jpg`
- `assets/good-enough/cover.jpg`
- `assets/three-little-pigs/cover.jpg`
- `assets/tortoise-and-hare/cover.jpg`
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: `Dino Reader Batch 47 Onboarding.dc.html`
```

## Batch 48 mock — `Dino Reader Batch 48 Store Only and Recording.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 48 Store Only and Recording.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+48+Store+Only+and+Recording.dc.html>).
- **Copied:** 2026-11-06, verbatim from `DesignSync get_file` (the `content` field, saved by a script). Its imports
  were already in this folder (`assets/*/cover.jpg`, `assets/three-little-pigs/3.jpg` and `5.jpg`; the app icon is
  `icons/ios/AppIcon-1024-light.png`, which the mock names `dino_reader_icons/ios/`).
- **Covers:** Slices 276 (TestFlight update button, frame 187), 278 (store books only, frames 177, 185, 186) and 279
  (the recording flow, frames 178-184 and 188-191). Recording frames are drawn at iPhone 8 size, 375 x 667.

| # | Screen (caption) | Line |
|---|---|---|
| 177 | Book sheet · Record, no Cast | 34 |
| 178 | Record intro · one character at a time | 56 |
| 179 | Record intro · whole pages | 87 |
| 180 | One line · ready to record | 111 |
| 181 | One line · recording | 132 |
| 182 | One line · recorded | 153 |
| 183 | Whole page · part 1 of 2 | 174 |
| 184 | Windows · one line, keyboard | 192 |
| 188 | Corner and book sheet · "Your voice" shown | 218 |
| 189 | Record intro · recordings already there | 244 |
| 190 | Listen and change · a recorded line | 273 |
| 191 | New take · keep it or go back | 295 |
| 185 | Grown-ups' corner · no Add a book | 325 |
| 186 | What leaves the design copy | 349 |
| 187 | iPhone · TestFlight install | 376 |

Open it in a text editor (`support.js` is not copied). To refresh: fetch again with `DesignSync get_file`,
overwrite the copy with a script, regenerate the table. Never call `DesignSync` to read visual values; read this copy.

Import prompt (verbatim, 2026-11-06):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+48+Store+Only+and+Recording.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 48 Store Only and Recording.dc.html`

Also read these files the selection imports:
- `assets/gingerbread-man/cover.jpg`
- `assets/good-enough/cover.jpg`
- `assets/three-little-pigs/3.jpg`
- `assets/three-little-pigs/5.jpg`
- `assets/three-little-pigs/cover.jpg`
- `assets/tortoise-and-hare/cover.jpg`
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: `Dino Reader Batch 48 Store Only and Recording.dc.html`
```

## Batch 50 mock — `Dino Reader Batch 50 Sound States and Sonos.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 50 Sound States and Sonos.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+50+Sound+States+and+Sonos.dc.html>).
- **Copied:** 2026-11-07, verbatim from `DesignSync get_file`: the `content` fields were written to disk by a script
  that read them out of the session transcript, never retyped. The Sonos files it shows are in `sonos/` here
  (the PNGs decoded from base64): `service-logo-20/40/80/112/200/400.png`, `service-logo-40.svg`,
  `service-logo-400.svg`, `full-logo.svg`, `badge-40.svg`, `support-banner.png`, `support-banner.svg`.
  `assets/three-little-pigs/5.jpg` was already in this folder; it was not fetched again.
- **Covers:** Slice 290 (the reading page's sound line as a pill, frames 197-199; it restyles what Slice 285 C built
  with no mock) and Slice 283 (the Sonos brand images for the developer portal).
- **One word differs from the app:** the mock's caption calls the shipped line "Getting {0} sound for your books".
  The shipped English string `ShelfAudio_Getting` is "Getting {0} audio for your books" and other screens use it too;
  Slice 290 keeps the shipped words and takes only the shape and the "·" before the count.
- Batch 49 (`Dino Reader Batch 49 Recording per Language.dc.html`, Slice 286) is copied here; see its own section below.

| # | Screen (caption) | Line |
|---|---|---|
| — | Reading page sound line · Slice 285 C · phone (group) | 23 |
| 197 | Getting sound · 3 of 15 | 28 |
| 198 | Waiting for Wi-Fi | 60 |
| 199 | One part has no sound | 92 |
| — | Sonos brand assets · Slice 283 (group) | 123 |
| — | Service logo · square | 128 |
| — | Full logo · 20 px high · and badge · 40 × 40 | 143 |
| — | Support banner · 800 × 200 | 158 |

(Generated from the `data-screen-label` attributes and section captions.) Open it in a text editor (`support.js` is
not copied). To refresh: fetch again with `DesignSync get_file`, overwrite the copy with a script, regenerate the
table. Never call `DesignSync` to read visual values; read this copy.

Import prompt (verbatim, 2026-11-07):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+50+Sound+States+and+Sonos.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 50 Sound States and Sonos.dc.html`

Also read these files the selection imports:
- `assets/three-little-pigs/5.jpg`
- `sonos/badge-40.svg`
- `sonos/full-logo.svg`
- `sonos/service-logo-112.png`
- `sonos/service-logo-20.png`
- `sonos/service-logo-200.png`
- `sonos/service-logo-40.png`
- `sonos/service-logo-80.png`
- `sonos/support-banner.png`

Implement: `Dino Reader Batch 50 Sound States and Sonos.dc.html`
```

## Batch 51 mock — `Dino Reader Stats Dashboard.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Stats Dashboard.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Stats+Dashboard.dc.html>).
- **Copied:** 2026-11-08 (refresh adding the phone and tablet frames 201-204; first copy 2026-11-07), written by a script
  from the `content` field of the saved `DesignSync get_file` result (77,037 bytes). The icon it imports, `dino_reader_icons/ios/AppIcon-1024-light.png`,
  is already here as `icons/ios/AppIcon-1024-light.png`; the book covers it names (`assets/<book>/cover.jpg`) are the
  store covers, served live by `/api/stories/<id>/cover`.
- **Covers:** Slice 287 (the `/stats` page), with the listens and hours of Slices 288 and 289. The numbers in the mock
  are made up; "Dino 1.112" and "10 Nov 2026" are placeholders for Slice 288's real release.
- **Not to copy:** the mock loads Fredoka from Google Fonts for the canvas only; the page itself loads no web font and
  makes no third-party requests (its own caption says so).

| # | Screen (caption) | Line |
|---|---|---|
| 201 | Stats phone portrait · 390 × 844 | 26 |
| 202 | Stats phone landscape · 844 × 390 | 105 |
| 203 | Stats tablet portrait · 820 × 1180 | 172 |
| 204 | Stats tablet landscape · 1180 × 820 | 237 |
| 200 | Stats dashboard · /stats · desktop, 1280 wide | 305 |
| — | Notes: Filters | 423 |
| — | Notes: Numbers and chart | 424 |
| — | Notes: States | 426 |

(Generated from the `data-screen-label` attribute and the note captions.) Open it in a text editor (`support.js` is not
copied). The page's behaviour (periods, grouping rules, platform order and colours, sorting) is in the script at the end
of the file. To refresh: fetch again with `DesignSync get_file`, write the copy with a script, regenerate the table.

Import prompt (verbatim, 2026-11-07):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Stats+Dashboard.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Stats Dashboard.dc.html`

Also read these files the selection imports:
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: `Dino Reader Stats Dashboard.dc.html`
```

## Batch 49 mock — `Dino Reader Batch 49 Recording per Language.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Batch 49 Recording per Language.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+49+Recording+per+Language.dc.html>).
- **Copied:** 2026-11-07, verbatim: the `DesignSync get_file` result came back inline (not truncated) and a script took its
  `content` field from the session transcript, so nothing was retyped. Its imports were already in this folder
  (`assets/{gingerbread-man,good-enough,three-little-pigs}/cover.jpg`, `assets/three-little-pigs/3.jpg`).
- **Covers:** Slice 286 (recording per story language). Changes Batch 48 frames 178, 182, 188 and 189. iPhone 8, 375 x 667,
  English app with Farsi as a side language. The mock's two review questions take the drawn answers: the way you record
  (one character at a time / whole pages) and the ticks are kept per language; the intro opens on the app language.

| # | Screen (caption) | Line |
|---|---|---|
| 192 | Record intro · English, recordings there | 34 |
| 193 | Record intro · Farsi, nothing yet | 67 |
| 194 | One line · Farsi | 101 |
| 195 | Book sheet · your voice per language | 127 |
| 196 | Reading a recorded book in Farsi | 153 |

Open it in a text editor (`support.js` is not copied). To refresh: fetch again with `DesignSync get_file`,
overwrite the copy with a script, regenerate the table. Never call `DesignSync` to read visual values; read this copy.

Import prompt (verbatim, 2026-11-07):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Batch+49+Recording+per+Language.dc.html

Focus on these files (the whole project is readable):
- `Dino Reader Batch 49 Recording per Language.dc.html`

Also read these files the selection imports:
- `assets/gingerbread-man/cover.jpg`
- `assets/good-enough/cover.jpg`
- `assets/three-little-pigs/3.jpg`
- `assets/three-little-pigs/cover.jpg`

Implement: `Dino Reader Batch 49 Recording per Language.dc.html`
```

## 2026-11-08 redesign (Slice 325)

Two mocks from 2026-11-08 restyle every Dino Reader page left after Slice 278: the grown-ups corner as three tabs
(Books, Store, Settings) with the way back in the ink header, a kids shelf to match, and onboarding in the same
skeleton. One layout rule: **under 600 wide the tabs go along the bottom; 600 or wider the ink becomes a left rail.**
The grown-up file builds option 1c of `Dino Reader Grown-ups Corner Controls.dc.html`, copied here too because it links
to it. Its image `dino_reader_icons/ios/AppIcon-1024-light.png` is already here as `icons/ios/AppIcon-1024-light.png`; the
book covers are already in `assets/`. Open the files in a text editor (`support.js` is not copied). Refresh: fetch
again with `DesignSync get_file`, overwrite the copy with a script, regenerate the tables. Never call `DesignSync` to
read visual values; read these copies.

## Grown-ups corner and kids shelf — `Dino reader- 2026-11-08 grownup.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino reader- 2026-11-08 grownup.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+reader-+2026-11-08+grownup.dc.html>).
- **Copied:** 2026-11-08, verbatim from `DesignSync get_file`: the `content` field was written to disk by a script, never retyped.
- **Covers:** Slice 325: the corner on every screen size (P, L, T, W frames) and the kids shelf (K frames). It replaces the rails in frames 111, 126, 139 and 155.

| # | Screen (caption) | Line |
|---|---|---|
| — | Phones · portrait · actual size in pt / dp (group) | 23 |
| P1 | iPhone 8 | 30 |
| P2 | iPhone 15 Pro | 55 |
| P3 | iPhone 17 Pro Max | 79 |
| P4 | Pixel 10 Pro | 103 |
| P5 | Samsung S26 Ultra | 127 |
| — | 600 wide or more · the ink becomes a rail (group) | 151 |
| L1 | Phone landscape | 156 |
| T1 | Tablet portrait | 179 |
| T2 | Tablet landscape Store | 201 |
| W1 | Windows Settings | 223 |
| — | Kids’ shelf · phones · portrait (group) | 260 |
| K1 | Shelf iPhone 8 | 267 |
| K1-de | Shelf German | 283 |
| K2 | Shelf iPhone 15 Pro | 297 |
| K3 | Shelf iPhone 17 Pro Max | 310 |
| K4 | Shelf Pixel 10 Pro | 323 |
| K5 | Shelf Samsung S26 Ultra | 336 |
| — | Kids’ shelf · 600 wide or more · a cream rail (group) | 349 |
| KL1 | Shelf phone landscape | 354 |
| KT1 | Shelf tablet portrait | 370 |
| KT2 | Shelf tablet landscape | 384 |
| KW1 | Shelf Windows | 398 |

(Generated from the `data-screen-label` attributes and section captions.)

## Onboarding — `Dino Reader- 2026-11-08 Onboarding.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader- 2026-11-08 Onboarding.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader-+2026-11-08+Onboarding.dc.html>).
- **Copied:** 2026-11-08, verbatim from `DesignSync get_file`: the `content` field was written to disk by a script, never retyped.
- **Covers:** Slice 325: first run (language, first books, getting the books, offline, ready) and the intro (two places, read, record, languages) on every screen size.

| # | Screen (caption) | Line |
|---|---|---|
| — | The whole flow · iPhone 15 Pro · 393 × 852 (group) | 32 |
| O1 | Language phone | 37 |
| O2 | First books phone | 53 |
| O3 | Getting books phone | 71 |
| O4 | Offline phone | 90 |
| O5 | Ready phone | 106 |
| O6 | Intro two places phone | 121 |
| O7 | Intro read phone | 145 |
| O8 | Intro record phone | 161 |
| O9 | Intro languages phone | 177 |
| — | The tightest phone · iPhone 8 · 375 × 667 (group) | 195 |
| O1-8 | Language iPhone 8 | 202 |
| O2-8 | First books iPhone 8 | 222 |
| O6-8 | Intro iPhone 8 | 242 |
| — | Phone landscape · iPhone 15 Pro · 852 × 393 (group) | 265 |
| OL1 | Language landscape | 270 |
| OL2 | First books landscape | 290 |
| OL7 | Intro landscape | 309 |
| — | Tablet portrait · 820 × 1180 (group) | 326 |
| OT2 | First books tablet portrait | 331 |
| OT6 | Intro tablet portrait | 350 |
| OT9 | Intro languages tablet portrait | 372 |
| — | Tablet landscape · 1180 × 820 (group) | 391 |
| OTL1 | Language tablet landscape | 396 |
| OTL7 | Intro read tablet landscape | 414 |
| OTL8 | Intro record tablet landscape | 429 |
| — | Windows · 1100 × 760 (group) | 444 |
| OW2 | First books Windows | 449 |
| OW6 | Intro Windows | 471 |
| OW5 | Ready Windows | 496 |

(Generated from the `data-screen-label` attributes and section captions.)

## Corner controls options — `Dino Reader Grown-ups Corner Controls.dc.html`

- **Source:** Claude Design project `90615641-f298-4e21-8c50-ca2efbeaaebc`, file `Dino Reader Grown-ups Corner Controls.dc.html`
  (<https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+Reader+Grown-ups+Corner+Controls.dc.html>).
- **Copied:** 2026-11-08, verbatim from `DesignSync get_file`: the `content` field was written to disk by a script, never retyped.
- **Covers:** the four options for the corner's controls; the 2026-11-08 grown-up mock builds option 1c. Reference only.

| # | Screen (caption) | Line |
|---|---|---|
| 1a | Labelled doors | 27 |
| 1b | One dock | 52 |
| 1c | Corner tabs | 75 |
| 1d | Exit top left | 98 |

(Generated from the `data-screen-label` attributes and section captions.)

Import prompt (verbatim, 2026-11-08):

```
Use the claude_design MCP (https://api.anthropic.com/v1/design/mcp, auth via /design-login) to import this project:
https://claude.ai/design/p/90615641-f298-4e21-8c50-ca2efbeaaebc?file=Dino+reader-+2026-11-08+grownup.dc.html

Focus on these files (the whole project is readable):
- `Dino reader- 2026-11-08 grownup.dc.html`
- `Dino Reader- 2026-11-08 Onboarding.dc.html`

Also read these files the selection imports:
- `dino_reader_icons/ios/AppIcon-1024-light.png`

Implement: the selected files
```
