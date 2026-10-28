### Slice 183 — Big pack downloads keep going when the phone is locked or the app is in the background

*Planned.* After Slice 182.

**Touches:** `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/*` (new download service), `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/*` (background URLSession), `src/ReadTheStupidText.Mobile/Platforms/Android/AndroidManifest.xml`, `src/DinoReader.App/Platforms/Android/AndroidManifest.xml`, `src/ReadTheStupidText.Mobile/Platforms/iOS/Info.plist`, `src/DinoReader.App/Platforms/iOS/Info.plist`, `src/ReadTheStupidText.Application/Translation/TranslationPackInstaller.cs`, `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/*`, `src/DinoReader.App/Resources/Strings/*`, `src/DinoReader.App/StorytellerDefault.cs`, `src/DinoReader.App/StoryLanguages.cs`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `PRIVACY.md`, `site/privacy.html`, `STORE.md`, `tests/ReadTheStupidText.Tests/*`, `.maestro/*`, `docs/plan/slices/183-pack-downloads-keep-going-when-locked.md`.

#### Why

Found on the user's Pixel 2026-10-28 (`reviews/pack-stall-phone.md` run 1): behind the PIN lock screen Android blocks the app's network (`dumpsys netpolicy`: `blocked_state=APP_BACKGROUND`), so the 124 MB voice pack stopped at 5.4 MB when the screen locked and only went on when the app was on screen again. A parent who starts the download and puts the phone down gets nothing. Today both apps declare only INTERNET and ACCESS_NETWORK_STATE (no service, no FOREGROUND_SERVICE, no POST_NOTIFICATIONS), and iOS `UIBackgroundModes` holds only `audio`.

#### Decisions locked with the user (2026-10-28)

- All big packs (voice, translation, scan/OCR) in both apps; Dino store books are small and stay as they are.
- Ask for notification permission (Android 13+) only when a big download starts, in Dino only inside the grown-up area; if refused, the download still runs, Android just hides the notification.
- Same behaviour on Android and iOS, in the same slice.

#### Work

- [ ] Smallest end-to-end first: Android, RTST voice pack: the existing downloader runs inside a foreground service of type `dataSync` (manifest `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_DATA_SYNC`, `POST_NOTIFICATIONS`) with an ongoing notification "Downloading voice… 45%" and a Cancel action; it stops itself when the pack is ready, failed or cancelled. Confirm the current MAUI/AndroidX APIs and the Android 14 foreground-service-type rules through context7 first (`library-docs`).
- [ ] The permission prompt at the start of a big download (the app-language explanation first, then the system prompt); never on app launch.
- [ ] iOS: big pack downloads use a background `URLSession` (`NSURLSessionConfiguration.CreateBackgroundSessionConfiguration`), handled in `HandleEventsForBackgroundUrl`, resuming the same `.partial`/install path; check whether a background mode beyond `audio` is needed (background URLSession itself needs none).
- [ ] Translation and scan packs in RTST, and Dino's storyteller and translation packs, go through the same path.
- [ ] Mobile-data rule unchanged (Wi-Fi or Ethernet, or the user's "Download now"); Slice 182's automatic resume reuses the service.
- [ ] Debug log lines `PACKDL service start|stop pack=<id>` and the existing PACKDL lines from inside the service.
- [ ] Store forms: Google Play's foreground-service declaration (data sync, user-initiated download) in Play Console for both apps, and a privacy-text check (no new data collected; the notification is local). The user submits the Play form; Claude drafts it.
- [ ] Strings in every app language (machine drafts marked for native review).

#### Verification

- Real phone (the only place the lock-screen block shows): start the voice pack download, lock the phone at about 10%: `PACKDL` lines keep coming, `.partial` keeps growing, the notification shows progress, `done` arrives with the screen locked. Same for a translation pack and Dino's Kokoro pack.
- Notification permission refused: the download still completes.
- Cancel from the notification stops it and deletes nothing that can resume.
- Emulator: Maestro flow for the permission prompt and the progress notification text.
- iOS: compile in CI; TestFlight: start a pack, lock the iPhone, it completes.
