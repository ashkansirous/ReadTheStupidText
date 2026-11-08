# Slice 295 — Bump `MauiVersion` once a MAUI release with dotnet/maui PR #38662 ships

*Parked (night 2026-11-08-1703 wave 1): waits for MAUI 10.0.130 or later on NuGet (newest 10.0.120 at 2026-11-08 17:04Z).*

*Planned 2026-11-07; starts only when the release exists.* **Park note (2026-11-07, night run wave 5):** parked: waits for MAUI 10.0.130 or later on NuGet; newest was 10.0.120 (checked 2026-11-07 ~20:20Z). An external wait, not a user item. From [Slice 274](274-dino-ios-crash-after-leaving-parent-page.md): the iOS crash after leaving a parent page has a local workaround; the underlying fix is dotnet/maui PR #38662, which needs 10.0.130 or later. 10.0.120 is the newest on NuGet as of 2026-11-06 (`Directory.Build.props:7` pins `10.0.120`). iOS dSYMs per release are Slice 243's, not this slice's. Moved from plan.md's *Outstanding work*. Nothing is filed upstream (user rule: nothing leaves this machine as a report).

**User, 2026-11-08:** "keep it in the plan until we get the update". The user sees no 10.0.130 planned; the next listed is 11.0. Read 2026-11-08 ~01:10Z: dotnet/maui #38662 merged 2026-09-30 into `inflight/current`, milestone ".NET 10 SR12"; its merge commit 334edc6b is not an ancestor of tags 10.0.120 or 11.0.100-rc.2 (GitHub compare: diverged), and 10.0.120's release notes don't mention #38662. Not checked: whether it was cherry-picked under another commit. NuGet newest: 10.0.120 and 11.0.0-rc.2.26505.5. Read 2026-11-08 03:29Z: NuGet newest still 10.0.120, so the slice stays planned. A night run checks NuGet (and an 11.0 GA release) and leaves this slice planned if neither carries the fix.

**Park note (2026-11-08, night 2026-11-08-0106 wave 3):** NuGet newest 10.0.x still 10.0.120 at 05:03Z, 11.0 still rc.2. Parked for the rest of the night (external wait, not a user item); the night's final feedback loop moves it back to planned, as the user asked. Done: moved back to planned by the night 2026-11-08-0106 feedback loop (2026-11-08).

**Touches:** `Directory.Build.props`, `src/DinoReader.App/DinoReader.App.csproj`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `tests/DinoReader.Tests/LifecycleSubscriptionTests.cs`, `docs/plan/slices/295-bump-maui-version-for-pr-38662.md`

## Work

- [ ] 1. Check NuGet for `Microsoft.Maui.Controls` 10.0.130 or later (read it that day, `dotnet package search` or the NuGet API). If absent, leave the slice planned and say so.
- [ ] 2. Context7 / `library-docs` for the release notes (confirm PR #38662 is in it), then bump `MauiVersion` in `Directory.Build.props` (the only place).
- [ ] 3. Android and iOS: build both Dino and RTST heads; run the emulator flows and an iOS build (compile, plus the TestFlight check for the Slice 274 case: leave the language page, background, return).
- [ ] 4. Decide from the fix whether Slice 274's workaround in `SettingsRows.cs` / `LanguagePage.xaml.cs` can go; if the fix is unproven keep it.

## Verification

- Both apps build on all heads on the new version; fast tests green; the Slice 274 flow (`settings-page-survives-background`) passes on the emulator.
