### Slice 122 — Dino: smaller text panel over pictures, and a switch to hide it

*Built 2026-10-25, not yet verified on an emulator or phone. 943 tests green, Android build clean.*

**Touches:** `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/Resources/Strings/*`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/PictureTextBook.cs`, `tests/DinoReader.Tests/PictureTextBookTests.cs`, `tests/DinoReader.Tests/StoryJsonTests.cs`

**Why.** Reported by the user on 2026-10-25 after the illustrated Three Little Pigs (Slice 119). With the phone upright, picture mode draws the story text in a panel over the bottom of the picture. The panel is capped at 58% of the card (`PictureTextPages.PanelHeightShare`, `UprightPage`). Its margin and shadow sit outside the cap, and AspectFit letterboxing makes the card taller than the visible picture, so on the phone the panel hides about 70% of the picture. That is far too much.

**Decisions locked with the user.**
- The panel takes **at most 30%** of the visible picture with the phone upright, margin and shadow included. Longer text scrolls inside the panel, as it already does. The font stays the same.
- A grown-up setting per book, **"Show the words over the pictures"**, sits in the book's grown-up settings, next to the animal-sounds switch (`CastRows`/`CastPage`). When it is off, picture mode shows only the picture, in both orientations. Reading aloud, highlighting and turning pages are unchanged.
- It is **off by default for books whose pictures already contain the words**: books imported from camera photos, gallery photos of pages or PDF pages: the Pages step stores `WordsOverPictures = false` (`StoryDraft.MarkPicturesAsPages`) when it adds them, unless the grown-up already chose. It is **on by default** for every other book, including the store and starter books, whose art has no text in it (Decision 127).
- Scans with placed lines already draw boxes on the page and no panel (`ScanPages`). The setting does not change that.

**Work.**
- [x] Pure Core rule, tested: `PictureTextBook.PanelHeightShare = 0.30` of the *visible picture* height (the AspectFit picture rect, not the card), with the margin inside the share. Add a pure helper that takes the card size and picture aspect and returns the panel's maximum height. Unit tests cover a tall card with letterbox bands, a wide picture and a short text.
- [x] `UprightPage` uses the helper. Remove the 58% constant and update its doc comment.
- [x] `Story.WordsOverPictures` (`bool?`; null means the default from the rule above), stored in `story.json` only when it was set (the `AnimalSoundsOn` pattern, no schema bump). Copy it through `StoryImport` and `StoryActions` `with`-copies.
- [x] `PictureTextBook.ShowsWordsOverPictures(Story)` gives the effective value (the stored value, else true). Tests cover each case.
- [x] `PictureTextPages`: when it is false, draw the picture card only. Upright, the picture fills the card. Sideways, the picture takes the full width and the transport row stays.
- [x] The grown-up switch row, with strings in every app language. The switch applies the next time the book opens (the reading page closes when the grown-up opens the cast screen).
- [ ] Emulator check, upright and sideways: on the pigs book the panel covers at most 30% of the picture. With the switch off, only the picture shows. A book imported from photos or a PDF has the switch off by default.

**Verification.** `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green, then the emulator check above via `device-test`.

*Changed while building:*
- The cap is `PictureTextBook.PanelMaxHeight` with `PanelHeightShare = 0.30`, of the visible AspectFit picture, margin included. An unknown aspect falls back to 30% of the card. The cap is re-applied on `SizeChanged`.
- No live re-render: the switch applies the next time the book opens, because the reading page closes when the cast screen opens.
- The default is set at import, not derived: camera/gallery page photos and PDF import set `WordsOverPictures = false` (`StoryDraft.MarkPicturesAsPages`, `PagesStep`). Picking art for typed pages leaves it on. The effective value is `?? true`.
- The switch row shows only for picture+text books. When off, pages with a picture show the picture only (both orientations); pages without a picture keep their text card.

*From review:* two passes (high 10 findings, medium 10). Fixed: blank text-only pages, a scan-only switch, a mixed-book default, aspect 0, a 0 cap before layout, a JSON omission test, a package round-trip test. Skipped: live re-render, a size cache.

**Not in scope.** Changing the font size, or moving the text above the picture.

**Art follow-on.** Story pictures keep their faces and action in the **top 70%**, with the bottom 30% calm. The `create-dino-story` skill is updated in Slice 123.

#### Design (Dino Reader Batch 25 mock, section 56-58, added 2026-10-25)

3 frames (`docs/design/dino-reader/Dino Reader Batch 25.dc.html`, 360x760 phone frames). The mock agrees with the plan: 30% of the visible picture, margin inside, a per-book switch under Animal sounds. It supersedes frame 34 (58% cap).

