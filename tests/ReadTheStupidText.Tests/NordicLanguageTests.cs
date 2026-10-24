using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Infrastructure.Translation;

namespace ReadTheStupidText.Tests;

public class NordicLanguageTests
{
    [Theory]
    [InlineData("Jeg har ikke noe å si, og hva skjer med henne etter at han kom, sa seg selv.", "nb")]
    [InlineData("Jeg har ikke noget at sige, og hvad sker der med hende efter han kom, sagde hun.", "da")]
    [InlineData("Jag har inte något att säga, och vad händer med hennes vän från byn, sa han.", "sv")]
    public void ScandinavianSamplesAreToldApart(string text, string expected)
    {
        Assert.Equal(expected, ScriptLanguageDetector.Detect(text)?.Tag);
    }

    [Theory]
    [InlineData("no")]
    [InlineData("nn")]
    [InlineData("NO")]
    public void LegacyNorwegianTagsAreBokmalEverywhere(string tag)
    {
        Assert.True(new VoiceAvailability([tag], []).CanPhoneRead("nb"));
        Assert.True(new VoiceAvailability(["nb"], []).CanPhoneRead(tag));
        Assert.Equal(TextScript.Latin, ScriptDetector.ScriptOf(tag));
        Assert.Equal("no", EngineLanguageCodes.ToEngine(tag));
    }

    [Fact]
    public void NaturallyReadableMatchesTheShippedPacks()
    {
        string workflow = ReadPacksWorkflow();
        string supertonic = Regex.Match(workflow, @"SUPERTONIC_LANGUAGES:\s*'([^']+)'").Groups[1].Value;
        var shipped = supertonic.Split(',').Append("fa").Append("zh").Order().ToList();

        var readable = PackConstants.NaturallyReadable.Select(AppLanguages.Code).Order().ToList();

        Assert.Equal(shipped, readable);
    }

    private static string ReadPacksWorkflow()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".github", "workflows", "packs.yml")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "packs.yml not found above the test output; run from a full checkout");
        return File.ReadAllText(Path.Combine(dir.FullName, ".github", "workflows", "packs.yml"));
    }
}
