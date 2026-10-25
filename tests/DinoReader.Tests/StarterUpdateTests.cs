using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class StarterUpdateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-starter-update-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, 1, false)]
    [InlineData(null, 2, true)]
    [InlineData(1, 2, true)]
    [InlineData(2, 2, false)]
    [InlineData(3, 2, false)]
    public void NeedsUpdate_ComparesTheInstalledVersionWithTheBundledOne(int? installed, int bundled, bool expected)
    {
        Assert.Equal(expected, StarterUpdate.NeedsUpdate(installed, bundled));
    }

    private static CastMember Member(string id, string voice, bool edited = false) => new(id, id, Gender.Female, voice, edited);

    private static Story StoryWith(string id, params CastMember[] cast) =>
        new(id, "Title", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Page.")], cast, CoverStyle.Picture, 1, "narrator", CoverFile: "cover-x.jpg");

    [Fact]
    public void Refresh_TakesTheBundledText_AndKeepsTheGrownUpsChoices()
    {
        Story stored = StoryWith("old", Member("pig", "supertonic:F3", edited: true), Member("wolf", "supertonic:M2")) with
        {
            Created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CharacterMerges = new Dictionary<string, string> { ["big-wolf"] = "wolf" },
            PreferredMode = ReadingMode.Text,
            TranslateToMyLanguage = false,
            ShowOriginalText = true,
            AnimalSoundsOn = false,
            UseRecordings = true,
        };
        Story bundled = StoryWith("new", Member("wolf", "supertonic:M1"), Member("pig", "supertonic:F1")) with
        {
            Pages = [new StoryPage("Gentle page.")],
            SourceId = "pkg",
            TranslateToMyLanguage = true,
        };

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal("new", refreshed.Id);
        Assert.Equal("pkg", refreshed.SourceId);
        Assert.Equal("Gentle page.", refreshed.Pages[0].Text);
        Assert.Equal(stored.Created, refreshed.Created);
        Assert.Equal(stored.Cast, refreshed.Cast);
        Assert.Equal(stored.CharacterMerges, refreshed.CharacterMerges);
        Assert.Equal(ReadingMode.Text, refreshed.PreferredMode);
        Assert.False(refreshed.TranslateToMyLanguage);
        Assert.True(refreshed.ShowOriginalText);
        Assert.False(refreshed.AnimalSoundsOn);
        Assert.Null(refreshed.Recordings);
    }

    private static Story Paged(params string[] texts) =>
        StoryWith("s") with { Pages = texts.Select(t => new StoryPage(t)).ToArray() };

    [Fact]
    public void RecordingMoves_FollowTheSameTextToItsNewPage_AndDropChangedText()
    {
        Story stored = Paged("One.", "Two.", "Three.") with
        {
            Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 3), [1] = new("en", 4), [2] = new("en", 5) },
        };
        Story bundled = Paged("Intro.", "Three.", "Two reworded.", "One.");

        var moves = StarterUpdate.RecordingMoves(stored, bundled);

        Assert.Equal([new StarterUpdate.RecordingMove(0, 3), new StarterUpdate.RecordingMove(2, 1)], moves);
    }

    [Fact]
    public void RecordingMoves_GiveRepeatedTextOneRecordingEach()
    {
        Story stored = Paged("Same.", "Same.") with { Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 1), [1] = new("en", 2) } };

        var moves = StarterUpdate.RecordingMoves(stored, Paged("Same.", "Other.", "Same."));

        Assert.Equal([new StarterUpdate.RecordingMove(0, 0), new StarterUpdate.RecordingMove(1, 2)], moves);
    }

    [Fact]
    public void Refresh_KeepsRecordingsOfUnchangedPages_AtTheirNewIndex()
    {
        Story stored = Paged("One.", "Two.") with
        {
            Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 3), [1] = new("en", 9) },
            UseRecordings = true,
        };

        Story refreshed = StarterUpdate.Refresh(stored, Paged("Two.", "New.") with { Id = "new" });

        Assert.Equal(new PageRecording("en", 9), Assert.Single(refreshed.Recordings!).Value);
        Assert.True(refreshed.Recordings!.ContainsKey(0));
        Assert.True(refreshed.UseRecordings);
    }

    [Fact]
    public void Refresh_WhenNoRecordedPageSurvives_LeavesNoRecordings()
    {
        Story stored = Paged("One.") with { Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 3) }, UseRecordings = true };

        Story refreshed = StarterUpdate.Refresh(stored, Paged("Changed."));

        Assert.Null(refreshed.Recordings);
        Assert.False(refreshed.UseRecordings);
    }

    [Fact]
    public void RecordingMoves_PreferTheSamePageIndex_WhenTheTextIsIdentical()
    {
        Story stored = Paged("Refrain.", "Middle.", "Refrain.") with { Recordings = new Dictionary<int, PageRecording> { [2] = new("en", 7) } };

        var moves = StarterUpdate.RecordingMoves(stored, Paged("Refrain.", "Middle.", "Refrain."));

        Assert.Equal([new StarterUpdate.RecordingMove(2, 2)], moves);
    }

    [Fact]
    public void Refresh_KeepsOnlyTheRecordingsWhoseFilesCopied()
    {
        Story stored = Paged("One.", "Two.") with
        {
            Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 3), [1] = new("en", 9) },
            UseRecordings = true,
        };
        Story bundled = Paged("One.", "Two.") with { Id = "new" };

        Story refreshed = StarterUpdate.Refresh(stored, bundled, [new StarterUpdate.RecordingMove(1, 1)]);

        Assert.Equal([1], refreshed.Recordings!.Keys);
        Assert.Null(StarterUpdate.Refresh(stored, bundled, []).Recordings);
    }

    [Fact]
    public void Refresh_KeepsTheTitle_AndTheGrownUpsCoverPick()
    {
        Story stored = Paged("One.") with { Title = "My pigs", CoverStyle = CoverStyle.Colour, CoverColourIndex = 5, CoverFile = null };
        Story bundled = Paged("One.") with { Id = "new", CoverStyle = CoverStyle.Colour, CoverColourIndex = 3 };

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal("My pigs", refreshed.Title);
        Assert.Equal(5, refreshed.CoverColourIndex);
    }

    [Fact]
    public void Refresh_KeepsAGalleryPhotoCover_OnlyWhenTheBundledCoverIsNotAPhoto()
    {
        Story stored = Paged("One.") with { CoverStyle = CoverStyle.Picture, CoverFile = "cover-abc.jpg" };
        Story plain = Paged("One.") with { Id = "new", CoverStyle = CoverStyle.Colour, CoverFile = null };
        Story photo = Paged("One.") with { Id = "new", CoverStyle = CoverStyle.Picture, CoverFile = "cover-new.jpg" };

        Assert.True(StarterUpdate.KeepsStoredPhoto(stored, plain));
        Assert.Equal("cover-abc.jpg", StarterUpdate.Refresh(stored, plain).CoverFile);
        Assert.False(StarterUpdate.KeepsStoredPhoto(stored, photo));
        Assert.Equal("cover-new.jpg", StarterUpdate.Refresh(stored, photo).CoverFile);
    }

    [Fact]
    public void IsEdited_IsTrueOnlyWhenTheTextMatchesNeitherTheBundledNorAnEarlierVersion()
    {
        const string pigs = "966400017c494fb5b045786f53852c9e";
        Story bundled = Paged("Gentle.");
        Story edited = Paged("My own words.");

        Assert.False(StarterUpdate.IsEdited(pigs, Paged("Gentle."), bundled));
        Assert.True(StarterUpdate.IsEdited(pigs, edited, bundled));
        Assert.False(StarterUpdate.IsEdited("unknown", Paged("Gentle."), bundled));
    }

    [Fact]
    public void AnUneditedV2CopyWithTheSameText_IsRefreshedToV3_KeepingEveryRecording()
    {
        const string pigs = "966400017c494fb5b045786f53852c9e";
        Story stored = Paged("One.", "Two.") with { CoverStyle = CoverStyle.Colour, CoverFile = null, Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 1), [1] = new("en", 2) }, UseRecordings = true };
        Story bundled = Paged("One.", "Two.") with
        {
            Id = "new",
            Pages = [new StoryPage("One.", "page-1.jpg"), new StoryPage("Two.", "page-2.jpg")],
            CoverStyle = CoverStyle.Picture,
            CoverFile = "cover-new.jpg",
        };

        Assert.True(StarterUpdate.NeedsUpdate(2, 3));
        Assert.False(StarterUpdate.IsEdited(pigs, stored, bundled));
        Story refreshed = StarterUpdate.Refresh(stored, bundled);
        Assert.Equal(2, refreshed.Recordings!.Count);
        Assert.True(refreshed.UseRecordings);
        Assert.Equal(["page-1.jpg", "page-2.jpg"], refreshed.Pages.Select(p => p.ImageFile));
        Assert.Equal(CoverStyle.Picture, refreshed.CoverStyle);
        Assert.Equal("cover-new.jpg", refreshed.CoverFile);
        Assert.True(StarterUpdate.IsEdited(pigs, Paged("My own words."), bundled));
    }

    private static Story PigsCopy(CoverStyle style, int colour, string? coverFile) =>
        Paged("One.") with { SourceId = StarterTexts.PigsId, CoverStyle = style, CoverColourIndex = colour, CoverFile = coverFile };

    private static Story PigsPictureBundle() =>
        Paged("One.") with { Id = "new", Pages = [new StoryPage("One.", "page-1.jpg")], CoverStyle = CoverStyle.Picture, CoverFile = "cover-new.jpg" };

    [Fact]
    public void Refresh_ReplacesAPigsCopyStillOnItsShippedColourCover_WithThePictureCover()
    {
        Story refreshed = StarterUpdate.Refresh(PigsCopy(CoverStyle.Colour, 3, null), PigsPictureBundle());

        Assert.Equal(CoverStyle.Picture, refreshed.CoverStyle);
        Assert.Equal("cover-new.jpg", refreshed.CoverFile);
    }

    [Fact]
    public void Refresh_KeepsAPigsCopyWhoseColourTheUserChanged()
    {
        Story refreshed = StarterUpdate.Refresh(PigsCopy(CoverStyle.Colour, 5, null), PigsPictureBundle());

        Assert.Equal(CoverStyle.Colour, refreshed.CoverStyle);
        Assert.Equal(5, refreshed.CoverColourIndex);
    }

    [Fact]
    public void Refresh_KeepsAPigsCopyWithAGalleryPhotoCover()
    {
        Story stored = PigsCopy(CoverStyle.Picture, 3, "cover-mine.jpg") with { CoverPageIndex = null };

        Story refreshed = StarterUpdate.Refresh(stored, PigsPictureBundle());

        Assert.Equal(CoverStyle.Picture, refreshed.CoverStyle);
        Assert.Equal("cover-mine.jpg", refreshed.CoverFile);
    }

    [Fact]
    public void Refresh_DropsAPageCoverPick_WhenThePageCountChanged()
    {
        Story stored = PigsCopy(CoverStyle.Picture, 3, null) with { Pages = [new StoryPage("A.", "a.jpg"), new StoryPage("B.", "b.jpg")], CoverPageIndex = 1 };
        Story bundled = PigsPictureBundle() with { Pages = [new StoryPage("A.", "x.jpg"), new StoryPage("B.", "y.jpg"), new StoryPage("C.", "z.jpg")] };

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal("cover-new.jpg", refreshed.CoverFile);
        Assert.Null(refreshed.CoverPageIndex);
    }

    [Fact]
    public void Refresh_KeepsAPageCoverPick_WhenThePageCountIsTheSame()
    {
        Story stored = PigsCopy(CoverStyle.Picture, 3, null) with { Pages = [new StoryPage("A.", "a.jpg"), new StoryPage("B.", "b.jpg")], CoverPageIndex = 1 };
        Story bundled = PigsPictureBundle() with { Pages = [new StoryPage("A.", "x.jpg"), new StoryPage("B.", "y.jpg")] };

        Assert.Equal(1, StarterUpdate.Refresh(stored, bundled).CoverPageIndex);
    }

    [Fact]
    public void EarlierPageTextHashes_HoldTheShippedV3PigsText()
    {
        var field = typeof(StarterUpdate).GetField("EarlierPageTextHashes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var table = (IReadOnlyDictionary<string, string[]>)field.GetValue(null)!;

        Assert.Contains("8C1DD87C17BFC3A1386D16CEF384F13FD3C04DEDA8FD2A7D514FC90130F1CC3D", table[StarterTexts.PigsId]);
    }

    [Fact]
    public void CopyToKeep_PicksTheCopyHoldingTheBundledText()
    {
        Story old = Paged("Old.") with { Id = "old" };
        Story current = Paged("Gentle.") with { Id = "current" };

        Assert.Equal("current", StarterUpdate.CopyToKeep([old, current], Paged("Gentle.")).Id);
        Assert.Equal("old", StarterUpdate.CopyToKeep([old, Paged("Other.") with { Id = "x" }], Paged("Gentle.")).Id);
    }

    [Fact]
    public void WithMovedRecordings_AddsOnlyToPagesWithNone()
    {
        Story target = Paged("One.", "Two.") with { Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 1) } };
        Story source = Paged("One.", "Two.") with { Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 8), [1] = new("en", 9) } };

        Story merged = StarterUpdate.WithMovedRecordings(target, source, [new StarterUpdate.RecordingMove(0, 0), new StarterUpdate.RecordingMove(1, 1)]);

        Assert.Equal(new PageRecording("en", 1), merged.Recordings![0]);
        Assert.Equal(new PageRecording("en", 9), merged.Recordings[1]);
    }

    [Fact]
    public void AnExportedPackage_CarriesNoVersion_WhenNoneIsSet()
    {
        Assert.DoesNotContain("\"version\"", new StoryPackageManifest(1, "0123456789abcdef0123456789abcdef", "T", "en").ToJson(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Refresh_WhenTheCharactersChanged_UsesTheBundledCast()
    {
        Story stored = StoryWith("old", Member("pig", "supertonic:F3", edited: true));
        Story bundled = StoryWith("new", Member("pig", "supertonic:F1"), Member("fox", "supertonic:M1"));

        Story refreshed = StarterUpdate.Refresh(stored, bundled);

        Assert.Equal(bundled.Cast, refreshed.Cast);
    }

    [Fact]
    public async Task ThePackageManifest_CarriesTheContentVersion()
    {
        var library = new FileStoryLibrary(_root);
        Story story = StoryWith(StoryIds.New(), Member("pig", "supertonic:F1")) with { CoverStyle = CoverStyle.Colour, CoverFile = null };
        await library.AddAsync(story);
        await using var package = new MemoryStream();

        await StoryPackageWriter.WriteAsync(story, library, package, version: 2);

        package.Position = 0;
        using var zip = new System.IO.Compression.ZipArchive(package);
        using var reader = new StreamReader(zip.GetEntry(StoryPackageManifest.ManifestFileName)!.Open());
        Assert.Equal(2, StoryPackageManifest.Parse(await reader.ReadToEndAsync()).Version);
        Assert.Null(StoryPackageManifest.Parse("{\"schemaVersion\":1,\"id\":\"0123456789abcdef0123456789abcdef\",\"title\":\"T\",\"language\":\"en\"}").Version);
    }

    [Fact]
    public void EveryBundledStarter_HasAVersion_AndTheGentleOnesAreAtLeastTwo()
    {
        Assert.Equal(StarterBuilder.AllFileNames.Order(), StarterBuilder.StarterVersions.Keys.Order());
        foreach (var (_, _, _, _, fileName) in StarterBuilder.Starters)
        {
            Assert.True(StarterBuilder.StarterVersions[fileName] >= 2, fileName);
        }
    }

    [Fact]
    public async Task TheShippedStarters_CarryTheirVersionInTheManifest()
    {
        foreach (string fileName in StarterBuilder.AllFileNames)
        {
            await using FileStream file = File.OpenRead(Path.Combine(StarterBuilder.StartersFolder(), fileName));
            using var zip = new System.IO.Compression.ZipArchive(file);
            using var reader = new StreamReader(zip.GetEntry(StoryPackageManifest.ManifestFileName)!.Open());

            Assert.Equal(StarterBuilder.StarterVersions[fileName], StoryPackageManifest.Parse(await reader.ReadToEndAsync()).Version);
        }
    }
}
