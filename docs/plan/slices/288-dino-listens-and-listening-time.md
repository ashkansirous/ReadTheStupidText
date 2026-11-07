# Slice 288 — Dino: the app counts listens and real listening time

*Planned 2026-11-07.* Dino Reader only. Android, iOS and the Windows head the same, in the same slice (the code is the
shared Core and App, so one change covers all three; verification runs on Android and iOS).

**Depends on:** Slice 250 (playback events and the session id). Feeds Slice 287 (the dashboard).
**Touches:** `src/DinoReader.Core/UsageEvent.cs` (new name `book_listened`, a `Seconds` field),
`src/DinoReader.App/{ReadingSession,UsageCounter}.cs`, `src/DinoReader.App/ListenTally.cs` (new: played-seconds counter),
`worker/events.ts` (rule for `book_listened`, optional `seconds` on `read_paused`/`read_stopped`/`book_listened`, written to
`double5`), `worker/events.test.mjs`, `tests/DinoReader.Tests/**`, `site/dino/privacy.html`, `PRIVACY.md`, `STORE.md`,
`worker/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, this file.

## Why

The user wants listens per book and in total, and hours listened per book and in total, on the dashboard (Slice 287).
Code check (origin/main, 2026-11-07): the app sends `book_opened`, `read_started/paused/resumed/stopped`, `book_left` and
`book_finished` (`src/DinoReader.Core/UsageEvent.cs:10-48`) but no duration, and the row timestamp is the batch send
time, so listening time cannot be worked out from what is stored.

## Decisions (user, 2026-11-07)

- A **listen** is a book played for at least 30 seconds; playing it again later counts again.
- **Hours** are real played seconds (audio actually playing, not paused, not the time the book is open), counted from this
  release on. Older data has no hours.

## Design (planning defaults)

- `ListenTally` adds up seconds while audio plays, per reading of a book (from a counted `read_started` to its
  `read_stopped`). A restart from page one, or a new opening of the book, is a new reading.
- `book_listened` (book, language, source, no detail) is sent **once per reading, the moment played time reaches 30 s**,
  so an app killed later still counts the listen.
- `read_paused` and `read_stopped` carry `seconds`: whole seconds played since the last start or resume (0-86400; above is
  dropped from the row, not the event). Hours = `SUM(double1 * double5)` over those two events. Paused-then-killed loses
  at most the seconds after the last resume, never more.
- Worker: `book_listened` in `EVENT_RULES` (sources shelf/own, no detail); `seconds` validated as an integer and stored
  in `double5` (-1 when absent). Analytics Engine allows 20 doubles; 4 are used today.
- Privacy: a number of seconds per reading is usage data like the existing playback events, no id; the privacy page and
  `STORE.md` get one line each. Check whether the Play Data safety and App Store privacy answers already cover "app
  interactions"; if a form needs changing, that is a user step written into *Outstanding work*.

#### Design (Dino Reader Stats Dashboard mock, frame 200, added 2026-11-07)

No app UI. The dashboard (Slice 287) shows the numbers: "Listens" with the sub "Played for 30 s or more", "Hours listened" with "Real played time from the app", and a note "Listens and hours count from Dino 1.112, 10 Nov 2026 (Slice 288)." The version and date are placeholders; record this slice's real release version and date here when it ships so Slice 287's note can use them. Nothing here changes: the 30 second rule and `double5` seconds match the mock. The dashboard splits iOS into iPhone and iPad from the batch's `blob12` form factor (Slice 166), already sent, so the app sends nothing new for it.

## Work

- [ ] 1. Core: `book_listened` name, `Seconds` on the event; tests (`UsageEventTests`).
- [ ] 2. Worker: rule and `double5`; `events.test.mjs` for valid, missing and out-of-range seconds.
- [ ] 3. App: `ListenTally` hooked into `ReadingSession.Playback` (start, pause, resume, stop, restart, failed); send
      `book_listened` at 30 s and `seconds` on pause and stop. Unit tests for the tally (pause/resume, restart, under 30 s,
      exactly 30 s, a second reading).
- [ ] 4. Privacy page, `PRIVACY.md`, `STORE.md` lines; worker and app `CLAUDE.md` rules (the `double5` meaning per event).
- [ ] 5. Scripted device flow: play a book 35 s, pause, resume 10 s, stop; the queued counts hold one `book_listened`
      and seconds 35 + 10 (read through the test hook, the off switch keeps them from being sent).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`, `node --test worker/events.test.mjs`.
- Android emulator and iOS simulator: the scripted flow in item 5 gives the same queued counts on both.
