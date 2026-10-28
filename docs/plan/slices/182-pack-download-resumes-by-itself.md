### Slice 182 — A pack download that stopped "offline" resumes by itself

*In progress (wave 2026-10-28), draft PR.*

**Touches:** `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile.Shared/Network/*` (new, one connectivity watcher), `src/ReadTheStupidText.Application/Reading/VoicePackSetup.cs`, `src/ReadTheStupidText.Application/Reading/PackRetryPolicy.cs`, `src/ReadTheStupidText.Application/Translation/TranslationPackInstaller.cs`, `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile/App.xaml.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/StorytellerDefault.cs`, `src/DinoReader.App/StoryLanguages.cs`, `tests/ReadTheStupidText.Tests/*`, `.maestro/*`, `docs/plan/slices/182-pack-download-resumes-by-itself.md`.

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
