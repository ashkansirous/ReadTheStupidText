using DinoReader.Core;

namespace DinoReader.Tests;

public class AspectFitTests
{
    [Fact]
    public void A_taller_view_letterboxes_top_and_bottom()
    {
        var r = AspectFit.ContentRect(200, 400, 200, 100);
        Assert.Equal((0d, 150d, 200d, 100d), r);
    }

    [Fact]
    public void A_wider_view_letterboxes_the_sides()
    {
        var r = AspectFit.ContentRect(400, 100, 100, 100);
        Assert.Equal((150d, 0d, 100d, 100d), r);
    }

    [Fact]
    public void A_matching_view_is_filled()
    {
        Assert.Equal((0d, 0d, 300d, 200d), AspectFit.ContentRect(300, 200, 600, 400));
    }

    [Fact]
    public void An_empty_size_gives_the_whole_view()
    {
        Assert.Equal((0d, 0d, 50d, 60d), AspectFit.ContentRect(50, 60, 0, 10));
    }
}
