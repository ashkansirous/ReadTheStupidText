using Sirous.Reading.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PackDownloadLogGlobalSinkCollection
{
    public const string Name = "PackDownloadLogGlobalSink";
}

/// <summary>
/// Slice 388: this test sets the process-wide PackDownloadLog.Sink, so it runs alone (a parallel test's PACKDL line
/// would reach the throwing sink, and overlapping save/restore could leave a stale sink installed).
/// </summary>
[Collection(PackDownloadLogGlobalSinkCollection.Name)]
public sealed class PackDownloadLogGlobalSinkTests
{
    [Fact]
    public void ThrowingGlobalSinkNeverEscapes()
    {
        Action<string>? previous = PackDownloadLog.Sink;
        PackDownloadLog.Sink = _ => throw new InvalidOperationException("sink");
        try
        {
            PackDownloadLog.Start("p-throw-global", 0);
        }
        finally
        {
            PackDownloadLog.Sink = previous;
        }
    }
}
