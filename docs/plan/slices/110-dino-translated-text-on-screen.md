# Slice 110 — Dino: translated text on screen, with "Show original text"

*Built 2026-10-24, not yet verified on the emulator.* *Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
reading language, one narrator, parent recordings, store filters" (2026-10-24) —
https://drive.google.com/file/d/1b2XPenCW2D4Kx1yx98js_LK4CRBSnnLf/view?usp=drivesdk (part 1, milestone 2).

**Batch:** 22
**Depends on:** Slice 107; runs after 108 and 109 (all three edit `StoryVoicing` and `ReadingSession`)
**Touches:** `src/DinoReader.Core/**` (voiced segment and display-span mapping types), `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/ScanPages.cs`, `src/DinoReader.App/TextPages.cs`, `src/DinoReader.Core/TranslatedStory.cs` (new), `src/DinoReader.App/BookSheet.cs` (new), `src/DinoReader.App/ReadingText.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

When a book reads in the grown-up's language, only the audio is translated; the screen shows the source
text, so what the child sees doesn't match what they hear. Slice 95 put on-screen translation out of
scope; this slice reverses that. It is more than a text swap: the highlight is driven by
`VoicedSegment.SourceStart/SourceEnd/TextStart` (`StoryVoicing.cs:220`), which index the source text, and
scanned pages highlight source-language OCR line boxes (`ScanPages`/`ScanLayout`).

## Decisions (2026-10-24, agreed with the user)

- **Decision 112: the screen shows the translation, with a per-story "Show original text" switch.** When
  `StoryTranslator.Applies`, every page shows the translated text and the highlight follows the
  translated sentence. Farsi and Arabic text is RTL; the layout never mirrors (Slice 96). Scanned pages
  with line boxes render as picture plus a translated text panel (Slice 98's `PictureTextPages` layout)
  while translated. A grown-up per-story switch "Show original text" (default off, saved as an optional
  `story.json` field) returns the screen to the source text and OCR boxes; the audio stays translated.
- On a page with a parent recording (Slice 111), the text shown is in the recording's language.

#### Design (Dino Reader Batch 22-23 mock, frames 41, 42 and 43, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 22-23.dc.html`, frames 41 (lines 132-141), 42 (144-158) and 43 (161-179). Three real-UI frames (360x760 phone frame, 6px `#2b2540` border, radius 34.), except the striped scan in 42. **Frame 41 supersedes the Batch 17-18 / v2 frame 28 note "nothing on screen changes"**: the translated text is shown on screen. The mock agrees with the plan on the behaviour and adds where the "Show original text" switch lives.

- **Reading page, translated (41).** Page `#FFF6E6`. Header: back arrow 44, title **translated** "سه بچه‌خوک" 20/600 RTL, a 40 menu button (radius 12, `rgba(43,37,64,.07)`, `#8a8399` dots). Text box white, radius 26, text 24 with line-height 1.85, `dir=rtl`, right-aligned, Vazirmatn. The layout does not mirror (Slice 96). Spoken sentence: `#FFE27A` highlight, radius 8, 4px spread; past text `#8a8399`; upcoming text dark. Page dots 9 (`#d9d2c4`), active 26x9 `#5B57E8`. Controls: skip 72 circles `#ECE7FF` and play 104 circle `#FF7A59` with shadow `0 8px 0 #E2583A`.
- **Scanned page, translated (42).** Title "قصهٔ پیتر خرگوشه". The page switches to the **picture + text layout from frame 34**: the scan fills the page (the mock draws a striped placeholder labelled "scanned page 2 (English print)", a stand-in, not UI) and the Farsi text sits in a white panel (about 58% height, 92% opacity, radius 18, shadow `0 6px 18px rgba(43,37,64,.18)`, text 20, line-height 1.8) with the same sentence highlight. Held sideways it uses frame 35. The English OCR boxes of frame 28 are not drawn while translated.
- **This book sheet (43, 110-4).** Opened from the reading page's add-book button, behind the grown-up gate. A bottom sheet over a dimmed page (`rgba(43,37,64,.5)`), white, radius 28/28/0/0. Title "This book" 20/600, subtitle the book title 13 `#8a8399`. Row "Show original text" (card `#F3EFE6`, radius 16, 64 high) with the line "Show the English words. The voice still reads in فارسی." 12.5 `#5a5470` and a switch (52x32, **off** = `#d9d2c4`). Second row "Read with my voice" (Slice 111). Two outlined pills Record (with a mic icon) and Cast (48 high, radius 24, 2px `#c9c0f5`), then "Back to the story" (50 high, radius 25, `#F3EFE6`, 15.5/600).
- **Behaviour (note).** "Show original text" is off by default, saved per book, and shown only when the book is being translated. It brings back the source text and its OCR boxes; the audio stays translated.

Changed to match the mock (2026-10-24): the switch lives in a "This book" sheet opened from the add-book button behind the gate (the plan said "the reading page's grown-up controls"); the line under it names the source and spoken languages. The scanned layout is the frame-34 picture + text layout, sideways frame 35. Icons in the mock are inline SVG strokes (2.2 wide, `#5B57E8`), not Phosphor names; the app uses its own `IconKind` set.

