namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// A reader that advertised streaming could not start the streamed read (Slice 75), for example because the
/// neural model went away after the readiness check. The caller falls back to speaking the whole text.
/// </summary>
public sealed class StreamingUnavailableException : Exception
{
    public StreamingUnavailableException()
        : base("The speech reader cannot stream this read.")
    {
    }

    public StreamingUnavailableException(Exception innerException)
        : base("The speech reader cannot stream this read.", innerException)
    {
    }
}
