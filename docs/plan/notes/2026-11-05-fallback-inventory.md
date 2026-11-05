# Fallback inventory (read from origin/main, 2026-11-05)

Method: git grep "fallback" (146 hits), catch blocks per file (top files read), and the read paths. Not line-by-line for every catch; rows marked "class" stand for many catches. Line numbers are origin/main.

## How analytics works today
- Dino: src/DinoReader.Core/UsageEvent.cs (UsageEventName; read_voice and audio_pack_download are Slice 267), AudioPackUsage.cs (closed lists: DeviceVoiceReason no_pack/pack_failed/text_mismatch/clip_missing; PackFailureKind network/size/unzip/clip/missing/storage). Sent from ReadingPage.xaml.cs:1274 using StoryVoicing.cs:83-86. A read counts as premade if ANY segment has a clip, so a partly-premade read hides missing pieces.
- RTST: src/ReadTheStupidText.Application/Usage/UsageEvent.cs + UsageEvents.cs (app_opened, read_started, translate_offered/chosen, voice_changed, timing, wait_shown/cancelled). read_started carries Engine (phone/supertonic/piper/melo/kokoro/windows) and Mode (original/translation); timing has stages model_download/translate/first_audio, outcome done/cancelled/failed. No fallback-reason field.
- Worker: worker/events.ts closed lists (READ_VOICES l.51, AUDIO_PACK_OUTCOMES l.52, WAIT_STAGES l.40, READ_STOP_REASONS l.54); unlisted values are dropped. Test: worker/events.test.mjs (~l.718 read_voice).
- Gate: UsageEligibility (Application/Usage), UsageGate (Mobile.Shared); worker drops major-0 versions and android/ios x64/x86. worker/CLAUDE.md lines 13-18, 29-30: closed-list fields only, no free text.
- Client tests: tests/DinoReader.Tests/{AudioPackUsageTests,UsageEventTests,UsagePlaybackTests}.cs; tests/ReadTheStupidText.Tests/{UsageTests,UsageServiceTests,UsageChannelTests,UsageEligibilityTests,ReadAloudServiceGeneratingTests}.cs.

Columns: app+platform | file:line | wanted -> instead | event today (YES / PARTIAL / NO) | proposed reason

## 1. Speech / voices
- RTST Win | Infrastructure/Reading/CompositeSpeechReader.cs:~60-80 | neural -> WinRT voice (model not ready) | PARTIAL (engine=windows) | neural_not_ready
- RTST Win | same ~l.70 catch NeuralEngineUnavailableException | neural -> WinRT at start | NO | neural_unavailable
- RTST Win | same ~l.110-113 + Application/Reading/ReadAloudService.Streaming.cs:61 | streamed neural -> whole text via WinRT | NO | stream_unavailable
- RTST Win | Infrastructure/Reading/SupertonicSpeechReader.cs:67-69,:811 | engine build failed -> Windows voice until model changes | NO | engine_build_failed
- RTST + Dino Android/iOS (Dino Win too) | Mobile.Shared/CompositeSpeechReader.cs:85-100 | neural -> phone/system TTS (model not ready) | PARTIAL (engine=phone) | neural_not_ready
- same | CompositeSpeechReader.cs:~92,~120,~160 catch NeuralEngineUnavailable (plain, scoped, segments) | model vanished after readiness check | NO | neural_unavailable
- same | CompositeSpeechReader.cs:142-167,:229-230 | Dino voiced segments -> joined text in one device voice | NO | segments_unsupported / pack_absent
- RTST Mobile | Mobile/ReaderPage.xaml.cs:532-534 + Application/Reading/VoiceRouting.cs (VoiceRoute Read/Downloading/NoVoice) | phone voice cannot speak language -> natural pack, or no voice | NO | phone_cannot_speak / no_voice
- RTST + Dino | Application/Reading/SilentOutputGuard.cs:48-89,:213-227 | silent synthesis -> retake -> sibling voice -> give up | NO | silent_retake / silent_sibling
- Dino | Core/CharacterTraits.cs:65; Core/CharacterRosterPackVoices.cs:20; StoryVoicing.cs:61 | suggested voice not in roster -> best-trait voice; Melo while storyteller pack absent | NO | voice_not_in_roster / storyteller_pack_missing
- Dino | ReadingSession.cs:464-466,:504 | recording unplayable -> generated voice | NO | recording_unplayable
- Dino | ReadingSession.cs:1139 | segment read -> one joined system utterance | NO | joined_utterance
- RTST Android | Mobile.Shared/Platforms/Android/AndroidSpeechReader.cs:186 | phone TTS lacks language | NO | phone_language_missing

