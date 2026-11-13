### Slice 362 — Dino: onboarding that shows what Dino can do (2026-11-13 mock)

*Built 2026-11-13 (wave 2026-11-13), PR #1076, not merged. Emulator flows pass; tablet, VM, iOS and wide layouts not yet verified.*

*Planned 2026-11-13 (user, RefineScope).*

**Touches:** `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/FirstBooksPage*.cs`, `src/DinoReader.App/OnboardingShell.cs`, `src/DinoReader.App/OnboardingSteps*.cs` (new), `src/DinoReader.App/OnboardingListenPage.cs` (new), `src/DinoReader.App/OnboardingVoicePage.cs` (new), `src/DinoReader.App/CornerTour.cs` (new), `src/DinoReader.App/IntroWizardPage*.cs` (removed), `src/DinoReader.App/IntroState.cs`, `src/DinoReader.App/RecordIntroPage.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/RecordViews.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/CornerShell.cs`, `src/DinoReader.App/CornerTabs.cs`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/StoryLanguagesPage.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.Core/OnboardingLayout.cs`, `src/DinoReader.Core/OnboardingDemo.cs` (new), `src/DinoReader.Core/UsageEvent.cs`, `worker/events.ts`, `worker/events.test.mjs`, `.maestro/dino/first-run.yaml`, `.maestro/dino/first-books-*.yaml`, `.maestro/dino/dino-redesign-*.yaml`, `.maestro/dino/dino-landscape-firstrun.yaml`, `.maestro/dino/dino-landscape-intro.yaml`, `.maestro/dino/dino-tablet-upright-cards-firstrun.yaml`, `.maestro/dino/dino-tablet-upright-cards-intro.yaml`, `.maestro/dino/onboarding-*.yaml` (new), `tools/desktop-vm/checks/dino-onboarding.ps1` (new), `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/FirstBooksGate*.cs`, `src/DinoReader.Core/BookAccess.cs` (FirstRunOffer only), `src/DinoReader.Core/StoryLanguageSet*.cs`, `tools/desktop-vm/checks/dino-onboarding-keys.ps1`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/Platforms/Windows/DinoKeyboard.cs`, `src/DinoReader.App/Testing/DinoTestCommands.cs` (wizard lines only), `src/DinoReader.App/AboutPage.cs`, `.maestro/dino/usage-screens-wizard.yaml`, `.maestro/dino/wizard-layout-record-large-font.yaml`, `tools/emulator/screenshot-frames.json`, `tools/desktop-vm/checks/dino-launch.ps1`, `tools/desktop-vm/Invoke-DesktopTest.ps1` (check list line; added with the coordinator's wizard-removal approval); never `tests/DinoReader.Tests/PictureBookSentenceDumpTests.cs` (live gedlg worktree)

## Why

The user redesigned the first run on 2026-11-13 ("Dino Reader 2026-11-13 Onboarding", copied to
`docs/design/dino-reader/Dino Reader 2026-11-13 Onboarding.dc.html`). It replaces Slice 325's first run and its
separate intro wizard (two places, read, record, languages) with one guided path of six required steps that
shows what Dino can do: language, story languages, first books, listen (voices, feelings, every family language),
your voice (record one character, hear yourself), then the grown-ups' corner tour and the child's shelf.

Today (origin/main, `ground-in-code` 2026-11-13): `App.xaml.cs:277` opens `FirstRunPage` (language), then
`FirstBooksPage` (two free books at least, `BookAccess.FirstRunOffer`), the Ready panel, then `IntroWizardPage`
(steps Places, Read, Record, Languages; `UsageEvent.cs:192-205`). Story languages are `StoryLanguageSet`, the app
language plus at most two more (`StoryLanguageSet.cs:16`).

**The mock trumps this plan and the shipped app** (`~/.claude/CLAUDE.md`, *Claude Design*). The final copy for every
screen is in `docs/plan/notes/2026-11-13-dino-copy.md` (*Onboarding*); the canvas carries the same text.

## Decisions (user, 2026-11-13, RefineScope)

1. **The demo recording does not stay.** The voice step is a try: its clips are deleted when onboarding ends
   (Continue on N7, or leaving the flow any other way). N7's second button is **Play again** (replays the page),
   not "Remove my voice".
2. **One set demo page per free book**, because the parent may not pick The Three Little Pigs. Steps 4 and 5 use
   the page of a picked free book: The Three Little Pigs page 13 (Pop scared, Pim calm; record Pim) when it is
   picked, otherwise the first picked of The Lion and the Mouse page 11 (Lion doubtful, Mouse sure; record Mouse),
   The Gingerbread Man page 3 (Granny pleading, Gingerbread Man laughing; record Granny), X Marks the Treasure page 10
   (Seagull worried, Pancake Polly cheerful; record Pancake Polly). Pages chosen by the planner from each
   `story.md` (two speakers, two feelings, short); the user may swap any. The copy names the recorded character
   through `{0}`, so no string is book-specific.
3. **The Three Little Pigs is ticked by default** on step 3 and can be unticked (the mock's N3). The step's line is
   the existing `FirstBooks_Body`.
4. **Microphone refused:** step 5 shows "Microphone is off" with "Dino needs the microphone to record. You can allow
   it in Settings.", an **Open settings** button and a quieter **Continue without recording** link, the only way past
   step 5 without a recording.
5. **"Show the intro again" goes** from Settings (the mock has no replay). The intro wizard and its strings are
   removed once nothing uses them.

## Work

- [x] A. Smallest end to end: the step bar and steps 1-3 in the new shell (N1-N3, WN2), replacing `FirstRunPage` and
  `FirstBooksPage`'s chrome; step 2 is required but Continue is live (app language ticked, grey, "App language ·
  always on"); Three Little Pigs pre-ticked; the getting-books wait and offline screen stay as built. Strings in all
  15 languages. Android, iOS and the Windows head (one MAUI path; the W frames for 600 or wider).
- [x] B. Step 4, listen (N4a-N4d, WN4): the demo page for the picked book (Decision 2, pure choice in
  `DinoReader.Core/OnboardingDemo.cs` with unit tests for every pick order), the reading page in demo mode (plays by
  itself, speaker chip, no view switch), the language bar with the amber pulse and the bubble after the first
  language ends, Continue waking as the second language starts (or at the page end with one language).
- [x] C. Step 5, your voice (N5, N6a/b, N7): the intro card with the privacy card (phone/tablet/computer variants),
  the one-line recording pages limited to the demo character's lines on the demo page (no Skip, no Done), the
  microphone-refused state (Decision 4), then the reading page with the grown-up's clips and the coral "· your
  voice" chip; Play again; the clips are deleted when the flow ends (Decision 1; unit test that nothing is left on
  the book). Microphone permission asked on Record. Android and iOS permission paths both; Windows the same flow.
- [x] D. Step 6, the corner and the shelf (N8a, N8b, N9, N10): welcome card over the Books tab, the three-tab tour
  (each tab lifted with the pulse, the tip, "{0} of {1}", Next), the Child's shelf pointer, the shelf card with Start
  reading; Android Back does nothing during the tour; reduce motion keeps the ring without the pulse. Rail layouts
  for 600 or wider (tips point at the rail tabs). After Start reading, later launches open on the shelf.
- [x] E. Remove the old intro: `IntroWizardPage*`, `IntroState` replay, the Settings row "Show the intro again"
  (Decision 5), the Ready panel, unused `Intro_*`/`FirstRun_Ready*` strings (`NoHardCodedUiTextTests` and resx
  tests stay green). Usage events: new onboarding step values replace `IntroSteps` (`UsageEvent.cs`,
  `worker/events.ts` and its test), production-only as today.
- [x] F. Maestro flows: replace `first-run`, `dino-redesign-onboarding`/`-intro`, landscape and tablet first-run/intro
  flows with `onboarding-*` flows (phone, landscape, tablet, two languages, one language, mic refused); Windows VM
  flow for WN2/WN4.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (demo pick, recording cleanup, strings in every language).
- `node --test worker/events.test.mjs`.
- Android emulator: the onboarding flows above, each frame checked against the mock, Farsi right to left on N4c.
- iOS: compile on Windows (`-p:IncludeIos=true`), then a TestFlight run of the whole flow including the microphone prompt.
- Windows head on the VM: WN2 and WN4, keyboard (Enter, Space, Tab).

#### Design (Dino Reader 2026-11-13 Onboarding mock, added 2026-11-13)

Source: `docs/design/dino-reader/Dino Reader 2026-11-13 Onboarding.dc.html` (frames N1, N2, N3, N4a-N4d, N5, N6a,
N6b, N7, N8a, N8b, N9, N10, WN2, WN4 and the "600 wide or more" rule: 14 frames, no frame for the mic-refused
state). The mock agrees with this plan on the six steps; where it differs, the mock wins (list at the end). Final
text: `docs/plan/notes/2026-11-13-dino-copy.md` (*Onboarding*); keys are named below, never retyped. Colours are the
mock's hex values: use the `DinoTokens` key that already holds the same value, else add one (brand `#5B57E8`, coral
`#FF7A59` with shadow `#E2583A`, cream `#FFF6E6`, ink `#2b2540`, text grey `#5a5470`, tint `#ECE7FF`, amber `#FFC24D`,
highlight `#FFE27A`). Fonts: Fredoka (UI), Andika (book text), Vazirmatn (Farsi/Arabic text only). The phone frames are
405 x 864 with a 6 px ink border (the device bezel, canvas furniture: not built); the status bar (9:41, 100%) and the
home bar are canvas furniture too. Icons in the mock are inline SVG, not Phosphor: build them from `IconKind`
(`Phone`, `Mic`, `Check`, `Play`, `Pause`, `TabBooks`, `TabStore`, `TabSettings`, `Globe`), adding a glyph only where
`IconKind` has none (sound wave, replay).

