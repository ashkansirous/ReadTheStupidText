# Slice 276 — Dino iPhone: the "Update needed" button opens TestFlight for TestFlight installs

*In progress 2026-11-06.* Bug fix, Dino Reader, iOS. Found while preparing the user's request (2026-11-06): set Dino's iOS `minVersion` to 1.106.8 once that build is approved for the External testing group.

**Touches:** `src/DinoReader.App/Platforms/iOS/StoreUpdater.ios.cs`, `src/DinoReader.App/UpdatePage*.cs` (button text only, if needed), `src/DinoReader.App/Resources/Strings/**` (only if the text changes), `tests/DinoReader.Tests/**`, `docs/plan/slices/276-dino-ios-update-button-testflight.md`.

## Why

- Dino on iPhone is only on TestFlight; it was never submitted to the App Store (`plan.md` *Outstanding work*, App Store line).
- The forced-update page's button opens the App Store page (`StoreUpdater.ios.cs:8`, `https://apps.apple.com/app/id6825053435`). For a TestFlight install that page has no app, so once `ios.minVersion` is raised, a tester on an old build sees "Update needed" with a button that leads nowhere, until TestFlight updates the app on its own.
- The app already knows its install channel (`store` or `testflight`: the crash reports and `UsageCounter`'s beta route use it).

## Work

- [ ] On iOS, a TestFlight install's update button opens the TestFlight app on Dino's page (`itms-beta://` / the TestFlight link for the app; check the scheme with `library-docs` or `web-research` first); an App Store install keeps the App Store page. The button text says what it opens ("Open TestFlight" vs "Open the App Store"), in every app language.
- [ ] Unit test of the pure choice (channel → link and text).
- [ ] Android: no change (Play internal and closed tracks open the same Play page; checked, not changed). Windows: no change.

## Verification

- `dotnet test tests/DinoReader.Tests`; iOS compiles in CI.
- The user's iPhone, once `minVersion` is set: an old TestFlight build shows the page and its button opens TestFlight (`unverified.md`).
- Gate for the user's request: `ios.minVersion` in the live Dino config is raised only after this slice has shipped to the External testing group.
