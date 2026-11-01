# Slice 244 — Dino: find and fix the crashes of 2026-10-31 and 2026-11-01

*In progress (2026-11-01).* From the user, after testing Dino Reader on an iPhone: "there are a lot of crashes ... as far as
I can tell, when the story is playing and I click on back, it does not pause/stop the story", then "another slice to
figuring out what can have cause the crashes today and yesterday and fixing them".

**Depends on:** none. Slice 243 adds the dSYM and log lines that would prove a cause; this slice does not wait for it
and works from the evidence below plus reproduction.
**Touches:** `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/Reading*.cs`, `src/DinoReader.App/Story*.cs`,
`src/DinoReader.App/Platforms/iOS/**`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader*.cs`,
`src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosTtsEngine.cs`,
`src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader*.cs`,
`src/ReadTheStupidText.Mobile.Shared/Crashes/**`, `src/ReadTheStupidText.Application/**/SpeechTextChunker.cs`,
`tests/DinoReader.Tests/**`, `tests/ReadTheStupidText.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Evidence (read 2026-11-01; raw files were in that session's scratchpad)

- **Apple, 5 TestFlight crash logs, one shape:** main thread, CATransaction commit, `-[UIView layoutSublayersOfLayer:]`,
  about 7 unnamed DinoReader.App frames, then `-[__NSDictionaryM __setObject:forKey:]: object cannot be nil`, SIGABRT.
  2026-10-28 21:40 (1.73.1, iPhone 12, iOS 26.6.2); 2026-11-01 13:45 and 13:46 (1.83.0) and 21:01:05 and 21:01:26
  (1.88.0), all iPhone 8 (iPhone10,4, iOS 16.7, under 3 GB RAM). 13-93 s after launch. Other threads show 40-74
  SherpaOnnxC frames (the voice was running).
- **Our R2 reports, the two 1.88.0 `unclean_exit`s:** the Farsi voice `piper:fa-mana` was reading. At 21:00:36 the
  reader queued the whole book at once (about 110 `cache miss` sentences in 10 ms); sentences then finished one by one
  at 0.4-1 s each, up to 24 s after the start, and at 21:01:01 a 6-character line came out silent twice and was rescued
  on the 3rd take. The app died before 21:01:05. The next launch read again at 21:01:20 and died at 21:01:26.
- **R2 `native` report, a different crash:** store 1.85.3, iOS 27, US, `0xdead10cc` (RUNNINGBOARD): iOS killed the app
  in the background because it held a file lock (or a shared database) while suspended.
- **Back:** `OnBackTapped` calls `Navigation.PopAsync()` (`ReadingPage.xaml.cs:1281`) and `OnDisappearing` stops and
  detaches the session when the page left the stack (`:741-760`). No log line says whether Back was pressed before
  either crash, and no device run has tested it yet.

## Likely causes (not proven; prove or drop each)

- The layout crash is a `UIStringAttributes`/attributed-string dictionary built with a null value, e.g. a font that
  could not be found or a paragraph style built during layout (`Platforms/iOS/IosTextMeasurer.cs:24`,
  `IosTextLineSource.cs:30-56`), or MAUI label code on a page being torn down (Back during reading).
- Queuing the whole book at once on a phone with under 3 GB RAM: memory pressure and many main-thread callbacks.
- `0xdead10cc`: a file kept open (rolling log, run marker, preferences or a SQLite store) when the app is suspended.

## Work

- [ ] 1. **Reproduce first.** Run the Farsi Three Little Pigs on the iOS simulator (CI macOS runner, scripted) and on
  the user's iPhone 8 when it is free: read, press Back mid-sentence, read again, change page while reading, background
  the app while reading. Same flows on the Android emulator (parity). Record what crashes.
- [ ] 2. **Layout crash:** find every iOS place that builds an attributes dictionary or attributed string (our code and
  the MAUI handlers we customise); guard each value (fall back to the system font, never put a null). Unit-test the
  pure parts; prove with the item 1 flow that it no longer crashes.
- [ ] 3. **Bounded synthesis queue (Android and iOS):** never queue the whole book; keep the current sentence plus a
  small look-ahead (number set from memory class) and cancel it all on Stop/Detach/Back. Check the Android reader does
  the same today and change both alike.
- [ ] 4. **Back while reading:** prove from logs (Slice 243 item 5 lines, or a local debug log) that Back stops audio and
  synthesis at once on both platforms; fix if it does not.
- [ ] 5. **`0xdead10cc`:** find files held open across suspension (rolling log writer, crash run marker, settings,
  any SQLite); close or flush them on background (`UIApplication.DidEnterBackground`) or wrap the write in a background
  task. Same review on Android for its equivalents.
- [ ] 6. Write what each crash turned out to be into this file, proven vs likely, with the flow that shows it fixed.

## Verification

- Unit tests for the guarded attribute builder and the bounded queue.
- Scripted iOS simulator flow and Android emulator flow: read, Back mid-sentence, re-read, background, 3 times each,
  no crash; queue length stays bounded in the log.
- After release: `tools/crashes` (or Slice 243's report) shows no new `NSDictionary` or `0xdead10cc` crash for a week.
