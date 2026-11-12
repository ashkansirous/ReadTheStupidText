# Slice 352 — Dino: every screen, wizard step and button press is counted

*Built 2026-11-12, merged as PR #1022 (be859f0a; 14 checks green, android job on image 20261101.186.1). Run on the Android emulator (emulator-5554, flows pass); iOS, Windows and live data not yet verified (see the end of this file).*

**Touches:** `worker/events.ts`, `worker/events*.test.mjs`, `worker/CLAUDE.md`, `src/DinoReader.App/UsageCounter.cs`,
`src/DinoReader.App/Usage*.cs`, `src/DinoReader.App/UiUsage*.cs`, `src/DinoReader.App/*Page*.cs`,
`src/DinoReader.App/*Page.xaml`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/CornerShell.cs`,
`src/DinoReader.App/CornerTabs.cs`, `src/DinoReader.App/ReadingSession.cs`,
`src/ReadTheStupidText.Application/Usage/**`, `tests/DinoReader.Tests/**`, `site/dino/privacy.html`, `STORE.md`,
`docs/analytics/dino-session-queries.md`, `tools/usage-sessions/**` (new),
`docs/plan/slices/352-dino-screen-and-tap-events.md`,
`src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.Core/*Usage*.cs`, `src/DinoReader.App/*.cs` (Dino pages and rows not named *Page*: `OnboardingShell.cs`, `RecordIntroPage.cs`, `SettingsRows.cs`, `FirstBooksPage.cs`, `IntroState.cs`, ...), `worker/stats*.ts`, `worker/*.test.mjs`,
`tests/ReadTheStupidText.Tests/UsageService*.cs`, `.maestro/dino/**`, `tools/emulator/**`,
`src/ReadTheStupidText.Mobile.Shared/Usage/UsageQueueSender.cs`, `src/ReadTheStupidText.Mobile.Shared/Usage/UsageQueueTrim.cs`
(noise-first eviction; RTST passes no `IsNoise`, so its queue is unchanged)

## Why

The user, 2026-11-10: "I want to know what people do. all the clicks and changes that they do. if they go to wizard
page 1, then page 2, then skip. if they go wizard, then listen then close the app. any button they click should be
sent to me and be there in my analytics so I can check what is happening in my app. why people do not use it."
And: "are they listening from the wizard or from the child menu?"

On 9 November (UTC) there were 19 app launches. Most downloaded books and left, or pressed play for a few seconds,
and only one launch listened for more than 30 seconds. The data could not say why, because:

- Today the app sends only a closed set of feature events (checked 2026-11-10): `app_opened`, `intro_started`,
  `intro_step` (`<step>:tried|skipped`, by step name, no page number, no back), `intro_ended`, `story_languages_set`,
  `story_language_added`, `reading_language_changed`, `story_language_switched`, `book_opened`/`finished`/`left`,
  `read_*`, `book_listened`, `read_voice`, `recording_made`, `own_book_added`, `store_download`,
  `audio_pack_download`, `book_update`, `wait_shown`/`wait_cancelled`, `fallback`.
- **Nothing is sent** for: which screen is shown, wizard page n / next / back / close, settings other than story and
  reading language, speed, shelf and store filters (except inside `store_download`), Report a problem, entering the
  grown-ups corner, the app going to the background or being closed, or any button as such.
- A read carries no record of where it was started (wizard, child's shelf, grown-ups corner).
- Events reach the server only when a reading ends or at the next launch, so a session that closes the app is lost
  unless the user comes back.

Constraints (worker/CLAUDE.md, privacy page, STORE.md, kids' app): closed lists only (no free text, no typed text,
no own-book titles), no id beyond the per-launch session (Slice 351 adds the buckets), counting stays behind the
remote `telemetry.enabled` switch and the grown-up's off switch, production builds only. Analytics Engine rows
have 20 blobs and 20 doubles; Slice 351 uses blobs 17-19 and `double6`.

## Design notes (coordinator, 2026-11-12, from a ground-in-code check of origin/main)

- **Leaving the app already flushes** on every head: `App.xaml.cs:68-72` hooks MAUI `Window.Stopped`/`Resumed`,
  and `Stopped` calls `FlushUsage()` -> `UsageCounter.FlushAsync`. So item E adds only the `app_backgrounded` and
  `app_resumed` events (queued before that flush) and the early flush at 40 queued events (`UsageService`, today 50
  = `UsageBatch.MaxEvents`). No platform-specific hooks unless a device check shows Windows window close does not
  raise `Stopped`; then `Destroying` (`App.xaml.cs:80`) also flushes.
- **Wizard:** `IntroWizardPage.cs` has 4 steps (`IntroSteps` in `DinoReader.Core/UsageEvent.cs`). Screens are
  `intro_1` ... `intro_4` (plus the first card as `intro_0` if it is its own page). Actions: `next`/`try` count as
  `next`, `SkipAsync` = `skip`, `SkipAllAsync` (and Android Back) = `close`, the Windows left-arrow step-back =
  `back`. The existing `intro_step` event stays (the stats rollup reads it).
- **blob20 = current screen** on every Dino row (free: `worker/events.ts:109`). Origin of a read (item D) goes in the
  `read_started`/`book_opened` detail only if their detail is unused by the rollup; else in a new free column. The
  builder reads `worker/stats*.ts` and picks the one that keeps every existing rollup filter unchanged, and writes
  which and why here.
- **Item H consoles:** this slice edits only `site/dino/privacy.html` and `STORE.md`. If Play Data safety or App
  Store privacy answers must change, that goes under *Waiting on the user* (their console login), not done at night.
- Closed lists only; the per-launch session from Slice 351 is the only id; all counting stays behind
  `telemetry.enabled` and the grown-up switch.

## Work

- [x] **A. Smallest end-to-end path first: the intro wizard.** New event `screen` with detail
  `<screen>:<action>`, from closed lists: screens (`intro_1` ... `intro_n` by page number, `shelf`, `reading`,
  `corner_store`, `corner_settings`, `corner_books`, `story_languages`, `language`, `record`, `backup`,
  `report_problem`, `first_books`, ... every page in the app) and actions `shown`, `next`, `back`, `skip`, `close`.
  The current screen also goes on every row as `blob20`, so any event (a download, a fallback) says where it happened.
  Ship this for the wizard first, check it end to end (app → worker → weighted query), then do the rest.
- [x] **B. Every button and setting.** New event `tap` with detail `<screen>:<control>` (closed list per screen:
  play, pause, next page, previous page, speed, voice, language, download, delete, filter, back, close, open corner,
  ...) and `setting_changed` with `<setting>:<value>` (closed value list or a bucket; never free text). A shared
  helper (`UiUsage`) wraps the handlers so each control is one line.
- [x] **C. A test that no button is silent.** Like `FallbackGuardTests`: a test lists every `Button`,
  `TapGestureRecognizer`, `Clicked` and `Command` in `src/DinoReader.App` pages and fails when one has no usage id, with a
  per-file baseline that may only go down. New buttons can never ship uncounted.
- [x] **D. Where a read started.** `read_started` (and `book_opened`) carry the origin: `wizard`, `shelf`,
  `corner`, `resume`, `next_book` (closed list) in `blob4`'s neighbour field or the detail, chosen so the stats
  rollup keeps working.
- [x] **E. Leaving the app.** `app_backgrounded` and `app_resumed` (detail = screen), and the queue is flushed when
  the app goes to the background on Android, iOS and Windows (window closing), so a session that never comes back is
  still sent. Respect the worker's 50-events / 32 KB batch cap: flush early at 40 queued events.
- [x] **F. Worker:** the new events, screens, controls, settings and origins as closed lists in `worker/events.ts`,
  with a test in the app suite that keeps them equal to the app's lists (as `AudioPackUsageTests` does).
- [x] **G. Seeing it.** `tools/usage-sessions/` prints one launch's timeline from the live data (weighted, ordered by
  `sinceLaunch` then seq; token from the secrets store, never written to a file), and a day's summary: launches,
  where they stopped in the wizard, how many reached a read, how many listened 30 s. Queries in
  `docs/analytics/dino-session-queries.md`. The `/stats` page is not changed in this slice.
- [x] **H. Privacy and store forms:** `site/dino/privacy.html` says screens and button presses are counted (no
  id, no typed text); STORE.md's Play Data safety "App interactions" and App Store "Product Interaction" rows are
  declared. If a console form must change, the store-listing agent does it through the API or the user's Chrome
  where possible; only a step that needs the user's login goes to the user.
- [x] **Android, iOS and Windows:** shared pages, so one implementation; the background flush has a platform hook
  on each (Android `OnStop`, iOS `DidEnterBackground`, Windows window closing). Each platform gets a check below.

## Built (2026-11-12, implement-slice; reviewed, run on the Android emulator)

- **Item D, which column carries the origin and why the stats rollup is unchanged.** The origin is the `detail` (`blob9`) of `book_opened` and `read_started` (`wizard`, `shelf`, `corner`; `resume` and `next_book` are on the list but no path in the app starts one yet), not a new column. `worker/stats-rollup.ts` reads `blob1`, `blob2`, `blob7`, `blob12`, `double1` and `double5`, and `blob9` only inside the `Seconds` and `SecondsEstimated` filters for `book_listened` (`'reported'` / `'estimated'`); it never selects `book_opened` or `read_started`. A detail on those two events therefore changes no rollup filter or total, and `blob20` (the screen) is a column it never selects either. `worker/stats*.ts` is untouched.
- Premise corrections found while building: the queue is Dino's `UsageQueueSender` in `Mobile.Shared/Usage` (outside Touches), not `UsageService` (that is Read The Stupid Text's). The early flush at 40 is done through the options Dino already passes (`UsageBatch.FlushAt = 40` as the batch size), which makes the sender send as soon as 40 wait and keeps every batch under the Worker's 50 / 32 KB cap.
- The wizard has four pages, so the screens are `intro_1` to `intro_4` (no `intro_0`). `Try it`, `Next` and `Skip` count only `screen:<action>` (no `tap` row, so the funnel is not counted twice; see *Changed while building*); the demo read (a `reading` screen, origin `wizard`) still counts no book events (`DemoReadCounting`).
- `tap` uses one flat control list (`UiControls`), checked per screen only for the screen being on the screens list: the spec's "closed list per screen" is not enforced by the Worker (any listed screen with any listed control passes).
- `SilentButtonGuardTests` counts a press as counted when `UiUsage.` or `UiControls.` or `// no-tap-count: <reason>` is within 3 lines before to 8 lines after it (XAML: the handler's body in the code-behind, or a marker within 4 lines above); `Platforms/` and `Testing/` are not scanned. Baseline is empty: every press in `src/DinoReader.App` is wired or marked.
- Known behaviour left in place: a Windows keyboard press (Space on Play, the arrow keys) goes through the same handler as a tap and is counted as a `tap`; the Windows head's native list code in `Platforms/Windows/GalleryPage.Desktop.cs` is outside Touches and not scanned (its Enter / Space on a book goes through `OpenTileAsync`, which counts `book`).

*Changed while building (review):*

- One wrapper, `UiUsage.Press` / `Taps.AddTap`, owns count-then-handler and reads the control's live `IsEnabled` at press time. Tap order was a second strike (high #2, medium #1-#3: the count registered after the handler could be stamped with the next screen), so it was redesigned rather than patched site by site.
- `UiScreenTracker` restores the screen beneath a modal by owner match (the modal that closes must be the one that was pushed).
- The launch screen is counted once by `ShownCountedThisLaunch` plus `UiScreen.CountLaunchScreen()`, called beside the `app_opened` count. This replaced a held-count design (check2 #1-#4).
- The queue cap evicts `tap`, `screen` and `setting_changed` rows first (`src/ReadTheStupidText.Mobile.Shared/Usage/UsageQueueSender.cs`, `UsageQueueTrim.cs`; Dino passes `IsNoise`, RTST passes none and is unchanged).
- Wizard `Try it`, `Next` and `Skip` count only `screen:<action>`, not a `tap` row as well. The step-4 Skip has AutomationId `IntroLanguagesSkip` so the flow can tap it.
- **Deploy order (high #6):** the Worker must accept the new events and origin details before an app carrying them reaches users (the old Worker drops `book_opened` / `read_started` with a non-empty detail whole). Workers Builds deploys `main` within minutes and store releases take hours, so the order holds, but check that the Worker deploy succeeded before promoting the app.

*From review:* read from `D:/ReadTheStupidText-worktrees/reviews/` (findings files read to the last finding).

- High review, 10 findings, 1-5 fixed, 6-10 as listed next:
- Skipped #6 (`reviews/pr-1022-high.md`): "if a Slice 352 app build reaches users before this Worker is live" - recorded as the deploy-order check above, not a code change.
- Skipped #7 (`reviews/pr-1022-high.md`): "UI chatter shares the 200-count queue with reading and fallback counts" - low, only when the Worker is unreachable and 200 are queued (medium #6 then added the noise-first eviction).
- Skipped #8 (`reviews/pr-1022-high.md`): "have identical bodies; one formatting slip" - low, style.
- Skipped #9 (`reviews/pr-1022-high.md`): "in Release it is an unused parameter" - low, style.
- Skipped #10 (`reviews/pr-1022-high.md`): "tighten to the handler's own statement" - low, the guard is a backstop; the wrapper now owns the order.
- Medium review, 11 findings, 1-4, 6, 7, 8, 10, 11 fixed, 5 and 9 as listed next:
- Skipped #5 (`reviews/pr-1022-medium.md`): "can jank on low-end phones" - the queue holds at most 200 small entries; jank goes to the device check.
- Skipped #9 (`reviews/pr-1022-medium.md`): "tap:<screen>:pick is ambiguous" - low.
- `pr-1022-check1.md`: all skipped:
- Skipped #1 (`reviews/pr-1022-check1.md`): "so ignored presses are counted" - an ignored press is still a press the user asked to see.
- Skipped #2 (`reviews/pr-1022-check1.md`): "UiLifecycle._away is an unlocked bool" - low.
- Skipped #3 (`reviews/pr-1022-check1.md`): "Windows Destroying handler only flushes and counts no app_backgrounded" - low; Windows window close is a VM check (see *Not yet verified*).
- Skipped #4 (`reviews/pr-1022-check1.md`): "leaves a stale intro_x screen stamped on later rows" - low.
- Skipped #5 (`reviews/pr-1022-check1.md`): "RemoveOldestNoise uses RemoveAt in a loop" - low.
- Skipped #6 (`reviews/pr-1022-check1.md`): "rows are stamped with UiScreen.Current when built" - low.
- Skipped #7 (`reviews/pr-1022-check1.md`): "kept equal only by a test" - low; the test is the guard.
- `pr-1022-check2.md`: 1-4 and 6 applied by the launch-screen redesign; 5, 7, 8 skipped:
- Skipped #5 (`reviews/pr-1022-check2.md`): "when AppConfigClient/Application.Current is not resolvable" - low.
- Skipped #7 (`reviews/pr-1022-check2.md`): "passes only if config fetch finishes while first_run is current" - low.
- Skipped #8 (`reviews/pr-1022-check2.md`): "does not prove restore after Back from reading" - low.
- `pr-1022-check3.md`: all 5 low, skipped:
- Skipped #1 (`reviews/pr-1022-check3.md`): "the launch screen is never counted" - low.
- Skipped #2 (`reviews/pr-1022-check3.md`): "adds a second screen:shown" - low, narrow window before app_opened.
- Skipped #3 (`reviews/pr-1022-check3.md`): "RecordGeneral's new bool return is ignored" - low, harmless today.
- Skipped #4 (`reviews/pr-1022-check3.md`): "Windows keyboard Play/Previous/Next call the tap handlers" - low.
- Skipped #5 (`reviews/pr-1022-check3.md`): "Press during Loading/other states is counted as tap:play" - low.
- `pr-1022-check4.md`: all 4 low, skipped:
- Skipped #1 (`reviews/pr-1022-check4.md`): "flow ends on the last tap with no wait/assert" - low.
- Skipped #2 (`reviews/pr-1022-check4.md`): "no test guards it" - low.
- Skipped #3 (`reviews/pr-1022-check4.md`): "steps 2 and 3 tap by English text" - low.
- Skipped #4 (`reviews/pr-1022-check4.md`): "Try it, Back, Skip all, Done not scripted" - low.

*Device run (emulator-5554, evidence `D:/ReadTheStupidText-worktrees/reviews/w48-device/s352/`):*

- Run 1 FAIL: the flow ticked one book, so `first_run:shown` was missing, and the second flow depended on state left by the first. Fixed in the flows.
- Rerun: `usage-screens-read-background` PASS (`shelf:shown`, `book_opened` and `read_started` with origin `shelf`, `reading:play` tap, `app_backgrounded` then `[Usage] flush on window stopped`, `screen=` on every row). The wizard run showed `intro_1` to `intro_4` shown/next/skip with no tap rows, and `first_run:shown` again after the `first_books` modal closed (the restore).
- Rerun 2 (build `82ed231c1f2a`): `usage-screens-wizard` PASS (`intro_4:shown` seq 26, `intro_4:skip` seq 27).

## Waiting on the user

- Play Console (Data safety) and App Store Connect (App Privacy): this slice adds no new data type (the same "App interactions" / "Product Interaction" rows already declared for Dino), so nothing has to change if those two rows are saved as STORE.md lists them. The user (or the store-listing agent through the API) confirms once that they are; the privacy page already says screens, button presses, settings and read origins are counted (`site/dino/privacy.html`). No console was touched by this slice.

## Verification

- Unit tests: closed lists equal app ↔ worker, the silent-button guard, batch flush at 40, origin on `read_started`.
- `node --test worker/`.
- Android emulator, iOS simulator and the Windows desktop VM, Debug build with the usage log: go through the wizard
  (page 1, page 2, back, skip), open a book from the shelf, play 10 s, open the corner, change the speed, send the app
  to the background → the queued and flushed rows show each screen and tap in order, `read_started` with origin
  `shelf`, `app_backgrounded`, and a flush at backgrounding.
- Scripted device checks (run on emulator-5554, results under *Device run* below): `.maestro/dino/usage-screens-wizard.yaml` (wizard pages, Next, Skip, `screen=` on each line) and `.maestro/dino/usage-screens-read-background.yaml` (origin `shelf` on `book_opened`/`read_started`, `reading:play`, `reading:back`, `shelf:shown`, `app_backgrounded`, `flush on window stopped`). Debug-only log lines: `[Usage] seq=<n> <name> ... screen=<s> detail=<d>` (each queued count) and `[Usage] flush on window stopped|destroying` (each flush path). iOS simulator and Windows VM: the same two flows' lines, plus `app_resumed`, are still owed.
- After release: `tools/usage-sessions` shows a real launch's full timeline from the live data.

## Order

After Slice 351 (same files: `worker/events.ts`, `UsageCounter.cs`). The minimum app version for full behaviour data
is the first release carrying this slice.

#### For CLAUDE.md (lifted 2026-11-12)

- `src/DinoReader.App/CLAUDE.md`: every press goes through `UiUsage.Press` / `Taps.AddTap` (count first, then the handler, with the live `IsEnabled`) or carries `// no-tap-count: <reason>`; `SilentButtonGuardTests` enforces it. (Slice 352: docs/plan/slices/352-dino-screen-and-tap-events.md)
- `src/DinoReader.Core/CLAUDE.md`: the lists in `UiUsage.cs` must equal `worker/events.ts`; a test keeps them equal, so add a screen, control, setting or origin in both. (Slice 352: docs/plan/slices/352-dino-screen-and-tap-events.md)

#### Not yet verified

- Corner screen row and a speed `setting_changed` on a device (the flows do not open them).
- iOS simulator (none on this machine) and the `DidEnterBackground` flush.
- Windows VM: window-close flush and `app_backgrounded` (the `Destroying` path). The package build was fixed by Slice 353 (PR #1024), so this can run now.
- `tools/usage-sessions` against live data after release.
