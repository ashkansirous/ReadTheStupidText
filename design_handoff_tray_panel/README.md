# Handoff: ReadTheStupidText — Tray Control Panel (Option C, "Media Card")

## Overview
This is the left-click control panel for **ReadTheStupidText**, a lightweight Windows 11
tray utility that reads selected or copied text aloud at a user-chosen speed. Left-clicking
the tray icon opens a small, borderless, always-on-top flyout docked above the taskbar, near
the system tray. The panel exposes every runtime control: play/pause, a playback-speed slider
(0.5×–2.0×, **hidden by default** — see below), voice selection, and three compact toggles —
"auto-read on selection", "auto-read on copy", and "launch at startup" — plus an **activity
log** button and a global hotkey hint (`Ctrl+Win+R`).

**Compact rev (this version):** the settings no longer occupy full-width labelled rows. They
collapse into a single **row of small icon buttons** (auto-read selection · auto-read on copy ·
launch at startup · activity log), each with a **hover tooltip** naming the control and its
state. The **speed slider is not shown** until the user taps the speed pill in the header. Both
changes exist to keep the flyout short — the previous build was unnecessarily tall.

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

The HTML opens in a browser; it uses a "canvas" wrapper to show the light and dark frames
side by side above a mock taskbar. **Only the floating panel (the rounded card anchored
bottom-right of each frame) is the deliverable.** The desktop wallpaper, taskbar, clock, and
other tray icons are context only — do not build them.

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
- **Transport row** (margin-top 15px, `display:flex; align-items:center; gap:12px`):
  - **Play/Pause button** — 40×40 circle, `rgba(255,255,255,0.20)` fill, white glyph. Shows a
    **pause** glyph (two 3.2-wide bars) while playing, a **play** triangle while paused.
  - **Progress / scrub bar** — `flex:1`, 4px tall track radius 2px (`rgba(255,255,255,0.30)`),
    filled portion white (50% in mock), with a 16px white circular thumb (shadow
    `0 1px 4px rgba(0,0,0,.3)`). Represents read-through progress; draggable to scrub.
  - **Speed pill** — text `1.35×` (12px / 700 / white) + a small chevron, in a pill
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

#### 2. Voice row + activity log  — `padding: 6px 8px 8px`
A single row: `display:flex; align-items:center; gap:12px; padding:10px`.
- **Leading icon tile:** 32×32, radius 7px, fill `--card`, 1px `--stroke` border, mic glyph in
  `--accent2`.
- Label `Voice` (13px `--text`).
- **Voice dropdown** (pushed right, `margin-left:auto`): `Cocytus` + chevron-down, in a
  `--control` fill / `--cborder` 1px / radius 6px chip. Opens the installed-voice picker
  (`ComboBox` in WinUI).
- **Activity-log button** (immediately right of the dropdown): 34×34, radius 7px, `--card`
  fill, 1px `--stroke`, log-lines glyph in `--accent2`. This is an **action, not a toggle** —
  it opens a separate read-aloud history window. Tooltip: "Activity log — opens the read-aloud
  history".

#### 3. Controls — compact icon toggles  — `padding: 0 10px 8px` (under a `CONTROLS` eyebrow)
Three near-square icon buttons in a `flex; gap:8px` row, each `flex:1; height:70px; radius:11px`.
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
- **Close:** X button, click-away (lost focus), or Esc → fade out.
- **Play/Pause:** toggles speech. Glyph swaps play⇄pause. While playing, status text shows what
  is being read (`Reading selection from Notepad…`, `Reading clipboard…`) and the waveform
  animates; while paused/idle it stops and text shows `Paused` / `Ready`.
- **Scrub bar:** reflects progress through the current utterance; dragging seeks (best-effort
  for TTS — may resync at sentence boundaries).
- **Speed pill / slider / presets:** the pill toggles the slider; the slider and the six
  preset buttons (0.5× / 1× / 1.25× / 1.5× / 1.75× / 2×) both set playback rate and stay in
  sync. Current value persists across sessions. Changing speed mid-read applies immediately.
- **Voice row:** opens picker of installed Windows TTS voices; selection persists.
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
- `voiceId: string` (default first available natural voice, e.g. Microsoft Aria)
- `autoReadSelection: boolean` (default true)
- `autoReadOnCopy: boolean` (default true)
- `launchAtStartup: boolean` (default false)

Transient runtime state:
- `isPlaying: boolean`
- `statusText: string` (source being read, e.g. "Reading clipboard…")
- `progress: number` 0–1 (utterance progress)
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

## Assets
- `assets/glyph.png` — app glyph (glasses mark); used as the header watermark (recolor white
  via `brightness(0) invert(1)`) and as the tray icon.
- `assets/app-tile.png` — full app logo tile (used in sibling options' headers).
These were provided by the user. In the real app, use the packaged app icon resources.
Header/list icons are simple line SVGs (speaker, text-cursor, power, play/pause, chevron) —
replace with the codebase's Fluent icon set (Segoe Fluent Icons glyphs).

## Files
- `Option C - Media Card.dc.html` — the design reference (light + dark frames). Open in a
  browser to inspect. Only the floating rounded panel is in scope.
- `assets/glyph.png`, `assets/app-tile.png` — brand assets.

For reference, the sibling directions (Option A "Compact stack", Option B "Hero transport")
live in `Tray Control Panel.dc.html` in the parent project and share all the tokens above.
