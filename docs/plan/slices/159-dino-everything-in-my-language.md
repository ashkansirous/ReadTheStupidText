# Slice 159 — Dino: everything in the chosen language

*Built 2026-10-27 (PR #473).* Dino Reader only.

**Batch:** 35 (Dino language, 2026-10-27; Decision 164)
**Touches:** `src/DinoReader.App/UiCulture.cs`, `src/DinoReader.App/Resources/Strings/AppResources.cs`,
`src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/LanguagePage.xaml.cs`,
`src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/IntroWizardPage.cs`,
`src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/GalleryText.cs`,
`src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StoryTranslator.cs`,
`src/DinoReader.App/LanguageCard.cs`, `src/DinoReader.App/StoryWizardPage*.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`,
`src/DinoReader.App/TranslationPackPrompt*` (new), `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`,
`src/ReadTheStupidText.Mobile.Shared/ITranslationPacks.cs` (new; pack checks stay off `Application/Translation/ITranslator.cs` so RTST does not rebuild), `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MlKitTranslator.cs`,
`worker/stories.ts`, `worker/stories.test.mjs` (new), `tests/DinoReader.Tests/**`,
`src/DinoReader.App/CLAUDE.md`

## Why

The user, with the app set to Farsi (2026-10-27): "half of the shit is still in english. All the book titles, the store,
the buttons on the parents page... in on boarding, I clicked on the first page to download some free stories, but when I
came back, the wizard text and buttons changed to english. When I say the app's language is in a specific language, all
the app text, buttons and content should be presented in that language." And for parents' own books: "every thing
should be in that language including the parents scanned or written books. every thing."

Grounding (origin/main f1083c9):

- **Strings are complete; the language is not applied reliably.** All 301 resx keys have Farsi values (no English copies,
  `LocalizedUiTests` checks parity) and no Dino XAML or C# has hard-coded English. But `AppResources.Get` reads
  `CultureInfo.CurrentUICulture` (`Resources/Strings/AppResources.cs:18`), which `UiCulture.Apply` assigns per thread /
  async flow (`UiCulture.cs:21-25`; the UI thread got an explicit culture at `MauiProgram.cs:23`). `src/DinoReader.App/CLAUDE.md`
  already warns that `CurrentUICulture` "only changes inside the picker's async flow"; RTST's own `UiCulture` never assigns it.
  The wizard rebuilds all its labels in `Render()` after returning from the store (`IntroWizardPage.cs:110-118`), on a flow
  without the chosen culture, so it flips to English. Likely the same for the parents page. (Most likely cause from the
  code; the first task confirms it on the emulator.)
- **Shipped titles only translate on the reading screen** (`ReadingPage.xaml.cs:198`, Slice 110). The shelf shows
  `story.Title` raw; the store shows `meta.json`'s single `title` (`worker/stories.ts`). Every shipped `.dino` already
  carries the title in all languages (`Story.Translations`, Slice 95).
- **Own books translate only on Android, and the pack downloads silently, over mobile data**
  (`Mobile.Shared/Platforms/Android/MlKitTranslator.cs`: `DownloadModelIfNeeded` on first use, `RequireWifi = false`).
  The shelf never shows an own book's translated title. iOS and Windows register no `ITranslator`.

## Decisions (Decision 164, settled with the user 2026-10-27)

1. **The rule:** when the app language is X, every word the app shows is in X: buttons, screens, alerts, the store, book
   titles, and book text, including books a parent scanned or typed. The only exceptions: a grown-up's explicit per-book
   "Show original text" (Slice 110), and books whose translation is impossible on this device (Decision 6 below).
2. **Strings always follow the stored pick**, never the thread's `CurrentUICulture`: `AppResources.Get` resolves through
   one app-wide culture set from `LanguageChoice` (the existing `AppResources.For(key, culture)` path), and number formats
   use the same culture.
3. **Titles show in the chosen language only** (no original underneath), on the shelf, the store and the reader. The store
   listing gets a `titles` map (language code to title) in `meta.json`, filled by the store builder from the book's
   bundled translations; the Worker passes it through (trimmed, capped like `title`); the app picks the chosen language and
   falls back to `title`. An older app ignores the field.
4. **The store lists every book**, sorted as today (every shipped book reads in every language).
5. **Own books translate on the phone, never through a server** (no unauthenticated public translate API). **Nothing
   downloads on its own:** when a parent turns on translation for an own book in another language and the pack is missing,
   the app says it will download that language's translation pack (about 30 MB) and asks; Download shows progress and
   Cancel; declining leaves the book in its own language. Packs are per language, not per pair (en->fa needs fa; whether
   `en` must also download is checked on the emulator). The translation (text and title) is saved inside the book once, so
   the shelf, grown-up screens and reader show it offline afterwards.
6. **iOS and Windows:** no on-device translator today (Windows has none for ordinary PCs; ML Kit exists on iOS but no .NET
   binding is known). A short check looks for an iOS ML Kit Translate binding; if none, own books on iOS and Windows stay in
   their own language with a one-line note in the book's language card saying translation is not available on this device.
   A follow-up slice covers iOS if a binding turns up.
7. **Layout still never mirrors** (Slice 96): this slice is about words, not direction.

## Work

Sub-slice A: the language sticks (smallest end-to-end fix; ship first if the slice runs long)
- [x] Emulator, Farsi: confirm the wizard flip (Store round trip) and the English parents-page buttons; log the culture
      `Render()` sees.
- [x] `AppResources`: one app-wide culture from `LanguageChoice`, set at startup and on change; `Get` uses it, never the
      thread's culture. `UiCulture.Apply` stops assigning `CurrentUICulture` per thread; fix its stale "no picker" comment.
      Number/percent formatting (`StorePage.xaml.cs:63`, `FirstRunPage.xaml.cs:260`) uses the same culture.
- [x] Unit test: switch the language, then resolve strings on a fresh thread and after an `await` on another flow; all in
      the new language.

Sub-slice B: shipped books' titles
- [x] Shelf (`GalleryPage`/`GalleryText`): show `StoryTranslator`'s title for the chosen language (bundled table, synchronous)
      for every shipped book.
- [x] Store: store builder writes `titles` into `meta.json`; `worker/stories.ts` passes it through (validation + tests);
      `StoreClient`/`StorePage` show the chosen language's title, fallback `title`. Re-upload all store books (Slice 158's
      job, or `tools/upload-store-story.ps1`).

