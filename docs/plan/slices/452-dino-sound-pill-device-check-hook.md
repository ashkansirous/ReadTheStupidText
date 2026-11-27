# Slice 452 — Dino: sound pill device check hook and Part B leftovers
**Product:** Dino
**Touches:** `src/Dino/DinoReader.App/Testing/**`, `src/Dino/DinoReader.App/**/SoundStatusPill.cs`, `src/Dino/DinoReader.App/**/SoundPillText.cs`, `.maestro/dino/**`

*In progress (wave 2026-11-27), draft PR.* (Planned 2026-11-25, moved out of Slice 290.)

**Why.** Slice 290 (built, #889 and #895) cannot be compared with frames 197-199 on a device: no flow stops on the reading page while a pill shows. Moved out of Slice 290 and plan.md on 2026-11-25.

**Work.**
- [ ] A Debug-only test hook (`src/Dino/DinoReader.App/Testing/**`) that opens a store book and holds the pill in each state (Getting with a count, Wi-Fi, Preparing, no sound), and a Maestro flow that screenshots each state. Restart the emulator cold first (the Maestro driver crash after boot).
- [ ] Low leftovers: `SoundStatusPill.cs` `Label.Text` is null under FormattedText; `SoundPillText.cs` line 12 `IndexOf` marks the first occurrence of the name; the FormattedString is rebuilt on each changed call (the ring redraws while hidden); ja/zh pill text uses the system font.
