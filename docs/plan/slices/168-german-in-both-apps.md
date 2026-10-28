# Slice 168 — German in both apps (UI, voice, detection, translation, Dino rules)

*Built 2026-10-27, not yet verified (German heard on devices waits on packs-8).* Read The Stupid Text (Windows, Android, iOS) and Dino Book Reader (Android, iOS, Windows). Slice 169 (German books) builds on it.

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
`src/ReadTheStupidText.App/Strings/de/Resources.resw` (new), `src/ReadTheStupidText.Mobile/Platforms/iOS/Resources/de.lproj/InfoPlist.strings` (new), `src/ReadTheStupidText.Mobile/Platforms/iOS/Info.plist`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `worker/events.ts` (the `READING_LANGUAGES` entry only),
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

- [x] 1. **Smallest end-to-end: RTST reads German.** Add `AppLanguage.German` (`de`), enable `de` in Supertonic
  (`SupertonicVoiceTable`, `packs.yml` language list), publish a new packs release and point `PackConstants` at it. Language
  picker entry on Windows and Mobile. Detection: German stop words in `ScriptLanguageDetector` and `ScriptDetector`'s Latin list.
  A German paragraph pasted on Windows, Android and iOS is detected as German and read with a German Supertonic voice.
- [x] 2. **RTST UI in German:** `AppResources.de.resx`, `UiLanguageTag`/`UiCulture`, `<Resource Language="de">` in the Windows
  manifest. The count of 14 becomes 15 in `AppLanguage`'s comment and the tests.
- [x] 3. **Translation:** `de` in the ML Kit lists (Android and iOS); Windows `de-en` and `en-de` Opus-MT packs in `packs.yml`
  and `TranslationPacks`. The review page offers German to English and English to German on all three platforms.
- [x] 4. **Dino UI and reading in German:** `RuleSets` entry and `GermanRules.cs` (like `DutchRules`), `UiLanguage`,
  `LanguageNames`, `ScriptLanguageGuesser` stop words, animal tables (`AnimalSoundTables`, `AnimalGuessWords`, `AnimalGuessAge`),
  ten-voice German roster in `CharacterRoster*`, `AppResources.de.resx`, Windows manifest. A typed own book in German reads with
  distinct character voices, and own-book translation offers German.
- [x] 5. **Site and store text:** German in `site/index.html` and the Dino site's language list; German listings in `STORE.md`.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests` and `tests/DinoReader.Tests` (15 languages, same resx keys, `NaturallyReadable`
  matches `packs.yml`).
- Android: `tools/emulator/run-flows.ps1` plus a German read on the emulator for both apps (logs show the `de` Supertonic voice).
- iOS: compile both apps; on TestFlight, a German read and German to English translation in each app.
- Windows: desktop read of German text with the Supertonic pack; German to English translation with the new pack.
- Listen: one German page in RTST and one Dino character line (user, phone).

*Changed while building:* 15 languages (`AppLanguage.German`). Supertonic `de` enabled (real model files map ss-sharp and umlauts via NFKD; sherpa lists de). `packs.yml` gains `de` in Supertonic plus Opus-MT `de-en` (Apache-2.0) and `en-de` (CC-BY-4.0), Xenova revisions and sha256 pinned, licence check case-insensitive. `PackConstants` now points at packs-8 (pack_version 4): existing Supertonic users re-download about 145 MB because `pack.json` carries the language list. ML Kit lists already had `de`. RTST: resx, Windows resw, iOS `de.lproj/InfoPlist.strings` and `CFBundleLocalizations`, manifests, pickers, detection (German-only stop words; der/die/den left out to avoid Dutch/Danish). Dino: `GermanRules` (quote pairs, `CloserOf`; `sie` female only next to a singular speech verb), `RuleSets`, animal tables, ten-voice roster, resx. `worker/events.ts` `READING_LANGUAGES` gains `de`. Sites and a German RTST listing in `STORE.md` (Dino listing not drafted). `GermanBooksPending.cs` skips `de` in 5 book checks until Slice 169. Run 40588343892 failed because upstream re-uploaded the Kokoro archive on 2026-10-26 (new sha256 72c34723..., the pin 4c3052ab... is the file packs-7 was built from); the kokoro-en pack must stay byte-identical (version 2), so `packs.yml` gained the input `kokoro_carry_over_from` (run packs-8 with `packs-7`): it skips the upstream fetch, build and sample, downloads the zip and sample from that release, checks them against the asset digests and that release's `packs.json` (id, version, sha256, size), and keeps the catalog entry with only its url moved to the new tag. The generic `carry_over` function can serve other packs. The pin is deliberately not changed.

*From review:* high pass 9 findings (applied 2, 3, 5, 7, 8, 9 plus a test for 4; 1 handled by order: publish packs-8 first; 6 skipped). Medium pass 8 findings (applied 1, 3, 4, 5, 8; 2 verified correct; 6, 7 skipped).

*Checks:* Dino 1602 pass, RTST 1438 pass (2 skip); Android and iOS compile both apps; Windows x64 both.

**Merge precondition:** run `packs.yml` from this branch with release_number=8, pack_version=4, translation_pack_version=1 and confirm the packs-8 release exists with all assets.

#### For CLAUDE.md

None beyond what the diff already changed.

#### Not yet verified

- German heard on Android, iOS (TestFlight) and Windows (waits on packs-8).
- Windows `de-en` / `en-de` translation.
- Dino German own book with distinct character voices.
- Native German speaker read of all German text (machine-written).
- Dino German store listing not drafted.
