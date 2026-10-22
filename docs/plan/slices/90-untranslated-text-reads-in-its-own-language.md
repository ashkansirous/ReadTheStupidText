# Slice 90 — Untranslated text is read in its own language

*Built 2026-10-22 (wave 2026-10-22), PR #258; verified on the RTST_Test emulator, see Not yet verified for the rest.*

**Batch:** 14 (follow-up to Slice 82)
**Depends on:** Slice 82 (`VoiceRouting`, `VoiceAvailability`, the phone voice's `SetLanguage` per read)
**Touches:** `src/ReadTheStupidText.Application/Reading/VoiceRouting.cs`, `src/ReadTheStupidText.Application/Reading/VoiceAvailability.cs`, `src/ReadTheStupidText.Application/Translation/ILanguageDetector.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Translation.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/AvSpeechReader.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/**`, `tests/ReadTheStupidText.Tests/VoiceRoutingTests.cs`, `src/ReadTheStupidText.Mobile/CLAUDE.md`, `src/ReadTheStupidText.Mobile.Shared/MobileVoiceModelService.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/IPlatformServices.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Application/Reading/IVoicePackService.cs`, `src/ReadTheStupidText.Application/Reading/IReadingModelResolver.cs` (new)
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

- [x] `VoiceRouting.ReadingLanguage(translated, targetLanguage, sourceChoice, detectedLanguage, appLanguage)`:
      pure, with tests (English text in a Farsi app, untranslated → `en`; translated → `fa`; an explicit
      source choice beats detection; unsure detection → app language).
- [x] `ReaderPage`: detect the text's language on load and change (reuse the translate source
      picker's detection; no second detector), and pass the reading language to the phone voice before
      each read. Play's "no voice" alert and the natural-voice route use the reading language.
- [x] A natural-voice pack for the reading language is used when installed. If none exists and the
      phone has no voice for it, show the existing "no voice" message for that language.
- [x] `Mobile/CLAUDE.md` rule (replaces Slice 82's app-language rule).

## Verification

- `dotnet test tests/ReadTheStupidText.Tests` (`VoiceRoutingTests`).
- Emulator, app in Farsi, translation off: paste English → read with an English voice (logcat shows
  an `eng` synthesis locale). Turn translation on → read in Farsi. App in English, paste Spanish with
  translation off → read in Spanish.

## Review pass 1 fixes
- The reading language's pack is resolved per read by `IReadingModelResolver` (cached pack scan) and handed to the reader as a `ReadVoiceScope`; the shared app-language model, stored voice and picker are never mutated.
- Unsure detection falls back to the text's script (`VoiceRouting.LanguageForScript`): the app language when it writes in that script, else the main language of the script. Mixed text is read whole in one language.
- Downloading is announced only for the app language's own pack; Play runs no detector; the head start and the voice label follow the reading language.

## Review pass 2 fixes
- An app-language read keeps the old unscoped neural path and head start; only a different-language read is scoped (`IReadScoped.SpeakScopedAsync(text, scope)`, `UsesScope`/`NaturalPackUsable` tested). iOS has no scoped neural read, so a different-language read there uses the phone voice in that language.
- The per-read language never persists; previews speak the app language. The per-language model cache is dropped on `IVoicePackService.InstalledPacksChanged`. Detection and voice plans are debounced and cached by text. CJK: kana → ja, Hangul → ko, Han-only → zh.

## Changed during acceptance
- The translate switch didn't refresh the voice label (it kept naming the untranslated text's voice). `OnTranslateToggled` now refreshes it (`ReaderPage.Translation.cs`).

## Checks
- `dotnet test tests/ReadTheStupidText.Tests`: 866 passed, 2 skipped. Android builds of Mobile and DinoReader.App; Windows App build. iOS compiled only by CI.
- Reviews: `<worktrees>/reviews/90-review-1.md` (13, all fixed), `90-review-2.md` (8, all fixed).
- Emulator (RTST_Test; Piper Farsi and Supertonic English packs installed), app in Farsi: English text, translation off → read by the English Supertonic voice (label "Momonga"; cached audio 44.1 kHz, Piper is 22.05 kHz). Farsi text → Farsi Piper voice "Amir" with a head-start cache hit (first audio 180 ms). English text, translation on → translated to Farsi and read by Piper (22.05 kHz). Label follows the switch: Momonga off, Amir on.

#### For CLAUDE.md (lifted 2026-10-22)
- The reading language is the translation target when translated, else the text's own language (source picker, then detection, then the script table); never read text with a voice for a different language.

#### Not yet verified
- A Spanish text in an English app (the same path as English-in-Farsi, covered by `VoiceRoutingTests`).
- iOS on a device: a different-language read uses the phone voice in that language.
- A language with no natural pack and no phone voice shows the "no voice" message (not reachable on the emulator).
