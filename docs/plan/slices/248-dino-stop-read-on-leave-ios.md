### Slice 248 — Dino iOS: leaving a book stops its read, and a new book never drives the old one's voice

*In progress (2026-11-02).* A bug the user reported on iOS dino-v1.94.0 (2026-11-02): "I played a story, then came back, it still continued playing. it should have stopped. then I opened another book, and clicked on pause. It paused the voice. I clicked on play again and it resumed the book1 instead of starting on book2. I tried with book 3, the same condition continued."

**Touches:** `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSession.cs` (only if needed), `tests/DinoReader.Tests/**` (only if a pure helper is extracted), `.maestro/dino/**` (one new or extended flow), `docs/plan/slices/248-dino-stop-read-on-leave-ios.md`

#### Cause (read in code and MAUI 10.0.120 source, 2026-11-02; not yet seen on a device log)

- Each `ReadingPage` has its own transient `ReadingSession` (`MauiProgram.cs:153-154`), but every session drives the one shared `ISpeechReader` singleton (`MobileSharedServices.cs:29`).
- `ReadingPage.OnDisappearing` (`ReadingPage.xaml.cs:806`) stops the read one dispatcher tick later, only if `Navigation.NavigationStack` no longer contains the page.
- On iOS, MAUI's `NavigationPage` still uses the legacy `NavigationRenderer` (`NavigationPage.cs:60-64`, `UseMauiHandler = false`). `PopAsync` fires `OnDisappearing` first and removes the page from the stack only after the pop animation finishes (`NavigationPage.Legacy.cs` `RemoveAsyncInner`: `FireDisappearing`, then `await args.Task`, then `RemoveFromInnerChildren`). One tick later the page is still on the stack, so nothing stops. Our Back chip (`OnBackTapped`, `PopAsync`) takes this path. On Android the stack is updated synchronously with `OnDisappearing`, so Android stops. iOS swipe-back removes the page before `OnDisappearing`, so it is not affected.
- Book 2's session then sees the shared reader `Playing` (book 1's text): its button shows Pause, Pause pauses book 1's voice, Play resumes it (`ReadingSession.Pause/Resume` act on `_reader.State`).

#### Work

- [x] **a. Stop on the real leave.** Override `OnNavigatedFrom` in `ReadingPage`: by then the stack is updated on every platform (MAUI raises it after the removal for `PopAsync` on iOS and Android). Run the same leave cleanup there when the page is no longer in `Navigation.NavigationStack`. Keep the `OnDisappearing` check (it covers iOS swipe-back and any path where `OnNavigatedFrom` is skipped). Move the cleanup into one idempotent `LeavePage()` (guarded by `_onStack`, so it runs once). A push over the page (the grown-ups' gate), a locked screen or another app still never stops the read.
- [x] **b. A new book never inherits another book's voice.** On the page's first `OnAppearing`, before `_appeared.TrySetResult()` (the intro read's `PlayWhenShownAsync` continues on it), stop whatever the shared reader and recording player are doing (`_session.Stop()`), so the new book's controls start Idle and Play reads this book. Later `OnAppearing` calls (back from the gate) must not stop.
- [x] **c. Check.** A scripted emulator flow (Android): open a book, play, leave with the Back chip, assert the read stopped (reader Idle in the test hook log or `TestHookLog` line); open a second book, assert its control shows Play (not Pause), tap Play, assert the second book's first sentence is read. Android never had bug a, so this flow proves b and that a did not break Android; iOS goes under Not yet verified.

#### Not yet verified

- iOS (TestFlight build after the merge): play a book, tap Back: the voice stops. Open another book: its button shows Play; Play reads that book. Swipe back from a playing book: the voice stops.

#### Built (2026-11-02)

- `ReadingPage.xaml.cs`: `OnNavigatedFrom` and `OnDisappearing` both call `LeavePageIfOffStack()`, which runs the one `LeavePage()` cleanup only while `_onStack` is set (so it runs once); `StopInheritedReadOnFirstAppearing()` calls `_session.Stop()` on the first `OnAppearing`, before `_appeared` is set. Debug log lines: `[Reading] left the page, read stopped`, `[Reading] opened idle, inherited read stopped`.
- `IRecordingPlayer` is transient (one per session), so item b cannot stop another page's recording; item a stops each page's own player when it leaves. `Stop()` on a fresh session only calls the shared reader's `Stop` (idle: harmless). The intro demo read starts after `_appeared`, so it starts after the stop.
- New flow `.maestro/dino/leave-book-stops-read.yaml` (not yet run on a device; needs a speaking voice for the Pause wait). Android and iOS compile; DinoReader.Tests 2243 pass; flow lint clean.

- From review: high: applied 1 (flow keeps the voice pack, no clearState), 2 (DEBUG log `[Reading] read starts story=<id>`, flow expects it for good-enough), 4 (OnBackTapped calls LeavePageIfOffStack after the pop). Skipped 3: on Android the flow can only prove no regression. Skipped 5: per MAUI push order the hook read starts after Appearing.
- From review: medium (`reviews/s248-medium.md`): applied 3 (dropped the `reader=Idle` expect-log, which earlier STATE lines also match; the unique `left the page` line stays) and 4 (`OnBackTapped` cleans up in a `finally` and logs a failed pop instead of throwing from async void). Skipped 1: stopping whatever the shared reader plays when a book opens is the intent (a voice sample from the grown-ups' pages should stop too). Skipped 2: once the page is off the stack, `Contains(this)` can only be false, so the stop runs; a stale proxy cannot keep it on.
- Emulator (Android, 2026-11-02): `first-run-pack-start` and `leave-book-stops-read` pass (105.7 s, no crash). The first run failed only on the flow's own marker (it logged the story id, a hash, not the slug); the marker now logs the title slug, and the rerun passed. On Android this proves item b and that item a did not break leaving; the iOS bug itself is checked only on TestFlight (Not yet verified).
- Usage data: TestFlight installs send no usage rows (`UsageGate.cs:121-126`, Slice 179) and Dino has no leave/pause/play event (`UsageEvent.cs:9-27`), so the analytics cannot show this bug; checked 2026-11-02, no iOS 1.94.0 rows.
