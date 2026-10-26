using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class ReadWaitStagesTests
{
    [Theory]
    [InlineData(true, true, WaitStage.DownloadingModel)]
    [InlineData(true, false, WaitStage.DownloadingModel)]
    [InlineData(false, true, WaitStage.Translating)]
    [InlineData(false, false, WaitStage.PreparingVoice)]
    public void Pick_prefers_the_download_then_the_translation(bool downloading, bool translating, WaitStage expected) =>
        Assert.Equal(expected, ReadWaitStages.Pick(downloading, translating));

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.404, 40)]
    [InlineData(1.0, 100)]
    [InlineData(1.7, 100)]
    [InlineData(-0.2, 0)]
    [InlineData(double.NaN, 0)]
    public void Percent_is_clamped_and_rounded(double fraction, int expected) =>
        Assert.Equal(expected, ReadWaitStages.Percent(fraction));
}
