# Slice 360 — Dino: Play on mobile data sits silent on "Waiting for Wi-Fi…"

*Planned 2026-11-13 (user bug report).*

**Touches:** `src/DinoReader.App/ShelfAudioPacks.cs`, `src/DinoReader.App/ShelfAudioNetwork.cs`, `src/DinoReader.App/ShelfAudioAsk.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/AudioPackStore.cs`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.App/StoryLanguagesPage.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.Core/AudioPackDue.cs`, `.github/scripts/publish-store-books.mjs`, `worker/stories.ts`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.Core/UsageEvent.cs`, `worker/events.ts`, `worker/events.test.mjs`, `src/DinoReader.App/Platforms/Android/AndroidManifest.xml`, `tests/DinoReader.Tests/*`

## Why

On Monday 2026-11-09 the user, away from Wi-Fi in a restaurant on mobile data, opened a book already on the shelf
to show a friend. Play showed a Wi-Fi message and nothing was read. On the restaurant Wi-Fi it played at once.

What three sources show (2026-11-13):
- **Code** (`ground-in-code`, origin/main): no error says "connect to Wi-Fi". The reading page shows the status pill
  `ShelfAudio_WaitingWifi` "Waiting for Wi-Fi…" (`ReadingPage.xaml.cs:1173`) while the book's audio pack for the
  current language is not on the phone. Launch and app-return pack runs are quiet and never allow mobile data
  (`ShelfAudioPacks.cs:82, 241, 424, 620-647`, `ShelfAudioNetwork.cs` `NeedsWifi`); only the grown-up's "Download now"
  answer in the add-a-language dialog (`ShelfAudioAsk.cs:23-25`) allows it. The wait reports **no** usage or fallback
  event, only a local log line. There is no setting to allow mobile data.
- **Emulator** (`device-test`, released `dino-v1.124.1`, mobile data only, 2026-11-13): a book played before and a book
  never played both read at once (their audio came with the book). After switching the app language to French, the same
  book showed `En attente du Wi-Fi…` and played silent (`[AudioPack] silent piece shown ... no_pack`). **Reproduced.**
- **Logs** (`analytics-query`, dino_usage): 2026-11-09 20:25 UTC, Android 1.123.2, GB, three launches within a minute:
  `book_opened`, `audio_pack_download failed:missing` x2 and `premade_audio:clip_missing` x5. Likely the user (time and
  place fit, repeated tries), not provable: no device id. If so, the pack the app wanted was missing on the server, and
  the app still showed a Wi-Fi wait. Four Dino releases were cut that day (1.123.0-1.123.3).

