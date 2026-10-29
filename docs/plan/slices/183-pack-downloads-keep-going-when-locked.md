### Slice 183 — Big pack downloads keep going when the phone is locked or the app is in the background

*Built 2026-10-29, PR #549, not yet verified on a device (iOS, PIN-locked phone). After Slice 182.*

**Touches:** `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/*` (new download service), `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/*` (background URLSession), `src/ReadTheStupidText.Mobile/Platforms/Android/AndroidManifest.xml`, `src/DinoReader.App/Platforms/Android/AndroidManifest.xml`, `src/ReadTheStupidText.Mobile/Platforms/iOS/Info.plist`, `src/DinoReader.App/Platforms/iOS/Info.plist`, `src/ReadTheStupidText.Application/Translation/TranslationPackInstaller.cs`, `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/*`, `src/DinoReader.App/Resources/Strings/*`, `src/DinoReader.App/StorytellerDefault.cs`, `src/DinoReader.App/StoryLanguages.cs`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `PRIVACY.md`, `site/privacy.html`, `STORE.md`, `tests/ReadTheStupidText.Tests/*`, `.maestro/*`, `docs/plan/slices/183-pack-downloads-keep-going-when-locked.md`.

#### Why

Found on the user's Pixel 2026-10-28 (`reviews/pack-stall-phone.md` run 1): behind the PIN lock screen Android blocks the app's network (`dumpsys netpolicy`: `blocked_state=APP_BACKGROUND`), so the 124 MB voice pack stopped at 5.4 MB when the screen locked and only went on when the app was on screen again. A parent who starts the download and puts the phone down gets nothing. Today both apps declare only INTERNET and ACCESS_NETWORK_STATE (no service, no FOREGROUND_SERVICE, no POST_NOTIFICATIONS), and iOS `UIBackgroundModes` holds only `audio`.

#### Decisions locked with the user (2026-10-28)

- All big packs (voice, translation, scan/OCR) in both apps; Dino store books are small and stay as they are.
- Ask for notification permission (Android 13+) only when a big download starts, in Dino only inside the grown-up area; if refused, the download still runs, Android just hides the notification.
- Same behaviour on Android and iOS, in the same slice.

#### Design (Batch 39 Downloads mock, frames R13, 96 and N1, added 2026-10-28)

Three frames in `docs/design/rtst-phone/Batch 39 Downloads.dc.html` (README section "Batch 39 mock"), group "Keeps going when locked". The mock agrees with the plan's decisions and adds the exact rules for the ask and the notification. Sizes, colours and tokens are in the file. Copy is a draft. The Android lock-screen frame N1 is a system screen drawn for comparison, not app UI.

- **R13 (RTST, before the notification question).** A sheet over the Voice page: "Keep downloading when the phone is locked?" / "The voice is 124 MB. Allow notifications to keep it downloading while the phone is locked or you use other apps. You'll see the progress there." Buttons Not now and Continue. Android 13+ only, shown once, when the first big download starts (voice, translation or scan pack) and only if notifications are not allowed yet. Never on app launch. Continue opens Android's own permission prompt. Not now skips it. Either way the download starts at once and still runs when locked; Android just hides the notification. The size in the copy is the pack's real size.
- **96 (Dino, same question in the Story languages sheet style, frame 90 of Dino Batch 37).** Copy: "The Svenska voice is 124 MB. Allow notifications so it keeps downloading while the phone is locked. You'll see the progress there." Only inside the grown-up area, behind the gate, so a child never sees the system prompt. If the first big download starts outside it (the storyteller pack after first run), the question waits until a parent next opens Settings, and that download still runs. Shown once. Order: this sheet, then the Wi-Fi question if on mobile data (frame 50), then the download.
- **N1 (Android lock screen).** A standard ongoing notification, so it cannot be swiped away while running. Its text is the app's own status line: RTST "Downloading voice... 45%", Dino "Getting the Svenska voice ready · 42%". One action, Cancel, which stops the download like the in-app Cancel (nothing restarts by itself). Tapping it opens the page where the download started (in Dino, behind the gate). It goes away by itself when the pack is ready, fails or is cancelled; no "done" notification is left. One notification per download per app.
- **iOS:** the mock says iOS shows no ask screen (a background download needs no permission) and no notification; the download just finishes in the background. So the iOS task is the background `URLSession` only: no sheet, no notification, no Cancel-from-lock-screen. This matches the plan, whose permission and notification items are Android items.

