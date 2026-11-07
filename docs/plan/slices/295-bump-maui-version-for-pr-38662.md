# Slice 295 — Bump `MauiVersion` once a MAUI release with dotnet/maui PR #38662 ships

*Planned 2026-11-07; starts only when the release exists.* From [Slice 274](274-dino-ios-crash-after-leaving-parent-page.md): the iOS crash after leaving a parent page has a local workaround; the underlying fix is dotnet/maui PR #38662, which needs 10.0.130 or later. 10.0.120 is the newest on NuGet as of 2026-11-06 (`Directory.Build.props:7` pins `10.0.120`). iOS dSYMs per release are Slice 243's, not this slice's. Moved from plan.md's *Outstanding work*. Nothing is filed upstream (user rule: nothing leaves this machine as a report).

**Touches:** `Directory.Build.props`, `src/DinoReader.App/DinoReader.App.csproj`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `tests/DinoReader.Tests/LifecycleSubscriptionTests.cs`, `docs/plan/slices/295-bump-maui-version-for-pr-38662.md`

## Work

- [ ] 1. Check NuGet for `Microsoft.Maui.Controls` 10.0.130 or later (read it that day, `dotnet package search` or the NuGet API). If absent, leave the slice planned and say so.
- [ ] 2. Context7 / `library-docs` for the release notes (confirm PR #38662 is in it), then bump `MauiVersion` in `Directory.Build.props` (the only place).
- [ ] 3. Android and iOS: build both Dino and RTST heads; run the emulator flows and an iOS build (compile, plus the TestFlight check for the Slice 274 case: leave the language page, background, return).
- [ ] 4. Decide from the fix whether Slice 274's workaround in `SettingsRows.cs` / `LanguagePage.xaml.cs` can go; if the fix is unproven keep it.

## Verification

- Both apps build on all heads on the new version; fast tests green; the Slice 274 flow (`settings-page-survives-background`) passes on the emulator.
