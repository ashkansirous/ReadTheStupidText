# CLAUDE.md — Windows.Tray

Loaded when you work under `src/Rtst/ReadTheStupidText.Windows.Tray` (`net11.0-windows`, Slice 128, Decision 135). Depends on Application and Domain.

- Tray-only Win32/UIA code lives here: global hotkey, UIA selection monitor, clipboard listener, selection copier, foreground probe and the startup task.
- Only `App` references this project, so a tray edit does not rebuild or ship Dino Reader (which shares Infrastructure). Never move tray code into Infrastructure.
- The interfaces (`IHotkeyService`, ...) and the activity log stay in Application/Domain, used by `ReadAloudService`. (Slice 128: docs/plan/slices/128-tray-code-out-of-shared-infrastructure.md)
