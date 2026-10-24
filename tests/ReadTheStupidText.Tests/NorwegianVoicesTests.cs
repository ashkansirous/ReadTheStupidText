using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class NorwegianVoicesTests
{
    [Theory]
    [InlineData("piper:nb-knn", true)]
    [InlineData("piper:nb-kmn", true)]
    [InlineData("piper:nb-mmn", false)]
    [InlineData("piper:nb-msv", false)]
    [InlineData("piper:fa-mana", null)]
    [InlineData("piper:nb-", null)]
    [InlineData("supertonic:F1", null)]
    public void GenderComesFromTheSpeakerCode(string id, bool? female)
    {
        Assert.Equal(female, NorwegianVoices.IsFemale(id));
    }

    [Fact]
    public void WorkflowVoicesAreTenKnownSpeakersWithConsecutiveSids()
    {
        string workflow = File.ReadAllText(FindWorkflow());
        var voices = Regex.Matches(workflow, @"""id"": ""(piper:nb-[a-z]+)"", ""name"": ""[^""]+"", ""sid"": (\d+)");

        Assert.Equal(10, voices.Count);
        Assert.Equal(Enumerable.Range(0, 10), voices.Select(m => int.Parse(m.Groups[2].Value)));
        Assert.All(voices, m => Assert.NotNull(NorwegianVoices.IsFemale(m.Groups[1].Value)));
    }

    [Fact]
    public void SupertonicServedLanguagesMatchThePacksWorkflow()
    {
        string workflow = File.ReadAllText(FindWorkflow());
        string declared = Regex.Match(workflow, @"SUPERTONIC_LANGUAGES:\s*'([^']+)'").Groups[1].Value;
        var served = declared.Split(',').ToHashSet();

        foreach (string code in Enum.GetValues<AppLanguage>().Select(AppLanguages.Code))
        {
            Assert.Equal(served.Contains(code), ReadTheStupidText.Domain.Reading.SupertonicVoiceTable.Serves(code));
        }
    }

    [Fact]
    public void PositionsAreOneToTenInPackOrderAndEveryVoiceHasAGender()
    {
        Assert.Equal(Enumerable.Range(1, 10), NorwegianVoices.Ids.Select(id => NorwegianVoices.Position(id)!.Value));
        Assert.All(NorwegianVoices.Ids, id => Assert.NotNull(NorwegianVoices.IsFemale(id)));
        Assert.Null(NorwegianVoices.Position("piper:fa-mana"));
        Assert.Equal(NorwegianVoices.Ids[0], NorwegianVoices.DefaultId);
    }

    [Fact]
    public void WorkflowVoiceIdsEqualTheIdsInPackOrder()
    {
        string workflow = File.ReadAllText(FindWorkflow());
        var ids = Regex.Matches(workflow, @"""id"": ""(piper:nb-[a-z]+)""").Select(m => m.Groups[1].Value);

        Assert.Equal(NorwegianVoices.Ids, ids);
    }

    [Fact]
    public void SupertonicDoesNotServeNorwegian()
    {
        Assert.False(ReadTheStupidText.Domain.Reading.SupertonicVoiceTable.Serves("nb"));
        Assert.True(ReadTheStupidText.Domain.Reading.SupertonicVoiceTable.Serves("sv"));
    }

    private static string FindWorkflow()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".github", "workflows", "packs.yml")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "packs.yml not found above the test output");
        return Path.Combine(dir!.FullName, ".github", "workflows", "packs.yml");
    }
}
