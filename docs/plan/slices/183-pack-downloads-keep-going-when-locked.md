### Slice 183 — Big pack downloads keep going when the phone is locked or the app is in the background

*Planned.* After Slice 182.

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
