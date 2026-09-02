# Handoff: ReadTheStupidText — Windows tray panel + Android app

## Overview
**Windows.** This is the left-click control panel for **ReadTheStupidText**, a Windows 11
tray utility that reads selected or copied text aloud at a user-chosen speed. Left-clicking
the tray icon opens a small, borderless, always-on-top flyout docked above the taskbar, near
the system tray. The panel exposes every runtime control: play/pause with **±10s skip** and an
**elapsed/total timer**, a playback-speed slider (0.5×–2.0×, **hidden by default** — see below)
with six presets, voice selection, **Read a file** (.txt/.pdf/.docx) and **activity log**
buttons, three compact toggles — "auto-read on selection", "auto-read on copy", "launch at
startup" — and a global hotkey hint (`Ctrl+Win+R`). The header is **drag-to-move** and its
position is remembered (Batch 4, Slice 24).

**Android (Batch 6, MAUI).** A deliberately different face, specified in the same file: a
normal foreground app — **no tray, no always-on-top panel, no auto-read triggers, no
launch-at-startup** (none are possible on the platform, Decision 38). Its three input paths
are **type/paste**, **camera → on-device OCR**, and **file upload**. What carries over: the
same bundled Supertonic neural voices, the same 0.5–2.0× playback-rate model, and the same
chunked transport (±10s skip, elapsed/total timer).

**Compact rev:** the Windows settings no longer occupy full-width labelled rows. They
collapse into a **row of large square icon buttons** (auto-read selection · auto-read on copy ·
launch at startup), each with a **hover tooltip** naming the control and its state. The
**speed slider and presets are not shown** until the user taps the speed pill in the header.

This package documents **Option C, the "Media Card" direction**: a gradient "now reading"
header (app identity + live status + waveform + transport + speed pill) sitting above a clean,
tappable settings list. It is specified for both **light and dark** themes; the app should
follow the Windows system theme.

## About the Design Files
The file in this bundle (`Option C - Media Card.dc.html`) is a **design reference created in
HTML** — a prototype showing the intended look, layout, and content. It is **not production
code to ship directly.** The task is to **recreate this design in the app's real environment**
using its established patterns and libraries.

ReadTheStupidText is a native Windows 11 tray app. Per the project plan it is a .NET / WinUI 3
(Windows App SDK) application, so the panel should be rebuilt as a **WinUI 3 flyout/window**
using native Fluent controls (`ToggleSwitch`, `Slider`, `ComboBox`, `Button`,
`AcrylicBrush`, theme resources), not as HTML/CSS. If you are instead targeting a different
stack, choose the closest-fitting native or framework controls and apply the measurements
below. Use the HTML purely as the visual + behavioral source of truth.

The HTML opens in a browser; it uses a "canvas" wrapper. The top row shows the Windows light
and dark frames side by side above a mock taskbar — **only the floating panel (the rounded
card anchored bottom-right of each frame) is the Windows deliverable.** The desktop wallpaper,
taskbar, clock, and other tray icons are context only — do not build them. The bottom row
shows the three **Android** screens (type/paste home, camera OCR, voice picker); there the
whole phone frame minus the OS status bar is the deliverable, built as MAUI XAML pages.

## Fidelity
**High-fidelity.** Colors, typography, spacing, radii, and shadows are final. Recreate the
panel faithfully using the platform's native Fluent equivalents. Exact values are listed under
Design Tokens; where a native theme resource exists (e.g. system accent, acrylic, layer
fills), prefer the resource over a hardcoded hex so the panel tracks the OS theme/accent.

## The Panel

### Window / surface
- **Type:** borderless, always-on-top, no title bar; dismiss on click-away or Esc. Opens on
  left-click of the tray icon; closes on the X in the header, on losing focus, or on Esc.
- **Position:** anchored to the system tray — bottom-right of the work area, ~16px in from the
  right edge and ~12px above the taskbar. In the mock the taskbar is 48px tall and the panel
  sits 60px from the frame bottom (48 taskbar + 12 gap).
