# Scope

## Goals

- Provide a Windows 11 tray app ("ReadTheStupidText") that reads selected or copied text aloud on demand.
- Let the user pick reading speed as a fine decimal rate (0.5x–2.0x in 0.05 steps) via a YouTube-style slider in the control panel, applied live, with quick presets (1x/1.25x/1.5x/1.75x/2x) in the tray menu.
- Let the user choose from high-quality **local neural voices** (bundled in the app), since the built-in Windows voices sound robotic and Narrator's neural voices are unreachable by a Store app.
- Auto-read text on selection where the app exposes it (UI Automation), **and auto-read on clipboard copy** for apps that don't expose a selection (terminals, CLI, Claude Code in Windows Terminal), with a global hotkey as the always-on fallback.
- Let the user see, live, what text the app is intercepting and what happens to each read (pending → reading → read, or ignored/interrupted/failed) in an activity-log window — which also doubles as the way to diagnose when a selection isn't read.
- Give the user two control surfaces from the tray icon: a **left-click control panel** (a small floating window with play/pause, a speed slider, a voice picker, and the auto-read/startup toggles) and a **right-click menu** for the same toggles plus Quit.
- Run automatically at Windows startup, minimized to the tray.
- Ship through the Microsoft Store.

## Approach

- **Stack:** C#/.NET + WinUI 3 (Windows App SDK), packaged as MSIX so it keeps full Win32 capabilities (tray icon, global hotkey, cross-app text read) while still being Store-installable.
- **Speech:** audio is played via `MediaPlayer` with `PlaybackRate` for pitch-corrected, live speed changes, regardless of which engine synthesized it.
- **Speed model:** a `PlaybackRate` value object (decimal multiplier, 0.5–2.0, snapped to 0.05 steps, range/step enforced in the type) — not an enum. `SpeedPresets` exposes the common stops for the native tray menu.
- **Voice (neural, local):** a bundled neural engine — the **sherpa-onnx** runtime (Apache-2.0) running the **Supertonic-3** voice model (Apache-2.0, `sherpa-onnx-supertonic-3-tts-int8-2026-05-11`, ~145 MB, 10 voices: 5 male / 5 female), via the `org.k2fsa.sherpa.onnx` NuGet package. The model **ships inside the package** (committed under `VoiceModel/`, read from `AppContext.BaseDirectory`) — no download, works offline from first launch. The picker offers **only** the Supertonic voices (default Male 1); a silent WinRT fallback remains only as a safety net if the packaged files are missing. The chosen voice is persisted and applies to the next read. Kokoro (Chinese-focused; no English male voice in the latest build) and Piper (GPL) were rejected; Supertonic is English-first, uses no espeak data (no GPL concern), and is ~half Kokoro's size.
- **Control surfaces:**
  - **Right-click** opens an `H.NotifyIcon.WinUI` context menu. It runs in the library's default `PopupMenu` mode — a *native* Win32 menu that invokes each item's `Command` (never the WinUI `Click` event) and renders a checkmark only for `ToggleMenuFlyoutItem`. Every item is therefore driven by an `ICommand`, and the five speed presets are `ToggleMenuFlyoutItem`s with selection managed in code. This — not any "visual root" issue — is the fix for the original speed-selection defect. The menu holds the auto-read toggle, launch-at-startup toggle, and Quit.
  - **Left-click** opens a **control panel**: a borderless, always-on-top `AppWindow` (`OverlappedPresenter`, `IsAlwaysOnTop`, no system title bar) positioned above the taskbar and sized to its content. It is **pinned** — it stays on top of every window until the user clicks its ✕ or left-clicks the tray icon again (no light-dismiss); closing only hides it (the app stays in the tray). It holds a play/pause toggle, a YouTube-style speed slider over the full 0.5–2.0 range (0.05 steps), a voice `ComboBox`, and auto-read/startup `ToggleSwitch`es. Rich controls like a slider and combo box can't live in the native `PopupMenu`, so the panel is a real window rather than a flyout.
  - **Right-click → "Show activity log"** opens the activity-log window (a normal resizable window, distinct from the pinned panel).
- **Selection capture:** UI Automation `TextPattern` selection monitoring as the primary auto-read path, plus a global hotkey (`Ctrl+Win+R`) that simulates copy and reads the clipboard as a fallback for apps without UIA text support. The monitor debounces growth events (500 ms) and emits a `SelectionCleared` signal on deselect so a deselect (or a new selection) interrupts a read in progress.
- **Activity log:** read activity flows through an in-memory, observable `IActivityLog` (Application; capped ring buffer, cleared on restart). `ReadAloudService` opens one entry per intercepted text and transitions its state — pending (during the debounce) → reading → read, or ignored (superseded while pending), interrupted (stopped mid-read), failed (synthesis error) — tagged by source (auto-read / hotkey / manual). The `ActivityLogWindow` renders entries live, each row updating in place.
- **Startup:** packaged `StartupTask` (Windows App SDK), declared in the manifest and user-toggleable, starting minimized to tray.
- **Persistence:** last-used speed, enabled state, and selected voice Id stored in `ApplicationData.Current.LocalSettings`; default speed is 1x, default voice is the default neural voice.