**Shared (all steps, phone, under 600 wide):**
- **A. Step bar:** padding 6 20 0, six segments side by side, gap 5, each flex 1, height 5, radius 3; done and current
  `#5B57E8`, later `#e6e0d4`. Under it the label, 12.5 / 600, `#5a5470`: `Onboarding_StepOf` "Step {0} of {1} · {2}"
  with the step name (N1 `Onboarding_Step_Language`, N2 `StoryLanguages_SettingsTitle`, N3 `Onboarding_Step_Books`,
  N4 `Onboarding_Step_Listen`, N5 to N7 `Record_YourVoice`). Steps 1 and 2 use padding-top 6, steps 4 to 7 use 4.
- **Continue:** height 56, radius 28, fill `#FF7A59`, shadow 0 5 0 `#E2583A`, white 17 / 600, centred (`Common_Continue`;
  N3 reads `FirstBooks_GetThese`). Sits at the foot, 20 from each side, 42 from the bottom (padding 12 20 42). Dim
  (opacity .45, not tappable) where a step says so.
- Headings 24 / 600, line 1.2 (21 / 600 on N4 and N7); body 14.5 (14 on N4 and N7) `#5a5470`, line 1.45 (1.4).
- Layout never mirrors (`src/DinoReader.App/CLAUDE.md`): N4c is Farsi text in the same layout, only the text runs right
  to left.

