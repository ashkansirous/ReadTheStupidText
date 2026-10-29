### Slice 189 — Dino: one "For grown-ups" button on the shelf, the gear inside grown-up mode, a ⋯ sheet per book, a reading card capped at 30%

*In progress (planned 2026-10-29).* Dino Reader only (Android, iOS and the Windows head). From the user's refresh of the Batch 37 Claude
Design mock (2026-10-29, frames 79, 80, 85, 85b, 91, 94). Changes what Slices 172 and 174 built. Decision 170.

**Touches:** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/TileOverlay.xaml`, `src/DinoReader.App/TileOverlay.xaml.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/StoryTile.cs`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/IconView.cs`, `src/DinoReader.App/GrownUpMode.cs`, `src/DinoReader.App/App.xaml.cs` (back routing), `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.Core/PictureTextBook.cs` (card height), `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `tools/flows/**` and `tools/desktop-vm/checks/**` only where a scripted flow taps the old shelf gear, `docs/design/dino-reader/**`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/189-dino-grown-ups-button-and-book-sheet.md`.

#### Why

The user updated the Batch 37 mock: the shelf gear led to Settings, and grown-up mode hid under Settings > Books.
That made grown-up mode two taps deep and put the book tools under a settings list. Now one button on the kids'
shelf opens grown-up mode, Settings sits behind a gear inside grown-up mode, and each book's tools live in one
sheet behind a ⋯ on its cover. The reading card also had a fixed five lines, which covered too much picture on
short screens; the mock caps it at 30% of the picture.

#### What changes (mock `docs/design/dino-reader/Dino Reader Batch 37.dc.html`; line numbers in its README)

1. **Kids' shelf (frame 79).** The gear (`SettingsButton`) becomes the **book-and-plus** button: a book outline
   with a `+` at its lower right (the mock's 24-unit SVG, 26 px icon, in a 48 dp quiet-fill circle like the
   language button). It runs the gate (hold, then sum) and opens **grown-up mode** (85), not Settings. Screen readers
   say "For grown-ups" (new string `Gallery_GrownUpsButton`; the hint stays `Gallery_OpensGateHint`). Failing the gate
   leaves the shelf as it was. The language button stays left of it (Slice 173 rules unchanged). No gear on the shelf.
2. **Grown-up mode (frame 85).** Header: title "Grown-ups", subtitle "Add and edit books", a **gear** top right that
   opens Settings (80) with no second gate (the gate was passed), and Done. Add from your own books and Store stay.
   Done and Android back return to the kids' shelf (unchanged).
3. **Settings (frame 80).** The **Books** row and its group are removed (`BooksRow`, `EnterBooksAsync`,
   `Settings_Books`, `Settings_BooksValue` go; strings removed from every `.resx`). Opened only from the gear in
   grown-up mode. Back from Settings returns to **grown-up mode** (still on), not the kids' shelf. Settings
   sub-pages go back to Settings. So the back chain is: sub-page -> Settings -> grown-up page -> kids' shelf
   (frame 94 note). Leaving the app still resets grown-up mode (`GrownUpMode`, unchanged).
4. **The ⋯ on each cover (frame 85b).** In grown-up mode every cover gets a 36 px white dot with a soft shadow
   (`0 2px 6px rgba(43,37,64,.2)`) and a `⋯` at its top-right corner; its tap area is 48 dp. It opens the existing
   `BookSheet` (frame 43). The old overlay's Edit, Cast, Record, Export and Delete buttons leave the cover; their
   actions move into the sheet. A tap on the bare cover in grown-up mode keeps opening the same sheet (today's
   behaviour; one more way in, nothing lost). Screen readers say "Options for <title>" on the dot.
5. **The sheet (frame 85b).** Header: the book's title and where it came from: "From the Store" for store books,
   "Starter book" for starters, "Your book" for own books (new strings; drafts, the mock shows only the store case).
   Rows as today: Show original text (translated books only), Read with my voice (only once a page is recorded), the
   Record and Cast pills. New: **Edit pages** (own books only; opens the existing editor, `StoryActions.EditAsync`),
   **Export** (wherever the cover overlay offered it before; the mock doesn't show it, but dropping a working feature
   is not the mock's call, so it stays as a quiet row and is flagged to the user), and **Remove from the shelf** (red
   text, bottom; asks to confirm through the existing `StoryActions.ConfirmDeleteAsync`; for store books the confirm
   text says it can be added again for free from the Store). The button at the foot is **Done** (replaces "Back to the
   story"); tapping outside or Done closes it.
6. **The reading card (frame 91).** The Picture + text card is never taller than **30% of the picture's height,
   inset included**, and holds as many **whole** lines as fit in that (four at 21 px on the mock's phone, three in
   Farsi), never growing to show more. If the picture is shorter than the space, the card keeps its 30% of the
   picture and may run past the picture's bottom edge towards the controls, never over them. Auto-scroll keeps the
   spoken sentence in view (unchanged). Replaces the fixed "five whole lines" of Slice 174
   (`PictureTextBook.CardHeightFor`, `PictureTextPages` around line 328). Landscape (92, 93) is unchanged.
7. **Store wording** stays as Slice 172 left it ("Store").

#### Acceptance

- Kids' shelf shows the language button (2-3 story languages) and the book-and-plus button, no gear; TalkBack reads
  "For grown-ups"; passing the gate shows grown-up mode; failing it leaves the shelf.
- Grown-up mode shows the gear; the gear opens Settings without a gate; Settings has no Books row.
- Android back: sub-page -> Settings -> grown-up mode -> kids' shelf -> Stay/Close question. iOS swipe-back and the
  Windows back follow the same chain where they exist.
- Each cover in grown-up mode has the ⋯ dot (no other overlay buttons); it opens the sheet with the right header and
  rows for a store, a starter and an own book; Edit pages only for own books; Remove asks first and removes the book;
  a removed store book can be added again from the Store.
- Picture + text card height <= 30% of the picture on a small phone, a tall phone and a tablet, whole lines only.
- Unit tests for the card-height rule (`PictureTextBook`) and for the sheet's row choice per book kind if that logic
  is pure; existing tests updated.
- Android emulator and the Windows VM run the flow (shelf -> gate -> grown-up -> gear -> Settings -> back chain;
  ⋯ sheet; Remove). iOS: compile check (no Mac). Scripted flows and VM checks that tapped the old shelf gear are
  updated to the new path.
- New strings in all 14 languages (13 machine drafts, flagged for native review).

#### Not in scope

- Settings' other rows and pages (Slice 172), story languages (173), landscape and the Stay/Close question (174).
- Slice 188 (the permanent Dino Settings VM check) must use the new path: shelf -> book-and-plus -> gate ->
  grown-up mode -> gear. If 188 starts first, it follows this slice's flow.

#### Design

Mock: `docs/design/dino-reader/Dino Reader Batch 37.dc.html` (refreshed 2026-10-29), frames 79 (line 27),
80 (45), 85 (170), 85b (187), 91 (347), 94 (373). Read visual values from that copy; never call `DesignSync`.
