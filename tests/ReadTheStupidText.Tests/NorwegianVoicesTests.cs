using System.Text.RegularExpressions;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

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
    public void WorkflowVoicesAreTenKnownSpeakersEachWithItsOwnSid()
    {
        string workflow = File.ReadAllText(FindWorkflow());
        var voices = Regex.Matches(workflow, @"""id"": ""(piper:nb-[a-z]+)"", ""name"": ""[^""]+"", ""sid"": (\d+)");

        Assert.Equal(10, voices.Count);
        Assert.Equal(Enumerable.Range(0, 10), voices.Select(m => int.Parse(m.Groups[2].Value)).Order());
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
    public void WorkflowVoiceIdsAreTheSameSetAsTheIds()
    {
        string workflow = File.ReadAllText(FindWorkflow());
        var ids = Regex.Matches(workflow, @"""id"": ""(piper:nb-[a-z]+)""").Select(m => m.Groups[1].Value);

        Assert.Equal(NorwegianVoices.Ids.Order(), ids.Order());
    }

    [Fact]
    public void DefaultIsKnvAndKeepsItsUpstreamSpeakerId()
    {
        Assert.Equal("piper:nb-knv", NorwegianVoices.DefaultId);
        Assert.Equal(1, NorwegianVoices.Position("piper:nb-knv"));
        Assert.Contains("{\"id\": \"piper:nb-knv\", \"name\": \"KNV\", \"sid\": 9", File.ReadAllText(FindWorkflow()));
    }

    [Fact]
    public void PackListedInUpstreamOrderStillDefaultsToKnvAndListsInCodeOrder()
    {
        string[] upstream = ["knn", "kmn", "kon", "ksv", "mmn", "mnn", "mnv", "mon", "msv", "knv"];
        var voices = upstream
            .Select((code, sid) => new VoiceInfo(NorwegianVoices.IdPrefix + code, code, "nb", VoiceEngineKind.Vits, sid))
            .ToList();
        var paths = new VoiceModelPaths("root", VoiceEngineKind.Vits, "nb", voices);

        Assert.Equal(NorwegianVoices.DefaultId, paths.ResolveVoice(null).Id);
        // StoryVoicing.NarratorFor takes pool[0] and the picker lists the pack, both built from this list.
        Assert.Equal(NorwegianVoices.DefaultId, paths.AvailableVoices[0].Id);
        Assert.Equal(NorwegianVoices.Ids.Order(), paths.AvailableVoices.Select(v => v.Id).Order());
        Assert.Equal(NorwegianVoices.Ids, paths.AvailableVoices.Select(v => v.Id));
    }

    [Fact]
    public void AStoredPickThatIsStillInstalledIsKeptAndOnlyAMissingOneFallsBack()
    {
        var voices = new[] { "knn", "knv" }
            .Select((code, sid) => new VoiceInfo(NorwegianVoices.IdPrefix + code, code, "nb", VoiceEngineKind.Vits, sid))
            .ToList();
        var paths = new VoiceModelPaths("root", VoiceEngineKind.Vits, "nb", voices);

        Assert.Equal("piper:nb-knn", paths.ResolveVoice("piper:nb-knn").Id);
        Assert.Equal(NorwegianVoices.DefaultId, paths.ResolveVoice("piper:nb-mmn").Id);
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