New strings (all 14 locales): "This book", "Show original text", "Show the {0} words. The voice still reads in {1}." (both language names), "Back to the story".

## Work

- [x] 1. Core: translated page text plus a piece-to-display-span map beside the source offsets, so a
  voiced segment can highlight either text; unit tests including RTL and multi-piece sentences.
- [x] 2. `ReadingSession`/`ReadingSurface`/`TextPages`: render and highlight the translated text.
- [x] 3. Scanned picture pages switch to the picture-plus-text layout while translated.
- [x] 4. The "Show original text" switch (grown-up only, in the "This book" sheet from the add-book button behind the gate, frame 43; shown only when translated) and its persistence.
- [x] 5. Rule (written as the For CLAUDE.md block below, not edited in the file) in `src/DinoReader.App/CLAUDE.md` (amend the Slice 95 "speakers in the source language"
  bullet: the screen shows the translation).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator, app in Farsi: a starter and a scanned English book show Farsi RTL text, highlight
  follows each spoken sentence, the scanned book shows picture plus text; "Show original text" brings back
  English and the OCR boxes.

## Built (2026-10-24)

- Core `TranslatedStoryBuilder.Build` (`TranslatedStory.cs`) turns the translated pieces into pages, joined text and **one span per source sentence in the same order**, so the spoken-sentence index, dots, taps and `PageFollower` work unchanged; only the text differs. Pieces map to a sentence by `SourceStart`; a quote's pieces get quote marks; paragraph breaks stay; pages keep `ImageFile` and drop `Lines` (so a scan becomes a picture + text page and `PictureTextPages` draws it, no OCR boxes).
- `StoryVoicing.TranslatedDisplayAsync` (null when not translated, "Show original text" on, or a model is missing) and `StoryTranslator.TitleAsync` (title from the table, else the engine, else as is). `ReadingPage.ShowStoryAsync` shows the source first only when the engine is needed, then swaps; a bundled table waits for the translation.
- A translated page ignores the spoken story index (`DisplayedPosition` is -1): offsets differ, so a sentence that spans pages follows from its first word.
- `Story.ShowOriginalText` (`story.json` `showOriginal`, absent when off). `BookSheet` (frame 43) opens from the add-book button behind the gate **only for a book that is being translated**; other books go to grown-up mode as before. Cast pill leaves for the cast screen in grown-up mode. Strings `Book_*`, Slice 110 block, all 14 locales (machine-drafted).
- `ShowOriginalText` is saved over the latest story and stripped from `.dino` exports (`StoryPackageWriter`), kept in backups. `StoryTranslator.SharedWork` shares each engine piece between the screen and the voice and serialises work on the same key.
- Not built: the Record pill and "Read with my voice" row (Slice 111); text in a parent recording's language (Slice 111).

*Changed while building:* a sentence running over a page break is shown once, on its starting page (medium review; the earlier note that it shows on every page is superseded). Source text shows first and swaps once when the engine is needed.

*From review (high 1-9, medium 1-10, all applied):* tracked mode for the translated page; the sheet's Dismissed resumes reading; the screen and voice share engine pieces; quote marks span pieces; the sheet opens only when `StoryTranslator.Applies` (the grown-up path change is in For CLAUDE.md); one source-then-translation swap; `ShowOriginalText` persistence and export stripping; `SharedWork` serialisation. Deferred to Slice 111: Record pill, "Read with my voice".

## For CLAUDE.md (lifted 2026-10-25)

- (`src/DinoReader.App/CLAUDE.md`, amend the Slice 95 "speakers in the source language" bullet) The screen shows the translation while a book is translated (`StoryTranslator.Applies`): `StoryVoicing.TranslatedDisplayAsync` builds a `TranslatedStory` with one span per source sentence, so sentence indexes are shared by source and translation; never map positions between the two texts, a translated page follows the sentence only. A grown-up's per-book "Show original text" (`Story.ShowOriginalText`, the "This book" sheet from the reading page's add-book button, translated books only) returns the source text and OCR boxes; the audio stays translated. Amend the Slice 97 sentence "it still opens the gate, then grown-up mode" (the reading page's grown-ups button): for a translated book (`StoryTranslator.Applies`) the reading page's button opens the "This book" sheet after the gate instead, the story stays paused under it and resumes when the sheet is dismissed (Back, scrim, system back button); grown-up mode is reached only through the sheet's Cast pill (it has no direct entry, the mock has none). Any other book still goes straight to grown-up mode. The screen's translation and the voice share each engine piece (`SharedWork` in `StoryTranslator`), cancelled when the page goes away; a sentence running over a page break is shown once, on the page it starts on. (Slice 110: docs/plan/slices/110-dino-translated-text-on-screen.md)

#### Not yet verified

- Emulator: an English book in Farsi shows Farsi RTL text with the highlight following (mock frame 41).
- Emulator: a scanned page shows as picture plus translated text (frame 42).
- Emulator: "Show original text" round-trip brings back English and the OCR boxes (frame 43).
- Native-speaker check of the machine-drafted `Book_*` strings in all 14 locales.
- Starter book titles may stay English where the tables lack titles.
