# Slice 82 — The phone voice reads in the text's language, and is only offered when it can

*In progress (wave 2026-10-22), draft PR.*

**Batch:** 14
**Decisions:** 89
**Depends on:** Slice 81 (the Farsi pack must actually download for Farsi to stay readable)
**Touches:** `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/AvSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/IPlatformServices.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidPlatformServices.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IOSPlatformServices.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Windows/WindowsPlatformServices.cs`, `src/ReadTheStupidText.Application/Reading/VoiceAvailability.cs` (new), `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Translation.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/**`, `tests/ReadTheStupidText.Tests/VoiceAvailabilityTests.cs` (new)
**Story issue:** —

## Goal

Text is never read by a voice that can't speak its language. The phone voice is switched to
the language being read; where the phone has no voice for it and no natural voice exists or is
installed, the option goes from the list instead of reading only the Latin fragments.

## Why

Seen on a Pixel 10 Pro, 1.16.0, 2026-10-22: app language فارسی, an English letter translated to
Farsi (logcat: `Source language: en, target language: fa`), voice "Phone voice". Only the Latin
parts ("MEM0186…", "Nest", "PT", digits) were spoken. `AndroidSpeechReader.OnInit` calls
`_tts.SetLanguage(Locale.Default)`, the device locale (en-GB), so Google TTS loaded `en-gb` and
skipped the Persian script. Nothing checks whether a voice can read a language. The user: "If
it cannot read farsi, or any other languages, it should remove the option from the list."

## Decisions locked with the user (2026-10-22)

- **Which lists:** the **Translate to** toggle and the **Phone voice** row. The app-language
  list is unchanged (every app language has a natural pack in the catalog).
- A language counts as readable when the phone TTS has a voice for it
  (`IsLanguageAvailable` ≥ `LangAvailable` on Android; an `AVSpeechSynthesisVoice` for it on
  iOS) **or** a natural pack for it is installed or offered by the catalog.
- The phone voice speaks the language of the text it is handed: the translation target when
  translating, else the app language.

## Steps

- [ ] `VoiceAvailability` (Application, pure): given the phone-TTS languages, the installed
      packs and the catalog, answers `CanPhoneRead(lang)` and `CanRead(lang)`; unit-tested.
- [ ] `IPlatformServices.PhoneVoiceLanguages()` (Android via `TextToSpeech.IsLanguageAvailable`
      per app language after init; iOS via `AVSpeechSynthesisVoice.GetSpeechVoices()`;
      Windows returns the installed SAPI/WinRT languages or empty).
- [ ] `AndroidSpeechReader`: `SetLanguage` to the reading language before each read (not only
      `Locale.Default` at init); same for `AvSpeechReader` if it uses the default voice.
- [ ] `VoicePickerPage`: the Phone voice row is shown only when `CanPhoneRead(app language)`;
      when it isn't and no natural voice is installed yet, the page shows the download state
      (Slice 81) instead of a row that would read garbage.
- [ ] `ReaderPage.Translation`: the translate toggle shows only when `CanRead(target)`; else
      the existing unsupported note explains why (new string in all nine `.resx` files).
- [ ] When the only voice that could read the text is still downloading, Play says so (an
      alert, like the pack-pending OCR note) rather than reading with the wrong voice.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`
- Pixel (Google TTS, no Persian voice), app in فارسی, Farsi pack not yet installed: the voice
  picker shows no Phone voice row, Play announces the Farsi voice is still downloading. After
  the pack lands, the Farsi translation is read in full by the Piper voice.
- App in English, phone voice selected, Spanish translation on: the phone voice reads Spanish
  with a Spanish voice (logcat shows the `es` superpack loaded), not en-GB.