**Open point: Dino ask is deferred.** The plan said "Dino only inside the grown-up area". The mock adds that the question waits until a parent next opens Settings when the download starts elsewhere, while the download still runs. **Default:** build it that way; keep a "notification question asked" flag per app.

**Open point: ask only when not already allowed, and a pre-ask for the sheet.** The plan said "the app-language explanation first, then the system prompt"; the mock fixes it as one sheet with Not now and Continue, shown once, skipped when notifications are already allowed. **Default:** as drawn. Not now is final; do not ask again.

**Open point: Cancel action and tap target.** The plan listed Cancel on the notification; the mock adds that tapping it opens the starting page and that no completion notification remains. **Default:** as drawn.

**Open point: RTST asks at the first big download anywhere.** The plan said "when a big download starts"; the mock settles it as the first one only, once. **Default:** once per app install.

New strings (every app language, machine drafts marked for native review): "Keep downloading when the phone is locked?", "The voice is {size} MB. Allow notifications to keep it downloading while the phone is locked or you use other apps. You'll see the progress there.", the Dino variant "The {Language} voice is {size} MB. Allow notifications so it keeps downloading while the phone is locked. You'll see the progress there.", "Not now", "Continue", and the notification texts "Downloading voice... {n}%" and "Getting the {Language} voice ready · {n}%" (reuse the in-app status lines).

#### Work