### Current unit of work

All planned slices are implemented: the speed-control fix, voice selection, launch-at-startup, the tray control panel (Slice 8), local neural voices (Slice 9 — sherpa-onnx + Supertonic-3, bundled), **Store packaging & CI (Slice 5 — merged; CI builds the MSIX and publishes `v*` tags to GitHub Releases)**, and the **live activity log + auto-read state machine (Slice 10)**. Remaining before a Store release is the account-dependent work in `STORE.md` (Partner Center identity, signing, first submission). The auto-read "nothing happens" report is to be root-caused with the new activity log at runtime.

## Out of Scope

- Voice *tuning* beyond rate (pitch, volume, SSML prosody).
- Selecting Windows/Narrator voices — the picker offers only the bundled neural (Supertonic) voices. The WinRT voice is an internal safety-net fallback only (used if the packaged model is missing).
- Additional voice models/languages beyond the bundled Supertonic model.
- ~~Reading from apps that expose no UIA text *without* using the hotkey fallback.~~ Now covered by auto-read on clipboard copy (Slice 10 follow-up).
- **Detecting a bare *selection* in the console** (Windows Terminal / PowerShell / `cmd`) or other non-UIA editors (e.g. Visual Studio's code editor). These apps expose no UI Automation text selection, so selecting alone gives no OS signal — Windows provides no API for it. Reading from them requires a **copy** (auto-read-on-copy) or the **hotkey**; Windows Terminal's `copyOnSelect` makes selecting auto-copy, which then auto-reads.
- Non-Store / sideload distribution as a primary channel.
- A **persistent/dockable** settings window with tabs, taskbar presence, or hotkey-remap UI. The control panel is transient and tray-toggled (pinned topmost while open, hidden otherwise); every control maps to an existing service rather than introducing new settings.
- Persisting, exporting, or configuring the activity log — it is in-memory, capped, live-only, and cleared on restart (a diagnostic surface, not a logging framework).
- Pure UWP packaging (rejected: the sandbox blocks tray presence, global input, and cross-app text read).

## Notes

- The selected voice is persisted by id (prefixed `supertonic:`); on startup it falls back to the default neural voice if unset or not present in the model.
- A voice change takes effect on the next utterance, not the current one. Speed, by contrast, stays live via `MediaPlayer.PlaybackRate` (the neural engine synthesizes at 1× and the player applies the rate).
- The neural model (`sherpa-onnx-supertonic-3-tts-int8-2026-05-11`, ~145 MB) is bundled in the package and committed to the repo under `VoiceModel/`; no network and no `internetClient` capability. Narrator's "Natural" voices remain unreachable by any third-party Store app — see [[project-natural-voices-unavailable]] — which is why the app brings its own engine.
- Auto-read debounces UIA selection events (500 ms) so a drag-select triggers one read, not a burst. A selection superseded during that wait is logged `ignored`; a new selection or a deselect (`SelectionCleared`) that stops a read in progress is logged `interrupted` (and stops the reader); a synthesis/playback error is `failed`. While a read is synthesizing (no audio yet) it is logged `generatingAudio`, flipping to `reading` on the first playback.
- **Console / non-UIA apps read on *copy*, not *selection*.** Windows Terminal, PowerShell, `cmd`, and editors like Visual Studio expose no UIA text selection, so a bare selection raises no event. Auto-read instead picks them up via a Win32 clipboard listener (`WM_CLIPBOARDUPDATE`) when text is **copied** (gated by the auto-read toggle), and the hotkey (`Ctrl+Win+R`) copies-then-reads regardless. Enabling Windows Terminal's `copyOnSelect` makes selecting auto-copy, so selection effectively auto-reads there. Clipboard reads use the focus-independent Win32 API (the WinRT clipboard is readable only while focused, and this tray window never is).
- Long text is synthesized in chunks (split on paragraph/sentence/word boundaries above ~200 chars) generated concurrently (up to 3 at once) and played strictly in order, so playback starts after the first chunk instead of after the whole text. A superseded read is cancelled (generation counter + `CancellationToken`) so stale audio can never play over the new one.
- The speed-selection defect (radio items not committing, rate not applying) was caused by H.NotifyIcon's `PopupMenu` mode invoking only each item's `Command` and ignoring `RadioMenuFlyoutItem` checkmarks — confirmed against the library source. The earlier "tray window never activated / no visual root" theory was wrong. The same Command-driven pattern is reused by the voice submenu.
- Auto-read and launch-at-startup appear in **both** the control panel and the right-click menu; both surfaces read and write the same services so their state stays in sync.
- `H.NotifyIcon.WinUI` is the one unavoidable third-party dependency, since WinUI 3 has no built-in tray icon.
- Repo stays named `ReadTheStupidText`; product display name is "ReadTheStupidText".
