# Scope

## Goals

- Provide a Windows 11 tray app ("ReadTheStupidText") that reads selected or copied text aloud on demand.
- Let the user pick reading speed from 1x, 1.25x, 1.5x, 1.75x, and 2x, applied live.
- Auto-read text on selection where the app exposes it, with a global-hotkey fallback for apps that don't (terminals, CLI, Claude Code).
- Give the user a tiny, always-available control beside the clock to pause/resume and change speed.
- Run automatically at Windows startup, minimized to the tray.
- Ship through the Microsoft Store.

## Approach

- **Stack:** C#/.NET + WinUI 3 (Windows App SDK), packaged as MSIX so it keeps full Win32 capabilities (tray icon, global hotkey, cross-app text read) while still being Store-installable.
- **Speech:** WinRT `Windows.Media.SpeechSynthesis` for synthesis, played via `MediaPlayer` with `PlaybackRate` for pitch-corrected, live speed changes using the system default Win11 voice.
- **Control surface:** notification-area (system tray) icon via `H.NotifyIcon.WinUI` — left-click opens a small flyout with Play/Pause and the five speed buttons; right-click gives a context menu (Pause, Enable/Disable, Settings, Quit). Global hotkeys also toggle pause/resume.
- **Selection capture:** UI Automation `TextPattern` selection monitoring as the primary auto-read path, plus a global hotkey (e.g. `Ctrl+Win+R`) that simulates copy and reads the clipboard as a fallback for apps without UIA text support.
- **Startup:** packaged `StartupTask` (Windows App SDK), declared in the manifest and user-toggleable, starting minimized to tray.
- **Persistence:** last-used speed and enabled state stored in `ApplicationData.Current.LocalSettings`; default speed is 1x.

### First vertical slice

A tray app that **reads the current clipboard aloud at a chosen speed on a global hotkey** — the smallest end-to-end change that proves TTS + speed control + tray flyout + hotkey together. Subsequent slices follow the same end-to-end pattern: (2) UIA auto-read on selection, then (3) `StartupTask` launch-at-boot, then (4) Store packaging/submission polish.

## Out of Scope

- Voice picker, multilingual voice selection, or voice tuning beyond playback rate (deferred to a later settings pass).
- Reading from apps that expose no UIA text *without* using the hotkey fallback.
- Non-Store / sideload distribution as a primary channel.
- A full settings window beyond the tray flyout.
- Pure UWP packaging (rejected: the sandbox blocks tray presence, global input, and cross-app text read).

## Notes

- Pure UWP was rejected because its sandbox cannot place a tray icon, register a global hotkey, or read text from other apps — all hard requirements here. WinUI 3 packaged MSIX satisfies both the capabilities and the Store goal.
- "Auto-read on any selection" has no single universal Windows API; UIA `TextPattern` covers Notepad/modern apps/most browsers but not terminals, the CLI, or Claude Code — hence the hotkey fallback is part of the core design, not optional.
- `H.NotifyIcon.WinUI` is the one unavoidable third-party dependency, since WinUI 3 has no built-in tray icon.
- Global input + tray may require a restricted-capability justification during Store certification; design stays within Store rules from day one.
- Repo stays named `ReadTheStupidText`; product display name is "ReadTheStupidText".
