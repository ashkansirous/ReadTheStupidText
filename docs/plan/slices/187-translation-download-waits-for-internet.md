### Slice 187 — Translation downloads wait for the internet (design frame R12)

*Planned 2026-10-29.* Follow-up of Slice 182 (its design frame R12 was not built: Slice 182 covered our own pack downloads, and the phone's translation models come from ML Kit's own download).

**Touches:** `src/ReadTheStupidText.Application/Reading/WaitStage.cs`, `src/ReadTheStupidText.Application/Translation/*` (new: the pure wait/resume decision), `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MlKitTranslator.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/MlKitTranslator.cs`, `src/ReadTheStupidText.Mobile.Shared/Network/PackResumeWatcher.cs`, `src/ReadTheStupidText.Mobile.Shared/Waiting/*`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Wait.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*`, `src/DinoReader.App/TranslationPackPrompt.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/Resources/Strings/AppResources*`, `tests/ReadTheStupidText.Tests/*`, `.maestro/*`, `docs/plan/slices/187-translation-download-waits-for-internet.md`.

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

#### Work

- [ ] Application: `WaitStage.WaitingForInternet` and a pure `TranslationDownloadRetry` decision (failed + offline -> wait; back online -> restart once; cancelled -> stop; network kind rules). Unit tests.
- [ ] Mobile.Shared, Android: on a download failure with no internet, report "waiting" instead of throwing; restart through the watcher; keep the existing cancel/delete behaviour.
- [ ] Mobile.Shared, iOS: same, from `MLKModelDownloadDidFail`; keep the last `NSProgress` share for the grey bar.
- [ ] `WaitOverlay`/`WaitController`: the waiting look (icon, grey stopped bar, Retry button next to Cancel).
- [ ] RTST `ReviewPage` and Dino `TranslationPackPrompt` / own-book translate use it.
- [ ] Strings in every app language for both apps (machine drafts marked for native review).
- [ ] `PACKDL resume reason=network pack=mlkit-<lang>` Debug log line, as in Slice 182.

#### Verification

- Unit tests for the decision.
- Android emulator, both apps: start an English to Farsi translation, turn on airplane mode mid-download, see "Waiting for the internet", turn it off, see it carry on and translate; Cancel while waiting stops it and nothing restarts.
- iOS: compile check on Windows; the same run on TestFlight (no Mac), listed as not verified until done.
