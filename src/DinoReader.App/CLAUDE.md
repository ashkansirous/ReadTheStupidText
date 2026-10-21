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
> Its screens are the mock `docs/design/dino-reader/Dino Reader v2.dc.html` (index in
> that folder's README): read visual values (colours, sizes, copy, layout) from it,
> never guess them, and never call `DesignSync` for them.

- Dino Reader: stories are `AppDataDirectory/stories/<guid>/story.json` via `FileStoryLibrary`, images only through `TryResolveFile`; grown-up pages implement `IGrownUpPage`, and app-launched activities are wrapped in `ExternalActivityGuard`; tokens come from `DinoTokens`. (Slice 61: docs/plan/slices/61-gallery-grownup-gate.md)
- Dino Reader reading: `StorySentences` and `SegmentSentenceSplitter` map `ChunkChanged` 1:1 to sentences; `FlowPager`/`PageComposer` (`DinoReader.Core`) paginate off the UI thread; story text ignores the font scale. (Slice 62: docs/plan/slices/62-reading-screen-modes.md)
- Dino Reader cast: edits go only through `CastEditor` and pin voices via `CastMember.Edited`; merges live in `Story.Merges`, applied each read by `CastMerges` in `StoryVoicing`, never in stored text; an unknown speaker gets the narrator voice (`VoiceCaster.VoiceForLine`). (Slice 65: docs/plan/slices/65-cast-screen.md)
- Dino languages: rules per language in `RuleSets` (cached per `AppLanguage`, `LanguageRules.For(code)`); only `ReadingPage` switches `SpokenLanguage`, never the cast screen; translation stays hidden until an `ITranslator` is registered in Dino's DI. (Slice 66: docs/plan/slices/66-dino-reader-languages-translation.md)
- Dino picture mode on real pages: `ScanLayout` (Core) maps sentences to line boxes; `ScanPages`/`ZoomablePage` (App) render them. Boxes are in the picture's own 0..1 coordinates, so zoom and EXIF rotation keep them on the text. Gestures go on the untransformed outer view; only the inner layout is scaled. Imports keep pictures only while the text is unedited; temp files live in a per-import folder and must be deleted on every exit path. (Slice 64: docs/plan/slices/64-picture-mode-real-pages.md)
- Dino Reader on Windows (Slice 67): a MAUI WinUI head, MSIX, no tray/hotkey. `Platforms/Windows/Package.appxmanifest` carries the reserved Partner Center identity (`AshkanSirous.DinoReader`, Store ID `9N9VHRLCZXN5`); tile and splash art are copied from `docs/design/dino-reader/icons/windows` (no `<MauiIcon>`). Build/package with `dotnet publish -f net10.0-windows10.0.19041.0 -p:IncludeAndroid=false -p:Platform=x64|ARM64` and never a global `RuntimeIdentifier` (see `.github/CLAUDE.md`). Windows has no `IDocumentScanner`, so the Scan tile is dimmed; the voice, OCR, PDF and image-keeper services are in `Mobile.Shared/Platforms/Windows` (the neural reader is Infrastructure's `SupertonicSpeechReader`, which now reads segments). `WindowsTextMeasurer` (Win2D) is the Windows `ITextMeasurer`. (Slice 67: docs/plan/slices/67-dino-reader-ios-windows.md)

- Dino Reader builds for Android, iOS (`-p:IncludeIos=true` on Windows, compile only) and Windows (MAUI WinUI, MSIX via
  `dotnet publish -f net10.0-windows10.0.19041.0 -p:IncludeAndroid=false -p:Platform=x64`). On a machine with only
  maui-android, pass `-p:IncludeWindows=false`. Platform code lives once in `Mobile.Shared/Platforms/<OS>`; RTST and Dino
  share it. The Windows package identity matches the Partner Center reservation `9N9VHRLCZXN5` (`STORE.md`);
  `store-submit-dino` runs only when `DINOREADER_STORE_APP_ID` is set. (Slice 67: docs/plan/slices/67-dino-reader-ios-windows.md)
