namespace Sirous.Reading.Application.Usage;

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

    // Slice 425: story_unreadable is no longer sent by the app (the Worker still accepts it for old builds and for a speaker's fault); each site has its own reason. Area "library".
    /// <summary>Dino Reader: a stored book's id differs from its folder name; it is left off the shelf. Names the store book when it is one.</summary>
    public const string StoryIdMismatch = "story_id_mismatch";

    /// <summary>Dino Reader: a stored book's file is empty; left off the shelf. Names the store book when it is one.</summary>
    public const string StoryEmpty = "story_empty";

    /// <summary>Dino Reader: a stored book's file is not valid; left off the shelf. Names the store book when it is one.</summary>
    public const string StoryInvalid = "story_invalid";

    /// <summary>Dino Reader: a stored book's file is from a newer version or has no valid id; left off the shelf. Names the store book when it is one.</summary>
    public const string StoryNewerVersion = "story_newer_version";

    /// <summary>Dino Reader: a stored book has a back cover but no pages; left off the shelf. Names the store book when it is one.</summary>
    public const string StoryNoPages = "story_no_pages";

    /// <summary>Dino Reader: a stored book's file is missing required fields; left off the shelf. Names the store book when it is one.</summary>
    public const string StoryMissingFields = "story_missing_fields";

    /// <summary>Dino Reader: the optional part-file listing of a book failed (Slice 464).</summary>
    public const string StoryListingCleanupFailed = "story_listing_cleanup_failed";

    /// <summary>Dino Reader: the shelf rebuild failed because a store call ran out of time.</summary>
    public const string ShelfRebuildTimeout = "shelf_rebuild_timeout";

    /// <summary>Dino Reader: the shelf rebuild was cancelled part-way.</summary>
    public const string ShelfRebuildCancelled = "shelf_rebuild_cancelled";

    /// <summary>Dino Reader: the shelf rebuild failed on a file or network error.</summary>
    public const string ShelfRebuildIo = "shelf_rebuild_io";

    /// <summary>Dino Reader: the shelf rebuild failed building its screen.</summary>
    public const string ShelfRebuildLayout = "shelf_rebuild_layout";

    /// <summary>Dino Reader: the shelf rebuild failed reading a book, package or backup.</summary>
    public const string ShelfRebuildLibrary = "shelf_rebuild_library";

    /// <summary>Dino Reader: the shelf rebuild failed for another reason.</summary>
    public const string ShelfRebuildOther = "shelf_rebuild_other";

    /// <summary>Dino Reader: listing the own-book folders or the own-book cleanup pass failed.</summary>
    public const string OwnBooksCleanupFailed = "own_books_cleanup_failed";

    /// <summary>Dino Reader: deleting one own book failed; it stays and the next start tries again.</summary>
    public const string OwnBookDeleteFailed = "own_book_delete_failed";

    /// <summary>Dino Reader: one store book from a backup could not be applied. Names the store book.</summary>
    public const string BackupBookUnapplied = "backup_book_unapplied";

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

    /// <summary>Slice 420 (area "input"): moving the keyboard focus one row on the Windows Settings tab threw; the arrow key is left unhandled.</summary>
    public const string FocusMoveFailed = "focus_move_failed";

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

    // Slice 360.
    /// <summary>Slice 360 (area "premade_audio"): a read needed a pack that was waiting for Wi-Fi (the Wi-Fi wait is gone; the reason stays so older rows and builds still parse).</summary>
    public const string WaitingWifi = "waiting_wifi";

    /// <summary>Slice 360 (area "premade_audio"): a piece had no sound because its book's pack for the reading language is missing on the phone while the child played.</summary>
    public const string PackMissingOnPlay = "pack_missing_on_play";

    /// <summary>Slice 358 (area "store"): the platform store answered but gave no price for a paid book's product (unknown or inactive product, account not allowed to see it), so the card cannot offer Buy.</summary>
    public const string PricesMissing = "prices_missing";

    /// <summary>Slice 358 (area "store"): the platform store was reached but its price query ended in an error (Play response code not OK, StoreKit request failed or timed out, Windows Store error); the code is in the Debug log.</summary>
    public const string BillingError = "billing_error";

    // Slice 346 part E.
    /// <summary>Slice 346 E2 (area "store"): the Worker refused a paid book's files with 402 (no purchase proof sent) or a 400; the book stays "Try again".</summary>
    public const string DownloadRefused = "download_refused";

    /// <summary>Slice 346 E2 (area "store"): the Worker refused a paid book's files with 403 (the store says the purchase is not valid), after any retry for a purchase just made.</summary>
    public const string PurchaseNotValid = "purchase_not_valid";

    // Slice 362.
    /// <summary>Slice 362 (area "library"): the grown-up's recorded try of the onboarding could not be deleted from a book; the next start retries.</summary>
    public const string OnboardingTryNotDeleted = "onboarding_try_not_deleted";

    // Slice 460: why a store call failed (area "store"), the closed list of causes. store_unreachable and store_failed stay accepted for older builds and for "other".
    /// <summary>Slice 460 (area "store"): the call ran out of time (the list's 20 s, a stalled body).</summary>
    public const string StoreTimeout = "store_timeout";

    /// <summary>Slice 460 (area "store"): the device had no route to the server (connection refused or reset, network down).</summary>
    public const string StoreNoNetwork = "store_no_network";

    /// <summary>Slice 460 (area "store"): the server's name could not be resolved.</summary>
    public const string StoreDns = "store_dns";

    /// <summary>Slice 460 (area "store"): the secure connection could not be set up (certificate, clock, protocol).</summary>
    public const string StoreTls = "store_tls";

    /// <summary>Slice 460 (area "store"): the server answered 5xx.</summary>
    public const string StoreHttp5xx = "store_http_5xx";

    /// <summary>Slice 460 (area "store"): the server answered 4xx that is not one of the Worker's own refusals.</summary>
    public const string StoreHttp4xx = "store_http_4xx";

    /// <summary>Slice 460 (area "store", Windows): the Microsoft Store says the app is not associated with the Store (0x803F6107), so it cannot be asked.</summary>
    public const string StoreNotAssociated = "store_not_associated";

    /// <summary>Slice 460 (area "store", Windows): the app's window did not exist yet when the Store context was needed.</summary>
    public const string StoreNoWindow = "store_no_window";

    /// <summary>Slice 460 (area "store"): the purchase read did not finish inside the settle budget, so the files were asked for without it.</summary>
    public const string StoreSettleTimeout = "store_settle_timeout";

    /// <summary>Slice 463 (area "store"): the background download that replaces a quick first book with the standard package failed (offline, killed, or the package did not verify); the book stays quick and the next start tries again.</summary>
    public const string QuickUpgradeFailed = "quick_upgrade_failed";

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
        OcrCropDegraded, UiaUnavailable, StoreFailed, PreferenceUnsaved, TapFailed, LayoutFailed, FocusMoveFailed,
        FileMissing, NoClip, AnimalClipCopyFailed, AnimalClipPlayFailed,
        BackCoverMissing, WaitingWifi, PackMissingOnPlay, PricesMissing, BillingError, DownloadRefused, PurchaseNotValid, OnboardingTryNotDeleted,
        StoreTimeout, StoreNoNetwork, StoreDns, StoreTls, StoreHttp5xx, StoreHttp4xx, StoreNotAssociated, StoreNoWindow, StoreSettleTimeout,
        QuickUpgradeFailed,
        StoryIdMismatch, StoryEmpty, StoryInvalid, StoryNewerVersion, StoryNoPages, StoryMissingFields, StoryListingCleanupFailed,
        ShelfRebuildTimeout, ShelfRebuildCancelled, ShelfRebuildIo, ShelfRebuildLayout, ShelfRebuildLibrary, ShelfRebuildOther,
        OwnBooksCleanupFailed, OwnBookDeleteFailed, BackupBookUnapplied,
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
