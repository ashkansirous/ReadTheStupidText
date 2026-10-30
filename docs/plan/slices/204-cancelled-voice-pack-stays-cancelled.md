### Slice 204 — A voice pack the user cancelled stays cancelled after a restart

*Built (wave 2026-10-30), PR #588.* *Planned 2026-10-30.* Follow-up to Slice 186 (found on the S21, 2026-10-30).

**Touches:** `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile.Shared/VoiceCancelHold.cs`, `src/ReadTheStupidText.Application/Reading/PackResumeCoordinator.cs` (only if the resume entry needs it), `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, `src/ReadTheStupidText.Mobile/LocalizedPackStatus.cs`, `src/ReadTheStupidText.Mobile.Shared/PackStatusText.cs`, `src/ReadTheStupidText.Mobile/App.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `tests/ReadTheStupidText.Tests/*`, `.maestro/rtst/*`, `src/DinoReader.App/SpeechStartup.cs`, `src/DinoReader.App/LocalizedPackStatus.cs`, `src/DinoReader.App/Resources/Strings/AppResources*`, `docs/plan/slices/204-cancelled-voice-pack-stays-cancelled.md`.

#### Why

After the user cancels the voice pack from its notification, a force-stop and relaunch resumes the voice download by itself from its partial file (Slice 182 resume), and the scan pack follows once it is Ready. The user stopped a big download and the app starts it again: the same class of bug as Slice 186.

#### Decision (coordinator's call, obvious behaviour)

The saved voice-cancel hold from Slice 186 (`VoiceCancelHold`, one owner: `VoicePackInstaller`) also holds the automatic voice-pack start and resume for that language: app start, network resume and foreground resume do not start it. The partial file is kept so a later start resumes. The hold lifts only by a user action: a Download/Retry tap for the voice pack, or a language change. While held, wherever the app shows the voice pack state it shows "Download stopped" with a Download button (no error colour), and reading uses whatever voice is available today (the system voice fallback), as it does while the pack is missing.

#### Claim notes (2026-10-30, grounded)

- No "stopped" state exists: after a notification Cancel the installer publishes `Idle` (`VoicePackInstaller.cs:238-241`), which shows nothing. Add `PackInstallState.Stopped`, published while the hold is on (and at app start when held); `CanRetry` (:124) is true for it, so the reader status line tap (`ReaderPage.xaml.cs:720-724`) and `LanguagePage` (:103/:138) start it. Text: `LocalizedPackStatus.cs` and the English copy in `Mobile.Shared/PackStatusText.cs`, keys in `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx` (every language).
- The relaunch resume comes from `App.xaml.cs:99` (`_installer.StartAsync()`, `VoicePackInstaller.cs:173`, no hold check), not from the resume coordinator (Cancelled ends already return false at :239). The check goes in `StartAsync`'s automatic path; a user tap passes `PackStartReason.User` (required on every call, no default) which clears the hold first.
- Reading while held uses the phone voice, as today when the pack is missing.

#### Work

- [x] Shared: `VoicePackInstaller` checks the hold before every automatic start/resume (app start, `ResumeAsync`, foreground); a user Download/Retry clears it and starts. Debug log `VOICEPACK held-user-cancelled <lang>`.
- [x] UI: the voice pack state shows "Download stopped" + Download while held (strings in every app language, machine drafts marked for native review).
- [x] Unit tests: held -> no auto start or resume; user Download clears and starts; language change clears; Offline/Failed never hold.
- [x] Android and iOS: shared code; both Android builds and both iOS compiles pass.
- [x] Scripted check (S21, by script): cancel via the `cancel-pack-downloads` hook, force-stop, relaunch: the held marker, no `PACKDL start pack=piper-*` for 30 s; tap Download: it resumes.

#### Verification

- Unit tests; Android emulator or the S21 (wireless adb) for the scripted check; iOS compile here, TestFlight later.

#### Not yet verified

- Dino on device; iOS TestFlight; native read of the Stopped strings (machine drafts).
- The Maestro flow end to end (the phone was driven by script instead).

- The scripted Maestro flow `.maestro/rtst/cancelled-voice-pack-stays-cancelled.yaml` has not run on a device. It cannot assert "no `PACKDL start` for 30 s": the device stage reads the log after the run.
- Dino has no `VoicePackInstaller.DebugMarker` wiring, so `VOICEPACK held-user-cancelled` appears in RTST's log only; a Dino device check would need it.

#### Run design (coordinator, 2026-10-30, after two review rounds found races in the run end)

Each run is one object, `VoiceRun` (id, language, `Cancelled`, its task). The installer holds at most one current run, under one lock.
- `StartAsync(reason)`, in a single locked step:
  - **User:** release the hold first.
  - **A current run is active and not cancelled:** join it (return its task). This comes before the held check, so a hold left beside a live run never stops it.
  - **Automatic, language held, no live run:** publish `Stopped`. Report nothing to the resume coordinator and start nothing.
  - **A current run is active and cancelled:** create the next run chained after the old task ends. It never returns the stale task.
  - **No current run:** start a new run.
- **Publishing:** every state publish carries its run id. A publish whose id is not the current run is dropped, so an old run can't overwrite a new one.
- **Cancel:** targets one run id and sets `run.Cancelled` on that run only. It is a no-op if that run is not current, is already cancelled, or already installed the voice, so a late cancel never re-sets a hold the user just released. Nothing is installer-wide, so nothing needs resetting.
- **Language change under a cancelled run:** if the cancelled run's language is no longer the chosen one, no hold is set and, after it ends, the new language's run is started (chained after it).
- **Run end:** one locked step does three things:
  1. Decide the end state.
  2. Set the hold if cancelled, still the current language, and not Ready.
  3. Clear current if it is still this run.
  Only after the lock is released does it report to the coordinator and publish, tagged with the run id.
- **Callers say why:** `SpokenLanguage.ServeAsync` takes the reason from its caller. A grown-up's language tap is User; every other path is Automatic.

#### Changed while building

- Built to the Run design below (VoiceRun per run, one lock, run-id-tagged publishes, per-run Cancelled, single locked end step). New `PackInstallState.Stopped` (CanRetry); `StartAsync(PackStartReason User|Automatic)` is required in both apps. `PackStatus_Stopped` text added in all RTST and Dino languages (machine drafts). `LanguagePage` and `VoicePicker` say "Download" when Stopped.

#### From review

- High pass: 9 of 10 findings fixed. Medium pass found races in the run end; two strikes, so the run lifecycle was redesigned (Run design). The redesign review gave 3 narrow findings, fixed with tests.
- Checks: RTST 1673 and Dino 1779 tests pass.

#### Device (S21, scripted)

- Cancel hook 0.5 s after start: Stopped status, partial kept. PASS.
- Force-stop and relaunch: `VOICEPACK held-user-cancelled fa`, no `PACKDL start` for 35 s. PASS.
- Tap status line: resumed from offset 1110657 to `PACK_READY`, then `ocr-arabic` 3 s later. PASS.

#### For CLAUDE.md (lifted 2026-10-30)

- `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`: every `VoicePackInstaller.StartAsync` call states a `PackStartReason`; only a person's tap is `User`, everything else is `Automatic`. The run lifecycle is the Run design in the slice file: change it there first. (Slice 204: docs/plan/slices/204-cancelled-voice-pack-stays-cancelled.md)
