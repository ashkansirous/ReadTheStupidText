using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// The one seam between the platform-neutral Mobile root and each OS's engines
/// (Slice 44, Decision 69). Implemented under <c>Platforms/&lt;OS&gt;/</c> and registered
/// from there by <c>MobileSharedServices.RegisterPlatformServices</c> (Mobile.Shared); apps must
/// not register it themselves. So nothing outside
/// <c>Platforms/</c> names an Android or iOS type. Each <c>Create*</c> returns a new
/// instance owned by the caller and is called once at startup.
/// </summary>
public interface IPlatformServices
{
    /// <summary>
    /// Whether this platform can run the downloaded Supertonic-3 voice pack. When false the
    /// pack is never fetched and every read uses the built-in OS voice.
    /// </summary>
    bool SupportsNeuralVoice { get; }

    /// <summary>
    /// Whether this platform can read text from a camera image. When false the Reader hides
    /// the Scan action (iOS until a Vision-based extractor lands).
    /// </summary>
    bool SupportsTextScan { get; }

    /// <summary>The local Supertonic-3 neural engine for this platform.</summary>
    ISpeechReader CreateNeuralReader();

    /// <summary>The built-in OS speech engine used until the voice pack is ready. May return a shared instance; callers must not dispose it.</summary>
    ISpeechReader CreateFallbackReader();

    /// <summary>
    /// The language codes (primary subtag) the built-in OS voice can speak (Slice 82); an empty
    /// list means it speaks none, null means it can't be asked (never hides or blocks anything).
    /// May wait for the OS engine to start.
    /// </summary>
    Task<IReadOnlyCollection<string>?> PhoneVoiceLanguagesAsync() => Task.FromResult<IReadOnlyCollection<string>?>(null);

    /// <summary>Forgets what the OS engine said about its languages (voice data may have been
    /// installed meanwhile) and lets a failed engine start be retried once more.</summary>
    void InvalidatePhoneVoices()
    {
    }
}

/// <summary>
/// Implemented by a reader whose voice follows the language of the text it is handed
/// (Slice 82), so the phone voice is switched to it instead of staying on the device locale.
/// </summary>
public interface IReadingLanguage
{
    /// <summary>The language code of the text of the next read.</summary>
    void SetReadingLanguage(string languageCode);
}

/// <summary>Builds the <see cref="VoiceAvailability"/> every page uses to decide what to offer.</summary>
public static class PlatformVoiceAvailability
{
    /// <summary>
    /// The phone voice's languages, plus every language a pack declares when the platform runs natural packs
    /// (<see cref="PackConstants.NaturallyReadable"/>).
    /// </summary>
    public static Task<VoiceAvailability> BuildAsync(IPlatformServices platform)
    {
        if (Known is { } cached)
        {
            return Task.FromResult(cached);
        }

        // Concurrent callers (reader page, read gate, picker) share one query.
        return _inFlight ??= QueryAsync(platform, _generation);
    }

    /// <summary>The last real answer, shared by every page; null until the phone has been asked.</summary>
    public static VoiceAvailability? Known { get; private set; }

    private static Task<VoiceAvailability>? _inFlight;
    private static int _generation;

    /// <summary>
    /// Drops the cached answer so the next caller asks again: on app resume (the user may have
    /// installed voice data in system settings) and when a voice pack install completes.
    /// </summary>
    public static void Invalidate(IPlatformServices platform)
    {
        _generation++;
        Known = null;
        _inFlight = null;
        platform.InvalidatePhoneVoices();
    }

    private static async Task<VoiceAvailability> QueryAsync(IPlatformServices platform, int generation)
    {
        IReadOnlyCollection<string>? phone = await AskPhoneAsync(platform);
        IEnumerable<string> natural = platform.SupportsNeuralVoice
            ? PackConstants.NaturallyReadable.Select(AppLanguages.Code)
            : [];
        var availability = new VoiceAvailability(phone, natural);

        // Only a real answer is kept, and only if nothing invalidated it meanwhile; an unknown
        // phone is asked again on the next call.
        if (generation == _generation)
        {
            _inFlight = null;
            if (availability.PhoneKnown)
            {
                Known = availability;
            }
        }

        return availability;
    }

    // A platform that fails to answer is an unknown phone, not one that reads nothing.
    private static async Task<IReadOnlyCollection<string>?> AskPhoneAsync(IPlatformServices platform)
    {
        try
        {
            return await platform.PhoneVoiceLanguagesAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>A neural engine that can read, or prepare a head start, with a pack named per call (Slice 90).</summary>
public interface IModelScopedReader
{
    /// <summary>Reads <paramref name="text"/> with <paramref name="model"/> instead of the app-language model.</summary>
    Task SpeakAsync(string text, VoiceModelPaths model, int? activityId = null);

    /// <summary>Prepares the head start with <paramref name="model"/> (null = the app-language model).</summary>
    Task PrepareAsync(string text, VoiceModelPaths? model);
}

/// <summary>A neural engine that can read voiced segments with a pack named per call (Slice 173: Dino Reader's child reads in a language other than the stored one).</summary>
public interface IModelScopedSegmentReader
{
    /// <summary>Reads <paramref name="segments"/> in their voices with <paramref name="model"/> instead of the app-language model.</summary>
    Task SpeakSegmentsAsync(IReadOnlyList<VoicedSegment> segments, VoiceModelPaths model, int? activityId = null);

    /// <summary>Warms the engine of <paramref name="model"/> in the background (Slice 177's rules: never evicts an engine in use), so the first read in that pack starts fast.</summary>
    Task WarmUpAsync(VoiceModelPaths model);
}

/// <summary>
/// A reader that can read, and prepare a head start, in the text's language (Slice 90). The scope is an
/// argument of the one read it belongs to, never state on the reader, so it cannot leak into or be
/// taken by another read.
/// </summary>
public interface IReadScoped
{
    /// <summary>
    /// Whether the natural engine can read with a pack named per read. Where it cannot (iOS), only the
    /// app language reads naturally and any other language falls to the phone voice.
    /// </summary>
    bool SupportsModelScope { get; }

    /// <summary>Reads <paramref name="text"/> the way <paramref name="scope"/> says, for this read only.</summary>
    Task SpeakScopedAsync(string text, ReadVoiceScope scope, int? activityId = null);

    /// <summary>Warms the engine of <paramref name="model"/> (the child's pick, Slice 173) ahead of the first read in it; nothing when the engine cannot take a named pack.</summary>
    Task WarmUpScopedAsync(VoiceModelPaths model);

    /// <summary>
    /// Reads <paramref name="segments"/> in their voices the way <paramref name="scope"/> says, for this read only (Slice 173): with the scope's pack when the
    /// engine can, else with the phone voice in the scope's language.
    /// </summary>
    Task SpeakSegmentsScopedAsync(IReadOnlyList<VoicedSegment> segments, ReadVoiceScope scope, int? activityId = null);

    /// <summary>Prepares the head start the way <paramref name="scope"/> would read.</summary>
    Task PrepareAsync(string text, ReadVoiceScope scope);
}
