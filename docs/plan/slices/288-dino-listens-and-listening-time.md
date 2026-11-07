# Slice 288 — Dino: the app counts listens and real listening time

*Built 2026-11-07 (wave 2026-11-07, PR #901), not merged. Fast tests only; the emulator run was blocked (see Not yet verified).* *Planned 2026-11-07.* Dino Reader only. Android, iOS and the Windows head the same, in the same slice (the code is the
shared Core and App, so one change covers all three; verification runs on Android and iOS).

**Depends on:** Slice 250 (playback events and the session id). Feeds Slice 287 (the dashboard).
**Touches:** `src/DinoReader.Core/UsageEvent.cs` (new name `book_listened`, a `Seconds` field),
`src/DinoReader.App/{ReadingSession,UsageCounter}.cs`, `src/DinoReader.App/ListenTally.cs` (new: played-seconds counter),
`worker/events.ts` (rule for `book_listened`, optional `seconds` on `read_paused`/`read_stopped`/`book_listened`, written to
`double5`), `worker/events.test.mjs`, `tests/DinoReader.Tests/**`, `site/dino/privacy.html`, `PRIVACY.md`, `STORE.md`,
`worker/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/ReadingPage.xaml.cs` (one handler line),
`.maestro/dino/usage-listen-seconds.yaml`, `tests/DinoReader.Tests/DinoReader.Tests.csproj`, this file.

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
- Every `read_paused` and `read_stopped` row always carries `seconds` (0 allowed, also when no tally is open): the whole seconds
  heard since the previous paused/stopped row of this reading (0-86400; above is dropped from the row, not the event).
  `book_listened` carries **no** seconds (so nothing is counted twice; the Worker ignores one if sent). Absent only on rows of
  other kinds. One reading is one story: a `read_started` for a different story ends the open tally first (its unsent seconds
  are dropped); in the app this cannot happen without a `read_stopped`, because leaving the page always stops the read
  first (`ReadingPage.cs` `LeavePage` -> `ReadingSession.Stop`, which closes the counted read), so it is only a guard. When the
  page unsubscribes the counter's audible state becomes false. The tally's audible flag follows the session's real audio state alone (a usage stop never sets it; the page reads the
  current state once on subscribe), and starting a read never resets an open tally: it ends at stop or leave. Hours =
  `SUM(double1 * double5)` over `read_paused`/`read_stopped` rows only, so -1 never reaches it. Paused-then-killed loses
  at most the seconds after the last resume, never more.
- Worker: `book_listened` in `EVENT_RULES` (sources shelf/own, no detail); `seconds` validated as an integer and stored
  in `double5` (-1 when absent). Analytics Engine allows 20 doubles; 4 are used today.
- Privacy: a number of seconds per reading is usage data like the existing playback events, no id; the privacy page and
  `STORE.md` get one line each. Check whether the Play Data safety and App Store privacy answers already cover "app
  interactions"; if a form needs changing, that is a user step written into *Outstanding work*.

#### Design (Dino Reader Stats Dashboard mock, frame 200, added 2026-11-07)

No app UI. The dashboard (Slice 287) shows the numbers: "Listens" with the sub "Played for 30 s or more", "Hours listened" with "Real played time from the app", and a note "Listens and hours count from Dino 1.112, 10 Nov 2026 (Slice 288)." The note's wording is the mock's; its version and date are filled with this slice's real release, so record the version and date here when it ships (Slice 287's `/api/stats` returns them). Nothing here changes: the 30 second rule and `double5` seconds match the mock. The dashboard splits iOS into iPhone and iPad from the batch's `blob12` form factor (Slice 166), already sent, so the app sends nothing new for it.

## Work

- [x] 1. Core: `book_listened` name, `Seconds` on the event; tests (`UsageEventTests`).
- [x] 2. Worker: rule and `double5`; `events.test.mjs` for valid, missing and out-of-range seconds.
- [x] 3. App: `ListenTally` hooked into `ReadingSession.Playback` (start, pause, resume, stop, restart, failed); send
      `book_listened` at 30 s and `seconds` on pause and stop. Unit tests for the tally (pause/resume, restart, under 30 s,
      exactly 30 s, a second reading).
