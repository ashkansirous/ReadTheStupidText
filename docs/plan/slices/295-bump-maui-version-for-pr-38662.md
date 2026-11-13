# Slice 295 — Bump `MauiVersion` once a MAUI release with dotnet/maui PR #38662 ships
**Product:** Shared

**Status (2026-11-13): built (merged as PR #1050, a5310321), not verified; do not re-park.** MAUI 10.0.130 is on NuGet (read 2026-11-13 11:50Z) and its release notes list #38662 as a cherry-pick (`gh api repos/dotnet/maui/releases/tags/10.0.130`, line 695, read by library-docs); `MauiVersion` is bumped in `Directory.Build.props`. Dino emulator flows pass on the new version; the TestFlight check is still owed, and the Slice 274 workaround stays until it passes (work item 5).

*History (2026-11-07 to 2026-11-12, all resolved):* planned 2026-11-07 and parked by several night runs as an external wait while the newest NuGet release was 10.0.120 (11.0 was rc.2; #38662 merged 2026-09-30 into `inflight/current`, milestone ".NET 10 SR12", merge commit 334edc6b not in 10.0.120 or 11.0.100-rc.2). User, 2026-11-08: "keep it in the plan until we get the update". From [Slice 274](274-dino-ios-crash-after-leaving-parent-page.md): the iOS crash after leaving a parent page has a local workaround; the underlying fix is dotnet/maui PR #38662. iOS dSYMs per release are Slice 243's. Nothing is filed upstream (user rule: nothing leaves this machine as a report).

**Touches:** `Directory.Build.props`, `src/DinoReader.App/DinoReader.App.csproj`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `tests/DinoReader.Tests/LifecycleSubscriptionTests.cs`, `docs/plan/slices/295-bump-maui-version-for-pr-38662.md`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/LanguagePage.xaml.cs` (item 4 only)

## Work

- [x] 1. Check NuGet for `Microsoft.Maui.Controls` 10.0.130 or later (read it that day, `dotnet package search` or the NuGet API). If absent, leave the slice planned and say so. (Done 2026-11-13: 10.0.130 is on NuGet.)
- [x] 2. Context7 / `library-docs` for the release notes (confirm PR #38662 is in it), then bump `MauiVersion` in `Directory.Build.props` (the only place). (Done 2026-11-13: release notes list #38662; bumped to 10.0.130.)
- [ ] 3. Android and iOS: build both Dino and RTST heads; run the emulator flows and an iOS build (compile, plus the TestFlight check for the Slice 274 case: leave the language page, background, return). (Fast checks done 2026-11-13: Dino and RTST build on net10.0-android and net10.0-ios from Windows, 0 errors; DinoReader.Tests 7005 passed, ReadTheStupidText.Tests 2221 passed. Windows heads built 2026-11-13 on the new version: Dino `dotnet build src/DinoReader.App -f net10.0-windows10.0.19041.0 -p:Platform=x64 -p:IncludeAndroid=false --disable-build-servers` 0 errors (11 warnings, existing CS8601 nullability); RTST `dotnet build src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64 --disable-build-servers` 0 errors, 0 warnings. Emulator run 2026-11-13 on emulator-5554, cached Dino build c4f1a8ac5479: `settings-page-survives-background` PASS (80.1 s), `open-book` PASS (46.2 s), no FATAL/AndroidRuntime/ANR lines; logs in `D:/ReadTheStupidText-worktrees/reviews/w52-device/` (`flow-settings.log`, `flow-open-book.log`, `dino-settings-page-survives-background.log`, `dino-open-book.log`). Still owed: the RTST emulator flows on 10.0.130 and the TestFlight check.)
- [x] 4. Decide from the fix whether Slice 274's workaround in `SettingsRows.cs` / `LanguagePage.xaml.cs` can go; if the fix is unproven keep it. (Decided 2026-11-13: the fix is unproven on a device until TestFlight, so the workaround stays untouched; remove it in a later slice once TestFlight shows the crash gone.)
- [ ] 5. Remove the Slice 274 workaround (`SettingsRows.cs`, `LanguagePage.xaml.cs`, and its `LifecycleSubscriptionTests.cs` allow-list entries, re-checking the allow-list against the MAUI 10.0.130 source) only after the TestFlight check in *Not yet verified* passes. Until then the slice stays "built, not verified".

## Verification

- Both apps build on all heads on the new version; fast tests green; the Slice 274 flow (`settings-page-survives-background`) passes on the emulator (Dino: passed 2026-11-13).

## Not yet verified

- RTST emulator flows on MAUI 10.0.130: not reached in the one emulator run (Dino only).
- TestFlight iPhone check of the earlier Slice 274 crash path on a release build: open a parent page, leave it (language page), background, return; no crash. Needs a TestFlight build after merge. Work item 5 waits on it.

## From review

- Skipped #1 (`reviews/s295-high.md`): "CI workload sets still pinned for 10.0.120." - MauiVersion is a NuGet override; library-docs read the 10.0.130 nuspec: same TFMs and deps as 10.0.120, no new workload manifest exists; the PR's CI on every head is the proof.
- Skipped #4 (`reviews/s295-high.md`): "the update-docs index pass on a chore/docs-index-* branch must change the row" - plan.md changes only in the index pass.
- Skipped #5 (`reviews/s295-medium.md`): "Work item 3 unchecked, yet Verification requires emulator flow settings-page-survives-background; only compile/unit tests ran" - the Dino emulator flow has since run and passed (2026-11-13, see item 3); the RTST flows remain in *Not yet verified*.
- Skipped #5 (`reviews/s295-high.md`): "The allow-list was not re-checked against 10.0.130" - the comment is outside the diff; the allow-list is re-checked when the workaround is removed (work item 5).
- Skipped #1 (`reviews/s295-check.md`): "comment claims #38662 cherry-picked into 10.0.130; check not shown" - low; the 10.0.130 release notes list #38662 (`gh api repos/dotnet/maui/releases/tags/10.0.130`, line 695, read by library-docs).
- Skipped #2 (`reviews/s295-check.md`): "unverified on device/CI (work item 3 unchecked, emulator flow and TestFlight owed)" - low; the Dino emulator run now covers the flow, the rest is in *Not yet verified*.
- Skipped #3 (`reviews/s295-check.md`): "false conflicts in parallel-check" - low; Touches keeps `SettingsRows.cs` and `LanguagePage.xaml.cs` because the open removal item (work item 5) will edit them.
- Applied: high #2, #3, #6; medium #1, #2, #3, #4. Review findings files read: s295-high.md 6/6, s295-medium.md 5/5, s295-check.md 3/3.
