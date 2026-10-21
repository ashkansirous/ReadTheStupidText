namespace ReadTheStupidText.Application.Translation;

/// <summary>A translation model is not on the device and could not be downloaded (offline, no Play services, no storage).</summary>
public sealed class TranslationModelUnavailableException : Exception
{
    public TranslationModelUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
