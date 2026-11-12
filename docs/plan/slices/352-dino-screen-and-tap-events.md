# Slice 352 — Dino: every screen, wizard step and button press is counted

*In progress (wave 2026-11-12), draft PR.*

**Touches:** `worker/events.ts`, `worker/events*.test.mjs`, `worker/CLAUDE.md`, `src/DinoReader.App/UsageCounter.cs`,
`src/DinoReader.App/Usage*.cs`, `src/DinoReader.App/UiUsage*.cs`, `src/DinoReader.App/*Page*.cs`,
`src/DinoReader.App/*Page.xaml`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/CornerShell.cs`,
`src/DinoReader.App/CornerTabs.cs`, `src/DinoReader.App/ReadingSession.cs`,
`src/ReadTheStupidText.Application/Usage/**`, `tests/DinoReader.Tests/**`, `site/dino/privacy.html`, `STORE.md`,
`docs/analytics/dino-session-queries.md`, `tools/usage-sessions/**` (new),
`docs/plan/slices/352-dino-screen-and-tap-events.md`,
`src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.Core/*Usage*.cs`, `src/DinoReader.App/*.cs` (Dino pages and rows not named *Page*: `OnboardingShell.cs`, `RecordIntroPage.cs`, `SettingsRows.cs`, `FirstBooksPage.cs`, `IntroState.cs`, ...), `worker/stats*.ts`, `worker/*.test.mjs`,
`tests/ReadTheStupidText.Tests/UsageService*.cs`, `.maestro/dino/**`, `tools/emulator/**`

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

- [ ] **A. Smallest end-to-end path first: the intro wizard.** New event `screen` with detail
  `<screen>:<action>`, from closed lists: screens (`intro_1` ... `intro_n` by page number, `shelf`, `reading`,
  `corner_store`, `corner_settings`, `corner_books`, `story_languages`, `language`, `record`, `backup`,
  `report_problem`, `first_books`, ... every page in the app) and actions `shown`, `next`, `back`, `skip`, `close`.
  The current screen also goes on every row as `blob20`, so any event (a download, a fallback) says where it happened.
  Ship this for the wizard first, check it end to end (app → worker → weighted query), then do the rest.
- [ ] **B. Every button and setting.** New event `tap` with detail `<screen>:<control>` (closed list per screen:
  play, pause, next page, previous page, speed, voice, language, download, delete, filter, back, close, open corner,
  ...) and `setting_changed` with `<setting>:<value>` (closed value list or a bucket; never free text). A shared
  helper (`UiUsage`) wraps the handlers so each control is one line.
- [ ] **C. A test that no button is silent.** Like `FallbackGuardTests`: a test lists every `Button`,
  `TapGestureRecognizer`, `Clicked` and `Command` in `src/DinoReader.App` pages and fails when one has no usage id, with a
  per-file baseline that may only go down. New buttons can never ship uncounted.
- [ ] **D. Where a read started.** `read_started` (and `book_opened`) carry the origin: `wizard`, `shelf`,
  `corner`, `resume`, `next_book` (closed list) in `blob4`'s neighbour field or the detail, chosen so the stats
  rollup keeps working.
- [ ] **E. Leaving the app.** `app_backgrounded` and `app_resumed` (detail = screen), and the queue is flushed when
  the app goes to the background on Android, iOS and Windows (window closing), so a session that never comes back is
  still sent. Respect the worker's 50-events / 32 KB batch cap: flush early at 40 queued events.
- [ ] **F. Worker:** the new events, screens, controls, settings and origins as closed lists in `worker/events.ts`,
  with a test in the app suite that keeps them equal to the app's lists (as `AudioPackUsageTests` does).
- [ ] **G. Seeing it.** `tools/usage-sessions/` prints one launch's timeline from the live data (weighted, ordered by
  `sinceLaunch` then seq; token from the secrets store, never written to a file), and a day's summary: launches,
  where they stopped in the wizard, how many reached a read, how many listened 30 s. Queries in
  `docs/analytics/dino-session-queries.md`. The `/stats` page is not changed in this slice.
- [ ] **H. Privacy and store forms:** `site/dino/privacy.html` says screens and button presses are counted (no
  id, no typed text); STORE.md's Play Data safety "App interactions" and App Store "Product Interaction" rows are
  declared. If a console form must change, the store-listing agent does it through the API or the user's Chrome
  where possible; only a step that needs the user's login goes to the user.
- [ ] **Android, iOS and Windows:** shared pages, so one implementation; the background flush has a platform hook
  on each (Android `OnStop`, iOS `DidEnterBackground`, Windows window closing). Each platform gets a check below.

## Verification

- Unit tests: closed lists equal app ↔ worker, the silent-button guard, batch flush at 40, origin on `read_started`.
- `node --test worker/`.
- Android emulator, iOS simulator and the Windows desktop VM, Debug build with the usage log: go through the wizard
  (page 1, page 2, back, skip), open a book from the shelf, play 10 s, open the corner, change the speed, send the app
  to the background → the queued and flushed rows show each screen and tap in order, `read_started` with origin
  `shelf`, `app_backgrounded`, and a flush at backgrounding.
- After release: `tools/usage-sessions` shows a real launch's full timeline from the live data.

## Order

After Slice 351 (same files: `worker/events.ts`, `UsageCounter.cs`). The minimum app version for full behaviour data
is the first release carrying this slice.
