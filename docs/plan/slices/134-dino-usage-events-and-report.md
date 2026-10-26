# Slice 134 — Dino: the rest of the usage events, and a report

*In progress (wave 2026-10-26 C), draft PR.*

**Batch:** 28 (Dino usage insights, Decisions 138-142)
**Touches:** `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastFlavourMenu.cs`, `src/DinoReader.App/NarratorPage.cs`, `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.App/App.xaml.cs`, `worker/events.ts`, `worker/*.test.ts`, `tools/dino-usage-report.ps1` (new), `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

Slice 133 proves the pipe with the reader events. This adds the rest of the user's questions.

## Work

- [ ] Events (fields per Decision 140, no ids, no typed text):
  - `store_download`: story id, story language, filters in use (age range set, author set).
  - `own_book_added`: kind (photo, PDF, scan, typed).
  - `recording_made`: book kind (store/starter/own), page count recorded as a bucket.
  - `voice_changed`: what changed (character voice, flavour, narrator), language.
  - `reading_language_changed`: from, to.
  - `app_opened`: version, platform, UI language.
- [ ] `tools/dino-usage-report.ps1`: Analytics Engine SQL API queries for the user's questions (top
      books per language, finished vs opened, store downloads and filter use, share of sessions with
      own books, recordings, voice changes); the API token comes from the secrets store
      (`manage-secrets`). Suppress any row with fewer than 10 events so a rare language cannot single
      out a family.
- [ ] Update the privacy texts' list if an event was added that Slice 133's text did not name.

## Verification

- Unit tests for each event's fields; emulator run of each action, then the report shows each count.
