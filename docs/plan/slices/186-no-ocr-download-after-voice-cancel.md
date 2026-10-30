### Slice 186 — No scan pack download after the user cancels the voice pack

*Built (wave 2026-10-30), PR #574.* *Planned 2026-10-29. Follow-up to Slice 183.*

**Touches:** `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile/App.xaml.cs`, `src/ReadTheStupidText.Mobile/CapturePage.xaml.cs` (in-app Cancel only), `tests/ReadTheStupidText.Tests/*`, `.maestro/rtst/*`, `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookDispatcher.cs`, `src/ReadTheStupidText.Mobile.Shared/PackDownloadHost.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MobileSharedServices.Android.cs` (Debug `cancel-pack-downloads` hook), `docs/plan/slices/186-no-ocr-download-after-voice-cancel.md`.

#### Why

Slice 183 recorded as "known behaviour": after the user cancels the voice pack download (in the app or from the notification), the OCR/scan pack starts downloading by itself. A user who just stopped a big download sees another one start, with a new notification. That is a bug, not a platform limit (2026-10-29 wave retro).

Grounded on `main` (2026-10-29):
- `OcrPackInstaller.EnsureForCurrentLanguageAsync` (`OcrPackInstaller.cs:64`) runs at app start (`App.xaml.cs:31`), on a language change (`:45`), on a re-run when the language changed mid-run (`:90-93`) and on a `PackResumeWatcher` resume (`:164`).
- `RunAsync` (`:108-111`) calls `WaitForVoiceDownloadsAsync` (`:126-134`), which only waits while `_voice.State` is Resolving or Downloading, then installs whatever the voice outcome was. A cancelled voice pack ends the wait and OCR starts.
- No cancel signal reaches OCR: `VoicePackInstaller._userCancelled` is private and reset per run (`:218`, `:283`); `PackResumeCoordinator` epochs are per key.
- Shared code: the same chain runs on Android and iOS. Dino Reader has no OCR installer and no voice-then-other-pack chain.

#### Decision (coordinator's call, obvious behaviour)

A user cancel of the voice pack for a language also holds the automatic scan-pack start for that language. The hold lifts when the voice pack for that language becomes Ready (a user retry), when the user changes language, or when the user opens Scan (the existing on-demand `ScanPackFlow` still asks and downloads). The hold is saved, so an app restart does not start the scan pack either. Offline and Failed voice ends are not user cancels and keep today's behaviour.

#### Claim notes (2026-10-30, grounded)

- Corrected 2026-10-30: the notification Cancel (`VoicePackInstaller.OnNotificationCancel`) is the only user cancel of the voice pack. `CapturePage.xaml.cs:452` is the Cancel of the scan-pack download, not the voice pack, so there is no in-app voice Cancel and `CapturePage` is not changed. `VoicePackInstaller` owns the saved hold (`VoiceCancelHold`), set in `OnNotificationCancel` and on the `PackCancelledByNotificationException` path, cleared on Ready and on a language change; `OcrPackInstaller` only reads it.

#### Work

- [x] Shared: `VoicePackInstaller` exposes a read-only "user cancelled for language X" fact (saved in Preferences under a named constant, cleared on Ready and on language change), set from `OnNotificationCancel` (the only voice cancel).
- [x] Shared: `OcrPackInstaller.RunAsync` checks it after `WaitForVoiceDownloadsAsync` and before `InstallAsync` (and on every trigger: app start, resume, language re-run); held means no install and a Debug log line `OCRPACK held-voice-cancelled <lang>`.
- [x] Opening Scan still offers the pack download (no change to `ScanPackFlow`), and starting it clears nothing on the voice side.
- [x] Unit tests for the hold rule (pure: cancel sets, Ready clears, language change clears, Offline/Failed do not set).
- [x] Android and iOS: same shared code; compile both targets.
- [ ] (flow written, not run end to end; see *Not yet verified*) Maestro flow `.maestro/rtst/`: start the voice download, Cancel, assert the hold marker `OCRPACK held-voice-cancelled` appears (run-flows has no negative log check; the restart case is covered by unit tests); shown failing once on the current `main` build.
- [x] Debug hook `cancel-pack-downloads` (`TestHookDispatcher` -> `PackDownloadHost.CancelAllActive` -> `AndroidPackKeepAlive.CancelAll`) so scripted runs can do the notification Cancel.

#### Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` (hold-rule tests).
- Emulator (headless, by serial): the Maestro flow above, plus in-app Cancel and the notification Cancel; then Retry the voice pack and see the scan pack follow once the voice pack is Ready.
- iOS: compile only here (no Mac); TestFlight check of the in-app Cancel goes to *Not yet verified*.

*Built 2026-10-30, verified on the S21 phone by script; Maestro flow and iOS not yet verified.*

*Changed while building:* the notification Cancel is the only voice-pack cancel (there is no in-app voice Cancel; `CapturePage`'s Cancel is the scan pack's), so `CapturePage` is unchanged. `VoicePackInstaller` owns the hold (`VoiceCancelHold` in Mobile.Shared, saved in Preferences). It is set on notification cancel / the `PackCancelledByNotificationException` path, only while the run is not Ready and its language is current. It is lifted on any Ready and on a language change. `OcrPackInstaller` checks it after the voice wait, logs `OCRPACK held-voice-cancelled <lang>`, reports Finished, and starts the scan pack when that language's voice pack becomes Ready. Added the Debug hook `cancel-pack-downloads`.

*From review:* high pass, 4 of 5 findings applied (including: skip the Preferences write inside the lock). Medium pass, 5 of 7 applied; the other two skipped: hold-survives-restart is the spec, and a test-isolation point.

*Checks:* 1588 tests pass; Android and iOS compile. S21 phone: hook ok, held marker 2 s later (pass); no `ocr-*` start for 40 s (pass); uncancelled run: scan pack starts 0.9 s after voice Ready, never overlapping (pass).

*Known behaviour:* after force-stop and relaunch the cancelled voice pack resumes by itself (existing resume behaviour, Slice 182), and the scan pack follows once it is Ready. Follow-up slice to plan: a user voice cancel should hold the voice resume too.

#### For CLAUDE.md (lifted 2026-10-30)

- `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`: a notification Cancel of the voice pack saves `VoiceCancelHold` (owned by `VoicePackInstaller`); `OcrPackInstaller` must check it before installing and never start the scan pack for a held language. It lifts on any voice Ready and on a language change; never clear it elsewhere. Debug hook `cancel-pack-downloads` simulates the notification Cancel in scripted runs. (Slice 186: docs/plan/slices/186-no-ocr-download-after-voice-cancel.md)

#### Not yet verified

- iOS TestFlight: the notification Cancel holds the scan pack.

- 2026-10-30: `voice-cancel-holds-scan-pack` passed end to end on the emulator in Slice 212's run (`OCRPACK held-voice-cancelled fa`).