- **Width:** 376px. Height: hugs content (~300–320px); do not stretch.
- **Corner radius:** 8px. **Material:** acrylic / mica — semi-transparent blurred fill
  (`backdrop-blur ~50px`, saturate ~1.8). In WinUI use `AcrylicBrush` (or the flyout's default
  acrylic). **Border:** 1px hairline (`--cborder`). **Shadow:** large soft drop shadow
  (light: `0 18px 50px rgba(20,30,60,0.30)`; dark: `0 18px 50px rgba(0,0,0,0.55)`).

### Layout (top → bottom)
A vertical stack of two zones: a **gradient header** (fixed) and a **settings list** (rows).

#### 1. Gradient header  — `padding: 14px 16px 16px`
Full-bleed brand gradient `linear-gradient(135deg, #5B57E8, #3B82F6)`. A faint watermark of the
app glyph sits top-right (`120×120`, white, opacity 0.13, rotated −8°, overflow clipped).
All header text/icons are white. Contains, in order:

- **Title row** (space-between):
  - Left, stacked: eyebrow `NOW READING` (9.5px / 700 / letter-spacing 1.4px / white 0.7),
    then `ReadTheStupidText` (14px / 600 / white, margin-top 2px).
  - Right: **close button** — 28×28, radius 5px, `rgba(255,255,255,0.16)` fill, white 11px ✕
    (1.3 stroke). Hover: raise fill opacity. Action: hide panel.
- **Status row** (margin-top 13px): a 5-bar **waveform** (bars 3px wide, radius 2px, heights
  9/15/7/18/11px, white 0.9, `align-items:flex-end`, gap 3px) + status text
  `Reading selection from Notepad…` (12.5px / white 0.92). The status text is dynamic — see
  Interactions. When idle the waveform is static/flat and text reads e.g. `Ready` / `Paused`.
- **Transport row** (margin-top 15px, `display:flex; align-items:center; gap:9px`):
  - **Skip back 10s / Skip forward 10s** — 26×26 circles, `rgba(255,255,255,0.14)` fill, white
    double-chevron glyph, flanking the play button (back on the left, forward on the right).
    Tooltips: "Back 10s / Forward 10s — Nearest chunk boundary". Best-effort chunk-boundary
    jump, **not** sample-accurate seek (Decision 32): clamp backward at 0 and forward at the
    furthest synthesized point.
  - **Play/Pause button** — 40×40 circle, `rgba(255,255,255,0.20)` fill, white glyph. Shows a
    **pause** glyph (two 3.2-wide bars) while playing, a **play** triangle while paused.
  - **Progress + timer column** — `flex:1`, `display:flex; flex-direction:column; gap:5px`:
    - **Progress / scrub bar** — 4px tall track radius 2px (`rgba(255,255,255,0.30)`),
      filled portion white (50% in mock), 16px white circular thumb (shadow
      `0 1px 4px rgba(0,0,0,.3)`).
    - **Elapsed / total timer** — a space-between row under the bar, 10px, tabular numerals,
      `rgba(255,255,255,0.82)`: elapsed on the left, total on the right. Format `mm:ss`,
      minutes not zero-padded or capped (`125:33` is valid); the total reads **`--:--`** until
      every chunk of the current read has been synthesized (Decision 33).
  - **Speed pill** — text `1.25×` (12px / 700 / white) + a small chevron, in a pill
    `rgba(255,255,255,0.20)`, padding 4px 8px, radius 11px, 1px white-30% border. It is the
    **toggle for the speed slider**: tapping it reveals/hides the slider; chevron flips. Default
    state = collapsed (slider hidden).
  - **Speed slider (revealed)** — appears as a new row directly under the transport row
    (`margin-top 14px`) only when the speed pill is active: `0.5×` label · white track (4px,
    50% fill for 1.25×) with an 18px white thumb + 7px accent inner dot · `2×` label.
  - **Speed presets (revealed)** — directly below the slider (`margin-top 10px`,
    `flex; gap:6px`): six equal-width buttons **0.5× · 1× · 1.25× · 1.5× · 1.75× · 2×** that
    set the rate on tap. Each: `flex:1; height:26px; radius:7px`, 11px/600 white text on a
    `rgba(255,255,255,0.16)` fill (hover `0.30`). The **current** preset is solid white with
    blue text (`#2f6ae0`, 700). Slider and presets reflect the same value.

