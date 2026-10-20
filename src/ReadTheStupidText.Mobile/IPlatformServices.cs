using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The one seam between the platform-neutral Mobile root and each OS's engines
/// (Slice 44, Decision 69). Implemented under <c>Platforms/&lt;OS&gt;/</c> and registered
/// from there via <c>MauiProgram.RegisterPlatformServices</c>, so nothing outside
/// <c>Platforms/</c> names an Android or iOS type. Each <c>Create*</c> returns a new
/// instance owned by the caller and is called once at startup.
/// </summary>
public interface IPlatformServices
{
    /// <summary>The local Supertonic-3 neural engine for this platform.</summary>
    ISpeechReader CreateNeuralReader();

    /// <summary>The built-in OS speech engine used until the voice pack is ready.</summary>
    ISpeechReader CreateFallbackReader();

    /// <summary>The on-device OCR used by the camera scan path.</summary>
    IImageTextExtractor CreateImageTextExtractor();
}
