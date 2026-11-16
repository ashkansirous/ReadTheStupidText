# Slice 401 — Dino iOS: declare the app's 15 languages so the App Store lists them
**Product:** Dino

*Built 2026-11-16, merged as PR #1212 (b95701ba), not yet verified on the App Store (see Not yet verified). Planned and started 2026-11-16 at the user's "do it". Dino Book Reader 1.131.1 went live on the App Store on 2026-11-16 with "Language: EN" only (iTunes lookup `languageCodesISO2A` = `["EN"]` in us, gb, se, de), although the store listing and the app's screens are translated into 15 languages.*

**Touches:** `src/Dino/DinoReader.App/Platforms/iOS/Info.plist`, `src/Dino/DinoReader.App/Platforms/iOS/Resources/*.lproj/InfoPlist.strings` (new), `src/Dino/DinoReader.App/DinoReader.App.csproj`, this slice file.

## Why

The App Store's "Languages" line comes from the localizations inside the app binary, not from the App Store Connect listing (App Store Connect help, "Localize app information": listing languages are metadata, "this differs from adding languages to your app in Xcode when you localize your binary"). Dino's `Info.plist` has no `CFBundleLocalizations` and the bundle has no `.lproj` folders, so Apple sees English only. Read The Stupid Text's iOS app already does this (`src/Rtst/ReadTheStupidText.Mobile/Platforms/iOS/Info.plist`, Slices 53 and 55); copy that pattern.

Likely side effect (not checked on a device): without the list, iOS may also treat the app as English only when it picks the app's language.

## Work

- [x] 1. `Info.plist`: add `CFBundleLocalizations` with the same 15 codes as RTST's (`en ar es fa fr ja da fi sv nb nl de pt ru zh-Hans`), keep `CFBundleDevelopmentRegion` `en`, with a comment naming this slice.
- [x] 2. One `Platforms/iOS/Resources/<code>.lproj/InfoPlist.strings` per code, holding only `NSMicrophoneUsageDescription`: the English text from `Info.plist` and a natural translation of it in each other language (same meaning: a parent can record their own voice reading a page; the recording stays on the device and is never uploaded). UTF-8.
- [x] 3. `DinoReader.App.csproj`: list each file as a `BundleResource` with `LogicalName="<code>.lproj/InfoPlist.strings"` in the iOS-only item group, as RTST's csproj does.
- [x] 4. A unit test (in `tests/Dino/DinoReader.Tests` or where the repo already checks plist/csproj files) that the 15 codes in `CFBundleLocalizations` match the app's `AppResources.*.resx` languages and that each has its `.lproj` file listed in the csproj.

## Verification

- CI's iOS build passes and the uploaded TestFlight build carries the 15 `.lproj` folders.
- After the next App Store release: `curl "https://itunes.apple.com/lookup?id=6825053435&country=gb"` shows 15 codes in `languageCodesISO2A`.
- On an iPhone set to German, the microphone prompt on the Record screen is in German.

## Not yet verified

- The App Store line (needs the next version reviewed and released).
- The German microphone prompt on a real iPhone.

## Built (2026-11-16)

Items 1-4 built on `fix/dino-ios-langs`. The new test `tests/Dino/DinoReader.Tests/IosAppLanguagesTests.cs` was not run locally (this PC has only the .NET 10 SDK); CI's `tests.yml` runs it on the PR.

## Review

- Pass 1 (high), `reviews/401-pass1.md`: fixed 1 (Brazilian Portuguese), 2 (nb grammar), 3 and 4 (stricter tests), 6 (ru wording), 7 (comments no longer state the store line as fact). Skipped 5 (own `RepoRoot()` helper beside `StarterBuilder.RepoRoot()`): tidiness only.
- Pass 2 (medium), `reviews/401-pass2.md`: 4 low findings, all skipped. Notably 2: `zh-Hans` is declared while the app's resx is plain `zh`, so Traditional Chinese devices fall back to English, same as RTST today.
- Device run: none possible for this change from this PC (iOS only); the checks stay under *Not yet verified*.
