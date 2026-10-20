using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText.Tests;

public class AudioChunkSweepTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0);
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(1);

    [Fact]
    public void Folder_younger_than_max_age_is_not_eligible()
    {
        DateTime lastWrite = Now - TimeSpan.FromHours(23);

        Assert.False(AudioChunkSweep.IsEligible(lastWrite, Now, MaxAge));
    }

    [Fact]
    public void Folder_exactly_at_max_age_is_eligible()
    {
        DateTime lastWrite = Now - MaxAge;

        Assert.True(AudioChunkSweep.IsEligible(lastWrite, Now, MaxAge));
    }

    [Fact]
    public void Folder_older_than_max_age_is_eligible()
    {
        DateTime lastWrite = Now - TimeSpan.FromDays(3);

        Assert.True(AudioChunkSweep.IsEligible(lastWrite, Now, MaxAge));
    }

    [Fact]
    public void Folder_written_after_now_is_not_eligible()
    {
        DateTime lastWrite = Now + TimeSpan.FromMinutes(1);

        Assert.False(AudioChunkSweep.IsEligible(lastWrite, Now, MaxAge));
    }
}
