# Slice 274 — Dino iPhone crash: leave the app from the grown-ups pages and come back

*Built 2026-11-06, merged as PR #843 (bc4b8a8c). Cause likely, not proven; not yet tried on the user's iPhone (see Not yet verified).* Planned 2026-11-06. Parallel with Slice 273 part A, which owns StoryVoicing, ReadingPage, ReadingSession and UsageCounter in DinoReader.App. Bug fix, Dino Reader, iOS first (Android checked for the same path). Reported by the user on 2026-11-06 ~00:10 UTC, testing the TestFlight build with Slice 270: "no crash on reading stories, but changing the app from parent page and coming back caused a crash."

**Touches:** `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/LanguagePage.xaml.cs`, `tests/DinoReader.Tests/LifecycleSubscriptionTests.cs`, `.maestro/dino/settings-page-survives-background.yaml`, `docs/plan/slices/274-dino-ios-crash-after-leaving-parent-page.md`.

## What we know (read 2026-11-06 00:15 UTC)

- `node tools/crashes/pull-crashes.mjs --app dino --from 2026-11-05 --kind managed,native` shows **no report of this crash**: the newest iOS report is the 2026-11-05 08:25 `FinishedPlaying` group that Slice 270 fixed. Reports saved in `D:/ReadTheStupidText-worktrees/reviews/ios-crash-1106/`. So either the app has not been opened again since (a managed report is sent on the next launch), or iOS killed the app in a way the managed handler does not see (watchdog, `0xdead10cc` on suspension, a native crash); native reports reach the store later (Slice 243/244).
- "Parent page" is the grown-ups area behind the gate (`GatePage.xaml.cs` and the pages it opens). The user switched to another app there and came back.
- Related open work: Slice 244 item 5 (`0xdead10cc`, iOS killed the app for a file held open across suspension; cause unproven, the rolling log writer is the one handle kept open) and Slice 243 (crash report gaps). Do not duplicate them: if the cause is the same, fix it here and note it in 244.

## Step 1 result and the design (coordinator, 2026-11-06 ~01:20 UTC)

**The report (step 1 done).** Not in our R2 crash store (no managed exception). Found in TestFlight crash feedback (App Store Connect `betaFeedbackCrashSubmissions`, id `AJNdotU-acUVb8mtL0GzLow`, created 2026-11-06 00:08:32Z): Dino Reader 1.106.2 (20261105.204959), iPhone10,4 (iPhone 8 Plus), iOS 16.7.14, crashed 00:08:18 UTC in the foreground. `EXC_BAD_ACCESS (SIGSEGV)` `KERN_INVALID_ADDRESS` on the main thread: `lookUpImpOrNilTryCache` <- `_NSKeyValueObservationInfoGetObservances` <- `CA::Layer::begin_change` / `remove_sublayer` <- `-[UIView removeFromSuperview]` <- `-[UIView dealloc]` <- app frames (not symbolicated: CI keeps no dSYM). `dino_usage_beta`: session `d8c561f4` opened 00:06:40, its last batch (00:08:20) holds a store download and audio pack download; a new session opened 00:08:21. Files: `D:/ReadTheStupidText-worktrees/reviews/ios-crash-1106/` (`step1-summary.md`, `symbolicated.md`, the crash log JSON).

**Likely cause (step 2; matches the stack, not yet proven by a run).** A MAUI bug fixed upstream in dotnet/maui PR #38662 (merged 2026-09-30 for ".NET 10 SR12", **not in 10.0.120**, which we use and which is the newest on NuGet as of 2026-11-06; checked: the fix's files `KeyValueObservation.cs` and `MauiKeyValueObserver.swift` are absent at tag 10.0.120). On iOS 17 and older, KVO keeps an unretained pointer to its observer; MAUI's `ViewExtensions.OnLoaded` registers managed KVO observers (`bounds`, `frame`) on the layer of every platform view that is not `IUIViewLifeCycleEvents` (internal: `UIButton` for Button/ImageButton, `UISlider`, `UISwitch`, `UIStepper`, `UIProgressView`, `PlatformGraphicsView`, raw `UIImageView`/`UIScrollView`, web views) until the view loads. A view that is unloaded and then discarded (a page popped while the app is in the background, `App.xaml.cs` `OnStopped` -> `PopGrownUpPagesAsync`) keeps them until the GC frees view and observer in any order; `-[UIView dealloc]` then hits the freed observer. iOS 18+ zeroes the pointer, so only older iPhones crash (the user's iPhone 8 Plus is on 16.7). MAUI watches a view's load state only while someone subscribes to its `Loaded`/`Unloaded`; our grown-ups pages do that on exactly such controls: `SettingsRows.cs:646` (`slider.Unloaded +=`) and `DinoKeyTarget.cs:206` (`button.Unloaded +=`).

