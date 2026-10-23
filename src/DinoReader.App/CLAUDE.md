# CLAUDE.md — Dino Reader app

Loaded when you work under `src/DinoReader.App`. The pure library is `src/DinoReader.Core`
(own CLAUDE.md); shared platform code is `src/ReadTheStupidText.Mobile.Shared`; MAUI gotchas
are in `src/ReadTheStupidText.Mobile/CLAUDE.md`; its CI target is in `.github/CLAUDE.md`.

> **Dino Reader (Batch 10):** a second product in this repo, a kids' book reader
> with a voice per character: a second MAUI app, `src/DinoReader.App`, sharing
> `src/ReadTheStupidText.Mobile.Shared` plus Domain/Application/Documents. The pure
> `src/DinoReader.Core` holds the dialogue segmenter, speaker attributor and voice
> caster (no framework deps). One shared version (`v<x.y.z>`) covers both products.
> (Decisions 72-82, `docs/plan/slices/58-67`)
> Its icons (all platforms, "Book hug" design) live in
> `docs/design/dino-reader/icons/`: copy from there, never redraw or regenerate them.
> Its screens are the mocks `docs/design/dino-reader/Dino Reader v2.dc.html` and, for the
> first run, store and backup (Batch 16), `Dino Reader Batch 16.dc.html` (index in
> that folder's README): read visual values (colours, sizes, copy, layout) from it,
> never guess them, and never call `DesignSync` for them.

- Dino Reader: stories are `AppDataDirectory/stories/<guid>/story.json` via `FileStoryLibrary`, images only through `TryResolveFile`; grown-up pages implement `IGrownUpPage`, and app-launched activities are wrapped in `ExternalActivityGuard`; tokens come from `DinoTokens`. (Slice 61: docs/plan/slices/61-gallery-grownup-gate.md)
- Dino Reader reading: `StorySentences` and `SegmentSentenceSplitter` map `ChunkChanged` 1:1 to sentences; `FlowPager`/`PageComposer` (`DinoReader.Core`) paginate off the UI thread; story text ignores the font scale. (Slice 62: docs/plan/slices/62-reading-screen-modes.md)
- Dino Reader cast: edits go only through `CastEditor` and pin voices via `CastMember.Edited`; merges live in `Story.Merges`, applied each read by `CastMerges` in `StoryVoicing`, never in stored text; an unknown speaker gets the narrator voice (`VoiceCaster.VoiceForLine`). (Slice 65: docs/plan/slices/65-cast-screen.md)
- Dino languages: rules per language in `RuleSets` (cached per `AppLanguage`, `LanguageRules.For(code)`); only `ReadingPage` switches `SpokenLanguage`, never the cast screen; translation stays hidden until an `ITranslator` is registered in Dino's DI. (Slice 66: docs/plan/slices/66-dino-reader-languages-translation.md)
- Dino picture mode on real pages: `ScanLayout` (Core) maps sentences to line boxes; `ScanPages`/`ZoomablePage` (App) render them. Boxes are in the picture's own 0..1 coordinates, so zoom and EXIF rotation keep them on the text. Gestures go on the untransformed outer view; only the inner layout is scaled. Imports keep pictures only while the text is unedited; temp files live in a per-import folder and must be deleted on every exit path. (Slice 64: docs/plan/slices/64-picture-mode-real-pages.md)
- Dino Reader on Windows (Slice 67): a MAUI WinUI head, MSIX, no tray/hotkey. `Platforms/Windows/Package.appxmanifest` carries the reserved Partner Center identity (`AshkanSirous.DinoReader`, Store ID `9N9VHRLCZXN5`); tile and splash art are copied from `docs/design/dino-reader/icons/windows` (no `<MauiIcon>`). Build/package with `dotnet publish -f net10.0-windows10.0.19041.0 -p:IncludeAndroid=false -p:Platform=x64|ARM64` and never a global `RuntimeIdentifier` (see `.github/CLAUDE.md`). Windows has no `IDocumentScanner`, so the Scan tile is dimmed; the voice, OCR, PDF and image-keeper services are in `Mobile.Shared/Platforms/Windows` (the neural reader is Infrastructure's `SupertonicSpeechReader`, which now reads segments). `WindowsTextMeasurer` (Win2D) is the Windows `ITextMeasurer`. (Slice 67: docs/plan/slices/67-dino-reader-ios-windows.md)

- Free stories (Slice 92): `StoreClient` talks only to `dinoreader.sirous.uk` under `/api/stories` (`StoreClient.Resolve` rejects any other host or path, so a list cannot send the app elsewhere), sends nothing about the user and keeps no cookies. A download goes to a temp file and is imported through `StoryPackageReader` only when whole; leaving `StorePage` does not cancel it: the download finishes into the library (`StoreClient.LibraryChanged` makes a showing `GalleryPage` reload) and a failure shows "Try again". A story whose `schemaVersion` is newer than `StoryPackageManifest.CurrentSchemaVersion` is hidden, not greyed out; "Downloaded" means an installed story's `SourceId` equals the store id. `StorePage` is an `IGrownUpPage`, entered from the "Free stories" button in grown-up mode; the privacy page, `AndroidManifest.xml` and `Info.plist` comments must stay in step with what the network is used for. The Worker side is in the root `CLAUDE.md` landing-site paragraph. (Slice 92: docs/plan/slices/92-dino-story-store.md)
- Dino Reader builds for Android, iOS (`-p:IncludeIos=true` on Windows, compile only) and Windows (MAUI WinUI, MSIX via
  `dotnet publish -f net10.0-windows10.0.19041.0 -p:IncludeAndroid=false -p:Platform=x64`). On a machine with only
  maui-android, pass `-p:IncludeWindows=false`. Platform code lives once in `Mobile.Shared/Platforms/<OS>`; RTST and Dino
  share it. The Windows package identity matches the Partner Center reservation `9N9VHRLCZXN5` (`STORE.md`);
  `store-submit-dino` runs only when `DINOREADER_STORE_APP_ID` is set. (Slice 67: docs/plan/slices/67-dino-reader-ios-windows.md)
- Same `SafeAreaEdges` pattern as RTST (`None` on page/root, `Container` on header, sheet or body; `Container, SoftInput`
  on text-entry pages). Bar icon colour comes from `SystemBarIcons`, which picks it from the page background shown,
  because MAUI follows system night mode but only the reading screen does. (Slice 73: docs/plan/slices/73-edge-to-edge-safe-areas.md)
- Dino pins `UserAppTheme = Light` (`App.xaml.cs`). The reading screen's night theme must read the system theme through `SystemTheme.IsDark` (`SystemTheme.cs`), never `RequestedTheme` or `PlatformAppTheme` directly: Windows reads `UISettings` there because `PlatformAppTheme` lags. (Slice 77: docs/plan/slices/77-dino-covers-and-light-theme.md)
- Cover photos are `cover-<id>.jpg` in the story folder; only those names may be deleted via `IStoryLibrary.DeleteFileAsync`. (Slice 77: docs/plan/slices/77-dino-covers-and-light-theme.md)
- Dino grown-up mode is a `GalleryPage` state (`GrownUpMode` singleton, never persisted), not a page: the same grid with a `TileOverlay` per tile and the Add tile first. There is no Manage page; Edit, the Add tile and a tile tap in grown-up mode all open `StoryWizardPage`; `StoryActions` holds Edit/Cast/Read/Delete. `DinoApp` alone resets it (`GrownUpMode.Leave()` inside `CloseGrownUpPagesAsync`): on window stop, or on returning past `AwayTracker.DefaultThreshold` from an app-launched activity; a quick file-picker trip keeps it. Every grown-up action re-checks `GrownUpMode` after each await, since the mode can end mid-action. (Slice 80: docs/plan/slices/80-grownup-gallery-overlay.md)
- The story editor is `StoryWizardPage` (Pages, Cover, Text); `AddStoryPage`/`EditStoryPage`/`PagesPage` are gone. (Slice 78: docs/plan/slices/78-dino-page-review-wizard.md)
- Never delete a story picture before the new `story.json` is saved (`StorySaver`). (Slice 78: docs/plan/slices/78-dino-page-review-wizard.md)
- Rotate/crop invalidate only that page's reading; edited text is kept. (Slice 78: docs/plan/slices/78-dino-page-review-wizard.md)
- Page text is composed from included regions only (`StoryPage.Regions`; null = heuristic); a region toggle never silently replaces typed text. (Slice 79: docs/plan/slices/79-background-text-filtering.md)
- Starter stories are `.dino` packages in `Resources/Raw/starters/`, shipped as `MauiAsset`s with flat names (the Windows package drops folders) and imported once by `StarterStories` (flag `dino.starters.installed`; a deleted starter never returns). Rebuild them with `DINO_BUILD_STARTERS=1 dotnet test tests/DinoReader.Tests` after editing `tests/DinoReader.Tests/StarterTexts.cs`. Speaker tags must read `Name said` or `said Name` with a bare capitalised name, or the caster finds no character. "Export story" is developer-only (`DeveloperMode`: Debug build, or seven taps on the gallery title in grown-up mode), never a kid or parent action. (Slice 91: docs/plan/slices/91-dino-story-packages-and-starters.md)
