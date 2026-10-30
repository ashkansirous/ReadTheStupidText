### Slice 187 — Translation downloads wait for the internet (design frame R12)

*Built (wave 2026-10-30), PR #576.* *Planned 2026-10-29.* Follow-up of Slice 182 (its design frame R12 was not built: Slice 182 covered our own pack downloads, and the phone's translation models come from ML Kit's own download).

**Touches:** `src/ReadTheStupidText.Application/Reading/WaitStage.cs`, `src/ReadTheStupidText.Application/Translation/*` (new: the pure wait/resume decision), `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MlKitTranslator.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/MlKitTranslator.cs`, `src/ReadTheStupidText.Mobile.Shared/Network/PackResumeWatcher.cs`, `src/ReadTheStupidText.Mobile.Shared/Translation/*` (new: the download session), `src/ReadTheStupidText.Mobile.Shared/Waiting/*`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Wait.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*`, `src/DinoReader.App/TranslationPackPrompt.cs`, `src/DinoReader.App/StoryLanguagesPage.cs`, `src/DinoReader.App/TextStep.xaml.cs`, `src/DinoReader.App/StoryWizardPage.xaml.cs` (exposes its `Wait`), `src/ReadTheStupidText.Mobile/ReviewPage.xaml` (AutomationId), `src/ReadTheStupidText.Application/Reading/PackDownloadLog.cs` (the `waiting` line), `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/Resources/Strings/AppResources*`, `tests/ReadTheStupidText.Tests/*`, `.maestro/*`, `src/ReadTheStupidText.Mobile.Shared/ITranslationPacks.cs`, `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookDispatcher.cs`, `src/ReadTheStupidText.Mobile.Shared/Testing/TestHooks.cs` (the Debug `delete-translation-model` hook), `docs/plan/slices/187-translation-download-waits-for-internet.md`.

#### Why

When the network drops during a translation model download, the phone shows a translation error (Android turns every ML Kit failure into `TranslationModelUnavailableException`, `MlKitTranslator.cs:74-78`) instead of waiting. A parent who walks back into Wi-Fi has to start again. Voice, scan and translation packs of our own already wait and carry on (Slice 182); ML Kit's model download is the one left.

What the code does today (checked 2026-10-29 on `main` 0ce0e56):
- Android: `RemoteModelManager.Download` with empty `DownloadConditions`; no progress (only 1 at the end); Cancel only stops waiting, the download keeps running and an unwanted model is deleted once it lands (`DeleteWhenDoneAsync`).
- iOS: `MLKTranslateRemoteModel` with a polled `NSProgress` (a real percentage) and a real cancel.
- RTST: `ReviewPage` shows `WaitStage.Translating` with Cancel. Dino: `TranslationPackPrompt` shows the same stage; own-book translate goes through `StoryTranslator.DownloadPackAsync`.

#### Design

Frame R12 in `docs/design/rtst-phone/Batch 39 Downloads.dc.html` (described in the Slice 182 file): the overlay's spinner becomes a no-Wi-Fi icon, the bar stops and goes grey, title "Waiting for the internet" ("Waiting for Wi-Fi" on mobile data when the download needs Wi-Fi), line "{Language} is {n}% downloaded. It carries on by itself when you're back online.", buttons Cancel and Retry. A waiting state, not an error: no red. When the network comes back it returns to the downloading overlay.

Decisions (Claude, 2026-10-29; obvious from Slice 182's rules):
- Android has no percentage: the line drops the "{n}% downloaded" part there ("It carries on by itself when you're back online." after "Downloading {Language}"), and the bar is indeterminate while downloading and hidden while waiting. iOS shows the percentage as drawn. Same screen, same strings otherwise.
- "Offline" is decided by MAUI `Connectivity` (no internet access) at the moment the download fails or stalls, not by ML Kit's error code.
- Retry tries at once on any network. Cancel stops it; nothing restarts after Cancel.
- Coming back online restarts the download automatically once per network change (reuse `PackResumeWatcher`, Slice 182), on Wi-Fi/Ethernet only unless the user had already said download on mobile data.

#### State design (coordinator, 2026-10-30, before claim)

- **One owner:** a shared `TranslationModelDownloadSession` (Mobile.Shared/Translation, one per requested language pair), created by the caller (RTST `ReviewPage`, Dino `TranslationPackPrompt` / `StoryTranslator`) and wrapping the per-platform `MlKitTranslator` download. Only the session moves between states; `MlKitTranslator` only reports the end of one attempt (done / failed) and its last progress. The overlay reads the session.
- **States:** Downloading -> Done | WaitingForInternet | Failed | Cancelled. WaitingForInternet -> Downloading (network back, or Retry) | Cancelled. Done, Failed and Cancelled are final.
- **Failed vs waiting:** an attempt that fails while `Connectivity` says no internet goes to WaitingForInternet; a failure with internet stays Failed (today's error message). Grounded: Android maps every failure to `TranslationModelUnavailableException` in the catch near `MlKitTranslator.cs:90`, not 74-78; iOS fail observer is `Platforms/iOS/MlKitTranslator.cs:116`.
- **Interrupts:** app killed or the page left -> the session ends as Cancelled (nothing saved; the next translate starts a new session; Android's existing `DeleteWhenDoneAsync` cleanup stays). Back online -> one restart per network change via `PackResumeWatcher`, only on Wi-Fi/Ethernet unless the user already allowed mobile data. Retry -> restart now on any network. Cancel -> final; the session unsubscribes from the watcher first, so nothing restarts after Cancel.
- **Review-fix rules (2026-10-30):** every failed or stalled attempt is judged by the connectivity at the moment it ended: no internet -> WaitingForInternet (also after a network restart, so flapping Wi-Fi keeps waiting), internet -> Failed. One download call is in flight per page and pair: an abandoned attempt is reused, never doubled. A download with no progress and no internet for `TranslationModelDownloadSession.DefaultStallLimit` (30 s) counts as a failed attempt (-> WaitingForInternet). The time spent waiting is left out of the `model_download` timing (`WaitedTotal`). The early translation's model download runs in a session too, so a tap meanwhile shows the waiting look. A queued display update never starts a wait the page already ended. Retry logs `resume reason=retry`.
- There is no Retry button in `WaitOverlay`/`WaitController` yet: add it.

#### Work

- [x] Application: pure `TranslationDownloadRetry` (no `WaitStage.WaitingForInternet`: `UsageWaitStage` and the Worker stage list are outside Touches, so the waiting look is a `WaitLook` on the downloading stage) decision (failed + offline -> wait; back online -> restart once; cancelled -> stop; network kind rules). Unit tests.
- [x] Mobile.Shared, Android (the session wraps the existing download; `MlKitTranslator` already only returns or throws per attempt, so it is unchanged): on a download failure with no internet, report "waiting" instead of throwing; restart through the watcher; keep the existing cancel/delete behaviour.
- [x] Mobile.Shared, iOS (same; the polled `NSProgress` share reaches the session on the pack path): same, from `MLKModelDownloadDidFail`; keep the last `NSProgress` share for the grey bar.
- [x] `WaitOverlay`/`WaitController`: the waiting look (icon, grey stopped bar, Retry button next to Cancel).
- [x] RTST `ReviewPage` (done) and Dino `TranslationPackPrompt`/own-book translate (done in the prompt and `StoryTranslator`, callers pass their `WaitController`).
- [x] Strings in every app language for both apps (machine drafts marked for native review).
- [x] `PACKDL resume reason=network pack=mlkit-<lang>` Debug log line, as in Slice 182.

#### Verification

- Unit tests for the decision.
- Android emulator, both apps: start an English to Farsi translation, turn on airplane mode mid-download, see "Waiting for the internet", turn it off, see it carry on and translate; Cancel while waiting stops it and nothing restarts.
- iOS: compile check on Windows; the same run on TestFlight (no Mac), listed as not verified until done.

#### Built deviations (accepted by the coordinator, 2026-10-30)

- No `WaitStage.WaitingForInternet`: it would need `UsageWaitStage` and the Worker stage list; the waiting look is a `WaitLook` on `DownloadingModel`, so usage counts are unchanged.
- The session's namespace is `ReadTheStupidText.Mobile.Shared.ModelDownload` (folder `Translation/`): `...Shared.Translation` would hide ML Kit's `Translation` class in the Android translator.
- `MlKitTranslator` is unchanged (it already returns or throws per attempt). RTST shows no percentage; Android shows the spinner, not a bar.
- `TranslationModelDownloadSession` (`Mobile.Shared/Translation`) owns the state; `MlKitTranslator` only reports attempt ends. `TranslationDownloadRetry` holds the pure rules. A stalled attempt (no progress for `DefaultStallLimit`, 30 s) counts as failed. `SingleModelCall` keeps one platform download in flight per page; the early view hands its `WaitController` to the tap view.
- `model_download` timing excludes waited time. The Cancel/Retry row wraps (outer stack `Center` -> `Fill`, in all wait overlays). TalkBack announces the look change.
- Debug hooks: `delete-translation-model`; log lines `PACKDL waiting` / `resume pack=mlkit-<lang>`.
- Strings: 5 keys x 14 languages x 2 apps, machine drafts.

#### From review

- High pass: 10 of 11 findings applied (the one-line-method part was skipped). Medium pass: 8 of 9 applied (the 9th went to device).

#### Verified

- Checks: 1610 RTST + 1756 Dino tests; RTST and Dino Android builds, both iOS compiles, Windows App build.
- Emulator: `translation-waits-for-internet.yaml` PASS (`PACKDL waiting pack=mlkit-it` 01:38:14, `resume reason=network` 01:38:25, then ReadingText). `translation-wait-cancel.yaml` PASS (no mlkit-it restart 30 s after airplane mode off).

#### Known behaviour

Android shows no percentage; RTST has no progress on either platform; ML Kit cannot cancel on Android (the delete-when-done cleanup stays).

#### For CLAUDE.md

In `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`: ML Kit model downloads go only through `TranslationModelDownloadSession`; never call `MlKitTranslator`'s download directly from a page. (Slice 187: docs/plan/slices/187-translation-download-waits-for-internet.md)

#### Not yet verified

- The waiting look by eye (title, grey bar).
- Dino's translation wait on a device.
- Other wait overlays after the `Center` -> `Fill` change.
- iOS on TestFlight.
- Native read of the new strings.
