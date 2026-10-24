# Slice 103 — Danish, Finnish, Swedish, Norwegian and Dutch in every app

*In progress (wave 2026-10-24), draft PR.* Planned 2026-10-24, from the user's request "add Scandinavian languages: Danish, Finnish, Swedish,
Norwegian and Dutch".

**Batch:** 21 (five more languages)
**Depends on:** Decision 68 (the nine first-run languages), Slices 53, 57, 66 (localised UIs)
**Touches:** `src/ReadTheStupidText.Application/Reading/AppLanguage.cs`, `src/ReadTheStupidText.Application/Images/Ocr/ScriptDetector.cs`, `src/ReadTheStupidText.App/UiLanguageTag.cs`, `src/ReadTheStupidText.App/Strings/{da,fi,sv,nb,nl}/Resources.resw` (new), `src/ReadTheStupidText.App/Package.appxmanifest`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources.{da,fi,sv,nb,nl}.resx` (new), `src/DinoReader.App/Resources/Strings/AppResources.{da,fi,sv,nb,nl}.resx` (new), `src/DinoReader.Core/UiLanguage.cs`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/AndroidManifest.xml`, `src/*/Platforms/iOS/Info.plist`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/LocalizedUiTests.cs`, `STORE.md`, `README.md`, `CLAUDE.md`

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

- [ ] 1. **Smallest end-to-end: Swedish on the phone.** `AppLanguage.Swedish`, the phone's Swedish `.resx`,
  picker entry. Choose Swedish, the UI is Swedish, and Swedish text reads with Supertonic.
- [ ] 2. The other four languages on the phone; `ScriptDetector`; the ML Kit code table.
- [ ] 3. Dino: five `.resx` files, the picker and first run (books and story rules are Slice 105).
- [ ] 4. Windows: five `.resw` folders, `UiLanguageTag`, the manifest `Resources`.
- [ ] 5. `STORE.md` listing texts, the "nine languages" wording in README/CLAUDE.md/STORE.md, rules.

## Verification

- Unit: key parity for all three apps in fourteen languages; `AppLanguages.Codes` round-trips; the
  `nb` to `no` ML Kit mapping.
- Emulator (device-test): pick each new language on the phone and in Dino. The UI is translated, a typed
  paragraph in that language reads aloud (Norwegian with the phone voice if the image has one), and a
  scanned Swedish page keeps å/ä/ö.
- Windows head: switch to Dutch, the tray menu and control panel are Dutch, Dutch text reads.
