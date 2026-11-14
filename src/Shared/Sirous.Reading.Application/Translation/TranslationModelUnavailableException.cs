namespace Sirous.Reading.Application.Translation;

/// <summary>A translation model is not on the device and could not be downloaded (offline, no Play services, no storage).</summary>
public sealed class TranslationModelUnavailableException : Exception
{
    public TranslationModelUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }

    /// <summary>Slice 273 part B: the engine already counted this failure as a fallback where it happened (a pack download that failed), so the code that catches it counts nothing more: one failure is one row.</summary>
    public bool FallbackReported { get; init; }
}
