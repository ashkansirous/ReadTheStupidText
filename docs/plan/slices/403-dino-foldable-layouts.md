# Slice 403 — Dino: foldable layouts (iPhone Duo, Galaxy Z Fold8, Galaxy Z Fold8 Ultra): phone layouts on the outer screen, rails and an open book on the inner screen
**Product:** Dino

*Planned 2026-11-17 from three mocks drawn 2026-11-16: `docs/design/dino-reader/Dino Reader 2026-11-16 iPhone Duo.dc.html`, `... Galaxy Z Fold8.dc.html` and `... Galaxy Z Fold8 Ultra.dc.html`. Scope approved by the user 2026-11-17.* *Items 1, 6, 11, 12 built (PR #1256, night 2026-11-17-2105 wave 4); items 2-5, 7-10, 13 are open (they need the platform fold adapters, a fold-capable emulator and the Duo simulator).*

**Touches:** `src/Dino/DinoReader.Core/FoldLayout.cs` (new, pure), `src/Dino/DinoReader.Core/LandscapeLayout.cs`, `OnboardingLayout.cs`, `WindowLayout.cs`, `GalleryLayout.cs`, `ReadingLayoutRules.cs`, `TabletCardLayout.cs`, `TabletPageLayout.cs`, `CornerLayout.cs`, `SettingsLayout.cs` (all under `src/Dino/DinoReader.Core/`), `src/Dino/DinoReader.App/ReadingPage.xaml*`, `ReadingSurface.cs`, `ReadingSession.cs` (page kept across fold and unfold), `RecordPage.cs`, `BackCoverPages.cs`, `GatePage.xaml*`, `GalleryPage.xaml*`, `StorePage.xaml*`, `SettingsPage.cs`, `CornerShell.cs`, `OnboardingShell.cs`, `OnboardingSteps.Languages.cs`, `OnboardingListenPage.cs` (all under `src/Dino/DinoReader.App/`), `src/Dino/DinoReader.App/Platforms/Android/MainActivity.cs`, `src/Dino/DinoReader.App/Platforms/Android/Folding/**` (new), `src/Dino/DinoReader.App/Platforms/iOS/SceneDelegate.cs`, `src/Dino/DinoReader.App/Platforms/iOS/Folding/**` (new), `src/Dino/DinoReader.App/DinoReader.App.csproj`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**`, `src/Dino/DinoReader.App/CLAUDE.md`, `tests/Dino/DinoReader.Tests/**`, `.maestro/dino/` (new fold flows), `tools/emulator/**` (foldable AVD profiles), `docs/plan/slices/403-dino-foldable-layouts.md`

## Why

Samsung's Galaxy Z Fold8 and Fold8 Ultra and Apple's iPhone Duo (announced 9 Sep 2026) are foldables with a small outer screen and a large inner one. The 600 split already gives the right family of layouts (phone under 600 wide, rail and two panes from 600), but nothing in the code knows about a fold: a book read across the crease puts the words over the hinge. The mocks add one thing the Duo and the Folds earn: the reading pages become a real open book, picture on the left page, words on the right, the fold as the gutter.

## Decisions (user, 2026-11-16 and 2026-11-17; settled, never re-ask)

1. **Use the sizes drawn on the pages.** Do not check the real screen sizes, or which side the Duo's camera is on, before building. When Xcode has the Duo, its safe areas replace the guesses, with no layout change.
2. **The Fold8 Ultra is drawn** (the Fold8 page's question 1 is answered): one more device in this slice.
3. **Dino beside another app on a foldable's inner screen** (Split View on the Duo, multi-window on the Folds): the half is under 600 wide, so Dino shows its outer-screen layouts. No extra design; it is checked in the device run.
4. **The grown-ups' gate is centred on the screen**, as on every other wide screen (user, 2026-11-16).
5. **Android and iOS are the same app:** every item below has an Android task and an iOS task.

## Rules from the mocks

- Under 600 wide (outer or cover screen, and a half-width split): the phone layouts. 600 and wider (inner screen): the corner's ink rail and the shelf's cream rail, two panes, as a tablet in landscape.
- **Grids and lists run across the fold.** Only reading, recording, the back cover and onboarding use the fold as a gutter (the open-book rule below).
- **Content never goes under the island strip or a camera cutout; backgrounds do.** (The Duo's 40 pt strip on the left; Android draws edge to edge, backgrounds under the status bar and the 24 dp gesture handle.)
- **Folding or unfolding keeps the same reading page** (and the same screen elsewhere) and moves it between the two screens.
- The grown-ups' gate card is centred on the whole screen; its ring and buttons stay off the crease line.

## Design (iPhone Duo, Galaxy Z Fold8, Fold8 Ultra mocks, 2026-11-16, added 2026-11-17)

Three pages, 15 frames each (5 outer or cover, 10 inner), 45 frames in all. The mocks agree with the existing 600 rule and Slices 359, 395 and 396-399; the differences are the fold gutter, the column counts and the rails below, which the slice builds. All frames are UI. Simplified or illustrative parts, not built: the status bars and "9:41" clocks, the Duo's island strip and the Folds' camera holes (drawn only to show where content must not go), the fold line between the pages, and the estimate labels. Copy for every frame: `docs/plan/notes/2026-11-17-dino-subscription-and-foldables-copy.md` (rows "iPhone Duo, Fold8, Fold8 Ultra").

**Sizes as drawn (estimates from published diagonals and ratios; used as given, Decision 1):**

| Device | Outer / cover | Inner | Notes |
| --- | --- | --- | --- |
| iPhone Duo (frames DO1-5, DI1-10) | about 433 x 613 pt | about 867 x 613 pt | 40 pt island strip on the left of both; 20 pt home-bar inset on the tab bar; two screens of one aspect ratio (the inner is the outer doubled, the square root of 2) |
| Galaxy Z Fold8 (FO1-5, FI1-10) | about 411 x 658 dp | about 857 x 643 dp | edge to edge, 24 dp gesture inset; camera hole on the cover |
| Galaxy Z Fold8 Ultra (UO1-5, UI1-10) | about 411 x 960 dp | about 800 x 800 dp (square) | the cover is a full tall phone, exactly the Pixel 10 Pro frames P4 and K4 |

**Outer / cover screens (phone layouts, tabs along the bottom, one column, coral button at the foot):**

- **Kids' shelf (DO1, FO1, UO1).** Duo and Fold8: three columns (short screen, two rows of covers in view); Ultra: two columns (as K4). Header and "Grown-ups" pill as K2/K4 (`Gallery_Title` "My stories", `Gallery_GrownUpsPill`).
- **Corner, Books (DO2, FO2, UO2).** Duo and Fold8: four columns of smaller covers (two rows in view on 613 / 658 tall); Ultra: three columns (as P4). Line "Only grown-ups see this. Your child's shelf is one tap away." (`Gallery_CornerLine`). On Android, Back on any tab goes to the child's shelf (P4's rule). Duo tab bar has the 20 pt home-bar inset; the Android ones the 24 dp gesture inset.
- **Corner, Store (DO3, FO3, UO3).** As Subscription SB1 (Slices 396-397), with Restore purchases on the first line; the subscription card is taller than half the screen on the short screens, so the first book starts below the fold of the scroll (fine: the card is what a parent needs first).
- **Reading (DO4, FO4, UO4).** The shipped reading page. The text card is capped at three lines so the picture keeps at least half the area on the short screens; on the Ultra cover the tall screen gives the picture most of the height.
- **Onboarding, reading languages (DO5, FO5, UO5).** The phone layout; the step bar "Step 2 of 6 · Story languages", title "Which languages does your family read in?", line "English is always on. Add any others your child hears."; the "Why more than one?" box is not drawn on these frames (the full explanation shows on the inner screen).

**Inner screens (rails and two panes; grids run across the fold):**

- **Corner, Books (DI1, FI1, UI1).** Ink rail 230 wide (name, Child's shelf, three tabs, tabs 44 high; narrower than L1's because the screen is only 613 / 643 / 800 tall). Duo and Fold8: five columns, gap 14 / 12; Ultra: four columns. Content title "Books" with "Only grown-ups see this." (`Gallery_CornerLineShort`).
- **Corner, Store (DI2, FI2, UI2).** Title row with "Store", "Picked and cast for Dino" and "Restore purchases" on the right. The subscription card goes side by side as SB10: words, terms and links on the left; the two plan buttons ("Try free, then £14.99 a year" with "Save 37%", "Try free, then £1.99 a month") in a **250 wide** column on the right. Books in **one column** below (two columns leave the titles too narrow at this width, on all three devices).
- **Corner, Settings (DI3, FI3, UI3).** Duo and Fold8: **two columns of groups**: Reading (Language, Story languages, Reading speed, Animal sounds) on the left; Books and buying (Subscription "Yearly · renews on 15 Nov 2027", Restore purchases, Redeem a code) and This phone (Backup, Help make Dino better, About & updates 1.131.0) on the right. Ultra: **one column** (the content area is too narrow for two). Sub-pages open in the content area with their own back arrow, as on Windows. Helper lines under Restore purchases and Redeem a code: the foldable frames do not draw them; they are shown as on SB9 (`Settings_RestoreLine_*`, `Settings_RedeemLine_*`; Slice 399; user decision 2026-11-17, the mock's SB9 wins).
- **Kids' shelf (DI4, FI4, UI4).** Cream rail with the app icon, "My stories" and the Grown-ups pill at its foot: Duo rail 220 wide, covers in four big columns; Fold8 four big columns; Ultra three big columns.
- **Grown-ups' gate (DI8, FI8, UI8).** The card from G1 ("For grown-ups", "Press and hold for 3 seconds", "Back to my child's shelf") in the middle of the screen over the shelf, over the fold: the fold is flat when open, and the ring and buttons stay off the crease line. The sum step uses the same card with the field and the 3 x 2 keys.

**Inner screens, reading as an open book (the one new layout):** picture fills the left page; the words sit large on the right page with the controls under them; nothing is read across the fold.

- **Reading (DI5, FI5, UI5).** Left page: the picture whole (page 13 in the frames), no text card over it. Right page: back, title ("The Three Little Pigs"), language and view switch at the top; the page's words in **Andika 24** with the shipped highlight, centred in the page; the three round controls at the foot. Ultra: the words get the whole tall right page, controls at its foot. **The view switch keeps working:** picture only fills both pages; text only puts the words on both pages in two columns.
- **Recording a line (DI6, FI6, UI6).** Picture left. Right: the title with "Line 4 of 12", "Done", the line card "Pim says: "Don't worry,"" (with its red ring while recording, as frame 181), the timer, and Skip, Record, Next under it. **Slice 395's rule holds: the card shows only the character's quoted words** (a run of one speaker's quote parts, never narration); this slice changes the placement only.
- **Back cover (DI7, FI7, UI7).** The drawn back cover fills the **right** page, like the real back of a book, with the credits card in its middle (Slice 359's rule, kept off the fold): "Original story by Aesop", "Retold for young readers by Ashkan Sirous", "Illustration: Ashkan Sirous", "November 2026" (book data and the date from the real book). The **left** page says "The end" (new `Reading_TheEnd`) with the title ("The Lion and the Mouse"), and holds "Read again" (`Reading_ReadAgain`) and the dimmed arrows.
- **Onboarding, reading languages (DI9, FI9, UI9).** The 600 rule (left pane = cream wash with the heading, never a button; right pane = everything you tap) lands on the two pages. Left page: step bar "Step 2 of 6 · Story languages", heading, and the full text "English is always on. Many children hear one language at home and another at school or with grandparents. Every book is read in each language you pick, and your child can switch on any page. Pick up to three." Right page: the language list (English "App language · always on", فارسی, Deutsch, Español, Français) and Continue. The mock omits "Add any others your child hears." on the wide frame (the Onboarding page's WN2 omits it too), so the wide text is built without it: see Work item 8.
- **Onboarding, listen (DI10, FI10, UI10).** Left page: step bar "Step 4 of 6 · Listen", heading "Listen to Dino read" and page 13's picture with the speaker chip ("Pop"). Right page: the language bar (English, فارسی), the hint "Every character has its own voice, and Dino reads with feeling.", the words (Pop's line highlighted), pause, and Continue (dim until the second language plays, as N4a).

New strings that need both locales (all 15 languages, `NoHardCodedUiTextTests`): `Reading_TheEnd` "The end"; `Onboarding_LanguagesBodyShort` "{0} is always on." (the wide onboarding text, followed by the built `Onboarding_WhyMoreBody`). Everything else is a built key listed in the copy note.

## Work

Smallest end-to-end thing first: item 1 (the pure fold rule) and item 2 (reading as an open book on the Fold8 inner screen on Android and on the Duo on iOS).

- [x] 1. Pure `FoldLayout` (Core): from the window size and an optional fold (vertical gutter x and width, or none), return the layout class (phone under 600 wide, wide from 600) and, when a fold lies inside the window, the left and right page rectangles; with no fold (a plain tablet, Windows, a split-screen half) the rules do not change. Unit tests at every size above (433 x 613, 867 x 613, 411 x 658, 857 x 643, 411 x 960, 800 x 800, and 428-wide half-screens) and for the column counts below. *Built: the rules are Core-only (`FoldLayout`), wired into the app by items 2-10; nothing in the app calls them yet. Every fold overload goes through `FoldLayout.EffectiveScreen`, which applies the 600 split first (Decision 3), tested at a 428-wide half.*
- [ ] 2. Reading as an open book. Android: Jetpack WindowManager fold bounds (`FoldingFeature`) through a small adapter in `Platforms/Android/Folding/`; **query context7 for WindowManager before writing it** (root `CLAUDE.md`). iOS: the Duo's fold geometry through `Platforms/iOS/Folding/` from Apple's own API once Xcode documents it (Apple has published none; checked 2026-11-16); until then the adapter places the gutter at the middle of an inner-screen window, which every drawn inner screen shows (two equal pages). The reading page puts the picture on the left page and the words (Andika 24, shipped highlight) with back, title, language, view switch and the three controls on the right page; picture only spans both pages; text only runs in two columns.
- [ ] 3. Folding and unfolding keeps the same page: `MainActivity` handles the size and layout configuration changes without recreating the reading session; the iOS scene does the same on a size change. Reading, recording and onboarding resume on the same page / line / step.
- [ ] 4. Recording on the open book (DI6, FI6, UI6): picture left; line card, timer and Skip / Record / Next on the right; the card text is Slice 395's pieces. Both platforms.
- [ ] 5. Back cover on the open book (DI7, FI7, UI7): drawn cover and credits right, "The end", title, "Read again" and the arrows left. Both platforms. New `Reading_TheEnd`.
- [x] 6. Column counts and rails at the drawn sizes, in Core with tests (`LandscapeLayout`, `GalleryLayout`, `CornerLayout`, `SettingsLayout`): outer / cover shelf 3 (Ultra 2); outer / cover corner Books 4 (Ultra 3); inner corner Books 5 (Ultra 4), rail 230, tabs 44; inner shelf 4 (Ultra 3), rail 220; inner Settings two columns (Duo, Fold8) and one (Ultra). Where an existing formula gives other counts at these sizes, the mock's counts win. Both platforms. *Built: the rules are Core-only with tests and are wired by items 2-10 (GalleryPage, StorePage and SettingsPage still use the old rules). `CornerLayout.SizesOf(shape, width, screen)` overrides only the rail width, tab height and gap; the other rail metrics (name size, tab label size and padding, content edges) stay the tablet-landscape family's and are compared with frames DI1, FI1, UI1 by items 7 and 13.*
- [ ] 7. Inner Store: the subscription card side by side with a 250 wide plan column, books in one column; Restore purchases on the title row (Slices 396-397 build the card and Restore; this item is the layout at fold sizes). Inner Settings with the Slice 399 group and its helper lines. Both platforms.
- [ ] 8. Inner onboarding (DI9, DI10): left page cream pane with the full text, right page the choices (`OnboardingLayout` split from 600); the wide languages text is `Onboarding_LanguagesBodyShort` + `Onboarding_WhyMoreBody` (built text keeps "Add any others your child hears." in front: the mock supersedes it for the wide layout). Outer onboarding keeps the phone line; confirm `OnboardingLayout.Compact` (under 700 tall) already drops the explanation box at 613 / 658 tall.
- [ ] 9. Grown-ups' gate centred on the whole screen at the inner sizes; ring and buttons off the crease line (both steps: hold, sum).
- [ ] 10. Safe areas: content never under the Duo's 40 pt island strip or a Fold camera cutout; backgrounds under them; the 20 pt / 24 dp bottom insets. Replace the guesses with Xcode's Duo safe areas when the Duo exists (no layout change).
- [x] 11. Strings: `Reading_TheEnd`, `Onboarding_LanguagesBodyShort` in `AppResources.resx` and every `AppResources.<lang>.resx` (15 languages).
- [x] 12. `src/Dino/DinoReader.App/CLAUDE.md`: one rule line for the fold rule (Core `FoldLayout`; the 600 split; the gutter pages) and `tools/emulator/**` foldable AVD profiles at the pages' sizes. *Built: the profiles are definitions only in `tools/emulator/fold-profiles.json`; `new-avd.ps1` does not read it, item 13's script will.*
- [ ] 13. Maestro flows (Android foldable emulator profiles at the pages' sizes): the open book on the Fold8 inner screen and the Ultra inner screen, recording, back cover, gate, Settings columns, fold and unfold keep the page; and the iOS flow on the Duo simulator when it exists.

## Verification

- Core unit tests for `FoldLayout` and the changed layout rules at every drawn size, with and without a fold.
- Android foldable emulator profiles at the pages' sizes (Fold8 cover and inner, Ultra cover and inner): the Maestro flows in item 13, including fold and unfold on the same reading page, and Dino beside another app on the inner screen (the half shows the cover layouts; Decision 3).
- iOS Duo simulator when Xcode has it: the same flows. Until then the Duo's rows go to *Not yet verified*.
- Android phone and tablet layouts unchanged (no fold bounds present): the existing landscape and tablet Maestro flows still pass.

## Changed while building

- Core `FoldLayout` holds `FoldScreen`, `FoldGutter`, `BookPages`, `ClassOf` (600 split), `PagesOf` (guards NaN and negative values) and `EffectiveScreen` (an inner window under 600 wide uses the outer result, Decision 3). Fold overloads were added to `LandscapeLayout`, `CornerLayout` and `SettingsLayout`; `FoldScreen.None` keeps today's layouts (pinned tests). Nothing in the app calls them yet; items 2-10 wire them.
- Checks: DinoReader.Tests 4790 passed, 2 skipped; Shared.Tests 1736 passed; Android build ok. No device run, since no app code path changed.
- `tools/emulator/fold-profiles.json` is definitions only; item 13's script reads it.
- Review #4 (high): the rail metrics beyond width, tab height and gap (name size, tab label size and padding, content edges) stay the tablet-landscape family's; items 7 and 13 compare them with frames DI1, FI1, UI1.

## Not yet verified

- The iPhone Duo on a simulator or device (no Duo in Xcode yet; Apple has published no pixel sizes, points or safe areas, 2026-11-16). The Fold8 and Ultra on a real device.
- The 14 non-English translations of `Reading_TheEnd` and `Onboarding_LanguagesBodyShort` are machine-written, not checked by a native reader.

## Out of scope

- Windows layouts (unchanged), tablets without a fold, and Dino beside another app beyond the device check.

#### From review

*From review:* high pass (8 of 8 read): the first five findings applied, the last three left as listed below. Medium pass (9 of 9 read): the second and ninth applied, the rest left as listed below. Check pass (8 of 8 read): nothing to reopen; all eight are follow-ups, listed after the skips.

- Skipped #6 (`reviews/pr-1256-high.md`): "SettingsColumns falls back to OneStoreColumn, a constant of the Store card grid, for the Settings groups" - a naming choice, no behaviour changes.
- Skipped #7 (`reviews/pr-1256-high.md`): "Two identical (inner, ultra) switches; constants InnerStorePlanColumn and WideFromWidth are unused outside tests/own ClassOf" - the switches read the same table, no behaviour; InnerStorePlanColumn waits for item 7.
- Skipped #8 (`reviews/pr-1256-high.md`): "TheoryData of FoldScreen record structs is not serializable in xUnit v3, so a failing row is reported as one opaque case" - only failure display names, not results.
- Skipped #1 (`reviews/pr-1256-medium.md`): "FoldScreen.IsInner is caller-supplied; EffectiveScreen only demotes inner to outer, never promotes" - inner/outer comes from the platform adapter (item 2), which knows the screen; Core cannot infer it.
- Skipped #3 (`reviews/pr-1256-medium.md`): "Fold ChromeOf overload ignores windows and height on the inner screen and calls PhoneWidthOf(width) for width >= 600" - inner screens are never the Windows head; the Phone shape on an inner screen is item 13's flow check.
- Skipped #4 (`reviews/pr-1256-medium.md`): "SettingsColumns falls back to the store's OneStoreColumn constant; the two StoreColumns overloads differ in meaning" - naming only, no behaviour.
- Skipped #5 (`reviews/pr-1256-medium.md`): "Fold ShelfLayout overload overrides only Columns and does" - sideways rows are unchanged after high #1's fix and covered by today's pinned tests.
- Skipped #6 (`reviews/pr-1256-medium.md`): "Onboarding_LanguagesBodyShort and Reading_TheEnd added to AppResources and 15" - the strings are the ask of the strings item; the book, back cover and onboarding items use them.
- Skipped #7 (`reviews/pr-1256-medium.md`): "Nothing in the App" - repeat of the high pass's wiring finding, already noted in the built-item lines.
- Skipped #8 (`reviews/pr-1256-medium.md`): "Read by no script; dp and px hand-copied, no drift check" - repeat of high #5; item 13's script reads it.

Follow-ups from the check pass (`reviews/pr-1256-check-medium.md`, all low, for the wiring items):

- Follow-up 1 (`src/Dino/DinoReader.Core/CornerLayout.cs:124`): fold `BookColumns` returns the outer count (4/3) for any outer window, ignoring shape and width; a 658x411 outer rail window may squeeze tiles.
- Follow-up 2 (`src/Dino/DinoReader.Core/LandscapeLayout.cs:89`): fold `ShelfLayout` forces 3/2 columns on any non-sideways outer shape, losing `TabletPortraitShelfColumns` at width 600+.
- Follow-up 3 (`src/Dino/DinoReader.Core/CornerLayout.cs:119`): `SizesOf(shape, width, screen)` applies inner rail metrics to any shape (e.g. Bar) and can disagree with the fold `ChromeOf`.
- Follow-up 4 (`src/Dino/DinoReader.Core/FoldLayout.cs:74`): `ClassOf` and `EffectiveScreen` treat a NaN width as Wide (NaN < 600 is false), so an unmeasured inner window gets inner rules; `PagesOf` already falls back for non-finite values.
- Follow-up 5 (`src/Dino/DinoReader.Core/SettingsLayout.cs:31`): `SettingsColumns` reuses the store constant `OneStoreColumn`; inner `StoreColumns` forces 1 column at any content width.
- Follow-up 6 (`src/Dino/DinoReader.Core/FoldLayout.cs:100`): `ShelfColumns` and `CornerBookColumns` duplicate one (inner, ultra) switch; `InnerStorePlanColumn` is unused until item 7.
- Follow-up 7 (`src/Dino/DinoReader.App/Resources/Strings/AppResources.cs:267`): the two new strings are unused in `src` until the pages are built.
- Follow-up 8 (`tools/emulator/fold-profiles.json:1`): read by no script; px values are hand-copied (857 dp at 420 dpi is 2249.6, the file says 2250).

#### For CLAUDE.md

- Already in `src/Dino/DinoReader.App/CLAUDE.md` on this branch (line 88, the fold rule): change fold layout rules only in Core `FoldLayout` with its tests; every fold-aware rule takes a `FoldScreen`, and `FoldScreen.None` keeps the existing answers; AVD sizes are in `tools/emulator/fold-profiles.json`. (Slice 403: docs/plan/slices/403-dino-foldable-layouts.md)
