### Slice 250 — Dino usage: playback events and a session id that changes on every cold start

*Built 2026-11-04 (night 2026-11-03-2349 wave 3), merged as PR #788 (6b9b9940).* After [Slice 249](249-dino-report-a-problem.md) (merged #781).

**Touches:** `src/DinoReader.App/Testing/DinoTestCommands.cs`, `src/ReadTheStupidText.Mobile.Shared/Crashes/CrashReporter*.cs`, `src/ReadTheStupidText.Mobile.Shared/Crashes/UserReport*.cs`, `src/DinoReader.App/ReportProblemPage.cs`, `tools/crashes/pull-crashes*.mjs`, `tools/dino-usage-report.ps1`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.Core/ReadStopReason.cs` (new), `src/DinoReader.App/TraceLines*.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.App/UsageCounter.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/UsageSession*.cs` (new), `src/ReadTheStupidText.Application/Usage/UsageBatch.cs`, `src/ReadTheStupidText.Mobile.Shared/Usage/UsageQueueSender.cs`, `src/ReadTheStupidText.Application/Crashes/CrashReport.cs`, `worker/events.ts`, `worker/crashes.ts`, `worker/*.test.mjs`, `worker/CLAUDE.md`, `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html`, `STORE.md`, `src/DinoReader.App/Platforms/iOS/Resources/PrivacyInfo.xcprivacy`, `tests/DinoReader.Tests/**`, `src/ReadTheStupidText.Application/Crashes/CrashReportJson.cs`, `src/ReadTheStupidText.Application/Crashes/CrashReportBuilder.cs`, `.maestro/dino/usage-playback-events.yaml`, `docs/plan/slices/250-dino-playback-events-and-session-id.md`

**Why.** The Slice 248 bug (iOS: leaving a book kept reading; the next book's Pause/Play drove the old one) could not be seen in the data: Dino counts only `book_opened` and `book_finished` (`UsageEvent.cs:9-27`), and rows cannot be tied together (no id of any kind, `UsageCounter.cs:10-11`, `PRIVACY.md:127-128`). With these events and a session id, that bug reads as "book left, no read stopped" followed by "read resumed" for the old book while a new one was open.

**Decisions locked with the user (2026-11-02).** "we should be able to record a sessionId. SessionIds would not be tied to a users, they'll get a new one on every cold start, so when the app is closed for a few minutes, we know that it is not the same session." So: a random id made at each cold start, kept in memory (and on disk only inside counts still waiting in the send queue), never reused for a later launch, never tied to the install, the device or a person. This replaces the "never an id" rule (`worker/CLAUDE.md:14`) for this one field; the rule is rewritten in this slice, not dropped.

**Design points added in planning (not user decisions; change them if they are wrong).**
- Each event carries its session id, so events queued in one session and sent in the next (the queue lives in Preferences, `UsageQueueSender.cs:42`) keep the session they happened in.
- Each event carries a sequence number within its session, because the stored timestamp is the send time of a batch and `day` is only the UTC date: without it the order inside a session is lost.
- Sampling is decided once per session, not per event (the remote config's sample rate), so a sampled session is complete and an unsampled one sends nothing.
- The session id also goes into crash reports and Slice 249's user reports, so a reported code leads to that session's usage rows.

**Claim design (coordinator, 2026-11-04, from a code read of origin/main).** `ReadingSession.Stop(string reason)` (`ReadingSession.cs:313`) already takes a trace string from `TraceLines.ReadingReason`; keep that trace text, and add a closed `ReadStopReason` enum (DinoReader.Core) that `Stop` takes, mapped to the trace text and to the usage Detail only at the boundary. Mapping of today's callers in `ReadingPage.xaml.cs`: LanguageChange (362) → `language_changed`; Superseded (579) → `restarted`; PageLeft (960) → `left_book`; User (1184, 1396) → `cancelled`, except a stop caused by the Windows window going away/closing → `window_stopped` (split the two at the call site; if neither is a window stop, `window_stopped` is not emitted); Skipped (1474) → `skipped` (new, added to the closed list); Other (918, `DinoTestCommands.cs:448`) → `other` (new). `finished` has no `Stop` caller: emit `read_stopped finished` where the read reaches its natural end. The Worker accepts exactly these eight reasons.

#### Work

- [x] **1. The session (Android and iOS, Dino Windows head too since it shares the code).** `UsageSession`: a random 128-bit id (hex) and a sequence counter, made once per process start, in memory only. `UsageEvent` gains `Session` and `Seq`; `UsageCounter` fills them. Sampling per session as above. RTST mobile and Windows send no session field (unchanged; the field is optional on the wire).
- [x] **2. Playback events.** New closed names in `UsageEventName` and the Worker's `EVENT_RULES` (`worker/events.ts:174-192`): `read_started`, `read_paused`, `read_resumed`, `read_stopped`, `book_left`. Each names its book (package id, "own" or "none", as today). `read_stopped`'s Detail is a closed reason, from the call sites of `ReadingSession.Stop` (`ReadingPage.xaml.cs` leave, window stop, Cancel, restart for a language change, a new read; confirm the list in code): `left_book`, `finished`, `cancelled`, `language_changed`, `restarted`, `window_stopped`, plus `skipped`, `failed` (a failed read) and `other` added in the build; every `read_started` gets exactly one `read_stopped`. The id lives in memory and is written only inside unsent queued counts (gone once sent), never reused for a later launch. `Stop()` takes the reason (enum, stringified only at the usage boundary).
- [x] **3. Worker.** `toDataPoint` (`worker/events.ts:296-306`) stores the session id in blob16 and the sequence number in double2 (check the Analytics Engine blob and double limits in Cloudflare's docs first; 15 blobs are used today). Validate the id's shape (32 hex) and the sequence (non-negative integer); drop the field, not the event, when it is malformed.
- [x] **4. Crash and user reports** carry the session id (`CrashReport.cs`, `worker/crashes.ts`), so `pull-crashes.mjs` prints it and the analytics query for that session can be run from it.
- [x] **5. Privacy text and store answers.** `PRIVACY.md:127-128` and both `site/` pages: say there is a random session id that is new every time the app starts, is held in memory and written only inside counts still waiting in the send queue, and cannot tell two sessions of one person apart. `worker/CLAUDE.md:14`: the rule now reads "no id except the per-launch session id". Check the Play Data safety and App Store privacy definitions (via `web-research`) for whether a per-launch random id counts as an identifier, and update `STORE.md` and `PrivacyInfo.xcprivacy` to match; a console change is the user's step, in Outstanding work.
- [x] **6. Reading it back.** The `analytics-query` agent's column map gains `session` and `seq`; add a short "one session in order" query to `worker/CLAUDE.md` or the query skill's notes.
- [x] **7. Check.** Unit tests: one id per process, a new one on a new `UsageSession`, sequence increments, sampling per session, every `read_stopped` call site passes its reason. Worker tests: the new names and reasons are accepted, unknown ones dropped, a malformed session id dropped from the row. Emulator flow (Android) with the local test worker: play a book, Back, open another, Play; the worker test log shows `read_started`, `book_left`, `read_stopped left_book`, `book_opened`, `read_started` in sequence under one session id. The harnesses' off switch still keeps every real run out of production.

**User, 2026-11-03:** The night may merge this slice; its Worker change goes live on merge through Workers Builds (production deploy approved). (This does not approve the store privacy form steps.)

#### Built

All seven items built on 2026-11-04. Checks: DinoReader.Tests 2401, ReadTheStupidText.Tests 2049 (2 skipped), worker 149, typecheck clean; Android, iOS and Windows head build. Emulator 2026-11-04: flow `usage-playback-events` PASS; logcat order `book_opened`, `read_started`, `book_left`, `read_stopped left_book`, `book_opened`, `read_started` under one session id.

#### Changed while building

Reasons are nine, not eight: `left_book`, `finished`, `cancelled`, `language_changed`, `restarted`, `window_stopped`, `skipped`, `other`, `failed`. `window_stopped` is accepted by the Worker but never emitted (no stop in the code is caused by the window going away). One `read_started` gets exactly one `read_stopped` on every path (`idleAfterReturn` closes with `other`). The language menu's pauses are not counted. The playback handler is subscribed on appearing, bound to the shown book, and unsubscribed on leave. Sampling is one roll per launch (test: 0.3 rate over 20000 sessions). The session id is written only inside unsent queued events. Item 4: `CrashReport`, `CrashContext` and the report JSON carry an optional `session` (null on the wire for Read The Stupid Text and for reports about an earlier launch: native, unclean exit); `CrashReporter.SessionProvider` (set by Dino in `MauiProgram`) names it for a managed crash and a user report; the Worker keeps a valid one and `pull-crashes.mjs` prints it. Touches was widened by the coordinator on 2026-11-04 to `src/ReadTheStupidText.Application/Crashes/CrashReportJson.cs`, `src/ReadTheStupidText.Application/Crashes/CrashReportBuilder.cs` and `.maestro/dino/usage-playback-events.yaml` (the Dino emulator flow; Debug-only `[Usage] <name> <detail> session=<id>` lines, written before the sampling check). `window_stopped` is accepted by the Worker but no call site emits it (no stop in the code is caused by the window going away). Item 5, **coordinator decision (2026-11-04):** a per-launch random id held only in memory is not a device-level id (Apple's Device ID means a device's advertising or other device-level id), and declaring Device ID in a Kids-category app invites review trouble, so `PrivacyInfo.xcprivacy` declares no Device ID and the App Store form gets no such row; Google's and Apple's pages do not settle the question themselves (`STORE.md`).

#### From review

`D:/ReadTheStupidText-worktrees/reviews/s250-review-high.md`:
- 1 (privacy text said "never stored on the device" though queued counts carry the id to disk): applied.
- 2 (a start that cuts a running read had no `read_stopped`): applied.
- 3 (a failed read got no `read_stopped`): applied (`failed` reason).
- 4 (language menu pause/resume counted as user pause/resume): applied.
- 5 (second roll source in `UsageCounter`): applied.
- 6 (`SessionProvider` resolved from DI inside the crash path): applied.
- 7 (low: `Seq` assigned before the queue lock, so queue order can differ from seq order): skipped; seq still restores order, mostly theoretical.
- 8 (low: `window_stopped` has no emitter): skipped, decided; kept as an accepted reason, documented.

`D:/ReadTheStupidText-worktrees/reviews/s250-review-medium.md`:
- 1 (`book_left` without a matched `book_opened`): skipped; the data is still readable.
- 2 (`Seq` before the queue lock): skipped, same as high 7.
- 3 (session id persisted inside queued counts, contradicting "never stored" wording): applied.
- 4 (`read_started` raised before knowing the start happens, spurious `read_stopped restarted` later): applied by closing the open start rather than moving it.
- 5 (low: `Failed` arm equals the default arm; mapping duplicated): skipped, style.
- 6 (low: `window_stopped` has no emitter): skipped, decided (same as high 8).
- 7 (`OnPlayback` never unsubscribed, reads `_story` at event time): applied (subscribed on appearing, unsubscribed on leave).

`D:/ReadTheStupidText-worktrees/reviews/s250-review-check-medium.md`, all skipped (low):
- 1: a startup crash before `SessionProvider` is set carries no session.
- 2: `Resume()` after a pending advance counts a resume without a counted pause.
- 3: the playback handler keeps the subscribe-time story until the page appears again.

#### For CLAUDE.md (lifted 2026-11-04)

- `src/DinoReader.App/CLAUDE.md`: `ReadingSession.Stop` takes a `ReadStopReason` (DinoReader.Core), turned into trace text and usage Detail only at the boundary; every `read_started` gets exactly one `read_stopped` on every path (a new path closes through `RaiseStopped`); the language menu's own pauses are not counted. (Slice 250: docs/plan/slices/250-dino-playback-events-and-session-id.md)
- `src/DinoReader.App/CLAUDE.md`: the session id is made once per launch, held in memory, written to disk only inside unsent queued events, never reused, never tied to an install, device or person; sampling is one roll per launch. (Slice 250: docs/plan/slices/250-dino-playback-events-and-session-id.md)
- `worker/CLAUDE.md`: the "no id" rule now reads "no id except the per-launch session id" (blob16, `seq` in double2); a malformed session or seq drops the field, not the event; the Worker accepts nine stop reasons (`window_stopped` is accepted, never emitted). (Slice 250: docs/plan/slices/250-dino-playback-events-and-session-id.md)
- `src/DinoReader.App/Platforms/iOS/Resources/PrivacyInfo.xcprivacy`: declares no Device ID for the session id (coordinator decision, reason recorded in `STORE.md`). (Slice 250: docs/plan/slices/250-dino-playback-events-and-session-id.md)

#### Not yet verified

- `seq` on a device (the Debug line has no `seq`; unit-tested only).
- The `usage-playback-events` flow shown failing on origin/main (needs a rebuild).
- iOS store build's first rows showing session ids and the new events.
- Live Worker rows after the deploy.

#### Decided 2026-11-07 (was: waiting on the user)

Store privacy form, Play Data safety (Dino). The user: "it is an id so I think we should have it as an id. but it is not personable id, so you should take that decision based on their documentation." Code (read 2026-11-07): the session id is 16 random bytes, new at each app start, in memory only (`src/DinoReader.App/UsageSession.cs:20`, `UsageCounter.cs:32`). Play's definition of Device or other IDs ("Identifiers that relate to an individual device, browser or app"; https://support.google.com/googleplay/android-developer/answer/10787469) and its in-app-event exception do not clearly cover a per-launch id, so the safe reading is to keep **Device or other IDs** declared (collected, not shared). The live form already has it ticked (checked 2026-11-06): **no change to make.** App Store Connect App Privacy (Dino): no change (no Device ID row, decision above). Text is in `STORE.md`, *Slice 250*.

**Not in this slice.** RTST events and session ids. Any id that survives a restart.

**Verification.** Unit, worker and emulator checks above. iOS: the TestFlight build sends no usage (until [Slice 251](251-dino-beta-usage-dataset.md)), so the iOS check is the store build's first rows showing session ids and the new events (Not yet verified until then).
