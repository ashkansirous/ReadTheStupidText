namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// Where a feature fell back to something else (Slice 273): the closed list of the <c>fallback</c> count's area. The Worker's <c>FALLBACK_AREAS</c> must be the same list
/// (a test in each app's suite compares them). Later parts of the slice append their areas here and there, inside their own block.
/// </summary>
public static class FallbackArea
{
    /// <summary>Dino Reader: a read that should have used the book's pre-made audio used the device voice for some or all pieces.</summary>
    public const string PremadeAudio = "premade_audio";

    // Part B (speech, voices and translation).
    /// <summary>A read that should have used the natural (neural) voice, or the voice the person picked, used another engine or voice.</summary>
    public const string Speech = "speech";

    /// <summary>A read that should have been translated was read in the original text, or a translation pack could not be had.</summary>
    public const string Translation = "translation";

    // Part C (packs, OCR, documents, store and config).
    /// <summary>A voice, storyteller or audio pack could not be installed or kept in line; the feature uses what it had.</summary>
    public const string Pack = "pack";

    /// <summary>A scan or picture was read by another recogniser than the one that should have, or the scanner could not start.</summary>
    public const string Ocr = "ocr";

    /// <summary>A document import fell back to a simpler reader.</summary>
    public const string Document = "document";

    /// <summary>Dino Reader: the story store or the book update check could not be reached or read.</summary>
    public const string Store = "store";

    /// <summary>The remote config could not be fetched; the last good copy, or the default, stayed in force.</summary>
    public const string Config = "config";

    /// <summary>The phone's language is not one the app is translated into; English is shown.</summary>
    public const string UiLanguage = "ui_language";

    // Part D (platform gaps and the swallowed-error class).
    /// <summary>A window could not be framed or placed as intended; the default frame or a corrected place is used.</summary>
    public const string Window = "window";

    /// <summary>iOS: a named font was not found; the system font stands in.</summary>
    public const string Font = "font";

    /// <summary>Dino Reader: the shelf's stored books or their order could not be read; the default is used.</summary>
    public const string Library = "library";

    // Slice 275.
    /// <summary>Windows: the desktop's selection could not be watched (UI Automation); the hotkey still reads.</summary>
    public const string Input = "input";

    // Slice 279.
    /// <summary>Dino Reader: a tap's action threw and was caught, so the tap did nothing visible.</summary>
    public const string Ui = "ui";

    public static IReadOnlyList<string> All { get; } = [PremadeAudio, Speech, Translation, Pack, Ocr, Document, Store, Config, UiLanguage, Window, Font, Library, Input, Ui];
}

/// <summary>Why it fell back (Slice 273): the closed list of the <c>fallback</c> count's reason; the Worker's <c>FALLBACK_REASONS</c> must be the same list.</summary>
public static class FallbackReason
{
    public const string NoPack = "no_pack";
    public const string PackFailed = "pack_failed";
    public const string TextMismatch = "text_mismatch";
    public const string ClipMissing = "clip_missing";

    /// <summary>Some pieces of a read had a clip and some fell back to the device voice.</summary>
    public const string Partial = "partial";

    // Part B, speech (area "speech").
    /// <summary>The neural voice's model was not ready, so the phone or Windows voice read.</summary>
    public const string NeuralNotReady = "neural_not_ready";

    /// <summary>The model went away (or could not be loaded) after the readiness check.</summary>
    public const string NeuralUnavailable = "neural_unavailable";

    /// <summary>A streamed neural read could not start; the whole text is read by the Windows voice.</summary>
    public const string StreamUnavailable = "stream_unavailable";

    /// <summary>The neural engine could not be built from an installed model.</summary>
    public const string EngineBuildFailed = "engine_build_failed";

    /// <summary>A read by voice segments was read as one joined text in the phone voice.</summary>
    public const string SegmentsUnsupported = "segments_unsupported";

    /// <summary>The phone voice cannot speak the text's language; the natural voice reads instead.</summary>
    public const string PhoneCannotSpeak = "phone_cannot_speak";

    /// <summary>No voice can read the text's language, so the read was not started.</summary>
    public const string NoVoice = "no_voice";

    /// <summary>A silent take was generated again and the new take was voiced.</summary>
    public const string SilentRetake = "silent_retake";

    /// <summary>A silent voice was replaced by the pack's sibling voice.</summary>
    public const string SilentSibling = "silent_sibling";

    /// <summary>A voice stayed silent through every retake and the sibling; the clip was played as it was.</summary>
    public const string SilentGaveUp = "silent_gave_up";

