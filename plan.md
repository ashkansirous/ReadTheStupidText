# Plan: Binders — Windows 11 read-aloud tray app

## Context

The user wants a lightweight Windows 11 utility ("Binders") that reads text
aloud at a user-chosen speed (1x / 1.25x / 1.5x / 1.75x / 2x), with a tiny
control beside the clock for pause/resume and speed, that auto-reads selected
text where possible and falls back to a global hotkey for apps that don't
expose their selection (terminals, CLI, Claude Code). It must run at startup
and ship through the Microsoft Store. The shape was settled in `scope.md`;
this plan turns it into ordered, shippable vertical slices.

## Decisions

1. **Framework:** C#/.NET + WinUI 3 (Windows App SDK), packaged as MSIX —
   keeps full Win32 capabilities (tray, global hotkey, cross-app read) while
   staying Store-installable. Pure UWP rejected (sandbox blocks all three).
2. **Speech:** WinRT `Windows.Media.SpeechSynthesis` rendered through
   `MediaPlayer` with `PlaybackRate` for live, pitch-corrected speed; system
   default Win11 voice in v1.
3. **Trigger:** UI Automation `TextPattern` selection monitoring as the
   primary auto-read path, **plus** a global hotkey (default `Ctrl+Win+R`)
   that simulates copy + reads the clipboard as the fallback.
4. **Control surface:** notification-area icon via `H.NotifyIcon.WinUI` —
   left-click flyout (Play/Pause + five speed buttons), right-click context
   menu (Pause, Enable/Disable, Settings, Quit). Global hotkey also toggles
   pause/resume.
5. **Startup:** packaged `StartupTask` (Windows App SDK), user-toggleable,
   starts minimized to tray.
6. **Persistence:** last speed + enabled state in
   `ApplicationData.Current.LocalSettings`; default speed 1x.
7. **Distribution:** Microsoft Store; design within Store/restricted-capability
   rules from day one.
8. **Naming:** repo stays `ReadTheStupidText`; product display name "Binders".
9. **Toolchain:** before writing any Windows App SDK / WinUI / H.NotifyIcon
   code, confirm current stable versions and APIs via context7
   (`/microsoft/windowsappsdk`, `/microsoft/winui`, `H.NotifyIcon`).

## Changes

Ordered as vertical slices — each is end-to-end and independently runnable.

- [x] **Slice 0 — Project scaffold.** ([#3](https://github.com/ashkansirous/ReadTheStupidText/issues/3)) Create the WinUI 3 packaged (single-project
      MSIX) app via `dotnet new` / template (version confirmed through
      context7). Add `.gitignore` (VisualStudio + OS noise), `README.md`,
      `CLAUDE.md` (project conventions), and `AGENTS.md` (`@CLAUDE.md`). App
      boots to an empty window. Establish the layered folder structure
      (`Domain` / `Application` / `Infrastructure` / `App`/UI).
- [x] **Slice 1 — Clipboard-read on hotkey at chosen speed (smallest E2E).**
      ([#4](https://github.com/ashkansirous/ReadTheStupidText/issues/4)) Register the global hotkey; on press, read current clipboard text aloud
      via `SpeechSynthesis` + `MediaPlayer`. Tray icon present with a flyout
      exposing Play/Pause and the five speed buttons; speed changes apply live
      via `PlaybackRate`. This single slice proves TTS + speed + tray + hotkey.
- [ ] **Slice 2 — Hotkey copies the current selection.** ([#5](https://github.com/ashkansirous/ReadTheStupidText/issues/5)) Extend the hotkey to
      simulate copy (send `Ctrl+C`) before reading, so the user can select in
      any app (incl. terminals/CLI/Claude Code) and have it read aloud. Persist
      last-used speed + enabled state to `LocalSettings`.
- [ ] **Slice 3 — Auto-read on selection (UIA).** ([#6](https://github.com/ashkansirous/ReadTheStupidText/issues/6)) Add UI Automation
      `TextPattern` monitoring so selecting text in supporting apps
      (Notepad, modern apps, most browsers) auto-reads without the hotkey.
      Enable/Disable toggle in the tray menu gates this behavior. Hotkey
      remains the fallback for non-UIA apps.
- [ ] **Slice 4 — Launch at startup.** ([#7](https://github.com/ashkansirous/ReadTheStupidText/issues/7)) Declare and wire a packaged
      `StartupTask`; app starts minimized to the tray; expose the toggle in
      the tray context menu.
- [ ] **Slice 5 — Store packaging & CI.** ([#8](https://github.com/ashkansirous/ReadTheStupidText/issues/8)) Finalize the MSIX manifest
      (identity, capabilities + restricted-capability justification), signing,
      and a GitHub Actions workflow that builds + packages the MSIX artifact.
      Prepare Store submission assets.

## Out of Scope

- Voice picker, multilingual voice selection, or any voice tuning beyond
  playback rate (deferred to a later settings pass).
- Reading from non-UIA apps *without* the hotkey fallback.
- Non-Store / sideload as a primary distribution channel (MSIX may be
  sideloaded for testing, but Store is the target).
- A full settings window beyond the tray flyout/context menu.
- Pure UWP packaging.

## Verification

- **Slice 0:** `dotnet build` succeeds; app launches to an empty window;
  scaffolding files present.
- **Slice 1:** copy text manually, press the hotkey → text is spoken; click
  each speed button → speech speed changes live; Play/Pause works.
- **Slice 2:** select text in Notepad, a terminal, a browser, and Claude Code,
  press the hotkey → each is read aloud; restart app → last speed/enabled
  state restored.
- **Slice 3:** with auto-read enabled, select text in Notepad/browser →
  reads automatically; select in a terminal → does not auto-read but hotkey
  still works; Disable toggle stops auto-read.
- **Slice 4:** enable startup, reboot → app is running in the tray; disable →
  it is not.
- **Slice 5:** CI run produces a signed MSIX; install the MSIX on a clean
  Win11 machine and confirm all of the above; Store certification dry-run
  passes capability checks.
- Manual UI checks driven through the running app; no browser E2E harness
  applies to a native tray app.
