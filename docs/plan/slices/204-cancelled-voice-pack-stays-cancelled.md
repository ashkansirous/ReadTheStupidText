### Slice 204 — A voice pack the user cancelled stays cancelled after a restart

*Planned 2026-10-30.* Follow-up to Slice 186 (found on the S21, 2026-10-30).

**Touches:** `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile.Shared/VoiceCancelHold.cs`, `src/ReadTheStupidText.Application/Reading/PackResumeCoordinator.cs` (only if the resume entry needs it), the screen that shows the voice pack state (find it; likely `src/ReadTheStupidText.Mobile/LanguagePage.xaml*` or the reader's status line), `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*`, `tests/ReadTheStupidText.Tests/*`, `.maestro/rtst/*`, `docs/plan/slices/204-cancelled-voice-pack-stays-cancelled.md`.

#### Why

After the user cancels the voice pack from its notification, a force-stop and relaunch resumes the voice download by itself from its partial file (Slice 182 resume), and the scan pack follows once it is Ready. The user stopped a big download and the app starts it again: the same class of bug as Slice 186.

#### Decision (coordinator's call, obvious behaviour)

The saved voice-cancel hold from Slice 186 (`VoiceCancelHold`, one owner: `VoicePackInstaller`) also holds the automatic voice-pack start and resume for that language: app start, network resume and foreground resume do not start it. The partial file is kept so a later start resumes. The hold lifts only by a user action: a Download/Retry tap for the voice pack, or a language change. While held, wherever the app shows the voice pack state it shows "Download stopped" with a Download button (no error colour), and reading uses whatever voice is available today (the system voice fallback), as it does while the pack is missing.

#### Work

- [ ] Shared: `VoicePackInstaller` checks the hold before every automatic start/resume (app start, `ResumeAsync`, foreground); a user Download/Retry clears it and starts. Debug log `VOICEPACK held-user-cancelled <lang>`.
- [ ] UI: the voice pack state shows "Download stopped" + Download while held (strings in every app language, machine drafts marked for native review).
- [ ] Unit tests: held -> no auto start or resume; user Download clears and starts; language change clears; Offline/Failed never hold.
- [ ] Android and iOS: shared code; compile both.
- [ ] Scripted check: cancel via the `cancel-pack-downloads` hook, force-stop, relaunch: the held marker, no `PACKDL start pack=piper-*` for 30 s; tap Download: it resumes.

#### Verification

- Unit tests; Android emulator or the S21 (wireless adb) for the scripted check; iOS compile here, TestFlight later.
