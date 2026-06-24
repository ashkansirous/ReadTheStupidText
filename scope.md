# Scope

## Goals

- Provide a Windows 11 tray app ("ReadTheStupidText") that reads selected or copied text aloud on demand.
- Let the user pick reading speed as a fine decimal rate (0.5x–2.0x in 0.05 steps) via a YouTube-style slider in the control panel, applied live, with quick presets (1x/1.25x/1.5x/1.75x/2x) in the tray menu.
- Let the user choose the narrator voice from the voices installed on Windows.
- Auto-read text on selection where the app exposes it, with a global-hotkey fallback for apps that don't (terminals, CLI, Claude Code).
- Give the user two control surfaces from the tray icon: a **left-click control panel** (a small floating window with play/pause, a speed slider, a voice picker, and the auto-read/startup toggles) and a **right-click menu** for the same toggles plus Quit.
- Run automatically at Windows startup, minimized to the tray.
- Ship through the Microsoft Store.

## Approach

- **Stack:** C#/.NET + WinUI 3 (Windows App SDK), packaged as MSIX so it keeps full Win32 capabilities (tray icon, global hotkey, cross-app text read) while still being Store-installable.
- **Speech:** WinRT `Windows.Media.SpeechSynthesis` for synthesis, played via `MediaPlayer` with `PlaybackRate` for pitch-corrected, live speed changes.
- **Speed model:** a `PlaybackRate` value object (decimal multiplier, 0.5–2.0, snapped to 0.05 steps, range/step enforced in the type) — not an enum. `SpeedPresets` exposes the common stops for the native tray menu.
- **Voice:** enumerate `SpeechSynthesizer.AllVoices` and let the user select one; the choice is persisted and applied to the next read (a voice can't be swapped mid-utterance). Default is the system voice.
- **Control surfaces:**
  - **Right-click** opens an `H.NotifyIcon.WinUI` context menu. It runs in the library's default `PopupMenu` mode — a *native* Win32 menu that invokes each item's `Command` (never the WinUI `Click` event) and renders a checkmark only for `ToggleMenuFlyoutItem`. Every item is therefore driven by an `ICommand`, and the five speed presets are `ToggleMenuFlyoutItem`s with selection managed in code. This — not any "visual root" issue — is the fix for the original speed-selection defect. The menu holds the auto-read toggle, launch-at-startup toggle, and Quit.
  - **Left-click** opens a **control panel**: a borderless, always-on-top `AppWindow` (`OverlappedPresenter`, `IsAlwaysOnTop`, no system title bar) positioned above the taskbar and sized to its content. It is **pinned** — it stays on top of every window until the user clicks its ✕ or left-clicks the tray icon again (no light-dismiss); closing only hides it (the app stays in the tray). It holds a play/pause toggle, a YouTube-style speed slider over the full 0.5–2.0 range (0.05 steps), a voice `ComboBox`, and auto-read/startup `ToggleSwitch`es. Rich controls like a slider and combo box can't live in the native `PopupMenu`, so the panel is a real window rather than a flyout.
- **Selection capture:** UI Automation `TextPattern` selection monitoring as the primary auto-read path, plus a global hotkey (`Ctrl+Win+R`) that simulates copy and reads the clipboard as a fallback for apps without UIA text support.
- **Startup:** packaged `StartupTask` (Windows App SDK), declared in the manifest and user-toggleable, starting minimized to tray.
- **Persistence:** last-used speed, enabled state, and selected voice Id stored in `ApplicationData.Current.LocalSettings`; default speed is 1x, default voice is the system voice.

### Current unit of work

Speed-control fix, narrator voice selection, and launch-at-startup are **done and merged**. The current slice is **Slice 8 — the tray control panel window**: left-click opens the borderless always-on-top panel described above, surfacing every interactive control in one place while the right-click menu stays intact. It binds to the existing `ReadAloudService`, `IVoiceCatalog`, and `IStartupService` (no new layers). The remaining slice after this is **Store packaging & CI**.

## Out of Scope

- Voice *tuning* beyond rate (pitch, volume, SSML prosody).
- Installing or downloading new voices/languages from within the app — the picker lists only voices already installed in Windows.
- Reading from apps that expose no UIA text *without* using the hotkey fallback.
- Non-Store / sideload distribution as a primary channel.
- A **persistent/dockable** settings window with tabs, taskbar presence, or hotkey-remap UI. The control panel is transient and tray-toggled (pinned topmost while open, hidden otherwise); every control maps to an existing service rather than introducing new settings.
- Pure UWP packaging (rejected: the sandbox blocks tray presence, global input, and cross-app text read).

## Notes

- The selected voice is persisted by `VoiceInformation.Id`; on startup it falls back to the system default if unset or if the saved voice is no longer installed.
- A voice change takes effect on the next utterance, not the current one. Speed, by contrast, stays live via `PlaybackRate`.
- The speed-selection defect (radio items not committing, rate not applying) was caused by H.NotifyIcon's `PopupMenu` mode invoking only each item's `Command` and ignoring `RadioMenuFlyoutItem` checkmarks — confirmed against the library source. The earlier "tray window never activated / no visual root" theory was wrong. The same Command-driven pattern is reused by the voice submenu.
- Auto-read and launch-at-startup appear in **both** the control panel and the right-click menu; both surfaces read and write the same services so their state stays in sync.
- `H.NotifyIcon.WinUI` is the one unavoidable third-party dependency, since WinUI 3 has no built-in tray icon.
- Repo stays named `ReadTheStupidText`; product display name is "ReadTheStupidText".