**Why the rule didn't save it** (`ground-in-code`, origin/main, 2026-11-13). The user's rule is Slice 267 line 5: every
book downloads its audio in all family languages at install, for books on the shelf, when a language is added, and in the
update check. Getting a book does that at once on any network (`StoreClient.cs:247-256`). The gaps come later:
- A failed pack is retried only by the weekly book update check (`BookUpdater.cs:426-443`, Slice 181's "at most once every
  7 days"), and a failed pack stays failed for the rest of the launch after two tries (`AudioPackStore.cs:565-569`).
  Slice 246's review flagged "failed pack ignored until the weekly check" as High and it was skipped as "by design" by the
  building session, without the user.
- A 404 (`failed:missing`, `StoreClient.cs:427-431`, Worker `stories.ts:553-558`: the listing doesn't offer that version)
  is not counted as a failure in the weekly sync (`StoreClient.cs:372-376`), so it is never retried.
- Launch and app-return runs never use mobile data (`ShelfAudioPacks.cs:241, 620-647`). That Wi-Fi rule came from Decision
  120 (the 100+ MB storyteller voice pack) and was carried over to the small per-book packs in Slices 278/285 without the user.
- Likely trigger on 11-09: commit `b60cca23` (08:54 UTC) re-rendered The Three Little Pigs in all 15 languages, so its pack
  versions changed; a phone with an older listing asked for a version the Worker no longer offers (404). Not proven: the
  book id in the 11-09 rows settles it (first work item below).

## Decisions (user, 2026-11-13)

1. **A missing or failed pack is retried every hour** while the app runs (and at the next launch), never weekly. Any
   failure counts, a 404 included: a 404 first refreshes the store listing, then retries with the listed version.
2. **Opening a book whose audio is missing** (in any family language) retries its packs at once.
3. **Mobile data is fine for every book audio download**: no Wi-Fi wait for book packs anywhere (launch, app return,
   language added, update, retry). The "Download on mobile data?" question for book audio goes. Decision 120's Wi-Fi rule
   stays only for anything large that is not a book pack, if any remains.

## Work

- [ ] **Hourly retry and retry on open** (Decisions 1-2): a pack-repair run separate from the weekly book update check;
  remove the per-launch two-failure cap for it (keep a back-off inside the hour); a 404 refreshes the listing and retries.
  Due-check logic in Core with tests (never tried, failed 59 min ago, failed 61 min ago, 404 then new version).
- [ ] **Mobile data everywhere for book packs** (Decision 3): drop the Wi-Fi wait in `ShelfAudioPacks`/`ShelfAudioNetwork`
  for book packs and the `ShelfAudioAsk` question on `LanguagePage`, `StoryLanguagesPage`, `IntroWizardPage`; remove the
  `ShelfAudio_WaitingWifi` pill if nothing else uses it. Update the rule in `src/DinoReader.App/CLAUDE.md`.
- [ ] **Keep old packs on the server** until phones have moved: `publish-store-books.mjs` leaves the previous pack version
  in R2 and the Worker serves a version one behind the listing, so a phone with a slightly old listing never gets a 404.

- [ ] **Play never waits silently.** When the reader opens a book whose pack for the reading language is missing, it
  downloads at once on any network (Decision 2) and shows the download's progress, not a Wi-Fi wait.
- [ ] **Say what is really wrong.** No network: "No internet: this book's sound for <language> isn't on the phone yet".
  Pack missing on the server or failed: the Slice 357 per-status message, plus "Read in <shelf language>" when that
  language's audio is on the phone. Strings in every `AppResources.<lang>.resx`.
- [ ] **Log it.** A fallback `premade_audio:waiting_wifi` and `premade_audio:pack_missing_on_play` (closed reasons,
  `FallbackReason` + `worker/events.ts` `FALLBACK_REASONS` + test), so this case shows in the data next time.
- [ ] **Find the missing pack.** From the 11-09 rows (book id in `blob` fields) list which book and language returned
  `failed:missing`, check that pack exists in R2 now, and record the answer here. If a release or store upload can leave
  a listing pointing at a pack that isn't uploaded yet, fix that order in `publish-store`.
- [ ] **iOS twin:** the same code is shared (`ShelfAudioPacks`, `ReadingPage`); verify the iOS cellular check
  (`ShelfAudioNetwork`) on a TestFlight build.
- [ ] **Side crash found on the run:** holding the grown-ups' 3-second press crashed 1.124.1 once with
  `PermissionException ... android.permission.VIBRATE` (log `D:\ReadTheStupidText-worktrees\reviews\dino-1.124.1-cellular-crash.txt`).
  Add the permission to `AndroidManifest.xml` or drop the haptic; check iOS needs nothing.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`; `node --test worker/events.test.mjs`.
- Emulator, mobile data only, released build: change the language, open a book, it downloads and reads; airplane mode
  shows the no-internet text with "Read in English".
- Emulator: the grown-ups' hold no longer crashes.
- Emulator, mobile data only: add a language with no question asked, every shelf book's pack for it downloads; a
  Debug hook forcing a 404 or network failure retries within the hour and again on opening the book.
- After release: `premade_audio:waiting_wifi` and `pack_missing_on_play` counts in dino_usage.

## Not yet verified

_(filled when built)_