**N1, language (A):** content padding 14 20 42, gap 18. Top: the app icon 80 x 80, radius 22, shadow 0 6 0
rgba(43,37,64,.08), then the title (`FirstRun_Hello`, 24 / 600) and the line (`FirstRun_HelloBody`, 14, max width 300,
centred). Then a grid of language rows, gap 8, each 52 high, radius 16, padding 0 12, a 28 round flag (ring 1.5
rgba(43,37,64,.12)), the name 16 / 600 (Vazirmatn for Arabic and Farsi). The ticked row is `#5B57E8` with white text and
shadow 0 3 0 `#3f3bc4`; others white. The phone's language is ticked first, so Continue is live at once.

**N2, story languages (A), WN2:** title `Onboarding_LanguagesTitle`, line `Onboarding_LanguagesBody` with `{0}` = the
app language's name. The tint card (`Onboarding_WhyMoreTitle` bold, then `Onboarding_WhyMoreBody`): margin 12 20 0, padding
12 14, radius 16, fill `#ECE7FF`, text `#2f2b6b` 13.5 / line 1.45, gap 10, with a small info glyph at the start. The list:
margin 12 20 0, white, radius 18, scrolls; rows min-height 56, padding 0 14, gap 12, a 1 px `#efe9dd` rule between rows, a 28
flag, the name 16 / 600 and a 28 x 28 radius-8 tick box at the end. The app language is first, box `#C9C3D6` with a
white check, not tappable, second line `StoryLanguages_AppLanguageAlwaysOn` 12.5 / 500 `#5a5470`. Ticked others: box
`#5B57E8` with a white check. Unticked: 2 px `#d9d2c4` border. Up to three in all (`StoryLanguageSet`). Continue is
live from the start; no Skip.
- **WN2 (Windows 1100 x 760, 600 or wider):** title bar 32 high (icon 16, `App_Name` 12.5 / 500, minimise, maximise,
  close 46 x 32: the Windows chrome, not built by us). Left pane 400 wide, fill `#FFE9C7`, padding 28 32, gap 14: the
  step label, the heading 30 / 600, and the body copy 15.5 `#4a4460`, line 1.5: `Onboarding_LanguagesBody` then
  `Onboarding_WhyMoreBody` as one paragraph (the tint card is not drawn here). The canvas's WN2 paragraph leaves out
  "Add any others your child hears."; the copy note lists both keys for WN2, so the note's text is built. Right pane: padding 28 40,
  gap 14, the list card only, Continue under it, right-aligned, 260 wide. Space toggles the focused row.

**N3, first books (A):** heading `FirstBooks_Title`, line `FirstBooks_Body`. Grid 2 columns, padding 14 20 0, gap 14,
covers radius 16; picked cover gets a ring 0 0 0 3 `#FFF6E6` + 0 0 0 6 `#5B57E8` and a 34 round `#5B57E8` badge with a
white check at right 8, top 8; unpicked: a white 34 round badge with a 2 px `#d9d2c4` border. Title under each cover 14 / 600.
Footer fades in (gradient from transparent to `#FFF6E6` at 30%), padding 10 20 42: the count `FirstBooks_PickedNoSize`
13.5 `#5a5470`, then Continue `FirstBooks_GetThese`, live at two picks. The Three Little Pigs is ticked at the start
(Decision 3).

