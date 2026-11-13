using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Tests;

public class ScriptDetectorTests
{
    [Fact]
    public void Analyze_NutkinLine_IsAllLatin()
    {
        ScriptShare share = ScriptDetector.Analyze(["Squirrel Nutkin loves exploring", "with his friend, Peter Rabbit."]);

        Assert.Equal(0, share.Count(TextScript.Arabic));
        Assert.True(share.Latin > 40);
        Assert.True(ScriptDetector.IsMostlyLatin(share));
        Assert.Equal(TextScript.Latin, share.Dominant);
    }

    [Fact]
    public void Analyze_FarsiLine_IsArabicScriptAndNotLatin()
    {
        ScriptShare share = ScriptDetector.Analyze(["سنجاب ناتکین عاشق گشتن است"]);

        Assert.Equal(0, share.Latin);
        Assert.False(ScriptDetector.IsMostlyLatin(share));
        Assert.Equal(TextScript.Arabic, share.Dominant);
    }

    [Fact]
    public void Analyze_DigitsAndPunctuation_AreNotCounted()
    {
        ScriptShare share = ScriptDetector.Analyze(["Page 12 of 340 -- ۱۲۳"]);

        Assert.Equal(6, share.Latin);
        Assert.Equal(0, share.Count(TextScript.Arabic));
    }

    [Fact]
    public void IsMostlyLatin_MixedLine_FollowsTheMajority()
    {
        Assert.True(ScriptDetector.IsMostlyLatin(ScriptDetector.Analyze(["The word کتاب means book in Persian"])));
        Assert.False(ScriptDetector.IsMostlyLatin(ScriptDetector.Analyze(["این کتاب است book"])));
    }

    [Fact]
    public void IsMostlyLatin_NoLetters_IsFalse()
    {
        ScriptShare share = ScriptDetector.Analyze(["123 456", ""]);

        Assert.Equal(0, share.Total);
        Assert.False(ScriptDetector.IsMostlyLatin(share));
        Assert.Null(share.Dominant);
    }

    [Theory]
    [InlineData("Привет мир", TextScript.Cyrillic)]
    [InlineData("日本語のテキスト", TextScript.Cjk)]
    [InlineData("안녕하세요 세계", TextScript.Hangul)]
    [InlineData("שלום עולם", TextScript.Hebrew)]
    [InlineData("नमस्ते दुनिया", TextScript.Devanagari)]
    [InlineData("Γειά σου Κόσμε", TextScript.Greek)]
    [InlineData("Café déjà vu", TextScript.Latin)]
    [InlineData("Blåbærsyltetøy på æblekaken, så är det både gott och rött", TextScript.Latin)]
    public void Dominant_NamesTheScript(string text, TextScript expected)
    {
        Assert.Equal(expected, ScriptDetector.Analyze([text]).Dominant);
    }

    [Theory]
    [InlineData("fa", TextScript.Arabic)]
    [InlineData("ar", TextScript.Arabic)]
    [InlineData("ru", TextScript.Cyrillic)]
    [InlineData("zh", TextScript.Cjk)]
    [InlineData("ja", TextScript.Cjk)]
    [InlineData("en", TextScript.Latin)]
    [InlineData("pt", TextScript.Latin)]
    [InlineData("da", TextScript.Latin)]
    [InlineData("fi", TextScript.Latin)]
    [InlineData("sv", TextScript.Latin)]
    [InlineData("nb", TextScript.Latin)]
    [InlineData("nl", TextScript.Latin)]
    [InlineData("de", TextScript.Latin)]
    [InlineData("ko", TextScript.Hangul)]
    [InlineData("he", TextScript.Hebrew)]
    [InlineData("hi", TextScript.Devanagari)]
    [InlineData("el", TextScript.Greek)]
    [InlineData("uk", TextScript.Cyrillic)]
    [InlineData("xx", TextScript.Unknown)]
    public void ScriptOf_MapsLanguages(string language, TextScript expected)
    {
        Assert.Equal(expected, ScriptDetector.ScriptOf(language));
    }
}
