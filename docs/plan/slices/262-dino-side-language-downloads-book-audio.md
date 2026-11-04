# Slice 262 — Dino: adding a second language downloads the shelf books' audio in it

*In progress (wave 2026-11-04), draft PR.* *Planned 2026-11-04.* Dino Reader only; Android and iOS alike. After Slice 261 (both edit `StoreClient.cs`).

**Touches:** `src/DinoReader.App/StoryLanguages.cs`, `src/DinoReader.App/StoryLanguagesPage.cs`, `src/DinoReader.App/StoryLanguagePacks*.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/AudioPackStore.cs`, `src/DinoReader.App/ShelfAudioPacks*.cs` (new), `src/DinoReader.Core/ShelfAudioPlan*.cs` (new), `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/side-language-audio.yaml` (new), `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/IntroWizardPage*.cs`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/Testing/DinoTestCommands*.cs`, `src/DinoReader.App/Testing/TestStore*.cs`, `src/DinoReader.App/CLAUDE.md` (added at claim from a code check: runner registration and launch resume, the intro languages step, the weekly check's pack sync, and a Debug test store that serves an audio pack), `docs/plan/slices/262-dino-side-language-downloads-book-audio.md`.

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

## Design note (coordinator, claim 2026-11-04, night 2026-11-04-1441 wave 2)

Code check on main after Slice 261 (68bdba69): adding a side language today runs only the reading-voice download and the translation offer (`StoryLanguagesPage.OnDownloadRequested`, `IntroWizardPage.DoneLanguagesAsync`); `AudioPackStore` is the only writer of `audio-packs/` and installs atomically (`.part`, `.tmp`, then `Directory.Move`), `InstalledVersion(book, lang)` says what is in; `StoreClient.WantedPacks` + `AudioPackStore.InstallPacksAsync` already fetch packs, but only at book download and in the weekly `BookUpdater` check.

1. **One owner: a `ShelfAudioPacks` app singleton** (registered in `MauiProgram.cs`), the only thing that runs this fetch. It holds no queue file: each run asks the pure Core `ShelfAudioPlan` for the work, from the shelf's store books, the store listing (`StoreItem.AudioIn(lang)`) and `AudioPackStore.InstalledVersion`, so the plan is always what is still missing. Packs go in one at a time through `AudioPackStore` (never a second writer).
2. **States:** Idle, Running(language, done, total, current book), WaitingForInternet (and WaitingForWifi when the existing Wi-Fi-only rule applies, as `StoryLanguagePacks` does), Cancelled(language). The page's in-row card (frame 169) and the intro languages step only show this state; leaving the page does not stop the run.
3. **Start:** after the reading-voice step of a side-language add (Story languages page and intro languages step), and on app launch or return to the foreground when a listed side language still has missing packs that the store has and the language is not marked cancelled.
4. **Interrupts.** Killed: the half-written pack is discarded by `AudioPackStore`'s atomic install; the next launch recomputes the plan and goes on. Offline: the run waits ("Waiting for the internet…", as Batch 39) and goes on when the connection returns (`Connectivity.ConnectivityChanged`). Cancel: stops the rest, keeps the language and the packs already in, and marks the language cancelled in Preferences so launch does not restart it; removing the language clears the mark, and re-adding it starts again. The weekly `BookUpdater` pack sync is unchanged (it may still fetch for a cancelled language, as today). A pack the store doesn't have is skipped, with the one quiet line per run.
5. **Remove a side language:** packs stay (no surprise deletes); a running fetch for it stops.
6. **Test:** the Debug test store lists `AudioIn` for a test book and serves a fixture pack zip, so `side-language-audio.yaml` adds a language and sees the pack arrive without the live store. Android emulator flow; iOS compiles in CI; nothing Android-only.

## Verification

- `dotnet test tests/DinoReader.Tests`; emulator flow `side-language-audio.yaml` (add a language, a shelf book gets its pack, the book reads it with pre-made audio).
