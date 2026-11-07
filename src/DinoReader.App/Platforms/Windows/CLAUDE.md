# CLAUDE.md — Dino Reader Windows head

Loaded when you work under `src/DinoReader.App/Platforms/Windows`. App-wide rules: `../../CLAUDE.md`.

- MAUI WinUI head, MSIX, no tray or hotkey. `Package.appxmanifest` carries the reserved Partner Center identity (`AshkanSirous.DinoReader`, Store ID `9N9VHRLCZXN5`); tile and splash art are copied from `docs/design/dino-reader/icons/windows` (no `<MauiIcon>`). Never set a global `RuntimeIdentifier` (see `.github/CLAUDE.md`). It plays pre-made clips and recordings and uses no speech engine. `WindowsTextMeasurer` (Win2D) is the `ITextMeasurer`. (Slice 67: docs/plan/slices/67-dino-reader-ios-windows.md)
- Keys go through one hook (`DinoKeyboard.cs`), then Core `DinoKeyMap`, then the top page's `IDinoKeyTarget`; pages handle no keys directly. Tooltips and "click" wording come only from `DesktopTips`. (Slice 235: docs/plan/slices/235-dino-windows-window.md)
- Per-book input lives on the native `ListViewBase` (never the tile template or MAUI recognizers); key handlers use `AddHandler(..., handledEventsToo: true)`. (Slice 235)
- Acceptance-test the head in the headless Hyper-V VM, never on the host desktop: `tools/desktop-vm/Invoke-DesktopTest.ps1 -App dino` runs `tools/desktop-vm/checks/dino-*.ps1`. Give a new screen a stable `AutomationId`. (Slice 163: docs/plan/slices/163-desktop-tests-in-hyperv-vm.md)
