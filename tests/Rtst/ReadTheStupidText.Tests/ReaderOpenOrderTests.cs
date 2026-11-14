using ReadTheStupidText.Mobile;
using Xunit;

namespace ReadTheStupidText.Tests;

public class ReaderOpenOrderTests
{
    [Fact]
    public void Open_RunsInitWaitFirst_ThenShowsTextOnce()
    {
        var calls = new List<string>();

        ReaderOpenOrder.Open(() => calls.Add("initWait"), () => calls.Add("showText"));

        Assert.Equal(new[] { "initWait", "showText" }, calls);
    }
}
