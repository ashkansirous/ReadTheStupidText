# Slice 441 — Dino: iPad reading page hides Back and the view switch from VoiceOver
**Product:** Dino
**Touches:** `src/Dino/DinoReader.App/ReadingPage.xaml.cs`, `.maestro/dino/**`

*Planned 2026-11-25 (plan-cleanup of that day; not built).*

Found by Slice 291's 2026-11-15 App Store pass (its item 11, still open on origin/main 1a8bf725). On the iPad the reading page header floats. `ApplyHeaderHeading` (`src/Dino/DinoReader.App/ReadingPage.xaml.cs`, line 967 on origin/main 1a8bf725) sets `SemanticProperties.Description` and `HeadingLevel` on the `Header` grid; iOS then likely exposes the header as one accessibility element that hides `ReadingBack` and `ReadingModes` (hierarchy dump of run 43669367967; read from code, so likely, not shown).

## Work
- [ ] 1. Reproduce on the iPad simulator (Maestro hierarchy: `ReadingBack` and `ReadingModes` missing while the header carries a Description).
- [ ] 2. Fix so Back and the view switch are reachable by VoiceOver (set the description on the title label instead of the container); check Android TalkBack on a tablet too.
- [ ] 3. Hierarchy check on iPad and iPhone shows both ids; no change to the visible layout.
