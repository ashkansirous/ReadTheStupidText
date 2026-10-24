# Slice 103 — Danish, Finnish, Swedish, Norwegian and Dutch in every app

*Built 2026-10-24 (PR #286), not yet verified on device.* Planned 2026-10-24, from the user's request "add Scandinavian languages: Danish, Finnish, Swedish,
Norwegian and Dutch".

**Batch:** 21 (five more languages)
**Depends on:** Decision 68 (the nine first-run languages), Slices 53, 57, 66 (localised UIs)
**Touches:** `src/ReadTheStupidText.Application/Reading/AppLanguage.cs`, `src/ReadTheStupidText.Application/Images/Ocr/ScriptDetector.cs`, `src/ReadTheStupidText.App/UiLanguageTag.cs`, `src/ReadTheStupidText.App/Strings/{da,fi,sv,nb,nl}/Resources.resw` (new), `src/ReadTheStupidText.App/Package.appxmanifest`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources.{da,fi,sv,nb,nl}.resx` (new), `src/DinoReader.App/Resources/Strings/AppResources.{da,fi,sv,nb,nl}.resx` (new), `src/DinoReader.Core/UiLanguage.cs`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/AndroidManifest.xml`, `src/*/Platforms/iOS/Info.plist`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/LocalizedUiTests.cs`, `.github/workflows/packs.yml`, `src/ReadTheStupidText.Mobile.Shared/IPlatformServices.cs`, `src/ReadTheStupidText.Application/Reading/PackConstants.cs`, `src/ReadTheStupidText.Application/Translation/EngineLanguageCodes.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MlKitTranslator.cs`, `src/ReadTheStupidText.Infrastructure/Translation/ScriptLanguageDetector.cs`, `src/DinoReader.Core/ScriptLanguageGuesser.cs`, `src/ReadTheStupidText.Mobile/UiCulture.cs`, `src/DinoReader.App/UiCulture.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/LanguageChooser.cs`, `src/DinoReader.App/LanguagePage.xaml`, `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile/TranslationLanguageChoices.cs`, `src/ReadTheStupidText.App/LanguageWindow.xaml.cs`, `STORE.md`, `README.md`, `CLAUDE.md`

## Why

All three apps (RTST Windows, RTST phone, Dino) share one closed list, `AppLanguage` (en, fa, fr, es, pt,
zh, ar, ru, ja). The user wants Danish, Finnish, Swedish, Norwegian and Dutch too. The voice model
(Supertonic-3, 31 languages) already speaks da, fi, sv and nl with all ten styles. It does not speak
Norwegian (Slice 104). ML Kit translates all five on Android. The built-in recognizer reads their
letters (å, æ, ø, ä, ö) with no OCR pack.

## Decisions (2026-10-24, agreed with the user)

- **Decision 107: fourteen app languages.** `AppLanguage` gains Danish (`da`), Finnish (`fi`), Swedish
  (`sv`), Norwegian Bokmål (`nb`) and Dutch (`nl`, Netherlands). All are left to right. Every app shows them
  in its first-run and language pickers, in its own name ("Dansk", "Suomi", "Svenska", "Norsk", "Nederlands").
- **Claude writes the UI translations** for all three apps (Windows `.resw`, phone and Dino `.resx`), as for
  the current nine. The existing key-parity tests cover the new files.
- **Reading:** da, fi, sv and nl read with Supertonic-3 (Dino casts from the same ten styles). Norwegian
  reads with the phone's own voice where one exists until Slice 104's pack lands; `VoiceAvailability`
  already hides a language nothing can read.
- **Code mapping:** the app uses `nb`; where an engine wants another code (ML Kit's Norwegian is `no`),
  the mapping is one named table in the adapter, never a string at the call site.
- **OCR:** add the five to the Latin entry in `ScriptDetector`; no pack.
- **Store listings:** Claude writes the five listing texts into `STORE.md`. They are not pasted into Play
  or the Microsoft Store until the apps are public (out of scope here).
- **Out of scope:** Nynorsk, Flemish (`nl_BE`) as a separate choice, and any right-to-left work.

## Work

- [x] 1. **Smallest end-to-end: Swedish on the phone.** `AppLanguage.Swedish`, the phone's Swedish `.resx`,
  picker entry. Choose Swedish, the UI is Swedish, and Swedish text reads with Supertonic.
- [x] 2. The other four languages on the phone; `ScriptDetector`; the ML Kit code table.
- [x] 3. Dino: five `.resx` files, the picker and first run (books and story rules are Slice 105).
- [x] 4. Windows: five `.resw` folders, `UiLanguageTag`, the manifest `Resources`.
- [x] 5. `STORE.md` listing texts, the "nine languages" wording in README/CLAUDE.md/STORE.md, rules.

## Verification

- Unit: key parity for all three apps in fourteen languages; `AppLanguages.Codes` round-trips; the
  `nb` to `no` ML Kit mapping.
- Emulator (device-test): pick each new language on the phone and in Dino. The UI is translated, a typed
  paragraph in that language reads aloud (Norwegian with the phone voice if the image has one), and a
  scanned Swedish page keeps å/ä/ö.
- Windows head: switch to Dutch, the tray menu and control panel are Dutch, Dutch text reads.

*Changed while building:*

- `AppLanguage` has 14 values. `AppLanguages.Canonical` maps `no`/`nn` to `nb` in one place, used by `TryParse`, `VoiceAvailability`, `ScriptDetector` and `EngineLanguageCodes` (`nb` to `no` for ML Kit).
- UI translations for da/fi/sv/nb/nl (written by Claude) in all three apps; `LanguageNames` and both `LanguageChoices` updated. Manifests: RTST and Dino Windows. RTST iOS gets `Info.plist` `CFBundleLocalizations` and `.lproj` files.
- `STORE.md` holds the five listing texts; they are not pasted into the stores.
- Stop-word detection for the five languages in `ScriptLanguageDetector` and Dino's `ScriptLanguageGuesser`, with distinctive words for da/nb/sv, and tests.
- `packs.yml` `SUPERTONIC_LANGUAGES` now includes da/fi/sv/nl. `PackConstants.NaturallyReadable` excludes `nb` until piper-nb (Slice 104 must add it); a test ties it to `packs.yml`.
- Starter and store coverage tests are pinned to the nine languages with tables, until Slice 105.

*From review:* high pass, 7 applied (#4 and #5 go to Slice 105, #9 Nynorsk is out of scope). Medium pass, 5 applied (#3 goes to Slice 105, #7 out of scope, #8 catalog-driven availability not done).

#### For CLAUDE.md

- `AppLanguages.Canonical` is the one place `no`/`nn` map to `nb`; route every language parse and engine code through it. `PackConstants.NaturallyReadable` must stay in step with `packs.yml`; Slice 104 adds `nb` when piper-nb lands. (Slice 103: docs/plan/slices/103-nordic-dutch-languages.md)

#### For README.md

- "Nine languages" becomes fourteen wherever README says it.

#### Not yet verified

- The `packs.yml` run and pin bump that gives the Supertonic pack da/fi/sv/nl (the coordinator does it right after merge); until then those languages read with the phone voice, and a pack that doesn't declare a language falls back safely.
- Emulator check of each new UI language.
- Windows head in Dutch.
- A native read of all translations.
- Dino's iOS app has no localisation plist entries (pre-existing).
