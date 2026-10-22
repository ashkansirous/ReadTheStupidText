# Slice 91 — Dino stories as packages, three starter stories, developer export

*Planned 2026-10-22.* Source: phone scope "Dino Reader: starter stories, story store, better
onboarding, backup" (Scope Inbox, 2026-10-22).

**Batch:** 16 (Dino Reader content)
**Depends on:** Slices 58-80 (built)
**Touches:** `src/DinoReader.Core/StoryPackage*.cs` (new), `src/DinoReader.Core/FileStoryLibrary.cs`, `src/DinoReader.Core/IStoryLibrary.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/StarterStories.cs` (new), `src/DinoReader.App/Resources/Raw/starters/**` (new), `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/TileOverlay.xaml`, `src/DinoReader.App/TileOverlay.xaml.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.Core/CLAUDE.md`, `docs/plan/slices/61-gallery-grownup-gate.md`
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
  path that escapes the story folder (zip-slip), and oversized entries.
- Starters: The Three Little Pigs, The Gingerbread Man, The Tortoise and the Hare. Public-domain
  text only, a generated colour cover, no copyrighted artwork or names. Installed once on first
  run (a settings flag) and deletable like any other story (Decision 95).
- "Export story" is developer-only (hidden switch or Debug build), never a kid or parent action.
  This reverses Slice 61's "No sync, no export" for export only.

## Work

- [ ] 1. **Package round-trip (first, smallest end-to-end).** `StoryPackageWriter` and
  `StoryPackageReader` in `DinoReader.Core`, plus an import method on `IStoryLibrary` that writes
  page images and covers into the new story folder (today `StoreFileAsync` accepts only
  `cover-<id>.jpg`). Tests: the cast, merges and pinned voices (`CastMember.Edited`) survive the
  round-trip; a newer schema, a zip-slip path and an oversized entry are refused.
- [ ] 2. **Developer export.** "Export story" in the grown-up tile overlay, shown only with the
  developer switch on. It writes the `.dino` file through the share sheet or save dialog.
- [ ] 3. **Write the three starters.** Public-domain text with any Gutenberg header stripped, the
  cast checked by reading each one through the app, then exported as `.dino` with `meta.json`.
- [ ] 4. **Bundle and install.** The `.dino` files ship as `MauiAsset`s. First run imports them
  once, and a deleted starter doesn't come back. The gallery shows them instead of an empty grid.
- [ ] 5. Amend Slice 61's out-of-scope line ("export allowed since Slice 91") and add the package
  and starter rules to `src/DinoReader.Core/CLAUDE.md` and `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (round-trip and refusal tests).
- Emulator: a fresh install shows three stories with distinct character voices. Delete one and
  restart: it stays gone. Export a scanned story and import it on a second install: the cast is
  intact.

## Not yet verified

_(filled in when built)_
