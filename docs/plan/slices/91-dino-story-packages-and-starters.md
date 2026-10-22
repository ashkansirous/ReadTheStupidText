# Slice 91 — Dino stories as packages, three starter stories, developer export

*Built 2026-10-23, device-tested on the emulator; re-import on a device not yet verified.* Planned 2026-10-22. Source: phone scope "Dino Reader: starter stories, story store, better
onboarding, backup" (Scope Inbox, 2026-10-22).

**Batch:** 16 (Dino Reader content)
**Depends on:** Slices 58-80 (built)
**Touches:** `src/DinoReader.Core/StoryPackage*.cs` (new), `src/DinoReader.Core/FileStoryLibrary.cs`, `src/DinoReader.Core/IStoryLibrary.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/StarterStories.cs` (new), `src/DinoReader.App/Resources/Raw/starters/**` (new), `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/TileOverlay.xaml`, `src/DinoReader.App/TileOverlay.xaml.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.Core/CLAUDE.md`, `docs/plan/slices/61-gallery-grownup-gate.md`
**Story issue:** —

## Why

A fresh install opens on an empty gallery, so a new user never hears what makes Dino different:
a voice for each character. Three bundled stories fix that on first launch. The store (Slice 92)
and backup (Slice 93) use the same `.dino` package format these stories ship in, so the format
comes first.

## Decisions locked with the user (2026-10-22)

- Package: a zip with `package.json` (`schemaVersion`), `story.json`, `cover-*.jpg` and the page
  images. `story.json` already uses plain file names and its layout boxes are 0..1, so a story
  packages as-is with no path rewriting (Decision 94).
- Import gives the story a new GUID, because `FileStoryLibrary.AddAsync` throws on an existing id
  and `TryReadAsync` skips a folder/id mismatch. It refuses a newer `schemaVersion`, any entry
  path that escapes the story folder (zip-slip), and oversized entries. The imported `story.json`
  records the package's own id as `sourceId`, so the store (Slice 92) can show "Downloaded"; the
  starters use the same ids as their store copies. Backup restore (Slice 93) is the one exception:
  it keeps the backed-up id.
- Starters: The Three Little Pigs, The Gingerbread Man, The Tortoise and the Hare. Public-domain
  text only, a generated colour cover, no copyrighted artwork or names. Installed once on first
  run (a settings flag) and deletable like any other story (Decision 95).
- "Export story" is developer-only (hidden switch or Debug build), never a kid or parent action.
  This reverses Slice 61's "No sync, no export" for export only.

## Design (Batch 16 mock, screens 18-20, added 2026-10-22)

Source: `docs/design/dino-reader/Dino Reader Batch 16.dc.html`, screens 18, 19 and 20 (lines 81, 100, 134; index in `docs/design/dino-reader/README.md`). The mock agrees with the plan; it adds where the export button sits.

- **Starters on the kids' shelf (screen 20, line 134).** Header "My stories". Three tiles in the 2-column gallery grid, 3:4 covers with 16 px radius: "The Three Little Pigs" (`#FF7A9C`), "The Gingerbread Man", "The Tortoise and the Hare". Generated flat colour covers with the title in 17 px/700 white, no artwork. Installed once; a grown-up can delete them; a deleted starter never returns (Decision 95).
- **Starters in grown-up mode (screen 19, line 100).** The same three tiles with the Slice 80 overlay: "Edit" and "Cast" buttons (44 px high, 12 px radius) on a `rgba(43,37,64,.55)` strip, red trash badge top-right. The dashed "Add a story" tile comes first.
- **Export story (screen 19, note at line 129).** A purple share badge, top-left of the tile, the mirror of the red trash badge. Shown only with the developer switch on or in a Debug build. No other export entry point appears in the mock.
- **Start order.** After first run the starters must already be on the shelf when the grown-up lands in grown-up mode (screen 19), so the install finishes before the "ready" screen's button leads there (Slice 93).
- New strings: none beyond the starter titles (English text; covers are drawn, not localised). The share badge needs an accessibility label ("Export story") in both locales.

## Work

- [x] 1. **Package round-trip (first, smallest end-to-end).** `StoryPackageWriter` and
  `StoryPackageReader` in `DinoReader.Core`, plus an import method on `IStoryLibrary` that writes
  page images and covers into the new story folder (today `StoreFileAsync` accepts only
  `cover-<id>.jpg`). Tests: the cast, merges and pinned voices (`CastMember.Edited`) survive the
  round-trip; a newer schema, a zip-slip path and an oversized entry are refused.
- [x] 2. **Developer export.** "Export story" in the grown-up tile overlay, shown only with the
  developer switch on. It writes the `.dino` file through the share sheet or save dialog.
- [x] 3. **Write the three starters.** Public-domain text with any Gutenberg header stripped, the
  cast checked by reading each one through the app, then exported as `.dino` with `meta.json`.
- [x] 4. **Bundle and install.** The `.dino` files ship as `MauiAsset`s. First run imports them
  once, and a deleted starter doesn't come back. The gallery shows them instead of an empty grid.
- [x] 5. Amend Slice 61's out-of-scope line ("export allowed since Slice 91") and add the package
  and starter rules to `src/DinoReader.Core/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (round-trip and refusal tests).
- Emulator: a fresh install shows three stories with distinct character voices. Delete one and
  restart: it stays gone. Export a scanned story and import it on a second install: the cast is
  intact.

## Not yet verified

- Re-importing a `.dino` on a device: there is no import UI until Slices 92/93, so only the unit round-trip tests cover it.

## Changed while building

- `Story.cs` gained a `SourceId` field (the package's original id), outside the original Touches.
- `IStoryLibrary.ImportAsync` is a default interface method.
- The share icon is a raw `Path`, not an `IconKind`.
- The Pigs cover uses `CoverPalette` index 3.
- Starter asset names are flat (the Windows package drops folders).
- No `meta.json` in the starters: the Slice 92 store keeps meta in R2.
- Starters are tracked per package id in Preferences, so a new starter can arrive later and a deleted one stays gone.
- Developer switch: Debug builds, or 7 taps within 2 s in grown-up mode.
- The cow is voiced male by the guesser.

## From review

High and medium passes: findings 1-9 fixed both times, and high #1-10 all fixed. Medium #10 skipped because `DraftLibrary` never imports. (Reviews: 91-review-high.md, 91-review-medium.md.)

## Device test (emulator RTST_Test, Debug embedded arm64)

Fresh install shows the three starters; Pigs opens and plays; the cast screens show distinct voices; a deleted starter stays gone after a restart; Export opens the share sheet with `TheThreeLittlePigs.dino`. No crashes. Import back not tested.

#### For CLAUDE.md

- (Already written into `src/DinoReader.Core/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md` by this slice; the index pass need only check they are there.) Package format and import refusals: `src/DinoReader.Core/CLAUDE.md`. Starters, flat asset names, rebuild command, speaker-tag form, developer-only export: `src/DinoReader.App/CLAUDE.md`. (Slice 91: docs/plan/slices/91-dino-story-packages-and-starters.md)

#### For README.md

None (not user-facing: no import UI yet).

#### Not yet verified

- Re-importing a `.dino` on a device (no import UI until Slices 92/93; unit round-trip tests cover it). (Slice 91: docs/plan/slices/91-dino-story-packages-and-starters.md)
