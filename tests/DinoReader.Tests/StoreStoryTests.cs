using ReadTheStupidText.Domain.Reading;
using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class StoreStoryTests : IDisposable
{
    private const int MinPages = 8;
    private const int MaxPages = 18; // the user's limit for a store book (2026-10-26, X Marks the Treasure!)
    private const int StoryCount = 11;

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
    public void TheStore_HasEveryStory_WithFixedUniqueIds()
    {
        Assert.Equal(StoryCount, StoreTexts.All.Count);
        Assert.Equal(StoryCount, StoreTexts.All.Select(s => s.Id).Distinct().Count());
        Assert.Equal(StoryCount, StoreTexts.All.Select(s => s.Stem).Distinct().Count());
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
            Assert.InRange(spec.AgeMin, 0, spec.AgeMax);
            Assert.InRange(spec.AgeMax, spec.AgeMin, 18);
            Assert.False(string.IsNullOrWhiteSpace(spec.Author), spec.Title);
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
            Assert.DoesNotContain(story.Cast, c => c.VoiceId == SupertonicVoiceTable.Default.Id);
        }
    }

    public static TheoryData<string> Languages() => new(UiLanguage.Supported);

    // The roster's duckling voices are one gender per language and an animal takes its own animal's voice before its gender, so the builder stores Duckling and Dilly
    // as no animal (StarterAnimals). The app casts a language other than the story's afresh, carrying only the animal settings; the story's language keeps its cast.
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheUglyDuckling_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        AssertEachIsPlayedByTheirGender(StoreBuilder.Build(StoreTexts.All.Single(s => s.Id == StoreTexts.UglyDucklingId)), language, new Dictionary<string, Gender>
        {
            ["Mother Duck"] = Gender.Female, ["Dilly"] = Gender.Female, ["Hen"] = Gender.Female, ["Duckling"] = Gender.Male, ["Swan"] = Gender.Male,
        });
    }

    // Every roster's bear voices (bear, big bear, cub) are men's, so the builder stores Mama Bear as no animal (StarterAnimals): she is cast by gender, a woman's voice.
    // Papa Bear and Baby Bear keep their bear voices and growl; Baby Bear is a boy cub ("he forgot to shut the door").
    // Mum has no line, so the cast is exactly these four (the helper fails on any other character).
    [Theory]
    [MemberData(nameof(Languages))]
    public void Goldilocks_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        AssertEachIsPlayedByTheirGender(StoreBuilder.Build(StoreTexts.All.Single(s => s.Id == StoreTexts.GoldilocksId)), language, new Dictionary<string, Gender>
        {
            ["Goldilocks"] = Gender.Female, ["Mama Bear"] = Gender.Female, ["Papa Bear"] = Gender.Male, ["Baby Bear"] = Gender.Male,
        });
    }

    // The Little Red Hen (Slice 121): the roster's kitten and duckling voices are girls in most languages but boys in Farsi, Spanish and Portuguese, so the builder
    // stores Cat and Duck as no animal (StarterAnimals) and they are cast by gender; Hen keeps the hen and Dog the puppy, which are one gender everywhere.
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheLittleRedHen_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        var expected = new Dictionary<string, Gender>
        {
            ["Hen"] = Gender.Female, ["Dog"] = Gender.Male, ["Cat"] = Gender.Female, ["Duck"] = Gender.Male,
        };

        Story story = StoreBuilder.Build(StoreTexts.All.Single(s => s.Id == StoreTexts.RedHenId));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // The Tale of Peter Rabbit (Slice 121): Mrs. Rabbit and Flopsy are "she", Peter, Mr. McGregor and Robin "he". The roster's bunny voice is a girl in most
    // languages, so the builder stores Peter as no animal (StarterAnimals) and he is cast as a boy; Mrs. Rabbit is grown, which no bunny voice plays.
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheTaleOfPeterRabbit_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        var expected = new Dictionary<string, Gender>
        {
            ["Mrs. Rabbit"] = Gender.Female, ["Flopsy"] = Gender.Female, ["Peter"] = Gender.Male, ["Mr. McGregor"] = Gender.Male, ["Robin"] = Gender.Male,
        };

        Story story = StoreBuilder.Build(StoreTexts.All.Single(s => s.Id == StoreTexts.PeterRabbitId));
        Assert.Equal(expected.Keys.Order(), story.Cast.Select(m => m.Name).Order());
        Assert.All(story.Cast, m => Assert.True(m.Gender == expected[m.Name], $"{m.Name} is stored as {m.Gender}"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // X Marks the Treasure! (Slice 121): Kaatrin, Izzy and Pancake Polly are girls, the Seagull is a "she" with no animal word the guesser knows, Billy is a boy.
    [Theory]
    [MemberData(nameof(Languages))]
    public void XMarksTheTreasure_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        var expected = new Dictionary<string, Gender>
        {
            ["Kaatrin"] = Gender.Female, ["Izzy"] = Gender.Female, ["Pancake Polly"] = Gender.Female, ["Seagull"] = Gender.Female, ["Billy"] = Gender.Male,
        };

        Story story = StoreBuilder.Build(StoreTexts.All.Single(s => s.Id == StoreTexts.TreasureId));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // The Gingerbread Man starter (also a store story) is a boy, so he must get a male voice in every language; Granny, the Cow and the Horse are "she",
    // Grandpa and the Fox "he" (the stored English genders are checked too, since every other language is cast from them).
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheGingerbreadMan_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        Story story = StarterBuilder.Build(StarterTexts.GingerbreadId, "The Gingerbread Man", StarterTexts.Gingerbread, 1);
        var expected = new Dictionary<string, Gender>
        {
            ["Granny"] = Gender.Female, ["Grandpa"] = Gender.Male, ["Gingerbread Man"] = Gender.Male, ["Cow"] = Gender.Female, ["Horse"] = Gender.Female, ["Fox"] = Gender.Male,
        };

        Assert.Equal(expected.Keys.Order(), story.Cast.Select(m => m.Name).Order());
        Assert.All(story.Cast, m => Assert.True(m.Gender == expected[m.Name], $"{m.Name} is stored as {m.Gender}"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // The Tortoise and the Hare starter (also a store story): the Hare, Owl and Hedgehog are "he", the Tortoise, Squirrel and Frog "she", three of each, and each
    // must get a voice of that gender in every language (the stored English genders are checked too, since every other language is cast from them).
    [Theory]
    [MemberData(nameof(Languages))]
    public void TheTortoiseAndTheHare_GivesEachCharacterAVoiceOfTheirGender_InEveryLanguagesRoster(string language)
    {
        Story story = StarterBuilder.Build(StarterTexts.TortoiseId, "The Tortoise and the Hare", StarterTexts.Tortoise, 0);
        var expected = new Dictionary<string, Gender>
        {
            ["Hare"] = Gender.Male, ["Tortoise"] = Gender.Female, ["Owl"] = Gender.Male, ["Squirrel"] = Gender.Female, ["Hedgehog"] = Gender.Male, ["Frog"] = Gender.Female,
        };

        Assert.Equal(expected.Keys.Order(), story.Cast.Select(m => m.Name).Order());
        Assert.All(story.Cast, m => Assert.True(m.Gender == expected[m.Name], $"{m.Name} is stored as {m.Gender}"));
        AssertEachIsPlayedByTheirGender(story, language, expected);
    }

    // Casts the story the way the app does in that language (a language other than the story's is cast afresh, carrying only the animal settings; the story's
    // language keeps its cast) and checks that each character's roster character has the expected gender.
    private static void AssertEachIsPlayedByTheirGender(Story story, string language, IReadOnlyDictionary<string, Gender> expected)
    {
        IReadOnlyList<CastMember> stored = language == story.Language
            ? story.Cast
            : story.Cast.Where(m => m.HasAnimalSetting).Select(m => m with { VoiceId = string.Empty }).ToList();
        List<Character> characters = story.Cast.Select(m => new Character(m.CharacterId, m.Name, m.Gender, 1)).ToList();
        IReadOnlyDictionary<string, AnimalKind> animals = AnimalSounds.Resolve(characters, story.Language, stored, story.Text);
        IReadOnlyList<StorytellerCharacter> speakers = CharacterRoster.Speakers(language);
        string narrator = CharacterRoster.Narrators(language)[0].VoiceId;

        VoiceCast cast = CastCharacters.Cast(
            characters, CharacterRoster.Choices(speakers, narrator), CharacterRoster.For(language), narrator, c => FlavourGuess.Guess(c.Name, LanguageRules.English),
            StoryCast.PinnedCharacters(stored), animalOf: c => animals.TryGetValue(c.Id, out AnimalKind kind) ? AnimalCharacters.HintFor(kind, c.Name, story.Language) : null);

        foreach (Character character in characters)
        {
            Assert.True(expected.ContainsKey(character.Name), $"{language}: the cast has {character.Name}, which this test has no expected gender for");
            StorytellerCharacter? played = CharacterRoster.Nearest(speakers, cast.VoiceFor(character.Id)!, cast.Flavours![character.Id], character.Gender);
            Assert.True(played?.Gender == expected[character.Name], $"{language}: {character.Name} is played by {played?.Name} ({played?.Gender})");
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
        Assert.Equal(UiLanguage.Supported.Count - 1, imported.Translations!.Count);
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
        Assert.Equal(spec.AgeMin, root.GetProperty("ageMin").GetInt32());
        Assert.Equal(spec.AgeMax, root.GetProperty("ageMax").GetInt32());
        Assert.Equal(spec.Author, root.GetProperty("author").GetString());
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
        Assert.All(StoreTexts.All, s => Assert.All(StoreCover.Wrap(s.Title), l => Assert.True(l.Length <= 10 && l.All(c => c is ' ' or '!' or (>= 'A' and <= 'Z')), l)));
    }

    [Fact]
    public void TheCover_RendersATitleWithAnExclamationMark()
    {
        IReadOnlyList<string> wrapped = StoreCover.Wrap("X Marks the Treasure!");
        byte[] jpeg = StoreCover.Render("X Marks the Treasure!", CoverPalette.ColourAt(0));

        Assert.Contains(wrapped, l => l.EndsWith('!'));
        Assert.Equal([0xFF, 0xD8], jpeg[..2]);
        Assert.Equal([0xFF, 0xD9], jpeg[^2..]);
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

    [Fact]
    public void EveryBundledStarter_HasAStoreEntry_WithTheSameIdAsTheBundledFile()
    {
        Assert.Equal(StarterBuilder.AllFileNames.Order(), StoreBuilder.StarterEntries.Keys.Order());
        var ids = StarterBuilder.Starters.ToDictionary(s => s.FileName, s => s.Id);
        ids[StarterBuilder.GoodEnoughFile] = StarterBuilder.GoodEnoughId;
        foreach (var (fileName, entry) in StoreBuilder.StarterEntries)
        {
            Assert.Equal(ids[fileName], entry.Id);
            Assert.InRange(entry.AgeMin, 0, entry.AgeMax);
            Assert.InRange(entry.AgeMax, entry.AgeMin, 18);
            Assert.False(string.IsNullOrWhiteSpace(entry.Credits) || string.IsNullOrWhiteSpace(entry.Author), entry.Title);
        }
    }

    [Fact]
    public async Task TheStarterStoreBuild_KeepsTheBundledIds_AndWritesMetaTheWorkerChecks()
    {
        var library = new FileStoryLibrary(_root);

        var built = await StarterBuilder.BuildAllAsync(library);

        Assert.Equal(StarterBuilder.AllFileNames.Count, built.Count);
        foreach (var (fileName, story) in built)
        {
            StoreStorySpec spec = StoreBuilder.StarterEntries[fileName].ToSpec(story, StarterBuilder.StarterVersions[fileName]);
            Assert.Equal(story.Id, spec.Id);
            using JsonDocument meta = JsonDocument.Parse(StoreBuilder.MetaJson(spec, story));
            Assert.Equal(story.Pages.Count, meta.RootElement.GetProperty("pageCount").GetInt32());
            Assert.Equal(StarterBuilder.StarterVersions[fileName], meta.RootElement.GetProperty("version").GetInt32());
        }
    }

    [Fact]
    public async Task APictureCover_IsStoredAsTheCoverPhoto_AndBecomesTheStoreCover()
    {
        var library = new FileStoryLibrary(_root);
        StoreStorySpec spec = StoreTexts.All[0];
        Story story = StoreBuilder.Build(spec);
        await library.AddAsync(story);
        string source = Path.Combine(_root, "source-cover.jpg");
        byte[] picture = [0xFF, 0xD8, 1, 2, 3, 0xFF, 0xD9];
        await File.WriteAllBytesAsync(source, picture);

        Story covered = await StarterBuilder.WithPictureCoverAsync(story, source, library);

        Assert.Equal(CoverStyle.Picture, covered.CoverStyle);
        Assert.Throws<InvalidOperationException>(() => StoreBuilder.CoverBytes(covered, spec, library));
        string thumb = Path.Combine(_root, "thumb.jpg");
        await File.WriteAllBytesAsync(thumb, [0xFF, 0xD8, 9, 0xFF, 0xD9]);
        Assert.Equal(await File.ReadAllBytesAsync(thumb), StoreBuilder.CoverBytes(covered, spec, library, thumb));
        Assert.Same(story, await StarterBuilder.WithPictureCoverAsync(story, null, library));
        Assert.Equal(CoverStyle.Colour, story.CoverStyle);
    }

    [Fact]
    public void PicturesOfTheWrongCount_FailTheBuild()
    {
        Story story = StoreBuilder.Build(StoreTexts.All[0]);
        string[] tooFew = Enumerable.Range(1, story.Pages.Count - 1).Select(i => $"{i}.jpg").ToArray();

        Assert.Throws<InvalidOperationException>(() => StarterBuilder.WithPictures(story, tooFew));
        Assert.All(StarterBuilder.WithPictures(story, Enumerable.Range(1, story.Pages.Count).Select(i => $"{i}.jpg").ToArray()).Pages, p => Assert.NotNull(p.ImageFile));
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