    /// <summary>Slice 341: a drop-prone voice's take was only a quiet hiss (the span rule alone would have played it); a new take was voiced.</summary>
    public const string QuietRetake = "quiet_retake";

    /// <summary>Slice 341: a drop-prone voice's quiet hiss was replaced by the pack's sibling voice.</summary>
    public const string QuietSibling = "quiet_sibling";

    /// <summary>Slice 341: a drop-prone voice stayed a quiet hiss through every retake and the sibling; the best take was played as it was.</summary>
    public const string QuietGaveUp = "quiet_gave_up";

    /// <summary>Dino Reader: the voice a book suggested for a character is not in the language's roster; the closest roster voice by traits is used.</summary>
    public const string VoiceNotInRoster = "voice_not_in_roster";

    /// <summary>Dino Reader: a grown-up's recording could not be played; the generated voice reads.</summary>
    public const string RecordingUnplayable = "recording_unplayable";

    /// <summary>The phone's text-to-speech has no voice for the text's language; its default voice reads.</summary>
    public const string PhoneLanguageMissing = "phone_language_missing";

    // Part B, translation (area "translation").
    public const string TranslationTimeout = "translation_timeout";
    public const string TranslationPackMissing = "translation_pack_missing";
    public const string TranslationError = "translation_error";

    /// <summary>A streamed translation could not be planned; the whole text is translated instead.</summary>
    public const string StreamPlanFailed = "stream_plan_failed";

    /// <summary>A streamed translation stopped part-way; the rest was read untranslated.</summary>
    public const string StreamPartial = "stream_partial";

    /// <summary>Dino Reader: the language model for the story's pair is gone, so the story is read as written.</summary>
    public const string TranslationModelMissing = "translation_model_missing";

    /// <summary>Dino Reader: a translated read was skipped and the story was read as written.</summary>
    public const string TranslationSkipped = "translation_skipped";

    /// <summary>A translation pack could not be downloaded.</summary>
    public const string TranslationPackFailed = "translation_pack_failed";

    // Part C, packs (area "pack"; a pack that could not be installed at all reuses PackFailed).
    /// <summary>Dino Reader: the audio pack sync or sweep failed; it is tried again at the next check.</summary>
    public const string PackSyncFailed = "pack_sync_failed";

    /// <summary>Dino Reader: the storyteller voice pack could not be had; the old voices stay.</summary>
    public const string StorytellerFailed = "storyteller_failed";

    /// <summary>Dino Reader: casting the books with a new storyteller failed; the old voices stay.</summary>
    public const string RecastFailed = "recast_failed";

    /// <summary>Dino Reader: the voice catalogue or a story language's pack could not be read or fetched.</summary>
    public const string CatalogueFailed = "catalogue_failed";

    // Part C, OCR (area "ocr").
    /// <summary>No OCR pack read the page, so the phone's built-in recogniser's reading was used.</summary>
    public const string OcrBuiltinNoPack = "ocr_builtin_no_pack";

    /// <summary>The phone's built-in recogniser failed, so the installed OCR packs were tried.</summary>
    public const string OcrBuiltinFailed = "ocr_builtin_failed";

    /// <summary>Windows: the page was read by the platform recogniser instead of an installed pack.</summary>
    public const string OcrPlatformFallback = "ocr_platform_fallback";

    /// <summary>An OCR pack could not be installed or run.</summary>
    public const string OcrPackFailed = "ocr_pack_failed";

    /// <summary>The document scanner could not start.</summary>
    public const string ScannerStartFailed = "scanner_start_failed";

    // Part C, documents (area "document").
    public const string PdfImportFailed = "pdf_import_failed";

    // Part C, store and updates (area "store").
    public const string StoreUnreachable = "store_unreachable";
    public const string UpdateCheckFailed = "update_check_failed";

    // Part C, config (area "config"): the last good copy stayed in force, or no copy exists and the default does.
    public const string ConfigCached = "config_cached";
    public const string ConfigDefault = "config_default";

    // Part C, UI language (area "ui_language").
    public const string UiLanguageUnsupported = "ui_language_unsupported";

    // Part D, platform gaps and swallowed errors.
    /// <summary>Dino Reader (area "speech"): the storyteller pack would serve this language but is not installed; the stand-in voices cast it.</summary>
    public const string StorytellerPackMissing = "storyteller_pack_missing";

    /// <summary>Dino Reader (area "speech"): an animal's sound clip could not be played and was skipped.</summary>
    public const string AnimalClipUnplayable = "animal_clip_unplayable";

