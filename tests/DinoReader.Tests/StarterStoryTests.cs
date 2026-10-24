using ReadTheStupidText.Domain.Reading;
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
    public async Task DumpPieces_WhenAskedByTheEnvironment_WritesThePieceFiles()
    {
        if (Environment.GetEnvironmentVariable(StarterBuilder.DumpSwitch) != "1")
        {
            return;
        }

        await StarterBuilder.DumpPiecesAsync();
    }

    [Fact]
    public void EveryStarter_HasSeveralCharactersWithTheirOwnVoices()
    {
        foreach (var (id, title, text, colour, _) in StarterBuilder.Starters)
        {
            Story story = StarterBuilder.Build(id, title, text, colour);

            Assert.True(story.Cast.Count >= 3, $"{title} has {story.Cast.Count} characters: {string.Join(", ", story.Cast.Select(c => c.Name))}");
            Assert.Equal(story.Cast.Count, story.Cast.Select(c => c.VoiceId).Distinct().Count());
            Assert.DoesNotContain(story.Cast, c => c.VoiceId == SupertonicVoiceTable.Default.Id);
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

    private async Task<Story> ImportGoodEnoughAsync()
    {
        await using FileStream file = File.OpenRead(Path.Combine(StarterBuilder.StartersFolder(), StarterBuilder.GoodEnoughFile));
        return await StoryPackageReader.ImportAsync(file, new FileStoryLibrary(_root));
    }

    [Fact]
    public async Task GoodEnough_OpensWithTenPicturePagesAndTheCoverPhoto()
    {
        var library = new FileStoryLibrary(_root);
        await using FileStream file = File.OpenRead(Path.Combine(StarterBuilder.StartersFolder(), StarterBuilder.GoodEnoughFile));

        Story story = await StoryPackageReader.ImportAsync(file, library);

        Assert.Equal(StarterBuilder.GoodEnoughId, story.SourceId);
        Assert.Equal(10, story.Pages.Count);
        Assert.All(story.Pages, p => Assert.NotNull(library.TryResolveFile(story.Id, p.ImageFile)));
        Assert.All(story.Pages.Take(7), p => Assert.NotEmpty(p.Lines!));
        Assert.Equal(CoverStyle.Picture, story.CoverStyle);
        Assert.StartsWith(Story.CoverFilePrefix + "-", story.CoverFile);
        Assert.True(File.Exists(library.TryResolveFile(story.Id, story.CoverFile)));
        Assert.Equal(story.CoverFile, story.CoverImageFile);
    }

    [Fact]
    public async Task GoodEnough_PagesWithLinesAreTheirLinesJoined()
    {
        Story story = await ImportGoodEnoughAsync();

        Assert.All(story.Pages.Take(7), p => Assert.Equal(string.Join("\n", p.Lines!.Select(l => l.Text)), p.Text));
        Assert.All(story.Pages.Skip(7), p => Assert.Null(p.Lines));
    }

    // Only Hazelnut speaks ("says Hazelnut", page 10); Carrot has no quoted line, so the caster finds no Carrot character (reported in Slice 94).
    [Fact]
    public async Task GoodEnough_HazelnutIsACharacterWithAVoiceOfHerOwn()
    {
        Story story = await ImportGoodEnoughAsync();

        CastMember hazelnut = story.Cast.Single(c => c.Name == "Hazelnut");
        Assert.NotEqual(SupertonicVoiceTable.Default.Id, hazelnut.VoiceId);
    }

    [Fact]
    public async Task EveryStarter_HasAllTheOtherLanguagesTranslated_ForEveryPiece()
    {
        var library = new FileStoryLibrary(_root);
        foreach (string fileName in StarterBuilder.AllFileNames)
        {
            await using FileStream file = File.OpenRead(Path.Combine(StarterBuilder.StartersFolder(), fileName));
            Story story = await StoryPackageReader.ImportAsync(file, library);
            Assert.True(story.TranslateToMyLanguage, fileName);
            IReadOnlyList<string> pieces = StoryPieces.TranslatablePieceTexts(story);

            foreach (string language in UiLanguage.Supported.Where(l => l != UiLanguage.Resolve(story.Language)))
            {
                IEnumerable<string> missing = pieces.Where(p => string.IsNullOrWhiteSpace(BundledTranslations.Lookup(story, language, p)));
                Assert.True(!missing.Any(), $"{fileName} has no {language} translation for: {string.Join(" | ", missing.Take(3))}");
            }
        }
    }
}
