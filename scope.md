# Scope

## Goals

- Provide a Windows 11 tray app ("ReadTheStupidText") that reads selected or copied text aloud on demand.
- Let the user pick reading speed as a fine decimal rate (0.5x–2.0x in 0.05 steps) via a YouTube-style slider in the control panel, applied live, with quick presets (1x/1.25x/1.5x/1.75x/2x) in the tray menu.
- Let the user choose from high-quality **local neural voices** (downloaded on first run), since the built-in Windows voices sound robotic and Narrator's neural voices are unreachable by a Store app.
- Auto-read text on selection where the app exposes it, with a global-hotkey fallback for apps that don't (terminals, CLI, Claude Code).
- Give the user two control surfaces from the tray icon: a **left-click control panel** (a small floating window with play/pause, a speed slider, a voice picker, and the auto-read/startup toggles) and a **right-click menu** for the same toggles plus Quit.
- Run automatically at Windows startup, minimized to the tray.
- Ship through the Microsoft Store.

## Approach

- **Stack:** C#/.NET + WinUI 3 (Windows App SDK), packaged as MSIX so it keeps full Win32 capabilities (tray icon, global hotkey, cross-app text read) while still being Store-installable.
- **Speech:** audio is played via `MediaPlayer` with `PlaybackRate` for pitch-corrected, live speed changes, regardless of which engine synthesized it.
- **Speed model:** a `PlaybackRate` value object (decimal multiplier, 0.5–2.0, snapped to 0.05 steps, range/step enforced in the type) — not an enum. `SpeedPresets` exposes the common stops for the native tray menu.
- **Voice (neural, local):** a bundled neural engine — the **sherpa-onnx** runtime (Apache-2.0) running the **Kokoro** voice model (Apache-2.0), via the `org.k2fsa.sherpa.onnx` NuGet package. The model downloads on first run from the sherpa-onnx GitHub release into app-local storage; the picker offers **only** the Kokoro voices (default a US male). While the model is downloading (or offline), reading falls back silently to the WinRT system voice so the app is never mute, but only neural voices are selectable. The chosen voice is persisted and applies to the next read. Piper was rejected as GPL (incompatible with closed-source Store distribution).
- **Control surfaces:**
  - **Right-click** opens an `H.NotifyIcon.WinUI` context menu. It runs in the library's default `PopupMenu` mode — a *native* Win32 menu that invokes each item's `Command` (never the WinUI `Click` event) and renders a checkmark only for `ToggleMenuFlyoutItem`. Every item is therefore driven by an `ICommand`, and the five speed presets are `ToggleMenuFlyoutItem`s with selection managed in code. This — not any "visual root" issue — is the fix for the original speed-selection defect. The menu holds the auto-read toggle, launch-at-startup toggle, and Quit.
  - **Left-click** opens a **control panel**: a borderless, always-on-top `AppWindow` (`OverlappedPresenter`, `IsAlwaysOnTop`, no system title bar) positioned above the taskbar and sized to its content. It is **pinned** — it stays on top of every window until the user clicks its ✕ or left-clicks the tray icon again (no light-dismiss); closing only hides it (the app stays in the tray). It holds a play/pause toggle, a YouTube-style speed slider over the full 0.5–2.0 range (0.05 steps), a voice `ComboBox`, and auto-read/startup `ToggleSwitch`es. Rich controls like a slider and combo box can't live in the native `PopupMenu`, so the panel is a real window rather than a flyout.
- **Selection capture:** UI Automation `TextPattern` selection monitoring as the primary auto-read path, plus a global hotkey (`Ctrl+Win+R`) that simulates copy and reads the clipboard as a fallback for apps without UIA text support.
- **Startup:** packaged `StartupTask` (Windows App SDK), declared in the manifest and user-toggleable, starting minimized to tray.
- **Persistence:** last-used speed, enabled state, and selected voice Id stored in `ApplicationData.Current.LocalSettings`; default speed is 1x, default voice is the default neural voice.

### Current unit of work

The speed-control fix, voice selection, launch-at-startup, the tray control panel (Slice 8), and **local neural voices (Slice 9 — sherpa-onnx + Kokoro)** are done. The remaining slice is **Store packaging & CI** (Slice 5), which must also resolve neural-model licensing due-diligence (the bundled `espeak-ng-data`) and the model-download UX on a metered connection.

## Out of Scope

- Voice *tuning* beyond rate (pitch, volume, SSML prosody).
- Selecting Windows/Narrator voices — the picker offers only the bundled neural (Kokoro) voices. The WinRT voice is an internal fallback only, used while the neural model downloads.
- Additional downloadable voice models/languages beyond the bundled Kokoro English model.
- Reading from apps that expose no UIA text *without* using the hotkey fallback.
- Non-Store / sideload distribution as a primary channel.
- A **persistent/dockable** settings window with tabs, taskbar presence, or hotkey-remap UI. The control panel is transient and tray-toggled (pinned topmost while open, hidden otherwise); every control maps to an existing service rather than introducing new settings.
- Pure UWP packaging (rejected: the sandbox blocks tray presence, global input, and cross-app text read).

## Notes

- The selected voice is persisted by id (prefixed `kokoro:`); on startup it falls back to the default neural voice if unset or not present in the model.
- A voice change takes effect on the next utterance, not the current one. Speed, by contrast, stays live via `MediaPlayer.PlaybackRate` (the neural engine synthesizes at 1× and the player applies the rate).
- The neural model (`kokoro-en-v0_19`, ~hundreds of MB) downloads on first run only; `internetClient` capability is required for that. Narrator's "Natural" voices remain unreachable by any third-party Store app — see [[project-natural-voices-unavailable]] — which is why the app brings its own engine.
- The speed-selection defect (radio items not committing, rate not applying) was caused by H.NotifyIcon's `PopupMenu` mode invoking only each item's `Command` and ignoring `RadioMenuFlyoutItem` checkmarks — confirmed against the library source. The earlier "tray window never activated / no visual root" theory was wrong. The same Command-driven pattern is reused by the voice submenu.
- Auto-read and launch-at-startup appear in **both** the control panel and the right-click menu; both surfaces read and write the same services so their state stays in sync.
- `H.NotifyIcon.WinUI` is the one unavoidable third-party dependency, since WinUI 3 has no built-in tray icon.
- Repo stays named `ReadTheStupidText`; product display name is "ReadTheStupidText".