    /// <summary>Dino Reader (area "premade_audio"): a pre-made clip could not be played; the device voice read that piece.</summary>
    public const string ClipUnplayable = "clip_unplayable";

    /// <summary>Dino Reader (area "premade_audio"): a clip's playback speed could not be set; it plays at its own speed.</summary>
    public const string ClipSpeedUnset = "clip_speed_unset";

    /// <summary>The in-app update could not start; the store page opens instead.</summary>
    public const string InAppUpdateFailed = "in_app_update_failed";

    /// <summary>Windows (area "window"): the window frame could not be applied; the default frame stays.</summary>
    public const string WindowFrameFailed = "window_frame_failed";

    /// <summary>Windows (area "window"): the displays could not be listed; the primary and the window's own display are used.</summary>
    public const string DisplaysUnreadable = "displays_unreadable";

    /// <summary>Windows (area "window"): the remembered position was off the work area and the panel was moved onto it.</summary>
    public const string SavedPositionMoved = "saved_position_moved";

    /// <summary>Windows (area "window"): a monitor's DPI could not be read; 96 is used.</summary>
    public const string DpiDefaulted = "dpi_defaulted";

    /// <summary>iOS (area "font"): a named font was not found; the system font stands in.</summary>
    public const string FontFallback = "font_fallback";

    /// <summary>Dino Reader (area "library"): the stored shelf order could not be read; the default order is used.</summary>
    public const string ShelfOrderUnreadable = "shelf_order_unreadable";

    /// <summary>Dino Reader (area "library"): a stored book could not be read and is left off the shelf.</summary>
    public const string StoryUnreadable = "story_unreadable";

    /// <summary>Read The Stupid Text (area "translation"): the text's language could not be detected, so no translation is offered.</summary>
    public const string LanguageDetectFailed = "language_detect_failed";

    /// <summary>Read The Stupid Text (area "ocr"): a scanned page could not be read at all and is left unread.</summary>
    public const string PageReadFailed = "page_read_failed";

    /// <summary>Dino Reader (area "config"): the saved intro step could not be resumed and was cleared, so the intro starts again.</summary>
    public const string IntroStateReset = "intro_state_reset";

    /// <summary>Slice 275 (area "ocr"): a line could not be cut from the full-size picture, so a lower-resolution crop of the detection image was recognised; small print may read worse.</summary>
    public const string OcrCropDegraded = "ocr_crop_degraded";

    /// <summary>Slice 275 (area "input"): UI Automation could not register for selection changes, so only the hotkey reads selected text.</summary>
    public const string UiaUnavailable = "uia_unavailable";

    /// <summary>Slice 275 (area "store"): the store answered but the listing lost entries, or a download failed for a reason that is not the network (full disk, bad package, unexpected error).</summary>
    public const string StoreFailed = "store_failed";

    /// <summary>Dino Reader (area "config"): a setting could not be read or written, so it follows its default.</summary>
    public const string PreferenceUnsaved = "preference_unsaved";

    /// <summary>Slice 279 (area "ui"): a tap handler threw; the error was caught and the tap did nothing.</summary>
    public const string TapFailed = "tap_failed";

    /// <summary>Slice 275 (area "ui"): a page of the story could not be laid out, so the reading card shows the error text instead.</summary>
    public const string LayoutFailed = "layout_failed";

    /// <summary>Slice 351 (area "premade_audio"): the pack has an entry for this piece but its file is not on disk.</summary>
    public const string FileMissing = "file_missing";

    /// <summary>Slice 351 (area "premade_audio"): the pack has no clip for this piece (an untranslated or new line).</summary>
    public const string NoClip = "no_clip";

    /// <summary>Slice 351 (area "speech"): copying an animal's bundled clip to the cache failed.</summary>
    public const string AnimalClipCopyFailed = "animal_clip_copy_failed";

    /// <summary>Slice 351 (area "speech"): the player threw while playing an animal's clip.</summary>
    public const string AnimalClipPlayFailed = "animal_clip_play_failed";

    // Slice 359.
    /// <summary>Slice 359 (area "library"): a book names a drawn back-cover picture but the file is missing, so the live credits page of Slice 237 is shown instead.</summary>
    public const string BackCoverMissing = "back_cover_missing";

