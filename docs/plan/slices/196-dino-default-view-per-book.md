### Slice 196 — Dino: each book opens in the view that fits it

*Built (wave 2026-10-30), PR #575.*

**Touches:** `src/DinoReader.Core/Story.cs`, `src/DinoReader.App/PictureTextPages.cs`, `tools/DinoReader.BookBuilder/StarterBuilder.cs`, `src/DinoReader.Core/StoryJson.cs`, `tools/DinoReader.BookBuilder/Books.cs`, `tools/DinoReader.BookBuilder/StoreBuilder.cs`, `tools/DinoReader.BookBuilder/BookHash.cs`, `content/dino/good-enough/book.json`, the committed Good Enough starter package, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`, `.maestro/dino/default-view-per-book.yaml` (new), `src/DinoReader.App/ModeSwitch.cs`

**Why.** Good Enough's pages 1-7 have the story words drawn into the pictures. That is on purpose: it shows parents they can use their own pictures with words in them. **Never redraw them.** But the book opens in Picture + text today, so kids see the words twice (in the picture and in the panel).

**Rule (the user, 2026-10-29).** When the reader has not picked a view for a book:

- a book whose pictures carry its words opens in **Picture only**;
- any other book with pictures opens in **Picture + text**;
- a book with no pictures opens in **Text only** (unchanged; the switch stays hidden).

A view the reader picked with the switch is kept, as today (`ReadingPage.xaml.cs` saves only on a tap, and `StarterUpdate` keeps it across updates). Existing installs of Good Enough have no saved view unless someone tapped, so they get Picture only after the update.

**Design.** The flag is book-level: an optional `textInPictures: true` in `book.json`, carried into the `.dino` package's `story.json` and onto `Story`. `Story.Mode` (`src/DinoReader.Core/Story.cs:165`) becomes: `HasPictures ? PreferredMode ?? (TextInPictures ? PictureOnly : PictureAndText) : Text`. A page with no picture in Picture only already shows its text card (`PictureTextPages.cs:407`). Optional field, so `schemaVersion` stays 1 (Core CLAUDE.md); older builds ignore it and open in Picture + text. The flag travels inside the package, so store books need no `meta.json` or Worker change. Photo, scan and PDF imports keep defaulting to Picture only (`StoryDraft.MarkPicturesAsPages`). All shared code, so Android, iOS and the Windows head change together.

- [x] `Story.TextInPictures` (default false) and the `story.json` field; `Story.Mode` uses it. Unit tests: flag on with pictures -> PictureOnly; flag off -> PictureAndText; no pictures -> Text; a saved choice wins in all three.
- [x] BookBuilder reads `textInPictures` from `book.json` (optional key), writes it into starter and store packages, and includes it in `BookHash` so `check` catches a change.
- [x] `content/dino/good-enough/book.json` gets `"textInPictures": true`; `stamp` raises its version; rebuild the starter.
- [x] Core CLAUDE.md: one line on the flag and the default rule.

- [x] In a book with `TextInPictures`, Picture only shows the text card for a page whose picture has no printed words (`StoryPage.ShowsTextCardInPictureOnly`, used by `PictureTextPages`): Good Enough pages 8-10 keep their words readable. Pages with printed words stay picture only; every other book's Picture only is unchanged.

**Known behaviour.** An install that skipped or was counted as edited by `StarterUpdate` (`BookUpdateSkip`) is not refreshed, so it keeps the old Picture + text default until it is; `Refresh` takes the flag from the bundled copy.

**Known behaviour (translation).** A translated Good Enough still opens Picture only, with the original-language words in its pictures (the translation shows only in the text card on pages without printed words).

No other book has words drawn in its pictures (Elves and Shoemaker page 15's shop sign is scenery, not the story text, so it stays off).

**Verification.**

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (the four mode cases), and `dotnet run --project tools/DinoReader.BookBuilder -- check`.
- Android emulator (scripted flow): a fresh install opens Good Enough in Picture only and another starter in Picture + text; switch Good Enough to Picture + text, reopen, it stays.
- iOS: same shared code; compile in CI, the open checked on TestFlight (*Not yet verified* if no run).

**Changed while building:**

- `book.json` `textInPictures` (Good Enough only; v5 to v6, starter rebuilt) -> `Story.TextInPictures` (`story.json`, written only when true) -> `Story.Mode` default Picture only; a saved choice wins. Set in `StarterBuilder.WithTranslations` for every build path; `BookHash` covers it.
- In a TextInPictures book, a picture page with no printed words (lines empty; translated pages keep this via `StoryPage.PicturePrintsWords`) shows its text card in Picture only. Every other book's Picture only is unchanged.
- `ModeSwitch`: the active segment's description is "<label>, Selected" (`Reading_Mode_Selected`, all 14 languages) so TalkBack and flows can see the state.

**Known behaviour (older builds).** Older app builds ignore the flag. Old packages read the flag as false. Installed Good Enough flips to Picture only on update (intended).

**From review:** high pass: 4 of 6 applied (+6 noted). Medium pass: 4 of 7 applied; skipped: OCR-empty lines, intended flip of installed Good Enough, old packages read false.

**Checks:** 1774 Dino tests, BookBuilder `check`, Dino Android + iOS compile. S21: flow `default-view-per-book.yaml` PASS on all steps; Good Enough page 9 text card in Picture only PASS; Three Little Pigs Picture only, no card PASS.

#### For CLAUDE.md

- `src/DinoReader.App/CLAUDE.md`: segmented controls (`ModeSwitch.cs`) expose the selected state in the control's description ("<label>, Selected"), not only in a hint, because Android drops hints from content-desc. (Slice 196: docs/plan/slices/196-dino-default-view-per-book.md)

#### Not yet verified

- The flow `default-view-per-book.yaml` has not been shown failing on old main.
- Good Enough pages 8 and 10 not checked by eye.
- Sideways (landscape) layout not checked.
- Windows head not checked.
- iOS TestFlight open of Good Enough not checked.
