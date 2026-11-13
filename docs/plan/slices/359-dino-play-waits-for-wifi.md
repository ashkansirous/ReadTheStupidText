# Slice 359 — Dino: Play on mobile data sits silent on "Waiting for Wi-Fi…"

*Planned 2026-11-13 (user bug report).*

**Touches:** `src/DinoReader.App/ShelfAudioPacks.cs`, `src/DinoReader.App/ShelfAudioNetwork.cs`, `src/DinoReader.App/ShelfAudioAsk.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.Core/UsageEvent.cs`, `worker/events.ts`, `worker/events.test.mjs`, `src/DinoReader.App/Platforms/Android/AndroidManifest.xml`, `tests/DinoReader.Tests/*`

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

## Work

- [ ] **Play never waits silently.** When the reader opens a book whose pack for the reading language is missing:
  on mobile data, download that one book's pack at once (the reader asked for it; it is one book, not the shelf).
  The quiet shelf-wide runs keep waiting for Wi-Fi.
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
- After release: `premade_audio:waiting_wifi` and `pack_missing_on_play` counts in dino_usage.

## Not yet verified

_(filled when built)_