**N4a-N4d, listen (B), WN4:** padding 8 20 6: title `Onboarding_ListenTitle` 21 / 600, line 14. Body per frame:
N4a and N4d `Onboarding_ListenBody`; N4b `Onboarding_ListenNext` ({0} = the language just heard); N4c
`Onboarding_ListenOther`; WN4 `Onboarding_ListenWide` ({0} first language, {1} second).
- **Top bar (all but N4d):** padding 4 12 4 8, gap 8: the back or read-aloud button 44 x 44 at the start (reading page's
  own), the language bar at the end: a segmented pill, padding 3, gap 2, radius 16, fill `#F0E8D8`; each segment 40 high,
  radius 13, padding 0 12 0 6, a 26 round flag, name 15 / 600 (Vazirmatn for Farsi/Arabic); the playing language is
  white with shadow 0 1 4 rgba(43,37,64,.16) and ink text, others `#5a5470`. Three languages give three segments. The view
  switch is hidden (one view). **N4d** (one family language): no bar, only the reading page's flag pill, 46 high, radius 16,
  fill `#F0E8D8`, padding 0 10 0 6, flag 34 with ring 0 0 0 2 #fff + 0 0 0 3 rgba(43,37,64,.14).
- Book title line: padding 0 8, 20 / 600, line 1.4 (`The Three Little Pigs`, from the book).
- **Stage:** margin 6 14 0, radius 18, fill `#F0E8D8`, the demo page's picture filling it. The words card on the picture:
  left/right/bottom 8, padding 10 0, `rgba(255,255,255,.95)`, radius 16, shadow 0 4 12 rgba(43,37,64,.16); text Andika
  20 / line 1.6 (Vazirmatn 19 / 1.75 for Farsi), padding 0 14. The sentence being read has `#FFE27A` behind, radius 8,
  spread 4; sentences not yet read are `#6f6884` in N4b and N4d, as the shipped highlight does.
- **Speaker chip:** 34 high, radius 17, fill `#2b2540`, white 14 / 600, padding 0 12 0 10, sound-wave glyph then the name, shadow 0 4 12
  rgba(43,37,64,.3), placed on the picture beside whoever speaks ("Pop", then "Pim"; "پیم" in Farsi, Vazirmatn). Positions
  in the mock are samples; use the character's place in the picture.
- **N4b bubble:** right 10, top 10 (below the language bar), 250 wide, padding 12 14, radius 16, fill `#2b2540`, white 14.5 /
  line 1.4, shadow 0 8 20 rgba(43,37,64,.35), with a 16 x 16 rotated square pointer at right 46, top -8 pointing at the second
  language; text `Onboarding_TapLanguage` ({0} = the language name, Vazirmatn for Farsi). The segment gets the amber
  ring pulse (`ringPulse` in the mock: ring 4 `rgba(255,194,77,.95)` growing to about 10 and fading); reduce motion keeps the
  ring and drops the pulse. Continue stays dim (opacity .45).
- **Foot:** padding 12 20 40, gap 12: a 60 round `#5B57E8` replay button with a white play or replay glyph (replays the
  page), then Continue (56 high). N4c: Continue is live as the second language starts; N4d: live when the page ends.
- **WN4 (Windows):** left pane as WN2 (`#FFE9C7`, 400 wide). Right pane padding 20 32 24, gap 12: a row with the title 20 /
  600 at the start and the language bar at the end; the stage below: picture (flex 1, radius 18) and the words panel 260 wide,
  white, radius 18, padding 14 4, text centred vertically, side by side with gap 16; the chip on the picture (left 170, top
  120 in the mock). Foot row gap 14: replay button 56 round, then Continue 260 wide at the right.

**N5, be the voice (C):** heading `Onboarding_VoiceTitle`, line `Onboarding_VoiceBody` ({0} = the demo
character; "you" bold). Picture card: margin 14 20 0, height 250, radius 24, the demo page's picture cropped on the character
(background 210% wide, 50% 52%); a red mic badge, 68 round `#E5484D`, 4 px `#FFF6E6` border, centred, hanging 18 below the card.
Privacy card: margin 30 20 0, padding 14, radius 16, white, gap 12; a 40 x 40 radius-12 `#DFF5E3` tile with the green phone icon
(`IconKind.Phone`, `#2E7D32`); title `Onboarding_StaysTitle_Phone` (`_Tablet`, `_Device` for Windows) 15 / 600, body
`Onboarding_StaysBody` 13.5 `#5a5470`. Foot padding 12 20 42, gap 8: `Onboarding_MicAsk` 13 `#5a5470`, then the coral button
`Onboarding_RecordLines` ({0} = character). The microphone permission is asked on Record, not here.

