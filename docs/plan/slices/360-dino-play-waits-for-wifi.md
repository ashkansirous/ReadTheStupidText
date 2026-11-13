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
  `ShelfAudio_WaitingWifi` pill (the mock supersedes "if nothing else uses it": frame 198's pill goes). Update the rule in `src/DinoReader.App/CLAUDE.md`.
- [ ] **Keep old packs on the server** until phones have moved: `publish-store-books.mjs` leaves the previous pack version
  in R2 and the Worker serves a version one behind the listing, so a phone with a slightly old listing never gets a 404.

- [ ] **Play never waits silently.** When the reader opens a book whose pack for the reading language is missing, it
  downloads at once on any network (Decision 2) and shows the download's progress, not a Wi-Fi wait.
- [ ] **Say what is really wrong** (the mock, Design SS1-SS4, supersedes the earlier wording). Four states under the words
  card, each a pill or card, never a pop-up, and the words keep showing and highlighting: downloading `Reading_GettingBookSound`
  "Getting {0} sound for this book · {1}%"; no internet `Reading_NoInternetSound` "No internet, so this book's {0} sound can't
  download yet." plus the button `Reading_ReadIn` "Read in {0}" when another family language's sound is on the phone; pack missing
  on the server or failed `Reading_SoundFailed` "Couldn't get the {0} sound. Dino will try again soon." (shortened for a child's
  page; not Slice 357's per-status text) plus the same button; nothing to switch to `Reading_NoInternetWordsShow` "No internet. The words
  still show, and the sound comes once you're back online." with no button (also used for a server problem when there is no other
  language). The `ShelfAudio_WaitingWifi` "Waiting for Wi-Fi..." pill (frame 198) goes, not "if nothing else uses it". Strings in every
  `AppResources.<lang>.resx`; `{0}` is the language's name in its own script.
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
- Emulator, mobile data only, released build: change the language, open a book, it downloads and reads (the pill shows
  "Getting {0} sound for this book · n%" and leaves with the first clip); airplane mode shows `Reading_NoInternetSound` with
  `Reading_ReadIn` "Read in English"; a Debug 404 shows `Reading_SoundFailed`; one family language shows `Reading_NoInternetWordsShow`
  with no button.
- Emulator: the grown-ups' hold no longer crashes.
- Emulator, mobile data only: add a language with no question asked, every shelf book's pack for it downloads; a
  Debug hook forcing a 404 or network failure retries within the hour and again on opening the book.
- After release: `premade_audio:waiting_wifi` and `pack_missing_on_play` counts in dino_usage.

#### Design (Dino Reader 2026-11-13 Missing States mock, section 3, added 2026-11-13)

Four frames (SS1 getting the sound, SS2 no internet, SS3 problem on the server, SS4 nothing to switch to), all the same Farsi reading
page of The Three Little Pigs in picture-and-text view on a phone (405 x 864; bezel and status bar are canvas furniture), so only
the strip under the words card is new. The mock agrees with Decisions 1-3 and removes frame 198's Wi-Fi pill. The picture in the
frames is a stand-in. Text keys: `docs/plan/notes/2026-11-13-dino-copy.md` (*Missing States*, SS1-SS4). Hex as in the mock (use the
`DinoTokens` key with the same value, else add one). Fredoka (Vazirmatn for the Farsi words). Layout never mirrors
(`src/DinoReader.App/CLAUDE.md`): the strip and the button sit the same in right-to-left languages; only text runs right to left.
- **Page above the strip (existing reading page):** title row (back 44 x 44, flag pill 46 high radius 16 `#F0E8D8`, view switch: three
  segments 44 x 40, radius 13, the current one white with shadow 0 1 4 rgba(43,37,64,.16) and `#5B57E8`), book title 20 / 600 (Vazirmatn 1.4),
  stage radius 18 `#F0E8D8` with the words card (left/right/bottom 8, `rgba(255,255,255,.95)`, radius 16, padding 10 0, Vazirmatn
  20 / 1.75; the read sentence on `#FFE27A`). Controls row (previous 64 round `#ECE7FF`, Play 84 round `#FF7A59`, next 64 round,
  gap 28, padding 12 0 30).
- **SS1, getting the sound:** the strip row is centred, padding 12 14 0. A pill: min-height 36, padding 7 14 7 9, radius 18, fill
  `#ECE7FF`, text `#3f3bc4`, 14 / 500, line 1.3, gap 8, the filling ring (Slice 290's `197` pill, now per book) then
  `Reading_GettingBookSound` ("Getting فارسی sound for this book · 40%"). Opening a book whose sound is missing starts the download at once, Wi-Fi or
  mobile data. The pill leaves when the first clip plays.
- **SS2, no internet:** padding 10 14 0 (centred). A sand card: radius 18, fill `#F0E8D8`, text `#3f3a52`, padding 10 14, gap 8, column. Line: a
  cloud-off glyph then `Reading_NoInternetSound` 14 / 500, line 1.35. Under it, centred, the button `Reading_ReadIn`: height 40, padding 0 14 0 6, radius 20,
  white, ink `#2b2540`, 14.5 / 600, gap 7, shadow 0 2 0 rgba(43,37,64,.08), a 28 round flag (ring 1.5 rgba(43,37,64,.12)) of the other language. It switches this
  book's reading language for this reading only and starts from this page. Dino tries the missing sound again by itself when the phone is back online.
- **SS3, problem on the server:** the same card shape and button with a warm fill `#FFF0EA`, text `#A4462A`, an alert glyph, text `Reading_SoundFailed`. The hourly retry and
  the retry on opening run quietly behind it.
- **SS4, nothing to switch to:** the sand card of SS2 with the single line `Reading_NoInternetWordsShow` and **no button** (one family
  language, or no other language's sound on the phone). On a server problem with nothing to switch to the line is SS3's, also without the button.

New strings (all 15 `AppResources.<lang>.resx`): `Reading_GettingBookSound`, `Reading_NoInternetSound`, `Reading_ReadIn`, `Reading_SoundFailed`,
`Reading_NoInternetWordsShow`. `ShelfAudio_WaitingWifi` and its translations go.

**Plan changed to match the mock (2026-11-13):**
- "Say what is really wrong" now names the five keys and the four states; the server-problem text is `Reading_SoundFailed`, not Slice 357's per-status message.
- Frame 198's Wi-Fi pill (`ShelfAudio_WaitingWifi`) is removed outright instead of "if nothing else uses it".
- The download progress is shown in a tint pill with the percent (SS1), per book.
- "Read in {0}" is a flag button on the card, per reading only (not a stored choice).
- **Rules in `src/DinoReader.App/CLAUDE.md` this changes (past design choices, in Touches):** "Nothing on the child's side starts or offers a
  download" and "the reading page starts no fetch" (Slice 173, 285): opening a book now starts the book's missing pack (Decision 2), and the
  language-change rule's `IntroWizardPage` wording goes with Slice 362. The honoured-only-if-installed rule for the child's pick stays: "Read in {0}" appears only when
  that audio is on the phone. No hard rule (privacy, store, parity) clashes: iOS and Android share the code.

## Not yet verified

_(filled when built)_
