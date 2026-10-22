using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

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
    /// The phone voice's languages, plus every app language when the platform runs natural packs
    /// (the catalog offers a pack for each, so a language is never hidden for want of one).
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
            ? Enum.GetValues<AppLanguage>().Select(AppLanguages.Code)
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