**N6a, N6b, record lines (C):** the shipped one-line record page (frames 180-182 of Batch 48) with step label on top and
`Onboarding_LinesTitle` 18 / 600 + `Record_LineOf` 12.5 `#5a5470` in the header (44 back arrow). Stage margin 4 14 0, radius
18, fill `#F0E0C0`, the page picture; the line card at the bottom (left/right/bottom 8, padding 12 14 14, `rgba(255,255,255,.96)`,
radius 16, shadow 0 4 12 rgba(43,37,64,.16), gap 4): `Record_Says` 13.5 / 600 `#5B57E8` and, once recorded, a green "Recorded"
tag (`Record_Recorded`, height 24, radius 12, `#DFF5E3`, `#2E7D32`, 12 / 700, check glyph); the line Andika 22 / 1.45. Controls
row (gap 30, padding 14 0 40): N6a: Record 80 round `#E5484D` with `Rec_Start`, Next 56 round `#ECE7FF`/`#5B57E8` at opacity .4;
N6b: Re-record 56 round white with 2 px `#F2C4C5` border, Play 80 round `#FF7A59`, Finish 56 round `#5B57E8` with a white
check (`Rec_Redo`, `Record_Play`, `Record_Finish`). No Skip and no Done in this step; Record is the only way on.
Recording and its stop square are the shipped ones. Finish goes to N7.
- **Mic refused (not drawn; Decision 4):** the shipped "Microphone is off" panel (`Rec_NoMicTitle`) with
  `Onboarding_MicOffBody`, a coral `Onboarding_OpenSettings` button and a quiet text link `Onboarding_ContinueWithoutRecording`.
  Built from the shipped look of that panel; no mock frame, so it is checked against that panel, not a frame.

**N7, hear yourself (C):** same page layout as N4a: title `Onboarding_ThatsYou` 21 / 600, line `Onboarding_ThatsYouBody`
({0} = the character), language bar as N4. The speaker chip is coral `#FF7A59` (not ink) and reads `Onboarding_YourVoiceChip`
("Pim · your voice"). The page plays from the first line: the first speaker in their own voice, then the character in the
grown-up's voice (highlight over the character's lines). Foot: replay circle 60, then a stacked pair: **Play again**
(`Onboarding_PlayAgain`, height 40, radius 20, 2 px `#d9d2c4` border, 14.5 / 600 `#3a3450`, replay glyph) above Continue (56).
Continue deletes the clips (Decision 1).

**N8a, welcome to the corner (D):** the real Books tab (ink header `#2b2540`: "Grown-ups’ corner" 11.5 / 600, letter-spacing
.8, `#FFC24D`, then the tab title 22 / 600, and the coral Child's shelf pill 44 high, radius 22, shadow 0 3 0 `#E2583A`; the
line under the header 13.5 `#5a5470`; the tab bar white with a 1 px `#e8e1d3` rule, tab 52 high, icon pill 52 x 28 radius
14, label 12 / 600) under a scrim `rgba(20,14,40,.62)`. Welcome card: left/right 20, top 190, radius 28, padding 22 20 20, fill `#FFF6E6`,
shadow 0 24 60 rgba(20,14,40,.45), gap 16. Head: a 56 round `#2b2540` disc with the corner glyph, `Tour_WelcomeTitle`
22 / 600, `Tour_WelcomeLine` 14 `#5a5470`. Three rows, gap 12: a 40 x 40 radius-12 `#ECE7FF` tile with the tab's own icon
(`TabStore`, `TabBooks`, `TabSettings`), title 15.5 / 600 and line 13.5 / 1.4 `#5a5470`: `Tour_StoreTitle`/`Tour_StoreLine`,
`Tour_BooksTitle`/`Tour_BooksLine`, `Tour_SettingsTitle`/`Tour_SettingsLine`. Coral button `Tour_ShowMeAround`.

**N8b, the tour (D):** the same screen under the scrim; the tab being explained is lifted out of the scrim: a white tile 120 x 58,
radius 18, at the bottom (bottom 36, centred on that tab), with the amber pulse (reduce motion: ring, no pulse). The tip: width
300, padding 16 16 14, radius 20, fill `#FFF6E6`, shadow 0 16 40 rgba(20,14,40,.4), gap 8, a 18 x 18 rotated-square pointer at the
bottom edge towards the tab, left 46 (bottom 122) for the Store; title 18 / 600, text 14 `#3a3450` line 1.45. Foot: the counter
`Intro_Counter` ("{0} of {1}", 12.5 / 600 `#5a5470`) and a brand button `Wizard_Next` (40 high, radius 20, `#5B57E8`, white
14.5 / 600). Tips: Books `Tour_TipBooks`, Store `Tour_TipStoreTitle` + `Tour_TipStore`, Settings `Tour_TipSettings`. Each
step switches to its tab behind the scrim. Android Back does nothing during the tour.