Sub-slice C: own books, on request
- [x] `ITranslationPacks` (new, Mobile.Shared; implemented by `MlKitTranslator`): `IsPackReadyAsync(language)`, `DownloadPackAsync(language, progress, cancel)`; `MlKitTranslator` never
      downloads inside `TranslateAsync` (missing pack = not translated). RTST Mobile keeps its current behaviour through
      its own call path (it is out of scope here).
- [x] Translation pack prompt (resx in all 14 languages): size, Download with progress and Cancel, decline keeps the
      original; shown when a parent turns translation on (book's language card, new-book wizard) and the pack is missing.
- [x] Save the translated title and text in the own book after the first translation; shelf, grown-up screens and reader use
      it; re-translate when the book's text or the app language changes.
- [x] iOS check: is there a maintained .NET binding for ML Kit Translate? Record the answer here. If none: the "not
      available on this device" note on iOS and Windows.

Sub-slice D: guards
- [x] Test that fails on any user-visible string literal assigned in Dino XAML or C# (Text/Title/Placeholder, alerts).
- [~] Scripted headless emulator run in Farsi: intro wizard with a Store round trip, shelf, store, parents page, reader,
      an own English book with the pack prompt; a screenshot of each screen in the slice's review notes.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green, including the new culture and literal tests.
- Worker tests green for `titles`.
- Emulator in Farsi: no English on any screen in the run above; returning from the store leaves the wizard in Farsi;
  shelf and store titles in Farsi; an own English book asks before downloading, then reads and shows in Farsi offline;
  airplane mode with no pack keeps the book in English and never starts a download.

**Parallel note:** `StoryTranslator.cs` has an uncommitted edit in the `s154b` worktree (Slice 154); sequence sub-slice B/C
after it merges or rebase onto it.

*Changed while building:*
- Shipped `.dino` tables carried no titles (the plan's premise was wrong), so titles live in the new `ShippedTitles.cs` (15 titles x 13 languages, machine-drafted, unreviewed), matched by `SourceId` only; shelf, reader and store rows read it. Store builder writes `titles` into `meta.json`; `worker/stories.ts` validates and passes it (new `worker/stories.test.mjs`); `StoreCatalog`/`StoreItem.Titles`. Fixed the Tortoise-and-Hare zh title. Live `meta.json` gets `titles` only at each book's next version bump, so the re-upload item is open and the app reads `ShippedTitles` meanwhile.
- A: percents use `AppResources.Culture`; `GalleryPage` re-applies all texts (`ApplyTexts`) and titles on a language change; `StorytellerStatusLine.Invalidate`.
- C: `ITranslationPacks` implemented by `MlKitTranslator` (RTST unchanged: `AutoDownload` defaults true; `StoryTranslator` sets false). `TranslationPackPrompt` (resx in 14 languages, overlay with Cancel, indeterminate because ML Kit gives no byte progress). `OwnBookTranslation` saves the translated title and text in the book; the reader offers the pack once per open. Decline or cancel never writes the story back; a cancelled pack download is deleted after it lands unless it was already there or wanted again.
- iOS check: binding `EngageSolutionsGroup.Apple.MLKitTranslate` 8.1.0 exists but is not wired; iOS and Windows still have no engine (a follow-up).
- D: `NoHardCodedUiTextTests` guard, plus culture, own-book and titles tests. Emulator screenshots for the review notes were not recorded (item marked `[~]`).

*From review (two passes), fixed:* stale-story overwrite of translations, silent re-translation without a wait, existing books never prompted, cancel not cancelling, title match by English title, cancelled offer throwing on restarts, decline turning the switch off, deleting shared packs, iOS/Windows null translator, double `endWait`, stale shelf titles.

*Checks:* DinoReader.Tests 1546, RTST Tests 1357, worker 94, typecheck clean, both Android builds. Emulator in Farsi: wizard, titles, pack decline/cancel/accept, persistence after restart, language switch updates shelf chrome; no crash.

#### For CLAUDE.md (lifted 2026-10-27)

In `src/DinoReader.App/CLAUDE.md`:
- UI text comes from `AppResources` with the app-wide culture (`LanguageChoice`), never `CurrentUICulture`; cached pages re-apply their text on a language change. (Slice 159: docs/plan/slices/159-dino-everything-in-my-language.md)
- Own-book translation never downloads silently: go through `ITranslationPacks` and `TranslationPackPrompt`. (Slice 159: docs/plan/slices/159-dino-everything-in-my-language.md)

#### Not yet verified

- Native-speaker review of `ShippedTitles` and the 7 new resx keys in 13 languages.
- Store credit line is still English (single string in `meta.json`).
- X Marks the Treasure! bundled translations are English copies (content bug).
- Live `meta.json` gets `titles` only at each book's next version bump.
- iOS ML Kit binding not wired; iOS/Windows have no translation engine.
- Airplane mode (no pack keeps the book in English, no download) untested.
- Debug `-t:Install` crash-loops on a missing sherpa-onnx native lib; use the embedded arm64 APK.
