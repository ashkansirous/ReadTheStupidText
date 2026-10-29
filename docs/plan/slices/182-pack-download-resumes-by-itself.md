### Slice 182 — A pack download that stopped "offline" resumes by itself

*Built 2026-10-29 (PR #544), not yet verified on a real phone or iOS.*

**Touches:** `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile.Shared/Network/*` (new, one connectivity watcher), `src/ReadTheStupidText.Application/Reading/VoicePackSetup.cs`, `src/ReadTheStupidText.Application/Reading/PackRetryPolicy.cs`, `src/ReadTheStupidText.Application/Translation/TranslationPackInstaller.cs`, `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile/App.xaml.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*`, `src/ReadTheStupidText.Mobile/LocalizedPackStatus.cs`, `src/ReadTheStupidText.Mobile.Shared/PackStatusText.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileSharedServices.cs`, `src/ReadTheStupidText.Application/Reading/PackDownloadLog.cs`, `src/ReadTheStupidText.Application/Reading/PackResume*.cs`, `src/DinoReader.App/Resources/Strings/AppResources*`, `src/DinoReader.App/LocalizedPackStatus.cs`, `src/DinoReader.App/StoryLanguagesPage.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/StorytellerDefault.cs`, `src/DinoReader.App/StoryLanguages.cs`, `tests/ReadTheStupidText.Tests/*`, `.maestro/*`, `docs/plan/slices/182-pack-download-resumes-by-itself.md`.

#### Why

Found on the user's Pixel 2026-10-28 (PR #537, `reviews/pack-stall-phone.md` run 2, a file in the worktrees' reviews folder, not in the repo): after a stall, if the network is still down when the retry runs, the download ends `Offline` and stays there (partial file kept) until someone taps Retry. A parent who walks back into Wi-Fi expects it to carry on. Today the downloads live in-process with no network or resume hook in RTST; Dino has two separate `Connectivity.ConnectivityChanged` hooks (`StorytellerDefault.cs:405`, `StoryLanguages.cs:280`) for other purposes.

#### Decisions locked with the user (2026-10-28)

- Two slices: this small one first, then background downloads (Slice 183).
- Applies to every big pack (voice, translation, scan/OCR) in both apps.

#### Design (Batch 39 Downloads mock, frames R12 and 95, added 2026-10-28)

Two frames in `docs/design/rtst-phone/Batch 39 Downloads.dc.html` (README section "Batch 39 mock"), group "Waiting for the internet". The mock mostly agrees with the plan and adds the screen states. Sizes, colours and tokens are in the file; the icons are inline SVG, not Phosphor. Both frames' copy is a draft.

- **R12 (RTST main page, extends Batch 29 R8).** The R8 overlay when the network drops. The spinner becomes a no-Wi-Fi icon. The bar stops and goes grey at the downloaded share. Title "Waiting for the internet"; line "French is 40% downloaded. It carries on by itself when you're back online." Buttons Cancel and Retry. It is a waiting state, not an error, so no red. When the network or the app comes back it returns to R8 ("Downloading French... 40%") from the same place. Retry tries at once. Cancel stops it and nothing restarts by itself after that. Scan and voice packs use the same layout in Settings.
- **95 (Dino, Story languages, extends frame 87 of Dino Batch 37).** The Svenska row offline: the bar keeps 42% but goes grey, the line shows the no-Wi-Fi icon and "Waiting for the internet · 42%", then "Carries on by itself when you're back online." with a small Retry link under it. Cancel still unticks the language. On return the row goes back to "Getting the voice ready · 42%" and keeps counting. The storyteller pack (frame 50) and own-book translation pack use the same line.
- **Mobile data wording.** On mobile data the title is "Waiting for Wi-Fi" instead, unless the user tapped "Download now" (frame 50 in Dino). This matches the plan's Wi-Fi/Ethernet rule.
- **iOS:** the mock shows no iOS-specific frame; same shared code and same layout.

**Open point: waiting-state UI is new.** The slice only said the status line reads "Waiting for the internet". The mock adds the grey stopped bar, the no-Wi-Fi icon, the Cancel and Retry buttons (Retry link in Dino), the second line, and the "Waiting for Wi-Fi" title on cellular. **Default:** build them as drawn.

**Open point: Retry meaning.** The plan says Retry stays available after a failure; the mock makes Retry force a try right now from the waiting state, even on cellular-only. **Default:** Retry from the waiting state starts the download at once, like "Download now" for that tap.

New strings (every app language, machine drafts marked for native review): "Waiting for the internet", "Waiting for Wi-Fi", "{Language} is {n}% downloaded. It carries on by itself when you're back online.", "Waiting for the internet · {n}%", "Carries on by itself when you're back online.".

#### Work

