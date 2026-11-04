# Slice 262 — Dino: adding a second language downloads the shelf books' audio in it

*Planned 2026-11-04.* Dino Reader only; Android and iOS alike. After Slice 261 (both edit `StoreClient.cs`).

**Touches:** `src/DinoReader.App/StoryLanguages.cs`, `src/DinoReader.App/StoryLanguagesPage.cs`, `src/DinoReader.App/StoryLanguagePacks*.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/AudioPackStore.cs`, `src/DinoReader.App/ShelfAudioPacks*.cs` (new), `src/DinoReader.Core/ShelfAudioPlan*.cs` (new), `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/side-language-audio.yaml` (new), `docs/plan/slices/262-dino-side-language-downloads-book-audio.md`.

## Decisions (user, 2026-11-04, RefineScope)

- When a grown-up adds a second ("side") language, in the wizard's last step or in Settings, **every store book on the shelf** downloads its pre-made audio in that language (not only the books picked at first run), plus that language's reading voice as today.
- A progress note shows while it downloads, with Cancel (`WaitController`, any wait over 500 ms); a book whose pack is not on the store yet reads with the reading voice, as today.

## Work

- [ ] Core: `ShelfAudioPlan` (which shelf books lack a pack in a language, from the installed packs and the store listing), with unit tests.
- [ ] App: on a side-language add, fetch those packs through `StoreClient`/`AudioPackStore` (the one writer), one at a time, resumable; progress note text in all 15 languages.
- [ ] Removing a side language leaves the packs (no surprise deletes); the 7-day update check keeps fetching packs for listed languages as today.
- [ ] Android task: emulator flow adds a side language and sees a book's pack arrive. iOS task: same code compiles; nothing Android-only.

## Verification

- `dotnet test tests/DinoReader.Tests`; emulator flow `side-language-audio.yaml` (add a language, a shelf book gets its pack, the book reads it with pre-made audio).
