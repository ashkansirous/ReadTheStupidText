### Slice 208 — Paused pack downloads tell you, and half-finished ones finish

*Planned 2026-10-30.* Follow-up to Slice 183, from the S21 night runs (docs/plan/notes/2026-10-29-s21-night-runs.md).

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/PackDownloadService.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidPackKeepAlive.cs`, `src/ReadTheStupidText.Mobile.Shared/PackDownloadHost.cs`, `src/ReadTheStupidText.Mobile.Shared/Network/PackResumeWatcher.cs`, `src/ReadTheStupidText.Mobile.Shared/Network/PackResumeCoordinator.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosBackgroundDownloader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/*Notification*.cs` (new), `src/ReadTheStupidText.Mobile/App.xaml.cs`, `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile/TranslationPackInstaller.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/StoryLanguages.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/ReadTheStupidText.Tests/**`, `.maestro/**`, `docs/plan/slices/208-paused-pack-downloads-tell-you-and-finish.md`

#### Why

- **Android 15 stops a background download and says nothing.** The `dataSync` foreground service has a time limit. When it fires, `PackDownloadService.OnTimeout` (`PackDownloadService.cs:92-97`) only logs and stops; the ongoing notification goes away with the service. The download then fails as Offline and resumes only when the user happens to open the app (`App.xaml.cs:45-47` -> `PackResumeWatcher`). On the S21 (timeout set to 30 s) this is exactly what happened.
- **A crash leaves a half-finished pack nobody finishes.** Resume entries live in memory only (`PackResumeCoordinator`, fed by `_resume.Report(...)` at the end of a run), so a crash forgets them. At launch only the current language's OCR and voice packs are started (`OcrPackInstaller.cs:88-99`). On the S21 an `ocr-arabic` `.partial` sat stalled after a crash. Likely cause (not proven): Arabic was not the current language at the next launch, so nothing started it.

#### Decisions (2026-10-30)

- When Android stops the service on its time limit, post a normal (not ongoing) notification on the existing pack channel: **"Download paused. Tap to finish."** with the pack's name, as each app already words its download notification (RTST `OcrPackInstaller.cs:55`, Dino `StoryLanguages.cs:127`). Tapping it opens the app; the existing foreground resume finishes the download, and the notification is removed when the download resumes or completes. Same for both apps (shared code in Mobile.Shared).
- **iOS twin:** iOS has no service time limit, but its background `URLSession` can still end a download with a transport error while the app is in the background (no network, or Wi-Fi gone with mobile data off; `IosBackgroundDownloader.cs`). Then post the same text as a local notification. Use **provisional** notification authorisation (`UNAuthorizationOptions.Provisional`), so there is no permission prompt: it is delivered quietly to Notification Center. Tapping it opens the app and the same foreground resume runs. If the user has turned notifications off, nothing is posted and the foreground resume still works.
- **Half-finished packs at launch:** on launch (after the existing current-language start), scan the pack folders for `.partial` files. A partial for a pack the current settings still want (current UI or reading language, a story language Dino has ticked, the translation pair in use) resumes with the existing Range-append path (`MobileVoicePackService.cs:430-453`) in the foreground. A partial for a pack nothing wants any more is deleted, freeing the space. Same rule on Android and iOS.
- Debug marker for each path (`PACKDL paused-notified`, `PACKDL stale-partial resume|delete <pack>`), so a device run can prove which ran.

- **Slice 183 leftovers folded in (same notification code), as its own defaults said** (`183-pack-downloads-keep-going-when-locked.md:26,64`):
  - Dino: when a pack download starts outside the grown-up area (the storyteller's first download), the notification-permission ask waits until a parent next opens Settings; the download still runs meanwhile. One "notification question asked" flag per app.
  - The download and paused notifications (and the ask sheet) name the pack's language and its size, e.g. "Downloading the Farsi voice (63 MB)".

#### Work

- [ ] Dino: defer the notification ask to the next grown-up Settings open when the download started elsewhere (flag per app).
- [ ] Both apps: language name and size in the download, paused and ask texts (all locales, machine drafts marked).

- [ ] Android: `OnTimeout` posts the paused notification (shared builder in `AndroidPackKeepAlive`, text from a new `PackDownloadHost.PausedNotificationText` hook set by each app), with a content intent that opens the app; it is cancelled on resume or completion.
- [ ] RTST and Dino strings for the paused text in every locale each app ships (machine drafts marked for native review, as before).
- [ ] iOS: provisional authorisation requested once, the first time a pack download starts; a background-session error while backgrounded posts the local notification; tap opens the app; removed on resume or completion.
- [ ] Shared: a pure `StalePartialPolicy` (Application or Mobile.Shared, unit-tested) that, given the partial files found and the packs the settings want, returns resume / delete for each.
- [ ] Both apps call the stale-partial scan at launch after their current-language start, in the foreground only.
- [ ] Debug markers above.
- [ ] Unit tests: the policy (wanted, unwanted, a partial for a pack with a newer version than the partial, an empty partial).

- [ ] Runner (Slice 213): set `ExpectPackNameInNotification = $true` in `tools/phone/checks/_lib.ps1` once the text names the language and size, so `notification-text` asserts it; add the `paused-notification` check script (lower the dataSync timeout, background the app, assert the paused notification and its tap-to-resume; reset the timeout in a finally), replacing the catalogue's "blocked until Slice 208" entry.

#### Verification

- S21: read the download notification text with `dumpsys notification --noredact` (never checked in the night runs): language name and size shown.
- Emulator: Dino storyteller first download from the gallery: no ask sheet; open grown-up Settings: the sheet shows once.

- Fast: the policy tests.
- S21 (Android 15, `tools/phone/` scripts): set `data_sync_fgs_timeout_duration 30000`, start a big pack, send the app to the background, see the paused notification within ~35 s (`PACKDL paused-notified`), tap it (`tap.ps1` on the notification shade, or `am start` the content intent), download finishes; reset the timeout afterwards.
- S21: leave a `.partial` for a wanted pack and one for an unwanted pack, force-stop, relaunch: the wanted one resumes, the unwanted one is deleted (markers).
- Emulator: a Maestro flow for the stale-partial launch path.
- iOS: compile in CI; TestFlight hand check (background the app mid-download in airplane mode, see the quiet notification in Notification Center, tap, download finishes).