**The fix (built for the likely cause; proven only when the user's iPhone runs the new build with no crash):** the cause above is likely, not proven: it matches the crash stack and MAUI PR #38662, and the only symbolicated app frame, `_xamarin_release_managed_ref+0x64`, is the same frame as in that PR's stack (`D:/ReadTheStupidText-worktrees/reviews/ios-crash-1106/symbolicated.md`). A MAUI-internal or other observer could still be the real trigger on iOS 16. The next-day crash-store read and the iPhone check stay the only proof. No app code subscribes to `Loaded`/`Unloaded` on a view whose iOS platform view lacks MovedToWindow (Button, ImageButton, Slider, Switch, Stepper, ProgressBar, GraphicsView, WebView, Image, ScrollView, CheckBox, RadioButton, Entry/Editor are `MauiTextField`/`MauiTextView`: check each against MAUI 10.0.120 source before listing it safe). Each such subscription moves to its page's lifecycle (page `Disappearing`/`Unloaded`, pages are safe) or to a wrapping `ContentView` (platform view `MauiView`, which raises MovedToWindow: verify in 10.0.120 source). A unit test in `tests/DinoReader.Tests` scans the source of `src/DinoReader.App`, `src/ReadTheStupidText.Mobile.Shared` and `src/ReadTheStupidText.Mobile` for `Loaded +=`/`Unloaded +=` and fails on any receiver not in an allow-list of safe types/pages (each entry with a one-line reason), so new code cannot bring it back. Same fix in RTST Mobile if it has such subscriptions (same MAUI, same iPhones). Android is not affected by this bug (no KVO) but runs the same code. When MAUI ships a version with #38662 (10.0.130 or later), bump `MauiVersion` in `Directory.Build.props` in its own slice; this slice does not wait for it.

## Work

- [x] 1. **Find the report.** Re-run `pull-crashes.mjs --app dino --from 2026-11-05 --kind managed,native` (it needs wrangler's login: `npm ci` at the repo root first if `npx --no-install wrangler` is missing). If nothing new: read Apple's crash data for the TestFlight build through the App Store Connect API (TestFlight crash feedback submissions and their crash logs; the shared Apple key in `D:/secrets`, see memory/`reference-apple-account-shared`; check the endpoint with `web-research` first). Also read `dino_usage_beta` around 2026-11-06 00:00-00:15 UTC (`analytics-query`) for the session's last events before the crash. Write what each source showed here.
- [ ] 2. **Find the cause** from the report: the stack (managed) or the termination reason (native: `0xdead10cc`, watchdog `0x8badf00d`, memory). Read the grown-ups pages' `OnAppearing`/`OnDisappearing` and the app's `OnSleep`/`OnResume`/`Window` lifecycle handlers for what runs on leaving and returning on these pages and not on the reading page. A cause read from code stays "likely" until a run or a report shows it.
- [x] 3. **Fix it on iOS and Android** (same lifecycle code path on both; Android gets the same fix even if it does not crash there).
- [x] 4. **A check**: an emulator flow that opens the grown-ups area, sends the app to the background (`adb shell input keyevent KEYCODE_HOME` or the Maestro equivalent), brings it back and asserts the page is alive; the iOS check goes to the user's iPhone on the next TestFlight build (`unverified.md`), and the crash store is read the next morning.

## Verification

- `dotnet test tests/DinoReader.Tests`; the new emulator flow passes (and fails on the old code if the bug also shows on Android).
- The user's iPhone, next TestFlight build: grown-ups page, switch app, come back: no crash; `pull-crashes.mjs` the next day shows no new report for that build.

## Built (items 3-4), checked against MAUI 10.0.120 source

Every `Loaded +=`/`Unloaded +=` in `src/DinoReader.App`, `Mobile.Shared`, `Mobile` (RTST Mobile has none; the Windows-only `ControlPanelWindow` is out of scope). Unsafe = platform view lacks `IUIViewLifeCycleEvents` (`ViewExtensions.OnLoaded`, `src/Core/src/Platform/iOS/ViewExtensions.cs` line 742), so MAUI falls back to layer KVO.

| Receiver (file) | MAUI 10.0.120 platform view | Result |
| --- | --- | --- |
| Slider (`SettingsRows.cs`) | `UISlider` (`SliderHandler.iOS.cs`) | unsafe: moved to a wrapping `ContentView` |
| NavigationPage (`LanguagePage.xaml.cs` `WhenLoadedAsync`) | `NavigationRenderer : UINavigationController`, plain view | unsafe: now polls `IsLoaded` / the handler's platform view every 50 ms (up to 2 s), no subscription; same readiness meaning (the NavigationPage can take a push) |
| Border (`DinoKeyTarget.cs`) | `ContentView : MauiView` (`BorderHandler.iOS.cs`) | safe (and Windows only) |
| ContentView (`TileOverlay`), Grid (`WaitOverlay`; `LayoutView : MauiView`) | `MauiView` implements `IUIViewLifeCycleEvents` | safe |
| ContentPage (AboutPage, IntroWizardPage, SettingsPage) | `PageHandler` -> `ContentView : MauiView` | safe |
| Not subscribed here, for the allow-list's record: Button/ImageButton `UIButton`, Switch `UISwitch`, Stepper `UIStepper`, ProgressBar `UIProgressView` | no `IUIViewLifeCycleEvents` | unsafe |
| MauiLabel, MauiTextField (Entry), MauiTextView (Editor), MauiCheckBox, MauiImageView, MauiScrollView, MauiWKWebView | listed implementers of `IUIViewLifeCycleEvents` | safe |

Guard: `tests/DinoReader.Tests/LifecycleSubscriptionTests.cs` fails on any subscription whose "file:receiver" is not in its allow-list (each entry with a reason) or when an entry is stale; it also scans `.xaml` files for `Loaded=`/`Unloaded=` attributes and keeps dotted receivers whole (`row.Slider`). Check: `.maestro/dino/settings-page-survives-background.yaml` (written, not run): the correct screen after coming back is the kids' shelf (OnStopped leaves grown-up mode and pops the grown-ups pages), and the debug line `[GrownUps] closed on stop` (logged after that pop) must appear, which a crash or restart would not produce. The Slider's `Unloaded` now sits on its ContentView host; whether MAUI raises it when a page is popped in the background is not covered by a test (finding 5, skipped).

Changed while building:
- `App.xaml.cs`: Debug-only log `[GrownUps] closed on stop`, written after a successful pop in the stop handler (the flow's marker).
- `LanguagePage.xaml.cs` `WhenLoadedAsync` ended as a poll of `navigation.IsLoaded` only (50 ms, 2 s cap), by the coordinator's design after review; the handler-platform-view fallback was dropped because it is weaker than the old wait.
- Flow `.maestro/dino/settings-page-survives-background.yaml` needed a dismissal-step fix before it passed.

From review (files outside the repo, read in full):
- High (`reviews/s274-high.md`, 7 findings): applied 1 (wording "likely", crash-store check stays the proof), 2 (guard keeps dotted receivers whole and scans `.xaml` `Loaded=`/`Unloaded=`), 4 (flow assertion), 6 (the `LanguagePage` wait). Skipped 3 (allow-list keyed by file and receiver name, not type: a known limit of a source scan, entries carry reasons), 5 (no runtime test that MAUI raises `Unloaded` on the ContentView host when a page is popped in the background: covered only by the emulator run), 7 (a shared helper or the MAUI bump later).
- Medium (`reviews/s274-medium.md`, 7 findings): applied 1 (`IsReadyToPush` now `IsLoaded` only, coordinator design) and 2 (marker comment). Skipped 3 and 4 (same source-scan limit as high 3; misses `AddHandler` and lambdas), 5 (ContentView wrapper's layout and accessibility effect, low), 6 (wall-clock 2 s wait without a token, bounded), 7 (Debug-only test hook, like the repo's others).
- Check pass 1 (`reviews/s274-check1.md`, 8 findings): all skipped. 1-3: the marker is Debug only; the restart question is settled by the app's pid staying the same across home/return. 8: no iOS check can run here, so it is in Not yet verified. 4-7: low.
- Check pass 2 (`D:/ReadTheStupidText-worktrees/reviews/s274-check2.md`, final code after the flow fix): 6 findings, all low or info, all skipped (1 Debug-only marker; 2-3 known limits of the source scan; 4 bounded 2 s poll; 5 covered by the emulator slider run; 6 the iOS check is under Not yet verified).

Verified on the Android emulator (emulator-5554): the flow passes (pid 11741 unchanged across home and return); slider drag then home and back passes (pid 9810, no FATAL); changing the language to French then reopening Settings passes.

#### For CLAUDE.md (lifted 2026-11-06)

- `src/DinoReader.App/CLAUDE.md`: never subscribe `Loaded`/`Unloaded` on a view whose iOS platform view lacks MovedToWindow (Slider, Button, ImageButton, Switch, Stepper, ProgressBar, GraphicsView, NavigationPage); use the page or a ContentView wrapper. `tests/DinoReader.Tests/LifecycleSubscriptionTests.cs` enforces it. (Slice 274: docs/plan/slices/274-dino-ios-crash-after-leaving-parent-page.md)

#### Not yet verified

- The user's iPhone 8 Plus (iOS 16) on the next TestFlight build: grown-ups page (Settings > Reading speed), switch app, come back: no crash.
- The next morning, `pull-crashes.mjs` and TestFlight crash feedback show no new report for that build.
- The iOS cause is only likely (stack matches dotnet/maui PR #38662; the one symbolicated app frame is `_xamarin_release_managed_ref+0x64`). CI keeps no dSYM, so the stack cannot be symbolicated further.

#### Follow-ups

- Bump `MauiVersion` when a MAUI release with dotnet/maui PR #38662 ships (10.0.130 or later), in its own slice. Until then MAUI's own `Loaded` watchers (Buttons etc.) stay exposed.
- Keep iOS dSYMs per release so crashes can be symbolicated.
