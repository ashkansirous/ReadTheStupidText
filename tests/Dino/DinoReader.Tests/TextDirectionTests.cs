using DinoReader.Core;

namespace DinoReader.Tests;

public class TextDirectionTests
{
    [Theory]
    [InlineData("خاله سوسکه", true)]
    [InlineData("القصص", true)]
    [InlineData("The Three Little Pigs", false)]
    [InlineData("Peter خاله", false)]
    [InlineData("12 خاله", true)]
    [InlineData("12 pigs", false)]
    [InlineData("١٢ pigs", false)]
    [InlineData("؟ خاله", true)]
    [InlineData("﻿The Pigs", false)]
    [InlineData("﻿خاله", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void StartsRightToLeft_FollowsTheFirstStrongLetter(string? text, bool expected) =>
        Assert.Equal(expected, TextDirection.StartsRightToLeft(text));
}
