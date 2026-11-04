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

#### Design (Dino Reader Batch 47 Onboarding mock, frame 169, added 2026-11-04)

Source: `docs/design/dino-reader/Dino Reader Batch 47 Onboarding.dc.html`, 1 frame (line 211, 360x760, the Story languages page). The mock agrees with the plan on one-at-a-time, Cancel and the reading-voice download first; it puts the progress inside the new language's row rather than in a full-page wait.

- **Page.** `Sand` background; header: back arrow, "Story languages" 20/600, "Done" pill (44 high, `Brand`). Intro line "Dino can read your books in these languages too." 13.5 `Body`. White card radius 18: row "English" 15/600 with "Main language" 12.5 `Muted` and a tick (min height 56, `Hairline` divider); then the side-language block, padding 12/14/14.
- **Progress card (inside the Farsi row).** Under the language name ("فارسی" 15/700, tick on the right), a `Sand` card radius 14, padding 12, gap 8: line "Getting Farsi audio for your books" 13.5/600 with "2 of 4" right in `Brand`; a 8 dp bar (`BrandTint` track, `Brand` fill, 44%); below, the book in progress ("The Gingerbread Man" 12.5 `Body`) and a Cancel pill (36 high, white, 2 dp `#e3dccd` border, 13.5/600). The count covers only books that need a pack. Then the quiet line, once: "The Lion and the Mouse has no Farsi audio yet. Dino’s reading voice will read it." 12.5 `Body`. The reading-voice download shows first in the same card, as today. "Add a language" row below (52 high, white, `Brand` 15/600).
- **Behaviour (mock text).** Done leaves and it carries on, resumable; Cancel stops the rest and keeps the language and the packs already in. The same card is used on the intro's languages step.
- **Files.** `StoryLanguagesPage.cs` (card), `StoryLanguagePacks*.cs`, `ShelfAudioPacks*.cs`, `AppResources*.resx`.
- **New strings (15 locales).** "Getting {0} audio for your books", "{0} of {1}" (reuse `Intro_Counter` if it fits), "{0} has no {1} audio yet. Dino’s reading voice will read it." Cancel exists.

**Open point: progress placement.** Slice says "a progress note ... with Cancel (`WaitController`, any wait over 500 ms)"; the mock draws an in-row card with no wait overlay, so the parent can leave. **Default:** in-row card as drawn, plus `WaitController` only for the reading-voice step as today.
**Open point: "has no audio yet" line.** Not in the slice (it only says such a book reads with the reading voice). **Default:** add the line, one per run, naming only the first book plus the count if several.
**Mock decides (slice silent):** the count "2 of 4" excludes books that need no pack; the book in progress is named.

## Verification

- `dotnet test tests/DinoReader.Tests`; emulator flow `side-language-audio.yaml` (add a language, a shelf book gets its pack, the book reads it with pre-made audio).
