using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class StoreStoryTests : IDisposable
{
    private const int MinPages = 8;
    private const int MaxPages = 15;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-store-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task BuildStore_WhenAskedByTheEnvironment_WritesTheStoreFolders()
    {
        if (Environment.GetEnvironmentVariable(StoreBuilder.BuildSwitch) != "1")
        {
            return;
        }

        await StoreBuilder.WriteAllAsync();
    }

    [Fact]
    public async Task DumpPieces_WhenAskedByTheEnvironment_WritesThePieceFilesOfTheStoreStories()
    {
        if (Environment.GetEnvironmentVariable(StarterBuilder.DumpSwitch) != "1")
        {
            return;
        }

        await StoreBuilder.DumpPiecesAsync();
    }

    [Fact]
    public void TheStore_HasTenStories_WithFixedUniqueIds()
    {
        Assert.Equal(10, StoreTexts.All.Count);
        Assert.Equal(10, StoreTexts.All.Select(s => s.Id).Distinct().Count());
        Assert.Equal(10, StoreTexts.All.Select(s => s.Stem).Distinct().Count());
        Assert.All(StoreTexts.All, s => Assert.True(StoryIds.IsValid(s.Id), s.Title));
        Assert.Empty(StoreTexts.All.Select(s => s.Id).Intersect(StarterBuilder.Starters.Select(s => s.Id).Append(StarterBuilder.GoodEnoughId)));
    }

    [Fact]
    public void EveryStoreStory_IsShortAndHasAMeaningfulCredit()
    {
        foreach (StoreStorySpec spec in StoreTexts.All)
        {
            Story story = StoreBuilder.Build(spec);

            Assert.InRange(story.Pages.Count, MinPages, MaxPages);
            Assert.False(string.IsNullOrWhiteSpace(spec.Credits), spec.Title);
            Assert.True(story.TranslateToMyLanguage, spec.Title);
        }
    }

    [Fact]
    public void EveryStoreStory_CastsItsNamedCharacters_EachWithAVoiceOfTheirOwn()
    {
        foreach (StoreStorySpec spec in StoreTexts.All)
        {
            Story story = StoreBuilder.Build(spec);
            IReadOnlyList<string> found = story.Cast.Select(c => c.Name).ToList();

            foreach (string expected in spec.Characters)
            {
                Assert.True(found.Contains(expected), $"{spec.Title}: no character {expected}; found {string.Join(", ", found)}");
            }

            Assert.Equal(story.Cast.Count, story.Cast.Select(c => c.VoiceId).Distinct().Count());
            Assert.DoesNotContain(story.Cast, c => c.VoiceId == story.NarratorVoiceId);
        }
    }

    [Fact]
    public void EveryStoreStory_HasAllTheOtherLanguagesTranslated_ForEveryPiece()
    {
        foreach (StoreStorySpec spec in StoreTexts.All)
        {
            Story story = StoreBuilder.Build(spec);
            IReadOnlyList<string> pieces = StoryPieces.TranslatablePieceTexts(story);

            foreach (string language in UiLanguage.Supported.Where(l => l != UiLanguage.Resolve(story.Language)))
            {
                IEnumerable<string> missing = pieces.Where(p => string.IsNullOrWhiteSpace(BundledTranslations.Lookup(story, language, p)));
                Assert.True(!missing.Any(), $"{spec.Stem} has no {language} translation for: {string.Join(" | ", missing.Take(3))}");
            }
        }
    }

    [Fact]
    public async Task APackedStoreStory_ImportsWithItsStoreId_AndItsTranslations()
    {
        StoreStorySpec spec = StoreTexts.All[0];
        var library = new FileStoryLibrary(_root);
        Story built = StoreBuilder.Build(spec);
        await library.AddAsync(built);
        using var package = new MemoryStream();
        await StoryPackageWriter.WriteAsync(built, library, package);
        package.Position = 0;

        Story imported = await StoryPackageReader.ImportAsync(package, new FileStoryLibrary(Path.Combine(_root, "other")));

        Assert.Equal(spec.Id, imported.SourceId);
        Assert.Equal(spec.Title, imported.Title);
        Assert.Equal(built.Cast, imported.Cast);
        Assert.True(imported.TranslateToMyLanguage);
        Assert.Equal(8, imported.Translations!.Count);
    }

    [Fact]
    public void TheMeta_HoldsWhatTheWorkerChecks_AndTheCredit()
    {
        StoreStorySpec spec = StoreTexts.All[0];
        Story story = StoreBuilder.Build(spec);

        using JsonDocument meta = JsonDocument.Parse(StoreBuilder.MetaJson(spec, story));

        JsonElement root = meta.RootElement;
        Assert.Equal(StoryPackageManifest.CurrentSchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(spec.Title, root.GetProperty("title").GetString());
        Assert.Equal("en", root.GetProperty("language").GetString());
        Assert.Equal(spec.Version, root.GetProperty("version").GetInt32());
        Assert.Equal(story.Pages.Count, root.GetProperty("pageCount").GetInt32());
        Assert.True(root.GetProperty("minutes").GetInt32() >= 1);
        Assert.Equal(spec.Added, root.GetProperty("added").GetString());
        Assert.Equal(spec.Credits, root.GetProperty("credits").GetString());
    }

    [Theory]
    [InlineData("A", 1)]
    [InlineData("The Town Mouse and the Country Mouse", 5)]
    [InlineData("Goldilocks and the Three Bears", 4)]
    public void TheCoverTitle_WrapsOnWholeWords_AtTenLettersAtMost(string title, int lines)
    {
        IReadOnlyList<string> wrapped = StoreCover.Wrap(title);

        Assert.Equal(lines, wrapped.Count);
        Assert.All(wrapped, l => Assert.True(l.Length <= 10, l));
    }

    [Fact]
    public void EveryStoreTitle_FitsTheCoverFont()
    {
        Assert.All(StoreTexts.All, s => Assert.All(StoreCover.Wrap(s.Title), l => Assert.True(l.Length <= 10 && l.All(c => c is ' ' or (>= 'A' and <= 'Z')), l)));
    }

    [Fact]
    public void TheCover_IsAJpegOfTheListSize_WithItsSegmentsInPlace()
    {
        byte[] jpeg = StoreCover.Render("The Lion and the Mouse", CoverPalette.ColourAt(1));

        Assert.Equal([0xFF, 0xD8], jpeg[..2]);
        Assert.Equal([0xFF, 0xD9], jpeg[^2..]);
        Dictionary<byte, int> segments = JpegSegments(jpeg);
        foreach (byte marker in new byte[] { 0xDB, 0xC0, 0xC4, 0xDA })
        {
            Assert.True(segments.ContainsKey(marker), $"missing marker 0xFF{marker:X2}");
        }

        int frame = segments[0xC0];
        Assert.Equal(StoreCover.Height, (jpeg[frame + 5] << 8) | jpeg[frame + 6]);
        Assert.Equal(StoreCover.Width, (jpeg[frame + 7] << 8) | jpeg[frame + 8]);
    }

    /// <summary>The first offset of each marker segment before the scan data (<c>FF xx</c> then a two-byte length), keyed by marker.</summary>
    private static Dictionary<byte, int> JpegSegments(byte[] jpeg)
    {
        var found = new Dictionary<byte, int>();
        int at = 2;
        while (at + 4 <= jpeg.Length && jpeg[at] == 0xFF)
        {
            byte marker = jpeg[at + 1];
            found.TryAdd(marker, at);
            if (marker == 0xDA)
            {
                break;
            }

            at += 2 + ((jpeg[at + 2] << 8) | jpeg[at + 3]);
        }

        return found;
    }
}
