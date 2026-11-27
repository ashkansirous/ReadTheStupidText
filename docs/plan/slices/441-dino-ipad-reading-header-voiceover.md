# Slice 441 — Dino: iPad reading page hides Back and the view switch from VoiceOver
**Product:** Dino
**Touches:** `src/Dino/DinoReader.App/ReadingPage.xaml.cs`, `src/Dino/DinoReader.App/ReadingPage.xaml`, `src/Dino/DinoReader.Core/ReadingLayoutRules.cs`, `tests/Dino/DinoReader.Tests/ReadingLayoutRulesTests.cs`, `.maestro/dino/**`

*Built 2026-11-27 (wave 3 of night 2026-11-27-0154), PR #1405, not merged. Checked on the Android tablet emulator only; iPad, iPhone, VoiceOver, TalkBack and Narrator are not yet verified.*

*Planned 2026-11-25 (plan-cleanup of that day).*

**Claim note (coordinator, 2026-11-27):** `TitleLabel` (`ReadingPage.xaml` about line 29) is likely hidden when `noTitle` is true, which is the only case `ApplyHeaderHeading` sets the description; the fix must still give screen readers the heading then (e.g. a non-visual heading element or the description on a visible child), without wrapping Back and the view switch.

Found by Slice 291's 2026-11-15 App Store pass (its item 11, still open on origin/main 1a8bf725). On the iPad the reading page header floats. `ApplyHeaderHeading` (`src/Dino/DinoReader.App/ReadingPage.xaml.cs`, line 967 on origin/main 1a8bf725) sets `SemanticProperties.Description` and `HeadingLevel` on the `Header` grid; iOS then likely exposes the header as one accessibility element that hides `ReadingBack` and `ReadingModes` (hierarchy dump of run 43669367967; read from code, so likely, not shown).

## Work
- [ ] 1. Reproduce on the iPad simulator (Maestro hierarchy: `ReadingBack` and `ReadingModes` missing while the header carries a Description).
- [x] 2. Fix so Back and the view switch are reachable by VoiceOver (set the description on the title label instead of the container). Built; shown on the Android tablet emulator only (RTST_Tablet), see below. TalkBack and VoiceOver reading are in *Not yet verified*.
- [x] 3. Hierarchy check shows both ids; no change to the visible layout. Ticked for the Android tablet only: `reading-header-back-and-modes`, `dino-tablet-upright-reading` and `dino-landscape-reading-tablet` PASS on RTST_Tablet. The iPad and iPhone simulator hierarchy is not shown yet (*Not yet verified*).

**Changed while building:** the cause is that the `Header` grid no longer carries a `Description` or `HeadingLevel`, so `ReadingBack` and `ReadingModes` are separate accessibility elements. The Level1 heading now sits on the visible title label (`TitleLabel`, else the tablet's `SideTitle`), chosen by `ReadingLayoutRules.HeadingLabelFor` (`src/Dino/DinoReader.Core/ReadingLayoutRules.cs`, unit tested in `tests/Dino/DinoReader.Tests/ReadingLayoutRulesTests.cs`); the log line is `[Reading] header heading=title|side|none`. Design changed after three review passes raised the hidden 1 dp heading label (Opacity 0.01, then Transparent text): that label was removed. New flow: `.maestro/dino/reading-header-back-and-modes.yaml`. Dino tests 5067 pass; Android build OK. The tablet emulator log showed `heading=side`, `title` and `none`.

**No heading by decision (check2 #1):** a page with no visible title label (phone held sideways, picture-only page, no text card, rail layout) gets no level-1 heading. Main had the heading only as the header-grid `Description`, which is what hid Back and the view switch. Follow-up, not built here.

#### From review
- Skipped #3 (`reviews/pr-1405-high.md`): "HeaderHeading gets no FlowDirection." - the label is never drawn (clear text colour, 1 dp); screen readers read the text in its own script, and FlowDirection only changes layout.
- Skipped #4 (`reviews/pr-1405-high.md`): "Per-head verification not recorded." - the slice record and *Not yet verified* lines are written by update-docs in slice mode.
- Skipped #5 (`reviews/pr-1405-medium.md`): "SideTitle shows the same title, so a screen reader on the sideways tablet layout may announce the title twice (heading plus SideTitle text)." - the reviewer marks it unconfirmed; only the iPad VoiceOver run shows a double announcement, so it goes to Not yet verified.
- Skipped #4 (`reviews/pr-1405-check1-medium.md`): "On the sideways layout SideTitle and the level-1 HeaderHeading carry the same title; VoiceOver may read it twice." - HeaderHeading is removed (coordinator design, 2026-11-27): only one visible title label carries the heading and the helper never marks both, so a double read cannot come from the heading; the iPad VoiceOver run stays in Not yet verified.
- Skipped #1 (`reviews/pr-1405-check2-medium.md`): "no level-1 heading and no title announced." - second check pass, so only crash, data-loss, security, privacy, parity or loading findings reopen code; no heading on pages with no visible title is a follow-up (main had the heading only as the header-grid Description that caused this bug).
- Skipped #2 (`reviews/pr-1405-check2-medium.md`): "heading appears and disappears as pages turn" - second check pass, so only crash, data-loss, security, privacy, parity or loading findings reopen code; the heading owner following the visible title label is a follow-up (main had the heading only as the header-grid Description that caused this bug).
- Skipped #3 (`reviews/pr-1405-check2-medium.md`): "depends on page 2 of three-little-pigs" - second check pass, so only crash, data-loss, security, privacy, parity or loading findings reopen code; the flow's page choice is a follow-up (main had the heading only as the header-grid Description that caused this bug).
- Applied: high #1, #2 (fixed, then the label was removed); medium #1-#4 (fixed); check1 #1, #2, #3, #5 (fixed); check2 had no other findings. Findings files read in full.
- Applied in the run-checks round (coordinator, 2026-11-27): the Windows Release build (`Build-TestPackage.ps1 -App dino`) failed with CS0234 on the new `TestHookLog` line, so it is now inside `#if DEBUG` like the file's other test-log calls; check pass 3 reviewed that change.
- Skipped #1 (`reviews/pr-1405-check3-medium.md`): "ApplyHeading reads visibility flags set by two separate methods" - a nit after the third check pass; the reviewer notes both run in the layout pass. Follow-up.
- Skipped #2 (`reviews/pr-1405-check3-medium.md`): "asserts ReadingTabletTitle and heading=side, which only holds on a tablet" - a nit; the flow is a tablet flow and runs with `-Instance tablet`. Follow-up.
- Skipped #3 (`reviews/pr-1405-check3-medium.md`): "removed Header description; no action needed" - the reviewer asks for no action.

#### For CLAUDE.md
- The reading page header (`ReadingPage.xaml`) never carries `SemanticProperties.Description` or `HeadingLevel` on a container holding Back or the view switch: iOS then hides them from VoiceOver. Put the heading on the visible title label via `ReadingLayoutRules.HeadingLabelFor`. (Slice 441: docs/plan/slices/441-dino-ipad-reading-header-voiceover.md)

#### Not yet verified
- [session] The iPad and iPhone simulator hierarchy (the CI iOS simulator, Maestro) shows `ReadingBack` and `ReadingModes` on the reading page (item 1 and item 3 for iOS).
- [owner] VoiceOver on an iPad reads Back, the view switch and the book title as a heading, and does not read the title twice on the sideways tablet layout (`SideTitle` plus heading).
- [phone] Android TalkBack on a tablet reads Back, the view switch and the title as a heading.
- [owner] Windows Narrator reads Back, the view switch and the title as a heading.
