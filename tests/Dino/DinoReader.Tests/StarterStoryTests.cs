using System.Text.Json;
using DinoReader.BookBuilder;
using Sirous.Reading.Domain.Reading;
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
    public async Task GoodEnough_OpensWithElevenPicturePagesAndTheCoverPhoto()
    {
        var library = new FileStoryLibrary(_root);
        await using FileStream file = File.OpenRead(Path.Combine(StarterBuilder.StartersFolder(), StarterBuilder.GoodEnoughFile));

        Story story = await StoryPackageReader.ImportAsync(file, library);

        Assert.Equal(StarterBuilder.GoodEnoughId, story.SourceId);
        Assert.Equal(11, story.Pages.Count);
        Assert.All(story.Pages, p => Assert.NotNull(library.TryResolveFile(story.Id, p.ImageFile)));
        Assert.All(story.Pages, p => Assert.Empty(p.Lines ?? []));
        Assert.Equal(CoverStyle.Picture, story.CoverStyle);
        Assert.StartsWith(Story.CoverFilePrefix + "-", story.CoverFile);
        Assert.True(File.Exists(library.TryResolveFile(story.Id, story.CoverFile)));
        Assert.Equal(story.CoverFile, story.CoverImageFile);
    }

    [Fact]
    public async Task GoodEnough_PagesCarryTheirTextFromLinesJson()
    {
        Story story = await ImportGoodEnoughAsync();
        string json = File.ReadAllText(Path.Combine(Books.ContentRoot(), StarterBuilder.GoodEnoughStem, "lines.json"));
        string[] expected = JsonDocument.Parse(json).RootElement.GetProperty("pages").EnumerateArray().Select(p => p.GetProperty("text").GetString()!).ToArray();

        Assert.Equal(expected, story.Pages.Select(p => p.Text));
    }

    // The cast has four members (Carrot, Hazelnut, Referee and Crowd, each with quoted lines); Hazelnut's own voice is checked here (Slice 156).
    [Fact]
    public async Task GoodEnough_HazelnutIsACharacterWithAVoiceOfHerOwn()
    {
        Story story = await ImportGoodEnoughAsync();

        CastMember hazelnut = story.Cast.Single(c => c.Name == "Hazelnut");
        Assert.NotEqual(SupertonicVoiceTable.Default.Id, hazelnut.VoiceId);
        Assert.Equal(Gender.Female, hazelnut.Gender);
        Assert.Equal("bee", hazelnut.SuggestedVoice);
        Assert.NotNull(hazelnut.Traits);
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

    [Fact]
    public async Task GermanUi_OpensEveryShippedStarterInGerman_WithItsNormalCast()
    {
        var library = new FileStoryLibrary(_root);
        foreach (string fileName in StarterBuilder.AllFileNames)
        {
            await using FileStream file = File.OpenRead(Path.Combine(StarterBuilder.StartersFolder(), fileName));
            Story story = await StoryPackageReader.ImportAsync(file, library);
            string text = StoryPieces.TranslatablePieceTexts(story).First(t => t.Any(char.IsLetter));
            string german = BundledTranslations.Lookup(story, "de", text)!;

            Assert.True(BundledTranslations.Has(story, "de"), fileName);
            Assert.True(BundledTranslations.Applies(story, "de", null), fileName);
            Assert.True(BundledTranslations.CanTranslate(story, "de", null), fileName);
            Assert.Equal(german, await BundledTranslations.Compose(story, "de", null)(text, CancellationToken.None));
            Assert.NotEmpty(story.Cast);
            Assert.All(story.Cast, c => Assert.False(string.IsNullOrWhiteSpace(c.VoiceId), fileName));
        }
    }
}
