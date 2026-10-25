# Slice 124 — Dino: the words panel and its switch match the Batch 25 mock

*Built 2026-10-25, not yet checked on an emulator (943 tests green, Android build clean).* Dino Reader only; RTST is not touched.

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

- [x] 1. Panel look and bottom alignment in `PictureTextPages` (upright `TextPanel`), with the cap unchanged.
- [x] 2. One switch card with a divider in `CastRows`/`CastPage`, and the sub-line chosen by the switch value.
- [x] 3. The two new strings in all 14 `.resx` files, and the old hint key removed if nothing else uses it.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and the Android build.
- Emulator: the pigs starter upright. A short page sits at the bottom of the panel with the fade at the top, and a long page scrolls. On the cast page the one card shows both switches, and turning the words switch on or off changes its sub-line.

## Changed while building

- The off sub-line is "already have the words" only when the book opened with the switch stored off; otherwise a new string, "Only the pictures show" (`Cast_WordsOverPictures_HintOffPlain`, 14 locales). The hint keys `HintOn`/`HintOff` replace the old `Hint` key.
- The fade uses the theme's TextCard colour (not fixed white) and shows only while the text is scrolled (`ScrollY > 1`).
- Bottom alignment and label padding apply to the upright layout only.
- One `SwitchesCard` holds both rows with the divider.

## From review

- High: findings 1-7 applied; 8 skipped (existing pattern).
- Medium: 1, 2, 3, 6, 9, 10 applied; 4 accepted as a limit; 5 not a bug; 7 and 8 skipped.
- Known limit: an illustrated book switched off in an earlier visit shows the photo-book reason.

#### For CLAUDE.md (lifted 2026-10-25)

- Already added to `src/DinoReader.App/CLAUDE.md` on this branch (Slice 124: docs/plan/slices/124-dino-words-panel-matches-mock.md). Nothing for the root file.

#### Not yet verified

- Emulator look of the panel fade, the bottom alignment and the one switch card (pigs starter upright).
- Native read of the 3 new strings in the 13 non-English languages.