**N9, point at the child's shelf (D):** scrim as N8a. The coral Child's shelf pill (as in the header: right 12, top 60) lifted and
pulsing. Tip: right 12, top 128, width 300, as N8b with the pointer at right 60, top -8; title `Tour_ShelfTitle`, text
`Tour_ShelfLine` ("Child’s shelf" bold), `Tour_LastStep` and a brand button `Tour_ShowMe`. The pill itself or the button both open the
kids' shelf.

**N10, the child's shelf, last card (D):** the kids' shelf (title "My stories" 22 / 600 with the 44 icon, white Grown-ups pill 44
high, radius 22, 15 / 600; tiles radius 16, shadow 0 4 0 rgba(43,37,64,.08), titles 14.5 / 600) under a lighter scrim
`rgba(20,14,40,.45)`; the Grown-ups pill is lifted and pulses. Card: left/right 16, bottom 40, radius 28, padding 20, fill
`#FFF6E6`, shadow 0 24 60 rgba(20,14,40,.45), gap 12: `Tour_ChildShelfTitle` 22 / 600, `Tour_ChildShelfLine` 14.5 `#3a3450`, a white
radius-16 padding-12 box with the hold hint `Tour_ChildShelfBack` ("Grown-ups" bold, 13.5) and a lock glyph, then the coral
`Tour_StartReading` button. Start reading closes it; later launches open on the shelf.

**600 wide or more (WN2, WN4 and every step):** two panes as the 2026-11-08 rule says: left pane `#FFE9C7`, 400 wide on
Windows, with the step bar, heading and body, never a button; right pane everything you tap, Continue at its foot, right-aligned,
260 wide on Windows. Listen and voice steps: the right pane is the page with picture and words side by side (WN4).
N8 to N10 use the rail layouts of the 2026-11-08 grown-up mock; tips point at the rail's tabs. Keyboard: Enter = Continue,
Space = play or pause (Space toggles the focused row in WN2), Tab moves through rows.

**New strings (all 15 `AppResources.<lang>.resx`, `NoHardCodedUiTextTests`):** every "New" and "Changed" row of
*Onboarding* in `docs/plan/notes/2026-11-13-dino-copy.md` (`Onboarding_*`, `Tour_*`, `StoryLanguages_AppLanguageAlwaysOn`,
`FirstBooks_GetThese`; changed: `FirstRun_Hello`, `FirstRun_HelloBody`). Old `Intro_*` and Ready strings go when unused.

**Plan changed to match the mock (2026-11-13):**
- N3's "ticked by default" note: kept as Decision 3 (the mock's own review question 3 asks "fine, or ...": the mock draws it ticked, so it stays).
- N7's "Remove my voice" and the mock's review question 1 (clips stay on the book): **superseded by the user's Decision 1**
  (2026-11-13), which the mock's text already carries: N7 shows **Play again**, the clips are deleted when setup ends.
- N6 mic refused: added as Decision 4 (the mock lists it as a proposal, user said yes); it has no frame.
- Frame 198 and the old intro wizard: replaced by N4 to N10 (Work E removes the old pages and strings).
- WN2's left pane has no tint card: it joins `Onboarding_LanguagesBody` and `Onboarding_WhyMoreBody` (the canvas
  paragraph lacks the sentence "Add any others your child hears."; the copy note's text wins). WN4 uses
  `Onboarding_ListenWide`.
- **Rules in `src/DinoReader.App/CLAUDE.md` that this mock changes (past design choices, not hard rules; the file is in
  Touches and must be updated in this slice):** "Intro (Slice 155, Decision 160) ... No coach-marks, tooltips, tips or
  what's-new screens" (the mock's N4b bubble and the N8b/N9 tips are tips); "`LaunchedFromIntro` makes Done read Continue"
  and "Windows intro keys" (the record page has no Done or Skip here); "Every way of changing the story or app language
  (`LanguagePage`, `StoryLanguagesPage`, `IntroWizardPage`)" (the wizard goes); "First run and intro are built on
  `OnboardingShell`" (now first run only, step 2 and 4 to 6 added). No hard-rule clash found: layout stays unmirrored,
  the recording stays in `RecordPage` only (N5 asks the microphone on Record, N6 uses `RecordPage`), and the privacy page
  and the manifest/plist microphone comments stay as they are because nothing new is recorded or sent; the privacy
  card's promise ("never uploaded") matches `StoryRecordings` (backups only where the grown-up sends them).

## From review

- Flow `onboarding-landscape` failed twice on `assertVisible "Viele Kinder hören zu Hause eine Sprache.*"` (run4): the paragraph is on screen, not missing or clipped. In split mode the step joins the lead and the "why more" sentence into one label (`OnboardingSteps.Languages.cs:49`), so the hierarchy exposes one text "Deutsch ist immer dabei. Füge weitere Sprachen hinzu, ... Viele Kinder hören zu Hause eine Sprache und in der Schule ... Wähle bis zu drei." Maestro matches the whole text, so a pattern that starts at "Viele" cannot match. Fixed in the flow with a leading `.*`; the other onboarding flows only assert standalone labels, so none has this pattern.

