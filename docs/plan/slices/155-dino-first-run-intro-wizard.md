# Slice 155 — Dino: a first-run intro wizard for the four things Dino does

*Planned 2026-10-26.* Dino Reader only.

**Batch:** 32 (Dino onboarding, 2026-10-26; Decision 160)
**Touches:** `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/FirstRunState.cs`, `src/DinoReader.App/IntroWizardPage*.cs` (new), `src/DinoReader.App/IntroState.cs` (new), `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/StoryWizardPage.xaml.cs`, `src/DinoReader.App/PagesStep.xaml.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/UsageCounter.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Images/**` (step pictures, if not already bundled), `src/DinoReader.Core/UsageEvent.cs`, `worker/events.ts`, `worker/*.test.ts`, `tools/dino-usage-report.ps1`, `site/dino/privacy.html`, `STORE.md`, `tests/DinoReader.Tests/**`, `docs/design/dino-reader/**`, `src/DinoReader.App/CLAUDE.md`

## Why

A new family doesn't know what Dino can do beyond the three starter books. The app has four distinct things,
and the user wants a new install to walk through them **in this order**, so people know what to expect:

1. read a book that's already on the shelf;
2. get more books from the free store;
3. record your own voice reading a story;
4. add your own book by taking pictures of it, or from a PDF.

The user dislikes helpers, coach-marks, tips and "what's new" screens, at least for first-timers. They want a
wizard with one clear message per step and clear options. The app is already usable after install (three
starters on the shelf, the voice pack downloaded in the first run), so skipping any step or all of them is fine.

