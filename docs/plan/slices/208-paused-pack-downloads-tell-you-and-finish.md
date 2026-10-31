### Slice 208 — Paused pack downloads tell you, and half-finished ones finish

*Built 2026-10-31, not merged; device checks mostly not yet run (see Not yet verified).* Planned 2026-10-30. Follow-up to Slice 183, from the S21 night runs (docs/plan/notes/2026-10-29-s21-night-runs.md).

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/PackDownloadService.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidPackKeepAlive.cs`, `src/ReadTheStupidText.Mobile.Shared/PackDownloadHost.cs`, `src/ReadTheStupidText.Mobile.Shared/Network/PackResumeWatcher.cs`, `src/ReadTheStupidText.Application/Reading/PackResumeCoordinator.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosBackgroundDownloader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/*Notification*.cs` (new), `src/ReadTheStupidText.Mobile/App.xaml.cs`, `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Application/Translation/TranslationPackInstaller.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/StoryLanguages.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/ReadTheStupidText.Tests/**`, `.maestro/**`, `docs/plan/slices/208-paused-pack-downloads-tell-you-and-finish.md`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/StalePartialPolicy.cs`, `tools/phone/**`

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

#### Design (Batch 42 Updates and Paused Downloads mock, frames N2 and N3, added 2026-10-30)

Two frames in `docs/design/rtst-phone/Batch 42 Updates and Paused Downloads.dc.html` (N2 line 126, N3 line 155; notes under each frame). N2 also shows the new N1 text (extends N1 of `Batch 39 Downloads.dc.html`). Both are 360x760 phones. The mock agrees with the plan on the paused text, the pack channel, tap-to-open and removal on resume. It adds the points below. The two apps' cards sit together in one frame only to compare. The copy is a draft.

- **Downloading card, new N1 text (N2, top card; Android).** Card `#f6f7fb`, radius 24. App line "Read The Stupid Text · now" (12px, 20px round icon). Title "Downloading the French voice (124 MB)… 45%" (15px, 600); 4px progress bar (`#5B57E8` on `rgba(0,0,0,.12)`); "Cancel" button (14px, 600, `#4A46D6`). Dino's text is "Getting the Svenska voice ready (124 MB) · 42%".
- **Paused notification (N2; Android 15 after the dataSync timeout).** A normal notification on the same pack channel, swipe-away, no buttons. Title "Download paused. Tap to finish." (15px, 600). Second line "French voice (124 MB) · 45% done" (13.5px, `rgba(0,0,0,.62)`). Dino: the same, "Svenska voice (124 MB) · 42% done", with its own icon and "Dino Reader · 3 min". Tap opens the app and the foreground resume continues from 45% (to R8 on RTST, frame 87 on Dino). Gone when the download resumes or finishes. Dino's tap lands on the shelf, not behind the gate; the download carries on there with no screen.
- **iOS twin (N3; Notification Center).** The same two lines ("Download paused. Tap to finish." and "French voice (124 MB) · 45% done"), 36px tile (radius 9), card `rgba(245,246,250,.82)` radius 20. Provisional authorisation: no prompt, delivered quietly to Notification Center only (no banner, sound or lock-screen alert). iOS adds its own "Keep..." and "Turn Off..." buttons the first time; the app cannot change them. Tap opens the app and the foreground resume finishes. Notifications off: nothing posted, resume still works. Dino posts the same with its own icon.

**New strings (every locale each app ships):** the new download text with language and size for RTST and Dino, the paused title "Download paused. Tap to finish.", the second line "{language} voice ({size}) · {percent}% done". Language names are placeholders (RTST "French", Dino "Svenska").

**Open point: percent in the paused text.** The plan (Decisions, first bullet) says the text is "Download paused. Tap to finish." with the pack's name and size; the mock adds "· 45% done". **Default:** show it when the bytes done are known at the timeout or the error; otherwise drop the percent part.

**Open point: language name form.** The plan example is "Downloading the Farsi voice (63 MB)". The mock shows English names for RTST ("French") and Dino's endonym style ("Svenska voice"), and Dino's text "Getting the Svenska voice ready (124 MB) · 42%". **Default:** follow each app's own language-name helper as the mock shows (RTST English, Dino as Dino's `StoryLanguages` names them).

**Open point: Dino tap target.** The plan says tapping "opens the app". The mock sends Dino to the shelf, not behind the grown-up gate. **Default:** shelf, and the resume runs without a screen.

**Open point: scope.** Naming language and size in every download text is already planned in the 183 leftovers (Decisions, last bullet); the mock applies it to both apps' download notifications, not only the ask sheet. **Default:** keep as planned and cover the download, paused and ask texts.

**Platform parity:** N2 is Android, N3 is its iOS twin; both apps on both.

**Launch scan (review design, 2026-10-31):**
- One owner: the pack service (`MobileVoicePackService`) owns pack files. The scan (`ScanStalePartialsAsync`) probes the same per-pack gate every download enters; a pack in flight or queued holds it and is never touched. A delete runs under that gate (a delete and a start cannot interleave), after the wanted languages are read again and the file is re-measured. The scan runs after the current-language start in both apps.
- Nothing is downloaded by the scan. A wanted partial that is exactly the catalog's version is reported as resumable; the app's normal start entry finishes it (RTST: `OcrPackInstaller.EnsureForCurrentLanguageAsync`, behind the voice download, with the voice cancel hold; Wi-Fi/consent rules apply there). Dino has no stored cancel hold, so it deletes only and never restarts a partial by itself; the grown-up's next tick finishes it.
- Keep (no delete, no resume): no catalog; a partial newer than the cached catalog; a wanted pack missing from the catalog; every translation and storyteller pack (their use cannot be read from the settings the scan sees).
- iOS: a pack with a live background task (even one without a waiter) is skipped; a delete also removes the `.resume` sidecar.
- Paused notice: posted where the run ends (`MobileVoicePackService`), only for Offline/Failed (or a thrown failure), and only while the app is in the background. Android also requires that the dataSync time limit stopped the service for that pack; iOS has no such gate (a terminal transport error after the retries). Cleared on success, cancel, an already-installed pack and the start of every new run.
- Dino's deferred notification ask keeps its pending mark until the question was really shown.

#### Work

- [x] Dino: defer the notification ask to the next grown-up Settings open when the download started elsewhere (flag per app).
- [x] Both apps: language name and size in the download, paused and ask texts (all locales, machine drafts marked).

- [x] Android: `OnTimeout` posts the paused notification (shared builder in `AndroidPackKeepAlive`, text from a new `PackDownloadHost.PausedNotificationText` hook set by each app), with a content intent that opens the app; it is cancelled on resume or completion.
- [x] RTST and Dino strings for the paused text in every locale each app ships (machine drafts marked for native review, as before).
- [x] iOS: provisional authorisation requested once, the first time a pack download starts; a background-session error while backgrounded posts the local notification; tap opens the app; removed on resume or completion.
- [x] Shared: a pure `StalePartialPolicy` (Application or Mobile.Shared, unit-tested) that, given the partial files found and the packs the settings want, returns resume / delete for each.
- [x] Both apps call the stale-partial scan at launch after their current-language start, in the foreground only.
- [x] Debug markers above.
- [x] Unit tests: the policy (wanted, unwanted, a partial for a pack with a newer version than the partial, an empty partial).

- [x] Runner (Slice 213): set `ExpectPackNameInNotification = $true` in `tools/phone/checks/_lib.ps1` once the text names the language and size, so `notification-text` asserts it; add the `paused-notification` check script (lower the dataSync timeout, background the app, assert the paused notification and its tap-to-resume; reset the timeout in a finally), replacing the catalogue's "blocked until Slice 208" entry.

#### Verification

- S21: read the download notification text with `dumpsys notification --noredact` (never checked in the night runs): language name and size shown.
- Emulator: Dino storyteller first download from the gallery: no ask sheet; open grown-up Settings: the sheet shows once.

- Fast: the policy tests.
- S21 (Android 15, `tools/phone/` scripts): set `data_sync_fgs_timeout_duration 30000`, start a big pack, send the app to the background, see the paused notification within ~35 s (`PACKDL paused-notified`), tap it (`tap.ps1` on the notification shade, or `am start` the content intent), download finishes; reset the timeout afterwards.
- S21: leave a `.partial` for a wanted pack and one for an unwanted pack, force-stop, relaunch: the wanted one resumes, the unwanted one is deleted (markers).
- Emulator: a Maestro flow for the stale-partial launch path.
- iOS: compile in CI; TestFlight hand check (background the app mid-download in airplane mode, see the quiet notification in Notification Center, tap, download finishes).

#### Built (implement-slice, unattended night 2026-10-31; fast checks only, no device run yet)

**Where.** Shared: `PackDownloadHost` (new hooks `NamedNotificationText`, `PausedTitle`, `PausedNotificationText`, `LanguageName`, `CurrentLanguage`, `PausedNotifier`; `NoteCatalog`/`LabelOf` name a pack's language and size from the last catalog; `Paused`/`ClearPaused`), `StalePartialPolicy` (pure), `MobileVoicePackService.ResumeStalePartialsAsync` (scan, delete, resume; clears the paused notice on a run's start, on Ok and on cancel). Android: `AndroidPackKeepAlive` (also the `IPackPausedNotifier`; `NotifyPaused` from `PackDownloadService.OnTimeout`, notice id 1831 tagged by pack id). iOS: `IosPackNotification` (provisional authorisation once, quiet local notice only while the app is not active), `IosBackgroundDownloader` (posts it on the two offline-failure paths). RTST: `OcrPackInstaller` (texts, English language names, sheet naming, `ResumeStalePartialsAsync`), `App.xaml.cs`. Dino: `StoryLanguages` (texts, deferred ask: `NoteNotificationAskPending` / `AskPendingNotificationAsync`), `App.xaml.cs` (scan after the config fetch, ask on `SettingsPage` appearing). Strings: `PackNotify_Downloading|PausedTitle|Named|PausedDetail` in both apps, all 14 locales (machine drafts, marked). Device checks written, not run: `.maestro/rtst/needs-file/stale-partial-launch.yaml`, `tools/phone/checks/paused-notification.ps1` (catalogue entry unblocked; `ExpectPackNameInNotification` now true, `notification-text` looks for "English" and "(<n> MB)").

**Defaults taken (no questions asked at night), with why:**
- Open points as their Defaults said: percent shown when the bytes done are known; each app's own language-name style (RTST English via `CultureInfo.EnglishName` without the region tail, Dino `LanguageNames.NativeName`); Dino tap lands on whatever the app opens to (launch intent), shelf on a cold start; the download, paused and ask texts all name language and size.
- A pack is named only when it can be named honestly: a voice or storyteller pack of the catalog, and a multilingual pack only by the app's current language. Anything else (scan packs, several packs at once) keeps the old plain status line, and the paused line then shows just the percent.
- The paused notice is skipped when the app is on screen when the time limit fires (the download carries on there; logged `paused-skipped`). `AppIsVisible` counts a foreground service as visible, so a stricter on-screen test was added.
- Stale partial scan "wanted" rule: a pack is wanted when it serves one of the settings' languages (RTST: the UI language; Dino: app language plus ticked side languages); translation and Dino storyteller packs are always kept (their pair/choice is not known at launch, and a wrong delete costs a re-download of a big file). Needs the catalog; offline means no decision and every partial stays. RTST skips the whole scan while the user's cancel of the voice pack is held (Slice 204: a cancelled download stays cancelled). A second partial of the same wanted pack, an empty one, and one older than the catalog's version are deleted.
- Dino's deferred ask: the generic ask hook only records a pending flag (and the pack's label); the sheet appears when `SettingsPage` appears, once (the existing per-app `pack_notification_asked` flag). The Settings > Story languages ask sheet names each language and size (catalog fetched only when the sheet is really shown).
- iOS: authorisation is requested once per process at the first body fetch (iOS remembers the answer); the notice is posted on a transport-error end or a stuck-offline end, only when the app is not active and notifications are not denied.

**Known behaviour left in place (named, none a wrong result):** the Android paused notice is posted only when a run that outlived the time limit then ends unfinished; it is removed the moment a new run of that pack starts (before its first byte), finishes, fails for good or is cancelled; iOS counts a Clear that lands while the notice is still being posted, so a late post cannot stay up over a running download (review fix). The first-run and `LanguagePage`/`FirstRunPage` ask sheets (files outside this slice's Touches) do not name the pack yet; only the deferred Dino ask, the Dino Story languages ask and the RTST generic ask do. `paused-notification` fires the notice's content intent with `am start` of the launcher activity instead of tapping the shade (SystemUI is not driven by the runner). No Maestro/phone check exists yet for the Dino deferred ask or the wanted-partial resume branch (phone Verification lines above cover them by hand).

**Not yet verified (device stage):** all of the Verification section above except the unit tests; iOS only compiles here (TestFlight hand check owed). The dataSync timeout commands are from developer.android.com (`device_config put activity_manager data_sync_fgs_timeout_duration`, `am compat enable FGS_INTRODUCE_TIME_LIMITS`), not yet run on the S21.

#### Changed while building

- Device bug, S21 offline 2026-10-31 ~13:57: `paused-notification` and `notification-text` failed to run. The check turned off Wi-Fi and data over the wireless adb link and lost the phone (left offline, timeout and compat flag set, packs moved aside). Rule: a check never cuts the link it is driven over. Reworked: a per-app block with `cmd netpolicy add restrict-background-blacklist <uid>`, a detached device-side restore after 240 s started before any change, and a guard that refuses whole-device network cuts on wireless serials (self-tested in `tools/phone/test-phone-lib.ps1`; see `tools/phone/README.md`). The per-app block is not yet run on a device.
- The "paused notice stays up while a download runs" behaviour the builder listed as known was fixed as a bug in review: iOS removes a late post, and the notice is posted from the background only.
- The stale-partial Maestro flow header now names the emulator dev package `uk.sirous.readthestupidtext`.

#### From review

- High (reviews/s208-high.md): all 9 findings applied.
- Medium (reviews/s208-medium.md): applied 1, 2, 3, 4, 6, 8. Skipped 5: Android posts the paused notice only on the time-limit stop by design, since an OS kill cannot post. Skipped 7 and 9: style.
- Checks: builds green including the iOS compile of both apps; both unit suites; `tools/phone` self-tests. Emulator: the stale-partial-launch flow passed on rerun (the first cold run after install deleted the partial but the marker was not seen within 40 s; not diagnosed).

#### For CLAUDE.md

- Phone checks never cut the network link they are driven over: on a wireless adb serial block only the app (`cmd netpolicy add restrict-background-blacklist <uid>`), start a detached device-side restore first; `tools/phone/` refuses whole-device cuts on wireless serials. Put it in `tools/CLAUDE.md`. (Slice 208: docs/plan/slices/208-paused-pack-downloads-tell-you-and-finish.md)

#### Not yet verified

- The whole S21 Verification list: paused notice within ~35 s and tap resumes; notification text (language and size); stale partial wanted resumes, unwanted deleted.
- Whether the netpolicy per-app block stops a foreground-service download: prove on an emulator first (`run-checks.ps1` has no emulator path and the emulator has no `.dbg` dev install).
- Dino storyteller first download shows no ask sheet and grown-up Settings shows it once (emulator, not reached).
- iOS TestFlight hand check.
- Stale-partial-launch first cold run after install: marker not seen within 40 s (not diagnosed).