- [x] One shared connectivity watcher in Mobile.Shared (reuse it for Dino's two existing hooks): when the network comes back, or the app returns to the foreground, every pack whose last result was `Offline` or `Failed` (and that the user had started) starts again from its `.partial` (Range resume already exists).
- [x] Respect the mobile-data rule: an automatic restart only on a network the existing policy allows (`StorytellerNetworkPolicy.AllowsAutomaticDownload` / `IPackUpdateNetwork`: Wi-Fi or Ethernet); on cellular-only it waits, unless that download was started with "Download now".
- [x] No restart loop: at most one automatic restart per network change, and none after the user pressed Cancel. Retry stays available.
- [x] The status line says "Waiting for the internet" (not a failure) while it waits; strings in every app language (machine drafts marked for native review).
- [x] `PACKDL resume reason=network|foreground pack=<id> offset=<n>` Debug log line.
- [x] Unit tests: the restart decision (offline/failed/cancelled, network kind, "Download now", one per change).
- [x] Android and iOS: same shared code; both apps.

#### Verification

- Unit tests above.
- Emulator: start a pack download, cut the app's network (`cmd connectivity set-package-networking-enabled false <pkg>` with `set-chain3-enabled true`), let it end Offline, restore: `PACKDL resume reason=network` then `range offset=` and `done`, no tap.
- Real phone, unlocked, app in the foreground: same, plus background then foreground the app while offline: resumes on return.
- iOS: compile in CI; TestFlight check of the same flow.

#### Not covered (decided 2026-10-29)

- Mobile translation packs (ML Kit's own model download, not `TranslationPackInstaller`; the R12 translation overlay of Batch 29 is part of it) and Windows `VoicePackSetup` do not resume by themselves.
- RTST shows the waiting state in the voice-pack status line (tap = try now), not a new overlay; Dino's Story languages row follows frame 95. Retry on a mobile-data phone still asks "Download now?" through the existing question.
- All new strings are machine drafts (marked in the resx comment) for native review.

#### Design after review (2026-10-29, two strikes on dropped resumes)

- Installers never add or remove a resume entry. Every run of a resumable pack (voice, OCR, Dino story languages, storyteller, translation pairs) reports its end once: `IPackResume.Report(key, PackRunEnd, resume)`, where `resume` restarts that exact pack (same language) and returns true when it started a run, false when another run was busy.
- `PackResumeCoordinator` alone owns the entry: Offline is kept and restarted on each network or foreground trigger; Failed gets one automatic retry per process (marked spent when a restart is attempted, given back when the restart finds the pack busy), then `PackResumeState.NeedsUserRetry`; Finished and Cancelled remove it. A trigger never removes an entry: a busy or throwing `resume` leaves it, and the active run's own report updates it.
- UIs read `StateOf(key)`: the Dino languages row shows "Couldn't get the voice" and Retry (no "carries on by itself") for `NeedsUserRetry`; the storyteller card already shows "Try again" for Failed.
- Wording: no network at all is "Waiting for the internet" in both apps (storyteller card has its own phase, pill "Try again"); mobile data only is "Waiting for Wi-Fi" with "Download now". "Download now" consent lasts until that download completes or the grown-up cancels.
- `PackResumeWatcher.Start()` is called when each app creates its window (sets `PackStatusText.CellularOnly`, subscribes); it is `IDisposable`, runs triggers off the connectivity thread, and a failing `NetworkChanged` subscriber cannot stop the others.

- Review 3 skip: translation-pair resume has no Windows watcher (no IPackResume on Windows), so Windows translation downloads do not resume by themselves; out of scope here, the reporting call stays for when a watcher is added.

#### Changed while building

- Resume state moved out of the installers into `PackResumeCoordinator` (`IPackResume`), the sole owner of entries; installers `Report(key, end, resume)` once per run. Offline resumes on every network change or app foreground (Wi-Fi/Ethernet only, or after "Download now"); Failed gets one automatic retry per process, then Retry; Cancel is final (per-key epoch). `PackResumeWatcher` (Mobile.Shared/Network) starts at window creation in both apps.
- Covered: RTST voice, OCR; Dino storyteller, story languages. Not covered: mobile ML Kit translation, Windows `VoicePackSetup`/translation resume (Report call present, no `IPackResume` registered on Windows).
- Design frame R12 (main-page wait overlay) does not apply: that overlay only shows for the ML Kit translation download. Possible follow-up slice.
- Waiting texts "Waiting for the internet" / "Waiting for Wi-Fi" in both apps' status line, the Dino storyteller card and the Story languages row; strings in all languages are machine drafts for native review.

#### From review

- Three passes (`reviews/182-review1..3.md`). Two strikes on dropped resumes led to the coordinator-owns-entries redesign (see "Design after review").

#### Checks

- RTST suite 1531 tests; both Android builds; both iOS compiles.
- Emulator, two rounds: RTST voice resume by foreground and by network change; OCR resume (round 1); Dino storyteller and story languages resume; Cancel stays cancelled; no FATAL.

#### Not yet verified

- OCR resume after the final fix round.
- "Waiting for Wi-Fi" on a real cellular-only phone.
- A real phone and the iOS runtime.
- Failed -> Retry state on a device.

#### For CLAUDE.md

- `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`: Resumable pack installers never track resume state themselves; they `Report` each run's end to `IPackResume`, which alone owns entries. (Slice 182: docs/plan/slices/182-pack-download-resumes-by-itself.md)