#### 2. Voice row + file & log actions  — `padding: 6px 8px 8px`
A single row: `display:flex; align-items:center; gap:12px; padding:10px`.
- Label `Voice` (13px `--text`) — no leading icon tile: a mic glyph here reads as a tappable
  button when it isn't one.
- **Voice dropdown** (`margin-left:auto; flex:1`, so it fills the row): `Cocytus` +
  chevron-down, in a `--control` fill / `--cborder` 1px / radius 6px chip. Opens the bundled
  neural-voice picker (`ComboBox` in WinUI).
#### 2b. Actions row — `padding: 9px 10px`, own hairlines above and below
The two window-opening actions are **labelled buttons on their own row**, not small icons —
they are easy to miss otherwise. Two side-by-side buttons, each `flex:1; height:38px;
radius:8px`, `--control` fill, 1px `--cborder2`, icon in `--accent2` + 12.5px/600 `--text`
label, 8px gap:
- **Read a file** — upload glyph. Opens a `FileOpenPicker` filtered to `.txt` / `.pdf` /
  `.docx`; picking a file **supersedes** any in-progress read (Decision 34).
- **Activity log** — log-lines glyph. Opens the separate activity-log window, which also
  carries the per-read timing diagnostics and its own **open-logs-folder** button (Slice 21).

Both are **actions, not toggles** — they never show an on/off state, which is why they are
visually separated from the Controls row below.

#### 3. Controls — compact icon toggles  — `padding: 0 10px 8px` (under a `CONTROLS` eyebrow)
Three icon buttons in a `flex; gap:8px` row, each `flex:1; height:52px; radius:10px` — shorter
than the actions row is wide, so the labelled actions stay the dominant element.
Each toggles an on/off setting and has a hover tooltip. **ON** = `--accent` fill + white icon;
**OFF** = `--card` fill, 1px `--stroke`, `--text2` icon.
1. **Auto-read on selection** (ON) — **select frame (rounded corner-brackets with tick marks)
   enclosing a two-wave “))”** sound glyph. Tooltip: "Auto-read selection · on — Reads aloud
   as you select".
2. **Auto-read on copy** (ON) — **copy pages (two stacked sheets) enclosing a two-wave “))”**
   sound glyph. Tooltip: "Auto-read on copy · on — Reads aloud when you copy".
3. **Launch at startup** (OFF) — **rocket** glyph (deliberately *not* a power/shutdown symbol,
   which the prior build confused it with). Tooltip: "Launch at startup · off — Start with
   Windows, in the tray".

#### Tooltips
Every icon button (the three toggles + the activity-log button) and the speed pill show a small
dark tooltip on hover (`#1f2330`, white ~10.5px, radius 6px, soft shadow, little caret) naming
the control and, for toggles, its current state. In WinUI use `ToolTipService.ToolTip`.

- **Hotkey footer** — `padding: 8px 10px 4px`, space-between. Left: `Hotkey fallback`
  (11px / `--text2`). Right: three kbd chips `Ctrl` `Win` `R` separated by `+` — each chip:
  `--text` color on `--kbd` fill, 1px `--cborder`, radius 4px, padding 2px 6px, 11px.

### Toggle switch spec
Track 40×21, radius 11px. Knob 13px circle.
- **ON:** track filled with `--accent`, knob white, knob aligned right (3px inset).
- **OFF:** track transparent with 1.5px `--text2` border, knob `--text2`, aligned left.

### Slider / scrub bar spec (header progress + any speed slider)
Track 4px, radius 2px. Thumb circle with `0 1px 4px rgba(0,0,0,.35)` shadow. On the header
progress bar the thumb is 16px solid white over a white fill. (If you add a dedicated speed
slider elsewhere, use a 20px thumb with an inner accent dot over an accent fill, matching the
Fluent slider in sibling options.)

