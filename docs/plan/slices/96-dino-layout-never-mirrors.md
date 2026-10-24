# Slice 96 — Dino's layout never mirrors when the language changes

*Planned 2026-10-24*, from the user's request "do not change the layout direction when changing language".

**Batch:** 18 (Dino Reader: layout, add-a-book button, picture + text books)
**Depends on:** Slice 93 (language picker), Decision 92 (RTST Mobile already never mirrors, Slice 85)
**Touches:** `src/DinoReader.App/UiCulture.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/IconView.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/BackupPage.xaml.cs`, `src/DinoReader.App/CastPage.xaml.cs`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/StoryWizardPage.xaml.cs`, `src/DinoReader.App/*Step.xaml*`, `src/DinoReader.App/CLAUDE.md`

## Why

Picking Farsi or Arabic flips every Dino screen right to left. `UiFlow.ForCurrentCulture()`
(`UiCulture.cs`) is set app-wide in `App.xaml.cs` and on each page. `IconView` mirrors the arrows,
`UiCulture.Mirror` mirrors the paddings, and the reader flips its layout and swipe direction from the
book's language (`ReadingSurface`, `ReadingPage`). Moving buttons and turning pages the other way
confuses a small child, and the user wants it gone.

## Decisions (2026-10-24, agreed with the user)

- **Decision 100: Dino's layout never mirrors.** Every screen keeps its left-to-right layout in every
  language: the gallery, grown-up pages, gate, wizard, store, backup and first run. Arrows point the same
  way, paddings stay physical, and the gate keypad stays as it is. This extends Decision 92 (RTST Mobile)
  to Dino.
- **Text follows its own content.** A label, title, card or story text in Farsi or Arabic is
  right-aligned and laid out right to left inside its own box (the per-text treatment `StorePage` already
  uses for titles). Mixed text (an English name in a Farsi label) still renders correctly.
- **The reading page doesn't mirror either, even for a Farsi book.** Pictures stay where they are and
  page turns go the same way as an English book (swipe left for the next page). Only the story's text
  lines are right-aligned. The highlight is unchanged.
- The language picker, fonts and voice choice are unchanged. Only the direction changes.

## Work

- [ ] 1. **Smallest end-to-end: the gallery in Farsi stays left to right.** `UiFlow` always returns
  LeftToRight; `App.xaml.cs` and each page stop setting a culture-driven direction. Tile titles and the
  header labels get per-text direction from their own text.
- [ ] 2. **Icons and paddings.** `IconView` stops mirroring arrows on the UI culture; `UiCulture.Mirror`
  is removed or becomes the identity, with its call sites cleaned up.
- [ ] 3. **Every grown-up screen.** Backup, Cast, FirstRun, Gate, Language, Store and the wizard steps:
  layout LTR, each text element right-aligned when its text is RTL. Remove the now-redundant LTR overrides
  (GatePage keypad, ZoomablePage) only where they're truly redundant.
- [ ] 4. **The reader.** `ReadingSurface` keeps the page layout and swipe direction LTR for every book; the
  text views of an RTL book are right-aligned (`FlowDirection` on the text element only).
- [ ] 5. Rule in `src/DinoReader.App/CLAUDE.md` (written at planning; check it still matches).

## Verification

- Unit: any pure helper that decides per-text direction (e.g. from the first strong character) has tests
  in `tests/DinoReader.Tests`.
- Emulator (device-test): switch the language to Farsi, then Arabic, then English. On each, the gallery,
  grown-up mode, gate, wizard, store, backup and a reading page keep the same positions as English. Farsi
  text is right-aligned, back arrows point left, and swiping left turns to the next page in a Farsi book.
- Windows head: the same check on the gallery and the reader.
