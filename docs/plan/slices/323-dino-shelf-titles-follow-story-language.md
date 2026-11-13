# Slice 323: Dino shelf titles follow the story language picked on the shelf
**Product:** Dino

*Built 2026-11-08; Android emulator verified.* User, 2026-11-08: "when I am in the childrens shelf page, when I change the language dropdown that is supposed to change the books language, it does not change the name/title of the books to that language. for example, I changed it to farsi, but the title still remained "good enough" and "The three little pigs"".

**Touches:** `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/StoryTile.cs`, `src/DinoReader.App/ShelfReorderTiles.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/StoryLanguages.cs`, `src/DinoReader.App/ShippedTitles.cs`, `src/DinoReader.Core/**` (only if a pure title-choice helper is added), `tests/DinoReader.Tests/**`, `.maestro/dino/**`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/173-dino-kid-story-languages.md`, `docs/plan/decisions.md`

## What is wrong (three sources)

- **Logs:** the user's TestFlight iPhone (iOS 1.109.0) sent `story_language_switched` `fa` from the shelf at 08:08 UTC on 2026-11-08 (`dino_usage_beta`): the pick reached the app. No fallback or crash concerns titles.
- **Code (origin/main a0a1bcc3):** the shelf button `OpenLanguageMenuAsync` (`GalleryPage.xaml.cs:114-132`) only calls `StoryLanguages.SetPick` and `ApplyLanguageButton`; the comment says "The shelf titles stay in the app language". Tiles are built in `LanguageChoice.Current()` (`:380`, `:447`) and rebuilt only when the app language changes (`ApplyMode`, `:365`). Farsi titles exist (`ShippedTitles.cs`).
- **Device:** the emulator run on dino-v1.118.0 could not reach the shelf button (it shows only with two or more story languages); not reproduced.
- **Why it was built so:** Slice 173 left this as an "open point" with a default ("shelf titles stay in the main language", `173-dino-kid-story-languages.md:64`); the user never chose it. Decision 173 replaces that default.

## Items

1. When the shelf's story-language button is shown, every tile title is in the story language picked there (`StoryLanguages.Pick()`), using the same title source the reading page uses for that language (shipped books: `ShippedTitles`; store books: their per-language titles; own books: their own title, or the translated title where one exists). A book with no title in that language shows its title in the app language as today.
2. Picking a language on the shelf rebuilds the tile titles at once (no leaving the shelf), and they stay that way after leaving and returning and after an app restart (the pick is already remembered).
3. When the button is hidden (one story language), titles stay in the app language, as today.
4. Screen-reader names of the tiles use the same title.
5. The comment at `GalleryPage.xaml.cs:113`, `src/DinoReader.App/CLAUDE.md` and Slice 173's open point are updated; Decision 173 is added.
6. Unit tests for the title choice (picked language present / missing / button hidden). A Maestro flow (Debug hooks: `set-language`, story languages) that sets two story languages, picks Farsi on the shelf and asserts a Farsi title on a tile.
7. Android and iOS share `GalleryPage`; Windows head too. One change covers all.

## Verified

- Android emulator (RTST_Test, 2026-11-08): `shelf-titles-follow-story-language.yaml` passed (Farsi pick shows "سه بچه خوک کوچولو", English brings "The Three Little Pigs" back, both `[ShelfTitles]` markers logged). `stage-book-update`, `premade-audio-starter`, `set-language`, `language-change-fetches-audio` and `shelf-reorder` passed on the reworked shelf refresh.

## Not yet verified

- iOS has no scripted run: the shelf title font for a Farsi title (Vazirmatn) on a tile and in the book sheet.
- On the user's iPhone (TestFlight): picking فارسی on the shelf retitles "Good Enough" and "The Three Little Pigs".

## Seen alongside, not in this slice

- Same 08:08 burst: `premade_audio:clip_missing` x2 and `premade_audio:text_mismatch` x2 for book `6eff8d77` in `fa` (iOS 1.109.0). Not investigated.
- Emulator run on dino-v1.118.0: tapping فارسی in Settings > Story languages left "Story languages: English" and a first-run language page showed; may be the slow arm64-on-x86 emulator. Not investigated.

## From review

- High (`reviews/s323-high.md`): 7 findings, all fixed.
- Medium (`reviews/s323-medium.md`): 1-6 fixed; 1 and 2 were the second miss in the shelf-refresh coordination, so it was redesigned as one serialized refresh loop (`RequestShelfRefresh`, `ShelfRefreshRule`). 7 skipped (a nit: `_shelfLanguage` and `_shelfTitleLanguage` are declared apart and track overlapping state).
- Check pass (`reviews/s323-check.md`): 1, 2, 3, 5 and 7 fixed with one rule (the refresh loop reports a fallback and never throws to callers; a failed build stays stale). 4 skipped because it was already on main: the shelf rebuild clears `_items` then adds, so a throw part-way leaves a partial shelf until the next refresh.
- Check pass 2 (`reviews/s323-check2.md`): 1-4 fixed (first build waits for the starter install; the staged-update refresh reports `UpdateCheckFailed` itself again; a cancel is not a failure).
- Check pass 3 (`reviews/s323-check3.md`): 4 low findings, none fixed. 1: a cancelled rebuild returns true, so the staged-update loop can start one more pass after teardown. 2: `_refreshLoop.IsCompleted` is the only re-entry guard (no re-entry path found). 3: `_callerReportsFailure` is shared by every caller waiting on the same loop, so a failure can be filed under the wrong fallback reason. 4: after two failures the flags stay set until the next trigger (by design).