    public static IReadOnlyList<string> All { get; } =
    [
        NoPack, PackFailed, TextMismatch, ClipMissing, Partial,
        NeuralNotReady, NeuralUnavailable, StreamUnavailable, EngineBuildFailed, SegmentsUnsupported, PhoneCannotSpeak, NoVoice, SilentRetake, SilentSibling, SilentGaveUp, QuietRetake, QuietSibling, QuietGaveUp,
        VoiceNotInRoster, RecordingUnplayable, PhoneLanguageMissing,
        TranslationTimeout, TranslationPackMissing, TranslationError, StreamPlanFailed, StreamPartial, TranslationModelMissing, TranslationSkipped, TranslationPackFailed,
        PackSyncFailed, StorytellerFailed, RecastFailed, CatalogueFailed,
        OcrBuiltinNoPack, OcrBuiltinFailed, OcrPlatformFallback, OcrPackFailed, ScannerStartFailed,
        PdfImportFailed, StoreUnreachable, UpdateCheckFailed, ConfigCached, ConfigDefault, UiLanguageUnsupported,
        StorytellerPackMissing, AnimalClipUnplayable, ClipUnplayable, ClipSpeedUnset, InAppUpdateFailed, WindowFrameFailed, DisplaysUnreadable, SavedPositionMoved, DpiDefaulted,
        FontFallback, ShelfOrderUnreadable, StoryUnreadable, LanguageDetectFailed, PageReadFailed, IntroStateReset,
        OcrCropDegraded, UiaUnavailable, StoreFailed, PreferenceUnsaved, TapFailed, LayoutFailed,
        FileMissing, NoClip, AnimalClipCopyFailed, AnimalClipPlayFailed,
        BackCoverMissing,
    ];
}

/// <summary>Reads a fallback kind written as <c>area:reason</c> (the Debug test hook's <c>id</c> argument, Slice 273 part C).</summary>
public static class FallbackKinds
{
    private const char Separator = ':';

    /// <summary>The area and reason of <paramref name="kind"/>; a blank kind is the part B default, a speech fallback with <see cref="FallbackReason.NeuralNotReady"/>. Values are not checked here: an unlisted one is dropped by the sender.</summary>
    public static (string Area, string Reason) Split(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return (FallbackArea.Speech, FallbackReason.NeuralNotReady);
        }

        int at = kind.IndexOf(Separator);
        return at < 0 ? (kind, string.Empty) : (kind[..at], kind[(at + 1)..]);
    }
}

/// <summary>
/// One fallback kind for one book and language, with how many times it happened in the session (Slice 273). <see cref="Book"/> is the book's real id (an own book's too), or empty for a count about no book.
/// </summary>
public readonly record struct FallbackTotal(string Area, string Reason, string Book, string Language, int Count);

/// <summary>
/// Counts the fallbacks of one app session (Slice 273), shared by both apps, so a fallback inside a loop is one row, not hundreds: the first of each (area, reason, book, language) is sent at once with
/// count 1 (<see cref="IsFirst"/>); on any flush <see cref="TakeRepeats"/> hands out one row per kind whose count is above what was already sent, with the session total, and marks it sent, so a row never repeats for an
/// unchanged count. Queries take max(count) per session, area, reason and book. In memory only; a new launch starts at zero.
/// </summary>
public sealed class FallbackTally
{
    private const int FirstTime = 1;
    private readonly object _lock = new();
    private readonly Dictionary<(string Area, string Reason, string Book, string Language), (int Count, int Sent)> _counts = [];

    /// <summary>Notes one more fallback of <paramref name="area"/> and <paramref name="reason"/> for <paramref name="book"/> (its real id, or empty for no book) read in <paramref name="language"/> and returns how many there are now.</summary>
    public int Note(string area, string reason, string book = "", string language = "")
    {
        lock (_lock)
        {
            (int count, int sent) = _counts.GetValueOrDefault((area, reason, book, language), (0, 0));
            _counts[(area, reason, book, language)] = (count + 1, count == 0 ? FirstTime : sent);
            return count + 1;
        }
    }

    /// <summary>True for the first fallback of its kind in the session, the only one sent at once.</summary>
    public static bool IsFirst(int count) => count == FirstTime;

    /// <summary>
    /// The kinds that happened again since their last row, each with its total so far; asking marks them sent, so a second flush with nothing new returns none.
    /// </summary>
    public IReadOnlyList<FallbackTotal> TakeRepeats()
    {
        lock (_lock)
        {
            List<FallbackTotal> repeats = [];
            foreach (((string area, string reason, string book, string language), (int count, int sent)) in _counts.ToArray())
            {
                if (count > sent)
                {
                    repeats.Add(new FallbackTotal(area, reason, book, language, count));
                    _counts[(area, reason, book, language)] = (count, count);
                }
            }

            return repeats;
        }
    }
}
