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

    /// <summary>The built-in OS speech engine used until the voice pack is ready.</summary>
    ISpeechReader CreateFallbackReader();
}
