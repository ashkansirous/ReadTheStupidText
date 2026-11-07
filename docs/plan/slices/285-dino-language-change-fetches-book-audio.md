### Slice 285 — Dino: changing the app language fetches the books' sound in that language

*Built (2026-11-07), branch fix/dino-language-change-audio.*

**Built:**
- Reviews: high `reviews/285-high.md` (all 10 applied); medium `reviews/285-medium.md` (1-5 and 8 applied; 6 checked on the emulator; 7 by design, since a quiet start follows the Wi-Fi rule).
- Fast checks: DinoReader.Tests 3757 passed, Android build, FallbackGuard 47/47.
- Emulator-5554 runs, 2026-11-07:
  - `language-change-fetches-audio` and `language-change-back-key-fetches-audio` PASS. The logs show `shelf audio fa: run starts (added)`, the fa packs installed and `premade clip plays`.
  - `premade-audio-plays` and `silent-piece-beat` PASS.
  - The first run of the two new flows failed on a flow bug: the open-book hook refuses in grown-up mode. The flows now leave grown-up mode first.

**Not yet verified:**
- iPhone: switch the app language, open a store book, press Play. Sound should play in the new language with no relaunch.
- Windows head: the same.
- The user's phone: Freddy in English after the v6 text update. The English `clip_missing` events of 2026-11-07 07:05Z have no code explanation yet.

**Touches:** `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.App/ShelfAudioPacks.cs`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/Testing/**`, `src/DinoReader.Core/ShelfAudioLanguages.cs`, `.maestro/dino/**`, `tests/DinoReader.Tests/**`, `docs/plan/slices/285-dino-language-change-fetches-book-audio.md`

## Why

User, 2026-11-07, on Dino 1.110.0: "the freddy's story does not have any sound. It says this part of the story/app does not have a sound! how the fuck is it passed your tests? fix it!"

What the three sources showed (2026-11-07):

- **Store:** fine. Freddy's text changed on 2026-11-06 (book v5 to v6, commit 572459ef). The store upload at 21:55 checked every manifest ("All 225 audio manifests line up") and rebuilt all 15 packs (en v2, fa v3, ar v3, ...). `/api/stories/<id>/audio/en?v=2` serves the zip.
- **Emulator, release 1.110.0, fresh install:**
  - English plays: the en v2 pack is installed and 117 of 117 pieces have a clip.
  - Switching the app language to Farsi on Grown-ups, Settings, Language gives no `shelf audio fa` and no `AudioPack` line. No fa pack is ever requested.
  - Play shows "This part has no sound" on every piece.
- **The app's own events (dino_usage):** a 1.110.0 session on 2026-11-07 07:05Z logged `fallback premade_audio:clip_missing` (en 20, fa 12), with Freddy opened and read in both languages and the language changed several times.

**Cause (proven for Farsi, by the emulator run):**
- `LanguagePage` (the main app language) saves the new story-language set, but never calls `ShelfAudioPacks.Start`.
- The other two ways to add a language do call it: `StoryLanguagesPage.StartAudioAsync` and `IntroWizardPage` line ~520.
- Slice 278 part B (5e786735) rewrote `LanguagePage` (-141 lines) and deleted `.maestro/dino/side-language-audio.yaml` and `first-run-pack-start.yaml`. Those were the emulator flows that would have caught this.
- `ShelfAudioPacks.Resume` at launch would fetch the pack. But it is rate-limited and only runs at launch or return, so a language picked mid-session stays silent.

**Not yet explained:** the English `clip_missing` misses on the user's phone. Their phone had Freddy's en pack from 2026-11-06 10:30, which was v1 for the old text. A 22:31 download is logged but its version isn't. If v1 was still the open pack when the v6 text arrived, the changed pieces miss.

## Work

- [x] **A. Language change starts the fetch.**
  - When `LanguagePage` is left with a changed language (`CommitSides` / `BackAsync`), call `ShelfAudioPacks.Start` for every language in the new set that is new compared with the set the page opened with. Use the same `ShelfAudioAsk` mobile-data question as `StoryLanguagesPage`.
  - The new main language counts, and so does a side language the change added.
  - Android, iOS and Windows: this is shared code, so all three get it.
- [x] **B. A book update fetches its newer pack at once.**
  - Check `BookUpdater` and `ShelfAudioPacks` for this case: a book on the shelf moves to a new version, and its listed pack version for an installed language rose.
  - The new pack must be fetched in the same check, and the reading page must open the newest installed version.
  - If it isn't (e.g. only the next launch `Resume` or the weekly check would do it), make the update start the shelf run for that language.
  - Add a unit test for the decision (pure code in `DinoReader.Core` if it lives there).
- [x] **C. The reading page tells the truth.**
  - "This part has no sound" is the default whenever no shelf run covers the book (`ReadingPage.xaml.cs` ~1098). Only one of those cases is real.
  - When the book has a pack listed in this language on the store and it isn't installed, start the shelf run for that language from the reading page. Show its downloading line instead of "no sound".
  - Keep "no sound" only for pieces the installed, newest pack really lacks.
- [x] **D. Tests that would have caught it.**
  - An emulator flow (`.maestro/dino/`): fresh install, starter book on the shelf, Grown-ups, Language, pick another language, open the book, Play. Assert the `shelf audio <lang>: run starts` line and a `premade clip plays` line, and that "no sound" is never shown.
  - A unit test for the set difference that decides which languages start (if pure).

## Verification

- Fast checks: `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`, plus the Dino app build for Android.
- Emulator (after both reviews):
  - The new flow.
  - The user's exact path on Freddy: switch to Farsi, then Play. Sound plays with no relaunch.
- iOS: compile only from Windows. The flow is listed as not yet verified on an iPhone.
