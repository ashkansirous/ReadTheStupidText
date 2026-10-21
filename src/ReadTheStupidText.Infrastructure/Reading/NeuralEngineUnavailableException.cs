namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// The neural engine could not be acquired for a read (no installed pack serves the language any more).
/// <see cref="CompositeSpeechReader"/> catches it and reads with the Windows voice instead.
/// </summary>
public sealed class NeuralEngineUnavailableException : Exception
{
    public NeuralEngineUnavailableException()
        : base("No neural voice model is ready.")
    {
    }

    public NeuralEngineUnavailableException(Exception innerException)
        : base("The neural voice engine could not be built.", innerException)
    {
    }
}