## 2. Pre-made audio (Dino)
- Dino all | StoryVoicing.cs:142-177 | clips -> device voice | YES (read_voice device:no_pack|pack_failed|text_mismatch|clip_missing) | existing four, add partial
- Dino | StoryVoicing.cs:100-106 | translated read -> original read still uses premade | NO | see translation

## 3. Packs / downloads
- Dino | AudioPackStore.cs:387,405,411 | pack download failed | YES (audio_pack_download failed:*) | existing
- Dino | BookUpdater.cs:232-234,:269-272 | pack sync/sweep failed, retried next open | NO | pack_sync_failed
- Dino | StorytellerDefault.cs:208-212,:430,:444,:493 | storyteller pack/recast failed, old voices stay | PARTIAL (voice_pack_download) | storyteller_failed / recast_failed
- Dino | StoryLanguages.cs:253,:485,:632-637 | voice catalogue unreadable / language pack failed | PARTIAL | catalogue_failed
- RTST Mobile + Mobile.Shared | MobileVoicePackService.cs (16 catches), VoicePackInstaller.cs (7), PackDownloadHost.cs, IosBackgroundDownloader.cs (8), PackResumeWatcher.cs | pack install failed -> phone voice (class) | PARTIAL (timing model_download failed) | pack_failed
- RTST Win | Infrastructure voice model service (not read) | pack failed -> Windows voice | PARTIAL | pack_failed

## 4. Translation
- RTST all | Application/Translation/ReadTranslator.cs:150-152,:175-187,:385-393 | translation -> original read (20 s DefaultBudget timeout, pack unavailable, any error) | PARTIAL (timing translate failed) | translation_timeout / translation_pack_missing / translation_error
- RTST | ReadTranslator.cs:114; ReadAloudService.Streaming.cs:88-90,:176-178 | streamed translation -> whole-text translation / partial text | NO | stream_plan_failed / stream_partial
- Dino | StoryTranslator.cs:142-144,:192-194,:316,:374 | translated read -> story as written | NO | translation_model_missing
- Dino | StoryVoicing.cs:100-106 (translationSkipped callback) | same | NO | translation_skipped
- iOS | Mobile.Shared/Platforms/iOS/MlKitTranslator.cs:86-88,:410-412 | ML Kit pack download failed | NO | translation_pack_failed

## 5. OCR / scan
- RTST + Dino Android/iOS | Mobile.Shared/Ocr/LanguageImageTextExtractor.cs:55-100,:144 | PaddleOCR pack -> ML Kit / Apple Vision (OcrSource.PlatformFallback, Application/Images/Ocr/OcrSource.cs:26) | NO | ocr_builtin_no_pack
- same | LanguageImageTextExtractor.cs:92-109 | built-in OCR failed -> packs tried | NO | ocr_builtin_failed
- Windows heads | Mobile.Shared/Platforms/Windows/WindowsOcrTextExtractor.cs:39 | OCR -> PlatformFallback | NO | ocr_platform_fallback
- RTST Mobile | ScanPackFlow.cs (5), OcrPackInstaller.cs (5), Mobile.Shared DocumentScannerStartFailedException | scanner/pack failed | NO | ocr_pack_failed / scanner_start_failed