- [x] 4. Privacy page, `PRIVACY.md`, `STORE.md` lines; worker and app `CLAUDE.md` rules (the `double5` meaning per event).
- [x] 5. Scripted device flow: play a book 35 s, pause, resume 10 s, stop; the queued counts hold one `book_listened`
      and seconds 35 + 10 (read through the test hook, the off switch keeps them from being sent).

## Built (2026-11-07)

- After review (high pass): the tally runs only while audio is really heard. `ReadingSession.Audible` (one flag, `IsAudible`: a clip or recording playing, or a silent piece running on a shown page; false while loading, paused by a person or a menu, or a covered silent piece) is raised before the pause count; `ReadingPage` passes it to `UsageCounter.SetAudible`, the only thing that runs or stops `ListenTally`. The playback verbs only read it (`TakeSeconds`: milliseconds kept, rounded once per row with the remainder carried; null when no tally is open, which the row sends as `seconds=0`). A pause or stop first queues a `book_listened` that just crossed 30 s; `book_listened` is queued under the same lock as the verbs, so it keeps its place in the session order. A `System.Threading.Timer` queues it at 30 s of played time; `ListenTally` (pure, clock passed in) is linked into `DinoReader.Tests`.
- **Play Data safety / App Store privacy: no form change.** `STORE.md` (Dino section) declares App activity > App interactions / Usage Data > Product Interaction, not linked, no tracking. Played seconds per reading and a listen count are the same data type: a number per reading, no id, no time of day. `PrivacyInfo.xcprivacy` is unchanged. `site/dino/privacy.html`, `PRIVACY.md` and `STORE.md` each have the new line. No user step.
- Files outside Touches (new, no clash possible): `.maestro/dino/usage-listen-seconds.yaml` (item 5's scripted flow; the Touches line did not name a flow path) and `tests/DinoReader.Tests/DinoReader.Tests.csproj` (one `Compile Include` link for `ListenTally.cs`, covered by `tests/DinoReader.Tests/**`).
- Known behaviours: an app killed while playing loses the seconds since the last resume (the listen itself is already queued); a Worker running the old rules drops `book_listened` (new builds only talk to the deployed Worker, so deploy the Worker first).
- After review (check pass 2): `ReadingSession._recorded` and `_restartStep` are properties whose setters run `UpdateAudible` (every field `IsAudible` reads is covered, so no transition skips it); the silent beat's end also updates it; `Stop` cancels and stops the recording (audible false) before raising `read_stopped`. A second `read_started` for the open story keeps the tally and takes the current language for the listen.
- Review findings skipped: 2 (`LeavePage` stops the session before unsubscribing, so no audio plays off-stack); 5 (`TickCount64` through a suspend: low, not changed); 6 (a lost timer plus an app kill: low, not changed); 7 (rounding: the remainder is carried, so totals match either way).
- After review (check pass 3): a language-switch `read_stopped` keeps the tally open (its `read_started` for the same story follows, language refreshed); `Stop` raises `read_stopped` first and only then ends the audio; `ReadingPage.LeavePage` ends with `UsageCounter.EndReading` (a still-open reading gets its `read_stopped` left_book with the seconds, then audible false); a premade clip is marked playing before `Play` but audible only after `Play` returns (`_playStarting`); `SetAudible(true)` with no reading open (demo reads) does nothing. This supersedes the pass-2 line about `Stop` ordering.
- Check pass 3 skipped: 4 (accepted: a consumer relying on listen-before-stop order across threads is not a case today); 5 (harmless: the explicit and setter calls are idempotent); 6 (no `Playback` subscriber reads `State`).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`, `node --test worker/events.test.mjs`.
- Android emulator and iOS simulator: the scripted flow in item 5 gives the same queued counts on both.

## Release and device run (2026-11-07)

- **Release order:** the Worker deploys with the batched release; a Worker still running the old rules drops `book_listened` (the app sends it, the old rules do not know the name).
- **Device run BLOCKED:** the emulator run of 2026-11-07 built (key 3c3f66c930d3) but the install failed with `INSTALL_FAILED_INTERNAL_ERROR ... Failed to extract native libraries, res=-110`, because C: was full (0 GB free; 124.5 GB of leftover `%TEMP%\tmp*.apk`). Nothing on a device was reached.
- **Ledger of the design as built:** `ListenTally` is driven only by `ReadingSession`'s real audible state (audible after Play starts; hidden, silent, held and recording changes all go through one setter). `read_paused` and `read_stopped` always carry `seconds` (0 allowed); `book_listened` carries none and is queued once at 30 s heard (also checked at pause and stop). One reading is one story and a language switch keeps the tally. `ReadingPage.LeavePage` ends the reading through `UsageCounter.EndReading`. The Worker stores seconds in `double5`; hours = `SUM(double5)` over `read_paused`/`read_stopped` rows.
- Slice 287's note ("count from Dino <version>, <date>") still needs this slice's real release version and date; record them here when it ships.

## From review

Findings files (outside the repo, `D:/ReadTheStupidText-worktrees/reviews/`): `s288-high.md` (9 findings), `s288-medium.md` (8), `s288-check1.md` (6), `s288-check2.md` (7), `s288-check3.md` (7), `s288-check4.md` (2). Outcomes below come from the notes under *Built* and the coordinator's list of fixer outcomes.

- High pass (9): applied 1 (the 30 s check also runs at pause/stop), 2-4 (the tally is driven by the session's real audible state), 5 (milliseconds accumulated, rounded once), 6 (then superseded by check 1's rule) and 7 (superseded by medium 5). Skipped 8 (the Worker tolerates the field) and 9 (style).
- Medium pass (8): applied 1 (the page reads `AudibleNow` on subscribe), 2 (audible follows only real audio), 3 (every pause/stop carries seconds), 4 (start never resets an open tally), 5 (decide under the lock, enqueue after) and 6 (`book_listened` goes through `RecordLogged`). Skipped 7 (remainder carried). 8 answered by 3.
- Check 1 (6): applied 1 (a different story ends the tally; cannot happen in the app, `LeavePage` stops first), 3 (docs follow the rule), 4 (`book_listened` carries no seconds), 5 (unsubscribe sets audible false), 6 (log spacing). Skipped 2 (accepted: order does not change counts).
- Check 2 (7): skipped 2 (`LeavePage` stops the session before unsubscribing), 5 (`TickCount64` through a suspend, low), 6 (lost timer plus an app kill, low), 7 (rounding: the remainder is carried). Applied per the notes: 1 (second `read_started` takes the current language), 3 and 4 (setters and the silent beat's end update audible). 
- Check 3 (7): skipped 4 (a consumer relying on listen-before-stop order across threads is not a case today), 5 (explicit and setter calls are idempotent), 6 (no `Playback` subscriber reads `State`). Applied per the notes: 1 (language switch keeps the tally), 2 (`LeavePage` ends with `EndReading`), 3 (`_playStarting`), 7 (`SetAudible(true)` with no open reading does nothing).
- Check 4 (2, both low): skipped both. 1 (a throwing handler reported as ClipUnplayable): no concrete path; the reviewer believes `RecordLogged` swallows errors, not verified. 2 (a different story discarding unsent seconds): no rebind without `LeavePage`.

#### For CLAUDE.md

Already written in the slice branch (`src/DinoReader.App/CLAUDE.md` line 126, `worker/CLAUDE.md` line 19), each with its Slice 288 link; the index pass lifts nothing more. Rules they carry: the tally is driven only by `ReadingSession`'s real audible state, through one setter (`UsageCounter.SetAudible`); `read_paused`/`read_stopped` always carry `seconds` (0 allowed), `book_listened` none; hours = `SUM(double5)` over paused/stopped rows with `double5 >= 0`, always filtered on `blob1`. Deploy the Worker with the batched release: an old Worker drops `book_listened`.

#### Not yet verified

- usage-listen-seconds flow (`.maestro/dino/usage-listen-seconds.yaml`) on the Android emulator: the 2026-11-07 run was blocked at install (C: full, `INSTALL_FAILED_INTERNAL_ERROR`, res=-110).
- The same flow on the iOS simulator.
- The same flow on the Windows head.
- The release version and date of this slice, for Slice 287's dashboard note (version line not filled until it ships).
