### Slice 196 — Dino: each book opens in the view that fits it

*Planned.*

**Touches:** `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `tools/DinoReader.BookBuilder/Books.cs`, `tools/DinoReader.BookBuilder/StoreBuilder.cs`, `tools/DinoReader.BookBuilder/BookHash.cs`, `content/dino/good-enough/book.json`, the committed Good Enough starter package, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`

**Why.** Good Enough's pages 1-7 have the story words drawn into the pictures. That is on purpose: it shows parents they can use their own pictures with words in them. **Never redraw them.** But the book opens in Picture + text today, so kids see the words twice (in the picture and in the panel).

**Rule (the user, 2026-10-29).** When the reader has not picked a view for a book:

- a book whose pictures carry its words opens in **Picture only**;
- any other book with pictures opens in **Picture + text**;
- a book with no pictures opens in **Text only** (unchanged; the switch stays hidden).

A view the reader picked with the switch is kept, as today (`ReadingPage.xaml.cs` saves only on a tap, and `StarterUpdate` keeps it across updates). Existing installs of Good Enough have no saved view unless someone tapped, so they get Picture only after the update.

**Design.** The flag is book-level: an optional `textInPictures: true` in `book.json`, carried into the `.dino` package's `story.json` and onto `Story`. `Story.Mode` (`src/DinoReader.Core/Story.cs:165`) becomes: `HasPictures ? PreferredMode ?? (TextInPictures ? PictureOnly : PictureAndText) : Text`. A page with no picture in Picture only already shows its text card (`PictureTextPages.cs:407`). Optional field, so `schemaVersion` stays 1 (Core CLAUDE.md); older builds ignore it and open in Picture + text. The flag travels inside the package, so store books need no `meta.json` or Worker change. Photo, scan and PDF imports keep defaulting to Picture only (`StoryDraft.MarkPicturesAsPages`). All shared code, so Android, iOS and the Windows head change together.

- [ ] `Story.TextInPictures` (default false) and the `story.json` field; `Story.Mode` uses it. Unit tests: flag on with pictures -> PictureOnly; flag off -> PictureAndText; no pictures -> Text; a saved choice wins in all three.
- [ ] BookBuilder reads `textInPictures` from `book.json` (optional key), writes it into starter and store packages, and includes it in `BookHash` so `check` catches a change.
- [ ] `content/dino/good-enough/book.json` gets `"textInPictures": true`; `stamp` raises its version; rebuild the starter.
- [ ] Core CLAUDE.md: one line on the flag and the default rule.

No other book has words drawn in its pictures (Elves and Shoemaker page 15's shop sign is scenery, not the story text, so it stays off).

**Verification.**

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (the four mode cases), and `dotnet run --project tools/DinoReader.BookBuilder -- check`.
- Android emulator (scripted flow): a fresh install opens Good Enough in Picture only and another starter in Picture + text; switch Good Enough to Picture + text, reopen, it stays.
- iOS: same shared code; compile in CI, the open checked on TestFlight (*Not yet verified* if no run).
