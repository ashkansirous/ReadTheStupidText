# Slice 160 — Translation on iOS (Android and iOS parity)

*Planned.* RTST Mobile and Dino Reader, iOS.

**Touches:** `src/ReadTheStupidText.Mobile.Shared/ReadTheStupidText.Mobile.Shared.csproj`,
`src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/**` (new `MlKitTranslator.cs`, translation packs),
`src/ReadTheStupidText.Mobile.Shared/MobileSharedServices.iOS.cs`,
`src/ReadTheStupidText.Mobile/Platforms/iOS/MauiProgram.iOS.cs`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`,
`src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`,
`src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/StoryTranslator.cs`,
`src/DinoReader.App/**/ToggleSwitch*.cs`, `.github/workflows/*ios*` (only if the binding needs a build change),
`docs/plan/slices/55-ios-batch9-features.md`, `docs/plan/slices/159-dino-everything-in-my-language.md`,
`docs/plan/decisions.md`, `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`

## Why

The user tested the iOS app on 2026-10-27 and found translation missing: "both apps should work exactly the same in
both platforms. It is a rule everywhere for me." The rule is now in the user's shared instructions (ClaudeSkills PR #54):
**Android and iOS are the same app.** It covers Android and iOS only; the Windows, macOS and Linux heads are out of scope.

Grounding (origin/main 9c4ec2a):

- Translation is the only hard iOS gap. On Android, `MlKitTranslator` (`Mobile.Shared/Platforms/Android/MlKitTranslator.cs`,
  package `Xamarin.Google.MLKit.Translate` in the Android-only ItemGroup, `Mobile.Shared.csproj:65`) is registered in
  `MobileSharedServices.Android.cs:18`. iOS has no class, registration or package, so:
  - RTST: the Review page's translate card is hidden (`ReviewPage.xaml.cs:297`; `MauiProgram.iOS.cs` says so).
  - Dino: own, typed and scanned books are never translated; `StoryTranslator` finds no `ITranslator` (`StoryTranslator.cs:28`).
    Store books still translate from their bundled tables.
- Slice 55 (`55-ios-batch9-features.md:104-112`) left it out on purpose (Apple Translation is Swift-only and has no
  Farsi; ML Kit iOS binding unverified). Slice 159 and Decision 148 repeated it. Those decisions are reversed here.
- Smaller gaps: Dino's toggle switch accessibility code is Android-only (`ToggleSwitch.cs:50`, `:98`,
  `ToggleSwitchAccessibility.cs`); RTST scanner messages say "Google Play services" (`AppResources.resx:39`, `:41`),
  which is wrong on iOS if they show there.

## Decisions (locked with the user, 2026-10-27)

- **Engine: Google ML Kit on iOS too**, the same on-device engine and languages (Farsi included) as Android.
  Not Apple's Translation framework (no Farsi, Swift-only).
- **Minimum iOS: 15.0 -> 15.5**, ML Kit's own floor. Source: ML Kit release notes, 2024-09-30, "On iOS, raised the
  minimum supported version of iOS to 15.5.0"; podspec `MLKitTranslate` 8.0.0 `platforms.ios: 15.5`. The latest iOS is
  27.2 (Apple, 2026-10-26), so no current iPhone is lost. Raise higher only if the binding's own package is proven to
  need it (read its nuspec/assembly, don't guess). Never hide translation by iOS version.
- **Binding:** try `EngageSolutionsGroup.Apple.MLKitTranslate` 8.1.0 (net9.0-ios18.0, updated 2025-09-04) first;
  confirm via `library-docs` that it loads in a net10.0-ios app. If it doesn't, bind the `MLKitTranslate` 8.0.0
  xcframework ourselves. Either way it must work on a real iPhone; the arm64 simulator is not a gate.
- Windows head: out of scope (no translation or scanner there stays as is).

## Work

- [ ] 1. **Smallest end to end: RTST translate card on iOS.** Add the iOS ML Kit Translate binding to the iOS ItemGroup
  of `Mobile.Shared.csproj`; write `Platforms/iOS/MlKitTranslator.cs` implementing `ITranslator` exactly like the
  Android one (same supported languages, on-demand model download with the 500 ms loading state and Cancel,
  same errors); register it in `MobileSharedServices.iOS.cs`; drop the "no translator" note in `MauiProgram.iOS.cs`.
  Raise `SupportedOSPlatformVersion` for iOS to 15.5 in Mobile.Shared, RTST Mobile and Dino Reader.
- [ ] 2. **Dino own-book translation on iOS.** Implement `ITranslationPacks` (Slice 159) on iOS so the download prompt
  and pack checks behave as on Android; confirm `StoryTranslator` picks up the engine for own, typed and scanned books.
- [ ] 3. **Small parity fixes.** iOS twin of the Dino toggle switch accessibility (VoiceOver reads label and on/off state
  like TalkBack); make RTST scanner messages platform-neutral or give iOS its own wording.
- [ ] 4. **Docs.** Mark the translation lines in Slice 55, Slice 159 and Decision 148 as reversed by Slice 160; update
  `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md` (translator on both platforms; never add a one-platform service).

## Verification

- `dotnet build` of Mobile.Shared, RTST Mobile and Dino Reader for `net10.0-ios` (`-p:IncludeIos=true
  -p:IncludeWindows=false -p:IncludeAndroid=false`) and for Android; both unit test suites green; CI iOS jobs green.
- Android emulator: translation unchanged (regression).
- Real iPhone through TestFlight (no Mac here): RTST shows the translate card for English -> Farsi and back,
  downloads the model with a progress state, translates offline afterwards; Dino translates a typed own book into
  Farsi; VoiceOver reads the toggle switches. Any item not run is reported as not run, never as passed.
