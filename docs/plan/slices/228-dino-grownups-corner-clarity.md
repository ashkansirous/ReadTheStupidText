### Slice 228 — Dino: make the kids' shelf and the grown-ups' corner obvious

*Planned 2026-11-01.* From the user's user test on 2026-11-01 (findings 1, 2 and 7). **Needs design by Claude Code** (an HTML mock, approved by the user, before any code).

**Touches:** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/GalleryText.cs`, `src/DinoReader.App/GrownUpMode.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.Core/BookSheetPlan.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/IntroWizardPage.Art.cs`, `src/DinoReader.App/IntroState.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/BookSheetPlanTests.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/SystemTheme.cs`, `src/DinoReader.App/ReadingTheme.cs`, `docs/design/dino-reader/**`, `docs/plan/slices/228-dino-grownups-corner-clarity.md`

#### Why (what the testers did)

1. They did not realise the app has two modes. Today it is one shelf (`GalleryPage`) that `GrownUpMode` re-skins: sand background, another title, Add/Store buttons, a "⋯" on each tile (`GalleryPage.xaml.cs:322-337`). The intro wizard (`IntroWizardPage`, Slice 155) never names the two modes; "Ready for my child" at the end is the only hint.
2. The grown-ups button was unclear: a 48x48 `BookPlus` icon with no label (Slice 189). One tester suggested a person icon.
3. Back in grown-up mode they did not find the way back to the child's shelf (a small "Done", `GalleryDone`). They tapped books and nothing played: in grown-up mode a tap opens `BookSheet` (`GalleryPage.xaml.cs:412,418`), which has no "read" action. The user dislikes tooltip onboarding.

#### Decisions locked with the user (2026-11-01, RefineScope)

- **Two named places, visibly different.** Kids' shelf stays bright, big tiles, no text. Grown-up mode becomes the **"Grown-ups' corner"**: a header bar and one line under it, roughly "Only grown-ups see this. Your child's shelf is one tap away."
- **Sticky way back.** A coral bottom button in the corner, always visible: "▶ Back to my child's shelf" (replaces the small Done; while `FirstRunState.HandoverPending` it reads "Ready for my child" as today).
- **"▶ Read now" first in the book sheet.** The biggest, first row of `BookSheet`: leaves grown-up mode and opens the book in the reader.
- **One intro card, no tooltips.** A new first card in the intro wizard showing both places side by side with a picture: "Kids' shelf: just tap and listen. Grown-ups' corner: behind a lock, for adding books and voices."
- **Grown-ups button = labelled pill** "Grown-ups" with a small lock or adult icon (word beats icon for adults; the gate keeps children out). The new icon is drawn in the mock and added to `docs/design/dino-reader/icons/` first (the folder rule: copy icons, never draw new ones in code).
- Android and iOS together (shared MAUI code); the Windows head gets the same pages.

#### Design (Dino Reader Batch 43 mock, frames 97-103 and 101b, added 2026-11-01)

Eight frames in `docs/design/dino-reader/Dino Reader Batch 43 Grown-ups Corner.dc.html` (file lines 33-218; portrait phone frames, 360 x 760 dp). The mock agrees with the decisions above, with the points marked open below. Frames 97-101b are real screens; the two mini phones inside 101 are small illustrations drawn from 97 and 98.

- **Pill (97, 102).** Replaces the 48 x 48 `BookPlus` button. 48 dp tall, radius 24, padding 0 14 0 11, fill `rgba(43,37,64,.07)` (quiet, so it does not pull a child's eye), text `#3a3450`. Lock icon, gap 6, label "Grown-ups" at 15 px/600. The language button (79) sits left of it when shown. Screen reader: "Grown-ups' corner, locked". The longest label is German, "Für Erwachsene": the pill grows, and if the title falls under 120 dp the title truncates, never the pill. Around it: 40 dp app icon (radius 11), "My stories" 22 px/600, header padding 10 12 12 16. Nothing else on the kids' shelf changes.
- **New icon (102).** The mock gives the drawing: a padlock with a keyhole on a 24 grid, stroke 2.2, round caps and joins, `currentColor`: `<rect x="5" y="10.5" width="14" height="10" rx="2.5"/><path d="M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5"/><path d="M12 14.6v2"/>` (inline in the mock, file line 185). Shown at 48, 24 and 18. The mock says it is saved as `dino_reader_icons/grownups-lock.svg`, but that file is not in the repo copy, and `docs/design/dino-reader/icons/` holds only app-icon art. **Add `grownups-lock.svg` to `docs/design/dino-reader/icons/` before any code uses it** (the folder rule). A lock beat a person icon: it says why a child cannot go in.
- **Corner (98, replaces 85).** Page `#F3EFE6`. Ink header bar `#2b2540`, white text, status bar inside it: amber lock icon, title "Grown-ups' corner" 22 px/600, gear in a 44 x 44 radius-14 tile `rgba(255,255,255,.12)`. The kids' shelf never has this bar. Line under it: "Only grown-ups see this. Your child's shelf is one tap away." 13.5 px, `#5a5470`, padding 12 18 10. "Add a book" (white, ink text) and "Store" (`#5B57E8`, white) share one row, 52 dp, radius 18, 15 px/600. Tiles go to three across (radius 12, caption 12.5 px/600, a 30 dp white round "more" button at top 4, right 4). Tapping a cover (not only the dots) opens the book sheet.
- **Sticky way back (98, 99).** Pinned above the home bar, the grid scrolls under it: 56 dp, radius 28, `#FF7A59`, white 17 px/600, play icon, gap 10, "Back to my child's shelf"; container padding 12 14 24. Android Back does the same. While `FirstRunState.HandoverPending` it reads "Ready for my child" (99), same place and colour. Replaces the small Done.
- **Book sheet (100).** Bottom sheet over a dimmed corner (`rgba(43,37,64,.5)`), white, top radius 28, padding 12 18 26, grab handle 40 x 5. Header row: 44 x 58 cover, title 19 px/600, source line ("From the Store") 13 px `#8a8399`. Then **Read now** first and biggest: 60 dp, radius 30, `#FF7A59`, 18 px/600, play icon. Then the "Read with my voice" card (`#F3EFE6`, radius 16, switch 52 x 32 `#5B57E8`, "3 of 16 pages recorded. Other pages use Dino's voices."), then Record and Cast outline pills (48 dp, 2 px `#c9c0f5`, text `#5B57E8`), then "Remove from the shelf" in `#C8402A`. Read now leaves grown-up mode and opens the reader in kids' mode, so Back lands on the kids' shelf. `BookSheetPlan` puts Read now at index 0 for every book kind (store, own, translated).
- **Intro, new first card (101).** Step bar: current dot 22 x 8 `#5B57E8`, others 8 x 8 `#d9d2c4`; "1 of 6"; "Skip all" 15 px/600 `#5B57E8` at the right. Picture: two mini phones tilted -4 and +4 degrees (120 x 200, 4 px ink border, radius 18). Title "Dino has two places" 26 px/600. Two white cards (radius 16): "Kids' shelf" / "Just tap a book and listen." and (lock icon) "Grown-ups' corner" / "Behind a lock, for adding books and voices." Button "Next" (56 dp, coral). No Try it on this card.
- **Intro, read step back (101b).** A read step that Batch 32 dropped returns as step 2: "Read a story together" / "Tap a book, then play. Dino reads it aloud and lights up each sentence." with "Try it" (opens The Tortoise and the Hare in the kids' reader, already playing; no Continue there, Back or the last page returns and moves on) and "Skip". Counters read "of 6".
- **Dark (103): out of scope** (user, 2026-11-01: "keep the light for now", "Dark is out of scope for now"). The theme pin `UserAppTheme = Light` stays; frame 103 is not built.
- **New strings (both locales, 13 languages).** "Grown-ups", "Grown-ups' corner", the corner line, "Back to my child's shelf", "Read now", "Dino has two places", "Kids' shelf", "Just tap a book and listen.", "Behind a lock, for adding books and voices.", "Read a story together", the read-step line, "Try it", the "N of 6" counter. ("Ready for my child" exists.)

**Decided: light only** (user, 2026-11-01). Keep `App.xaml.cs:30` as it is; dark is out of scope.

**Decided: intro steps map forward by two** (101 and 101b), and the read step returns to the wizard's list.

**Decided: the book sheet drops its own Done** (tap outside or swipe down closes it; `BookSheet.cs:378` builds one today).

**Decided: one string, "Add a book",** everywhere, including the landscape rail (Batch 44, 111).

Settled by the mock: the file is Batch 43, not "Batch 40"; the way-back button stays coral (indigo is used for Done in Slice 229).

#### Work

- [x] **Design (Claude Code):** mock done (Batch 43, 2026-11-01: `docs/design/dino-reader/Dino Reader Batch 43 Grown-ups Corner.dc.html`, frames 97-102 and 101b; 103 dark not built). User answers recorded above (light only; never mirror, Slice 96).
- [ ] Grown-ups pill with label and the new icon (copied from `icons/`).
- [ ] Grown-ups' corner header + one-line explanation; sticky coral "Back to my child's shelf" button.
- [ ] `BookSheetPlan`: "Read now" row first; tapping it leaves grown-up mode and opens the reader. Unit test the row order.
- [ ] Intro wizard: new first card (two places); step counter and `IntroState` step numbers moved so a resumed wizard lands on the right step (old saved steps map forward by one).
- [ ] Strings in all 13 languages.
- [ ] Android and iOS: both built and checked in the same pass.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (book sheet order, intro step mapping).
- Scripted emulator flow: open gate, see corner header and sticky button, tap a book, tap Read now, reader opens in kids' mode. iOS: simulator or TestFlight build, same flow.
- The user re-runs the user test: a new tester can say what the two modes are and gets from the corner to the shelf without help.
