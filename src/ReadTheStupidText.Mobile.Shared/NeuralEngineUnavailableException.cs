namespace ReadTheStupidText.Mobile;

/// <summary>
/// Thrown by a neural reader, before anything is spoken, when no engine could be acquired for
/// the read (the model went away between the readiness check and the read, or the reader was
/// disposed). <see cref="CompositeSpeechReader"/> catches it and reads with the phone voice,
/// so a read is never silently dropped.
/// </summary>
public sealed class NeuralEngineUnavailableException : InvalidOperationException
{
    private const string DefaultMessage = "No neural engine is available for this read.";

    public NeuralEngineUnavailableException()
        : base(DefaultMessage)
    {
    }

    public NeuralEngineUnavailableException(Exception innerException)
        : base(DefaultMessage, innerException)
    {
    }
}
