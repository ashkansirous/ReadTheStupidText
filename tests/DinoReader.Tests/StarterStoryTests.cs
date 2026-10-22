using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class StarterStoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-starter-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task BuildStarters_WhenAskedByTheEnvironment_RewritesTheBundledFiles()
    {
        if (Environment.GetEnvironmentVariable(StarterBuilder.BuildSwitch) != "1")
        {
            return;
        }

        await StarterBuilder.WriteAllAsync();
    }

    [Fact]
    public void EveryStarter_HasSeveralCharactersWithTheirOwnVoices()
    {
        foreach (var (id, title, text, colour, _) in StarterBuilder.Starters)
        {
            Story story = StarterBuilder.Build(id, title, text, colour);

            Assert.True(story.Cast.Count >= 3, $"{title} has {story.Cast.Count} characters: {string.Join(", ", story.Cast.Select(c => c.Name))}");
            Assert.Equal(story.Cast.Count, story.Cast.Select(c => c.VoiceId).Distinct().Count());
            Assert.DoesNotContain(story.Cast, c => c.VoiceId == story.NarratorVoiceId);
        }
    }

    [Fact]
    public async Task BundledFiles_ImportWithTheirStoreId_AndTheShippedCast()
    {
        var library = new FileStoryLibrary(_root);
        foreach (var (id, title, text, colour, fileName) in StarterBuilder.Starters)
        {
            await using FileStream file = File.OpenRead(Path.Combine(StarterBuilder.StartersFolder(), fileName));

            Story imported = await StoryPackageReader.ImportAsync(file, library);

            Story expected = StarterBuilder.Build(id, title, text, colour);
            Assert.Equal(id, imported.SourceId);
            Assert.Equal(title, imported.Title);
            Assert.Equal(expected.Cast, imported.Cast);
            Assert.Equal(expected.Pages.Select(p => p.Text), imported.Pages.Select(p => p.Text));
        }

        Assert.Equal(StarterBuilder.Starters.Count, (await library.ListAsync()).Count);
    }
}
