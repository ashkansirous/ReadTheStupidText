using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class TextDirectionTests
{
    [Theory]
    [InlineData("سلام دنیا", true)]
    [InlineData("مرحبا", true)]
    [InlineData("12. سلام", true)]
    [InlineData("\"שלום\"", true)]
    [InlineData("Hello سلام", false)]
    [InlineData("1) Hello", false)]
    [InlineData("日本語", false)]
    [InlineData("123 !?", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRightToLeft_follows_the_first_strong_character(string? text, bool expected) =>
        Assert.Equal(expected, TextDirection.IsRightToLeft(text));

    [Theory]
    [InlineData('֏', false)] // just below Hebrew (Armenian dram sign)
    [InlineData('֐', false)] // Hebrew block start, unassigned: not a letter
    [InlineData('א', true)] // Hebrew alef
    [InlineData('ؠ', true)] // Arabic
    [InlineData('ܐ', true)] // Syriac
    [InlineData('ހ', true)] // Thaana
    [InlineData('ߊ', true)] // NKo
    [InlineData('ࢠ', true)] // Arabic extended-A
    [InlineData('ࣿ', false)] // top of the range, not a letter
    [InlineData('ऀ', false)] // Devanagari, just above
    [InlineData('יִ', true)] // presentation forms A start (Hebrew yod with hiriq)
    [InlineData('﬜', false)]
    [InlineData('ﷻ', true)] // presentation forms A, Arabic ligature
    [InlineData('ﹰ', true)] // presentation forms B start
    [InlineData('ﻼ', true)] // presentation forms B end letter
    [InlineData('Ａ', false)] // fullwidth Latin
    public void IsRightToLeft_covers_the_range_boundaries(char letter, bool expected) =>
        Assert.Equal(expected, TextDirection.IsRightToLeft(letter.ToString()));

    [Theory]
    [InlineData("", true, true)]
    [InlineData(null, true, true)]
    [InlineData("", false, false)]
    [InlineData("123", true, false)] // letterless content is left-to-right, not the UI language
    [InlineData("Hello", true, false)]
    [InlineData("سلام", false, true)]
    public void Resolve_uses_the_ui_direction_only_for_empty_text(string? text, bool ui, bool expected) =>
        Assert.Equal(expected, TextDirection.Resolve(text, ui));
}
