# Slice 267 — Dino bugs: books on the shelf keep the old voice; the first-run buttons overlap

*Built 2026-11-05, merged as PR #826 (e67f88bf); emulator-5554 checks passed, not yet tried on the user's phone.* Bug fixes, Dino Reader, Android and iOS alike. Reported by the user on the latest Android build (dino-v1.102.1, 2026-11-04 20:10 UTC): "why the hell is it still using the old shitty voice? We changed that yesterday" and "the skip and check it out are on top of each other". The user also asked why the tests passed; the answers are under each bug.

**Rule (user, 2026-11-04, decided): audio packs follow only the user's languages.** A family has a main language and up to two more (the story languages); all of them count the same. Every book they have or pick downloads its pre-made audio in exactly those languages, at book install, for books already on the shelf (starters included), when a language is added, and in the update check. A book has no language of its own for this: never add the language the book was written in (today `StoreClient.WantedPacks`, `StoreClient.cs:259-260`, adds "the book's own language", so every book also fetches English). "If a person has French and Farsi, any book he selects should always be downloaded in French and Farsi. No English." The fixes for Bugs A and C follow this rule; a unit test pins it (French + Farsi family: packs asked for are exactly `fr` and `fa`).

**Touches:** `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/AudioPackStore.cs`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/IntroWizardPage*.cs`, `src/DinoReader.App/Testing/DinoTestCommands.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/AudioPackLog.cs`, `src/DinoReader.App/ReadingPage.xaml.cs` (voice-used event only), the starter import path in `src/DinoReader.App` (Bug C, file named when found), `src/DinoReader.Core/AudioPack*.cs` (pack-due rule, new), `src/DinoReader.Core/UsageEvent*.cs`, `tests/DinoReader.Tests/**`, `.maestro/dino/**`, `tools/emulator/**` (layout check), `tools/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `worker/events.ts`, `worker/events.test.mjs`, `worker/CLAUDE.md`, `site/dino/privacy.html`, `PRIVACY.md`, `STORE.md`, `docs/analytics/**` (new), `.github/workflows/release.yml`, `.github/workflows/tools-selftests.yml` (the layout-check self-test step), `.github/CLAUDE.md` (Bug E), `docs/plan/slices/267-dino-old-voice-and-wizard-buttons.md`, `src/DinoReader.App/ShelfAudioPacks*.cs`, `src/DinoReader.App/MauiProgram.cs` (added by the coordinator, 2026-11-04), `src/DinoReader.Core/ShelfAudioNetwork.cs` (the quiet-run start rule, builder's addition), `src/DinoReader.App/UsageCounter.cs` (one new method, builder's addition).

#### Design (coordinator, 2026-11-04, written before coding)

One owner for shelf pack fetching. Slice 262 made the `ShelfAudioPacks` singleton the only fetcher of a side language's shelf packs; this slice extends that same owner to **every family language** (main + story languages, never the book's own language), run on each cold start (`Resume`, forced) and after a language is added (`Start`), one pack at a time, skipping packs already installed (a killed app resumes at the next start), a failed pack retried at the next start. The pure "which packs are due" rule is the Core function `AudioPackDue.For(item, familyLanguages)` (French + Farsi family asks exactly `fr` and `fa`), used by `StoreClient.WantedPacks` at book install, so the install path and the shelf runs cannot disagree. The pre-made audio no longer waits for the reading voice to be installed (`ShelfAudioStart.Decide`): a run for a language whose voice is missing still fetches the packs.

## Bug A: books already on the shelf read with the device voice

**What is live (read 2026-11-04 ~20:20 UTC):** `/api/stories` lists 16 books, every one with pre-made audio in 15 languages (the four starters included). The pre-made playback code is in every build from dino-v1.100.0 (Slice 246 part C, #790).

**Likely cause (from the code, not seen on the phone):** a book gets its audio pack at install (`StoreClient.cs:158-167`), but a book that was already on the shelf gets it only from `BookUpdater.SyncPacksAsync` (`BookUpdater.cs:239-255`), which runs from the update check at most once every 7 days on a cold start (`BookUpdater.cs:7, 30-33`, key `dino.books.lastupdatecheck`). A phone that checked in the last week keeps the device voice for every book it already has, up to 7 days. Without a pack, `StoryVoicing.cs:129-139` falls back to the device voice and nothing is logged in a release build. A failed pack fetch is swallowed and waits for the next 7-day check (`BookUpdater.cs:226-233`, `StoreClient.cs:285-297`).

**Why the tests passed:** every pre-made audio flow (`premade-audio-plays`, `premade-audio-starter`) installs or fetches the book's pack on a fresh emulator install and then reads; none starts from an install with books already on the shelf and then upgrades. The upgrade path, which is what every real phone had, was never tested.

- [ ] Check first on the user's phone (wireless adb, Debug log or `dumpsys`): app version, `dino.books.lastupdatecheck`, whether `files/.../audio-packs/<book>/` exists. Write the result here; if the cause is different, fix that instead.
- [x] Fix: missing packs no longer wait for the 7-day check. On every cold start (and right after a story language is added), compare the shelf books with the store list's `audio` entries and fetch any missing pack for the app's languages, one at a time, resumable; the 7-day check stays for book updates. Progress shows as a small note (any wait over 500 ms), never blocking reading.
- [x] A release-build log line names which voice a read used (pre-made pack, or device voice and why), so a report like this can be checked from the phone's log.
- [ ] Test the real path: an emulator flow installs the previous release build, opens a book (device voice), installs the new build over it, cold-starts, and asserts `[AudioPack] premade clip plays` for that same book without any test hook fetching the pack. Android task and iOS task (the same code, iOS compile in CI).

## Bug B: "Skip" and "See what Dino can do" sit on top of each other

**Not a change from today:** no commit between dino-v1.100.0 and dino-v1.102.1 touched `FirstRunPage*`, `IntroWizardPage*` or the shared layout rules (`OnboardingLayout`, `LandscapeLayout`, `TabletPageLayout`, `WindowLayout`); the last layout changes were Slice 232 part E (`d860f63a`) and Slice 234 part B (`5a5a49f6`). Most likely screen (unproven): the Ready screen, `FirstRunPage.xaml:104-118`, a `VerticalStackLayout` (`ReadyActions`) holding the usage line, the "See what Dino can do" button and the "Skip, start reading" button inside a sized Grid row; on a short screen, a tall translation or a large font size, that row can be too small for the stack.

**Screen identified from the user's screenshot (2026-11-04, Pixel 10 Pro, Farsi):** not the Ready screen. It is the **intro wizard's Record step** ("record your own reading of a story", the line about recordings staying on this phone and Settings → Backup): the "Skip" label (رد کردن) is drawn on top of the coral "Try it" button (امتحان کنید), both centred in the same spot at the bottom. So the wizard step footer (`IntroWizardPage*.cs`) places Skip and Try it in the same cell; the guess above was wrong. Fix the footer on every wizard step, every language (long Farsi/German text), portrait and landscape.

**Why the tests passed:** the flows check that a text is visible and tap it by text. Maestro taps the centre of the element's bounds from the accessibility tree, so a button covered by another still "passes". No flow checks bounds, overlap or clipping; the only screenshots (`dino-landscape-reading`, `-settings`, `-shelf`) are saved and never compared or looked at; no flow runs the Ready screen on a phone in portrait with a large font.

- [ ] Reproduce from the user's screenshot (no further request to the user, feedback loop 2026-11-05), then the same screen size and font scale on the emulator (`wm size`, `wm density`, `settings put system font_scale`). Name the screen and the cause here.
- [x] Fix the layout so the buttons stack with their full height at every screen size and font scale (no fixed row height that the stack can outgrow), portrait and landscape, phone and tablet; Android and iOS share the page.
- [ ] Layout check in the device tests: a scripted step reads the element bounds from `uiautomator dump` and fails when two tappable elements on one screen overlap or one is cut off by the screen edge. Run it on every first-run and intro screen, on a phone in portrait at font scale 1.0 and 1.3, and in landscape. Same idea for iOS through the XCUITest tree where a simulator run exists, else recorded under *Not yet verified*.
- [ ] Screenshots taken by flows are put on one contact sheet per run (`reviews/<slice>-shots/`) for the review step, instead of being saved and never seen.

## Bug C: Farsi books read with the device voice; English is fine (user, 2026-11-04 ~20:20 UTC, Pixel 10 Pro, fresh install, Farsi then English)

**What the usage counts show** (`dino_usage`, Android 1.102.0, sessions 78e4, 5818, f907, read 2026-11-04): the install started in Farsi; English was added in Settings at 19:36 (`story_languages_set 2:settings`); books 96640001 (Three Little Pigs), 61bb25b8 (Good Enough), bee62232 (Gingerbread Man) were read in fa and en. **Every fa read showed a `translating` wait, the en reads none.** The events do not say which voice read, so the cause is not provable from them.
**Checked:** `StoreClient.WantedPacks` (`StoreClient.cs:259`) asks for every story language plus the book's own, so "only one language" (the user's option b) is unlikely; the live `/api/stories` lists `fa` audio for all 16 books.
**Likely (unproven):** either the fa text came from on-device translation and no longer matches the recorded sentences, so playback falls back to the device voice; or the fa pack was never fetched for books installed before English was added (Bug A's 7-day rule). An emulator reproduction (Debug build, which logs `[AudioPack]`) is running; its result goes here.

**Reproduced 2026-11-04 ~20:35 UTC** (emulator-5554, Dino Debug from origin/main f750d33d; fresh install, Farsi picked in first run, English added in Settings > Story languages, Goldilocks installed from the Store): the four starter books (61bb25b8 Good Enough, bee62232 Gingerbread Man, 96640001 Three Little Pigs, 0fa29ae0 Tortoise and the Hare) have **only `en-v1`** under `files/audio-packs/<id>/`; Goldilocks (67695905, installed from the Store after English was added) has `fa-v1` and `en-v1` (`pack .../fa v1: installed` 20:32:56). Three Little Pigs in Farsi: no `[AudioPack]` line at all, `[ReadStart] first sound after 5154 ms (device voice)`; in English: `78 of 78 pieces have a clip`, `premade clip plays (sentence 0)`, first sound after 182 ms. The user's Farsi reads were exactly these starter books. **Cause: the starter books get packs only in their own language (English), never in the app's chosen language or the story languages.** Which line skips them (starter import at first run, or the pack sync's language list then) was not found inside the 10-minute run; the code trace says the pack is chosen by `StoreClient.WantedPacks` (`StoreClient.cs:259-260`) and installed by `BookUpdater.SyncPacksAsync` (`BookUpdater.cs:241-251`). The "translating" wait is not a clue: it shows on every read through a book's table (`StoryTranslator.Applies`, ReadingPage ~1190).

- [x] Find the line (starter import and the first pack fetch for shelf books) and fix it: every shelf book, starters included, gets its pack in the app language and every story language, at first run and whenever a language is added. Existing installs keep their starter books (Slice 261), so this fix is needed even after starters leave the app. Android and iOS.
- [ ] Test the real path, no fetch hook: fresh install, Farsi first, then a starter-era shelf book reads Farsi with pre-made audio (`premade clip plays`); also English added later.

## Bug D: no log can say which voice read a book

The user's report could not be checked from our data: the usage events carry no voice, no pack download, no failure reason, and the `[AudioPack]` lines reach the log only in Debug builds (`AudioPackLog.cs:12`).

- [x] Usage event per read: book id, language, voice used (`premade` or `device`) and, for `device`, the reason (`no_pack`, `pack_failed`, `text_mismatch`, `clip_missing`), from one shared, unit-tested place; Android and iOS alike. No book text, titles or file names (the usage-count and crash-report privacy rules); privacy page and store answers updated if a new field needs it (`worker/CLAUDE.md`, `STORE.md`).
- [x] Usage event per pack download: book id, language, `started` / `done` / `failed` and the failure kind (network, size mismatch, unzip, missing clip).
- [x] The same lines in the release build's rolling log (`AudioPackLog`), so a crash or problem report from a phone carries them (Slice 249).
- [x] Worker: `worker/events.ts` accepts the new fields (tests); the query that answers "which voice did session X use" is written into `docs/` next to the other analytics queries.

## Bug E: the store-crash fix took hours to reach the testers

The Slice 260 badge crash (`AutomationId may only be set one time`, `StoreTagBadge.Refresh`) crashed the Store on the user's Pixel 10 Pro at 20:21 UTC (crash report `crashes/dino/2026-11-04/d1f49026-11dd-43f0-a674-355705aaf061.json`, version 1.102.0). The fix #805 merged at 19:02 UTC, but its release runs (19:02, 19:10, 20:01) were each cancelled by the next merge, and dino-v1.102.1 (published 20:10) was built from #806 (18:47), before the fix. The run that carries it (f750d33, dino-v1.102.2) started 20:16.

> **Correction (feedback loop of night 2026-11-04-1441):** `release.yml` has `cancel-in-progress: false`, so no merge cancelled a running release; the fix's runs waited as *pending* behind an 89-minute 1.102.1 run, and each later merge replaced the pending run in the concurrency group (GitHub keeps one pending run per group). The starter session's "my #811 merge cancelled the fix release and added ~15 minutes" was wrong, and holding #812 for it gained nothing. The fix below (a separate group for `fix/` merges) still addresses the real cause: the fix's run never left the queue.

- [ ] (built, not yet verified: see *Verification*) A merge whose PR is labelled or titled as a fix of a crash (`fix(...)` touching app code) is never cancelled by a later merge: the release workflow's concurrency lets it finish, or the next run is held until it has shipped (`.github/workflows/release.yml`, `.github/CLAUDE.md`). Verify on a test branch: two quick merges, the first one's release still publishes.
- [x] After a crash fix merges, the wave or session confirms the build that contains it reached testers (release tag contains the fix commit: `git merge-base --is-ancestor`), and says so; never "fixed" while only the code is.

## Verification

- `dotnet test tests/DinoReader.Tests`; the upgrade flow (Bug A) and the layout check (Bug B) on the emulator, both shown failing on the current build first, then passing.
- The user's phone: after the fix build, a book already on the shelf reads with the pre-made voice after one cold start, and the Ready screen shows both buttons apart.

## Built (builder, 2026-11-04; device results below)

- **Causes found in code.** Bugs A and C: (1) `StoreClient.WantedPacks` added the book's own language and the 7-day `BookUpdater` check was the only thing that fetched packs for a book already on the shelf; (2) `ShelfAudioPacks.Begin` started a launch run only when the language's reading voice was installed (`ShelfAudioStart.Decide(voiceReady, ...)`), so a family language without its voice (the Farsi main language on a phone whose voice was not there) never fetched its packs. Both fixed: `AudioPackDue`, and the voice condition removed from the quiet-run rule (a run still waits for a voice that is on its way). Bug B: `IntroWizardPage.PlaceButtons` reset the buttons grid to no row definitions upright, and a MAUI Grid with none has one implicit row that clamps `Grid.Row = 1` into row 0, so Skip was drawn over Try it; upright it now has two Auto rows (sideways one). The Ready screen (`FirstRunPage.xaml`) was a wrong guess and is unchanged.
- **Bug D.** Core `AudioPackUsage` (closed details), events `read_voice` and `audio_pack_download` (Worker lists + tests, a Core test keeps both lists equal), `StoryVoicing.VoiceUsed` raised per read and counted by `ReadingPage` (demo reads excluded), `AudioPackStore.Outcome` per pack download, `AudioPackLog` also writes to the release rolling log (`AppTrace`). Privacy page and STORE.md updated; queries in `docs/analytics/dino-voice-queries.md`.
- **Bug E.** `release.yml`: a `fix/` merge runs in `release-fix-<sha>`; the wait step blocks each run behind every older active run of a different group. `actionlint` passes; the new `jq` was checked by hand only (no `jq` on this PC).
- **New checks.** `AudioPackDueTests`, `AudioPackUsageTests`, the `ShelfAudioStart` theory, the two Worker tests in `events.test.mjs`; `tools/emulator/test-layout-check.ps1` (fails on the Skip-over-Try-it fixture, a cut-off button; passes stacked and nested); flows `shelf-audio-on-start` (Bugs A and C) and `wizard-layout-record-large-font` (Bug B, `# layout-check` + `# font-scale: 1.3`, new run-flows.ps1 headers).
- **Changed while building (review).** `ShelfAudioStart.Decide` no longer takes `voiceReady`, on purpose: dropping it is the Bug C fix. Packs do not need the device voice, so the quiet run starts for every family language whether or not its device voice is installed. `read_voice` is counted by the owning `ReadingSession` (its own `VoiceUsed` event), not a singleton event, so two pages never count one read twice.

## Built, final summary (2026-11-05)

- **Bugs A and C:** `AudioPackDue.For` (family languages only, never the book's own; fr+fa test); `ShelfAudioStart.Decide` no longer waits for the reading voice to be installed (the likely Bug C cause); shelf packs are fetched each cold start and after a language is added, by the `ShelfAudioPacks` owner; failed packs are retried at most once per launch (counter is in memory, per launch).
- **Bug B:** `IntroWizardPage.PlaceButtons` gave the buttons grid no rows upright, so Skip sat on Try it; now two Auto rows.
- **Bug D:** events `read_voice` (once per book and reading language per session) and `audio_pack_download` (closed kinds, including `failed:missing` and `failed:storage`), Worker lists and tests, privacy page, STORE.md, `worker/CLAUDE.md`, `docs/analytics/dino-voice-queries.md`. `AudioPackLog` also writes the release rolling log, exception type names only (no messages or URLs).
- **Bug E:** `release.yml` puts a `/fix/` merge (case-insensitive) in its own concurrency group; the wait step holds behind older active runs; the commit lookup is retried 3 times, then treated as not blocking with a warning.
- **Tooling:** `tools/emulator/layout-check.ps1` (+ `test-layout-check.ps1`, run in `tools-selftests.yml`); `# layout-check` and `# font-scale:` flow headers in `run-flows.ps1` (restores the prior font scale). Device runs found two tool bugs, both fixed: label-over-button overlaps were not checked (only clickable pairs), and `Get-UiDumpXml`'s `uiautomator dump /dev/tty` returned no XML from a non-tty shell (now dumps to /sdcard and `exec-out cat`).

## Device results (emulator-5554, 2026-11-05)

- `wizard-layout-record-large-font`: PASS on the fixed build 446095cc516b; FAILS on the pre-fix build cfd10992f4e6 ("Try it" covered by "Skip").
- `shelf-audio-on-start`: PASS. `premade-audio-plays`: PASS.
- One run died on a network error and one on a degraded emulator (restarted); both were rerun.

## From review

Files: `reviews/s267-high.md` (9 findings), `reviews/s267-medium.md` (9), `reviews/s267-check1.md` (10); all read to the last finding.

- **High:** applied 1-6. Skipped 7 by design (a read in the book's own language, when the translation does not apply, can still report `device:no_pack`; that follows the user's rule of no own-language pack). Skipped 8 (the failed-this-launch rule in `AudioPackStore.Report` is a dense `AddOrUpdate` lambda; make it a named helper) and 9 (the Outcome handler in `MauiProgram.cs` duplicates a log line and resolves `UsageCounter` per event); both low and style/duplication only, so skipped (9's per-event resolve was later fixed as medium 9).
- **Medium:** applied 2, 3, 4, 5, 8, 9. Skipped 1 by design (`read_voice` is counted once per book and language per session, from the first read start). Skipped 6 (the retry counter is in memory, per launch, so it resets at the next launch). Skipped 7 as intentional (starting the quiet run without the voice installed is the Bug C fix).
- **Check pass 1:** none applied. 5 and 6 are the user's rule and the Bug C fix. 2, 3, 4, 10 are low and left. 1, 7, 8, 9 are recorded under *Outstanding* below.

## Outstanding

- check1 #1: `read_voice` is counted before the read really starts (also before `StopBeforeRecordedPage` or an empty-segments return), so a Play pressed while the pack downloads or fails is counted `device:*` and later premade reads in that session are skipped (`ReadingSession.cs:570`).
- check1 #7: `layout-check.ps1` is dot-sourced by `run-flows.ps1`, so its `param()` block and `$ErrorActionPreference` leak into run-flows scope; move the pure functions to a param-less library.
- check1 #8: the label-overlap check flags a labelled node overlapping a clickable non-ancestor sibling (scrim, tap-to-dismiss layer): false positives on `# layout-check` flows (the launcher shows 13).
- check1 #9: `IntroWizardPage.PlaceButtons` run again (rotation, font-scale change) may leave stale `Grid.Row`/`Grid.Column` on Skip and the main button (unconfirmed).

#### For CLAUDE.md (lifted 2026-11-05)

- Audio packs follow only the family's languages (main + story languages): use `AudioPackDue.For` in `src/DinoReader.Core/AudioPackDue.cs`; never add the book's own language. `ShelfAudioStart.Decide` (`src/DinoReader.Core/ShelfAudioNetwork.cs`) must not wait for the reading voice: packs do not need it. (Slice 267: docs/plan/slices/267-dino-old-voice-and-wizard-buttons.md)
- A flow whose screen must not overlap or clip buttons carries `# layout-check` (and `# font-scale: 1.3` for a large font) as a header in `.maestro/dino/*.yaml`; `tools/emulator/layout-check.ps1` reads the `uiautomator` dump. A Maestro text tap passes on a covered button, so it cannot catch overlap. (Slice 267: docs/plan/slices/267-dino-old-voice-and-wizard-buttons.md)
- `.github/workflows/release.yml`: a merge whose commit text contains `/fix/` (case-insensitive) runs in its own `release-fix-<sha>` group and is never cancelled by a later merge; the concurrency expression and the wait step must classify by the same text. (Slice 267: docs/plan/slices/267-dino-old-voice-and-wizard-buttons.md)

#### Not yet verified

- The user's phone check (no phone tonight), including the "check first on the user's phone" item (app version, `dino.books.lastupdatecheck`, whether `audio-packs/<book>/` exists): the cause is from code and the emulator reproduction, not from the phone.
- The upgrade-from-previous-release path (previous build, then install over it) is not scripted.
- The layout check at font 1.0 and in landscape on every wizard and first-run screen: only the Record step at font 1.3 has a flow. On an emulator without a microphone the Record step may hide Try it.
- The contact sheet of flow screenshots (`reviews/<slice>-shots/`) was not built.
- Bug E on main: the wait step has run (read 2026-11-05 by the feedback loop: the `fix/` merge #826, release run 41950767114, success; #799's run 41951758486 ran 01:55-03:47 UTC, about 112 min against a normal 62-68, and succeeded). Still unchecked: whether that run held behind the fix run on purpose (read its version-job log), and the lookup-failed path. Cost: a `fix/` merge no longer coalesces with later merges (one full release per fix merge, against the Actions quota rules in `.github/CLAUDE.md`).
- iOS runtime and the XCUITest twins (`net10.0-ios` compiles; the XCUITest layout check does not exist).
- The `read_voice` and failure-path log lines carry no messages or URLs: only the success lines were read.
