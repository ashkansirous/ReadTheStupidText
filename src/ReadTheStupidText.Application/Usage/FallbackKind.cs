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

    public static IReadOnlyList<string> All { get; } = [PremadeAudio, Speech, Translation];
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

    public static IReadOnlyList<string> All { get; } =
    [
        NoPack, PackFailed, TextMismatch, ClipMissing, Partial,
        NeuralNotReady, NeuralUnavailable, StreamUnavailable, EngineBuildFailed, SegmentsUnsupported, PhoneCannotSpeak, NoVoice, SilentRetake, SilentSibling, SilentGaveUp,
        VoiceNotInRoster, RecordingUnplayable, PhoneLanguageMissing,
        TranslationTimeout, TranslationPackMissing, TranslationError, StreamPlanFailed, StreamPartial, TranslationModelMissing, TranslationSkipped, TranslationPackFailed,
    ];
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
