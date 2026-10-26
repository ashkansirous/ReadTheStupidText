using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class HeadStartRunTests
{
    [Fact]
    public async Task A_preparation_that_stored_the_clips_completes_normally()
    {
        await HeadStartRun.CompleteAsync(CancellationToken.None, _ => Task.FromResult(true));
    }

    [Fact]
    public async Task A_preparation_that_stored_nothing_ends_cancelled()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => HeadStartRun.CompleteAsync(CancellationToken.None, _ => Task.FromResult(false)));
    }

    [Fact]
    public async Task A_preparation_that_stored_its_clips_completes_even_if_cancelled_just_after()
    {
        using var cts = new CancellationTokenSource();

        await HeadStartRun.CompleteAsync(cts.Token, _ =>
        {
            cts.Cancel(); // a read started right after the clips were stored
            return Task.FromResult(true);
        });
    }

    [Fact]
    public async Task A_preparation_cancelled_before_it_started_ends_cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => HeadStartRun.CompleteAsync(cts.Token, _ => Task.FromResult(true)));
    }

    [Fact]
    public async Task A_skipped_preparation_ends_cancelled()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HeadStartRun.Skipped);
    }

    [Fact]
    public async Task A_cancelled_handler_is_not_a_prepared_head()
    {
        var voice = new EarlyStartVoice
        {
            Handler = (_, _, _) => Task.FromCanceled<bool>(new CancellationToken(canceled: true)),
        };

        await voice.VoiceAsync("One. Two.", "en", CancellationToken.None);

        Assert.False(voice.TakeEarly("One. Two. Three.", "en"));
    }

    [Fact]
    public async Task A_handler_that_prepared_makes_the_read_early()
    {
        var voice = new EarlyStartVoice { Handler = (_, _, _) => Task.FromResult(true) };

        await voice.VoiceAsync("One. Two.", "en", CancellationToken.None);

        Assert.True(voice.TakeEarly("One. Two. Three.", "en"));
    }
}