## Interactions & Behavior
- **Open:** left-click tray icon → panel fades/scales in (Fluent flyout transition, ~150ms,
  ease-out) anchored above the tray.
- **Close:** the ✕ button, or left-clicking the tray icon again. The panel stays
  **pinned-topmost** — no light-dismiss, no Esc dismiss (Decision 20).
- **Move:** the gradient header is a **drag handle** (`cursor:grab`) — pointer-drag moves the
  whole `AppWindow`; the position is persisted and restored, clamped to the work area
  (Slice 24). Child controls swallow their own pointer input, so a drag only starts on the
  header's empty areas.
- **Play/Pause:** toggles speech. Glyph swaps play⇄pause. While playing, status text shows what
  is being read (`Reading selection from Notepad…`, `Reading clipboard…`) and the waveform
  animates; while paused/idle it stops and text shows `Paused` / `Ready`.
- **Scrub bar:** reflects progress through the current utterance; dragging seeks (best-effort
  for TTS — may resync at sentence boundaries).
- **Skip ±10s:** jumps to the nearest chunk boundary at/after `elapsed ± 10s` — accurate to
  roughly one chunk, never sample-accurate. Backward clamps at 0; forward clamps at the
  furthest synthesized point (can't skip into audio that doesn't exist yet).
- **Timer:** `elapsed/total` ticks about once a second while reading. The total stays `--:--`
  until the last chunk of the read finishes synthesizing, then appears at once.
- **Read a file:** picker → text is extracted (`.txt` plain, `.pdf` via PdfPig, `.docx` via
  OpenXml) → feeds the normal read pipeline and supersedes the current read. The activity-log
  Source column shows the file name.
- **Speed pill / slider / presets:** the pill toggles the slider; the slider and the six
  preset buttons (0.5× / 1× / 1.25× / 1.5× / 1.75× / 2×) both set playback rate and stay in
  sync. Current value persists across sessions. Changing speed mid-read applies immediately.
- **Voice row:** opens the picker of bundled Supertonic (Overlord-named) voices; selection
  persists. A change mid-read applies at the **next chunk** — the current sentence finishes in
  the old voice, nothing already heard repeats and no unheard text is skipped (Slice 23).
- **Auto-read selection (toggle):** when ON, selecting text anywhere starts reading it aloud
  automatically. Persists.
- **Launch at startup (toggle):** when ON, registers the app to start with Windows minimized to
  tray. Persists (writes the Run registry key / startup task).
- **Hotkey:** `Ctrl+Win+R` is a global fallback to trigger read-aloud of the current selection
  even when the panel is closed.
- **Theme:** follows Windows light/dark. Accent ideally follows the system accent color;
  default brand accent is the blue below.
- **Hover states:** header close button and list rows lighten their background on hover
  (`--hover`); toggles/slider use standard Fluent hover.

## State Management
Persisted user settings (registry / app settings store):
- `playbackSpeed: number` (default 1.25)
- `voiceId: string` (default `Momonga`, the bundled Supertonic default)
- `panelPosition: {x, y} | null` (device pixels; null → default bottom-right pin)
- `autoReadSelection: boolean` (default true)
- `autoReadOnCopy: boolean` (default true)
- `launchAtStartup: boolean` (default false)

Transient runtime state:
- `isPlaying: boolean`
- `statusText: string` (source being read, e.g. "Reading clipboard…")
- `progress: number` 0–1 (utterance progress)
- `timing: { elapsed: TimeSpan, total: TimeSpan? }` (total null until fully synthesized)
- `panelOpen: boolean`

Triggers: tray left-click → `panelOpen=true`; play/pause button → `isPlaying`; speech engine
callbacks → `progress`, `statusText`, and auto-set `isPlaying=false` on completion.

## Design Tokens

