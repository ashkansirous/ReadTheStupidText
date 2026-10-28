### Slice 182 — A pack download that stopped "offline" resumes by itself

*Planned.*

**Touches:** `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile.Shared/Network/*` (new, one connectivity watcher), `src/ReadTheStupidText.Application/Reading/VoicePackSetup.cs`, `src/ReadTheStupidText.Application/Reading/PackRetryPolicy.cs`, `src/ReadTheStupidText.Application/Translation/TranslationPackInstaller.cs`, `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile/App.xaml.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/StorytellerDefault.cs`, `src/DinoReader.App/StoryLanguages.cs`, `tests/ReadTheStupidText.Tests/*`, `.maestro/*`, `docs/plan/slices/182-pack-download-resumes-by-itself.md`.

#### Why

Found on the user's Pixel 2026-10-28 (PR #537, `reviews/pack-stall-phone.md` run 2): after a stall, if the network is still down when the retry runs, the download ends `Offline` and stays there (partial file kept) until someone taps Retry. A parent who walks back into Wi-Fi expects it to carry on. Today the downloads live in-process with no network or resume hook in RTST; Dino has two separate `Connectivity.ConnectivityChanged` hooks (`StorytellerDefault.cs:405`, `StoryLanguages.cs:280`) for other purposes.

#### Decisions locked with the user (2026-10-28)

- Two slices: this small one first, then background downloads (Slice 183).
- Applies to every big pack (voice, translation, scan/OCR) in both apps.

#### Work

- [ ] One shared connectivity watcher in Mobile.Shared (reuse it for Dino's two existing hooks): when the network comes back, or the app returns to the foreground, every pack whose last result was `Offline` or `Failed` (and that the user had started) starts again from its `.partial` (Range resume already exists).
- [ ] Respect the mobile-data rule: an automatic restart only on a network the existing policy allows (`StorytellerNetworkPolicy.AllowsAutomaticDownload` / `IPackUpdateNetwork`: Wi-Fi or Ethernet); on cellular-only it waits, unless that download was started with "Download now".
- [ ] No restart loop: at most one automatic restart per network change, and none after the user pressed Cancel. Retry stays available.
- [ ] The status line says "Waiting for the internet" (not a failure) while it waits; strings in every app language (machine drafts marked for native review).
- [ ] `PACKDL resume reason=network|foreground pack=<id> offset=<n>` Debug log line.
- [ ] Unit tests: the restart decision (offline/failed/cancelled, network kind, "Download now", one per change).
- [ ] Android and iOS: same shared code; both apps.

#### Verification

- Unit tests above.
- Emulator: start a pack download, cut the app's network (`cmd connectivity set-package-networking-enabled false <pkg>` with `set-chain3-enabled true`), let it end Offline, restore: `PACKDL resume reason=network` then `range offset=` and `done`, no tap.
- Real phone, unlocked, app in the foreground: same, plus background then foreground the app while offline: resumes on return.
- iOS: compile in CI; TestFlight check of the same flow.
