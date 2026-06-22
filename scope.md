# Scope

## Goals

- Provide a Windows 11 tray app ("ReadTheStupidText") that reads selected or copied text aloud on demand.
- Let the user pick reading speed from 1x, 1.25x, 1.5x, 1.75x, and 2x, applied live — and have that selection actually take effect and show as selected.
- Let the user choose the narrator voice from the voices installed on Windows.
- Auto-read text on selection where the app exposes it, with a global-hotkey fallback for apps that don't (terminals, CLI, Claude Code).
- Give the user a tiny, always-available control beside the clock to pause/resume, change speed, and pick the voice.
- Run automatically at Windows startup, minimized to the tray.
- Ship through the Microsoft Store.

## Approach

- **Stack:** C#/.NET + WinUI 3 (Windows App SDK), packaged as MSIX so it keeps full Win32 capabilities (tray icon, global hotkey, cross-app text read) while still being Store-installable.
- **Speech:** WinRT `Windows.Media.SpeechSynthesis` for synthesis, played via `MediaPlayer` with `PlaybackRate` for pitch-corrected, live speed changes.
- **Voice:** enumerate `SpeechSynthesizer.AllVoices` and let the user select one; the choice is persisted and applied to the next read (a voice can't be swapped mid-utterance). Default is the system voice.
- **Control surface:** notification-area (system tray) icon via `H.NotifyIcon.WinUI` — a flyout with Play/Pause, the five speed buttons, an auto-read toggle, and a **Voice** submenu listing installed voices. The tray flyout must have a proper XAML root so stateful (radio) items commit their selection — this is the fix for the speed-selection defect.
- **Selection capture:** UI Automation `TextPattern` selection monitoring as the primary auto-read path, plus a global hotkey (`Ctrl+Win+R`) that simulates copy and reads the clipboard as a fallback for apps without UIA text support.
- **Startup:** packaged `StartupTask` (Windows App SDK), declared in the manifest and user-toggleable, starting minimized to tray.
- **Persistence:** last-used speed, enabled state, and selected voice Id stored in `ApplicationData.Current.LocalSettings`; default speed is 1x, default voice is the system voice.

### Current unit of work

Two slices, each end-to-end and independently shippable: **(1) fix speed control** so the five speed items commit selection and change the rate live, then **(2) narrator voice selection** — a tray "Voice" submenu over the installed Windows voices, persisted and applied to the next read. These come before the remaining Startup and Store-packaging slices.

## Out of Scope

- Voice *tuning* beyond rate (pitch, volume, SSML prosody).
- Installing or downloading new voices/languages from within the app — the picker lists only voices already installed in Windows.
- Reading from apps that expose no UIA text *without* using the hotkey fallback.
- Non-Store / sideload distribution as a primary channel.
- A full settings window — voice and speed live in the tray flyout, not a separate window.
- Pure UWP packaging (rejected: the sandbox blocks tray presence, global input, and cross-app text read).

## Notes

- The selected voice is persisted by `VoiceInformation.Id`; on startup it falls back to the system default if unset or if the saved voice is no longer installed.
- A voice change takes effect on the next utterance, not the current one. Speed, by contrast, stays live via `PlaybackRate`.
- The speed-selection defect (radio items not committing, rate not applying) is caused by the tray-hosting window never being activated, leaving the flyout without a proper visual root; the fix roots the flyout correctly and benefits the new voice submenu too.
- `H.NotifyIcon.WinUI` is the one unavoidable third-party dependency, since WinUI 3 has no built-in tray icon.
- Repo stays named `ReadTheStupidText`; product display name is "ReadTheStupidText".
