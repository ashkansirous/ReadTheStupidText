### Slice 228 — Dino: make the kids' shelf and the grown-ups' corner obvious

*Built (wave 2026-11-01), PR #673.* Planned 2026-11-01. From the user's user test on 2026-11-01 (findings 1, 2 and 7). **Design done: the user approved the Batch 43 mock (see User answers)** (corrected by the 2026-11-01 feedback loop; it said "Needs design by Claude Code").

**Touches:** `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/GalleryText.cs`, `src/DinoReader.App/GrownUpMode.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.Core/BookSheetPlan.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/IntroWizardPage.Art.cs`, `src/DinoReader.App/IntroState.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/BookSheetPlanTests.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/SystemTheme.cs`, `src/DinoReader.App/ReadingTheme.cs`, `docs/design/dino-reader/**`, `src/DinoReader.Core/UsageEvent.cs`, `worker/events.ts`, `tests/DinoReader.Tests/UsageEventTests.cs`, `src/DinoReader.App/IconView.cs`, `.maestro/dino/**`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/StoryActions.cs`, `worker/events.test.mjs`, `docs/plan/slices/228-dino-grownups-corner-clarity.md`

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

**User answers (2026-11-01):** the user approved the Batch 43 mock (frames 104-108). Build to it. iOS gets a compile check in the night run; the VoiceOver / TestFlight / iPhone / iPad hand checks go to this slice's *Not yet verified* for the user to do later.

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
- [x] Grown-ups pill with label and the new icon (copied from `icons/`).
- [x] Grown-ups' corner header + one-line explanation; sticky coral "Back to my child's shelf" button.
- [x] `BookSheetPlan`: "Read now" row first; tapping it leaves grown-up mode and opens the reader. Unit test the row order.
- [x] Intro wizard: new first card (two places); step counter and `IntroState` step numbers moved so a resumed wizard lands on the right step (old saved steps map forward by one).
- [x] Strings in all 13 languages (machine drafts).
- [x] Android and iOS: both built (iOS compile only; Android run on the emulator).

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (book sheet order, intro step mapping).
- Scripted emulator flow: open gate, see corner header and sticky button, tap a book, tap Read now, reader opens in kids' mode. iOS: simulator or TestFlight build, same flow.
- The user re-runs the user test: a new tester can say what the two modes are and gets from the corner to the shelf without help.

*Changed while building:*

- Labelled Grown-ups pill (AutomationId `GalleryGrownUps`) with the lock icon from the mock; `IconKind.Lock` was changed app-wide.
- Corner: header bar, one line, Add/Store row and three tiles across. Sticky coral `GalleryDone` reads "Back to my child's shelf", or "Ready for my child" during hand-over.
- Book sheet: "Read now" first row (`BookSheetReadNow`), close button `BookSheetClose`; the Done row and swipe-down are removed, a tap outside closes. `BookSheetPlan.Rows` is ordered with a `BookSheetRow` enum.
- Intro has 6 steps: new "Dino has two places" first card, and the Read step is back with Try it, which plays the shortest starter and returns at its end. Skipped and untouched rules apply; no auto-play when the voice isn't ready. `IntroSteps.FromLegacy` maps saved older intros; `INTRO_STEPS` in `worker/events.ts` updated.
- The demo read records no `BookOpened`/`BookFinished` (`DemoReadCounting.Counts`).
- 6+6 new strings in the 15 resx files, machine-drafted.

*Verified:* emulator flows `corner-read-now`, `grown-ups-button`, `record-page-shows-words` PASS (Dino debug 8a844b84ae24). By scripted taps: intro first card "1 of 6", the Read step plays and returns at "3 of 6", and the demo read adds no usage counts.

#### Review notes (high pass)

- Fixed: intro read leaves via RunWhenFreeAsync; grown-up mode restored when the Read step opens nothing; the intro demo read records no BookOpened/BookFinished (DemoReadCounting, unit-tested); labelled close button on the book sheet and the swipe-down removed; a separate _hasStarter flag for the Read step.
- Finding 4 (voice or pack not ready): the reading page already shows its loading overlay after 500 ms with Cancel, and Cancel or Back pops to the intro, which then moves on; no new code.
- Skipped, finding 7 (BookSheetPlan.Rows used only by tests): structure only, no user-visible drift today.
- Skipped, finding 8 (unused Gallery_GrownUps strings, shelf-mode gear colours): cleanup.
- Skipped, finding 9 (Worker must deploy before the app sends places:*/read:* intro steps): the Worker deploys on merge within minutes; the app reaches users days later through the stores.

#### Review notes (medium pass)

- Fixed: the intro read only auto-plays when the voice pack is ready (Idle or Ready); otherwise the reader opens without playing, shows its pack status, and Back still moves the intro on. The auto-leave is cancelled by any grown-up tap (Play, sentence, Previous, Next), so a later Finished never pops the page. The Maestro flow taps `BookSheetClose` instead of a fixed point.
- Not a bug, finding 2 (corner tile titles wrap to 3 lines): the tile title style has MaxLines 2 with tail truncation and a fixed two-line height, so a narrower tile truncates and never wraps to a third line.
- Skipped, finding 3 (mode restored only on appear): fix 2 of the high pass already restores the mode on failure; a kill mid-push is rare and the next launch starts in kids' mode.
- Skipped, finding 4 (Worker must deploy before the app): the Worker deploys on merge within minutes; the app reaches users days later.
- Skipped, finding 5 (brace formatting): style.
- Skipped, finding 6 (unused Gallery_GrownUps strings): cleanup.

#### Review notes (medium pass 2)

- Fixed: the book sheet Read now row closes with Dismiss (the shelf rebuilds after a switch changed in the sheet); the demo read is marked where the intro opens the reader (`MarkDemoRead`), so it counts nothing even when the voice is not ready; the intro pop is a one-shot (consumed by the first Finished, cancelled by any touch or by the page being covered, re-checked as top of stack when it runs).
- Skipped, finding 3 (brace formatting): style.
- Skipped, finding 4 (corner title probe at 3 columns): the title style is MaxLines 2 with tail truncation; a device check covers the look.
- Skipped, findings 5 and 6 (mode left before the push; hand-over cleared before a failed open): low, recorded before.
- Skipped, finding 8 (Worker before app): the Worker deploys on merge, the app ships days later.

#### Review notes (check pass 2, `reviews/s228-medium-check2.md`)

- Fixed, finding 9: the sheet's close button is 48 dp (Android's minimum touch target; the mock drew 36).
- Skipped: 1 (a one-shot spent when the page is not on top: not returning then is right, the grown-up did something); 2 (no modal opens over the reader in this flow); 3 (Dismiss refresh and `_mode.Leave` both run on the UI thread in order; no failure path shown); 4 and 5 (structure); 6 (recorded before); 7 (Idle means no download running; the reader shows its pack status and plays with what is installed); 8 (style).

#### For CLAUDE.md

In `src/DinoReader.App/CLAUDE.md`:

- Maestro flow expect-log markers must be written with `TestHookLog.Write` (RTST-TEST tag), not `Debug.WriteLine`. Flows dismiss the "Storyteller voices are ready" note by its text, never by a point tap: the sticky `GalleryDone` sits at the bottom. (Slice 228: docs/plan/slices/228-dino-grownups-corner-clarity.md)

#### Not yet verified

- Windows head and VM: shared UI, not run.
- iOS: compile only.
- Corner tiles and title truncation on a small phone and on a tablet.
- Read step with no starter book.
- The translated strings are machine drafts.
- The intro read's return being cancelled by Play/pause, skip or a page turn (the emulator recheck tapped the page surface, which is not one of those, and the read returned at its end as designed).
- The 48 dp close button on a device (changed after the last emulator run; check pass only).