### Color — Light
| Token | Value | Use |
|---|---|---|
| panel surface | `rgba(251,251,253,0.82)` | acrylic flyout fill |
| text | `rgba(0,0,0,0.89)` | primary text |
| text2 | `rgba(0,0,0,0.56)` | secondary / subtitles |
| text3 | `rgba(0,0,0,0.40)` | chevrons / hints |
| stroke | `rgba(0,0,0,0.08)` | row dividers |
| card | `rgba(255,255,255,0.60)` | icon tile fill |
| cborder | `rgba(0,0,0,0.10)` | panel + chip border |
| accent | `#3B6FE3` | toggle ON, fills |
| accent2 | `#2A57C0` | list-row icons |
| kbd | `rgba(0,0,0,0.05)` | kbd chip fill |
| hover | `rgba(0,0,0,0.06)` | row/button hover |
| shadow | `0 18px 50px rgba(20,30,60,0.30)` | panel shadow |

### Color — Dark
| Token | Value | Use |
|---|---|---|
| panel surface | `rgba(43,43,47,0.80)` | acrylic flyout fill |
| text | `rgba(255,255,255,0.92)` | primary text |
| text2 | `rgba(255,255,255,0.58)` | secondary / subtitles |
| text3 | `rgba(255,255,255,0.40)` | chevrons / hints |
| stroke | `rgba(255,255,255,0.08)` | row dividers |
| card | `rgba(255,255,255,0.045)` | icon tile fill |
| cborder | `rgba(255,255,255,0.10)` | panel + chip border |
| accent | `#5B8DF0` | toggle ON, fills |
| accent2 | `#9CBEFF` | list-row icons |
| kbd | `rgba(255,255,255,0.08)` | kbd chip fill |
| hover | `rgba(255,255,255,0.08)` | row/button hover |
| shadow | `0 18px 50px rgba(0,0,0,0.55)` | panel shadow |

### Brand gradient (header, both themes)
`linear-gradient(135deg, #5B57E8, #3B82F6)` — also used for the brand mark. White content on top.

### Typography
- **Family:** `Segoe UI Variable` / `Segoe UI` (Windows system font). Use the native font.
- Eyebrow `NOW READING`: 9.5px / 700 / letter-spacing 1.4px
- App title (header): 14px / 600
- Status text: 12.5px / 400
- Speed pill: 12px / 700
- Row title: 13px / 400–500
- Row subtitle: 11.5px / 400
- Footer label / kbd chips: 11px

### Spacing & shape
- Panel width 376px, radius 8px, border 1px.
- Header padding `14px 16px 16px`; list padding `6px 8px 8px`; row padding 10px; row gap 12px.
- Icon tile 32×32 / radius 7px. Close btn 28×28 / radius 5px. Play btn 40×40 circle.
- Toggle 40×21 / radius 11px, knob 13px. Scrub track 4px / radius 2px, thumb 16px.
- kbd chip radius 4px, padding 2px 6px.
- Dividers: 1px, inset 10px left/right.

## Android app (Batch 6 · MAUI)

Built as MAUI XAML pages in `src/ReadTheStupidText.Mobile` (`net10.0-android`), reusing
`Domain` + `Application` unchanged. **Not** a port of the tray panel — a different face for a
different platform.

**What is deliberately absent** (Decision 38): the auto-read-on-selection and auto-read-on-copy
toggles, launch-at-startup, the global hotkey, the tray icon, always-on-top/drag behavior, and
(for this release) any activity-log or on-disk diagnostics screen. None have an Android
equivalent — do not invent one.

**Shared with Windows:** the ten bundled Supertonic voices and `SupertonicVoiceTable`, the
`PlaybackRate` 0.5–2.0× model and its six presets, chunked synthesis with ±10s skip and the
`mm:ss` / `--:--` timer, and the same document extractors for uploads.

### Screen 1 — Type or paste (home)
- **Status bar** 30px, brand `#5B57E8` (light content).
- **App bar** — brand gradient, `padding 14px 18px 16px`, glyph watermark at 13% opacity,
  rotated −8°. Title "Read The Stupid Text" 16px/600; sub-line `Momonga · 1.25×` 11.5px at
  78% white. Right: a 30px circular mic button → opens the voice picker (Screen 3).
