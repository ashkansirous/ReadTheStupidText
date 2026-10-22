# Slice 90 — Untranslated text is read in its own language

*Planned.*

**Batch:** 14 (follow-up to Slice 82)
**Depends on:** Slice 82 (`VoiceRouting`, `VoiceAvailability`, the phone voice's `SetLanguage` per read)
**Touches:** `src/ReadTheStupidText.Application/Reading/VoiceRouting.cs`, `src/ReadTheStupidText.Application/Reading/VoiceAvailability.cs`, `src/ReadTheStupidText.Application/Translation/ILanguageDetector.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Translation.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/AvSpeechReader.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/**`, `tests/ReadTheStupidText.Tests/VoiceRoutingTests.cs`, `src/ReadTheStupidText.Mobile/CLAUDE.md`
**Story issue:** —

## Goal

With translation off, text is read in its own language. English text pasted while the app is in
Farsi is read with an English voice, not the Farsi one. Translated text is still read in the
target (app) language.

## Why

The user (2026-10-22), on Slice 82's rule that the phone voice speaks the app language: "it is a
fucking english text and the user says dont translate it! then the fucking app should read it in
English! this is obvious." This replaces Slice 82's "else the app language" decision.

## Decisions locked with the user (2026-10-22)

- The reading language is the translation target when the text is translated. Otherwise it is the
  text's own language: the translate source picker's choice if the user set one, else the language
  `ILanguageDetector` detects. The app language is only the fallback when detection is unsure.
- The text is read even when the app language has no phone voice, as long as a voice exists for the
  text's own language. The "no voice for this language" block applies only to the language actually
  being read.

## Steps

- [ ] `VoiceRouting.ReadingLanguage(translated, targetLanguage, sourceChoice, detectedLanguage, appLanguage)`:
      pure, with tests (English text in a Farsi app, untranslated → `en`; translated → `fa`; an explicit
      source choice beats detection; unsure detection → app language).
- [ ] `ReaderPage`: detect the text's language on load and change (reuse the translate source
      picker's detection; no second detector), and pass the reading language to the phone voice before
      each read. Play's "no voice" alert and the natural-voice route use the reading language.
- [ ] A natural-voice pack for the reading language is used when installed. If none exists and the
      phone has no voice for it, show the existing "no voice" message for that language.
- [ ] `Mobile/CLAUDE.md` rule (replaces Slice 82's app-language rule).

## Verification

- `dotnet test tests/ReadTheStupidText.Tests` (`VoiceRoutingTests`).
- Emulator, app in Farsi, translation off: paste English → read with an English voice (logcat shows
  an `eng` synthesis locale). Turn translation on → read in Farsi. App in English, paste Spanish with
  translation off → read in Spanish.