## 6. Documents
- Dino | PagesStep.xaml.cs:606,:650-657 | PDF import failed -> plain-text fallback reader | NO | pdf_import_failed
- Documents | PdfTextExtractor.cs:11 (no OCR fallback; empty page = empty text) | not a swap | NO | none
- RTST | ReadAloudService.cs:401-410 | file too large / unreadable | NO | error, not fallback

## 7. Network / store / config
- RTST + Dino | Mobile.Shared/Usage/RemoteConfigClient.cs:21-23,:60-67,:143-145 | fetched config -> last good copy -> default | NO | config_cached / config_default
- Dino | StoreClient.cs:81,:108,:179,:209,:302 | store list/cover/story/update failed | NO | store_unreachable
- Dino | BookUpdater.cs:189-191,:328 | update check failed / baseline unreadable | NO | update_check_failed
- Win | App/WindowsUpdate.cs:24,:201; Infrastructure/Usage/WindowsUsage.cs:13,:81 | unknown version -> 0.0.0 (event then dropped by design) | NO | n/a
- RTST Mobile | Mobile/UiCulture.cs:12-31 | UI language unsupported -> English | NO | ui_language_unsupported

## 8. Platform gaps
- Dino iOS | Platforms/iOS/AttributeFallback.cs, IosAttributes.cs | named font missing -> system font | NO | font_fallback
- Dino Win / RTST Win | DinoWindowFrame.cs:62,139,205,301; ControlPanelWindow.xaml.cs:45; WindowPlacer.cs:24 | window placement/DPI | NO | skip (cosmetic)
- RTST Win | Application/Input/ISelectionCopier.cs:6; ReadAloudService.cs:519 | UIA selection unavailable -> copy keystroke / clipboard | NO | selection_via_copy

## 9. Swallowed errors (class)
Crash/log housekeeping (CrashReporter 13 catches, RollingLogFile 7, PendingCrashStore 7, UsageService 6, DinoLog) must NOT be instrumented (loops). Candidates not read line by line: Dino GalleryPage (11), ReadingPage (10), CastPage (8), RecordPage (6), StoryPackageReader (11), FileStoryLibrary (7), App.xaml.cs (8); RTST ReviewPage (16), CapturePage (12). ReadingSession.cs:136-138 (best-effort prepare), :544-546 FailRead.

## Counts (rows)
speech/voices 13, pre-made 2, packs 6, translation 5, OCR 4, documents 3, network/config 5, platform gaps 3, swallowed-error classes 1.

## Files a change would touch
Worker: worker/events.ts, worker/events.test.mjs, worker/CLAUDE.md.
Dino: src/DinoReader.Core/UsageEvent.cs, src/DinoReader.Core/AudioPackUsage.cs (or new FallbackUsage.cs), src/DinoReader.App/{UsageCounter,StoryVoicing,StoryTranslator,ReadingSession,ReadingPage.xaml,StorytellerDefault,BookUpdater,StoreClient,PagesStep.xaml}.cs, src/DinoReader.App/CLAUDE.md; tests/DinoReader.Tests/{UsageEventTests,AudioPackUsageTests}.cs.
RTST/shared: src/ReadTheStupidText.Application/Usage/{UsageEvent,UsageEvents}.cs, Application/Reading/{ReadAloudService.Usage,ReadAloudService.Streaming,SilentOutputGuard}.cs, Application/Translation/ReadTranslator.cs, Infrastructure/Reading/{CompositeSpeechReader,SupertonicSpeechReader}.cs, Mobile.Shared/{CompositeSpeechReader.cs,Ocr/LanguageImageTextExtractor.cs,Usage/RemoteConfigClient.cs,Platforms/iOS/MlKitTranslator.cs}, Mobile/ReaderPage.xaml.cs; tests/ReadTheStupidText.Tests/{UsageTests,UsageServiceTests,ReadAloudServiceGeneratingTests}.cs. Optionally .maestro/dino/usage-playback-events.yaml.
