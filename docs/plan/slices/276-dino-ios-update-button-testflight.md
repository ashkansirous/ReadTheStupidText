# Slice 276 — Dino iPhone: the "Update needed" button opens TestFlight for TestFlight installs
**Product:** Dino

*Built 2026-11-06, merged as PR #853 (83314dd6).* Bug fix, Dino Reader, iOS. Found while preparing the user's request (2026-11-06): set Dino's iOS `minVersion` to 1.106.8 once that build is approved for the External testing group.

**Touches:** `src/DinoReader.App/Platforms/iOS/StoreUpdater.ios.cs`, `src/DinoReader.App/UpdatePage*.cs` (button text only, if needed), `src/DinoReader.App/Resources/Strings/**` (only if the text changes), `tests/DinoReader.Tests/**`, `docs/plan/slices/276-dino-ios-update-button-testflight.md`.

## Why

- Dino on iPhone is only on TestFlight; it was never submitted to the App Store (`plan.md` *Outstanding work*, App Store line).
- The forced-update page's button opens the App Store page (`StoreUpdater.ios.cs:8`, `https://apps.apple.com/app/id6825053435`). For a TestFlight install that page has no app, so once `ios.minVersion` is raised, a tester on an old build sees "Update needed" with a button that leads nowhere, until TestFlight updates the app on its own.
- The app already knows its install channel (`store` or `testflight`: the crash reports and `UsageCounter`'s beta route use it).

## Work

- [x] On iOS, a TestFlight install's update button opens the TestFlight app on Dino's page (`itms-beta://` / the TestFlight link for the app; check the scheme with `library-docs` or `web-research` first); an App Store install keeps the App Store page. The button text says what it opens ("Open TestFlight" vs "Open the App Store"), in every app language.
- [x] Unit test of the pure choice (channel → link and text).
- [x] Android: no change (Play internal and closed tracks open the same Play page; checked, not changed). Windows: no change.

#### Design (Dino Reader Batch 48 mock, frame 187, added 2026-11-06)

One frame (187, line 376 of `docs/design/dino-reader/Dino Reader Batch 48 Store Only and Recording.dc.html`, 360 x 760). It agrees with the built slice. Its caption: "Frame 61 with only the button text changed (PR #853)".

- **Update page, TestFlight install (187).** Title "Dino needs an update" (28px, 600). Body "Ask a grown-up to get the new version. Your books and recordings stay just as they are." (14.5px, `#5a5470`, line-height 1.45, max 280px). One full-width button pinned to the bottom: 56px high, radius 28, fill `#FF7A59`, shadow `0 5px 0 #E2583A`, white 17px 600 text. The 150px icon block with the 120px app icon is unchanged from frame 61.
- **Button text.** "Open TestFlight" for a TestFlight install or one whose channel can't be read; "Open the App Store" for a real App Store install. Android and Windows keep "Update Dino". Matches `IosUpdateTarget` below.
- **Settings row.** The caption says the Settings "A new version is ready" row uses the same link and text. Matches the review note below.
- **Simplified:** status bar ("9:41", "100%") and phone outline are illustration.
- No new strings; no open points.

## Review notes

- Link: Dino's public TestFlight link `https://testflight.apple.com/join/WnF7M53D` (the one on `site/dino/index.html`), one constant in `IosUpdateTarget`. Only a positively App Store install opens the App Store page; TestFlight and unreadable channels open TestFlight (Dino has no App Store app yet).
- The Settings "A new version is ready" row shares `StoreUpdater.OpenStoreAsync`, so it now also opens TestFlight for TestFlight installs (intended).
- Review finding 4 (catch filter only covers `InvalidOperationException`/`FeatureNotSupportedException`, pre-existing): not changed.
- Review finding 6 (tests cover only the channel-to-link mapping, not the channel source or link validity): not changed; link validity is a device check.

## Verification

- `dotnet test tests/DinoReader.Tests`; iOS compiles in CI.
- The user's iPhone, once `minVersion` is set: an old TestFlight build shows the page and its button opens TestFlight (`unverified.md`).
- Gate for the user's request: `ios.minVersion` in the live Dino config is raised only after this slice has shipped to the External testing group.
