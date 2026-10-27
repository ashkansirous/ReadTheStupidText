# Slice 168 — German in both apps (UI, voice, detection, translation, Dino rules)

*In progress (wave 2026-10-27), draft PR.* Read The Stupid Text (Windows, Android, iOS) and Dino Book Reader (Android, iOS, Windows). Slice 169 (German books) builds on it.

**Touches:** `src/ReadTheStupidText.Application/Reading/AppLanguage.cs`,
`src/ReadTheStupidText.Domain/Reading/SupertonicVoiceTable.cs`, `src/ReadTheStupidText.Application/**/PackConstants.cs`,
`.github/workflows/packs.yml`, `src/ReadTheStupidText.Infrastructure/Translation/TranslationPacks.cs`,
`src/ReadTheStupidText.Infrastructure/**/ScriptLanguageDetector.cs`,
`src/ReadTheStupidText.Application/Images/Ocr/ScriptDetector.cs`,
`src/ReadTheStupidText.App/LanguageChoices.cs`, `src/ReadTheStupidText.Mobile/LanguageChoices.cs`,
`src/ReadTheStupidText.Mobile/**/UiLanguageTag.cs`, `src/ReadTheStupidText.Mobile/**/UiCulture.cs`,
`src/ReadTheStupidText.Mobile/Resources/Strings/AppResources.de.resx` (new),
`src/DinoReader.App/Resources/Strings/AppResources.de.resx` (new),
`src/ReadTheStupidText.App/Package.appxmanifest`, `src/DinoReader.App/Platforms/Windows/Package.appxmanifest`,
`src/DinoReader.Core/RuleSets.cs`, `src/DinoReader.Core/GermanRules.cs` (new), `src/DinoReader.Core/UiLanguage.cs`,
`src/DinoReader.Core/AnimalSoundTables.cs`, `src/DinoReader.Core/AnimalGuessWords.cs`, `src/DinoReader.Core/AnimalGuessAge.cs`,
`src/DinoReader.Core/LanguageNames.cs`, `src/DinoReader.Core/ScriptLanguageGuesser.cs`, `src/DinoReader.Core/ShippedTitles.cs`,
`src/DinoReader.Core/CharacterRoster*.cs`, `src/ReadTheStupidText.Mobile.Shared/**/MlKitTranslator.cs` (language lists, both platforms),
`tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**` (not `content/dino/**`),
`site/index.html`, `site/dino/index.html`, `STORE.md`, `src/ReadTheStupidText.Mobile/CLAUDE.md`, `src/DinoReader.Core/CLAUDE.md`

## Why

German was left out of the language list by mistake (user, 2026-10-27). It is one of the biggest app-store markets, and both
apps already support 14 languages. Grounding (2026-10-27): `AppLanguage` has 14 values; German has no voice, no translation pack
on Windows, no detection stop words, no Dino rule set and no resx files today.

## Decisions (approved by the user, 2026-10-27)

- **Voice: Supertonic-3.** Its model card lists German (de) among its 31 languages
  (https://huggingface.co/Supertone/supertonic-3). Enable `de` in the Supertonic language set and the pack build, so German
  needs no new engine, download or licence. Dino casts German characters from the Supertonic voices with the ten-voice roster,
  like Dutch. Fallback only if German sounds bad when listened to: Piper `thorsten-medium` as a `piper-de` pack (follows
  `piper-nb`; espeak-ng GPL note per Decision 108).
- **Translation on every platform.** Android and iOS use ML Kit, which already supports German (add `de` to both
  `MlKitTranslator` lists). Windows gets two Opus-MT packs, `de-en` and `en-de` (Helsinki-NLP/opus-mt-de-en and opus-mt-en-de,
  converted and published by `packs.yml` like the existing ten; check each model card's licence first).
- **UI text:** Claude writes `AppResources.de.resx` for both apps (same keys as the neutral file; `LocalizedUiTests` checks).
- **Store listings:** draft the German listing for Play, Microsoft Store and App Store in `STORE.md`; entering them in the
  consoles waits until the apps are public.

## Work

- [ ] 1. **Smallest end-to-end: RTST reads German.** Add `AppLanguage.German` (`de`), enable `de` in Supertonic
  (`SupertonicVoiceTable`, `packs.yml` language list), publish a new packs release and point `PackConstants` at it. Language
  picker entry on Windows and Mobile. Detection: German stop words in `ScriptLanguageDetector` and `ScriptDetector`'s Latin list.
  A German paragraph pasted on Windows, Android and iOS is detected as German and read with a German Supertonic voice.
- [ ] 2. **RTST UI in German:** `AppResources.de.resx`, `UiLanguageTag`/`UiCulture`, `<Resource Language="de">` in the Windows
  manifest. The count of 14 becomes 15 in `AppLanguage`'s comment and the tests.
- [ ] 3. **Translation:** `de` in the ML Kit lists (Android and iOS); Windows `de-en` and `en-de` Opus-MT packs in `packs.yml`
  and `TranslationPacks`. The review page offers German to English and English to German on all three platforms.
- [ ] 4. **Dino UI and reading in German:** `RuleSets` entry and `GermanRules.cs` (like `DutchRules`), `UiLanguage`,
  `LanguageNames`, `ScriptLanguageGuesser` stop words, animal tables (`AnimalSoundTables`, `AnimalGuessWords`, `AnimalGuessAge`),
  ten-voice German roster in `CharacterRoster*`, `AppResources.de.resx`, Windows manifest. A typed own book in German reads with
  distinct character voices, and own-book translation offers German.
- [ ] 5. **Site and store text:** German in `site/index.html` and the Dino site's language list; German listings in `STORE.md`.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests` and `tests/DinoReader.Tests` (15 languages, same resx keys, `NaturallyReadable`
  matches `packs.yml`).
- Android: `tools/emulator/run-flows.ps1` plus a German read on the emulator for both apps (logs show the `de` Supertonic voice).
- iOS: compile both apps; on TestFlight, a German read and German to English translation in each app.
- Windows: desktop read of German text with the Supertonic pack; German to English translation with the new pack.
- Listen: one German page in RTST and one Dino character line (user, phone).
