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
