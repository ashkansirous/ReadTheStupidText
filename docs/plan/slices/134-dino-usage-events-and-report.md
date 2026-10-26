# Slice 134 — Dino: the rest of the usage events, and a report

*Built 2026-10-26 (PR #368), not yet verified live: telemetry is not switched on, so no event has reached real data.*

**Batch:** 28 (Dino usage insights, Decisions 138-142)
**Touches:** `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastFlavourMenu.cs`, `src/DinoReader.App/NarratorPage.cs`, `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.App/App.xaml.cs`, `worker/events.ts`, `worker/*.test.ts`, `tools/dino-usage-report.ps1` (new), `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

Slice 133 proves the pipe with the reader events. This adds the rest of the user's questions.

## Work

- [x] Events (fields per Decision 140, no ids, no typed text):
  - `store_download`: story id, story language, filters in use (age range set, author set).
  - `own_book_added`: kind (photo, PDF, scan, typed).
  - `recording_made`: book kind (store/starter/own), page count recorded as a bucket.
  - `voice_changed`: what changed (character voice, flavour, narrator), language.
  - `reading_language_changed`: from, to.
  - `app_opened`: version, platform, UI language.
- [x] `tools/dino-usage-report.ps1`: Analytics Engine SQL API queries for the user's questions (top
      books per language, finished vs opened, store downloads and filter use, share of sessions with
      own books, recordings, voice changes); the API token comes from the secrets store
      (`manage-secrets`). Suppress any row with fewer than 10 events so a rare language cannot single
      out a family.
- [x] Update the privacy texts' list if an event was added that Slice 133's text did not name.

## Verification

- Unit tests for each event's fields; emulator run of each action, then the report shows each count.

*Changed while building:*

- Events are the closed set `store_download`, `recording_made` (first take of a page only; shelf/own split via `StarterStories.IsCatalogueId`), `voice_changed` (narrator in `NarratorPage`; character voice and/or flavour in `CastPage`, only when the save was kept and something changed), `reading_language_changed` (once, on leaving `LanguagePage`, only if changed; closed list of the 14 reading languages, the worker list kept in sync by a test), `own_book_added` (once per wizard session after a successful save; kind scan/photo/pdf/typed) and `app_opened` (once per process).
- Each event has a closed `Detail` field (`UsageDetail`; worker `EVENT_RULES`; blob9). `UsageCounter` never throws, keeps one flush in flight, and flushes on launch, on window stop and when the queue reaches a batch.
- `tools/dino-usage-report.ps1` runs nine Analytics Engine SQL queries and hides groups under 10 events. That is a noise floor, not anonymity (there are no ids by design). `-DryRun` prints the SQL. Not run against real data (no binding yet).
- Privacy: `site/dino/privacy.html` and `STORE.md` name every event and field; the IP may be seen by the server and is not stored with counts.
- Files outside Touches (approved): `UsageCounter.cs`, `PagesStep`/`TextStep`/`StoryWizardPage`, `site/dino/privacy.html`, `STORE.md`.
- Not wired: `own_book_added` for backup restore and `.dino` import.
- Tests: worker 73, Dino 1180, Android build.

#### For CLAUDE.md

- Dino usage events are a closed set: a new event needs an entry in worker `EVENT_RULES`, a `UsageDetail` value, and a line in `site/dino/privacy.html` and `STORE.md` naming it and its fields. No ids and no typed text. `UsageCounter` must never throw. The reading-language list in the worker is kept in sync with the app's by a test. (Slice 134: docs/plan/slices/134-dino-usage-events-and-report.md)
- `tools/dino-usage-report.ps1` hides groups under 10 events; that is a noise floor, not anonymity. (Slice 134: docs/plan/slices/134-dino-usage-events-and-report.md)

#### Not yet verified

- Everything live waits on switching telemetry on (human: enable Analytics Engine, add the binding, set `telemetry.enabled`), then running `tools/dino-usage-report.ps1`.
- Emulator smoke of each event.
- Play Data safety and App Store privacy answers updated for the new events (human, when the sending release ships).
- `own_book_added` is not wired for backup restore or `.dino` import.