- [x] Smallest end-to-end first: Android, RTST voice pack: the existing downloader runs inside a foreground service of type `dataSync` (manifest `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_DATA_SYNC`, `POST_NOTIFICATIONS`) with an ongoing notification "Downloading voice… 45%" and a Cancel action; it stops itself when the pack is ready, failed or cancelled. Confirm the current MAUI/AndroidX APIs and the Android 14 foreground-service-type rules through context7 first (`library-docs`).
- [x] The permission prompt at the start of a big download (the app-language explanation first, then the system prompt); never on app launch.
- [x] iOS: big pack downloads use a background `URLSession` (`NSURLSessionConfiguration.CreateBackgroundSessionConfiguration`), handled in `HandleEventsForBackgroundUrl`, resuming the same `.partial`/install path; check whether a background mode beyond `audio` is needed (background URLSession itself needs none).
- [x] Translation and scan packs in RTST, and Dino's storyteller and translation packs, go through the same path.
- [x] Mobile-data rule unchanged (Wi-Fi or Ethernet, or the user's "Download now"); Slice 182's automatic resume reuses the service.
- [x] Debug log lines `PACKDL service start|stop pack=<id>` and the existing PACKDL lines from inside the service.
- [x] Store forms: Google Play's foreground-service declaration (data sync, user-initiated download) in Play Console for both apps, and a privacy-text check (no new data collected; the notification is local). The user submits the Play form; Claude drafts it. (declaration drafted below; the user still has to submit it)
- [x] Strings in every app language (machine drafts marked for native review).

#### Verification

- Real phone (the only place the lock-screen block shows): start the voice pack download, lock the phone at about 10%: `PACKDL` lines keep coming, `.partial` keeps growing, the notification shows progress, `done` arrives with the screen locked. Same for a translation pack and Dino's Kokoro pack.
- Notification permission refused: the download still completes.
- Cancel from the notification stops it and deletes nothing that can resume.
- Emulator: Maestro flow for the permission prompt and the progress notification text.
- iOS: compile in CI; TestFlight: start a pack, lock the iPhone, it completes.

#### Built (implementer notes, 2026-10-29)

- Seam: every pack (voice, storyteller, translation, scan) downloads through `MobileVoicePackService.DownloadAsync`, so one keep-alive scope there covers both apps (`PackDownloadHost`, `IPackKeepAlive`, `IPackBodyFetcher`). Installers still only `Report` run ends; Cancel from the notification raises `PackDownloadHost.CancelRequested(packId)` and `VoicePackInstaller` / Dino `StoryLanguagePacks` end their run with `Cancelled` (final).
- Android: `PackDownloadService` (foreground service, `dataSync`), notification = the app's own status line, one Cancel action, tap opens the app. The keep-alive is best effort: any start or `StartForeground` failure (background start, dataSync quota, `SecurityException`) is caught and logged `PACKDL service start-refused`, drops its own entry, and the download runs as before. One lock in `AndroidPackKeepAlive` owns start, stop and notify; `OnTimeout` stops the service and the download goes on (Slice 182's resume covers a kill).
- iOS: `IosBackgroundDownloader` (background `NSURLSession`, Range append into the same `.partial`, Apple resume data kept beside it on a drop or a pause). Waiters register before a task is looked up; a per-file lock orders the relaunch append against the next run's offset read; no network, or Wi-Fi gone while mobile data is not allowed, ends as an offline transport error. `HandleEventsForBackgroundUrl` is a real `override` in both `AppDelegate.cs`. Compiles; not run (no Mac).
- Asks: RTST asks once in `LanguagePage` before the first download, and at the first download of any other pack (translation, scan) through `PackDownloadHost.AskNotification`. Dino asks in `FirstRunPage` and `StoryLanguages.RequestAsync`. Draft strings; other languages fall back to English until reviewed. The asks never throw and never block the download.
- Review pass 2 redesign (2026-10-29, two strikes on Android foreground-service crash paths). The app never calls `StartForegroundService`: `AndroidPackKeepAlive.Begin` adds the pack to a thread-safe registry and, only while the app is visible (`ActivityManager.GetMyMemoryState` importance), calls plain `StartService`, in try/catch (a refused background start means that download runs without a notification). The service calls `StartForeground` in `OnStartCommand` inside try/catch and `StopSelf()`s on failure, which is legal because no foreground promise was made. The service owns its lifetime: the app side only adds/removes registry entries and calls `Refresh`, which posts the percent or, when the registry is empty, does `StopForeground` + `StopSelf`; the app never calls `StopService`. An automatic resume in the background runs without the service (Slice 182 resumes again on the next trigger).
- Review pass 2 other fixes: iOS Wi-Fi-only decides cellular from `PackResumeWatcher.CellularOnlyKnown` (a failed read means no mobile data), the resume file records the setting and is dropped on a mismatch, and an adopted task with a different setting is cancelled (awaited) and recreated; a task in state Canceling is never adopted and a stuck transfer awaits its cancel before failing as offline; the "events finished" flag is forgotten after 10 s unclaimed; every delegate override and the cancel completion handler is try/catch + `PACKDL service ios-delegate-failed`. `VoicePackInstaller` resets `_userCancelled`/`_resolvedPackId` per run, a late Cancel never overwrites Ready, and a background pack update honours Cancel. Dino `StoryLanguagePacks` maps a pack to every language using it, cancels all, and untracks at run end. `PackCancelledByNotificationException` is handled as Cancelled (no error UI) in `VoicePackInstaller`, `StorytellerDefault` (run and update), `ScanPackFlow`, the OCR and translation installers and `StoryLanguagePacks` (the Windows `VoicePackSetup` never sees it: no keep-alive there). The notification ask runs only for a download the user started (`PackResumeScope` marks automatic restarts), only with the app in the foreground and the permission not yet granted, and gives up after 5 minutes so a later ask can run.
- Debug-only test hook: create the marker file `throttle-pack-downloads` in the app's external files folder (`adb shell run-as` is not needed: `/sdcard/Android/data/<package>/files/`) or set `RTST_THROTTLE_PACK_DOWNLOADS`, and the HTTP pack copy runs at about 200 KB/s so a test can reach the notification's Cancel. Compiled out of Release (`PackDownloadThrottle`, Mobile.Shared); not applied to the iOS background session.
- Not built: the Dino storyteller's first-download deferral ("waits until a parent next opens Settings"; the storyteller starts before the ask); the pack size and language name in the notification/sheet copy.

