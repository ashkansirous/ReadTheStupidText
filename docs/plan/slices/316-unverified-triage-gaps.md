# Slice 316: Gaps found by the unverified-checks triage

*Planned 2026-11-08.* The [triage of `unverified.md`](../notes/2026-11-08-unverified-triage.md) found lines that were not device checks but real missing pieces. They move here so they get built instead of waiting for a device run that can never close them. Slice 307 item 10 (`phone-network-guard.mjs`) was also one; it stays in plan.md's *Outstanding work*, where it is already tracked.

**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `.maestro/**`, `tools/phone/run-checks.ps1`, `tools/premade-audio/README.md`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/Platforms/Windows/**`, `tests/ReadTheStupidText.Tests/ReadAloudServiceGeneratingTests.cs`, `.github/scripts/ship-targets.mjs`, `docs/plan/slices/228-*.md`, `docs/plan/slices/233-*.md`

## Items

Android and iOS: items 1-3 and 7 are shared MAUI code; each scripted check gets an Android flow and the matching iOS flow, as Platform parity requires.

1. **RTST Android timer** (Slice 60): the elapsed label can show `elapsed > total` and is not reset on idle (`ReaderPage.xaml.cs` `ElapsedLabel`). Fix it and add a unit test for the clamp and the reset. Check whether iOS shares the bug.
2. **Mid-book Farsi read start** (Slice 296): `ClipOffsetAsync` for a read that starts mid-book is not modelled or run. Add a test that starts on a later page and checks the clip match count.
3. **Failure-path log lines** (Slice 267): the `read_voice` failure-path lines carry no message or URL. Add them, so a fallback in production names what failed.
4. **Missing scripted checks**: Review-pick-then-Read (Slice 304); the phone flow asserting that the tablet title stays hidden (Slice 242 item 6); the alert-versus-silent re-post on a repeated failed automatic resume (Slice 233).
5. **Tooling printouts**: `tools/phone/run-checks.ps1` `Stop-CacheMissed` prints the inputs that missed (Slice 294); document the `exit_code_for` exit rule in `tools/premade-audio/README.md` (Slice 259).
6. **Unchecked review skip reasons** (Slices 228 and 233): the code facts the skip reasons state ("Android's Notify reports no failure"; no modal over the reader; Dismiss refresh before `_mode.Leave`; Idle means no download; Worker before app). Read each one in the code. A false reason reopens its finding as a fix in this slice.
7. **Dino Windows window** (Slice 235): a window clamped to a small screen and saved as maximised reopens maximised. Decide: reopen at the clamped normal size, which is the obvious behaviour unless the code says otherwise.
8. **Release paths** (Slice 58): check that a Dino-only project change in `ReadTheStupidText.slnx` or the workflows no longer re-releases RTST (`ship-targets.mjs` core paths). Fix the path list if it still does.
9. **Flaky test**: `ReadAloudServiceGeneratingTests.Auto_read_prepares_the_head_start_before_the_settle_delay_ends` (timing; failed once in CI on 2026-10-21). Make it deterministic with a fake clock, or close it if it has not failed in CI since.

## Not yet verified

(Filled in when built.)