- **Editor card** — fills available height, white, radius 14px, 1px `rgba(0,0,0,.09)`,
  padding 14px, text 13.5px / line-height 1.55. This is the paste/type target; a caret shows
  focus. Empty state: "Paste or type text…".
- **Transport card** — white, radius 14px. Row: 34px circular skip-back · **52px gradient
  play/pause** (shadow `0 4px 14px rgba(91,87,232,.38)`) · 34px skip-forward · progress bar
  (`#5B57E8` fill, 14px thumb) with the `00:03 / --:--` timer beneath.
- **Speed presets** — a 6-up row of 28px pills inside the same card; active pill solid
  `#5B57E8` with white 700 text, the rest `rgba(0,0,0,.05)`. No hidden slider on mobile: the
  presets *are* the speed control (a 0.05-step slider is a poor touch target).
- **Bottom nav** — white, 1px top divider, three tabs (**Type** · **Camera** · **File**), active
  tab in `#5B57E8`. These are the app's three input paths; there is nothing else in the nav.
  All hit targets ≥ 44px.

### Screen 2 — Camera → OCR (single shot)
- Full-bleed dark viewfinder; a 2px `rgba(91,150,255,.95)` detection frame with a heavy scrim
  outside it, and a small `TEXT FOUND` chip when ML Kit sees text. Hint pill at the top:
  "Hold steady over the text".
- **Capture bar** — `#0b0d12`: 42px gallery button (pick an existing photo), 68px shutter
  (white ring + solid white core), 42px flash/torch button.
- **Result bar** — white: one line explaining the flow plus a pill **Read** button
  (`#5B57E8`, 38px tall). Capture → on-device ML Kit extraction → the text flows into the same
  `SpeechTextChunker` / `ReadAloudService` pipeline as typed text.
- v1 is **single-shot only** — no live scanning overlay, no cloud fallback (Decision 40).

### Screen 3 — Voice picker
- Brand app bar with a back chevron; sub-line "Bundled offline · no download".
- Two grouped white cards under `MALE` / `FEMALE` eyebrows listing the ten Overlord voices;
  each row is a 30px initial avatar + name + a **preview play** affordance. The selected row
  (Momonga, "Default") tints `rgba(91,87,232,.07)` with a `#5B57E8` check and a filled avatar.
- Footer note: a voice change applies at the next chunk — the current sentence finishes in the
  old voice.

### Mobile tokens (delta from the Windows set)
| Token | Value | Use |
|---|---|---|
| brand | `#5B57E8` | app bar, active nav/preset, progress fill |
| gradient | `linear-gradient(135deg,#5B57E8,#3B82F6)` | app bar, play button |
| page | `#f4f5f9` | page background |
| surface | `#fff` + 1px `rgba(0,0,0,.09)`, radius 14px | cards |
| text | `#1f2027` / `rgba(0,0,0,.5)` | primary / secondary |
| camera chrome | `#0b0d12`, detection `#5B96FF` | capture screen |

Type: the platform font (Roboto). Body 13.5px, labels 11–11.5px, titles 15.5–16px — map to
MAUI/Material type styles rather than hard pixel values where the platform provides them.

## Assets
- `assets/glyph.png` — app glyph (glasses mark); used as the header watermark (recolor white
  via `brightness(0) invert(1)`) and as the tray icon.
- `assets/app-tile.png` — full app logo tile (used in sibling options' headers).
These were provided by the user. In the real app, use the packaged app icon resources.
Header/list icons are simple line SVGs (speaker, text-cursor, power, play/pause, chevron) —
replace with the codebase's Fluent icon set (Segoe Fluent Icons glyphs).

## Files
- `Option C - Media Card.dc.html` — the design reference. Top row: the Windows panel (light +
  dark frames) — only the floating rounded panel is in scope. Bottom row: the three Android
  screens.
- `assets/glyph.png`, `assets/app-tile.png` — brand assets.

For reference, the sibling directions (Option A "Compact stack", Option B "Hero transport")
live in `Tray Control Panel.dc.html` in the parent project and share all the tokens above.