#### Draft: Play Console foreground-service declaration (for the user to submit, both apps)

- Foreground service type: **Data sync** (`FOREGROUND_SERVICE_DATA_SYNC`). Service: `PackDownloadService`.
- What the service does: "Downloads a language voice, translation or text-recognition pack of 10 to 130 MB that the user asked for, and shows an ongoing notification with its progress and a Cancel button. It runs only while a download is in progress and stops itself when the download finishes, fails or is cancelled."
- Why it must be a foreground service: "On a locked phone Android blocks the network of apps that are not in the foreground, so the download stopped when the screen locked. The user starts the download and then puts the phone down; the service lets it finish."
- User-visible and user-initiated: "Yes. It starts from the user's own action (choosing a language, or a grown-up choosing a story language). The notification is ongoing and has a Cancel action."
- Typical duration: "Under 5 minutes on Wi-Fi; never longer than the 6 hours Android allows."
- Video for the form: the lock-screen run (start a voice download, lock the phone, show the progress notification).

#### Changed while building

- Final Android design (after review pass 2): no `StartForegroundService`. Plain `StartService` only while the app is visible; the service owns its lifetime through a registry with a 15 s grace period (back-to-back packs join the same service); `StartForeground` failure leads to `StopSelf`. A download started while backgrounded runs without the service (Slice 182 resumes it on the next trigger).
- iOS: background `URLSession` with resume data, Wi-Fi rule honoured, Offline mapped for Slice 182's waiting UI.
- Notification ask once before the first big download; never when already granted, never from an automatic resume. Cancel from the notification is final for every pack kind.
- Debug throttle marker `throttle-pack-downloads` (about 200 KB/s, Debug only) so a test can reach Cancel.

#### From review

Three passes (`reviews/183-review1.md` to `183-review3.md`). Two strikes on foreground-service crash paths in pass 2 led to the redesign above instead of more patches.

#### Checks

- Both unit suites, both Android builds, both iOS compiles: green.
- Emulator: screen off + HOME continues under the dataSync foreground service with a rising percent; voice then OCR pack back to back with the screen off (service joined, no Offline); service stops 15 s after nothing is left; Cancel from the shade is final (no restart after a Wi-Fi flap); Dino shows the ask and the system prompt, and no sheet when already granted; no FATAL in logcat.
- Known behaviour: HOME within about 1 s of starting a download makes Android refuse the service; the download stops as Offline and resumes on the next foreground. After cancelling the voice pack the OCR pack still auto-starts: a bug, fixed by [Slice 186](186-no-ocr-download-after-voice-cancel.md).

#### Not yet verified

- iOS background session on a device (TestFlight).
- Real PIN-locked phone network block (the emulator cannot show it).
- Android 15 six-hour `OnTimeout`.
- Dino storyteller first-download ask deferral (the storyteller starts before the ask).

#### User steps

- [x] Play Console foreground-service declaration: filed for both apps (checked 2026-10-29; demo videos at `/videos/pack-download-foreground-service.mp4` on both sites, PR #561).
- Native review of the new strings (13 machine drafts per app).

#### For CLAUDE.md (lifted 2026-10-29)

In `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`:

- Never call `StartForegroundService` for pack downloads. The keep-alive service starts only while the app is visible (plain `StartService`) and owns its own stop through the registry (`AndroidPackKeepAlive`, `PackDownloadService`); the app never calls `StopService`. The Debug marker file `throttle-pack-downloads` slows pack downloads to about 200 KB/s for device tests. (Slice 183: docs/plan/slices/183-pack-downloads-keep-going-when-locked.md)
