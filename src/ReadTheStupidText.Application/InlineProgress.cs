namespace ReadTheStupidText.Application;

/// <summary>
/// Runs the callback on the reporting thread. <see cref="Progress{T}"/> posts to the thread pool, so a
/// late tick could land after the final terminal state was published.
/// </summary>
public sealed class InlineProgress(Action<double> report) : IProgress<double>
{
    public void Report(double value) => report(value);
}
