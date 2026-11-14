using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

public class ClipMissesTests
{
    [Fact]
    public void ALookedUpAgainPieceCountsOnce()
    {
        var misses = new ClipMisses();
        misses.Note(3, ClipMiss.TextMismatch);
        misses.Note(3, ClipMiss.TextMismatch);
        misses.Note(4, ClipMiss.FileMissing);
        misses.Note(4, ClipMiss.FileMissing);
        misses.Note(5, ClipMiss.None);

        Assert.Equal(1, misses.TextMismatch);
        Assert.Equal(1, misses.FileMissing);
    }

    [Fact]
    public void ReasonIsTextMismatchWhenAnyHashMissed()
    {
        var misses = new ClipMisses();
        misses.Note(1, ClipMiss.FileMissing);
        Assert.Equal(DeviceVoiceReason.ClipMissing, misses.Reason);

        misses.Note(2, ClipMiss.TextMismatch);
        Assert.Equal(DeviceVoiceReason.TextMismatch, misses.Reason);
    }
}
