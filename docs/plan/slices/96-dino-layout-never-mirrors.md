# Slice 96 — Dino's layout never mirrors when the language changes

*In progress (wave 2026-10-24), draft PR.* Planned 2026-10-24, from the user's request "do not change the layout direction when changing language".

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

#### Design (Dino Reader Batch 17-18 mock, screens 29 and 30, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 17-18.dc.html`. Two frames: shelf in Farsi (screen 29, lines 67-82) and a Farsi book in the reader (screen 30, lines 84-94). The mock agrees with the plan: nothing mirrors, only RTL text aligns right inside its own box. Both frames are real UI at 360x760.

- **Shelf in Farsi (29, Decision 100).** Page `#FFF6E6`. Header row, padding 10/16/12, gap 10: app icon 40x40 radius 11 on the left, label "قصه‌های من" (`dir="rtl"`, Vazirmatn 21/700, `text-align:right`, flex:1) and the 44x44 add-book button on the right (Slice 97). Icon left and button right are the same as English. Grid is 2 columns, 14px side padding, 12px gap, tiles fill left to right. Tile titles follow their own text: "Good Enough", "The Three Little Pigs" and "The Gingerbread Man" stay left-aligned (Fredoka 14/600, line-height 1.25); the Farsi book "خاله سوسکه" is `dir="rtl"`, Vazirmatn 14/700, line-height 1.35, right-aligned. Its generated cover is `#B9E4C9` with the title in Vazirmatn 18/700 `#2b2540`, line-height 1.35, centred (Latin generated covers are Andika 18/700 white).
- **Farsi book in the reader (30, replaces v2 frame 14).** Header (wrap, padding 8/12/4/8, gap 8): back arrow 44x44 left, spacer, add-book button 40x40 radius 12 right, then the title on its own row (`order:3`, full width, padding 0 8): "پیتر و دُم‌پنبه" `dir="rtl"`, Vazirmatn 20/700, line-height 1.3, right-aligned. Text card: margin 6/14/0, radius 24, fill `#DFF5E3`, padding 24/22, content centred vertically. Story text is a `dir="rtl"` flex column, gap 3, Vazirmatn 24, line-height 1.8, `#2b2540`, right-aligned. The spoken sentence (two wrapped lines) has fill `rgba(255,214,0,.42)`, inset ring 2px `#F5B800`, radius 9, bleeding 6px past the text edge (`margin:0 -6px; padding:0 6px`). Page dots (9px round `#d9d2c4`, active 26x9 pill `#5B57E8`, gap 8) fill left to right; controls 72 / 104 / 72 circles unmirrored, previous left and next right. Copy: "پیتر تا درِ باغ پرید.", "«من هم بیام؟» پیتر پرسید.", "دُم‌پنبه گفت: «اگه تند", "بیای!» و دوید و رفت."
- **Behaviour (notes).** Swipe left for the next page, as in English. Only the story text and title are right-aligned and laid out right to left; the sentence box is unchanged. Same rule on gate, wizard, store, backup and first run.

**Settled by the mock: tile covers.** A generated cover for an RTL title uses Vazirmatn with dark text `#2b2540` on a pale colour (`#B9E4C9`); white text stays for the Latin covers on saturated colours.

New strings: none (the Farsi samples above are mock data, not shipped copy).

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
