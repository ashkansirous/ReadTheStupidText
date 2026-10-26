# Slice 155 — Dino: a first-run intro wizard for the four things Dino does

*Planned 2026-10-26.* Dino Reader only.

**Batch:** 32 (Dino onboarding, 2026-10-26; Decision 160)
**Touches:** `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/FirstRunState.cs`, `src/DinoReader.App/IntroWizardPage*.cs` (new), `src/DinoReader.App/IntroState.cs` (new), `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/StoryWizardPage.xaml.cs`, `src/DinoReader.App/PagesStep.xaml.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/UsageCounter.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.Core/UsageEvent.cs`, `worker/events.ts`, `worker/*.test.ts`, `tools/dino-usage-report.ps1`, `site/dino/privacy.html`, `STORE.md`, `tests/DinoReader.Tests/**`, `docs/design/dino-reader/**`, `src/DinoReader.App/CLAUDE.md`

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
- **Each step does the thing.** One clear message, a picture, and two buttons: **Try it** (opens the real
  feature) and **Skip**. When the person comes back from the feature, the wizard continues at the next step.
  - Step 1 opens a starter book in `ReadingPage` (the shortest starter).
  - Step 2 opens `StorePage`. Offline, the store's existing calm offline screen shows, and Skip still works.
  - Step 3 opens `RecordPage` on the shortest starter book. The microphone permission is asked there, as today.
  - Step 4 opens Add pages (`StoryWizardPage`). On Windows the step says "Add photos or a PDF" and the camera
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
  - `intro_step`, detail `<step>:<action>` with step `read` | `store` | `record` | `add` and action
    `tried` | `skipped`;
  - `intro_ended`, detail `finished` | `skipped_all`.
  What happens inside a feature is already counted by the existing events (`book_opened`, `store_download`,
  `recording_made`, `own_book_added`), so those are not changed. The counts switch (Decision 141) covers the new
  events too: off means none are sent.
- **Design first** (as Decision 99): a mock of the four step screens before any UI code.
- New strings in all 13 Dino locales plus the base file; the layout never mirrors (Decision 100).

## Work

- [ ] **Mock.** Draw the four step screens and the replay row in the Dino Reader Claude Design project in the same
  style as the Batch 16 first-run screens (16-19). Copy it into `docs/design/dino-reader/` and write its Design
  subsection here (`design-to-plan`).
- [ ] **Step 1 end to end (the smallest thing that works).** `IntroState` (current step, saved in settings under a
  named key; finished/skipped). `IntroWizardPage` with the counter, message, picture, **Try it**, **Skip**, **Skip
  all**. "Ready to read!"'s **Set up the stories** becomes the way into the wizard. Step 1 opens the shortest starter
  in `ReadingPage`; coming back advances. Skip all and the last step land on the hand-over. Events
  `intro_started`, `intro_step`, `intro_ended`: a name in `UsageEventName`, details in `UsageDetail`, a rule in
  `worker/events.ts` `EVENT_RULES`, the `UsageCounter` calls, and tests on both sides.
- [ ] **Steps 2-4.** Store (offline screen as today), Record on the shortest starter, Add pages, each opened
  without the gate and returning to the wizard. The Windows text for step 4.
- [ ] **Resume.** A launch with a saved step mid-wizard opens the wizard at that step.
- [ ] **Show the intro again** row in grown-up mode (`SettingsRows`); it starts at step 1 with
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
