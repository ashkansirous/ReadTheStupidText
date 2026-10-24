# Slice 97 — The grown-ups button says "add a book", not a lock

*Planned 2026-10-24*, from the user's request "the menu button for grown-ups is very bad. It should be
something indicating that you could add a book, not a lock."

**Batch:** 18 (Dino Reader: layout, add-a-book button, picture + text books)
**Depends on:** Slice 80 (grown-up mode as a gallery state), Slice 61 (gate)
**Touches:** `src/DinoReader.App/IconView.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/CLAUDE.md`

## Why

The gallery header's grown-ups button (`LockButton`, `IconView Kind="Lock"`, `GalleryPage.xaml`) and the
reading page's (`LockIcon`, `ReadingPage.xaml:31`) are padlocks. To a grown-up it says "locked", not "this is where you add books", which is the main reason to
press it.

## Decisions (2026-10-24, agreed with the user)

- **Decision 101: the grown-ups button is a book with a plus, everywhere.** A new drawn `IconKind.AddBook`
  (a closed book with a "+" badge, same stroke style as the other `IconView` paths) replaces the lock in
  the gallery header (24px) and on the reading page (40x40 button, 22px icon). The gallery control is renamed from `LockButton`/`OnLockTapped` to
  `AddBookButton`/`OnAddBookTapped`. Its accessible name is "Add a book (grown-ups)" in all nine
  languages.
- **Same safety check, same destination.** The tap still goes through the gate (hold, then the sum) and
  then into grown-up mode as today, with the Add tile first and Edit/Cast/Read/Delete on each book.
  Nothing moves.
- **The reading page's lock changes too** (changed to match the mock, 2026-10-24: one grown-ups button
  everywhere). It keeps what it opens (grown-up options for the open story, behind the gate) and gets the
  same icon and accessible name.
- **Out of scope:** the small lock glyph on `BackupPage`, which stays as it is.

#### Design (Dino Reader Batch 17-18 mock, screen 27, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 17-18.dc.html`, screen 27 (lines 27-42; the same button is drawn on screens 28, 30, 34, 35). One frame at 360x760, real UI except the dashed outline, which the note says is for this note only and is not shipped.

- **Gallery header button (27).** Control 44x44, radius 14, fill `rgba(43,37,64,.07)`, icon colour `#8a8399`, right end of the header row (app icon 40x40 radius 11, title "My stories" 22/600). Same quiet look as the lock it replaces.
- **`IconKind.AddBook` (24px in the gallery).** 24x24 viewBox, `fill="none"`, stroke `currentColor`, width 2, round caps and joins. Book body: `M5 18V5.5A2.5 2.5 0 017.5 3H17v12.5`, then the two lower curves `M5 18a2.5 2.5 0 012.5-2.5H12` and `M5 18a2.5 2.5 0 002.5 2.5H12`. Badge: circle at (18,18) r 4.6 filled and stroked `#5B57E8`, with a white plus `M18 15.9v4.2M15.9 18h4.2` (stroke 2). The badge colour is the brand colour and does not follow the icon colour.
- **Behaviour (note).** Tap still opens the gate, then grown-up mode with **Add a story** first. Screen reader name: "Add a book (grown-ups)".
- **Reading page (28, 30, 34, 35).** The same icon is used there: button 40x40, radius 12, same fill, icon 22px (today `ReadingPage.xaml:31`, size 18).

Changed to match the mock (2026-10-24): the reading page's lock becomes the add-book icon (it was out of scope), and the gallery icon is 24px (`GalleryPage.xaml:113` draws the lock at 20).

New strings (all nine locales): "Add a book (grown-ups)" (already in the slice).

## Work

- [ ] 1. **Smallest end-to-end:** the `AddBook` icon path in `IconView`, swapped into the gallery header
  and renamed; the gate and grown-up mode are unchanged.
- [ ] 1b. The reading page's `LockIcon` becomes `AddBook` (40x40 button, icon 22) with the same accessible name.
- [ ] 2. The accessible name and tooltip strings in every language's `.resx`.
- [ ] 3. Rule in `src/DinoReader.App/CLAUDE.md` (written at planning; check it still matches).

## Verification

- Emulator (device-test): the reading page shows the same icon in its header. The gallery header shows the book-with-plus icon in light and dark themes, at
  44x44 with the same fill as before. Tapping it opens the gate; after the gate, grown-up mode shows the
  Add tile first. TalkBack reads "Add a book (grown-ups)".
- Windows head: the icon renders and the gate opens.
