# Slice 2 — Hotkey copies the current selection

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 2 — Hotkey copies the current selection.** ([#5](https://github.com/ashkansirous/ReadTheStupidText/issues/5)) Extend the hotkey to
      simulate copy (send `Ctrl+C`) before reading, so the user can select in
      any app (incl. terminals/CLI/Claude Code) and have it read aloud. Persist
      last-used speed + enabled state to `LocalSettings`.

## Verification (moved from plan.md)

- **Slice 2:** select text in Notepad, a terminal, a browser, and Claude Code,
  press the hotkey → each is read aloud; restart app → last speed/enabled
  state restored.