- Skipped #11 (`reviews/pr-1076-high.md`): "OnWindowResumed re-checks Permissions.CheckStatusAsync<Microphone>(); on Windows this likely returns Granted without a real check, so a refused mic could bounce the page back to the intro" - not reachable on Windows: `RecordPage.EnsureMicrophoneAsync` returns true under `#if WINDOWS` (the recorder asks when it opens the device), so `MicRefused` and the "Microphone is off" state are never set there and the re-check never runs; on Android and iOS it reads the real status.
- Applied with a different shape: #10 (`reviews/pr-1076-high.md`): "FirstRunState.HandoverPending is never set true now ... FallbackKind.IntroStateReset has no remaining user" - the CLAUDE.md lines and the dangling cref were fixed; the dead handover pill branch, `Gallery_ReadyForChild` strings and the `intro_state_reset` reason (a closed list the Worker must match) stay for a later clean-up slice.
- Applied as one design (round 2: high #4; medium #1, #3, #6, #7): the child's try is one owned record (`OnboardingTryRecord`, kept by `OnboardingTryStore`), the book id plus the exact clip keys and files `RecordPage` made in the voice step, saved before each clip is stored on the story; clips the book held before are never in it.
  Deleting it removes exactly those clips and files, under one async lock shared by the steps and the gallery. It runs when the child leaves the voice step (Continue on N7 or Continue without recording; the step has no Back, Decision 1 "or leaving the flow any other way"), and at every onboarding step and gallery appearing while a record exists and the saved step is not Voice (an abandoned or killed setup).
  The record is cleared only after the delete finished; a failure keeps it and reports `onboarding_try_not_deleted`. "That's you" shows only when the record has a live clip on the book. The Record page's finish wait is bounded (15 s), so a page that returns without a clip leaves Record usable.
  Pure parts are in `DinoReader.Core/OnboardingTryRecord.cs` with tests (which clips, when, round trip).
- Not applied: #3 loading state (`reviews/pr-1076-medium.md`): "ShowCornerAsync awaits DeleteTryAsync (lists the shelf, updates and deletes files for every book) with the old page on screen and no wait overlay; can exceed 500 ms on slow disk or many books" - the delete now touches the one recorded book only (one read, one update, a few file deletes), which cannot reach 500 ms, so no overlay; the "two concurrent deletes" half is fixed by the lock.
- Applied with a different shape: #8 (`reviews/pr-1076-medium.md`): "SwitchDemoLanguageAsync calls LanguageStarted (which wakes Continue) before awaiting SwitchLanguageAsync" - the switch runs behind the shared wait overlay (shown after 500 ms), and Continue wakes only after the switch finished.
- Applied (check 1, `reviews/pr-1076-check1.md`) #1, #2, #3, #6, #8: a clip joins an existing try record of any book (`TryClip.BookId`, deletion settles each book), the listen step is saved before the books gate clears, `LanguageStarted` only when the session did not raise `Failed`, `ShowShelfInstead` records the finished event, the default tick waits until the Pigs are listed.
- Skipped #4 (`reviews/pr-1076-check1.md`): "Chrome not redrawn on rotation/IsWide change; stale body text variant" - low, no failure path shown (the chrome is rebuilt on the next state change).
- Skipped #5 (`reviews/pr-1076-check1.md`): "FirstRunState.HandoverPending never set true now; gallery branches dead" - low, same as the earlier skipped clean-up of the dead handover pill (high #10).
- Applied (check 2, `reviews/pr-1076-check2.md`) #1, #2, #4, #6: every tour end (also a stop that cannot be drawn) leaves grown-ups mode; a kill on the story languages step reopens it (`FirstBooksGate.StoryLanguagesPending`, cleared by its Continue after the pick is saved) and the voice step saves the corner step before the try is deleted; the listen and "That's you" reads carry origin `wizard` (no onboarding origin exists; the demo read counts no `book_opened`/`read_started` anyway, `DemoReadCounting`), pinned in `UiUsageTests`.
- Skipped #3 (`reviews/pr-1076-check2.md`): "StepDone(StoryLanguages) recorded on every Continue (Back from step 3 then Continue again); _startCounted is per page instance so intro_started recounts after a kill. Funnel counts inflated." - low, no failure path shown.
- Skipped #5 (`reviews/pr-1076-check2.md`): "MoveOnAsync(from) sends a failed/empty Listen to Corner, skipping Voice though Voice needs only the demo page; no StepDone/skipped usage event for the skipped steps." - low, no failure path shown.
- Skipped #7 (`reviews/pr-1076-check2.md`): "one-letter-apart names for different steps; reports can mix buckets" - low, no failure path shown.
- Skipped #7 (`reviews/pr-1076-check1.md`): "exceptions unobserved, AfterRecordAsync may read MicRefused before pop finishes" - low, no failure path shown.
- Skipped #9 (`reviews/pr-1076-check1.md`): "LaunchedFromIntro / _header.Continue = LaunchedFromIntro dead since IntroWizardPage deleted" - nit.
- Skipped #10 (`reviews/pr-1076-check1.md`): "BuildReplay and BuildPlayAgain duplicate the ReplayTapped wiring" - nit.
- Skipped #1 (`reviews/pr-1076-check3.md`): "Restart at step 2 opens OnboardingLanguagesPage as root with BackRequested null; Back exits app" - low, no failure path shown (matches the restart route of the books step).
- Skipped #2 (`reviews/pr-1076-check3.md`): "StoryLanguagesPending set in ContinueAsync but step-2 Back (Language=null; PopAsync) never clears it" - low, no visible bug today (the review says IsNeeded wins in RootPage).
- Skipped #3 (`reviews/pr-1076-check3.md`): "New Pending && StoryLanguagesPending branch in RootPage is untested" - low, no failure path shown.
- Skipped #4 (`reviews/pr-1076-check3.md`): "End() calls _showShelf() unconditionally; normal end already shows the shelf at the ChildShelf stop, so twice" - low, no failure path shown.
- Skipped #5 (`reviews/pr-1076-check3.md`): "mixing with legacy wizard data in analytics" - low, no failure path shown (a separate origin needs a Worker allowed-value change).
- Skipped #1 (`reviews/pr-1076-check4.md`): "Wait for RecordButton times out if Record page is not in Ready state" - low, covered by the emulator runs that passed (`onboarding-mic-refused`, run2).
- Skipped #2 (`reviews/pr-1076-check4.md`): "Both permission-dialog taps optional and the refusal log marker is only a comment" - low, covered by the emulator runs that passed (`onboarding-mic-refused`, run2).
- Skipped #3 (`reviews/pr-1076-check4.md`): "FirstRun_HelloBody now two sentences; slice fixes 14 pt / max width 300, not re-checked for longer de/sv/ru/fi" - low, covered by the emulator runs that passed; no `layout-check ok` line was printed, so layout is not confirmed (see *Not yet verified*).
- Skipped #4 (`reviews/pr-1076-check4.md`): "scrollUntilVisible added for the Swedish row; header comment line 4 still says OnboardingLanguage-sv" - low, a comment only; the flow passed in run2.

## Device run 2 (emulator, `onboarding-phone`): not an app bug

- The flow said `Missing log marker(s): [Onboarding] voice record opened lines=`, but the app log has the voice step: `18:48:40.811 [Onboarding] voice shape=stack`, `18:48:52.513 [Onboarding] voice record opened lines=1`, then `[Record] finish` and `listen enter kind=HearYourself` before the tour. The listen step ended normally (`listen page-ended stage=First continue=True`, `listen continue`), so the "failed Listen skips Voice" route was never taken.
- Cause: `Test-LogMarker` (tools/emulator/common.ps1) refuses a marker followed by a word character, so a marker ending in `lines=` can never match `lines=1`. Fixed in the flow: the marker is now `lines=1` (Pim has one line on Pigs page 13). The route is unchanged; check2 #5 stays skipped.

## Device run 3 (emulator-5554, Dino debug; logs `D:/ReadTheStupidText-worktrees/reviews/w54-362-emu/`, run1..run5)

- Final PASS: `onboarding-phone` (run3, marker `[Onboarding] voice record opened lines=1`), `onboarding-mic-refused` and `onboarding-two-languages` (run2), `onboarding-one-language` and `first-books-picker` (run1), `first-books-cancel` and `first-books-offline` (run3), `onboarding-landscape` (run5).
- Every earlier failure was a flow or string fault, each fixed: the old English `FirstRun_Hello`, the Record button tapped before the deny, the sv row below the fold, a marker ending `lines=` that can never match, "Weiter" vs "Continue", the joined paragraph label. No app logic fault found on the device.
- No `layout-check ok` line was printed for any flow, so layout is not confirmed by the run.
- Tooling gap (for the feedback loop): `Test-LogMarkerCanMatch` in `tools/emulator/common.ps1` tests with " end" appended, so a marker ending mid-token (`lines=`) passes lint but can never match at run time.

## Not yet verified

- `onboarding-tablet`: no tablet instance.
- VM checks `dino-onboarding` and `dino-launch`: free memory 6.5 GB, below the 8 GB VM rule.
- iOS: compile only; the microphone prompt needs a TestFlight run.
- WN2 and WN4 wide layouts, and right-to-left (Farsi N4c), on a device.
- Lion and X Marks demo pages: no such books in the test store.
- 55 new strings are machine drafts and need a native read.
- Layout on the emulator flows: no `layout-check ok` line was printed.

#### For CLAUDE.md

None given in the brief.
