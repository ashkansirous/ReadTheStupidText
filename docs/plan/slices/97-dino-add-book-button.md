# Slice 97 — The grown-ups button says "add a book", not a lock

*Planned 2026-10-24*, from the user's request "the menu button for grown-ups is very bad. It should be
something indicating that you could add a book, not a lock."

**Batch:** 18 (Dino Reader: layout, add-a-book button, picture + text books)
**Depends on:** Slice 80 (grown-up mode as a gallery state), Slice 61 (gate)
**Touches:** `src/DinoReader.App/IconView.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/CLAUDE.md`

## Why

The gallery header's grown-ups button (`LockButton`, `IconView Kind="Lock"`, `GalleryPage.xaml`) is a
padlock. To a grown-up it says "locked", not "this is where you add books", which is the main reason to
press it.

## Decisions (2026-10-24, agreed with the user)

- **Decision 101: the gallery's grown-ups button is a book with a plus.** A new drawn `IconKind.AddBook`
  (a closed book with a "+" badge, same stroke style as the other `IconView` paths) replaces the lock in
  the gallery header. The control is renamed from `LockButton`/`OnLockTapped` to
  `AddBookButton`/`OnAddBookTapped`. Its accessible name is "Add a book (grown-ups)" in all nine
  languages.
- **Same safety check, same destination.** The tap still goes through the gate (hold, then the sum) and
  then into grown-up mode as today, with the Add tile first and Edit/Cast/Read/Delete on each book.
  Nothing moves.
- **Out of scope:** the reading page's own lock button (`ReadingPage`), which opens grown-up options for
  the open story rather than adding a book, and the small lock glyph on `BackupPage`. Both stay as they
  are.

## Work

- [ ] 1. **Smallest end-to-end:** the `AddBook` icon path in `IconView`, swapped into the gallery header
  and renamed; the gate and grown-up mode are unchanged.
- [ ] 2. The accessible name and tooltip strings in every language's `.resx`.
- [ ] 3. Rule in `src/DinoReader.App/CLAUDE.md` (written at planning; check it still matches).

## Verification

- Emulator (device-test): the gallery header shows the book-with-plus icon in light and dark themes, at
  44x44 with the same fill as before. Tapping it opens the gate; after the gate, grown-up mode shows the
  Add tile first. TalkBack reads "Add a book (grown-ups)".
- Windows head: the icon renders and the gate opens.
