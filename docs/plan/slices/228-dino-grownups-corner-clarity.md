### Slice 228 — Dino: make the kids' shelf and the grown-ups' corner obvious

*Planned 2026-11-01.* From the user's user test on 2026-11-01 (findings 1, 2 and 7). **Needs design by Claude Code** (an HTML mock, approved by the user, before any code).

**Touches:** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/GalleryText.cs`, `src/DinoReader.App/GrownUpMode.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.Core/BookSheetPlan.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/IntroWizardPage.Art.cs`, `src/DinoReader.App/IntroState.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/BookSheetPlanTests.cs`, `docs/design/dino-reader/**`, `docs/plan/slices/228-dino-grownups-corner-clarity.md`

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

#### Work

- [ ] **Design (Claude Code):** HTML mock in `docs/design/dino-reader/` (new `Dino Reader Batch 40 user test.dc.html` or similar): kids' shelf, grown-ups' corner header and sticky button, book sheet with Read now, the intro "two places" card, the Grown-ups pill and its icon, light and dark, portrait (landscape comes in Slice 232). Publish it as a private page for the user; record their answer here.
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