Today (code check 2026-10-26): `FirstRunPage`/`FirstRunState` (Slice 93) asks the language, downloads the voice
pack with progress, then shows "Ready to read!" with "Hear Dino say hello" and **Set up the stories**, which opens
grown-up mode. `FirstRunState.IsNeeded` is `settings.Language is null`; `dino.handover.pending` shows "Ready for my
child" until the first hand-over. There are no tips or what's-new screens anywhere. Recording is **per page**
(`RecordPage`, `rec-<page>.m4a`), not per character. Recording and Add pages (`StoryWizardPage` → `PagesStep`)
sit behind the grown-up gate. Windows has no camera scan (`PagesStep`'s Scan tile is dimmed there).

## Decisions locked with the user (Decision 160)

- **Where it sits.** Language and voice download stay as they are. The four steps replace the **Set up the
  stories** button on "Ready to read!", and the wizard ends on the existing "Ready for my child" hand-over.
  This narrows Decision 98's "no tutorial": a step-by-step wizard of real actions is fine; coach-marks, tooltips,
  tips and "what's new" screens are still not.
- **Each step does the thing (three steps per the Batch 32 mock: store, record, add; no read step).** One clear message, a picture, and two buttons: **Try it** (opens the real
  feature) and **Skip**. When the person comes back from the feature, the wizard continues at the next step.
  - (Mock: the read step is dropped, the shelf is the read step.)
  - Step 1 opens `StorePage`. Offline, the store's existing calm offline screen shows, and Skip still works.
  - Step 2 opens `RecordPage` on the shortest starter book. The microphone permission is asked there, as today.
  - Step 3 opens Add pages (`StoryWizardPage`). On Windows the step says "Add photos or a PDF" and the camera
    isn't mentioned.
- **No grown-up gate during the wizard.** The person installing is the grown-up; steps 3 and 4 open directly.
  After the hand-over the gate works as today.
- **Every step** shows a counter ("2 of 4") and a **Skip all** link that goes straight to the hand-over.
- **New installs only.** Existing installs never see it (no what's-new). A **Show the intro again** row in grown-up
  mode, next to the language and version rows, replays the four steps (not language or voice download).
- **Resuming.** The current step is saved, so if the app is closed mid-wizard the next launch continues at that
  step, until the wizard is finished or skipped.
- **Telemetry, like the rest of the app** (Slices 133-134, Decision 140). The wizard's actions are counted through
  `UsageCounter`. Closed lists only, book and source `none`, nothing typed and no id:
  - `intro_started`, detail `first_run` | `replay`;
  - `intro_step`, detail `<step>:<action>` with step `store` | `record` | `add` and action
    `tried` | `skipped`;
  - `intro_ended`, detail `finished` | `skipped_all`.
  What happens inside a feature is already counted by the existing events (`book_opened`, `store_download`,
  `recording_made`, `own_book_added`), so those are not changed. The counts switch (Decision 141) covers the new
  events too: off means none are sent.
- **Design first** (as Decision 99): a mock of the four step screens before any UI code.
- New strings in all 13 Dino locales plus the base file; the layout never mirrors (Decision 100).

#### Design (Dino Reader Batch 32 mock, screens 73-77, added 2026-10-26)

File: `docs/design/dino-reader/Dino Reader Batch 32.dc.html` (index in that folder’s `README.md`, "Batch 32 mock"). Five frames: 73 at line 27, 74 at 53, 75 at 77, 76 at 99, 77 at 126. All are real UI, 360x760 phone frames (border 6px `#2b2540`, radius 34px, status bar 28px). Page font Fredoka. **The mock changes the plan: it has three steps (store, record, add), not four.** Reading a book has no step, because the shelf is right there. This slice now follows the mock. Decision 160’s text still says four steps; fix it in the index pass.

- **73, Ready to read (bottom changed from Batch 16 frame 18).** Background `#FFF6E6`; content padding 80px 22px 26px, gap 16px, centred. Dino icon 120px (radius 30px) on a 150px circle `#DFF5E3`, with a 44px green badge `#4FC060` (4px `#FFF6E6` border, white tick, stroke 3.2) at right 4 / bottom 4. Title "Ready to read!" 28px/600. Line "Every character gets their own voice. Three stories are on the shelf." 14.5px `#5a5470`, max width 280px. "Hear Dino say hello" stays: 48px pill `#ECE7FF`, text `#5B57E8` 15px/600, 34px round `#5B57E8` play button. Bottom, pinned (margin-top auto): primary button **"See what Dino can do"** (56px, radius 28px, `#FF7A59`, shadow 0 5px 0 `#E2583A`, white 17px/600) and a text button **"Skip, start reading"** (44px tall, `#5B57E8` 15px/600). The first starts step 1. The second works like Skip all and goes straight to "Ready for my child".
- **Step bar (74-76).** 44px-tall row under the status bar, padding 6px 10px 0 22px, gap 10px: three dots (8px circles; the current one is a 22x8px pill; done and current are `#5B57E8`, ahead are `#d9d2c4`), then the counter "1 of 3" (13px/600 `#8a8399`), a spacer, then **"Skip all"** (44px tall, `#5B57E8` 15px/600). It is the only extra chrome.
- **Step body (74-76).** Padding 28px 22px 26px, gap 14px, centred. Picture block 260px square (75: 220x260), title 26px/600 (margin-top 10px), line 14.5px `#5a5470` max 280px, then pinned at the bottom **Try it** (same orange button as 73) over **Skip** (44px, `#5a5470` 15px/600).
- **74, Step 1, store.** Title "Get more books". Line "The free store has more stories, ready to download." Picture: a fan of three covers on a `#DFF5E3` ellipse (inset 10px 20px): centre 132x176 radius 16 (`good-enough/2.jpg`), left 118x158 rotated -8deg (`good-enough/cover.jpg`), right 118x158 rotated +8deg (`three-little-pigs/5.jpg`), radius 14, shadow 0 8px 20px. Badge 60px `#4FC060` with a white download glyph (arrow into a tray, stroke 2.8) at right 52 / bottom 14. Try it opens `StorePage`. Offline, the store’s own offline screen (frame 22) shows and back still moves on. The step does not check the network and its message does not change.
- **75, Step 2, record.** Title "Read it in your voice". Line "Record yourself reading a story, page by page. Your child can play it any time." Picture: one 170x226 page (radius 18, `three-little-pigs/5.jpg`) on a `#FFE3D9` ellipse (inset 10px 0); 60px `#FF7A59` badge with a white microphone (stroke 2.6) at right 6 / bottom 6. Try it opens `RecordPage` (frame 44) on the shortest starter, no gate. The microphone permission is asked there, never on this screen. Saying no and going back still moves on.
- **76, Step 3, add.** Title "Add your own book". Line "Take pictures of a book’s pages, or pick a PDF. Dino reads it aloud." Picture: two photos with a 5px white border, radius 12, 124x166, tilted -6deg (left 34 / top 40, `good-enough/1.jpg`) and +5deg (right 34 / top 54, `good-enough/3.jpg`) on a `#FFF0C2` ellipse; 60px `#5B57E8` badge with a white camera (stroke 2.4) at right 30 / bottom 14. Try it opens Add pages (`StoryWizardPage`), no gate. Back, or a finished book, ends the intro on "Ready for my child". **Windows variant:** title "Add photos or a PDF", line "Pick photos of a book’s pages, or a PDF. Dino reads it aloud.", and a picture icon on the badge instead of the camera.
- **77, replay row.** Settings page, background `#F3EFE6`. The row **"Show the intro again"** sits in the white card (radius 18) that holds Version, as its first row, directly above Version, with a chevron. Its label is `#5B57E8` 14.5px/600 (other rows are black, values 14.5px/700). Row height 50px, padding 0 14px, 1px `#efe9dd` divider. It replays steps 1-3 only (never the language page or the voice download) and ends back in settings, not on the hand-over. Steps 2 and 3 open without a second gate. The Reading speed and Animal sounds cards in the frame are shortened copies of frame 70, not part of this slice.
- **Pictures** are the starter-book images already in the repo (`tests/DinoReader.Tests/Starters/`). The app must bundle them (or build the step art from the shelf’s starters); the mock only shows them.
- **New strings (all 13 Dino locales plus base):** "See what Dino can do", "Skip, start reading", "Skip all", "Try it", "Skip", "{0} of 3", the three titles and three lines above, the Windows title and line for step 3, and "Show the intro again". "Ready to read!" and "Hear Dino say hello" already exist. "Set up the stories" goes away.

**Adopted from the mock (plan changed to match):** three steps, not four, with no read step; step order store, record, add; the two buttons on 73; the step bar; the replay row above Version.

**Open point: copy drafts.** The mock calls "See what Dino can do", "Skip, start reading" and "Show the intro again" drafts. **Default:** use them as drawn.
**Open point: telemetry step names.** The plan counted `read` | `store` | `record` | `add`; the mock has no read step. **Default:** `store` | `record` | `add`; the worker rule rejects `read`.
**Open point: Windows step 3 badge.** The mock gives the Windows copy but not the glyph. **Default:** a picture glyph from the app’s existing icons, same badge size and colour; the photo composition stays.
Checked, no conflict: no gate in the intro, the step is saved and resumes after a restart (74 note), the microphone prompt is on Record only, the offline store uses its own screen, replay ends in settings.

## Work

- [x] **Mock** (Batch 32, see Design above). Was: draw the four step screens and the replay row in the Dino Reader Claude Design project in the same
  style as the Batch 16 first-run screens (16-19). Copy it into `docs/design/dino-reader/` and write its Design
  subsection here (`design-to-plan`).
- [ ] **Ready to read and step 1, the store (the smallest thing that works).** `IntroState` (current step, saved in settings under a
  named key; finished/skipped). `IntroWizardPage` with the counter, message, picture, **Try it**, **Skip**, **Skip
  all**. "Ready to read!" gets **See what Dino can do** (into the wizard) and **Skip, start reading** (to the hand-over). Step 1 opens `StorePage`;
  coming back advances. Skip all and the last step land on the hand-over. Events
  `intro_started`, `intro_step`, `intro_ended`: a name in `UsageEventName`, details in `UsageDetail`, a rule in
  `worker/events.ts` `EVENT_RULES`, the `UsageCounter` calls, and tests on both sides.
- [ ] **Steps 2-3.** Record on the shortest starter, Add pages, each opened
  without the gate and returning to the wizard. The Windows text for step 3. Offline, the store shows its offline screen as today.
- [ ] **Resume.** A launch with a saved step mid-wizard opens the wizard at that step.
- [ ] **Show the intro again** row in grown-up mode (`SettingsRows`); it sits above Version, starts at step 1 with
  `intro_started:replay` and ends back in grown-up mode, not at the hand-over.
- [ ] **Strings** in every Dino locale. **Privacy texts:** one line naming the three new events and their fields in
  `site/dino/privacy.html` and `STORE.md`. Add the new events to `tools/dino-usage-report.ps1` (a step funnel:
  tried/skipped per step, finished vs skipped all).
- [ ] Rule in `src/DinoReader.App/CLAUDE.md`: the intro is a wizard of real actions; no coach-marks, tips or
  what's-new screens.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (intro state and resume logic, the new event names
  and details); `npm test` for `worker/events.ts` (accepts the new shapes, rejects any other step or action).
- On the Android emulator, a fresh install: language, voice download, Ready to read, then each step's **Try it**
  opens the right screen and coming back shows the next step; **Skip** and **Skip all** work; offline the store
  step shows the offline screen; killing the app on step 3 reopens on step 3; an existing install (language set)
  never sees the wizard; **Show the intro again** replays it. On Windows, step 4 has no camera wording.
- With the counts switch on, a debug log of the batched events shows `intro_started:first_run`, one `intro_step`
  per step and `intro_ended`; with it off, nothing is queued.