- **Frame 56, upright, panel at most 30%.** Picture area: 14px side and 6px top padding, `aspect-ratio 3/4`, corner 18px, `object-fit: contain`. Panel: left, right and bottom 8px inside the picture, `max-height: calc(30% - 8px)`, `rgba(255,255,255,.92)`, corner 16px, padding 10px 14px, text `Andika` 21px, line-height 1.5, ink `#2b2540`, shadow `0 4px 12px rgba(43,37,64,.16)`. Text is bottom-aligned in the panel. A 22px fade runs along the panel's top edge (`rgba(255,255,255,.95)` to transparent, top corners rounded), the hint that long text scrolls. Read text `#8a8399`, spoken sentence on `#FFE27A` (radius 8, 4px spread). Page dots 9px (active 26px, `#5B57E8`), transport row: skip 72px, play/pause 104px `#FF7A59`, skip 72px.
- **Frame 57, words off.** The same layout with the picture only: no panel, no highlight. The transport shows a play triangle (the book is paused). Text says: sideways also picture only; pages with no picture keep their text card; scans with placed lines keep their boxes, the switch does not touch them.
- **Frame 58, Who's speaking, photo book.** Subtitle "Bear's Big Day · English · from photos". Animal sounds and the new switch share one white card (radius 18) split by a 1px `#efe9dd` divider, each row min 64px, padding 12px 14px, title 15px/600, sub-line 12.5px `#5a5470`, switch 52x32 (on `#5B57E8`, off `#d9d2c4`, knob 26px). Title: "Show the words over the pictures". Sub-line when off by the photo default: "Off because these pictures already have the words in them". Sub-line when on (store and starter books, frame 52): "The words show in a panel at the bottom". "Merge two characters" outline button (48px) under the card.
- **Settled by the mock.** The switch shows only on picture + text books. Photo, gallery and PDF books default off; every other book on. The change shows the next time the book opens. The mock's copy is marked as a draft.
- **New strings (every app language):** `Cast_WordsOverPictures_Hint` text for on ("The words show in a panel at the bottom") and a second key for the off-by-default case ("Off because these pictures already have the words in them").

#### Gaps against the mock

Checked against the built code on `main` (PR #335), 2026-10-25.

- **Open point: switch sub-lines.** The code has one fixed hint, "The story text sits on the picture while it is read" (`Resources/Strings/AppResources.resx:291`, used at `CastRows.cs:77`). The mock shows two: "The words show in a panel at the bottom" when on, "Off because these pictures already have the words in them" when off. **Default:** switch the hint on the value (on gets the first, off the second) and add the key in every locale.
- **Open point: one card or two.** The mock puts both switches in one card with a divider. The code builds two separate cards (`CastRows.cs:81` `SwitchCard`, added in turn at `CastPage.cs:240-243`). **Default:** keep two cards unless the grown-up wants the mock's grouping; the order (under Animal sounds) already matches.
- **Top fade missing.** No 22px white-to-clear gradient on the panel's top edge (`PictureTextPages.cs:198-220`, `TextPanel`). **Default:** add it for the upright panel only.
- **Text not bottom-aligned.** The mock bottom-aligns a short text in the panel; the code puts the label in a top-aligned `ScrollView` (`PictureTextPages.cs:215`).
- **Panel measurements differ.** Margin 10 (`:23`) vs 8; padding 16x14 (`:137`) vs 14x10; line-height 1.55 (`:19`) vs 1.5; corner 18 (`:21`) vs 16; shadow radius 18, offset 6, opacity .18 (`:142`) vs `0 4 12 .16`. The cap counts the margin inside the 30% as the mock does (`PictureTextBook.cs:29-34`), so a smaller margin gives a 2px taller panel.
- **Matching the mock:** 30% of the visible picture (`PictureTextBook.PanelMaxHeight`); switch under Animal sounds and only for picture + text books (`CastPage.cs:241`); photo/PDF default off (`PictureTextBook.cs:43`, `StoryDraft.MarkPicturesAsPages`); picture only when off, no label so no highlight (`PictureTextPages.cs:187`); pages without a picture keep their text card; scans with placed lines go through `ScanPages`, unaffected.

#### For CLAUDE.md

- The words panel over a picture is capped at 30% of the visible AspectFit picture, margin included (`PictureTextBook.PanelMaxHeight` in `src/DinoReader.Core/PictureTextBook.cs`); never size it from the card. `Story.WordsOverPictures` is `bool?`, omitted from `story.json` when null, effective value `?? true`; photo/PDF page imports set it false (`StoryDraft.MarkPicturesAsPages`). (Slice 122: docs/plan/slices/122-dino-text-panel-30-and-switch.md)

#### Not yet verified

- Emulator/phone check, upright and sideways, on the pigs book: the panel covers at most 30% of the picture.
- With the switch off only the picture shows; a new photo or PDF import has the switch off.
- A sideways full-width picture does not overlap the transport row.
