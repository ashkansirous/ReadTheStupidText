# Slice 124 — Dino: the words panel and its switch match the Batch 25 mock

*In progress (wave 2026-10-25), draft PR.* Dino Reader only; RTST is not touched.

**Batch:** 26 (Batch 25 mock follow-ups)
**Depends on:** Slice 122 (built, PR #335)
**Touches:** `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.App/CLAUDE.md`

## Why

The Batch 25 mock (`docs/design/dino-reader/Dino Reader Batch 25.dc.html`, frames 56-58) was imported
after Slice 122 merged. The behaviour matches (30% of the visible picture, switch under Animal sounds,
photo books off by default), but the look differs. The gaps are listed in
[Slice 122's "Gaps against the mock"](122-dino-text-panel-30-and-switch.md).

## Decisions

- Follow the mock for every gap (no open points left to the user):
  - **Sub-line follows the switch:** on = "The words show in a panel at the bottom", off = "Off because
    these pictures already have the words in them". It replaces the current fixed hint. Every app
    language gets both strings.
  - **One card holds both switches:** Animal sounds on top, a 1px `#efe9dd` divider, then "Show the
    words over the pictures". Both rows keep their own `AccessibleName`. The words row still shows
    only on picture + text books.
  - **The panel (upright):** a 22px top fade from `rgba(255,255,255,.95)` to clear; short text sits at the
    bottom of the panel, and long text still scrolls; margin 8, padding 14x10, line height 1.5, corner
    16, shadow `0 4 12` at .16. The 30% cap keeps the margin inside it.

## Work

- [ ] 1. Panel look and bottom alignment in `PictureTextPages` (upright `TextPanel`), with the cap unchanged.
- [ ] 2. One switch card with a divider in `CastRows`/`CastPage`, and the sub-line chosen by the switch value.
- [ ] 3. The two new strings in all 14 `.resx` files, and the old hint key removed if nothing else uses it.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and the Android build.
- Emulator: the pigs starter upright. A short page sits at the bottom of the panel with the fade at the top, and a long page scrolls. On the cast page the one card shows both switches, and turning the words switch on or off changes its sub-line.
